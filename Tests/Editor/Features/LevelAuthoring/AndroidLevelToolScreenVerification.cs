using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public static class AndroidLevelToolScreenVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static bool priorEnabled, done;
        private static EnterPlayModeOptions priorOptions;
        private static double deadline;
        private static int exit;
        private static string recoveryFolder, expectedState, incomingState;
        private static LevelToolScreen screen;
        private static VisualElement Root => screen.GetComponent<UIDocument>().rootVisualElement;

        public static void Run()
        {
            priorEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            priorOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            string folder = Path.GetFullPath("Logs/AndroidLevelToolStage01/screen-" + Guid.NewGuid().ToString("N"));
            recoveryFolder = Path.Combine(folder, "Recovery");
            var workspace = new AuthoringToolWorkspace();
            workspace.Create(Path.Combine(folder, "Projects", "Current"), LevelToolDocuments.NewProject());
            incomingState = workspace.ExportState();
            AuthoringEditSession session = workspace.Session;
            string id = session.SelectedLevelId;
            session.SelectCells(new[] { 10, 11 });
            session.Apply("미저장 편집", documents => documents[id].Data["displayName"] = "복구 화면 검사");
            session.Apply("다시 실행할 편집", documents => documents[id].Data["displayName"] = "Redo 화면 검사");
            session.Undo();
            expectedState = session.ExportState();
            new ToolDraftStore(recoveryFolder).Save(workspace.ExportState(), new JObject { ["layer"] = "Cover" }, 1);
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
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Android 내부 프로젝트 화면 검사 시간 초과");
                if (!EditorApplication.isPlaying) return;
                screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                if (screen == null || (bool)Get("busy")) return;
                LevelToolRecoveryIsolation.Verify(screen, recoveryFolder);
                Check(Root.Q<Button>("restore-draft") != null && screen.Workspace.Session == null,
                    "dirty recovery decision precedes opening any workspace");
                VerifyBootstrapGuards();
                Click("restore-draft");
                VerifyPreserved("recovery button restores dirty selection and undo/redo");
                VerifyInternalChooser();
                VerifyDirtyCancellation();
                VerifyReenable();
                Debug.Log("PASS Android internal project UI callbacks, bootstrap precedence, recovery and reenable preservation");
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

        private static void VerifyBootstrapGuards()
        {
            VisualElement recoveryModal = (VisualElement)Get("modal");
            string priorIncoming = (string)Get("incomingWorkspace");
            bool priorDefault = (bool)Get("incomingDefault");
            try
            {
                Set("incomingWorkspace", null); Set("incomingDefault", false);
                Invoke("TryOpenInternalDefault");
                Check(screen.Workspace.Session == null && ReferenceEquals(Get("modal"), recoveryModal),
                    "internal bootstrap waits for recovery modal");

                // 실제 복구 모달은 보존하고 각 우선 조건을 독립적으로 검사한다.
                Set("modal", null);
                Set("busy", true);
                Invoke("TryOpenInternalDefault");
                Check(screen.Workspace.Session == null, "internal bootstrap waits for ongoing storage");
                Set("busy", false);
                Set("recoveryBlocksDefault", true);
                Invoke("TryOpenInternalDefault");
                Check(screen.Workspace.Session == null, "failed recovery blocks internal bootstrap");
                screen.OfferDefaultWorkspace(incomingState);
                Check(screen.Workspace.Session == null, "failed recovery also blocks default handoff");
                Set("recoveryBlocksDefault", false);
                Set("incomingWorkspace", null); Set("incomingDefault", false);
                screen.OfferWorkspace(incomingState);
                Check(Root.Q<Button>("open-incoming-workspace") != null, "explicit handoff has an actionable toolbar control");
                Invoke("TryOpenInternalDefault");
                Check(screen.Workspace.Session == null && (string)Get("incomingWorkspace") == incomingState,
                    "explicit handoff precedes internal bootstrap");
                Set("modal", recoveryModal);
                screen.OfferDefaultWorkspace(incomingState);
                Check(!(bool)Get("incomingDefault") && screen.Workspace.Session == null,
                    "default handoff cannot replace explicit incoming work or recovery choice");
            }
            finally
            {
                Set("modal", recoveryModal); Set("busy", false); Set("recoveryBlocksDefault", false);
                Set("incomingWorkspace", priorIncoming); Set("incomingDefault", priorDefault);
                Invoke("DrawIncomingWorkspace");
            }
        }

        private static void VerifyInternalChooser()
        {
            string selected = null;
            Action<string> choose = folder => selected = folder;
            // Editor에서는 Android 분기 진입점만 직접 호출하고 실제 버튼 콜백을 검사한다.
            Invoke("ChooseInternalProject", choose, true);
            VisualElement modal = Root.Q("modal");
            Check(modal.Q<TextField>("project-name") != null && modal.Q<Button>("choose-project") != null &&
                modal.Q("folder-path") == null && modal.Q("browse") == null && modal.Q("choose-folder") == null,
                "internal creation requests a project name without PC folder controls");
            Label error = modal.Children().First().Children().OfType<Label>().Last();
            foreach (string invalid in new[] { "", "../escape", "child/name", "child\\name" })
            {
                modal.Q<TextField>("project-name").value = invalid;
                Click("choose-project");
                Check(selected == null && ReferenceEquals(Root.Q("modal"), modal) && !string.IsNullOrEmpty(error.text),
                    "invalid internal name keeps dialog open and reports error: " + invalid);
                VerifyPreserved("invalid internal name preserves draft");
            }
            Click("cancel");
            Check(Root.Q("modal") == null && selected == null, "internal creation cancel does not call selection callback");
            VerifyPreserved("internal creation cancellation");

            Invoke("ChooseInternalProject", choose, false);
            Check(Root.Q<Button>("choose-project").text == "선택한 프로젝트 열기", "open chooser uses the open action");
            Click("cancel");
            Check(selected == null, "internal open cancel does not call selection callback");
            VerifyPreserved("internal open cancellation");

            string projectName = "screen-choice-" + Guid.NewGuid().ToString("N");
            string internalRoot = (string)typeof(LevelToolScreen).GetProperty("InternalProjectsRoot", Flags).GetValue(screen);
            string expectedFolder = Path.Combine(internalRoot, projectName);
            Check(!Directory.Exists(expectedFolder), "callback verification target is unused");
            Invoke("ChooseInternalProject", choose, true);
            Root.Q<TextField>("project-name").value = projectName;
            Click("choose-project");
            Check(selected == expectedFolder && Root.Q("modal") == null && !Directory.Exists(expectedFolder),
                "valid project name resolves internal path and calls selection without writing a project");
            VerifyPreserved("successful name selection before a file transition");
        }

        private static void VerifyDirtyCancellation()
        {
            bool called = false;
            Func<string, bool> action = choice => { called = true; return true; };
            Invoke("WithDirty", action);
            Check(Root.Q<Button>("save-and-continue") != null && Root.Q<Button>("discard-and-continue") != null,
                "internal file transition retains save/discard/cancel policy");
            Click("cancel");
            Check(!called && Root.Q("modal") == null, "dirty transition cancel never starts storage action");
            VerifyPreserved("dirty file transition cancellation");
            Invoke("TryOpenInternalDefault");
            VerifyPreserved("existing session precedes internal default project");
        }

        private static void VerifyReenable()
        {
            AuthoringEditSession session = screen.Workspace.Session;
            screen.enabled = false;
            screen.enabled = true;
            Check(ReferenceEquals(session, screen.Workspace.Session) && Root.Q("modal") == null && !(bool)Get("busy"),
                "reenable retains memory session without another recovery choice");
            Check(Root.Q<DropdownField>("edit-layer").value == "Cover", "reenable retains recovered editing layer");
            VerifyPreserved("reenable preserves selected cells and history");
            string id = session.SelectedLevelId;
            session.Redo();
            Check((string)session.Get(id).Data["displayName"] == "Redo 화면 검사", "reenabled redo applies original pending change");
            session.Undo();
            VerifyPreserved("undo returns to exact recovered state");
        }

        private static void VerifyPreserved(string context)
        {
            AuthoringEditSession session = screen.Workspace.Session;
            Check(session != null && session.ExportState() == expectedState && session.IsDirty && session.CanUndo && session.CanRedo &&
                session.SelectedCells.SequenceEqual(new[] { 10, 11 }), context);
        }

        private static object Get(string name) => typeof(LevelToolScreen).GetField(name, Flags).GetValue(screen);
        private static void Set(string name, object value) => typeof(LevelToolScreen).GetField(name, Flags).SetValue(screen, value);
        private static object Invoke(string name, params object[] arguments) =>
            (typeof(LevelToolScreen).GetMethod(name, Flags) ?? throw new Exception("Missing LevelToolScreen." + name)).Invoke(screen, arguments);

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
