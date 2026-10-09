using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelTool;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolShellVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static bool priorEnabled;
        private static EnterPlayModeOptions priorOptions;
        private static double deadline;
        private static int exit;
        private static bool done;
        private static string recoveryFolder;
        private static LevelToolScreen screen;
        private static VisualElement Root => screen.GetComponent<UIDocument>().rootVisualElement;

        public static void Run()
        {
            priorEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            priorOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            recoveryFolder = Path.GetFullPath("Logs/LevelToolUIStage01/shell-" + Guid.NewGuid().ToString("N"));
            LevelToolRecoveryIsolation.Configure(recoveryFolder);
            exit = 0; done = false; deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += State;
            EditorApplication.EnterPlaymode();
        }

        private static void State(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= State;
            EditorSettings.enterPlayModeOptionsEnabled = priorEnabled;
            EditorSettings.enterPlayModeOptions = priorOptions;
            EditorApplication.Exit(exit);
        }

        private static void Tick()
        {
            try
            {
                if (done) return;
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("레벨툴 화면 구조 검사 시간 초과");
                if (!EditorApplication.isPlaying) return;
                screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                if (screen == null || (bool)typeof(LevelToolScreen).GetField("busy", Flags).GetValue(screen)) return;
                LevelToolRecoveryIsolation.Verify(screen, recoveryFolder);
                VerifyShell();
                VerifyNavigation();
                Debug.Log("PASS level tool shell structure, feature navigation, validation and edit state preservation");
                done = true;
                EditorApplication.ExitPlaymode();
            }
            catch (Exception error)
            {
                exit = 1; done = true; Debug.LogException(error);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                else State(PlayModeStateChange.EnteredEditMode);
            }
        }

        private static void VerifyShell()
        {
            // 구조가 아직 없는 화면도 컴파일 오류가 아닌 요구사항 실패로 보고한다.
            foreach (string id in new[] { "workspace-tabs", "left-panel", "right-panel", "validation-panel", "board", "board-scroll", "levels", "palette", "properties", "status" })
                Check(Root.Q(id) != null, "shell container " + id);
            foreach (string id in new[] { "workspace-edit", "workspace-flow", "workspace-tutorial", "workspace-test", "workspace-records", "menu-file", "menu-edit", "menu-view", "menu-test", "menu-help", "sidebar-levels", "sidebar-materials", "inspector-selection", "inspector-level", "save", "undo", "redo", "play" })
                Check(Root.Q<Button>(id) != null, "shell control " + id);
            Check(Root.Q("workspace-tabs").Query<Button>().ToList().Count == 5, "exactly five workspace tabs");
            Check(typeof(LevelToolScreen).GetMethod("BuildEditorLayout", Flags) != null, "layout construction entry point");
            Check(typeof(LevelToolScreen).GetMethod("SelectWorkspacePage", Flags, null, new[] { typeof(string) }, null) != null, "workspace navigation entry point");
        }

        private static void VerifyNavigation()
        {
            screen.Workspace.Open(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt"), "Cancel");
            AuthoringEditSession session = screen.Workspace.Session;
            string id = session.Documents.First(document => document.Kind == "level" && (int)document.Data["schemaVersion"] == 5).Id;
            session.SelectLevel(id);
            session.SelectCells(new[] { 10, 11 });
            string original = session.Get(id).Data.ToString();
            session.Apply("화면 이동 중 보존할 편집", documents => documents[id].Data["displayName"] = "화면 이동 보존 검사");
            session.Apply("화면 이동 중 보존할 Redo", documents => documents[id].Data["displayName"] = "Redo 보존 검사");
            string redone = session.ExportState();
            session.Undo();
            Invoke("Refresh");
            Invoke("ValidateLevel", false);
            Check((bool)Invoke("ValidationIsCurrent"), "validation starts current");
            Check(Root.Q("validation-panel").Contains(Root.Q("validation-results")), "validation results belong to bottom panel");
            string expected = session.ExportState();
            VisualElement board = Root.Q("board");
            VisualElement shell = Root.Q("workspace-tabs");

            string[] tabs = { "workspace-flow", "workspace-tutorial", "workspace-test", "workspace-records", "workspace-edit" };
            string[] pages = { "흐름·공급", "튜토리얼", "검사·봇 시험", "시험 기록", "보드 편집" };
            for (int i = 0; i < tabs.Length; i++)
            {
                Click(tabs[i]);
                Check(Root.Q<Button>(tabs[i]).ClassListContains("selected"), "active workspace " + pages[i]);
                if (tabs[i] == "workspace-tutorial") Check(Root.Q("tutorial-preview") != null, "tutorial authoring reachable");
                if (tabs[i] == "workspace-test") Check(Root.Q("bot-new") != null && Root.Q("batch-new") != null, "single and batch bot trials reachable");
                if (tabs[i] == "workspace-records") Check(Root.Q("history-refresh") != null, "stored trial records reachable");
                VerifyPreserved(session, expected, pages[i]);
                Check((bool)Invoke("ValidationIsCurrent"), "workspace navigation keeps validation current " + pages[i]);
                Check(ReferenceEquals(board, Root.Q("board")) && ReferenceEquals(shell, Root.Q("workspace-tabs")), "navigation keeps shell containers " + pages[i]);
            }

            Click("sidebar-materials");
            Check(Visible(Root.Q("palette")) && !Visible(Root.Q("levels")), "materials tab shows palette alone");
            Click("sidebar-levels");
            Check(Visible(Root.Q("levels")) && !Visible(Root.Q("palette")), "levels tab shows level list alone");
            Click("inspector-selection");
            Check(Root.Q<Button>("inspector-selection").ClassListContains("selected"), "selected item inspector tab");
            Click("inspector-level");
            Check(Root.Q<Button>("inspector-level").ClassListContains("selected"), "level settings inspector tab");
            VerifyPreserved(session, expected, "side panel tabs");

            Invoke("ToggleBoardFocus");
            Check(!Visible(Root.Q("left-panel")) && !Visible(Root.Q("right-panel")), "board focus hides both panels");
            VerifyPreserved(session, expected, "board focus");
            Invoke("ToggleBoardFocus");
            Check(Visible(Root.Q("left-panel")) && Visible(Root.Q("right-panel")), "board focus restores both panels");

            // 이전 검사와 제작 경로가 사용하는 선택자를 그대로 유지한다.
            string[] legacyPages = { "공급", "흐름", "연결", "모양", "튜토리얼", "자동 시험" };
            string[] controls = { "sources", "path-start", "connection-add", "shape-register", "tutorial-preview", "bot-new" };
            for (int i = 0; i < legacyPages.Length; i++)
            {
                DropdownField page = Root.Q<DropdownField>("inspector-page");
                Check(page != null && page.choices.Contains(legacyPages[i]), "legacy navigation available " + legacyPages[i]);
                page.value = legacyPages[i];
                Check(Root.Q(controls[i]) != null, "legacy feature reachable " + legacyPages[i]);
                VerifyPreserved(session, expected, "legacy page " + legacyPages[i]);
            }
            Invoke("SelectWorkspacePage", "보드 편집");
            VerifyPreserved(session, expected, "direct workspace navigation");
            session.Redo(); Check(session.ExportState() == redone, "navigation preserves redo result");
            session.Undo(); Check(session.ExportState() == expected, "navigation preserves undo after redo");
            session.Undo(); Check(session.Get(id).Data.ToString() == original && !session.IsDirty && !session.CanUndo && session.SelectedCells.SequenceEqual(new[] { 10, 11 }), "navigation preserves original undo result");
            session.Redo();
            Check(session.Get(id).Data["displayName"].ToString() == "화면 이동 보존 검사" && session.IsDirty, "navigation preserves edited state");
            session.Apply("검사 최신 상태 무효화", documents => documents[id].Data["displayName"] = "검사 후 변경");
            Invoke("Refresh");
            Check(!(bool)Invoke("ValidationIsCurrent"), "document edit invalidates validation");
            session.Apply("잘못된 이동 횟수", documents => documents[id].Data["moveCount"] = 0);
            Invoke("Refresh");
            Click("inspector-selection");
            Invoke("ToggleBoardFocus");
            Invoke("ValidateLevel", false);
            string invalidState = session.ExportState();
            Invoke("FocusValidationIssue", id, new Levels.LevelValidationIssue(Levels.LevelValidationCode.InvalidLevelNumber, "이동 횟수 검사", "moveCount"));
            Check(Visible(Root.Q("right-panel")) && Visible(Root.Q("level-settings")), "validation reveals hidden level settings");
            Check(session.ExportState() == invalidState, "validation navigation preserves data and history");
            Root.Q<DropdownField>("edit-layer").value = "Cover";
            screen.enabled = false; screen.enabled = true;
            Check(Root.Q<DropdownField>("edit-layer").value == "Cover", "reenable preserves displayed editing layer");
        }

        private static void VerifyPreserved(AuthoringEditSession session, string expected, string context)
        {
            Check(ReferenceEquals(session, screen.Workspace.Session) && session.ExportState() == expected && session.IsDirty && session.CanUndo && session.CanRedo && session.SelectedCells.SequenceEqual(new[] { 10, 11 }),
                "session, selection, dirty and history preserved: " + context);
        }

        private static bool Visible(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
                if (current.style.display == DisplayStyle.None) return false;
            return element != null;
        }

        private static object Invoke(string name, params object[] arguments)
        {
            MethodInfo method = typeof(LevelToolScreen).GetMethod(name, Flags) ?? throw new Exception("Missing LevelToolScreen." + name);
            return method.Invoke(screen, arguments);
        }

        private static void Click(string name)
        {
            Button button = Root.Q<Button>(name) ?? throw new Exception("Missing button " + name);
            Check(button.enabledInHierarchy, "control enabled " + name);
            typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
        }

        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("FAIL " + message);
            Debug.Log("PASS " + message);
        }
    }
}
