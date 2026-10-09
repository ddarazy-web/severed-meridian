using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LevelTool;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolViewportVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const string Output = "Logs/LevelToolUiStage01";
        private static readonly Vector2Int[] Sizes = { new Vector2Int(1366, 768), new Vector2Int(1920, 1080), new Vector2Int(1280, 800), new Vector2Int(1024, 768) };
        private static readonly StringBuilder Measurements = new StringBuilder();
        private static bool priorEnabled, done;
        private static EnterPlayModeOptions priorOptions;
        private static double deadline, next;
        private static int phase, sizeIndex, exit;
        private static string recoveryFolder, expectedState;
        private static LevelToolScreen screen;
        private static PanelSettings panel;
        private static RenderTexture texture, oldTarget;
        private static float normalBoardWidth;
        private static VisualElement Root => screen.GetComponent<UIDocument>().rootVisualElement;

        public static void Run()
        {
            Directory.CreateDirectory(Output);
            Measurements.Clear();
            Measurements.AppendLine("UI Toolkit viewport geometry and rendered capture evidence. PNGs require separate visual review.");
            priorEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            priorOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            recoveryFolder = Path.GetFullPath(Output + "/viewport-recovery-" + Guid.NewGuid().ToString("N"));
            LevelToolRecoveryIsolation.Configure(recoveryFolder);
            sizeIndex = 0; phase = 0; exit = 0; done = false;
            deadline = EditorApplication.timeSinceStartup + 240;
            next = 0;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += State;
            EditorApplication.EnterPlaymode();
        }

        private static void State(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= State;
            if (panel != null) panel.targetTexture = oldTarget;
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            EditorSettings.enterPlayModeOptionsEnabled = priorEnabled;
            EditorSettings.enterPlayModeOptions = priorOptions;
            File.WriteAllText(Output + "/viewport-geometry.txt", Measurements.ToString());
            EditorApplication.Exit(exit);
        }

        private static void Tick()
        {
            try
            {
                if (done) return;
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("레벨툴 뷰포트 검사 시간 초과: " + phase);
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
                if (phase == 0)
                {
                    screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                    if (screen == null || (bool)typeof(LevelToolScreen).GetField("busy", Flags).GetValue(screen)) return;
                    LevelToolRecoveryIsolation.Verify(screen, recoveryFolder);
                    panel = screen.GetComponent<UIDocument>().panelSettings;
                    oldTarget = panel.targetTexture;
                    screen.Workspace.Open(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt"), "Cancel");
                    string id = screen.Workspace.Session.Documents.First(document => document.Kind == "level" && (int)document.Data["schemaVersion"] == 5).Id;
                    screen.Workspace.Session.SelectLevel(id);
                    screen.Workspace.Session.SelectCells(new[] { 40 });
                    screen.Workspace.Session.Apply("뷰포트 전환 보존 초안", documents => documents[id].Data["displayName"] = "화면 크기별 편집 상태 보존");
                    expectedState = screen.Workspace.Session.ExportState();
                    Invoke("SelectWorkspacePage", "보드 편집");
                    Invoke("Refresh");
                    phase = 1;
                }
                if (phase == 1)
                {
                    // 이전 크기의 검사 결과는 다음 기본 화면에 남기지 않는다.
                    typeof(LevelToolScreen).GetField("validatedDocuments", Flags).SetValue(screen, null);
                    Invoke("DrawValidationResults");
                    if (texture != null) { panel.targetTexture = null; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
                    Vector2Int size = Sizes[sizeIndex];
                    texture = new RenderTexture(size.x, size.y, 0); texture.Create(); panel.targetTexture = texture;
                    Invoke("Refresh");
                    Advance(2);
                }
                else if (phase == 2)
                {
                    VerifyLayout("default");
                    Capture("default");
                    normalBoardWidth = Root.Q("board-scroll").worldBound.width;
                    Invoke("ValidateLevel", false);
                    Advance(3);
                }
                else if (phase == 3)
                {
                    VerifyLayout("validation");
                    VisualElement validation = Root.Q("validation-panel");
                    Check(validation.worldBound.height >= 25 && validation.worldBound.yMin >= Root.Q("workspace-body").worldBound.yMax - 2, "validation occupies bottom region");
                    Check(Root.Q<Foldout>("validation-results").value, "validation results are expanded");
                    Within(Root.Q("validation-summary"), Root.worldBound, "validation summary");
                    Capture("validation");
                    Invoke("ChooseFolder", new object[] { new Action<string>(_ => { throw new Exception("Viewport test must not select a folder"); }) });
                    Advance(4);
                }
                else if (phase == 4)
                {
                    VisualElement modal = Root.Q("modal");
                    Check(modal != null && !Root.Q("editor").enabledInHierarchy, "modal disables background editor");
                    Within(modal.Q(className: "dialog"), Root.worldBound, "folder dialog");
                    foreach (string id in new[] { "folder-path", "browse", "choose-folder", "cancel" })
                    {
                        VisualElement control = modal.Q(id);
                        Within(control, Root.worldBound, "modal " + id);
                        Check(control.enabledInHierarchy, "modal control enabled " + id);
                    }
                    Check(modal.Contains(Root.focusController.focusedElement as VisualElement), "keyboard focus stays inside modal");
                    Capture("modal");
                    Click("cancel");
                    Check(Root.Q("modal") == null && Root.Q("editor").enabledInHierarchy, "modal cancel restores editor");
                    if (Sizes[sizeIndex].x == 1366)
                    {
                        Invoke("OpenShortcutHelp"); Advance(6); return;
                    }
                    if (Sizes[sizeIndex].x == 1024)
                    {
                        Invoke("ToggleBoardFocus");
                        Advance(5);
                    }
                    else NextSize();
                }
                else if (phase == 6)
                {
                    Check(Root.Q("left-panel").style.backgroundImage.value.sprite != null && Root.Q("save").Q("tool-icon") != null,
                        "dedicated atlas skin and toolbar icon loaded");
                    foreach (string command in new[] { "save", "undo", "redo", "play" })
                    {
                        var button = Root.Q<Button>(command);
                        Check(button.style.whiteSpace == WhiteSpace.NoWrap && button.worldBound.width >= button.text.Length * 14 + 39,
                            "icon toolbar reserves readable label width " + command);
                    }
                    var helpTitle = Root.Q("shortcut-search").parent.ElementAt(0);
                    Check(helpTitle.worldBound.yMax < Root.Q("shortcut-search").worldBound.yMin, "help title and search do not overlap");
                    Within(Root.Q("shortcut-search"), Root.worldBound, "help search");
                    Within(Root.Q("shortcut-entries"), Root.worldBound, "help scroll region");
                    Within(Root.Q("close-help"), Root.worldBound, "help close");
                    Capture("help");
                    Root.Q<TextField>("shortcut-search").value = "붙여넣기";
                    Advance(7);
                }
                else if (phase == 7)
                {
                    var entries = Root.Q("shortcut-entries").Query<Label>().ToList();
                    Check(entries.Count > 0 && entries.All(value => value.text.Contains("붙여넣기")), "Korean help search filters descriptions");
                    Capture("help-search"); Click("close-help"); Click("menu-file"); Advance(8);
                }
                else if (phase == 8)
                {
                    Within(Root.Q("command-menu"), Root.worldBound, "file command menu");
                    Check(Root.Q("command-menu").worldBound.yMin >= Root.Q("menu-file").worldBound.yMax - 2, "menu opens below its heading");
                    Check(Root.Q("command-Save").Query<Label>().ToList().Any(label => label.text == "Ctrl+S"), "menu displays save gesture");
                    Capture("menu"); Click("close-menu"); NextSize();
                }
                else if (phase == 5)
                {
                    Check(Root.Q("left-panel").resolvedStyle.display == DisplayStyle.None && Root.Q("right-panel").resolvedStyle.display == DisplayStyle.None, "1024 panels collapse");
                    Check(Root.Q("board-scroll").worldBound.width > normalBoardWidth + 300, "1024 board gains panel space");
                    Within(Root.Q("board-scroll"), Root.worldBound, "focused board viewport");
                    foreach (string id in new[] { "menu-view", "save", "undo", "redo", "play" }) Within(Root.Q(id), Root.worldBound, "focused command " + id);
                    Capture("board-focus");
                    Invoke("ToggleBoardFocus");
                    Advance(9);
                }
                else if (phase == 9)
                {
                    VerifyLayout("restored-panels");
                    NextSize();
                }
            }
            catch (Exception error)
            {
                exit = 1; done = true;
                Measurements.AppendLine("FAIL " + error);
                Debug.LogException(error);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                else State(PlayModeStateChange.EnteredEditMode);
            }
        }

        private static void Advance(int nextPhase)
        {
            phase = nextPhase;
            // 크기 변경 뒤 레이아웃과 실제 렌더링을 모두 기다린다.
            next = EditorApplication.timeSinceStartup + 1.5;
        }

        private static void NextSize()
        {
            Check(screen.Workspace.Session.ExportState() == expectedState, "viewport, validation and modal preserve complete editing state");
            sizeIndex++;
            if (sizeIndex < Sizes.Length) { Advance(1); return; }
            done = true;
            Debug.Log("PASS viewport geometry and 16 nonblank captures; PNG visual review and OS Player window behavior are separate checks");
            EditorApplication.ExitPlaymode();
        }

        private static void VerifyLayout(string mode)
        {
            Vector2Int size = Sizes[sizeIndex];
            Rect window = Root.worldBound;
            Measurements.AppendLine(size.x + "x" + size.y + " " + mode + " root=" + window);
            float scaleX = size.x / window.width, scaleY = size.y / window.height;
            Check(window.width > 0 && window.height > 0 && Mathf.Abs(scaleX - scaleY) < .01f, "panel preserves requested viewport aspect with production scaling");
            Measurements.AppendLine("physical=" + size + " logical=" + window.size + " scale=" + scaleX);
            foreach (string id in new[] { "menu-file", "menu-edit", "menu-view", "menu-test", "menu-help", "save", "undo", "redo", "play", "workspace-edit", "workspace-flow", "workspace-tutorial", "workspace-test", "workspace-records", "sidebar-levels", "sidebar-materials", "new-level", "duplicate", "inspector-selection", "inspector-level", "status" })
                Within(Root.Q(id), window, id);
            VisualElement left = Root.Q("left-panel"), right = Root.Q("right-panel");
            ScrollView board = Root.Q<ScrollView>("board-scroll");
            Within(left, window, "left panel"); Within(right, window, "right panel"); Within(board, window, "board viewport");
            Check(left.worldBound.xMax <= board.worldBound.xMin + 2 && board.worldBound.xMax <= right.worldBound.xMin + 2, "side panels do not overlap board");
            Rect boardArea = board.contentViewport.worldBound;
            Check(boardArea.width >= 160 && boardArea.height >= 120, "usable board viewport");
            Check(Root.Q("board").Query<Label>(className: "cell").ToList().Count == 81, "all board cells exist");
            Check(Root.Q("board").worldBound.width >= 252, "board keeps readable minimum cell size");
            if (boardArea.width >= 325 && boardArea.height >= 325)
                Within(Root.Q("board"), boardArea, "all nine rows and columns fit the board viewport");
            foreach (string id in new[] { "levels", "properties", "board-scroll" })
            {
                ScrollView scroll = Root.Q<ScrollView>(id);
                if (scroll.contentContainer.worldBound.height > scroll.contentViewport.worldBound.height + 2)
                    Check(scroll.verticalScroller.highValue > 0, "overflow is vertically scrollable " + id);
                if (id == "board-scroll" && Root.Q("board").worldBound.width > scroll.contentViewport.worldBound.width + 2)
                    Check(scroll.horizontalScroller.highValue > 0, "board overflow is horizontally scrollable");
            }
            Measurements.AppendLine("left=" + left.worldBound + " board=" + boardArea + " right=" + right.worldBound + " validation=" + Root.Q("validation-panel").worldBound);
        }

        private static void Within(VisualElement element, Rect bounds, string name)
        {
            Check(element != null, "exists " + name);
            Rect rect = element.worldBound;
            Check(rect.width > 0 && rect.height > 0 && !float.IsNaN(rect.width) && rect.xMin >= bounds.xMin - 2 && rect.yMin >= bounds.yMin - 2 && rect.xMax <= bounds.xMax + 2 && rect.yMax <= bounds.yMax + 2,
                "visible bounds " + name + " " + rect);
        }

        private static void Capture(string mode)
        {
            RenderTexture previous = RenderTexture.active;
            Texture2D captured = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture;
                captured.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); captured.Apply();
                HashSet<Color32> colors = new HashSet<Color32>();
                for (int y = 8; y < texture.height; y += 16)
                    for (int x = 8; x < texture.width; x += 16) colors.Add((Color32)captured.GetPixel(x, y));
                Check(colors.Count > 8, "capture contains rendered detail " + mode);
                string path = Output + "/viewport-" + texture.width + "x" + texture.height + "-" + mode + ".png";
                File.WriteAllBytes(path, captured.EncodeToPNG());
                Measurements.AppendLine("CAPTURE " + path + " sampledColors=" + colors.Count + " (visual review pending)");
                Debug.Log("CAPTURE " + path);
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(captured); }
        }

        private static object Invoke(string name, params object[] arguments)
        {
            MethodInfo method = typeof(LevelToolScreen).GetMethod(name, Flags) ?? throw new Exception("Missing LevelToolScreen." + name);
            return method.Invoke(screen, arguments);
        }

        private static void Click(string name)
        {
            Button button = Root.Q<Button>(name) ?? throw new Exception("Missing button " + name);
            typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
        }

        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("FAIL " + message);
            Debug.Log("PASS " + message);
        }
    }
}
