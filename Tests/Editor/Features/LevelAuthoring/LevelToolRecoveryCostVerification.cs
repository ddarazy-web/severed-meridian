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
        private static void Measure(bool small)
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
                        timer.Restart(); JObject record = store.Read(); double readMs = timer.Elapsed.TotalMilliseconds;
                        Volatile.Write(ref phase, "restore");
                        timer.Restart(); var restored = new AuthoringToolWorkspace(); restored.RestoreState((string)record["workspace"]);
                        double restoreMs = timer.Elapsed.TotalMilliseconds;
                        if (state != restored.ExportState() || !restored.Session.SelectedCells.SequenceEqual(new[] { 10, 11 }))
                            throw new Exception("복구 후 문서/선택/Undo/Redo 불일치");
                        workspace.Session.Redo(); restored.Session.Redo();
                        if (workspace.ExportState() != restored.ExportState()) throw new Exception("복구 Redo 불일치");
                        workspace.Session.Undo(); restored.Session.Undo();
                        if (workspace.ExportState() != restored.ExportState()) throw new Exception("복구 Undo 불일치");
                        JObject history = JObject.Parse(state)["session"] as JObject;
                        File.AppendAllText(report, new JObject { ["edits"] = count, ["status"] = "PASS",
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
