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
    public static class LevelToolAdvancedTouchVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const string Output = "Logs/AndroidLevelToolStage05";
        private static readonly Vector2Int[] Sizes = { new Vector2Int(360, 780), new Vector2Int(780, 360), new Vector2Int(975, 800), new Vector2Int(1280, 800) };
        private static readonly Queue<Action> Steps = new Queue<Action>();
        private static readonly StringBuilder Evidence = new StringBuilder();
        private static LevelToolScreen screen;
        private static AuthoringEditSession session;
        private static PanelSettings panel;
        private static RenderTexture texture, previousTexture;
        private static PanelScaleMode previousScaleMode;
        private static float previousScale;
        private static bool previousOptionsEnabled, initialized, finished;
        private static EnterPlayModeOptions previousOptions;
        private static double next, deadline;
        private static int sizeIndex, exit, focusedRun;
        private static string runFolder, recoveryFolder, levelId, before, after, regularSupply, tutorialBefore;
        private static JObject expected;
        private static VisualElement Root => screen.GetComponent<UIDocument>().rootVisualElement;
        private static JObject Data => session.Get(levelId).Data;

        public static void Run() => Begin(0);
        public static void RunLongTouch() => Begin(1);
        public static void RunVertex() => Begin(2);

        private static void Begin(int focus)
        {
            focusedRun = focus;
            Directory.CreateDirectory(Output);
            Evidence.Clear(); Steps.Clear(); initialized = finished = false; sizeIndex = exit = 0;
            Evidence.AppendLine("Editor Play Mode UI Toolkit touch replay at four rendered viewports. Physical Android touch, OS Back, IME and device rotation remain unverified.");
            previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            runFolder = Path.GetFullPath(Output + "/advanced-touch-" + Guid.NewGuid().ToString("N"));
            recoveryFolder = Path.Combine(runFolder, "Recovery");
            LevelToolRecoveryIsolation.Configure(recoveryFolder);
            deadline = EditorApplication.timeSinceStartup + 600; next = 0;
            EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Changed;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            try
            {
                if (finished || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Advanced touch timeout at viewport " + sizeIndex + " remaining " + Steps.Count);
                if (!initialized)
                {
                    screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                    if (screen == null || (bool)Get("busy")) return;
                    LevelToolRecoveryIsolation.Verify(screen, recoveryFolder); Call("CloseModal");
                    panel = screen.GetComponent<UIDocument>().panelSettings;
                    previousTexture = panel.targetTexture; previousScaleMode = panel.scaleMode; previousScale = panel.scale;
                    panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1;
                    initialized = true; PrepareViewport(); return;
                }
                if (Steps.Count > 0) { Steps.Dequeue()(); next = EditorApplication.timeSinceStartup + .5; return; }
                if (focusedRun == 0 && ++sizeIndex < Sizes.Length) { PrepareViewport(); return; }
                Evidence.AppendLine("PASS all requested viewport touch checks completed; actual platform validation remains pending.");
                finished = true; EditorApplication.ExitPlaymode();
            }
            catch (Exception error)
            {
                Evidence.AppendLine("FAIL " + error); Debug.LogException(error); exit = 1; finished = true;
                EditorApplication.ExitPlaymode();
            }
        }

        private static void PrepareViewport()
        {
            Call("CancelPendingInput");
            Check(screen.Workspace.Create(Path.Combine(runFolder, "Project-" + sizeIndex), LevelToolDocuments.NewProject(), "Discard"), "isolated viewport project created");
            session = screen.Workspace.Session; levelId = session.SelectedLevelId;
            Set("brush", null); Set("moveBody", null); Set("boardTouchTool", "tap"); Set("cellSize", 32f); Set("autoFitBoard", false);
            if (texture != null) { panel.targetTexture = null; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            Vector2Int size = Sizes[sizeIndex]; texture = new RenderTexture(size.x, size.y, 0); texture.Create(); panel.targetTexture = texture;
            Call("Refresh");
            Root.schedule.Execute(() => Call("ApplyMobileViewport", new Vector2(size.x, size.y), new Rect(12, 18, size.x - 36, size.y - 42), new Rect())).StartingIn(200);
            Evidence.AppendLine("VIEWPORT " + size);
            next = EditorApplication.timeSinceStartup + 1.5;
            if (focusedRun == 1) PlanLongTouch();
            else if (focusedRun == 2) PlanConnections();
            else { PlanFlow(); PlanConnections(); PlanTutorial(); PlanShapeAndSupply(); }
        }

        private static void PlanLongTouch()
        {
            Steps.Enqueue(() => Page("흐름")); ButtonStep("path-start"); TapStep(30);
            Steps.Enqueue(() => Root.Q<ScrollView>("board-scroll").ScrollTo(Root.Q("cell-31")));
            Steps.Enqueue(() =>
            {
                string state = session.ExportState(); int[] selection = session.SelectedCells;
                VisualElement cell = Root.Q("cell-31"); Vector2 point = cell.worldBound.center;
                Touch(cell, EventType.MouseDown, 1, point); Call("UpdateBoardTouch", Time.unscaledTime + 1);
                Touch(Root, EventType.MouseUp, 1, point);
                Check(session.ExportState() == state && session.SelectedCells.SequenceEqual(selection) && Root.Q("modal") == null && ((List<int>)Get("flowRoute")).SequenceEqual(new[] { 30 }),
                    "long touch preserves pending advanced path and selection");
            });
        }

        private static void PlanFlow()
        {
            Steps.Enqueue(() => Page("흐름"));
            ButtonStep("board-touch-select"); TapStep(10);
            Steps.Enqueue(() => { before = Data.ToString(); expected = (JObject)Data.DeepClone(); FlowDocumentEditing.SetGravity(expected, new[] { 10 }, "Right"); });
            ButtonStep("gravity-right"); Steps.Enqueue(() => Check(JToken.DeepEquals(Data, expected), "touch gravity equals common command"));
            ButtonStep("undo"); Steps.Enqueue(() => Check(Data.ToString() == before, "touch gravity is one undo"));
            ButtonStep("board-touch-area"); ButtonStep("path-start");
            Steps.Enqueue(() => { before = Data.ToString(); expected = (JObject)Data.DeepClone(); FlowDocumentEditing.SetPath(expected, new[] { 30, 31, 32 }); });
            TapStep(30); TapStep(31); TapStep(32);
            Steps.Enqueue(() => Check(Data.ToString() == before && ((List<int>)Get("flowRoute")).SequenceEqual(new[] { 30, 31, 32 }), "area tool does not intercept path touch picks"));
            Steps.Enqueue(() =>
            {
                string state = session.ExportState(); int[] selection = session.SelectedCells;
                VisualElement cell = Root.Q("cell-31"); Vector2 point = cell.worldBound.center;
                Touch(cell, EventType.MouseDown, 1, point); Touch(cell, EventType.MouseDown, 2, point + new Vector2(15, 0));
                Touch(Root, EventType.MouseUp, 2, point + new Vector2(15, 0)); Touch(Root, EventType.MouseUp, 1, point);
                Check(session.ExportState() == state && session.SelectedCells.SequenceEqual(selection) && ((List<int>)Get("flowRoute")).Count == 3, "pinch gesture never appends or commits pending path");
                Touch(cell, EventType.MouseDown, 1, point); Call("UpdateBoardTouch", Time.unscaledTime + 1);
                Touch(Root, EventType.MouseUp, 1, point);
                Check(session.ExportState() == state && session.SelectedCells.SequenceEqual(selection) && Root.Q("modal") == null, "long touch preserves pending advanced path and selection");
            });
            ButtonStep("path-finish");
            Steps.Enqueue(() => { Check(JToken.DeepEquals(Data, expected), "touch path equals common command"); after = Data.ToString(); });
            ButtonStep("undo"); Steps.Enqueue(() => Check(Data.ToString() == before, "path completion is one undo"));
            ButtonStep("redo"); Steps.Enqueue(() => Check(Data.ToString() == after, "path redo restores authored JSON"));
            ButtonStep("path-start"); TapStep(40); ButtonStep("tool-cancel");
            Steps.Enqueue(() => Check(Data.ToString() == after && ((List<int>)Get("flowRoute")).Count == 0, "path cancel discards only uncommitted route"));
            ButtonStep("portal-start"); TapStep(40);
            Steps.Enqueue(() => Check(Data["flow"]["portals"].Any(value => !(bool)value["hasExit"]), "portal first touch retains explicit draft"));
            TapStep(60); Steps.Enqueue(() => Check((bool)Data["flow"]["portals"].Last["hasExit"], "portal second touch completes pair"));
            ButtonStep("wall-start"); TapStep(50); TapStep(51);
            Steps.Enqueue(() => Check(Data["flow"]["walls"].Count() == 1, "touch wall pair is authored"));
            ButtonStep("wall-erase"); TapStep(50); TapStep(51); ButtonStep("tool-cancel");
            Steps.Enqueue(() => Check(!Data["flow"]["walls"].Any(), "touch wall erase uses matching geometry"));
            ButtonStep("path-start"); TapStep(30);
            Steps.Enqueue(() => before = session.ExportState());
            ButtonStep("mobile-properties");
            Steps.Enqueue(() => Check(session.ExportState() == before && ((List<int>)Get("flowRoute")).SequenceEqual(new[] { 30 }), "closing mobile panel preserves pending path without committing"));
            ButtonStep("mobile-properties");
            Steps.Enqueue(() => Check(session.ExportState() == before && ((List<int>)Get("flowRoute")).SequenceEqual(new[] { 30 }), "returning mobile panel preserves pending path without committing"));
            Steps.Enqueue(() =>
            {
                string state = session.ExportState(); VisualElement cell = Root.Q("cell-31");
                Touch(cell, EventType.MouseDown, 1, cell.worldBound.center); Call("OnApplicationPause", true); Call("OnApplicationPause", false);
                Check(session.ExportState() == state, "pause without Up never commits path");
            });
            ButtonStep("tool-cancel");
        }

        private static void PlanConnections()
        {
            Steps.Enqueue(() =>
            {
                Dictionary<string, JObject> definitions = LevelToolDocuments.Definitions(session);
                string generator = definitions.First(pair => (string)pair.Value["reaction"] == "GeneratorCharge").Key;
                string target = definitions.First(pair => (string)pair.Value["reaction"] == "Durability" && LevelToolDocuments.Size(pair.Value) == 2 && (string)pair.Value["removal"]?["kind"] != "Scrap").Key;
                session.Apply("전선 검사 배치", docs => { LevelDocumentEditing.Place(docs[levelId].Data, definitions, generator, 2, 2, false); LevelDocumentEditing.Place(docs[levelId].Data, definitions, target, 2, 6, false); });
                Page("연결");
            });
            ButtonStep("connection-add");
            if (focusedRun != 2) Steps.Enqueue(() => Set("moveBody", (string)Data["connections"][0]["targetId"]));
            ButtonStep("wire-start");
            Steps.Enqueue(() => Check(Get("moveBody") == null, "wire start clears body move tool"));
            Steps.Enqueue(() => Root.Q<ScrollView>("board-scroll").ScrollTo(Root.Q("wire-vertex-99")));
            Steps.Enqueue(() =>
            {
                VisualElement corner = Root.Q("wire-vertex-99"); Vector2 point = corner.worldBound.center + Vector2.one;
                Check(Root.Q<ScrollView>("board-scroll").contentViewport.worldBound.Contains(point), "outer boundary vertex long touch is visible");
                string state = session.ExportState(); int[] route = ((List<int>)Get("wireRoute")).ToArray();
                Touch(corner, EventType.MouseDown, 1, point);
                Check((int)Get("boardTouchCell") == -1, "outer boundary vertex has no background cell");
                Call("UpdateBoardTouch", Time.unscaledTime + 1); Touch(Root, EventType.MouseUp, 1, point);
                Check(session.ExportState() == state && ((List<int>)Get("wireRoute")).SequenceEqual(route) && Root.Q("modal") == null,
                    "long touch on outer boundary vertex never appends wire or changes selection");
            });
            VertexStep(24); ButtonStep("wire-save-draft");
            Steps.Enqueue(() => { Check(Data["connections"][0]["vertices"].Count() == 1, "touch enlarged vertex saves partial route"); before = Data.ToString(); });
            VertexStep(25); VertexStep(26); ButtonStep("wire-finish");
            Steps.Enqueue(() => { Check(Data["connections"][0]["vertices"].Count() == 3, "touch wire reaches target terminal"); after = Data.ToString(); });
            ButtonStep("undo"); Steps.Enqueue(() => Check(Data.ToString() == before, "wire completion one undo"));
            ButtonStep("redo"); Steps.Enqueue(() => Check(Data.ToString() == after, "wire redo retains reference IDs"));
            ButtonStep("wire-start"); ButtonStep("wire-clear-input"); VertexStep(33); ButtonStep("wire-save-draft");
            Steps.Enqueue(() => Check(Data.ToString() == after, "invalid touch wire inside 2x2 is rejected atomically"));
            ButtonStep("wire-cancel"); Steps.Enqueue(() => Check(Data.ToString() == after && !(bool)Get("wireDrawing"), "wire cancel preserves saved route"));
        }

        private static void PlanTutorial()
        {
            Steps.Enqueue(() => Page("튜토리얼")); ButtonStep("tutorial-add-step");
            Steps.Enqueue(() => Root.Q<DropdownField>("tutorial-kind").value = "교환");
            ButtonStep("board-touch-area"); ButtonStep("tutorial-pick-action");
            Steps.Enqueue(() => before = session.ExportState()); TapStep(30); TapStep(31);
            Steps.Enqueue(() => Check(session.ExportState() == before && ((JObject)Get("tutorialPick"))["cells"].Count() == 2, "tutorial touch pending picks preserve document and history"));
            ButtonStep("tutorial-pick-cancel"); Steps.Enqueue(() => Check(session.ExportState() == before && Get("tutorialPick") == null, "tutorial cancel preserves complete original session"));
            Steps.Enqueue(() => tutorialBefore = Data.ToString());
            ButtonStep("tutorial-pick-action"); TapStep(30); TapStep(31); ButtonStep("tutorial-pick-confirm");
            Steps.Enqueue(() => { Check((int)Data["tutorial"]["steps"].Last["first"]["column"] == 3 && (int)Data["tutorial"]["steps"].Last["second"]["column"] == 4, "tutorial touch confirm preserves pick order"); after = Data.ToString(); });
            ButtonStep("undo"); Steps.Enqueue(() => Check(Data.ToString() == tutorialBefore, "tutorial confirmed pick has one undo"));
            ButtonStep("redo"); Steps.Enqueue(() => Check(Data.ToString() == after, "tutorial redo restores complete document"));
            Steps.Enqueue(() => Root.Q<DropdownField>("tutorial-new-condition").value = "파워 생성"); ButtonStep("tutorial-add-condition");
            Steps.Enqueue(() =>
            {
                Check((string)Data["tutorial"]["steps"].Last["conditions"][0]["kind"] == "Generated", "target picker fixture uses generated condition");
                DropdownField target = Root.Q<DropdownField>("tutorial-target-kind-0");
                Check(target != null && target.choices.Contains("선택 영역"), "generated condition exposes area target UI");
                target.value = "선택 영역";
            });
            Steps.Enqueue(() => tutorialBefore = Data.ToString());
            ButtonStep("tutorial-target-select-0");
            Steps.Enqueue(() => before = session.ExportState()); TapStep(40);
            Steps.Enqueue(() => Check(session.ExportState() == before, "condition target touch remains pending until confirmation"));
            ButtonStep("tutorial-pick-confirm");
            Steps.Enqueue(() =>
            {
                JToken cells = Data["tutorial"]["steps"].Last["conditions"][0]["target"]["cells"];
                Check(cells.Count() == 1 && (int)cells[0]["row"] == 4 && (int)cells[0]["column"] == 4, "condition target uses touch picker coordinates");
                after = Data.ToString();
            });
            ButtonStep("undo"); Steps.Enqueue(() => Check(Data.ToString() == tutorialBefore, "condition target confirmation is one undo"));
            ButtonStep("redo"); Steps.Enqueue(() => Check(Data.ToString() == after, "condition target redo restores complete document"));
        }

        private static void PlanShapeAndSupply()
        {
            Steps.Enqueue(() => Page("모양")); ButtonStep("board-touch-select"); TapStep(40);
            ButtonStep("deactivate-cells"); Steps.Enqueue(() => Check(!(bool)Data["board"]["cells"][40]["isActive"], "touch selected shape mask edited"));
            ButtonStep("undo"); Steps.Enqueue(() => Check((bool)Data["board"]["cells"][40]["isActive"], "shape edit is one undo"));
            Steps.Enqueue(() => Page("공급")); TapStep(0);
            Steps.Enqueue(() => Root.Q<DropdownField>("source-mode").value = "고정 순서");
            Steps.Enqueue(() =>
            {
                before = Data.ToString(); expected = (JObject)Data.DeepClone();
                Dictionary<string, JObject> definitions = LevelToolDocuments.Definitions(session);
                string supply = (string)Get("supplyToAdd");
                SupplyDocumentEditing.SetItems(expected, definitions, new[] { 0 }, new JArray(new JObject
                {
                    ["definitionId"] = supply, ["count"] = (int)Get("supplyCount"), ["color"] = (string)Get("supplyColor"),
                    ["direction"] = (string)Get("supplyDirection"), ["durability"] = (int)Get("supplyDurability")
                }), true);
            });
            ButtonStep("append-supply"); Steps.Enqueue(() => { Check(JToken.DeepEquals(Data, expected), "touch regular fixed supply equals common command"); after = Data.ToString(); });
            ButtonStep("undo"); Steps.Enqueue(() => Check(Data.ToString() == before, "regular fixed supply append is one undo"));
            ButtonStep("redo"); Steps.Enqueue(() => Check(Data.ToString() == after, "regular fixed supply redo restores authored JSON"));
            Steps.Enqueue(() => { Page("튜토리얼 공급"); regularSupply = Data["elementSupply"].ToString(); });
            TapStep(10); ButtonStep("sources"); ButtonStep("append-supply");
            Steps.Enqueue(() => Check(Data["tutorial"]["supply"]["sources"].Any(value => value["items"].Any()) && Data["elementSupply"].ToString() == regularSupply, "touch tutorial fixed supply preserves regular supply"));
            ButtonStep("undo"); Steps.Enqueue(() => Check(!Data["tutorial"]["supply"]["sources"][0]["items"].Any(), "tutorial supply append is one undo"));
        }

        private static void Page(string value)
        {
            Call("CancelPendingInput"); Set("inspectorPage", value); Set("brush", null); Call("Refresh");
            if ((string)Get("mobilePanel") != "properties") Call("SelectMobilePanel", "properties");
        }

        private static void ButtonStep(string name)
        {
            Steps.Enqueue(() =>
            {
                VisualElement button = Root.Q(name) ?? throw new Exception("Missing " + name);
                if (Root.Q<ScrollView>("properties").Contains(button)) Root.Q<ScrollView>("properties").ScrollTo(button);
                else if (Root.Q<ScrollView>("board-touch-tools").Contains(button)) Root.Q<ScrollView>("board-touch-tools").ScrollTo(button);
            });
            Steps.Enqueue(() =>
            {
                VisualElement button = Root.Q(name); Check(button.enabledInHierarchy, "enabled touch button " + name);
                Rect bounds = button.worldBound;
                Check(bounds.width > 0 && bounds.height > 0 && Root.worldBound.Contains(bounds.center), "visible touch button " + name);
                if (Root.Q<ScrollView>("properties").Contains(button)) Check(Root.Q<ScrollView>("properties").contentViewport.worldBound.Contains(bounds.center), "scrolled confirmation/control reachable " + name);
                Touch(button, EventType.MouseDown, 1, bounds.center); Touch(button, EventType.MouseUp, 1, bounds.center);
            });
        }

        private static void TapStep(int cell)
        {
            Steps.Enqueue(() => Root.Q<ScrollView>("board-scroll").ScrollTo(Root.Q("cell-" + cell)));
            Steps.Enqueue(() =>
            {
                VisualElement tile = Root.Q("cell-" + cell); Vector2 point = tile.worldBound.center;
                Check(Root.Q<ScrollView>("board-scroll").contentViewport.worldBound.Contains(point), "board touch visible " + cell);
                Touch(tile, EventType.MouseDown, 1, point); Touch(tile, EventType.MouseUp, 1, point);
            });
        }

        private static void VertexStep(int vertex)
        {
            Steps.Enqueue(() => Root.Q<ScrollView>("board-scroll").ScrollTo(Root.Q("wire-vertex-" + vertex)));
            Steps.Enqueue(() =>
            {
                VisualElement dot = Root.Q("wire-vertex-" + vertex); Vector2 point = dot.worldBound.center + new Vector2(14, 0);
                Check(dot.worldBound.width >= 48 && dot.worldBound.height >= 48 && dot.worldBound.Contains(point), "mobile vertex has enlarged touch area " + vertex);
                Check(Root.Q<ScrollView>("board-scroll").contentViewport.worldBound.Contains(point), "vertex touch visible " + vertex);
                // 축소 상태에서는 옆 점의 확장 영역도 겹친다. 이벤트 대상보다 실제 위치에 가까운 점을 골라야 한다.
                VisualElement overlapping = Root.Q("wire-vertex-" + (vertex + 1));
                Check(overlapping.worldBound.Contains(point), "neighboring enlarged hit area overlaps touch position " + vertex);
                Touch(overlapping, EventType.MouseDown, 1, point); Touch(overlapping, EventType.MouseUp, 1, point);
                Check(((List<int>)Get("wireRoute")).Last() == vertex, "nearby vertex touch picks nearest point " + vertex);
            });
        }

        private static void Touch(VisualElement target, EventType type, int pointerId, Vector2 position)
        {
            Event source = new Event { type = type, button = 0, mousePosition = position };
            using EventBase evt = type == EventType.MouseDown ? (EventBase)PointerDownEvent.GetPooled(source) : PointerUpEvent.GetPooled(source);
            Type eventType = evt.GetType();
            eventType.GetProperty("pointerType", Flags).SetValue(evt, UnityEngine.UIElements.PointerType.touch);
            eventType.GetProperty("pointerId", Flags).SetValue(evt, pointerId);
            eventType.GetProperty("isPrimary", Flags).SetValue(evt, pointerId == 1);
            evt.target = target; target.SendEvent(evt);
        }

        private static object Get(string name) => typeof(LevelToolScreen).GetField(name, Flags).GetValue(screen);
        private static void Set(string name, object value) => typeof(LevelToolScreen).GetField(name, Flags).SetValue(screen, value);
        private static object Call(string name, params object[] arguments) => typeof(LevelToolScreen).GetMethod(name, Flags).Invoke(screen, arguments);
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Evidence.AppendLine("PASS " + message); Debug.Log("PASS " + message); }

        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Changed;
            if (panel != null) { panel.targetTexture = previousTexture; panel.scaleMode = previousScaleMode; panel.scale = previousScale; }
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); texture = null; }
            EditorSettings.enterPlayModeOptionsEnabled = previousOptionsEnabled; EditorSettings.enterPlayModeOptions = previousOptions;
            File.WriteAllText(Output + "/advanced-touch-verification.txt", Evidence.ToString());
            EditorApplication.Exit(exit);
        }
    }
}
