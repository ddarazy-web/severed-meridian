using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LevelAuthoring.Editing;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolTouchVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string Output = "Logs/AndroidLevelToolStage03";
        private static readonly StringBuilder Evidence = new StringBuilder();
        private static bool priorEnabled, done, tapOnly, keyDelivered;
        private static int focusedRun;
        private static string priorClipboard;
        private static TextField modalInput;
        private static Vector2 pinchCoordinate, pinchCenter;
        private static EnterPlayModeOptions priorOptions;
        private static PanelSettings panel;
        private static PanelScaleMode priorScaleMode;
        private static float priorScale;
        private static RenderTexture texture, priorTarget;
        private static LevelToolScreen screen;
        private static AuthoringEditSession session;
        private static string recoveryFolder, projectFolder, levelId, definitionId, baseline;
        private static int phase, exit;
        private static double deadline, next;
        private static VisualElement Root => screen.GetComponent<UIDocument>().rootVisualElement;

        public static void RunTap() => Begin(true);
        public static void Run() => Begin(false);
        public static void RunModalInput() => Begin(false, 1);
        public static void RunInterruptedTouch() => Begin(false, 2);
        public static void RunPinchPivot() => Begin(false, 4);
        public static void RunBodyProperties() => Begin(false, 5);
        public static void RunExplicitReplace() => Begin(false, 6);
        public static void RunCaptureOwnership() => Begin(false, 7);
        public static void RunBodyReplace() => Begin(false, 8);
        public static void RunAdditionalCapture() => Begin(false, 9);
        public static void RunLayerClipboard() => Begin(false, 10);
        public static void RunFinePinch() => Begin(false, 11);

        private static void Begin(bool onlyTap, int focus = 0)
        {
            tapOnly = onlyTap; focusedRun = focus; priorClipboard = GUIUtility.systemCopyBuffer;
            Evidence.Clear(); Directory.CreateDirectory(Output);
            Evidence.AppendLine("Editor Play Mode UI Toolkit touch event replay. Physical Android touch, OS back and device rotation remain unverified.");
            priorEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            priorOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            string run = Path.GetFullPath(Output + "/touch-" + Guid.NewGuid().ToString("N"));
            recoveryFolder = Path.Combine(run, "Recovery"); projectFolder = Path.Combine(run, "Project");
            LevelToolRecoveryIsolation.Configure(recoveryFolder);
            phase = exit = 0; done = false; next = 0;
            deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Tick; EditorApplication.playModeStateChanged += State;
            EditorApplication.EnterPlaymode();
        }

        private static void State(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= State;
            if (panel != null)
            {
                panel.targetTexture = priorTarget; panel.scaleMode = priorScaleMode; panel.scale = priorScale;
            }
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); texture = null; }
            EditorSettings.enterPlayModeOptionsEnabled = priorEnabled;
            EditorSettings.enterPlayModeOptions = priorOptions;
            GUIUtility.systemCopyBuffer = priorClipboard;
            string evidenceFile = tapOnly ? "touch-tap" : focusedRun == 1 ? "touch-modal-input" : focusedRun == 2 ? "touch-interrupted" :
                focusedRun == 4 ? "touch-pinch-pivot" : focusedRun == 5 ? "touch-body-properties" : focusedRun == 6 ? "touch-explicit-replace" :
                focusedRun == 7 ? "touch-capture-ownership" : focusedRun == 8 ? "touch-body-replace" : focusedRun == 9 ? "touch-additional-capture" :
                focusedRun == 10 ? "touch-layer-clipboard" : focusedRun == 11 ? "touch-fine-pinch" : "touch";
            File.WriteAllText(Output + "/" + evidenceFile + "-verification.txt", Evidence.ToString());
            EditorApplication.Exit(exit);
        }

        private static void Tick()
        {
            try
            {
                if (done) return;
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Touch verification timed out at " + phase);
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
                switch (phase)
                {
                    case 0:
                        screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                        if (screen == null || (bool)Get("busy")) return;
                        LevelToolRecoveryIsolation.Verify(screen, recoveryFolder);
                        Call("CloseModal");
                        panel = screen.GetComponent<UIDocument>().panelSettings;
                        priorTarget = panel.targetTexture; priorScaleMode = panel.scaleMode; priorScale = panel.scale;
                        panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1;
                        Check(screen.Workspace.Create(projectFolder, LevelToolDocuments.NewProject()), "isolated touch project created");
                        session = screen.Workspace.Session; levelId = session.SelectedLevelId;
                        Dictionary<string, JObject> definitions = LevelToolDocuments.Definitions(session);
                        definitionId = definitions.First(pair => LevelToolDocuments.Layer(pair.Value) == "Block" && LevelToolDocuments.Size(pair.Value) == 1).Key;
                        Set("layer", "Block"); Set("brush", definitionId); Call("Refresh");
                        baseline = session.Get(levelId).Data.ToString();
                        SetViewport(new Vector2Int(412, 915)); Advance(1); break;
                    case 1:
                        if (focusedRun != 0) { Advance(focusedRun == 1 ? 30 : focusedRun == 2 ? 20 : focusedRun == 4 ? 50 : focusedRun == 5 ? 40 : focusedRun == 6 ? 70 : focusedRun == 7 ? 80 : focusedRun == 8 ? 90 : focusedRun == 9 ? 100 : focusedRun == 10 ? 110 : 120); break; }
                        VerifyTap();
                        if (tapOnly) { Finish(); break; }
                        Advance(2); break;
                    case 2:
                        VerifyCancellation(); Advance(3); break;
                    case 3:
                        VerifyLongTouch(); Advance(4); break;
                    case 4:
                        VerifyTwoFingers(); Advance(16); break;
                    case 16:
                        VerifyReverseRelease(); Advance(5); break;
                    case 5:
                        VerifyZoom(); Advance(8); break;
                    case 8:
                        Click("board-touch-select"); Advance(28); break;
                    case 28:
                        Tap(41);
                        Check(session.SelectedCells.SequenceEqual(new[] { 41 }), "tap after pinch/zoom selects the visible board coordinate");
                        Advance(19); break;
                    case 19:
                        VerifyPan(); Advance(9); break;
                    case 9:
                        Click("board-touch-select"); Click("board-touch-area");
                        Advance(29); break;
                    case 29:
                        Tap(30); Advance(10); break;
                    case 10:
                        Tap(40);
                        Check(session.SelectedCells.OrderBy(item => item).SequenceEqual(new[] { 30, 31, 39, 40 }), "actual area tool selects rectangle without Shift");
                        Unchanged("area selection preserves document and Undo"); Advance(11); break;
                    case 11:
                        Root.Q<Toggle>("board-touch-add").value = true; Tap(50); Advance(14); break;
                    case 14:
                        Tap(60);
                        Check(session.SelectedCells.OrderBy(item => item).SequenceEqual(new[] { 30, 31, 39, 40, 50, 51, 59, 60 }), "additional area selection accumulates both rectangles");
                        Root.Q<Toggle>("board-touch-add").value = false;
                        Click("board-touch-cancel"); Click("board-actions");
                        Check(Root.Q("modal") != null, "selected action button opens actual menu"); Call("CloseModal");
                        Advance(6); break;
                    case 6:
                        Click("board-touch-select"); Set("brush", definitionId); Call("Refresh"); Advance(15); break;
                    case 15:
                        VerifyRotationStart(); Advance(7); break;
                    case 7:
                        VerifyRotationEnd(); Root.Q<ScrollView>("board-scroll").ScrollTo(Root.Q("cell-40")); Advance(12); break;
                    case 12:
                        VerifyMouse(); Advance(13); break;
                    case 13:
                        VerifyExplicitReplaceStart(); Advance(27); break;
                    case 27:
                        VerifyExplicitReplaceEnd(); Advance(17); break;
                    case 17:
                        VerifyMenuCommands(); Advance(18); break;
                    case 18:
                        Check(JToken.DeepEquals(savedDocument, screen.Workspace.Session.Get(levelId).Data), "save/reopen preserves touch and mouse document semantics");
                        Finish(); break;
                    case 20:
                        StartInterruptedTouch(); Call("OnApplicationPause", true); Call("OnApplicationPause", false);
                        VerifyFreshPointer(2, "pause without prior Up permits a new touch"); Advance(21); break;
                    case 21:
                        StartInterruptedTouch(); Call("OnApplicationFocus", false); Call("OnApplicationFocus", true);
                        VerifyFreshPointer(3, "focus loss without prior Up permits a new touch"); Advance(22); break;
                    case 22:
                        StartInterruptedTouch();
                        using (PointerCaptureOutEvent evt = PointerCaptureOutEvent.GetPooled(Root, null, 1)) { Root.SendEvent(evt); }
                        VerifyFreshPointer(4, "capture loss without prior Up permits a new touch"); Advance(23); break;
                    case 23:
                        Click("board-touch-pan"); Click("board-zoom-in"); Click("board-zoom-in");
                        Advance(24); break;
                    case 24:
                        Root.Q<ScrollView>("board-scroll").ScrollTo(Root.Q("cell-40")); Advance(25); break;
                    case 25:
                        VerifyPanInterruptions(); Advance(26); break;
                    case 26:
                        VerifyThirdFinger(); Finish(); break;
                    case 30:
                        Click("board-actions");
                        modalInput = new TextField { name = "modal-native-input", value = "ABC" };
                        Root.Q("board-actions-menu").Add(modalInput);
                        Advance(31); break;
                    case 31:
                        modalInput.Focus(); Advance(36); break;
                    case 36:
                        VisualElement nativeFocus = Root.focusController.focusedElement as VisualElement;
                        Check(nativeFocus == modalInput || (nativeFocus != null && modalInput.Contains(nativeFocus)), "modal field has actual native focus after focus layout");
                        modalInput.SelectRange(1, 2); SendModalKey(KeyCode.Delete, EventModifiers.FunctionKey); Advance(32); break;
                    case 32:
                        Check(modalInput.value == "AC", "actual modal text field receives native Delete"); Unchanged("modal Delete preserves board document");
                        modalInput.SelectAll(); SendModalKey(KeyCode.A, EventModifiers.Control);
                        SendModalKey(KeyCode.C, EventModifiers.Control); Advance(33); break;
                    case 33:
                        Check(GUIUtility.systemCopyBuffer == "AC", "actual modal text field receives native Ctrl+C");
                        modalInput.value = ""; modalInput.SelectRange(0, 0); SendModalKey(KeyCode.V, EventModifiers.Control); Advance(34); break;
                    case 34:
                        Check(modalInput.value == "AC", "actual modal text field receives native Ctrl+V");
                        modalInput.SelectAll(); SendModalKey(KeyCode.X, EventModifiers.Control); Advance(35); break;
                    case 35:
                        Check(modalInput.value == "" && GUIUtility.systemCopyBuffer == "AC", "actual modal text field receives native Ctrl+X");
                        Unchanged("modal clipboard editing preserves board document and Undo"); Finish(); break;
                    case 40:
                        Click("board-touch-select"); Set("layer", "Obstacle");
                        Dictionary<string, JObject> bodyDefinitions = LevelToolDocuments.Definitions(session);
                        string bodyDefinition = bodyDefinitions.First(pair => LevelToolDocuments.Layer(pair.Value) == "Obstacle" &&
                            LevelToolDocuments.Size(pair.Value) == 2 && (int)pair.Value["placement"]["maxDurability"] > 1).Key;
                        session.Apply("2×2 속성 검사 준비", docs => LevelDocumentEditing.Place(docs[levelId].Data, bodyDefinitions, bodyDefinition, 3, 3, false));
                        savedDocument = session.Get(levelId).Data; Call("Refresh"); Advance(41); break;
                    case 41:
                        VerifyBodySelection(30); Advance(42); break;
                    case 42:
                        VerifyBodySelection(31); Advance(43); break;
                    case 43:
                        VerifyBodySelection(39); Advance(44); break;
                    case 44:
                        VerifyBodySelection(40);
                        Click("board-actions"); Click("board-properties"); Advance(45); break;
                    case 45:
                        Root.Q<ScrollView>("properties").ScrollTo(Root.Q<IntegerField>("selected-durability")); Advance(46); break;
                    case 46:
                        IntegerField durability = Root.Q<IntegerField>("selected-durability");
                        Check(Root.Q("right-panel").resolvedStyle.display != DisplayStyle.None && durability.worldBound.width > 0 &&
                            Root.Q<ScrollView>("properties").contentViewport.worldBound.Contains(durability.worldBound.center),
                            "body action menu opens reachable actual durability control in properties panel");
                        int originalDurability = durability.value; durability.value = originalDurability - 1;
                        Check((int)session.Get(levelId).Data["elements"].Single()["durability"] == originalDurability - 1, "footprint selection edits one body through actual durability field");
                        session.Undo(); Check(JToken.DeepEquals(savedDocument, session.Get(levelId).Data), "body property edit takes one shared Undo"); Finish(); break;
                    case 50:
                        Click("board-touch-pan"); Click("board-zoom-in"); Click("board-zoom-in"); Advance(51); break;
                    case 51:
                        DiagnoseBoardGeometry("before ScrollTo");
                        Root.Q<ScrollView>("board-scroll").ScrollTo(Root.Q("cell-40"));
                        DiagnoseBoardGeometry("after ScrollTo"); Advance(52); break;
                    case 52:
                        VerifyRepeatedPinchStart(); Advance(53); break;
                    case 53:
                        DiagnoseBoardGeometry("after repeated pinch layout");
                        Vector2 actualPivot = Root.Q("board").LocalToWorld(pinchCoordinate * (float)Get("cellSize"));
                        Evidence.AppendLine("PINCH PIVOT expected=" + pinchCenter + " actual=" + actualPivot + " coordinate=" + pinchCoordinate + " cellSize=" + Get("cellSize"));
                        Check(Vector2.Distance(actualPivot, pinchCenter) < 2, "repeated same-frame pinch moves retain final midpoint board coordinate after actual layout");
                        Unchanged("repeated pinch transforms do not edit document or Undo"); Finish(); break;
                    case 70:
                        Dictionary<string, JObject> replaceDefinitions = LevelToolDocuments.Definitions(session);
                        session.Apply("명시 교체 검사 준비", docs => LevelDocumentEditing.Place(docs[levelId].Data, replaceDefinitions, definitionId, 4, 4, false, color: "Type1"));
                        savedDocument = session.Get(levelId).Data; Call("Refresh"); Advance(71); break;
                    case 71:
                        VerifyExplicitReplaceStart(); Advance(72); break;
                    case 72:
                        VerifyExplicitReplaceEnd(); Finish(); break;
                    case 80:
                        VerifyChildCaptureOwnership(); Advance(81); break;
                    case 81:
                        StartInterruptedTouch();
                        using (PointerCaptureOutEvent evt = PointerCaptureOutEvent.GetPooled(Root, null, 1)) { Root.SendEvent(evt); }
                        Touch(Root.Q("board"), EventType.MouseUp, 1, Root.Q("cell-40").worldBound.center);
                        Unchanged("actual root capture loss cancels pending touch release");
                        VerifyFreshPointer(2, "root capture loss permits next fresh tap"); Finish(); break;
                    case 90:
                        Click("board-touch-select"); Set("layer", "Obstacle");
                        Dictionary<string, JObject> bodyReplaceDefinitions = LevelToolDocuments.Definitions(session);
                        string originalBody = bodyReplaceDefinitions.First(pair => LevelToolDocuments.Layer(pair.Value) == "Obstacle" && LevelToolDocuments.Size(pair.Value) == 2).Key;
                        definitionId = bodyReplaceDefinitions.First(pair => LevelToolDocuments.Layer(pair.Value) == "Obstacle" && LevelToolDocuments.Size(pair.Value) == 1).Key;
                        session.Apply("2×2 교체 검사 준비", docs => LevelDocumentEditing.Place(docs[levelId].Data, bodyReplaceDefinitions, originalBody, 3, 3, false));
                        savedDocument = session.Get(levelId).Data; Call("Refresh"); Advance(91); break;
                    case 91:
                        Tap(30); Check(session.SelectedCells.Length == 4, "body replacement fixture selects complete 2×2 body");
                        Set("brush", definitionId); Call("Refresh"); Advance(92); break;
                    case 92:
                        Click("board-actions"); Click("board-replace");
                        JArray bodyReplacement = (JArray)session.Get(levelId).Data["elements"];
                        Check(bodyReplacement.Count == 1 && (string)bodyReplacement[0]["definitionId"] == definitionId &&
                            (int)bodyReplacement[0]["coordinate"]["row"] == 3 && (int)bodyReplacement[0]["coordinate"]["column"] == 3,
                            "actual menu replaces one 2×2 body with one 1×1 element at original anchor");
                        session.Undo(); Check(JToken.DeepEquals(savedDocument, session.Get(levelId).Data), "body replacement has one Undo restoring original body identity");
                        Finish(); break;
                    case 100:
                        VerifyAdditionalCapture(); Finish(); break;
                    case 110:
                        Click("board-touch-select");
                        Dictionary<string, JObject> clipboardDefinitions = LevelToolDocuments.Definitions(session);
                        session.Apply("클립보드 층 검사 준비", docs => LevelDocumentEditing.Place(docs[levelId].Data, clipboardDefinitions, definitionId, 4, 4, false));
                        savedDocument = session.Get(levelId).Data; Call("Refresh"); Advance(111); break;
                    case 111:
                        Tap(40); Click("board-actions"); Click("board-command-Copy");
                        Check(JToken.DeepEquals(savedDocument, session.Get(levelId).Data), "actual menu Copy keeps Block source document");
                        Set("layer", "Dust"); Call("Refresh"); Advance(112); break;
                    case 112:
                        Click("board-actions");
                        Check(!Root.Q<Button>("board-command-Paste").enabledInHierarchy, "Block clipboard Paste is disabled in Dust action menu");
                        Check(JToken.DeepEquals(savedDocument, session.Get(levelId).Data), "clipboard layer mismatch preserves document");
                        Click("board-close-actions"); Set("layer", "Block"); Call("Refresh"); Advance(113); break;
                    case 113:
                        Tap(41); Click("board-actions"); Click("board-command-Paste");
                        JArray clipboardResult = (JArray)session.Get(levelId).Data["elements"];
                        Check(clipboardResult.Count == 2 && clipboardResult.Any(item => (string)item["definitionId"] == definitionId &&
                            (int)item["coordinate"]["row"] == 4 && (int)item["coordinate"]["column"] == 5),
                            "returning to Block restores actual menu Paste into selected target");
                        session.Undo(); Check(JToken.DeepEquals(savedDocument, session.Get(levelId).Data), "same-layer paste is one shared Undo"); Finish(); break;
                    case 120:
                        Click("board-touch-pan"); Click("board-zoom-in"); Click("board-zoom-in");
                        Click("board-zoom-in"); Click("board-zoom-in"); Advance(121); break;
                    case 121:
                        Root.Q<ScrollView>("board-scroll").ScrollTo(Root.Q("cell-40")); Advance(122); break;
                    case 122:
                        VerifyFinePinch(); Advance(123); break;
                    case 123:
                        DiagnoseBoardGeometry("after fine pinch layout");
                        ScrollView fineScroll = Root.Q<ScrollView>("board-scroll");
                        Check(fineScroll.horizontalScroller.highValue > 0 && fineScroll.verticalScroller.highValue > 0,
                            "fine pinch result can scroll both axes to retain its midpoint");
                        Vector2 finePivot = Root.Q("board").LocalToWorld(pinchCoordinate * (float)Get("cellSize"));
                        Evidence.AppendLine("FINE PINCH PIVOT expected=" + pinchCenter + " actual=" + finePivot + " coordinate=" + pinchCoordinate + " cellSize=" + Get("cellSize"));
                        Check(Vector2.Distance(finePivot, pinchCenter) < 2, "fine pinch retains midpoint coordinate after actual layout");
                        Unchanged("fine pinch preserves document and Undo"); Finish(); break;
                }
            }
            catch (Exception error)
            {
                exit = 1; done = true; Evidence.AppendLine("FAIL " + error); Debug.LogException(error);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode(); else State(PlayModeStateChange.EnteredEditMode);
            }
        }

        private static void VerifyTap()
        {
            VisualElement cell = Root.Q("cell-40");
            Check(cell != null && cell.worldBound.width > 0, "actual board cell is attached and laid out");
            Check(Root.Q<ScrollView>("board-scroll").contentViewport.worldBound.Contains(cell.worldBound.center), "initial touch target center lies inside actual viewport");
            DiagnoseTouch("before Down", cell);
            Touch(cell, EventType.MouseDown, 1, cell.worldBound.center);
            DiagnoseTouch("after Down", cell);
            Check(session.Get(levelId).Data.ToString() == baseline && !session.IsDirty && !session.CanUndo,
                "actual touch PointerDown with brush leaves document, dirty and Undo unchanged");
            Touch(cell, EventType.MouseUp, 1, cell.worldBound.center);
            DiagnoseTouch("after Up", Root.Q("cell-40"));
            JToken[] placements = session.Get(levelId).Data["elements"].Where(item => (string)item["definitionId"] == definitionId &&
                (int)item["coordinate"]["row"] == 4 && (int)item["coordinate"]["column"] == 4).ToArray();
            Check(placements.Length == 1 && session.IsDirty && session.CanUndo, "actual touch PointerUp commits exactly one placement at visible cell");
            session.Undo();
            Check(session.Get(levelId).Data.ToString() == baseline && !session.CanUndo && !session.IsDirty, "one Undo restores entire pre-tap document");
            session.Redo();
            Check(session.Get(levelId).Data["elements"].Any(item => (string)item["instanceId"] == (string)placements[0]["instanceId"]), "Redo restores placement identity");
            session.Discard(); Call("Refresh");
        }

        private static void VerifyCancellation()
        {
            VisualElement cell = Root.Q("cell-40"); Vector2 point = cell.worldBound.center;
            Touch(cell, EventType.MouseDown, 1, point);
            Touch(Root.Q("board"), EventType.MouseDrag, 1, point + new Vector2(45, 0));
            Touch(Root.Q("board"), EventType.MouseUp, 1, point + new Vector2(45, 0));
            Unchanged("movement beyond tap tolerance cancels placement");
            cell = Root.Q("cell-40"); point = cell.worldBound.center;
            Touch(cell, EventType.MouseDown, 1, point);
            using (PointerCancelEvent evt = PointerCancelEvent.GetPooled())
            {
                ConfigureTouch(evt, 1); evt.target = Root.Q("board"); Root.Q("board").SendEvent(evt);
            }
            Touch(Root.Q("board"), EventType.MouseUp, 1, point); Unchanged("pointer cancel suppresses later Up");
            Touch(cell, EventType.MouseDown, 1, point);
            using (PointerCaptureOutEvent evt = PointerCaptureOutEvent.GetPooled(Root, null, 1)) { Root.SendEvent(evt); }
            Touch(Root.Q("board"), EventType.MouseUp, 1, point); Unchanged("pointer capture loss suppresses later Up");
            Touch(cell, EventType.MouseDown, 1, point); Call("OnApplicationPause", true);
            Touch(Root.Q("board"), EventType.MouseUp, 1, point); Unchanged("application pause cancels pending touch");
            Call("OnApplicationPause", false);
            Touch(cell, EventType.MouseDown, 1, point); Click("menu-file");
            Touch(Root.Q("board"), EventType.MouseUp, 1, point); Unchanged("popup opening cancels pending touch");
            Call("CloseModal");
        }

        private static void VerifyLongTouch()
        {
            VisualElement cell = Root.Q("cell-40"); Vector2 point = cell.worldBound.center;
            Touch(cell, EventType.MouseDown, 1, point);
            Call("UpdateBoardTouch", Time.unscaledTime + .59f);
            Check(Root.Q("modal") == null, "before 0.6 seconds long touch has no menu");
            Unchanged("before long touch threshold");
            Call("UpdateBoardTouch", Time.unscaledTime + .61f);
            Check(Root.Q("modal") != null, "after 0.6 seconds long touch opens actual action menu");
            Unchanged("long touch menu opening does not edit");
            Touch(Root.Q("board"), EventType.MouseUp, 1, point);
            Unchanged("long touch release does not place"); Call("CloseModal");
        }

        private static void VerifyTwoFingers()
        {
            VisualElement cell = Root.Q("cell-40"); Vector2 point = cell.worldBound.center;
            float before = (float)Get("cellSize");
            Touch(cell, EventType.MouseDown, 1, point);
            Touch(Root.Q("cell-41"), EventType.MouseDown, 2, point + new Vector2(60, 0));
            Touch(Root.Q("board"), EventType.MouseDrag, 2, point + new Vector2(90, 0));
            Touch(Root.Q("board"), EventType.MouseUp, 2, point + new Vector2(90, 0));
            Touch(Root.Q("board"), EventType.MouseUp, 1, point);
            Unchanged("two fingers cancel first pending tap and remaining finger release");
            Check((float)Get("cellSize") > before, "two finger outward movement actually increases board scale");
        }

        private static void VerifyReverseRelease()
        {
            VisualElement cell = Root.Q("cell-40"); Vector2 point = cell.worldBound.center;
            Touch(cell, EventType.MouseDown, 1, point);
            Touch(Root.Q("cell-41"), EventType.MouseDown, 2, point + new Vector2(60, 0));
            Touch(Root.Q("board"), EventType.MouseUp, 1, point);
            Touch(Root.Q("board"), EventType.MouseUp, 2, point + new Vector2(60, 0));
            Unchanged("two finger reversed release order also never taps");
        }

        private static JObject savedDocument;

        private static void VerifyZoom()
        {
            float size = (float)Get("cellSize");
            Click("board-zoom-in"); Check((float)Get("cellSize") > size, "zoom plus changes actual board scale");
            Click("board-zoom-out"); Unchanged("zoom buttons preserve document and Undo");
        }

        private static void VerifyPan()
        {
            Click("board-touch-pan");
            VisualElement cell = Root.Q("cell-40"); Vector2 point = cell.worldBound.center;
            Touch(cell, EventType.MouseDown, 1, point);
            Touch(Root.Q("board"), EventType.MouseDrag, 1, point + new Vector2(-50, -50));
            Touch(Root.Q("board"), EventType.MouseUp, 1, point + new Vector2(-50, -50));
            Unchanged("explicit one finger pan tool does not place");
        }

        private static void VerifyRotationStart()
        {
            VisualElement cell = Root.Q("cell-40"); Touch(cell, EventType.MouseDown, 1, cell.worldBound.center);
            Check(cell.ClassListContains("touch-preview"), "rotation fixture starts an actual pending touch preview");
            SetViewport(new Vector2Int(915, 412));
        }

        private static void VerifyRotationEnd()
        {
            Touch(Root.Q("board"), EventType.MouseUp, 1, Root.Q("cell-40").worldBound.center);
            Unchanged("actual viewport rotation cancels pending touch");
            Check((string)Get("layer") == "Block" && (string)Get("brush") == definitionId, "rotation retains layer and brush");
        }

        private static void VerifyMouse()
        {
            VisualElement cell = Root.Q("cell-40");
            Check(Root.Q<ScrollView>("board-scroll").contentViewport.worldBound.Contains(cell.worldBound.center), "mouse regression uses visible scrolled cell");
            Event source = new Event { type = EventType.MouseDown, button = 0, mousePosition = cell.worldBound.center };
            using (PointerDownEvent evt = PointerDownEvent.GetPooled(source)) { evt.target = cell; cell.SendEvent(evt); }
            Check(session.Get(levelId).Data["elements"].Any(item => (string)item["definitionId"] == definitionId &&
                (int)item["coordinate"]["row"] == 4 && (int)item["coordinate"]["column"] == 4), "Windows mouse still commits on PointerDown");
            source.type = EventType.MouseUp;
            using (PointerUpEvent evt = PointerUpEvent.GetPooled(source)) { evt.target = Root.Q("board"); Root.Q("board").SendEvent(evt); }
            savedDocument = session.Get(levelId).Data;
            session.Undo(); Check(session.Get(levelId).Data.ToString() == baseline, "mouse placement is one shared Undo");
            session.Redo(); Check(JToken.DeepEquals(savedDocument, session.Get(levelId).Data), "mouse Redo restores placement and references");
            Call("Refresh");
        }

        private static void VerifyExplicitReplaceStart()
        {
            Set("color", "Type2"); Tap(40);
            Check(JToken.DeepEquals(savedDocument, session.Get(levelId).Data), "ordinary occupied tap preserves original placement");
            Root.Q<Toggle>("board-touch-replace").value = true;
        }

        private static void VerifyExplicitReplaceEnd()
        {
            Tap(40);
            Check(session.Get(levelId).Data["elements"].Any(item => (string)item["definitionId"] == definitionId &&
                (int)item["coordinate"]["row"] == 4 && (int)item["coordinate"]["column"] == 4 && (string)item["color"] == "Type2"),
                "explicit replacement toggle permits occupied current-layer touch edit");
            session.Undo(); Check(JToken.DeepEquals(savedDocument, session.Get(levelId).Data), "replacement takes one Undo and restores original document");
            Root.Q<Toggle>("board-touch-replace").value = false; Set("color", "Type1"); Call("Refresh");
        }

        private static void VerifyMenuCommands()
        {
            Click("board-actions"); Click("board-command-Copy");
            Check(JToken.DeepEquals(savedDocument, session.Get(levelId).Data), "actual action menu Copy preserves document");
            Click("board-actions"); Click("board-command-Delete");
            Check(session.Get(levelId).Data.ToString() == baseline, "actual action menu Delete erases selected current layer");
            Click("undo"); Check(JToken.DeepEquals(savedDocument, session.Get(levelId).Data), "action menu Delete is one common Undo");
            screen.Workspace.Save(); Check(!session.IsDirty, "touch workspace saves through common storage");
            Check(screen.Workspace.Open(projectFolder, "Cancel"), "touch workspace reopens through common storage");
            Call("Refresh");
        }

        private static void Tap(int index)
        {
            VisualElement cell = Root.Q("cell-" + index);
            Check(cell != null && cell.worldBound.width > 0, "visible tap cell " + index);
            Vector2 point = cell.worldBound.center;
            Check(Root.Q<ScrollView>("board-scroll").contentViewport.worldBound.Contains(point), "tap center is inside viewport " + index);
            Touch(cell, EventType.MouseDown, 1, point); Touch(cell, EventType.MouseUp, 1, point);
        }

        private static void DiagnoseTouch(string context, VisualElement cell)
        {
            string text = "TOUCH DIAGNOSTIC " + context + " cellBounds=" + cell.worldBound +
                " viewport=" + Root.Q<ScrollView>("board-scroll").contentViewport.worldBound +
                " boardBounds=" + Root.Q("board").worldBound + " scroll=" + Root.Q<ScrollView>("board-scroll").scrollOffset +
                " primary=" + Get("boardTouchPrimary") + " candidate=" + Get("boardTouchCell") + " cancelled=" + Get("boardTouchCancelled") +
                " pointerCount=" + ((System.Collections.IDictionary)Get("boardTouchPoints")).Count + " brush=" + Get("brush") +
                " time=" + Time.unscaledTime + " status=" + Root.Q<Label>("status").text +
                " placements=" + session.Get(levelId).Data["elements"].ToString(Newtonsoft.Json.Formatting.None);
            Evidence.AppendLine(text); Debug.Log(text);
        }

        private static void StartInterruptedTouch()
        {
            VisualElement cell = Root.Q("cell-40"); Touch(cell, EventType.MouseDown, 1, cell.worldBound.center);
            Check(cell.ClassListContains("touch-preview"), "interruption starts actual pending touch");
        }

        private static void VerifyFreshPointer(int pointerId, string context)
        {
            Unchanged("interruption itself preserves document");
            VisualElement cell = Root.Q("cell-40"); Vector2 point = cell.worldBound.center;
            Touch(cell, EventType.MouseDown, pointerId, point); Touch(cell, EventType.MouseUp, pointerId, point);
            Check(session.IsDirty && session.CanUndo && session.Get(levelId).Data["elements"].Any(item =>
                (int)item["coordinate"]["row"] == 4 && (int)item["coordinate"]["column"] == 4), context);
            session.Undo(); Unchanged("fresh tap commits exactly one shared Undo"); session.Discard(); Call("Refresh");
        }

        private static void VerifyPanInterruptions()
        {
            VisualElement cell = Root.Q("cell-40"); Vector2 point = cell.worldBound.center;
            Touch(cell, EventType.MouseDown, 1, point); Touch(Root.Q("board"), EventType.MouseDown, 2, point + new Vector2(40, 0));
            Touch(Root.Q("board"), EventType.MouseDrag, 2, point + new Vector2(60, 0));
            Touch(Root.Q("board"), EventType.MouseUp, 1, point);
            Vector2 before = Root.Q<ScrollView>("board-scroll").scrollOffset;
            Touch(Root.Q("board"), EventType.MouseDrag, 2, point + new Vector2(65, 0));
            Check((Root.Q<ScrollView>("board-scroll").scrollOffset - before).sqrMagnitude < .01f, "surviving finger after pinch release does not move or snap viewport");
            Touch(Root.Q("board"), EventType.MouseUp, 2, point + new Vector2(65, 0)); Unchanged("pinch survivor never edits");
            cell = Root.Q("cell-40"); point = cell.worldBound.center;
            Touch(cell, EventType.MouseDown, 1, point); Click("board-touch-cancel");
            before = Root.Q<ScrollView>("board-scroll").scrollOffset;
            Touch(Root.Q("board"), EventType.MouseDrag, 1, point + new Vector2(-40, -40));
            Check((Root.Q<ScrollView>("board-scroll").scrollOffset - before).sqrMagnitude < .01f, "cancelled pan ignores subsequent movement");
            Touch(Root.Q("board"), EventType.MouseUp, 1, point + new Vector2(-40, -40)); Unchanged("cancelled pan never edits");
        }

        private static void VerifyThirdFinger()
        {
            Click("board-touch-select"); Set("brush", definitionId);
            VisualElement cell = Root.Q("cell-40"); Vector2 point = cell.worldBound.center;
            Touch(cell, EventType.MouseDown, 1, point);
            Touch(Root.Q("board"), EventType.MouseDown, 2, point + new Vector2(20, 0));
            Touch(Root.Q("board"), EventType.MouseDown, 3, point + new Vector2(40, 0));
            Touch(Root.Q("board"), EventType.MouseUp, 2, point + new Vector2(20, 0));
            Touch(Root.Q("board"), EventType.MouseUp, 1, point);
            Touch(Root.Q("board"), EventType.MouseUp, 3, point + new Vector2(40, 0));
            Unchanged("three finger transitions never commit a tap");
        }

        private static void SendModalKey(KeyCode key, EventModifiers modifiers)
        {
            VisualElement target = Root.focusController.focusedElement as VisualElement ?? Root;
            if (target is TextField || target is IntegerField)
                target = target.Q<TextElement>("unity-text-input") ?? target.Query<TextElement>().ToList().Last();
            keyDelivered = false;
            EventCallback<KeyDownEvent> probe = _ => keyDelivered = true;
            target.RegisterCallback(probe, TrickleDown.TrickleDown);
            Event source = new Event { type = EventType.KeyDown, keyCode = key, modifiers = modifiers };
            string before = "MODAL INPUT before " + key + " value=" + modalInput.value + " raw=" + modalInput.text + " selection=" + modalInput.cursorIndex + "/" + modalInput.selectIndex +
                " target=" + target.GetType().Name + ":" + target.name + " focus=" + Root.focusController.focusedElement.GetType().Name;
            Evidence.AppendLine(before); Debug.Log(before);
            using (KeyDownEvent evt = KeyDownEvent.GetPooled(source)) { evt.target = target; target.SendEvent(evt); }
            string after = "MODAL INPUT after " + key + " value=" + modalInput.value + " raw=" + modalInput.text + " selection=" + modalInput.cursorIndex + "/" + modalInput.selectIndex;
            Evidence.AppendLine(after); Debug.Log(after);
            target.UnregisterCallback(probe, TrickleDown.TrickleDown);
            Check(keyDelivered, "modal native input owns " + modifiers + "+" + key);
            source.type = EventType.KeyUp;
            using (KeyUpEvent evt = KeyUpEvent.GetPooled(source)) { evt.target = target; target.SendEvent(evt); }
        }

        private static void VerifyBodySelection(int index)
        {
            Tap(index);
            Check(session.SelectedCells.OrderBy(item => item).SequenceEqual(new[] { 30, 31, 39, 40 }), "2×2 occupied cell selects one complete body " + index);
            Check(Root.Q<IntegerField>("selected-durability") != null, "complete body selection exposes actual durability inspector " + index);
            Check(JToken.DeepEquals(savedDocument, session.Get(levelId).Data), "body selection preserves full document " + index);
        }

        private static void VerifyRepeatedPinchStart()
        {
            VisualElement cell = Root.Q("cell-40"); Vector2 point = cell.worldBound.center;
            pinchCenter = point + new Vector2(20, 0);
            DiagnoseBoardGeometry("before repeated pinch");
            Check(Root.Q<ScrollView>("board-scroll").contentViewport.worldBound.Contains(pinchCenter), "pinch fixture midpoint is in actual viewport");
            pinchCoordinate = Root.Q("board").WorldToLocal(pinchCenter) / (float)Get("cellSize");
            float before = (float)Get("cellSize");
            Touch(cell, EventType.MouseDown, 1, point); Touch(Root.Q("board"), EventType.MouseDown, 2, point + new Vector2(40, 0));
            Touch(Root.Q("board"), EventType.MouseDrag, 2, point + new Vector2(60, 0));
            Touch(Root.Q("board"), EventType.MouseDrag, 1, point - new Vector2(20, 0));
            Touch(Root.Q("board"), EventType.MouseUp, 2, point + new Vector2(60, 0));
            Touch(Root.Q("board"), EventType.MouseUp, 1, point - new Vector2(20, 0));
            Check((float)Get("cellSize") > before, "repeated pinch fixture actually expands board scale");
            Unchanged("same-frame pinch moves preserve document");
        }

        private static void DiagnoseBoardGeometry(string context)
        {
            VisualElement board = Root.Q("board"); ScrollView scroll = Root.Q<ScrollView>("board-scroll");
            string text = "BOARD GEOMETRY " + context + " cell40=" + Root.Q("cell-40").worldBound + " viewport=" + scroll.contentViewport.worldBound +
                " board=" + board.worldBound + " layout=" + board.layout + " resolvedWidth=" + board.resolvedStyle.width +
                " styleWidth=" + board.style.width + " scroll=" + scroll.scrollOffset + " scrollLimits=" + scroll.horizontalScroller.highValue + "/" + scroll.verticalScroller.highValue +
                " cellSize=" + Get("cellSize") + " zoomPending=" + Get("boardZoomPending") + " zoomCoordinate=" + Get("boardZoomCoordinate") + " zoomPivot=" + Get("boardZoomPivot");
            Evidence.AppendLine(text); Debug.Log(text);
        }

        private static void VerifyAdditionalCapture()
        {
            VisualElement cell = Root.Q("cell-40"); Vector2 point = cell.worldBound.center;
            Touch(cell, EventType.MouseDown, 1, point); Check(Root.HasPointerCapture(1), "first touch has actual root capture");
            Touch(Root.Q("cell-41"), EventType.MouseDown, 2, point + new Vector2(20, 0));
            Check(Root.HasPointerCapture(2), "second touch also has actual root capture");
            Touch(Root.Q("cell-42"), EventType.MouseDown, 3, point + new Vector2(40, 0));
            Check(Root.HasPointerCapture(3), "third touch also has actual root capture");
            Unchanged("additional captured fingers preserve document");
            VisualElement outside = Root.panel.visualTree;
            Check(outside != Root && !Root.Contains(outside), "pointer release fixture targets panel outside document root");
            Touch(outside, EventType.MouseUp, 2, new Vector2(-30, -30));
            Touch(outside, EventType.MouseUp, 3, new Vector2(-40, -40));
            Touch(outside, EventType.MouseUp, 1, new Vector2(-50, -50));
            Check(!Root.HasPointerCapture(1) && !Root.HasPointerCapture(2) && !Root.HasPointerCapture(3), "releases outside document root release all actual captures");
            Unchanged("outside releases never commit cancelled taps");
            VerifyFreshPointer(4, "all fingers released outside permit next fresh tap");
        }

        private static void VerifyFinePinch()
        {
            VisualElement board = Root.Q("board"); ScrollView scroll = Root.Q<ScrollView>("board-scroll");
            Rect viewport = scroll.contentViewport.worldBound; pinchCenter = viewport.center;
            DiagnoseBoardGeometry("before fine pinch");
            Check(scroll.horizontalScroller.highValue > 0 && scroll.verticalScroller.highValue > 0,
                "fine pinch fixture can scroll both axes to retain its midpoint");
            Vector2 first = pinchCenter - new Vector2(100, 0), second = pinchCenter + new Vector2(100, 0);
            Check(viewport.Contains(first) && viewport.Contains(second) && board.worldBound.Contains(first) && board.worldBound.Contains(second),
                "fine pinch starts with two actual board/viewport points 200 pixels apart");
            float before = (float)Get("cellSize"); pinchCoordinate = board.WorldToLocal(pinchCenter) / before;
            Touch(board, EventType.MouseDown, 1, first); Touch(board, EventType.MouseDown, 2, second);
            for (int step = 1; step <= 20; step++)
            {
                Touch(board, EventType.MouseDrag, 2, second + new Vector2(step, 0));
                Touch(board, EventType.MouseDrag, 1, first - new Vector2(step, 0));
            }
            Touch(board, EventType.MouseUp, 2, second + new Vector2(20, 0));
            Touch(board, EventType.MouseUp, 1, first - new Vector2(20, 0));
            float expected = Mathf.Clamp(Mathf.Round(before * 1.2f), 32, 144);
            string text = "FINE PINCH before=" + before + " actual=" + Get("cellSize") + " expected=" + expected + " distance=200→240 moves=40";
            Evidence.AppendLine(text); Debug.Log(text);
            Check(Mathf.Abs((float)Get("cellSize") - expected) < .01f, "40 one-pixel pinch moves accumulate the full 20 percent zoom");
            Unchanged("fine pinch event replay never edits document");
        }

        private static void VerifyChildCaptureOwnership()
        {
            VisualElement cell = Root.Q("cell-40"); Vector2 point = cell.worldBound.center;
            Touch(cell, EventType.MouseDown, 1, point);
            Check(cell.ClassListContains("touch-preview"), "root-owned touch starts before old child capture-out");
            VisualElement oldChild = Root.Q("cell-41");
            using (PointerCaptureOutEvent evt = PointerCaptureOutEvent.GetPooled(oldChild, Root, 1)) { oldChild.SendEvent(evt); }
            Check(cell.ClassListContains("touch-preview"), "bubbled capture loss from old child preserves root-owned touch");
            Unchanged("old child capture loss does not edit document");
            Touch(cell, EventType.MouseUp, 1, point);
            Check(session.IsDirty && session.Get(levelId).Data["elements"].Any(item => (int)item["coordinate"]["row"] == 4 &&
                (int)item["coordinate"]["column"] == 4), "root-owned touch still commits after old child capture-out");
            session.Undo(); Unchanged("capture ownership tap has one shared Undo"); session.Discard(); Call("Refresh");
        }

        private static void SetViewport(Vector2Int size)
        {
            if (texture != null) { panel.targetTexture = null; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            texture = new RenderTexture(size.x, size.y, 0); texture.Create(); panel.targetTexture = texture;
            Root.schedule.Execute(() => Call("ApplyMobileViewport", new Vector2(size.x, size.y), new Rect(12, 18, size.x - 36, size.y - 42), new Rect())).StartingIn(200);
        }

        private static void Touch(VisualElement target, EventType type, int pointerId, Vector2 position)
        {
            Event source = new Event { type = type, button = 0, mousePosition = position };
            if (type == EventType.MouseDown)
            {
                using (PointerDownEvent evt = PointerDownEvent.GetPooled(source)) { ConfigureTouch(evt, pointerId); evt.target = target; target.SendEvent(evt); }
            }
            else if (type == EventType.MouseUp)
            {
                using (PointerUpEvent evt = PointerUpEvent.GetPooled(source)) { ConfigureTouch(evt, pointerId); evt.target = target; target.SendEvent(evt); }
            }
            else
            {
                using (PointerMoveEvent evt = PointerMoveEvent.GetPooled(source)) { ConfigureTouch(evt, pointerId); evt.target = target; target.SendEvent(evt); }
            }
        }

        private static void ConfigureTouch(EventBase evt, int pointerId)
        {
            // 실제 보드 콜백에 풀링한 터치 이벤트를 보내며 플랫폼 입력 백엔드만 대체한다.
            Type type = evt.GetType();
            type.GetProperty("pointerType", Flags).SetValue(evt, UnityEngine.UIElements.PointerType.touch);
            type.GetProperty("pointerId", Flags).SetValue(evt, pointerId);
            type.GetProperty("isPrimary", Flags).SetValue(evt, pointerId == 1);
        }

        private static void Click(string name)
        {
            Button button = Root.Q<Button>(name) ?? throw new Exception("Missing touch control " + name);
            Check(button.enabledInHierarchy, "enabled control " + name);
            typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
        }
        private static void Unchanged(string message) => Check(session.Get(levelId).Data.ToString() == baseline && !session.IsDirty && !session.CanUndo, message);
        private static void Advance(int target) { phase = target; next = EditorApplication.timeSinceStartup + 1.5; }
        private static void Finish() { Evidence.AppendLine("PASS requested touch replay checks completed; physical Android interaction remains unverified."); done = true; EditorApplication.ExitPlaymode(); }
        private static object Get(string name) => typeof(LevelToolScreen).GetField(name, Flags).GetValue(screen);
        private static void Set(string name, object value) => typeof(LevelToolScreen).GetField(name, Flags).SetValue(screen, value);
        private static object Call(string name, params object[] arguments) => (typeof(LevelToolScreen).GetMethod(name, Flags) ?? throw new Exception("Missing LevelToolScreen." + name)).Invoke(screen, arguments);
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Evidence.AppendLine("PASS " + message); Debug.Log("PASS " + message); }
    }
}
