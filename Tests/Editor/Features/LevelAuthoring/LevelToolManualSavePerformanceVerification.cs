using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using LevelTool;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LevelAuthoring.Editor
{
    public static class LevelToolManualSavePerformanceVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
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

        private static LevelToolScreen screen;
        private static ContentSnapshot source;
        private static string folder, recoveryFolder, report;
        private static bool previousEnabled;
        private static bool allocationCounterAvailable;
        private static EnterPlayModeOptions previousOptions;
        private static int phase, trial, unexpectedProjectWrites, recoveryWrites;
        private static volatile bool allowSave;
        private static long peakBytes;
        private static Timer watchdog;
        private static Stopwatch sampleWatch, saveWatch, idleWatch;
        private static FileSystemWatcher projectWatcher, recoveryWatcher;
        private static JObject sample;
        private static JArray operations;
        private static double next, previousTick, maximumGap;
        private static double coreEditMs;
        private static bool Busy => (bool)typeof(LevelToolScreen).GetField("busy", Flags).GetValue(screen);

        public static void Run()
        {
            long allocationStart = GC.GetAllocatedBytesForCurrentThread();
            var allocationProbe = new byte[65536]; GC.KeepAlive(allocationProbe);
            allocationCounterAvailable = GC.GetAllocatedBytesForCurrentThread() > allocationStart;
            folder = Path.GetFullPath("Logs/LevelToolManualSave/performance-" + Guid.NewGuid().ToString("N"));
            recoveryFolder = Path.Combine(folder, "recovery"); Directory.CreateDirectory(recoveryFolder);
            report = Path.Combine(folder, "measurements.jsonl");
            previousEnabled = EditorSettings.enterPlayModeOptionsEnabled; previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            LevelToolRecoveryIsolation.Configure(recoveryFolder);
            phase = 0; trial = 0;
            EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying) return;
                screen ??= UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                if (screen == null) return;
                double now = EditorApplication.timeSinceStartup;
                if (previousTick > 0) maximumGap = Math.Max(maximumGap, (now - previousTick) * 1000);
                previousTick = now;
                if (phase == 0)
                {
                    if (Busy) return;
                    LevelToolRecoveryIsolation.Verify(screen, recoveryFolder);
                    var prepare = Stopwatch.StartNew();
                    string input = "Logs/LevelToolRecoveryPerformance/stage01/fixed-ten-record.json";
                    var workspace = new AuthoringToolWorkspace();
                    workspace.RestoreState((string)JObject.Parse(File.ReadAllText(input))["workspace"]);
                    source = workspace.Session.CreateSnapshot();
                    if (source.Documents.Length != 38) throw new Exception("Expected fixed 38-document fixture");
                    File.AppendAllText(report, new JObject { ["kind"] = "preparation", ["restoreAndSnapshotMs"] = prepare.Elapsed.TotalMilliseconds,
                        ["documents"] = source.Documents.Length, ["sourceFingerprint"] = ContentSnapshotFingerprint.Compute(source),
                        ["allocationCounterAvailable"] = allocationCounterAvailable }.ToString(Formatting.None) + "\n");
                    recoveryWatcher = new FileSystemWatcher(recoveryFolder) { IncludeSubdirectories = true };
                    recoveryWatcher.Changed += (_, __) => Interlocked.Increment(ref recoveryWrites);
                    recoveryWatcher.Created += (_, __) => Interlocked.Increment(ref recoveryWrites);
                    recoveryWatcher.Deleted += (_, __) => Interlocked.Increment(ref recoveryWrites);
                    recoveryWatcher.Renamed += (_, __) => Interlocked.Increment(ref recoveryWrites);
                    recoveryWatcher.EnableRaisingEvents = true;
                    phase = 1;
                }
                if (phase == 1)
                {
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    ++trial; sampleWatch = Stopwatch.StartNew(); peakBytes = 0;
                    watchdog = new Timer(_ =>
                    {
                        if (!GetProcessMemoryInfo(new IntPtr(-1), out MemoryCounters memory, (uint)Marshal.SizeOf<MemoryCounters>()))
                        { File.WriteAllText(Path.Combine(folder, "protection.json"), "memory measurement failed"); TerminateProcess(new IntPtr(-1), 3); return; }
                        long bytes = (long)memory.WorkingSet.ToUInt64(); Interlocked.Exchange(ref peakBytes, Math.Max(Interlocked.Read(ref peakBytes), bytes));
                        if (sampleWatch.Elapsed.TotalSeconds < 30 && bytes < 8L * 1024 * 1024 * 1024) return;
                        File.WriteAllText(Path.Combine(folder, "protection.json"), new JObject { ["trial"] = trial, ["phase"] = phase,
                            ["seconds"] = sampleWatch.Elapsed.TotalSeconds, ["workingSetBytes"] = bytes }.ToString());
                        TerminateProcess(new IntPtr(-1), 2);
                    }, null, 0, 100);
                    sample = new JObject { ["trial"] = trial, ["temperature"] = trial == 1 ? "first" : "warm" };
                    operations = new JArray(); sample["operations"] = operations;
                    var setup = Stopwatch.StartNew();
                    screen.Workspace.Create(Path.Combine(folder, "sample-" + trial), source, "Discard");
                    Call("Refresh");
                    sample["createAndDisplayMs"] = setup.Elapsed.TotalMilliseconds;
                    File.AppendAllText(report, new JObject { ["kind"] = "sample-preparation", ["trial"] = trial,
                        ["createAndDisplayMs"] = setup.Elapsed.TotalMilliseconds }.ToString(Formatting.None) + "\n");
                    sample["managedBytesBefore"] = GC.GetTotalMemory(false);
                    projectWatcher = new FileSystemWatcher(screen.Workspace.Folder) { IncludeSubdirectories = true };
                    FileSystemEventHandler changed = (_, __) => { if (!allowSave) Interlocked.Increment(ref unexpectedProjectWrites); };
                    projectWatcher.Changed += changed; projectWatcher.Created += changed; projectWatcher.Deleted += changed;
                    projectWatcher.Renamed += (_, __) => { if (!allowSave) Interlocked.Increment(ref unexpectedProjectWrites); };
                    unexpectedProjectWrites = 0; projectWatcher.EnableRaisingEvents = true;
                    // 준비 비용과 정상 편집 표본의 30초 한도를 분리한다. 준비 자체도 위 watchdog으로 제한한다.
                    sampleWatch.Restart();
                    previousTick = 0; maximumGap = 0; phase = 2; next = now + .2; return;
                }
                if (now < next) return;
                if (phase >= 2 && phase < 12)
                {
                    int edit = phase - 1;
                    Measure("edit-" + edit, () => Call("Edit", (Action)(() =>
                    {
                        var core = Stopwatch.StartNew();
                        screen.Workspace.Session.EditLevel("측정", level => LevelDocumentEditing.SetName(level, "측정 편집 " + edit));
                        coreEditMs = core.Elapsed.TotalMilliseconds;
                    })));
                    if ((string)screen.Workspace.Session.Get(screen.Workspace.Session.SelectedLevelId).Data["displayName"] != "측정 편집 " + edit)
                        throw new Exception("Measured edit was not applied");
                    ++phase; return;
                }
                if (phase == 12) { Measure("undo", () => Command("Undo")); ++phase; return; }
                if (phase == 13)
                {
                    Measure("redo", () => Command("Redo"));
                    sample["editMaxEditorUpdateGapMs"] = maximumGap;
                    maximumGap = 0; previousTick = 0; idleWatch = Stopwatch.StartNew();
                    next = EditorApplication.timeSinceStartup + 1; ++phase; return;
                }
                if (phase == 14)
                {
                    if (unexpectedProjectWrites != 0 || recoveryWrites != 0) throw new Exception("Unexpected automatic file write before Save");
                    sample["idleObservedMs"] = idleWatch.Elapsed.TotalMilliseconds;
                    sample["idleMaxEditorUpdateGapMs"] = maximumGap;
                    maximumGap = 0; allowSave = true; saveWatch = Stopwatch.StartNew();
                    Measure("save-command", () => Command("Save")); phase = 15; return;
                }
                if (phase == 15)
                {
                    if (Busy) return;
                    sample["manualSaveCompletionMs"] = saveWatch.Elapsed.TotalMilliseconds;
                    sample["saveMaxEditorUpdateGapMs"] = maximumGap;
                    if (screen.Workspace.Session.IsDirty) throw new Exception("Manual save did not mark clean");
                    var verification = Stopwatch.StartNew();
                    var stored = new ContentSnapshotStore(screen.Workspace.Folder).Read();
                    if (ContentSnapshotFingerprint.Compute(stored.Snapshot) != ContentSnapshotFingerprint.Compute(screen.Workspace.Session.CreateSnapshot()))
                        throw new Exception("Saved JSON differs from current documents");
                    sample["postSaveVerificationMs"] = verification.Elapsed.TotalMilliseconds;
                    next = now + .3; phase = 16; return;
                }
                if (phase == 16)
                {
                    allowSave = false;
                    bool left = false; Measure("clean-leave", () => screen.RequestLeave(() => left = true));
                    if (!left) throw new Exception("Clean leave unexpectedly prompted");
                    Measure("disable", () => screen.enabled = false);
                    next = now + .3; phase = 17; return;
                }
                if (phase == 17)
                {
                    if (unexpectedProjectWrites != 0 || recoveryWrites != 0) throw new Exception("Automatic file writes during idle or clean leave");
                    sample["unexpectedProjectWrites"] = unexpectedProjectWrites; sample["recoveryWrites"] = recoveryWrites;
                    sample["managedBytesAfter"] = GC.GetTotalMemory(false); sample["peakWorkingSetBytes"] = Interlocked.Read(ref peakBytes);
                    sample["sampleTotalMs"] = sampleWatch.Elapsed.TotalMilliseconds; sample["status"] = "PASS";
                    watchdog.Dispose(); watchdog = null; projectWatcher.Dispose(); projectWatcher = null;
                    File.AppendAllText(report, sample.ToString(Formatting.None) + "\n");
                    Debug.Log("PASS manual save performance sample " + trial);
                    if (trial == 3) { Debug.Log("PASS manual save performance: " + report); Finish(0); return; }
                    screen.enabled = true; phase = 1; next = 0;
                }
            }
            catch (Exception error) { Debug.LogException(error); File.AppendAllText(report, "FAIL " + error + "\n"); Finish(1); }
        }

        private static void Measure(string label, Action action)
        {
            long allocated = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew(); action();
            var measured = new JObject { ["operation"] = label, ["synchronousMs"] = timer.Elapsed.TotalMilliseconds,
                ["mainThreadAllocatedBytes"] = allocationCounterAvailable ? (JToken)(GC.GetAllocatedBytesForCurrentThread() - allocated) : JValue.CreateNull() };
            if (label.StartsWith("edit-", StringComparison.Ordinal))
            {
                measured["coreEditMs"] = coreEditMs;
                measured["remainingUiAndCommandMs"] = (double)measured["synchronousMs"] - coreEditMs;
            }
            operations.Add(measured);
            File.AppendAllText(report, new JObject { ["kind"] = "operation", ["trial"] = trial, ["value"] = measured.DeepClone() }.ToString(Formatting.None) + "\n");
        }
        private static void Call(string method, params object[] args) => typeof(LevelToolScreen).GetMethod(method, Flags).Invoke(screen, args);
        private static void Command(string name) => Call("TryExecuteCommand", Enum.Parse(typeof(LevelToolScreen).GetNestedType("ToolCommandId", BindingFlags.NonPublic), name));
        public static void RunAllocationProbe()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var allocated = new byte[65536]; GC.KeepAlive(allocated);
            long delta = GC.GetAllocatedBytesForCurrentThread() - before;
            Debug.Log("Allocation counter probe: requested 65536 bytes, reported delta=" + delta +
                ". A zero result is unavailable instrumentation, not zero allocation.");
            EditorApplication.Exit(0);
        }
        private static void Finish(int code)
        {
            EditorApplication.update -= Tick; watchdog?.Dispose(); projectWatcher?.Dispose(); recoveryWatcher?.Dispose();
            if (screen != null) typeof(LevelToolScreen).GetField("busy", Flags).SetValue(screen, true);
            EditorSettings.enterPlayModeOptionsEnabled = previousEnabled; EditorSettings.enterPlayModeOptions = previousOptions;
            EditorApplication.Exit(code);
        }
    }
}
