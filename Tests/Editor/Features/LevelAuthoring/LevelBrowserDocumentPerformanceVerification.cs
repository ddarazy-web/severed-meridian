using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace LevelAuthoring.Editor
{
    // 정식 원본은 읽기만 하고, 모든 기록은 이번 검사 전용 폴더에 쓴다.
    public static partial class LevelBrowserDocumentPerformanceVerification
    {
        private static string report;
        private static int count, sample;
        private static bool allocationAvailable;
        private static System.Threading.Timer guard;
        private static volatile string activeOperation;
        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryCounters
        {
            public uint Size, PageFaults;
            public UIntPtr PeakWorkingSet, WorkingSet, PeakPaged, Paged, PeakNonPaged, NonPaged, Pagefile, PeakPagefile;
        }
        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetProcessMemoryInfo(IntPtr process, out MemoryCounters counters, uint size);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr process, uint code);
        public static void Run100() => Run(100);
        public static void Run1000() => Run(1000);
        public static void Run10000() => Run(10000);
        public static void RunOpen1000() => Run(1000, true);
        public static void RunPipeline100() => Run(100, false, true);
        public static void RunPipeline1000() => Run(1000, false, true);
        public static void RunPipeline10000() => Run(10000, false, true);

        private static void Run(int levelCount, bool openOnly = false, bool pipeline = false)
        {
            count = levelCount;
            long allocationProbe = GC.GetAllocatedBytesForCurrentThread();
            byte[] probe = new byte[65536]; GC.KeepAlive(probe);
            allocationAvailable = GC.GetAllocatedBytesForCurrentThread() > allocationProbe;
            string root = Path.GetFullPath("Logs/LevelBrowserDocuments");
            string owned = Path.Combine(root, "run-" + Guid.NewGuid().ToString("N"));
            string fixture = Path.Combine(owned, "data");
            Directory.CreateDirectory(owned);
            report = Path.Combine(owned, "measurements.jsonl");
            guard = new System.Threading.Timer(_ =>
            {
                if (!GetProcessMemoryInfo(new IntPtr(-1), out MemoryCounters counters, (uint)Marshal.SizeOf<MemoryCounters>()))
                { File.WriteAllText(Path.Combine(owned, "protection.json"), "Native memory counter unavailable"); TerminateProcess(new IntPtr(-1), 3); return; }
                long memory = (long)counters.WorkingSet.ToUInt64();
                if (memory < 6L * 1024 * 1024 * 1024) return;
                File.WriteAllText(Path.Combine(owned, "protection.json"), new JObject { ["workingSetBytes"] = memory,
                    ["operation"] = activeOperation, ["reason"] = "6 GiB test memory limit" }.ToString());
                TerminateProcess(new IntPtr(-1), 3);
            }, null, 1000, 1000);
            int exit = 1;
            try
            {
                JObject selection = JObject.Parse(File.ReadAllText("ProjectSettings/AuthoringSource.json"));
                string sourcePath = Path.GetFullPath(Path.Combine("ProjectSettings", (string)selection["folder"]));
                var sourceStore = new ContentSnapshotStore(sourcePath);
                StoredContentSnapshot source = sourceStore.Read();
                ContentDocument[] original = source.Snapshot.Documents;
                ContentDocument[] templates = original.Where(doc => doc.Kind == "level").OrderBy(doc => (int)doc.Data["levelNumber"]).ToArray();
                if (templates.Length == 0) throw new Exception("No real source levels");
                var store = new ContentSnapshotStore(fixture);
                sample = -1;
                Measure("fixture-stream-write", () =>
                {
                    var files = new JsonContentStore(fixture);
                    string generation = "snapshots/" + Guid.NewGuid().ToString("N");
                    var entries = new JArray();
                    foreach (ContentDocument document in original.Where(doc => doc.Kind != "level" && doc.Kind != "project"))
                    {
                        string path = generation + "/" + entries.Count.ToString("D5") + ".json";
                        files.Save(document, path, null); entries.Add(new JObject { ["id"] = document.Id, ["path"] = path });
                    }
                    for (int i = 0; i < count; i++)
                    {
                        var document = new ContentDocument("level", "measure-level-" + (i + 1), templates[i % templates.Length].Data);
                        document.Data["levelNumber"] = i + 1; document.Data["displayName"] = "대량 검사 " + (i + 1);
                        string path = generation + "/" + entries.Count.ToString("D5") + ".json";
                        files.Save(document, path, null); entries.Add(new JObject { ["id"] = document.Id, ["path"] = path });
                    }
                    ContentDocument project = original.Single(doc => doc.Kind == "project");
                    project.Data["documents"] = entries;
                    files.Save(project, "project.json", null);
                });
                File.AppendAllText(report, new JObject { ["kind"] = "fixture", ["levels"] = count, ["sourceHash"] = source.Hash,
                    ["scope"] = openOnly ? "read-session-switch-edit diagnostic" : pipeline ? "streamed full pipeline, no UI" : "full baseline",
                    ["sourceLevels"] = templates.Length, ["documents"] = original.Count(doc => doc.Kind != "level") + count,
                    ["diskBytes"] = Directory.GetFiles(fixture, "*.json", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length),
                    ["unity"] = UnityEngine.Application.unityVersion, ["runtime"] = Environment.Version.ToString(),
                    ["os"] = Environment.OSVersion.ToString(), ["cpuCount"] = Environment.ProcessorCount,
                    ["boardCells"] = templates.Sum(doc => ((JArray)doc.Data["board"]["cells"]).Count),
                    ["elements"] = templates.Sum(doc => (doc.Data["elements"] as JArray)?.Count ?? 0)
                }.ToString(Formatting.None) + "\n");
                original = null; templates = null; source = null;
                for (sample = 0; sample < 4; sample++)
                {
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    StoredContentSnapshot loaded = null;
                    // 공개 세대만 측정한다. 이전 저장 세대가 쌓여도 읽기 범위는 바뀌지 않는다.
                    if (!openOnly && !pipeline)
                    {
                        ContentDocument manifest = ContentJson.Read(File.ReadAllText(Path.Combine(fixture, "project.json")));
                        string[] paths = ((JArray)manifest.Data["documents"]).Select(entry => Path.Combine(fixture, (string)entry["path"])).ToArray();
                        string[] texts = null;
                        Measure("file-read", () => texts = paths.Select(File.ReadAllText).ToArray());
                        ContentDocument[] parsed = null;
                        Measure("json-parse-and-document-validation", () => parsed = texts.Select(ContentJson.Read).ToArray());
                        Measure("snapshot-copy-and-reference-validation", () => new ContentSnapshot(parsed.Concat(new[] { manifest })));
                        texts = null; parsed = null;
                    }
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    Measure("store-read-total", () => loaded = store.Read());
                    AuthoringEditSession session = null;
                    Measure("session-create", () => session = new AuthoringEditSession(loaded, "measure-level-1"));
                    loaded = null;
                    Measure("select-level", () => session.SelectLevel("measure-level-" + count));
                    Measure("edit-one-level", () => session.EditLevel("측정", data => data["displayName"] = "수정 " + sample));
                    if (openOnly) { session = null; continue; }
                    if (pipeline)
                    {
                        string recoveryFile = Path.Combine(fixture, "session-recovery.json");
                        Measure("recovery-export", () => { using var output = new StreamWriter(recoveryFile, false, new System.Text.UTF8Encoding(false)); session.WriteState(output); });
                        File.AppendAllText(report, new JObject { ["kind"] = "recovery-size", ["sample"] = sample, ["bytes"] = new FileInfo(recoveryFile).Length }.ToString(Formatting.None) + "\n");
                        Measure("recovery-restore", () => { using var input = new StreamReader(recoveryFile); AuthoringEditSession.Restore(input); });
                    }
                    else
                    {
                        string recovery = null;
                        Measure("recovery-export", () => recovery = session.ExportState());
                        Measure("recovery-restore", () => AuthoringEditSession.Restore(recovery));
                        recovery = null;
                    }
                    Measure("save-total", () => session.Save(store));
                    session = null;
                }
                File.WriteAllText(Path.Combine(owned, "result.txt"), openOnly ?
                    "PASS read/session/switch/edit diagnostic only; first=0, normal=1..3. Save, recovery and UI remain unmeasured." :
                    "PASS " + (pipeline ? "streamed pipeline" : "baseline") + " measurements completed; first sample=0, normal samples=1..3. No UI timing included.");
                exit = 0;
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(owned, "result.txt"), "FAIL " + error);
                UnityEngine.Debug.LogException(error);
            }
            finally
            {
                guard?.Dispose();
                // 생성한 data 하위만 정리한다. 결과와 실제 제작 원본은 보존한다.
                if (Path.GetFullPath(fixture).StartsWith(owned + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fixture))
                    Directory.Delete(fixture, true);
            }
            EditorApplication.Exit(exit);
        }

        private static void Measure(string operation, Action action)
        {
            activeOperation = operation;
            File.AppendAllText(report, new JObject { ["kind"] = "start", ["sample"] = sample, ["operation"] = operation }.ToString(Formatting.None) + "\n");
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long managed = GC.GetTotalMemory(false);
            using var allocationRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            bool counterAvailable = allocationRecorder.Valid && allocationRecorder.UnitType.ToString() == "Bytes";
            long counterBefore = counterAvailable ? allocationRecorder.CurrentValue : 0;
            var watch = Stopwatch.StartNew();
            action(); watch.Stop();
            long counterBytes = counterAvailable ? allocationRecorder.CurrentValue - counterBefore : -1;
            long? allocatedBytes = allocationAvailable ? GC.GetAllocatedBytesForCurrentThread() - allocated :
                counterAvailable && counterBytes >= 0 ? counterBytes : (long?)null;
            bool nativeMemory = GetProcessMemoryInfo(new IntPtr(-1), out MemoryCounters memory, (uint)Marshal.SizeOf<MemoryCounters>());
            File.AppendAllText(report, new JObject { ["kind"] = "measurement", ["levels"] = count, ["sample"] = sample,
                ["operation"] = operation, ["ms"] = watch.Elapsed.TotalMilliseconds,
                ["allocatedBytes"] = allocatedBytes.HasValue ? (JToken)allocatedBytes.Value : JValue.CreateNull(),
                ["allocationSource"] = allocationAvailable ? "GC.GetAllocatedBytesForCurrentThread" :
                    allocatedBytes.HasValue ? "Unity GC Allocated In Frame counter delta; frame scope, includes measurement overhead" : "unavailable",
                ["managedBefore"] = managed, ["managedAfter"] = GC.GetTotalMemory(false),
                ["workingSet"] = nativeMemory ? (JToken)(long)memory.WorkingSet.ToUInt64() : JValue.CreateNull() }.ToString(Formatting.None) + "\n");
        }

        public static void RunAllocationProbe()
        {
            string root = Path.GetFullPath("Logs/LevelBrowserDocuments/allocation-probe-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var results = new JArray();
            try
            {
                GC.KeepAlive(new byte[65536]);
                foreach (bool sumInFrame in new[] { false, true })
                {
                    var options = Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
                    if (sumInFrame) options |= Unity.Profiling.ProfilerRecorderOptions.SumAllSamplesInFrame;
                    using var recorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC.Alloc", 32, options);
                    GC.KeepAlive(new byte[65536]); GC.KeepAlive(new byte[32768]);
                    long current = recorder.Valid ? recorder.CurrentValue : -1;
                    recorder.Stop();
                    long total = 0;
                    if (recorder.Valid) for (int index = 0; index < recorder.Count; index++) total += recorder.GetSample(index).Value;
                    results.Add(new JObject { ["sumInFrame"] = sumInFrame, ["valid"] = recorder.Valid,
                        ["unit"] = recorder.Valid ? recorder.UnitType.ToString() : "unavailable", ["current"] = current,
                        ["samples"] = recorder.Valid ? recorder.Count : 0, ["total"] = total,
                        ["wrapped"] = recorder.Valid && recorder.WrappedAround, ["payloadBytes"] = 98304 });
                }
                var handles = new System.Collections.Generic.List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
                Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
                foreach (var handle in handles)
                {
                    var description = Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(handle);
                    if (!description.Name.Contains("GC") || description.UnitType.ToString() != "Bytes") continue;
                    using var recorder = new Unity.Profiling.ProfilerRecorder(handle, 32, Unity.Profiling.ProfilerRecorderOptions.StartImmediately);
                    long before = recorder.CurrentValue;
                    GC.KeepAlive(new byte[65536]); GC.KeepAlive(new byte[32768]);
                    long after = recorder.CurrentValue; recorder.Stop();
                    results.Add(new JObject { ["counter"] = description.Name, ["unit"] = description.UnitType.ToString(),
                        ["before"] = before, ["after"] = after, ["difference"] = after - before, ["payloadBytes"] = 98304 });
                }
                File.WriteAllText(Path.Combine(root, "result.json"), results.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(root, "error.txt"), error.ToString()); UnityEngine.Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
