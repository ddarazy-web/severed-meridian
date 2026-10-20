using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolMobileVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static string Output = "Logs/AndroidLevelToolStage02";
        private static readonly Vector2Int[] Sizes =
        {
            new Vector2Int(360, 780), new Vector2Int(412, 915), new Vector2Int(780, 360),
            new Vector2Int(975, 800), new Vector2Int(976, 800), new Vector2Int(1280, 800), new Vector2Int(800, 1280)
        };
        private static readonly string[] Panels = { "levels", "materials", "properties", "more" };
        private static readonly string[] AdvancedPages = { "flow", "tutorial", "test", "records" };
        private static readonly string[] AdvancedInspectorPages = { "흐름", "튜토리얼", "자동 시험", "시험 기록" };
        private static readonly string[] AdvancedControls = { "tool-cancel", "tutorial-add-step", "bot-new", "records-count" };
        private static readonly Vector2Int[] ScaledSizes =
        {
            new Vector2Int(1920, 1200), new Vector2Int(1900, 1200), new Vector2Int(824, 1830), new Vector2Int(1440, 900)
        };
        private static readonly float[] ViewportScales = { 2, 2, 2, 1.5f };
        private static readonly StringBuilder Evidence = new StringBuilder();
        private static LevelToolScreen screen;
        private static PanelSettings panel;
        private static PanelScaleMode priorScaleMode;
        private static float priorScale;
        private static RenderTexture texture, priorTarget;
        private static bool priorEnabled, done, pendingInputOnly, advancedScreensOnly, pendingCommandsOnly, scaledViewportsOnly;
        private static float viewportScale = 1;
        private static EnterPlayModeOptions priorOptions;
        private static int phase, sizeIndex, panelIndex, advancedIndex, exit;
        private static double deadline, next;
        private static bool viewportApplyPending;
        private static string recoveryFolder, projectFolder, levelId, expectedState, definitionId;
        private static AuthoringEditSession session;
        private static VisualElement modal;
        private static TextField input;
        private static Rect safeBounds;
        private static int cursor, selection;
        private static string pendingText, pendingValue;
        private static IntegerField commandNumber;
        private static int originalMoves;
        private static string commandBeforeState, commandSavedFiles;
        private const string HiddenSavedName = "숨긴 입력 저장 검사";
        private const string HiddenUndoName = "숨긴 입력 Undo 검사";
        private const string NormalCommandName = "명령 이후 정상 입력";
        private static VisualElement Root => screen.GetComponent<UIDocument>().rootVisualElement;

        public static void Run() => Begin(false);

        public static void RunPendingInput() => Begin(true);

        public static void RunAdvancedScreens() => Begin(false, true);

        public static void RunPendingCommands() => Begin(false, false, true);

        public static void RunIsolatedPendingCommands()
        {
            Output = "Logs/LevelToolRecoveryPerformance/pending-" + Guid.NewGuid().ToString("N");
            Begin(false, false, true);
        }

        public static void RunScaledViewports() => Begin(false, false, false, true);

        private static void Begin(bool onlyPendingInput, bool onlyAdvancedScreens = false, bool onlyPendingCommands = false, bool onlyScaledViewports = false)
        {
            pendingInputOnly = onlyPendingInput;
            advancedScreensOnly = onlyAdvancedScreens;
            pendingCommandsOnly = onlyPendingCommands;
            scaledViewportsOnly = onlyScaledViewports;
            viewportScale = 1;
            viewportApplyPending = false;
            Directory.CreateDirectory(Output);
            Evidence.Clear();
            Evidence.AppendLine(scaledViewportsOnly ?
                "Editor UI Toolkit mobile simulation, constant pixel scales 1.5 and 2. Physical DPI/dp, Android OS keyboard and Korean IME remain unverified. PNGs require visual review." :
                "Editor UI Toolkit mobile simulation, constant pixel scale 1. Physical dp, Android OS keyboard and Korean IME remain unverified. PNGs require visual review.");
            if (pendingInputOnly) Evidence.AppendLine("Focused pending-input run: navigation pointer departure/return, rotation, keyboard and Undo/Redo/save/reopen. Full viewport matrix and modal checks are excluded.");
            if (advancedScreensOnly) Evidence.AppendLine("Advanced screen access run: actual navigation pointer entry, property scrolling, rotation, panel close and board return. No field editing, trial execution or record deletion.");
            if (pendingCommandsOnly) Evidence.AppendLine("Hidden pending command run: actual Save/Undo pointers, delayed text persistence, invalid integer rejection, unchanged saved files and subsequent normal input.");
            if (scaledViewportsOnly) Evidence.AppendLine("Scaled viewport run: physical/rendered to logical UI mapping, safe width layout selection, 48 UI-unit touch geometry, keyboard modal and retained input/session across actual panel scale changes.");
            priorEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            priorOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            string runFolder = Path.GetFullPath(Output + "/run-" + Guid.NewGuid().ToString("N"));
            recoveryFolder = Path.Combine(runFolder, "Recovery");
            projectFolder = Path.Combine(runFolder, "Project");
            LevelToolRecoveryIsolation.Configure(recoveryFolder);
            phase = sizeIndex = panelIndex = advancedIndex = exit = 0; done = false; next = 0;
            deadline = EditorApplication.timeSinceStartup + (pendingInputOnly ? 90 : advancedScreensOnly || scaledViewportsOnly ? 150 : pendingCommandsOnly ? 120 : 300);
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += State;
            EditorApplication.EnterPlaymode();
        }

        private static void State(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= State;
            if (panel != null)
            {
                panel.targetTexture = priorTarget;
                panel.scaleMode = priorScaleMode;
                panel.scale = priorScale;
            }
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); texture = null; }
            EditorSettings.enterPlayModeOptionsEnabled = priorEnabled;
            EditorSettings.enterPlayModeOptions = priorOptions;
            File.WriteAllText(Output + (pendingInputOnly ? "/mobile-pending-input-verification.txt" : advancedScreensOnly ? "/mobile-advanced-screens-verification.txt" : pendingCommandsOnly ? "/mobile-pending-commands-verification.txt" : scaledViewportsOnly ? "/mobile-scaled-viewports-verification.txt" : "/mobile-verification.txt"), Evidence.ToString());
            EditorApplication.Exit(exit);
        }

        private static void Tick()
        {
            try
            {
                if (done) return;
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("모바일 UI 검사 시간 초과: " + phase);
                if (!EditorApplication.isPlaying || viewportApplyPending || EditorApplication.timeSinceStartup < next) return;
                switch (phase)
                {
                    case 0:
                        screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                        if (screen == null || (bool)Get("busy")) return;
                        LevelToolRecoveryIsolation.Verify(screen, recoveryFolder);
                        panel = screen.GetComponent<UIDocument>().panelSettings;
                        priorTarget = panel.targetTexture; priorScaleMode = panel.scaleMode; priorScale = panel.scale;
                        panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1;
                        Check(screen.Workspace.Create(projectFolder, LevelToolDocuments.NewProject()), "isolated project created");
                        session = screen.Workspace.Session; levelId = session.SelectedLevelId;
                        Dictionary<string, JObject> definitions = LevelToolDocuments.Definitions(session);
                        definitionId = definitions.First(pair => LevelToolDocuments.Layer(pair.Value) == "Block" && LevelToolDocuments.Size(pair.Value) == 1).Key;
                        session.SelectCells(new[] { 40, 41 });
                        session.Apply("회전 검사 보드 배치", documents => LevelDocumentEditing.Place(documents[levelId].Data, definitions, definitionId, 4, 4, false, color: "Type1"));
                        session.Apply("Redo 검사", documents => documents[levelId].Data["displayName"] = "회전 후 Redo");
                        session.Undo();
                        Set("layer", "Block"); Set("brush", definitionId);
                        Invoke("Refresh"); expectedState = session.ExportState();
                        if (scaledViewportsOnly)
                        {
                            viewportScale = ViewportScales[sizeIndex]; panel.scale = viewportScale; SetViewport(ScaledSizes[sizeIndex]);
                        }
                        else SetViewport(pendingInputOnly || advancedScreensOnly || pendingCommandsOnly ? new Vector2Int(412, 915) : Sizes[sizeIndex]);
                        Advance(pendingInputOnly ? 16 : advancedScreensOnly ? 20 : pendingCommandsOnly ? 40 : scaledViewportsOnly ? 60 : 1); break;
                    case 16:
                        Click("mobile-properties"); Click("inspector-level"); Advance(7); break;
                    case 60:
                        VerifyBaseLayout();
                        Check(Root.ClassListContains(sizeIndex == 0 ? "mobile-wide" : sizeIndex == 2 ? "mobile-portrait" : "mobile-landscape"),
                            "scaled physical fixture selects independently specified wide/compact layout " + sizeIndex);
                        foreach (string id in new[] { "save", "undo", "redo", "play", "mobile-levels", "mobile-materials", "mobile-properties", "mobile-more" })
                        {
                            Rect hit = Root.Q(id).worldBound;
                            float expectedPixels = viewportScale == 2 ? 96 : 72;
                            Check(hit.width >= 48 && hit.height >= 48 && hit.width * viewportScale >= expectedPixels && hit.height * viewportScale >= expectedPixels,
                                "actual scaled touch geometry " + id + " ui=" + hit.size + " renderedPixels=" + hit.size * viewportScale + " minimumPixels=" + expectedPixels);
                        }
                        Capture("scaled-base-" + viewportScale);
                        Invoke("ChooseInternalProject", new Action<string>(_ => { throw new Exception("배율 검사는 프로젝트 선택을 확정하지 않습니다."); }), true);
                        Advance(61); break;
                    case 61:
                        modal = Root.Q("modal"); input = modal.Q<TextField>("project-name");
                        input.SetValueWithoutNotify("배율 변경 중 입력 보존"); input.Focus(); input.SelectRange(1, 5); Advance(62); break;
                    case 62:
                        cursor = input.cursorIndex; selection = input.selectIndex; pendingText = input.text; pendingValue = input.value;
                        Vector2Int keyboardSize = ScaledSizes[sizeIndex];
                        SetViewport(keyboardSize, new Rect(0, 0, keyboardSize.x, keyboardSize.y * .30f)); Advance(63); break;
                    case 63:
                        VerifyModalInput(); Within(input, safeBounds, "scaled keyboard input");
                        Capture("scaled-keyboard-" + viewportScale);
                        viewportScale = viewportScale == 2 ? 1.5f : 2; panel.scale = viewportScale;
                        Vector2Int rescaledSize = ScaledSizes[sizeIndex];
                        SetViewport(rescaledSize, new Rect(0, 0, rescaledSize.x, rescaledSize.y * .30f)); Advance(64); break;
                    case 64:
                        Check(Mathf.Abs(Root.worldBound.width - texture.width / viewportScale) < 2 && Mathf.Abs(Root.worldBound.height - texture.height / viewportScale) < 2,
                            "actual panel rescale maps physical pixels to expected logical root");
                        VerifyModalInput(); Within(input, safeBounds, "rescaled keyboard input");
                        Capture("scaled-keyboard-rescale-" + viewportScale);
                        modal.Q<ScrollView>().ScrollTo(modal.Q<Button>("choose-project")); Advance(66); break;
                    case 66:
                        Within(modal.Q<Button>("choose-project"), safeBounds, "scaled keyboard confirmation access");
                        Within(modal.Q<Button>("choose-project"), modal.Q<ScrollView>().contentViewport.worldBound, "scaled modal viewport confirmation access");
                        VerifyPreserved("scaled modal confirmation scroll");
                        modal.Q<ScrollView>().ScrollTo(modal.Q<Button>("cancel")); Advance(67); break;
                    case 67:
                        Within(modal.Q<Button>("cancel"), safeBounds, "scaled keyboard cancellation access");
                        Within(modal.Q<Button>("cancel"), modal.Q<ScrollView>().contentViewport.worldBound, "scaled modal viewport cancellation access");
                        Capture("scaled-keyboard-cancel-visible-" + viewportScale);
                        PointerClick("cancel"); VerifyPreserved("scaled modal cancellation"); sizeIndex++;
                        if (sizeIndex < ScaledSizes.Length)
                        {
                            viewportScale = ViewportScales[sizeIndex]; panel.scale = viewportScale;
                            SetViewport(ScaledSizes[sizeIndex]); Advance(60);
                        }
                        else Advance(65);
                        break;
                    case 65:
                        Check(Root.Q("modal") == null && Root.Q("editor").enabledInHierarchy, "scaled modal cancellation restores editor access");
                        VerifyPreserved("scaled viewport completion");
                        Evidence.AppendLine("PASS actual scales 1.5/2, logical root and safe area mapping, wide/compact width selection, UI/rendered-pixel hit geometry and keyboard modal/input/session preservation across rescaling. Actual physical DPI/dp and Android OS keyboard/IME remain unverified.");
                        done = true; EditorApplication.ExitPlaymode(); break;
                    case 40:
                        PointerClick("mobile-properties"); Click("inspector-level"); Advance(41); break;
                    case 41:
                        input = Root.Q<TextField>("level-name"); input.Focus(); Advance(42); break;
                    case 42:
                        SetRawCommandInput(input, HiddenSavedName);
                        Check(input.isDelayed && input.text == HiddenSavedName && input.value != HiddenSavedName, "actual delayed name has pending hidden-save text");
                        commandBeforeState = session.ExportState(); PointerClick("mobile-levels"); Advance(43); break;
                    case 43:
                        Check(Root.Q("right-panel").resolvedStyle.display == DisplayStyle.None && input.text == HiddenSavedName && session.ExportState() == commandBeforeState, "hiding pending save field does not commit document");
                        PointerClick("save"); Advance(44); break;
                    case 44:
                        if (!CommandsIdle()) return;
                        Check((string)session.Get(levelId).Data["displayName"] == HiddenSavedName && (string)ReadSavedDocument()["displayName"] == HiddenSavedName && !session.IsDirty,
                            "actual Save pointer persists hidden delayed name in session and JSON");
                        Check((string)Get("mobilePanel") == "properties", "Save restores hidden input panel");
                        Check(((System.Collections.IDictionary)Get("mobilePendingInputs")).Count == 0, "successful hidden Save releases pending input cache");
                        commandNumber = Root.Q<IntegerField>("level-moves"); originalMoves = commandNumber.value;
                        Check(commandNumber.isDelayed, "restored numeric field retains delayed validation");
                        commandNumber.Focus(); Advance(45); break;
                    case 45:
                        SetRawCommandInput(commandNumber, "-");
                        Check(commandNumber.text == "-" && commandNumber.value == originalMoves, "actual numeric field has invalid uncommitted raw input");
                        commandBeforeState = session.ExportState(); commandSavedFiles = CaptureProjectFiles();
                        TracePendingNumber("before numeric navigation PointerDown");
                        PointerClick("mobile-levels");
                        TracePendingNumber("after numeric navigation PointerUp"); Advance(46); break;
                    case 46:
                        TracePendingNumber("after numeric navigation geometry settles");
                        Check(Root.Q("right-panel").resolvedStyle.display == DisplayStyle.None, "invalid pending numeric panel is hidden");
                        Check(commandNumber.text == "-", "hidden numeric field preserves raw invalid minus string");
                        Check(session.ExportState() == commandBeforeState, "invalid pending numeric panel hiding preserves complete document/history state");
                        PointerClick("save"); Advance(47); break;
                    case 47:
                        if (!CommandsIdle()) return;
                        Check((string)Get("mobilePanel") == "properties" && ReferenceEquals(commandNumber, Root.Q<IntegerField>("level-moves")) && commandNumber.text == "-" && commandNumber.value == originalMoves,
                            "invalid hidden Save restores original field and retains raw invalid string/value");
                        Check(session.ExportState() == commandBeforeState && !session.IsDirty && session.CanUndo && CaptureProjectFiles() == commandSavedFiles,
                            "invalid hidden Save leaves document, dirty state, complete history and every saved byte unchanged");
                        Check(Root.Q<Label>("status").text.Contains("정수"), "invalid hidden Save explains integer validation failure");
                        Check(commandNumber.isDelayed && ((System.Collections.IDictionary)Get("mobilePendingInputs")).Count == 0, "invalid command releases input cache and delayed-input guard");
                        SetRawCommandInput(commandNumber, originalMoves.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        commandNumber.SetValueWithoutNotify(originalMoves);
                        input = Root.Q<TextField>("level-name"); input.Focus(); Advance(48); break;
                    case 48:
                        SetRawCommandInput(input, HiddenUndoName);
                        Check(input.isDelayed && input.text == HiddenUndoName && input.value == HiddenSavedName, "hidden Undo draft remains uncommitted");
                        commandBeforeState = session.ExportState(); PointerClick("mobile-levels"); Advance(49); break;
                    case 49:
                        Check(Root.Q("right-panel").resolvedStyle.display == DisplayStyle.None && session.ExportState() == commandBeforeState, "hiding pending Undo name preserves complete session");
                        PointerClick("undo"); Advance(50); break;
                    case 50:
                        if (!CommandsIdle()) return;
                        Check((string)session.Get(levelId).Data["displayName"] == HiddenSavedName && session.CanRedo && (string)ReadSavedDocument()["displayName"] == HiddenSavedName,
                            "actual Undo pointer commits and undoes hidden draft to immediately preceding saved name");
                        Check(((System.Collections.IDictionary)Get("mobilePendingInputs")).Count == 0, "hidden Undo releases pending input cache");
                        PointerClick("redo"); Advance(51); break;
                    case 51:
                        if (!CommandsIdle()) return;
                        Check((string)session.Get(levelId).Data["displayName"] == HiddenUndoName && session.IsDirty, "actual Redo pointer restores precisely the hidden draft undone by Undo");
                        input = Root.Q<TextField>("level-name"); Check(input.isDelayed, "normal delayed field remains usable after cached command");
                        input.Focus(); Advance(52); break;
                    case 52:
                        SetRawCommandInput(input, NormalCommandName);
                        Check(input.text == NormalCommandName && input.value != NormalCommandName, "subsequent normal field accepts new uncommitted text");
                        PointerClick("save"); Advance(53); break;
                    case 53:
                        if (!CommandsIdle()) return;
                        Check((string)session.Get(levelId).Data["displayName"] == NormalCommandName && (string)ReadSavedDocument()["displayName"] == NormalCommandName && !session.IsDirty,
                            "subsequent normal Save commits new input after cache guard removal");
                        Check(ReferenceEquals(screen.Workspace.Session, session) && session.SelectedCells.SequenceEqual(new[] { 40, 41 }), "hidden commands retain session identity and selected cells");
                        Capture("pending-commands-complete");
                        Evidence.AppendLine("PASS hidden pending Save persistence, invalid numeric no-change rejection, hidden draft Undo/Redo and subsequent normal input/save. Actual Android keyboard/IME remains unverified.");
                        done = true; EditorApplication.ExitPlaymode(); break;
                    case 20:
                        PointerClick("mobile-more"); Advance(31); break;
                    case 31:
                        Root.Q<ScrollView>("mobile-more-panel").ScrollTo(Root.Q<Button>("workspace-" + AdvancedPages[advancedIndex]));
                        Advance(21); break;
                    case 21:
                        PointerClick("workspace-" + AdvancedPages[advancedIndex]); Advance(22); break;
                    case 22:
                        Check((string)Get("inspectorPage") == AdvancedInspectorPages[advancedIndex], "actual pointer enters advanced page " + AdvancedPages[advancedIndex]);
                        VerifyAdvancedSession("page entry"); PointerClick("mobile-properties"); Advance(23); break;
                    case 23:
                        ScrollView advancedProperties = Root.Q<ScrollView>("properties");
                        VisualElement advancedControl = Root.Q(AdvancedControls[advancedIndex]);
                        Check(advancedControl != null, "advanced representative control exists " + AdvancedControls[advancedIndex]);
                        if (AdvancedPages[advancedIndex] == "records")
                        {
                            advancedProperties.ScrollTo(Root.Q<Foldout>("record-management").Q<Toggle>()); Advance(33);
                        }
                        else { advancedProperties.ScrollTo(advancedControl); Advance(24); }
                        break;
                    case 33:
                        Foldout recordManagement = Root.Q<Foldout>("record-management");
                        Check(!recordManagement.value, "record management starts collapsed");
                        Toggle recordToggle = recordManagement.Q<Toggle>();
                        Within(recordToggle, Root.Q<ScrollView>("properties").contentViewport.worldBound, "visible record management toggle");
                        PointerClick(recordToggle, "record-management toggle"); Advance(34); break;
                    case 34:
                        Check(Root.Q<Foldout>("record-management").value, "actual pointer expands record management foldout");
                        VerifyAdvancedSession("record foldout expansion");
                        Root.Q<ScrollView>("properties").ScrollTo(Root.Q("records-count")); Advance(24); break;
                    case 24:
                        VerifyAdvancedControl(); Capture("advanced-" + AdvancedPages[advancedIndex] + "-portrait");
                        SetViewport(new Vector2Int(915, 412)); Advance(25); break;
                    case 25:
                        Check((string)Get("inspectorPage") == AdvancedInspectorPages[advancedIndex] && (string)Get("mobilePanel") == "properties", "advanced page and panel survive rotation");
                        VerifyAdvancedSession("advanced rotation");
                        Root.Q<ScrollView>("properties").ScrollTo(Root.Q(AdvancedControls[advancedIndex])); Advance(26); break;
                    case 26:
                        VerifyAdvancedControl(); Capture("advanced-" + AdvancedPages[advancedIndex] + "-landscape");
                        PointerClick("mobile-properties"); Advance(27); break;
                    case 27:
                        Check(Get("mobilePanel") == null && Root.Q("right-panel").resolvedStyle.display == DisplayStyle.None, "actual pointer closes advanced properties panel");
                        VerifyAdvancedSession("advanced panel close");
                        PointerClick("mobile-more"); Advance(32); break;
                    case 32:
                        Root.Q<ScrollView>("mobile-more-panel").ScrollTo(Root.Q<Button>("workspace-edit")); Advance(28); break;
                    case 28:
                        PointerClick("workspace-edit"); Advance(29); break;
                    case 29:
                        Check((string)Get("inspectorPage") == "기본" && Root.Q("board").resolvedStyle.display != DisplayStyle.None, "actual pointer returns to board editor");
                        Check(Root.Q("board").Query<Label>(className: "cell").ToList().Count == 81, "advanced page return restores 81 board cells");
                        VerifyAdvancedSession("advanced board return");
                        PointerClick("mobile-more"); Advance(30); break;
                    case 30:
                        Within(Root.Q("board-scroll"), safeBounds, "board viewport after advanced close/return");
                        Check(Get("mobilePanel") == null, "board return closes more panel");
                        VerifyAdvancedSession("advanced final panel close"); Capture("advanced-" + AdvancedPages[advancedIndex] + "-board-return");
                        advancedIndex++;
                        if (advancedIndex < AdvancedPages.Length) { SetViewport(new Vector2Int(412, 915)); Advance(20); }
                        else
                        {
                            Evidence.AppendLine("PASS flow/tutorial/bot/records actual navigation entry, scroll access, rotation, panel close and 81-cell board return; complete dirty session/history preserved. Actual device verification remains pending.");
                            done = true; EditorApplication.ExitPlaymode();
                        }
                        break;
                    case 1:
                        VerifyBaseLayout(); Capture("board");
                        panelIndex = 0; Click("mobile-" + Panels[panelIndex]); Advance(2); break;
                    case 2:
                        VerifyPanel(Panels[panelIndex]); VerifyPreserved("panel " + Panels[panelIndex]); Capture(Panels[panelIndex]);
                        Click("mobile-" + Panels[panelIndex]);
                        panelIndex++;
                        if (panelIndex < Panels.Length) { Click("mobile-" + Panels[panelIndex]); Advance(2); }
                        else
                        {
                            Invoke("ChooseInternalProject", new Action<string>(_ => { throw new Exception("모바일 검사는 프로젝트 선택을 확정하지 않습니다."); }), true);
                            Advance(3);
                        }
                        break;
                    case 3:
                        modal = Root.Q("modal"); input = modal.Q<TextField>("project-name");
                        Check(modal != null && input != null && !Root.Q("editor").enabledInHierarchy, "internal name modal isolates editor");
                        input.SetValueWithoutNotify("미확정 프로젝트 이름"); input.Focus(); input.SelectRange(2, 6);
                        Advance(14); break;
                    case 14:
                        cursor = input.cursorIndex; selection = input.selectIndex; pendingText = input.text; pendingValue = input.value;
                        SetViewport(new Vector2Int(Sizes[sizeIndex].y, Sizes[sizeIndex].x)); Advance(4); break;
                    case 4:
                        VerifyModalInput(); Capture("rotated-modal");
                        SetViewport(new Vector2Int(780, 360), new Rect(0, 0, 780, 140)); Advance(5); break;
                    case 5:
                        VerifyModalInput();
                        ScrollView modalScroll = modal.Q<ScrollView>();
                        Check(modalScroll != null, "mobile modal has scrollable overflow");
                        modalScroll.ScrollTo(input); Advance(10); break;
                    case 10:
                        Within(input, safeBounds, "keyboard input after scroll");
                        Within(modal.Q(className: "dialog"), safeBounds, "keyboard constrained dialog");
                        modal.Q<ScrollView>().ScrollTo(modal.Q<Button>("choose-project")); Advance(11); break;
                    case 11:
                        Within(modal.Q<Button>("choose-project"), safeBounds, "keyboard confirm after scroll");
                        modal.Q<ScrollView>().ScrollTo(modal.Q<Button>("cancel")); Advance(6); break;
                    case 6:
                        Within(modal.Q<Button>("cancel"), safeBounds, "keyboard cancel after scroll"); Capture("keyboard-modal");
                        Click("cancel"); Check(Root.Q("modal") == null && Root.Q("editor").enabledInHierarchy, "cancel restores editor");
                        VerifyPreserved("modal rotation and keyboard");
                        sizeIndex++;
                        if (sizeIndex < Sizes.Length) { SetViewport(Sizes[sizeIndex]); Advance(1); }
                        else { SetViewport(new Vector2Int(412, 915)); Click("mobile-properties"); Click("inspector-level"); Advance(7); }
                        break;
                    case 7:
                        input = Root.Q<TextField>("level-name"); Check(input != null && input.isDelayed, "real delayed property input exists");
                        input.Focus();
                        TextElement textInput = input.Q<TextElement>("unity-text-input") ?? input.Query<TextElement>().ToList().Last();
                        ((INotifyValueChanged<string>)textInput).SetValueWithoutNotify("아직 확정하지 않은 한글 이름");
                        input.SelectRange(1, 7); Advance(15); break;
                    case 15:
                        Check(ReferenceEquals(input, Root.Q<TextField>("level-name")), "pending draft keeps original field before navigation");
                        VerifyPreserved("draft before navigation");
                        pendingText = input.text; pendingValue = input.value; cursor = input.cursorIndex; selection = input.selectIndex;
                        Check(input.value != pendingText, "actual property contains uncommitted text");
                        PointerClick("mobile-levels"); Advance(12); break;
                    case 12:
                        Check((string)Get("mobilePanel") == "levels", "actual navigation pointer opens levels panel");
                        Check(ReferenceEquals(input, Root.Q<TextField>("level-name")), "panel switch retains delayed property VisualElement");
                        VerifyPendingInput(false); VerifyPreserved("uncommitted property pointer panel departure");
                        PointerClick("mobile-properties"); Advance(13); break;
                    case 13:
                        Check((string)Get("mobilePanel") == "properties", "actual navigation pointer returns to properties panel");
                        VerifyInput(); VerifyPreserved("uncommitted property pointer panel return"); Capture("property-panel-return");
                        SetViewport(new Vector2Int(915, 412)); Advance(8); break;
                    case 8:
                        Check(ReferenceEquals(input, Root.Q<TextField>("level-name")), "rotation reuses property VisualElement");
                        VerifyInput(); VerifyPreserved("uncommitted property rotation"); Capture("property-rotation");
                        SetViewport(new Vector2Int(915, 412), new Rect(0, 0, 915, 150)); Advance(9); break;
                    case 9:
                        VerifyInput(); VerifyPreserved("uncommitted property keyboard");
                        Check((string)Get("mobilePanel") == "properties", "open properties panel survives rotation and keyboard");
                        // 미확정 값은 확정하지 않고 되돌린 뒤 보드 이력과 저장 의미를 독립적으로 확인한다.
                        input.SetValueWithoutNotify((string)session.Get(levelId).Data["displayName"]);
                        input.Blur(); SetViewport(new Vector2Int(1280, 800));
                        session.Redo(); Check((string)session.Get(levelId).Data["displayName"] == "회전 후 Redo", "original redo survives all viewport transitions");
                        session.Undo(); Check(session.ExportState() == expectedState, "undo restores complete pre-rotation state");
                        JObject expectedDocument = (JObject)session.Get(levelId).Data.DeepClone();
                        screen.Workspace.Save(); Check(!session.IsDirty, "rotated board saves successfully");
                        Check(screen.Workspace.Open(projectFolder, "Cancel"), "saved project reopens");
                        Check(JToken.DeepEquals(expectedDocument, screen.Workspace.Session.Get(levelId).Data), "save/reopen preserves board placement and full document semantics");
                        Check(screen.Workspace.Session.Get(levelId).Data["elements"].Any(item => (string)item["definitionId"] == definitionId && (int)item["coordinate"]["row"] == 4 && (int)item["coordinate"]["column"] == 4), "saved board edit remains at original coordinate");
                        Evidence.AppendLine(pendingInputOnly ?
                            "PASS pending input pointer panel switch/return, rotation/keyboard preservation and board edit→Undo/Redo→save/reopen. Full viewport/modal matrix was not run; actual OS keyboard/IME and dp remain unverified." :
                            "PASS mobile geometry, panel/modal/input preservation and board edit→rotation→Undo/Redo→save/reopen. Actual OS keyboard/IME and dp remain unverified.");
                        done = true; EditorApplication.ExitPlaymode(); break;
                }
            }
            catch (Exception error)
            {
                exit = 1; done = true; Evidence.AppendLine("FAIL " + error); Debug.LogException(error);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode(); else State(PlayModeStateChange.EnteredEditMode);
            }
        }

        private static void SetViewport(Vector2Int size, Rect keyboard = default)
        {
            if (texture != null) { panel.targetTexture = null; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            texture = new RenderTexture(size.x, size.y, 0); texture.Create(); panel.targetTexture = texture;
            // 비대칭 노치와 하단 시스템 영역을 매번 같은 물리 좌표 계약으로 넣는다.
            Rect safe = new Rect(12, 18, size.x - 36, size.y - 42);
            float bottom = Mathf.Max(safe.yMin, keyboard.height > 0 ? keyboard.yMax : safe.yMin);
            float expectedScale = scaledViewportsOnly ? viewportScale : 1;
            safeBounds = new Rect(safe.xMin / expectedScale, (size.y - safe.yMax) / expectedScale, safe.width / expectedScale, (safe.yMax - bottom) / expectedScale);
            viewportApplyPending = true;
            IVisualElementScheduledItem apply = null;
            apply = Root.schedule.Execute(() =>
            {
                // targetTexture/scale 교체 직후에는 이전 패널 크기로 여백을 계산하지 않는다.
                if (Mathf.Abs(Root.layout.width - size.x / expectedScale) >= 2 ||
                    Mathf.Abs(Root.layout.height - size.y / expectedScale) >= 2) return;
                Invoke("ApplyMobileViewport", new Vector2(size.x, size.y), safe, keyboard);
                viewportApplyPending = false;
                apply.Pause();
                next = Math.Max(next, EditorApplication.timeSinceStartup + .2);
            }).Every(50).StartingIn(200);
            Evidence.AppendLine("VIEWPORT " + size + " scale=" + expectedScale + " expectedLogicalRoot=" + new Vector2(size.x / expectedScale, size.y / expectedScale) + " safe=" + safe + " keyboard=" + keyboard + " expectedUI=" + safeBounds);
        }

        private static void VerifyBaseLayout()
        {
            float expectedScale = scaledViewportsOnly ? viewportScale : 1;
            Check(Mathf.Abs(Root.worldBound.width - texture.width / expectedScale) < 2 && Mathf.Abs(Root.worldBound.height - texture.height / expectedScale) < 2, "actual panel uses requested viewport geometry and scale");
            Rect editorBounds = Root.Q("editor").worldBound;
            Evidence.AppendLine("SAFE editor=" + editorBounds + " expected=" + safeBounds + " tools=" + Root.Q("board-touch-tools")?.worldBound + " board=" + Root.Q("board-scroll").worldBound);
            Debug.Log("SAFE editor=" + editorBounds + " expected=" + safeBounds);
            Check(Mathf.Abs(editorBounds.xMin - safeBounds.xMin) < 2 && Mathf.Abs(editorBounds.xMax - safeBounds.xMax) < 2 && Mathf.Abs(editorBounds.yMin - safeBounds.yMin) < 2, "safe area inset is applied exactly once");
            foreach (string id in new[] { "save", "undo", "redo", "play", "mobile-levels", "mobile-materials", "mobile-properties", "mobile-more", "status" }) Within(Root.Q(id), safeBounds, id);
            foreach (string id in new[] { "save", "undo", "redo", "play", "mobile-levels", "mobile-materials", "mobile-properties", "mobile-more" })
                Check(Root.Q(id).worldBound.height >= 44 && Root.Q(id).worldBound.width >= 44, "practical simulated touch area " + id);
            ScrollView board = Root.Q<ScrollView>("board-scroll"); Within(board, safeBounds, "board viewport");
            Check(board.contentViewport.worldBound.width >= 150 && board.contentViewport.worldBound.height >= 100, "usable actual board viewport");
            Check(Root.Q("board").Query<Label>(className: "cell").ToList().Count == 81, "all 81 board cells exist");
            List<Scroller> scrollers = Root.Query<Scroller>().ToList();
            Check(scrollers.Count > 0, "actual mobile scrollbars exist");
            foreach (Scroller scroller in scrollers)
            {
                VisualElement slider = scroller.Q(className: "unity-base-slider");
                Check(slider != null, "scrollbar slider exists " + scroller.name);
                VisualElement sliderInput = slider.Q(className: "unity-base-field__input");
                Check(sliderInput != null, "scrollbar slider input exists " + scroller.name);
                Check(slider.resolvedStyle.minHeight.value < 48, "scrollbar slider avoids 48-unit field touch minimum " + scroller.name);
                Check(sliderInput.resolvedStyle.minHeight.value < 40, "scrollbar input avoids 40-unit field touch minimum " + scroller.name);
            }
            bool wide = safeBounds.width >= 940;
            Check(Root.ClassListContains(wide ? "mobile-wide" : safeBounds.height >= safeBounds.width ? "mobile-portrait" : "mobile-landscape"), "layout follows safe usable width and orientation");
            if (wide)
            {
                Within(Root.Q("left-panel"), safeBounds, "wide left panel"); Within(Root.Q("right-panel"), safeBounds, "wide right panel");
                Check(Root.Q("left-panel").worldBound.xMax <= board.worldBound.xMin + 2 && board.worldBound.xMax <= Root.Q("right-panel").worldBound.xMin + 2, "wide panels leave board space");
            }
            VerifyPreserved("base viewport");
        }

        private static void VerifyPanel(string name)
        {
            Check((string)Get("mobilePanel") == name, "panel selection " + name);
            string target = name == "levels" || name == "materials" ? "left-panel" : name == "properties" ? "right-panel" : "mobile-more-panel";
            Within(Root.Q(target), safeBounds, "active panel " + name);
            if (!Root.ClassListContains("mobile-wide"))
            {
                string other = target == "left-panel" ? "right-panel" : "left-panel";
                Check(Root.Q(other).resolvedStyle.display == DisplayStyle.None, "compact panel excludes " + other);
            }
        }

        private static void VerifyModalInput()
        {
            Check(ReferenceEquals(modal, Root.Q("modal")) && ReferenceEquals(input, modal.Q<TextField>("project-name")), "same modal and field survive viewport change");
            Within(modal.Q(className: "dialog"), safeBounds, "modal dialog"); VerifyInput(); VerifyPreserved("modal input");
        }

        private static void VerifyInput()
        {
            VisualElement focus = Root.focusController.focusedElement as VisualElement;
            VerifyPendingInput();
            Check(focus == input || (focus != null && input.Contains(focus)), "editing field keeps focus");
        }

        private static void VerifyPendingInput(bool checkSelection = true)
        {
            Evidence.AppendLine("INPUT raw=" + input.text + " value=" + input.value + " cursor=" + input.cursorIndex + "/" + cursor + " selection=" + input.selectIndex + "/" + selection + " expectedValue=" + pendingValue);
            Check(input.text == pendingText && input.value == pendingValue && (!checkSelection || input.cursorIndex == cursor && input.selectIndex == selection),
                checkSelection ? "uncommitted raw string, committed field value, cursor and text selection survive" : "hidden draft raw string and committed value survive; selection is checked on panel return");
        }

        private static void VerifyPreserved(string context)
        {
            Check(ReferenceEquals(screen.Workspace.Session, session) && session.ExportState() == expectedState && session.IsDirty && session.CanUndo && session.CanRedo && session.SelectedCells.SequenceEqual(new[] { 40, 41 }), "session/history/selection preserved " + context);
            Check((string)Get("layer") == "Block" && (string)Get("brush") == definitionId, "layer and selected tool preserved " + context);
        }

        private static void VerifyAdvancedSession(string context)
        {
            // 작업 페이지 전환은 도구를 해제하지만 문서·선택·이력을 변경해서는 안 된다.
            Check(ReferenceEquals(screen.Workspace.Session, session) && session.ExportState() == expectedState && session.IsDirty && session.CanUndo && session.CanRedo && session.SelectedCells.SequenceEqual(new[] { 40, 41 }),
                "advanced navigation preserves complete session/history/selection " + context);
            Check((string)Get("layer") == "Block", "advanced navigation retains editing layer " + context);
        }

        private static void VerifyAdvancedControl()
        {
            if (AdvancedPages[advancedIndex] == "records") Check(Root.Q<Foldout>("record-management").value, "record management remains expanded for scroll access");
            ScrollView propertiesScroll = Root.Q<ScrollView>("properties");
            VisualElement control = Root.Q(AdvancedControls[advancedIndex]);
            Within(control, safeBounds, "advanced scrolled control " + AdvancedControls[advancedIndex]);
            Within(control, propertiesScroll.contentViewport.worldBound, "advanced property viewport control " + AdvancedControls[advancedIndex]);
            Check(control.enabledInHierarchy, "advanced representative control remains enabled");
            if (propertiesScroll.contentContainer.worldBound.height > propertiesScroll.contentViewport.worldBound.height + 2)
                Check(propertiesScroll.verticalScroller.highValue > 0, "advanced property overflow is scrollable");
        }

        private static bool CommandsIdle() => !(bool)Get("busy") && !(bool)Get("committingCommandInput") && !(bool)Get("commandAwaitingInput");

        private static void TracePendingNumber(string context)
        {
            VisualElement focused = Root.focusController.focusedElement as VisualElement;
            System.Collections.IDictionary cache = (System.Collections.IDictionary)Get("mobilePendingInputs");
            string cachedFields = string.Join(",", cache.Keys.Cast<VisualElement>().Select(element => element.GetType().Name + ":" + element.name));
            string currentState = session.ExportState();
            int difference = 0;
            while (difference < currentState.Length && difference < commandBeforeState.Length && currentState[difference] == commandBeforeState[difference]) difference++;
            string diagnostic = "DIAGNOSTIC " + context + " panel=" + Get("mobilePanel") + " rightDisplay=" + Root.Q("right-panel").resolvedStyle.display +
                " fieldIsCurrent=" + ReferenceEquals(commandNumber, Root.Q<IntegerField>("level-moves")) + " attached=" + Root.Contains(commandNumber) +
                " raw=" + JValue.CreateString(commandNumber.text).ToString(Newtonsoft.Json.Formatting.None) + " value=" + commandNumber.value + " originalValue=" + originalMoves +
                " delayed=" + commandNumber.isDelayed + " focus=" + (focused == null ? "null" : focused.GetType().Name + ":" + focused.name) +
                " fieldHasFocus=" + (focused == commandNumber || focused != null && commandNumber.Contains(focused)) +
                " cacheCount=" + cache.Count + " cacheFields=" + cachedFields + " sessionUnchanged=" + (currentState == commandBeforeState) +
                " dirty=" + session.IsDirty + " undo=" + session.CanUndo + " redo=" + session.CanRedo + " documentMoves=" + session.Get(levelId).Data["moveCount"];
            if (currentState != commandBeforeState)
            {
                int start = Math.Max(0, difference - 60);
                diagnostic += " firstDifference=" + difference + " before=" + commandBeforeState.Substring(start, Math.Min(160, commandBeforeState.Length - start)) +
                    " after=" + currentState.Substring(start, Math.Min(160, currentState.Length - start));
            }
            Evidence.AppendLine(diagnostic); Debug.Log(diagnostic);
        }

        private static void SetRawCommandInput(VisualElement field, string value)
        {
            // 값 변경 이벤트를 일으키지 않고 실제 내부 입력 문자열만 바꿔 지연 확정을 재현한다.
            TextElement text = field.Q(className: "unity-base-field__input").Q<TextElement>();
            ((INotifyValueChanged<string>)text).SetValueWithoutNotify(value);
        }

        private static JObject ReadSavedDocument() => new ContentSnapshotStore(projectFolder).Read().Snapshot.Get(levelId).Data;

        private static string CaptureProjectFiles()
        {
            return string.Join("\n", Directory.GetFiles(projectFolder, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => path + ":" + Convert.ToBase64String(File.ReadAllBytes(path))));
        }

        private static void Within(VisualElement element, Rect bounds, string name)
        {
            Check(element != null, "exists " + name);
            Rect rect = element.worldBound;
            Check(element.resolvedStyle.display != DisplayStyle.None && rect.width > 0 && rect.height > 0 && !float.IsNaN(rect.width) && rect.xMin >= bounds.xMin - 2 && rect.yMin >= bounds.yMin - 2 && rect.xMax <= bounds.xMax + 2 && rect.yMax <= bounds.yMax + 2, "visible safe bounds " + name + " " + rect);
        }

        private static void Capture(string mode)
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D captured = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture; captured.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); captured.Apply();
                HashSet<Color32> colors = new HashSet<Color32>();
                for (int y = 8; y < texture.height; y += 16)
                    for (int x = 8; x < texture.width; x += 16) colors.Add((Color32)captured.GetPixel(x, y));
                Check(colors.Count > 8, "capture has rendered detail " + mode);
                string path = Output + "/mobile-" + sizeIndex + "-" + texture.width + "x" + texture.height + "-" + mode + ".png";
                File.WriteAllBytes(path, captured.EncodeToPNG()); Evidence.AppendLine("CAPTURE " + path + " sampledColors=" + colors.Count + " visual review pending");
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(captured); }
        }

        private static void Advance(int target) { phase = target; next = EditorApplication.timeSinceStartup + 1.5; }
        private static object Get(string name) => typeof(LevelToolScreen).GetField(name, Flags).GetValue(screen);
        private static void Set(string name, object value) => typeof(LevelToolScreen).GetField(name, Flags).SetValue(screen, value);
        private static object Invoke(string name, params object[] arguments) => (typeof(LevelToolScreen).GetMethod(name, Flags) ?? throw new Exception("Missing LevelToolScreen." + name)).Invoke(screen, arguments);
        private static void Click(string name)
        {
            Button button = Root.Q<Button>(name) ?? throw new Exception("Missing button " + name);
            Check(button.enabledInHierarchy, "enabled " + name);
            typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
        }
        private static void PointerClick(string name)
        {
            Button button = Root.Q<Button>(name) ?? throw new Exception("Missing pointer button " + name);
            PointerClick(button, name);
        }
        private static void PointerClick(VisualElement element, string name)
        {
            Check(element.enabledInHierarchy, "pointer control enabled " + name);
            Within(element, safeBounds, "pointer control " + name);
            // Clickable 내부 호출을 우회하지 않고 포인터의 기본 포커스 동작까지 검사한다.
            Event source = new Event { type = EventType.MouseDown, button = 0, mousePosition = element.worldBound.center };
            using (PointerDownEvent evt = PointerDownEvent.GetPooled(source)) { evt.target = element; element.SendEvent(evt); }
            source.type = EventType.MouseUp;
            using (PointerUpEvent evt = PointerUpEvent.GetPooled(source)) { evt.target = element; element.SendEvent(evt); }
        }
        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("FAIL " + message);
            Evidence.AppendLine("PASS " + message); Debug.Log("PASS " + message);
        }
    }
}
