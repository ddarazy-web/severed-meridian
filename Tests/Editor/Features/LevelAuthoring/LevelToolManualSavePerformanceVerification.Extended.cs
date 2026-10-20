using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using LevelTool;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace LevelAuthoring.Editor
{
    public static partial class LevelToolManualSavePerformanceVerification
    {
        private sealed class Scenario
        {
            internal string Name;
            internal Action Prepare, Action, Verify;
        }
        private static readonly List<Scenario> scenarios = new List<Scenario>();
        private static readonly List<WeakReference> releasedReferences = new List<WeakReference>();
        private static int extendedPhase, scenarioIndex, readyTicks, repeatIndex;
        private static string measuredLevel, baselineName, normalDefinition, sharedFlow;
        private static double extendedDeadline;
        private static Stopwatch readyWatch;
        private static JObject pendingMeasurement;
        private static Scenario activeScenario;
        private static Dictionary<string, JObject> measuredDefinitions;
        private static ElementSelectionEditing measuredClipboard;
        private static SharedTutorialDraft measuredShared;
        private static VisualElement preservedBoard, preservedMaterial, preservedInput;
        private static Vector2 preservedScroll;
        private static long unchangedVersion;
        private static bool focusedRefreshOnly;
        private static AuthoringEditSession EditSession => screen.Workspace.Session;
        private static VisualElement ExtendedRoot => screen.GetComponent<UIDocument>().rootVisualElement;
        private static object Field(string name) => typeof(LevelToolScreen).GetField(name, Flags).GetValue(screen);
        private static void SetField(string name, object value) => typeof(LevelToolScreen).GetField(name, Flags).SetValue(screen, value);

        public static void RunRepresentative()
        {
            focusedRefreshOnly = false; BeginRepresentative();
        }
        public static void RunFocusedRefresh()
        {
            focusedRefreshOnly = true; BeginRepresentative();
        }
        private static void BeginRepresentative()
        {
            folder = Path.GetFullPath("Logs/LevelToolRefreshPerformance/extended-" + Guid.NewGuid().ToString("N"));
            recoveryFolder = Path.Combine(folder, "recovery"); Directory.CreateDirectory(recoveryFolder);
            report = Path.Combine(folder, "measurements.jsonl");
            previousEnabled = EditorSettings.enterPlayModeOptionsEnabled; previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            LevelToolRecoveryIsolation.Configure(recoveryFolder);
            extendedDeadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update += ExtendedTick; EditorApplication.EnterPlaymode();
        }

        private static void ExtendedTick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > extendedDeadline) throw new Exception("Extended verification timeout");
                if (!EditorApplication.isPlaying) return;
                screen ??= UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                if (screen == null || Busy) return;
                if (extendedPhase == 0)
                {
                    LevelToolRecoveryIsolation.Verify(screen, recoveryFolder);
                    sampleWatch = Stopwatch.StartNew();
                    watchdog = new Timer(_ =>
                    {
                        if (!GetProcessMemoryInfo(new IntPtr(-1), out MemoryCounters memory, (uint)Marshal.SizeOf<MemoryCounters>()))
                        { File.WriteAllText(Path.Combine(folder, "protection.json"), "memory measurement failed"); TerminateProcess(new IntPtr(-1), 3); return; }
                        long bytes = (long)memory.WorkingSet.ToUInt64();
                        Interlocked.Exchange(ref peakBytes, Math.Max(Interlocked.Read(ref peakBytes), bytes));
                        if (sampleWatch.Elapsed.TotalSeconds < 30 && bytes < 8L * 1024 * 1024 * 1024) return;
                        File.WriteAllText(Path.Combine(folder, "protection.json"), new JObject
                        { ["phase"] = extendedPhase, ["seconds"] = sampleWatch.Elapsed.TotalSeconds, ["bytes"] = bytes }.ToString());
                        TerminateProcess(new IntPtr(-1), 2);
                    }, null, 0, 100);
                    var restored = new AuthoringToolWorkspace();
                    restored.RestoreState((string)JObject.Parse(File.ReadAllText("Logs/LevelToolRecoveryPerformance/stage01/fixed-ten-record.json"))["workspace"]);
                    source = restored.Session.CreateSnapshot();
                    measuredLevel = restored.Session.SelectedLevelId;
                    if (source.Documents.Length != 38) throw new Exception("Expected 38 documents");
                    screen.Workspace.Create(Path.Combine(folder, "project"), source);
                    EditSession.SelectLevel(measuredLevel); Call("Refresh");
                    baselineName = (string)EditSession.Get(measuredLevel).Data["displayName"];
                    measuredDefinitions = LevelToolDocuments.Definitions(EditSession);
                    normalDefinition = measuredDefinitions.First(pair => (string)pair.Value["supply"]?["behavior"] == "FixedNormal").Key;
                    projectWatcher = new FileSystemWatcher(screen.Workspace.Folder) { IncludeSubdirectories = true };
                    recoveryWatcher = new FileSystemWatcher(recoveryFolder) { IncludeSubdirectories = true };
                    projectWatcher.Changed += (_, __) => Interlocked.Increment(ref unexpectedProjectWrites);
                    projectWatcher.Created += (_, __) => Interlocked.Increment(ref unexpectedProjectWrites);
                    projectWatcher.Deleted += (_, __) => Interlocked.Increment(ref unexpectedProjectWrites);
                    projectWatcher.Renamed += (_, __) => Interlocked.Increment(ref unexpectedProjectWrites);
                    recoveryWatcher.Changed += (_, __) => Interlocked.Increment(ref recoveryWrites);
                    recoveryWatcher.Created += (_, __) => Interlocked.Increment(ref recoveryWrites);
                    recoveryWatcher.Deleted += (_, __) => Interlocked.Increment(ref recoveryWrites);
                    recoveryWatcher.Renamed += (_, __) => Interlocked.Increment(ref recoveryWrites);
                    projectWatcher.EnableRaisingEvents = recoveryWatcher.EnableRaisingEvents = true;
                    WriteExtended(new JObject { ["kind"] = "input", ["documents"] = 38,
                        ["fingerprint"] = ContentSnapshotFingerprint.Compute(source), ["mainThreadAllocatedBytes"] = JValue.CreateNull(),
                        ["readyDefinition"] = "two subsequent Editor updates with attached panel, valid board geometry and current display document; not GPU completion" });
                    BuildScenarios();
                    if (focusedRefreshOnly) scenarios.RemoveAll(value => value.Name != "focus-scroll-preservation");
                    extendedPhase = 1; sampleWatch.Restart(); return;
                }
                if (extendedPhase == 1)
                {
                    if (scenarioIndex == scenarios.Count)
                    {
                        if (focusedRefreshOnly)
                        {
                            ExtendedCheck(unexpectedProjectWrites == 0 && recoveryWrites == 0, "focused refresh has zero automatic writes");
                            WriteExtended(new JObject { ["kind"] = "complete", ["status"] = "PASS", ["focusedRefreshOnly"] = true });
                            ExtendedFinish(0); return;
                        }
                        ResetExtended(); SetField("levelSettingsVisible", true); Call("Refresh");
                        repeatIndex = 0; extendedPhase = 3; WriteExtended(MemoryRecord("repeat-baseline")); return;
                    }
                    ResetExtended();
                    activeScenario = scenarios[scenarioIndex++];
                    activeScenario.Prepare?.Invoke();
                    // 준비 시 발생한 레이아웃은 다음 update에서 먼저 처리한다.
                    extendedPhase = 2; readyTicks = 0; return;
                }
                if (extendedPhase == 2)
                {
                    ++readyTicks;
                    if (activeScenario.Name == "focus-scroll-preservation" && readyTicks == 2)
                    {
                        var scroll = (ScrollView)Field("properties");
                        ExtendedCheck(scroll.verticalScroller.highValue >= 20, "focus test has real scrollable overflow");
                        scroll.scrollOffset = new Vector2(0, 20); return;
                    }
                    if (readyTicks < 3) return;
                    StartExtendedMeasurement(activeScenario.Name, activeScenario.Action);
                    extendedPhase = 4; return;
                }
                if (extendedPhase == 3)
                {
                    if (repeatIndex == 100)
                    {
                        ExtendedCheck(((ICollection)typeof(AuthoringEditSession).GetField("undo", Flags).GetValue(EditSession)).Count == 100,
                            "100 edits retain exactly 100 Undo entries");
                        CaptureAndReplaceSession(); extendedPhase = 5; readyTicks = 0; return;
                    }
                    int number = ++repeatIndex;
                    StartExtendedMeasurement("repeat-name", () => Call("Edit", (Action)(() =>
                        EditSession.EditLevel("반복 이름", level => level["displayName"] = "반복 " + number))));
                    extendedPhase = 6; return;
                }
                if (extendedPhase == 4 || extendedPhase == 6)
                {
                    if (++readyTicks < 2) return;
                    var board = (VisualElement)Field("board");
                    if (ExtendedRoot.panel == null || !float.IsFinite(board.worldBound.width) || board.worldBound.width <= 0 ||
                        board.Q("cell-40") == null || board.Q("cell-40").worldBound.width <= 0) return;
                    pendingMeasurement["displayReadyMs"] = readyWatch.Elapsed.TotalMilliseconds;
                    pendingMeasurement["displayDocumentReads"] = (int)Field("displayDocumentReadCount") - (int)pendingMeasurement["readsBefore"];
                    pendingMeasurement["definitionBuilds"] = (int)Field("definitionBuildCount") - (int)pendingMeasurement["buildsBefore"];
                    pendingMeasurement.Remove("readsBefore"); pendingMeasurement.Remove("buildsBefore");
                    ExtendedCheck(JToken.DeepEquals((JObject)Field("displayedLevel"), EditSession.Get(measuredLevel).Data), "display document is current");
                    if (extendedPhase == 4) activeScenario.Verify?.Invoke();
                    else
                    {
                        ExtendedCheck(((Label)Field("levelTitle")).text.Contains("반복 " + repeatIndex), "repeat name is visible");
                        pendingMeasurement["index"] = repeatIndex;
                    }
                    WriteExtended(pendingMeasurement); pendingMeasurement = null;
                    if (extendedPhase == 6 && repeatIndex % 10 == 0)
                    {
                        WriteExtended(MemoryRecord("batch-" + repeatIndex));
                        sampleWatch.Restart();
                    }
                    extendedPhase = extendedPhase == 4 ? 1 : 3; return;
                }
                if (extendedPhase == 5)
                {
                    if (++readyTicks < 5) return;
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    ExtendedCheck(releasedReferences.All(reference => !reference.IsAlive), "old session document and replaced board/input controls released");
                    ExtendedCheck((string)EditSession.Get(measuredLevel).Data["displayName"] == baselineName && !EditSession.CanUndo && !EditSession.IsDirty,
                        "new session restores clean saved baseline");
                    WriteExtended(MemoryRecord("after-session-replacement"));
                    ExtendedCheck(unexpectedProjectWrites == 0 && recoveryWrites == 0, "automatic project and recovery writes remain zero");
                    WriteExtended(new JObject { ["kind"] = "complete", ["status"] = "PASS",
                        ["projectWrites"] = unexpectedProjectWrites, ["recoveryWrites"] = recoveryWrites,
                        ["peakWorkingSetBytes"] = Interlocked.Read(ref peakBytes) });
                    ExtendedFinish(0);
                }
            }
            catch (Exception error) { File.AppendAllText(Path.Combine(folder, "failure.txt"), error.ToString()); Debug.LogException(error); ExtendedFinish(1); }
        }

        private static void BuildScenarios()
        {
            for (int run = 0; run < 3; run++)
            {
                scenarios.Add(new Scenario { Name = "cell-place", Prepare = () => ChangeLevel(level => LevelDocumentEditing.Erase(level, measuredDefinitions, "Block", 4, 4)),
                    Action = () => ChangeLevel(level => LevelDocumentEditing.Place(level, measuredDefinitions, normalDefinition, 4, 4, false, color: "Type1")),
                    Verify = () => ExtendedCheck(EditSession.Get(measuredLevel).Data["elements"].Any(item => (int)item["coordinate"]["row"] == 4 &&
                        (int)item["coordinate"]["column"] == 4 && (string)item["definitionId"] == normalDefinition), "placed block present") });
                scenarios.Add(new Scenario { Name = "cell-delete", Prepare = () => ChangeLevel(level => LevelDocumentEditing.Place(level, measuredDefinitions, normalDefinition, 4, 4, true)),
                    Action = () => ChangeLevel(level => LevelDocumentEditing.Erase(level, measuredDefinitions, "Block", 4, 4)),
                    Verify = () => { Call("Edit", (Action)(() => EditSession.Undo())); Call("Edit", (Action)(() => EditSession.Redo()));
                        ExtendedCheck(!EditSession.Get(measuredLevel).Data["elements"].Any(item => (string)item["layer"] == "Block" &&
                            (int)item["coordinate"]["row"] == 4 && (int)item["coordinate"]["column"] == 4), "delete Undo/Redo remains current"); } });
                scenarios.Add(new Scenario { Name = "supply", Prepare = () => { SetField("inspectorPage", "공급"); Call("Refresh"); },
                    Action = () => ChangeLevel(level => level["elementSupply"]["scrapTarget"] = 7),
                    Verify = () => { ExtendedCheck(ExtendedRoot.Q<IntegerField>("scrapTarget").value == 7, "supply visible");
                        Call("Edit", (Action)(() => EditSession.Undo())); Call("Edit", (Action)(() => EditSession.Redo()));
                        ExtendedCheck(ExtendedRoot.Q<IntegerField>("scrapTarget").value == 7, "supply Redo visible"); } });
                scenarios.Add(new Scenario { Name = "tutorial", Prepare = () => { SetField("inspectorPage", "튜토리얼"); Call("Refresh"); },
                    Action = () => Call("Edit", (Action)(() => TutorialDraftEditing.EditLevel(EditSession, "튜토리얼 안내", level => level.Tutorial.steps[0].instructions = "측정 안내"))),
                    Verify = () => { ExtendedCheck(ExtendedRoot.Q<TextField>("tutorial-instructions")?.value == "측정 안내", "tutorial field current");
                        SetField("inspectorPage", "기본"); Call("Refresh"); SetField("inspectorPage", "튜토리얼"); Call("Refresh");
                        ExtendedCheck(ExtendedRoot.Q<TextField>("tutorial-instructions")?.value == "측정 안내", "hidden tutorial reentry current"); } });
                scenarios.Add(new Scenario { Name = "shared-apply", Prepare = PrepareShared,
                    Action = () => Call("Edit", (Action)(() => { measuredShared.Apply(EditSession); SetField("sharedTutorialDraft", null); })),
                    Verify = () => { ExtendedCheck(ExtendedRoot.Q<Button>("tutorial-edit-flow") != null &&
                            (string)EditSession.Get(sharedFlow).Data["steps"][0]["instructions"] == "공유 측정", "shared apply returns to sharing menu with current document");
                        Call("Edit", (Action)(() => EditSession.Undo())); Call("Edit", (Action)(() => EditSession.Redo()));
                        SetField("sharedTutorialDraft", new SharedTutorialDraft(EditSession)); Call("Refresh");
                        ExtendedCheck(ExtendedRoot.Q<TextField>("tutorial-instructions")?.value == "공유 측정", "shared Redo and draft reentry visible"); } });
                scenarios.Add(new Scenario { Name = "selection", Prepare = () => unchangedVersion = EditSession.GetDocumentVersion(measuredLevel),
                    Action = () => Call("Edit", (Action)(() => EditSession.SelectCells(new[] { 40 }))),
                    Verify = () => ExtendedCheck(EditSession.SelectedCells.SequenceEqual(new[] { 40 }) &&
                        EditSession.GetDocumentVersion(measuredLevel) == unchangedVersion &&
                        ((VisualElement)Field("board")).Q("cell-40").ClassListContains("selected"), "selection visible without document edit") });
                scenarios.Add(new Scenario { Name = "zoom", Prepare = () => unchangedVersion = EditSession.GetDocumentVersion(measuredLevel),
                    Action = () => { SetField("cellSize", 64f); Call("Refresh"); },
                    Verify = () => ExtendedCheck(EditSession.GetDocumentVersion(measuredLevel) == unchangedVersion &&
                        Math.Abs(((VisualElement)Field("board")).Q("cell-40").worldBound.width - 64) < 1, "zoom geometry changes without document edit") });
                scenarios.Add(new Scenario { Name = "clipboard-compatibility", Prepare = () =>
                    { ChangeLevel(level => { LevelDocumentEditing.Place(level, measuredDefinitions, normalDefinition, 4, 4, true);
                        LevelDocumentEditing.Erase(level, measuredDefinitions, "Block", 4, 5); });
                        EditSession.SelectCells(new[] { 40 }); measuredClipboard = new ElementSelectionEditing(EditSession, () => measuredDefinitions); measuredClipboard.Copy("Block"); },
                    Action = () => Call("Edit", (Action)(() => measuredClipboard.Paste(4, 5))),
                    Verify = () => ExtendedCheck(EditSession.Get(measuredLevel).Data["elements"].Any(item => (string)item["layer"] == "Block" &&
                        (int)item["coordinate"]["row"] == 4 && (int)item["coordinate"]["column"] == 5), "compatibility paste works") });
                scenarios.Add(new Scenario { Name = "graph-construction", Action = () =>
                    { using var graph = new AuthoringObjectGraph(EditSession.Documents); graph.Resolve(measuredLevel); } });
                scenarios.Add(new Scenario { Name = "snapshot-validation", Action = () => EditSession.CreateSnapshot() });
                scenarios.Add(new Scenario { Name = "store-read-validation", Action = () => new ContentSnapshotStore(screen.Workspace.Folder).Read() });
            }
            scenarios.Add(new Scenario { Name = "focus-scroll-preservation", Prepare = () =>
                { SetField("levelSettingsVisible", true); Call("Refresh");
                    ((ScrollView)Field("properties")).style.maxHeight = 120;
                    preservedInput = ExtendedRoot.Q<TextField>("level-name"); preservedInput.Focus();
                    ((INotifyValueChanged<string>)preservedInput.Q(className: "unity-base-field__input").Q<TextElement>()).SetValueWithoutNotify("미확정 입력");
                    preservedBoard = ((VisualElement)Field("board"))[0]; preservedMaterial = ((ScrollView)Field("palette")).contentContainer[0]; },
                Action = () => Call("Refresh"), Verify = () =>
                { ExtendedCheck(ReferenceEquals(preservedInput, ExtendedRoot.Q<TextField>("level-name")), "unchanged refresh preserves input");
                    ExtendedCheck(ReferenceEquals(preservedBoard, ((VisualElement)Field("board"))[0]), "unchanged refresh preserves board");
                    ExtendedCheck(ReferenceEquals(preservedMaterial, ((ScrollView)Field("palette")).contentContainer[0]), "unchanged refresh preserves palette");
                    ExtendedCheck(preservedInput.Contains(ExtendedRoot.focusController.focusedElement as VisualElement) ||
                        ReferenceEquals(preservedInput, ExtendedRoot.focusController.focusedElement), "input focus retained");
                    ExtendedCheck(preservedScroll.y > 0 && ((ScrollView)Field("properties")).scrollOffset == preservedScroll &&
                        ((TextField)preservedInput).text == "미확정 입력" && !EditSession.IsDirty, "scroll and pending input retained without document edit"); } });
        }

        private static void PrepareShared()
        {
            sharedFlow = TutorialDraftEditing.CreateFlow(EditSession, "측정 공유 구성");
            measuredShared = new SharedTutorialDraft(EditSession);
            TutorialDraftEditing.EditFlow(measuredShared.Session, sharedFlow, "공유 안내", flow => flow.steps[0].instructions = "공유 측정");
            SetField("sharedTutorialDraft", measuredShared); SetField("inspectorPage", "튜토리얼"); Call("Refresh");
        }
        private static void ChangeLevel(Action<JObject> action) => Call("Edit", (Action)(() => EditSession.EditLevel("대표 조작", action)));
        private static void ResetExtended()
        {
            sampleWatch.Restart(); measuredClipboard = null; measuredShared = null;
            preservedInput = preservedBoard = preservedMaterial = null;
            ExtendedRoot.focusController?.focusedElement?.Blur(); Call("CancelPendingInput");
            ((ScrollView)Field("properties")).style.maxHeight = StyleKeyword.Null;
            SetField("sharedTutorialDraft", null); EditSession.Discard(); EditSession.SelectLevel(measuredLevel);
            SetField("inspectorPage", "기본"); SetField("levelSettingsVisible", false);
            // 자동 맞춤의 예약 갱신과 수동 줌 측정을 섞지 않는다.
            SetField("autoFitBoard", false); SetField("cellSize", 52f); Call("Refresh");
        }
        private static void StartExtendedMeasurement(string name, Action action)
        {
            pendingMeasurement = new JObject { ["kind"] = "operation", ["operation"] = name, ["documents"] = EditSession.DocumentIds.Length,
                ["readsBefore"] = (int)Field("displayDocumentReadCount"), ["buildsBefore"] = (int)Field("definitionBuildCount") };
            if (name == "focus-scroll-preservation")
            {
                preservedScroll = ((ScrollView)Field("properties")).scrollOffset;
                pendingMeasurement["scrollOffsetBefore"] = preservedScroll.y;
                preservedInput = ExtendedRoot.Q<TextField>("level-name");
                preservedBoard = ((VisualElement)Field("board"))[0];
                preservedMaterial = ((ScrollView)Field("palette")).contentContainer[0];
            }
            readyWatch = Stopwatch.StartNew(); action();
            pendingMeasurement["synchronousMs"] = readyWatch.Elapsed.TotalMilliseconds; readyTicks = 0;
            if (name == "repeat-name" || name == "cell-place" || name == "cell-delete" || name == "supply" ||
                name == "tutorial" || name == "shared-apply" || name == "clipboard-compatibility")
                pendingMeasurement["sessionEditCopies"] = (int)typeof(AuthoringEditSession).GetField("lastEditCopyCount", Flags).GetValue(EditSession);
        }
        private static JObject MemoryRecord(string name)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            GetProcessMemoryInfo(new IntPtr(-1), out MemoryCounters memory, (uint)Marshal.SizeOf<MemoryCounters>());
            return new JObject { ["kind"] = "memory", ["name"] = name, ["managedBytes"] = GC.GetTotalMemory(false),
                ["workingSetBytes"] = (long)memory.WorkingSet.ToUInt64(),
                ["undoEntries"] = ((ICollection)typeof(AuthoringEditSession).GetField("undo", Flags).GetValue(EditSession)).Count,
                ["visualElements"] = ExtendedRoot.Query<VisualElement>().ToList().Count,
                ["levelObjects"] = Resources.FindObjectsOfTypeAll<Levels.LevelDefinition>().Length };
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void CaptureAndReplaceSession()
        {
            sampleWatch.Restart(); activeScenario = null; scenarios.Clear();
            preservedInput = preservedBoard = preservedMaterial = null; measuredClipboard = null; measuredShared = null;
            releasedReferences.Add(new WeakReference(EditSession));
            releasedReferences.Add(new WeakReference(Field("displayedLevel")));
            releasedReferences.Add(new WeakReference(((VisualElement)Field("board"))[0]));
            releasedReferences.Add(new WeakReference(ExtendedRoot.Q<TextField>("level-name")));
            screen.Workspace.Reload("Discard"); Call("Refresh");
        }
        private static void WriteExtended(JObject value) => File.AppendAllText(report, value.ToString(Formatting.None) + "\n");
        private static void ExtendedCheck(bool value, string message)
        {
            if (!value) throw new Exception(message);
            File.AppendAllText(Path.Combine(folder, "checks.txt"), "PASS " + message + "\n");
        }
        private static void ExtendedFinish(int code)
        {
            EditorApplication.update -= ExtendedTick; Finish(code);
        }
    }
}
