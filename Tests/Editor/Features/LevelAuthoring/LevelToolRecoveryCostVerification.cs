using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace LevelAuthoring.Editor
{
    public static class LevelToolRecoveryCostVerification
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryCounters
        {
            public uint Size, PageFaults;
            public UIntPtr PeakWorkingSet, WorkingSet, PeakPaged, Paged, PeakNonPaged, NonPaged, Pagefile, PeakPagefile;
        }
        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetProcessMemoryInfo(IntPtr process, out MemoryCounters counters, uint size);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr process, uint exitCode);
        public static void Run() => Measure(false);
        public static void RunSmall() => Measure(true);
        public static void RunPreparedSeries() => Measure(false, true);
        // 이전 보호 종료 표본의 정확히 같은 v1 입력으로 저장/읽기/적용 비용을 비교한다.
        public static void RunFixedTen() => MeasureFixedTen(true);
        public static void RunFixedTenHistory() => MeasureFixedTen(false);
        public static void RunFixedTenParts() => MeasureFixedTen(false, true);
        public static void RunFixedTenCodec() => MeasureFixedTen(false, false, true);
        public static void RunFixedTenRepeated()
        {
            try
            {
                for (int trial = 1; trial <= 3; trial++)
                {
                    // 앞 표본의 폐기 가능한 객체만 회수하며 각 표본의 실행 중 최대 메모리는 그대로 측정한다.
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    Debug.Log("Recovery repeat " + trial + (trial == 1 ? " first" : " warm"));
                    MeasureFixedTen(true, exit: false);
                }
                Debug.Log("PASS three fixed-ten recovery samples"); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void MeasureFixedTen(bool write, bool parts = false, bool codec = false, bool exit = true)
        {
            string input = Path.GetFullPath("Logs/LevelToolRecoveryPerformance/stage01/fixed-ten-record.json");
            string root = Path.GetFullPath("Logs/LevelToolRecoveryPerformance/fixed-ten-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string report = Path.Combine(root, "measurements.jsonl");
            string phase = "load";
            if (!GetProcessMemoryInfo(new IntPtr(-1), out MemoryCounters initial, (uint)Marshal.SizeOf<MemoryCounters>()))
                throw new Exception("Initial memory measurement failed");
            long startBytes = (long)initial.WorkingSet.ToUInt64();
            long peak = startBytes;
            var total = Stopwatch.StartNew();
            using (var watchdog = new Timer(_ =>
            {
                if (!GetProcessMemoryInfo(new IntPtr(-1), out MemoryCounters counters, (uint)Marshal.SizeOf<MemoryCounters>()))
                { File.AppendAllText(report, "FAIL memory measurement\n"); TerminateProcess(new IntPtr(-1), 3); return; }
                long bytes = (long)counters.WorkingSet.ToUInt64();
                Interlocked.Exchange(ref peak, Math.Max(Interlocked.Read(ref peak), bytes));
                if (total.Elapsed.TotalSeconds < 30 && bytes < 8L * 1024 * 1024 * 1024) return;
                File.AppendAllText(report, new JObject { ["status"] = "PROTECTION_LIMIT", ["phase"] = Volatile.Read(ref phase),
                    ["seconds"] = total.Elapsed.TotalSeconds, ["workingSetBytes"] = bytes }.ToString(Newtonsoft.Json.Formatting.None) + "\n");
                TerminateProcess(new IntPtr(-1), 2);
            }, null, 0, 100))
            try
            {
                JObject inputRecord = JObject.Parse(File.ReadAllText(input));
                string state = (string)inputRecord["workspace"];
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    File.AppendAllText(report, new JObject { ["inputSHA256"] = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(input))).Replace("-", ""),
                        ["stateBytes"] = System.Text.Encoding.UTF8.GetByteCount(state) }.ToString(Newtonsoft.Json.Formatting.None) + "\n");
                if (codec)
                {
                    var timings = new JObject(); var watch = Stopwatch.StartNew();
                    Volatile.Write(ref phase, "workspace-parse");
                    JObject workspaceObject = JObject.Parse(state);
                    timings["workspaceParseMs"] = watch.Elapsed.TotalMilliseconds;
                    Volatile.Write(ref phase, "removed-session-string-roundtrip"); watch.Restart();
                    string sessionText = workspaceObject["session"].ToString(Newtonsoft.Json.Formatting.None);
                    timings["removedSessionStringMs"] = watch.Elapsed.TotalMilliseconds;
                    watch.Restart(); JObject.Parse(sessionText);
                    timings["removedSessionParseMs"] = watch.Elapsed.TotalMilliseconds;
                    Volatile.Write(ref phase, "session-decode"); watch.Restart();
                    var decode = typeof(AuthoringEditSession).GetMethod("RestoreObject", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                    var session = (AuthoringEditSession)decode.Invoke(null, new object[] { workspaceObject["session"] });
                    timings["sessionDecodeMs"] = watch.Elapsed.TotalMilliseconds;
                    Volatile.Write(ref phase, "session-encode"); watch.Restart();
                    string encoded = session.ExportState(); timings["sessionEncodeMs"] = watch.Elapsed.TotalMilliseconds;
                    if (encoded != sessionText) throw new Exception("Session v1 형식 불일치");
                    ((JArray)workspaceObject["session"]["current"]["documents"]).First(doc => (string)doc["id"] == session.SelectedLevelId)["data"]["displayName"] = "external-mutated";
                    if (session.ExportState() != encoded) throw new Exception("외부 JSON 참조 오염");
                    Volatile.Write(ref phase, "workspace-decode"); watch.Restart();
                    var decodedWorkspace = new AuthoringToolWorkspace(); decodedWorkspace.RestoreState(state);
                    timings["workspaceDecodeMs"] = watch.Elapsed.TotalMilliseconds;
                    Volatile.Write(ref phase, "workspace-encode"); watch.Restart();
                    string workspaceText = decodedWorkspace.ExportState(); timings["workspaceEncodeMs"] = watch.Elapsed.TotalMilliseconds;
                    if (workspaceText != state) throw new Exception("Workspace v1 형식 불일치");
                    timings["status"] = "CODEC_PASS"; timings["totalSeconds"] = total.Elapsed.TotalSeconds;
                    timings["peakWorkingSetBytes"] = Interlocked.Read(ref peak);
                    File.AppendAllText(report, timings.ToString(Newtonsoft.Json.Formatting.None) + "\n");
                    Debug.Log("PASS isolated v1 codec costs: " + report); EditorApplication.Exit(0); return;
                }
                if (parts)
                {
                    byte[] bytes = new System.Text.UTF8Encoding(false).GetBytes(inputRecord.ToString(Newtonsoft.Json.Formatting.None));
                    string temporary = Path.Combine(root, "part-draft.json");
                    var validate = typeof(ToolDraftStore).GetMethod("Validate", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                    Action<string, Action> measure = (label, operation) =>
                    {
                        Volatile.Write(ref phase, label); var watch = Stopwatch.StartNew(); operation();
                        File.AppendAllText(report, new JObject { ["phase"] = label, ["ms"] = watch.Elapsed.TotalMilliseconds }.ToString(Newtonsoft.Json.Formatting.None) + "\n");
                    };
                    measure("input-validation", () => validate.Invoke(null, new object[] { inputRecord }));
                    measure("temporary-write-flush", () =>
                    {
                        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                    });
                    measure("temporary-byte-validation", () =>
                    { if (!bytes.SequenceEqual(File.ReadAllBytes(temporary))) throw new Exception("임시 바이트 불일치"); });
                    measure("existing-record-validation", () => validate.Invoke(null, new object[] { JObject.Parse(File.ReadAllText(temporary)) }));
                    File.AppendAllText(report, new JObject { ["status"] = "PARTS_PASS", ["totalSeconds"] = total.Elapsed.TotalSeconds,
                        ["peakWorkingSetBytes"] = Interlocked.Read(ref peak) }.ToString(Newtonsoft.Json.Formatting.None) + "\n");
                    Debug.Log("PASS isolated recovery phase costs: " + report); EditorApplication.Exit(0); return;
                }
                var store = new ToolDraftStore(root);
                Volatile.Write(ref phase, write ? "save" : "fixture-copy"); var timer = Stopwatch.StartNew();
                if (write) store.Save(state, (JObject)inputRecord["view"], 1);
                else File.Copy(input, Path.Combine(root, "draft.json"));
                File.AppendAllText(report, new JObject { ["phase"] = phase, ["ms"] = timer.Elapsed.TotalMilliseconds }.ToString(Newtonsoft.Json.Formatting.None) + "\n");
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                Volatile.Write(ref phase, "read"); timer.Restart();
                object prepared = typeof(ToolDraftStore).GetMethod("ReadPrepared", flags).Invoke(store, null);
                File.AppendAllText(report, new JObject { ["phase"] = phase, ["ms"] = timer.Elapsed.TotalMilliseconds }.ToString(Newtonsoft.Json.Formatting.None) + "\n");
                Volatile.Write(ref phase, "apply"); timer.Restart();
                var restored = new AuthoringToolWorkspace();
                prepared.GetType().GetMethod("ApplyTo", flags).Invoke(prepared, new object[] { restored, false });
                File.AppendAllText(report, new JObject { ["phase"] = phase, ["ms"] = timer.Elapsed.TotalMilliseconds }.ToString(Newtonsoft.Json.Formatting.None) + "\n");
                Volatile.Write(ref phase, "verify-state");
                timer.Restart(); string exported = restored.ExportState();
                File.AppendAllText(report, new JObject { ["phase"] = "export", ["ms"] = timer.Elapsed.TotalMilliseconds }.ToString(Newtonsoft.Json.Formatting.None) + "\n");
                if (exported != state) throw new Exception("전체 복구 상태 불일치");
                Volatile.Write(ref phase, "verify-history");
                timer.Restart();
                restored.Session.Redo(); restored.Session.Undo();
                if (restored.ExportState() != state) throw new Exception("복구 Redo/Undo 불일치");
                File.AppendAllText(report, new JObject { ["status"] = "PASS", ["totalSeconds"] = total.Elapsed.TotalSeconds,
                    ["historyVerificationMs"] = timer.Elapsed.TotalMilliseconds,
                    ["startingWorkingSetBytes"] = startBytes, ["workingSetDeltaBytes"] = Interlocked.Read(ref peak) - startBytes,
                    ["peakWorkingSetBytes"] = Interlocked.Read(ref peak), ["draftBytes"] = new FileInfo(Path.Combine(root, "draft.json")).Length
                }.ToString(Newtonsoft.Json.Formatting.None) + "\n");
                Debug.Log("PASS fixed ten recovery: " + report); if (exit) EditorApplication.Exit(0);
            }
            catch (Exception error) { File.AppendAllText(report, "FAIL " + error + "\n"); if (!exit) throw; Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void Measure(bool small, bool preparedPath = false)
        {
            string root = Path.GetFullPath("Logs/LevelToolRefactorStage04/recovery-cost-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string report = Path.Combine(root, "measurements.jsonl");
            try
            {
                // 사용자 원본은 읽기만 하고 각 표본의 저장·복구 파일은 별도 폴더에 둔다.
                var source = small ? LevelTool.LevelToolDocuments.NewProject() : new ContentSnapshotStore(Path.GetFullPath("ContentData/Candidates/main")).Read().Snapshot;
                foreach (int count in small ? new[] { 10 } : new[] { 10, 30, 60 })
                {
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    string folder = Path.Combine(root, count.ToString());
                    var workspace = new AuthoringToolWorkspace();
                    workspace.Create(Path.Combine(folder, "project"), source);
                    workspace.Session.SelectCells(new[] { 10, 11 });
                    var total = Stopwatch.StartNew();
                    long peak = 0;
                    string phase = "editing";
                    // Unity가 메인 스레드에서 멈춰도 이 검사 프로세스만 보호 한도로 종료한다.
                    using (var watchdog = new Timer(_ =>
                    {
                        if (GetProcessMemoryInfo(new IntPtr(-1), out MemoryCounters memoryInfo, (uint)Marshal.SizeOf<MemoryCounters>()))
                        {
                            long memory = (long)memoryInfo.WorkingSet.ToUInt64();
                            Interlocked.Exchange(ref peak, Math.Max(Interlocked.Read(ref peak), memory));
                            if (total.Elapsed.TotalSeconds < 30 && memory < 8L * 1024 * 1024 * 1024) return;
                            File.AppendAllText(report, new JObject { ["edits"] = count, ["status"] = "PROTECTION_LIMIT",
                                ["phase"] = Volatile.Read(ref phase), ["seconds"] = total.Elapsed.TotalSeconds,
                                ["workingSetBytes"] = memory }.ToString(Newtonsoft.Json.Formatting.None) + "\n");
                            TerminateProcess(new IntPtr(-1), 2);
                        }
                        else
                        {
                            File.AppendAllText(report, "FAIL native process memory measurement unavailable\n");
                            TerminateProcess(new IntPtr(-1), 3);
                        }
                    }, null, 0, 100))
                    {
                        File.AppendAllText(report, new JObject { ["edits"] = count, ["status"] = "START", ["documents"] = source.Documents.Length }.ToString(Newtonsoft.Json.Formatting.None) + "\n");
                        for (int i = 0; i < count; i++)
                        {
                            int value = i;
                            workspace.Session.EditLevel("복구 비용 표본", level => LevelDocumentEditing.SetName(level, "측정 " + value));
                        }
                        workspace.Session.Undo();
                        Volatile.Write(ref phase, "export");
                        var timer = Stopwatch.StartNew();
                        string state = workspace.ExportState();
                        double exportMs = timer.Elapsed.TotalMilliseconds;
                        var store = new ToolDraftStore(folder);
                        File.AppendAllText(report, new JObject { ["edits"] = count, ["phase"] = "export", ["ms"] = exportMs,
                            ["stateBytes"] = System.Text.Encoding.UTF8.GetByteCount(state) }.ToString(Newtonsoft.Json.Formatting.None) + "\n");
                        Volatile.Write(ref phase, "save");
                        timer.Restart(); store.Save(state, new JObject(), 1); double saveMs = timer.Elapsed.TotalMilliseconds;
                        File.AppendAllText(report, new JObject { ["edits"] = count, ["phase"] = "save", ["ms"] = saveMs }.ToString(Newtonsoft.Json.Formatting.None) + "\n");
                        Volatile.Write(ref phase, "read");
                        timer.Restart();
                        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                        object prepared = preparedPath ? typeof(ToolDraftStore).GetMethod("ReadPrepared", flags).Invoke(store, null) : null;
                        JObject record = preparedPath ? null : store.Read();
                        double readMs = timer.Elapsed.TotalMilliseconds;
                        Volatile.Write(ref phase, "restore");
                        timer.Restart(); var restored = new AuthoringToolWorkspace();
                        if (preparedPath) prepared.GetType().GetMethod("ApplyTo", flags).Invoke(prepared, new object[] { restored, false });
                        else restored.RestoreState((string)record["workspace"]);
                        double restoreMs = timer.Elapsed.TotalMilliseconds;
                        if (state != restored.ExportState() || !restored.Session.SelectedCells.SequenceEqual(new[] { 10, 11 }))
                            throw new Exception("복구 후 문서/선택/Undo/Redo 불일치");
                        workspace.Session.Redo(); restored.Session.Redo();
                        if (workspace.ExportState() != restored.ExportState()) throw new Exception("복구 Redo 불일치");
                        workspace.Session.Undo(); restored.Session.Undo();
                        if (workspace.ExportState() != restored.ExportState()) throw new Exception("복구 Undo 불일치");
                        JObject history = JObject.Parse(state)["session"] as JObject;
                        File.AppendAllText(report, new JObject { ["edits"] = count, ["status"] = "PASS", ["preparedPath"] = preparedPath,
                            ["documents"] = source.Documents.Length, ["undo"] = ((JArray)history["undo"]).Count,
                            ["redo"] = ((JArray)history["redo"]).Count, ["draftBytes"] = new FileInfo(Path.Combine(folder, "draft.json")).Length,
                            ["exportMs"] = exportMs, ["saveMs"] = saveMs, ["readMs"] = readMs, ["restoreMs"] = restoreMs,
                            ["totalSeconds"] = total.Elapsed.TotalSeconds, ["peakWorkingSetBytes"] = Interlocked.Read(ref peak)
                        }.ToString(Newtonsoft.Json.Formatting.None) + "\n");
                    }
                }
                Debug.Log("PASS recovery cost and restored history: " + report); EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                File.AppendAllText(report, "FAIL " + error + "\n"); Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
    }
}
