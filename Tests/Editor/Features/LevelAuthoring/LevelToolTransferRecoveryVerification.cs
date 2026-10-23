using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.IO.Compression;
using Cysharp.Threading.Tasks;
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
    public static class LevelToolTransferRecoveryVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static string directory, expected;
        private static double next, deadline;
        private static int phase;
        private static bool priorEnabled;
        private static EnterPlayModeOptions priorOptions;
        private static LevelToolScreen screen;
        private static bool transferRun;
        private static MemoryStream exported;
        private static string extendedMode, originalFolder, originalHash, levelId;
        private static AuthoringEditSession originalSession;
        private static FaultStream transferStream;
        private static FakeTransfer fakeTransfer;
        private static JObject recoveryRecord;
        private static readonly string[] SuiteCases = { "Export", "ImportSuccess", "ImportCancel", "ImportTransitionCancel",
            "ImportReadFailure", "ImportCloseFailure", "ExportWriteFailure", "ExportFlushFailure", "ExportCloseFailure", "ExportCancel", "ExportSaveFailure",
            "ExportOpenFailure", "ExportSourceChanged", "Back", "LeaveSaveFailure", "Pause", "ReloadRecovery" };
        private static bool suiteRun;
        private static int suiteIndex;
        private static string suiteDirectory;
        private static bool exitRaised;
        public static void RunPause() { suiteRun = false; extendedMode = null; transferRun = false; Begin(); }
        public static void RunExport() { suiteRun = false; extendedMode = null; transferRun = true; Begin(); }
        public static void RunExportOpenFailure() { suiteRun = false; extendedMode = "ExportOpenFailure"; Begin(); }
        public static void RunExportSourceChanged() { suiteRun = false; extendedMode = "ExportSourceChanged"; Begin(); }
        public static void RunImportSuccess() { suiteRun = false; extendedMode = "ImportSuccess"; Begin(); }
        public static void RunImportCancel() { suiteRun = false; extendedMode = "ImportCancel"; Begin(); }
        public static void RunImportTransitionCancel() { suiteRun = false; extendedMode = "ImportTransitionCancel"; Begin(); }
        public static void RunImportReadFailure() { suiteRun = false; extendedMode = "ImportReadFailure"; Begin(); }
        public static void RunImportCloseFailure() { suiteRun = false; extendedMode = "ImportCloseFailure"; Begin(); }
        public static void RunExportFlushFailure() { suiteRun = false; extendedMode = "ExportFlushFailure"; Begin(); }
        public static void RunExportCloseFailure() { suiteRun = false; extendedMode = "ExportCloseFailure"; Begin(); }
        public static void RunExportCancel() { suiteRun = false; extendedMode = "ExportCancel"; Begin(); }
        public static void RunReloadRecovery() { suiteRun = false; extendedMode = "ReloadRecovery"; Begin(); }
        public static void RunBack() { suiteRun = false; extendedMode = "Back"; Begin(); }
        public static void RunLeaveSaveFailure() { suiteRun = false; extendedMode = "LeaveSaveFailure"; Begin(); }
        public static void RunAll() { suiteRun = true; suiteIndex = 0; extendedMode = null; transferRun = true; Begin(); }
        private static void Begin()
        {
            directory = Path.GetFullPath("Logs/AndroidLevelToolStage04/recovery-" + Guid.NewGuid().ToString("N"));
            if (suiteRun) { suiteDirectory = directory; directory = Path.Combine(suiteDirectory, SuiteCases[suiteIndex]); }
            Directory.CreateDirectory(directory);
            priorEnabled = EditorSettings.enterPlayModeOptionsEnabled; priorOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            LevelToolRecoveryIsolation.Configure(directory);
            deadline = EditorApplication.timeSinceStartup + (suiteRun ? 480 : 180); phase = 0; next = 0;
            EditorApplication.update += Tick; EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Recovery UI test timeout");
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
                if (extendedMode != null && phase > 0) { TickExtended(); return; }
                if (phase == 0)
                {
                    screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                    if (screen == null || (bool)Get("busy")) return;
                    LevelToolRecoveryIsolation.Verify(screen, directory);
                    Check(screen.Workspace.Create(Path.Combine(directory, "project"), LevelToolTestProject.Create()), "isolated project");
                    Set("levelSettingsVisible", true); Call("Refresh");
                    phase = 1; next = EditorApplication.timeSinceStartup + 1;
                }
                else if (phase == 1)
                {
                    if (transferRun)
                    {
                        exported = new MemoryStream(); fakeTransfer = new FakeTransfer(exported); Set("fileTransfer", fakeTransfer);
                        var commandType = typeof(LevelToolScreen).GetNestedType("ToolCommandId", BindingFlags.NonPublic);
                        object command = Enum.Parse(commandType, "ExportProject");
                        Check((bool)typeof(LevelToolScreen).GetMethod("TryExecuteCommand", Flags).Invoke(screen, new[] { command }), "export command connected");
                        Check(Root.Q<Label>("export-summary") != null, "export preview shows project metadata before choosing destination");
                        string summary = Root.Q<Label>("export-summary").text;
                        Check(summary.Contains(screen.Workspace.Session.CreateSnapshot().Project.Id) && summary.Contains("레벨 1개") && summary.Contains("콘텐츠 버전 1"), "preview project ID count and version are exact");
                        Click("export-project-confirm");
                    }
                    else Root.Q<TextField>("level-name").Focus();
                    phase = 2; next = EditorApplication.timeSinceStartup + 1;
                }
                else
                {
                    if (transferRun)
                    {
                        if ((bool)Get("busy")) return;
                        Check(!exported.CanWrite, "export closes caller-owned output before completion");
                        Check(System.Text.RegularExpressions.Regex.IsMatch(fakeTransfer.SuggestedName, @"^project_\d{8}-\d{6}-\d{3}\.zip$"), "dated ZIP filename");
                        Check(Root.Q<Label>("status").text.Contains("클라우드 동기화는 해당 서비스"), "local success does not claim cloud sync");
                        string outcome = Root.Q<Label>("status").text;
                        Click("save-status-details");
                        Check(Root.Q<Label>("save-details-outcome")?.text == outcome, "ZIP outcome stays accessible in save details");
                        Click("close-save-details");
                        using (var archive = new ZipArchive(new MemoryStream(exported.ToArray()), ZipArchiveMode.Read))
                            Check(archive.GetEntry("project.json") != null && archive.Entries.Count > 1, "actual UI exports project archive");
                        Check(!screen.Workspace.Session.IsDirty && Root.Q<Label>("status").text.Contains("ZIP 내보내기 완료"),
                            "export success is distinct from internal save; dirty=" + screen.Workspace.Session.IsDirty + "; status=" + Root.Q<Label>("status").text);
                        Finish(0); return;
                    }
                    TextField input = Root.Q<TextField>("level-name");
                    TextElement text = input.Q(className: "unity-base-field__input").Q<TextElement>();
                    ((INotifyValueChanged<string>)text).SetValueWithoutNotify("중단 전 미확정 한글");
                    input.SelectRange(1, 5);
                    expected = screen.Workspace.ExportState();
                    Call("OnApplicationPause", true);
                    JObject record = new ToolDraftStore(directory).Read();
                    Check(record == null, "pause does not write checkpoint");
                    Call("OnApplicationPause", false);
                    Check(screen.Workspace.ExportState() == expected && input.text == "중단 전 미확정 한글", "resume keeps live document and input");
                    Debug.Log("PASS pause without checkpoint, pending input and in-memory history preserved"); Finish(0);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static void TickExtended()
        {
            if (phase == 1)
            {
                originalFolder = screen.Workspace.Folder;
                originalHash = new ContentSnapshotStore(originalFolder).Read().Hash;
                byte[] package;
                using (MemoryStream source = new MemoryStream())
                {
                    ProjectPackageStore.Export(originalFolder, source, CancellationToken.None);
                    package = source.ToArray();
                }
                originalSession = screen.Workspace.Session;
                levelId = originalSession.SelectedLevelId;
                originalSession.SelectCells(new[] { 12, 13 });
                originalSession.Apply("전송 검사 초안", docs => docs[levelId].Data["displayName"] = "이전 편집 초안");
                originalSession.Apply("전송 검사 Redo", docs => docs[levelId].Data["displayName"] = "되살릴 이름");
                originalSession.Undo();
                expected = screen.Workspace.ExportState();
                Call("Refresh");
                if (extendedMode == "Back" || extendedMode == "LeaveSaveFailure")
                {
                    Call("ResetBoardTouch"); Call("CancelPendingInput"); Set("brush", null); Set("mobilePanel", null);
                    exitRaised = false;
                    screen.ExitRequested += OnExitRequested;
                    if (extendedMode == "Back")
                    {
                        Click("menu-file");
                        Check((bool)Get("commandMenuOpen") && Get("modal") != null, "actual file menu opens before back");
                        Call("HandleAndroidBack");
                        Check(!(bool)Get("commandMenuOpen") && Get("modal") == null && !exitRaised,
                            "back closes menu before requesting exit");
                        Set("mobilePanel", "properties");
                        Call("HandleAndroidBack");
                        Check(Get("mobilePanel") == null && Get("modal") == null && !exitRaised,
                            "back closes properties panel before requesting exit");
                        Call("HandleAndroidBack");
                        Check(Root.Q<Button>("save-and-leave") != null && !exitRaised,
                            "back with no active tool or panel offers dirty exit choices");
                        Click("cancel");
                        Check(ReferenceEquals(screen.Workspace.Session, originalSession) && screen.Workspace.ExportState() == expected && !exitRaised,
                            "dirty exit cancellation preserves current saved selection undo and redo");
                        Check(new ContentSnapshotStore(originalFolder).Read().Hash == originalHash, "back cancellation never saves original JSON");
                        Finish(0); return;
                    }
                    // 정상 JSON에 공백만 추가해 외부 변경에 따른 저장 해시 충돌을 재현한다.
                    File.AppendAllText(Path.Combine(originalFolder, "project.json"), "\n");
                    originalHash = new ContentSnapshotStore(originalFolder).Read().Hash;
                    screen.RequestLeave(() => exitRaised = true);
                    Check(Root.Q<Button>("save-and-leave") != null, "dirty RequestLeave offers save before exit");
                    phase = 2; next = EditorApplication.timeSinceStartup + 1; return;
                }
                if (extendedMode == "ReloadRecovery")
                {
                    Root.Q<TextField>("level-name").Focus();
                    phase = 2; next = EditorApplication.timeSinceStartup + 1; return;
                }
                bool import = extendedMode.StartsWith("Import", StringComparison.Ordinal);
                transferStream = new FaultStream(import ? package : Array.Empty<byte>(),
                    extendedMode == "ImportReadFailure", extendedMode == "ExportFlushFailure",
                    extendedMode == "ImportCloseFailure" || extendedMode == "ExportCloseFailure", extendedMode == "ExportWriteFailure");
                fakeTransfer = new FakeTransfer(import ? null : transferStream,
                    import && extendedMode != "ImportCancel" ? transferStream : null);
                if (extendedMode == "ExportCancel") fakeTransfer = new FakeTransfer(null);
                if (extendedMode == "ExportOpenFailure") fakeTransfer = new FakeTransfer(null, onCreate: () => throw new IOException("provider-open-fixture"));
                if (extendedMode == "ExportSourceChanged") fakeTransfer = new FakeTransfer(transferStream, onCreate: () =>
                {
                    File.AppendAllText(Path.Combine(originalFolder, "project.json"), "\n");
                    originalHash = new ContentSnapshotStore(originalFolder).Read().Hash;
                });
                Set("fileTransfer", fakeTransfer);
                if (extendedMode == "ExportSaveFailure")
                {
                    File.AppendAllText(Path.Combine(originalFolder, "project.json"), "\n");
                    originalHash = new ContentSnapshotStore(originalFolder).Read().Hash;
                }
                Execute(import ? "ImportProject" : "ExportProject");
                if (!import) Click("export-project-confirm");
                phase = 2; next = EditorApplication.timeSinceStartup + 1; return;
            }
            if (extendedMode == "LeaveSaveFailure")
            {
                if (phase == 2)
                {
                    Click("save-and-leave"); phase = 3; next = EditorApplication.timeSinceStartup + 1; return;
                }
                if ((bool)Get("busy")) return;
                Check(!exitRaised, "save failure does not invoke leave continuation");
                Check(ReferenceEquals(screen.Workspace.Session, originalSession) && screen.Workspace.ExportState() == expected && screen.Workspace.Session.IsDirty,
                    "failed save before exit preserves draft saved baseline selection undo and redo");
                Check(new ContentSnapshotStore(originalFolder).Read().Hash == originalHash,
                    "failed save before exit does not overwrite externally changed JSON");
                Check(Root.Q<Label>("status").text.Contains("종료 취소"), "save failure is reported as cancelled exit");
                Finish(0); return;
            }
            if (extendedMode == "ReloadRecovery")
            {
                if (phase == 2)
                {
                    TextField input = Root.Q<TextField>("level-name");
                    TextElement text = input.Q(className: "unity-base-field__input").Q<TextElement>();
                    ((INotifyValueChanged<string>)text).SetValueWithoutNotify("재실행 전 미확정 한글");
                    input.SelectRange(1, 5);
                    expected = screen.Workspace.ExportState();
                    // 이전 버전에서 생성한 기록을 시험 폴더에 명시적으로 준비한다.
                    var view = new JObject { ["levelSettingsVisible"] = true,
                        ["pendingInputs"] = (JArray)typeof(LevelToolScreen).GetMethod("CaptureRecoveryInputs", Flags).Invoke(screen, null) };
                    new ToolDraftStore(directory).Save(expected, view, 1);
                    Call("OnApplicationPause", true);
                    recoveryRecord = new ToolDraftStore(directory).Read();
                    Check((string)recoveryRecord["workspace"] == expected, "dirty checkpoint retains saved/current/undo/redo");
                    Call("OnApplicationPause", false);
                    Check(screen.Workspace.ExportState() == expected && input.text == "재실행 전 미확정 한글", "live resume preserves pending text and history");
                    // 새 컴포넌트의 빈 작업공간이 실제 EnableRecovery 선택 경로를 타게 한다.
                    screen.enabled = false;
                    JObject disabledRecord = new ToolDraftStore(directory).Read();
                    Check(JToken.DeepEquals(disabledRecord["view"]["pendingInputs"], recoveryRecord["view"]["pendingInputs"]),
                        "disable captures pending inputs before disposing panels");
                    GameObject previous = screen.gameObject;
                    GameObject replacement = UnityEngine.Object.Instantiate(previous);
                    UnityEngine.Object.Destroy(previous);
                    screen = replacement.GetComponent<LevelToolScreen>();
                    screen.enabled = true;
                    phase = 3; next = EditorApplication.timeSinceStartup + 1; return;
                }
                if (phase == 3)
                {
                    if ((bool)Get("busy")) return;
                    Check(Root.Q<Button>("restore-draft") == null && screen.Workspace.Session == null, "fresh screen does not automatically read recovery");
                    Execute("LegacyRecovery"); phase = 31; next = EditorApplication.timeSinceStartup + 1; return;
                }
                if (phase == 31)
                {
                    if ((bool)Get("busy")) return;
                    Check(Root.Q<Button>("restore-draft") != null, "explicit command offers recovery");
                    Click("restore-draft"); phase = 4; next = EditorApplication.timeSinceStartup + 1; return;
                }
                Check(screen.Workspace.ExportState() == expected, "fresh screen restores exact saved/current/history/selection");
                Check(!ReferenceEquals(screen.Workspace.Session, originalSession), "recovery constructs a new session");
                TextField restored = Root.Q<TextField>("level-name");
                Check(restored != null && restored.text == "재실행 전 미확정 한글", "RestoreRecovery reapplies pending text after view rebuild");
                Check(screen.Workspace.Session.CanUndo && screen.Workspace.Session.CanRedo, "recovered undo and redo remain available");
                screen.Workspace.Session.Redo();
                Check((string)screen.Workspace.Session.Get(levelId).Data["displayName"] == "되살릴 이름", "recovered redo performs the original change");
                screen.Workspace.Session.Undo();
                Check((string)screen.Workspace.Session.Get(levelId).Data["displayName"] == "이전 편집 초안", "recovered undo returns to checkpoint draft");
                Check(new ContentSnapshotStore(originalFolder).Read().Hash == originalHash, "recovery does not publish the pending draft");
                Check(JToken.DeepEquals(new ToolDraftStore(directory).Read(), recoveryRecord), "explicit recovery preserves original legacy record");
                screen.Workspace.SaveCopy(Path.Combine(directory, "manual-copy"));
                Check(!screen.Workspace.Session.IsDirty && screen.Workspace.Session.CanUndo, "explicit recovered copy saves current JSON and retains Undo");
                Check(new ContentSnapshotStore(originalFolder).Read().Hash == originalHash &&
                    JToken.DeepEquals(new ToolDraftStore(directory).Read(), recoveryRecord), "copy save preserves source JSON and legacy recovery");
                Finish(0); return;
            }
            bool importing = extendedMode.StartsWith("Import", StringComparison.Ordinal);
            if (phase == 2 && importing)
            {
                TextField name = Root.Q<TextField>("import-project-name");
                Check(name != null, "actual import command opens project naming modal");
                name.value = "검증 가져오기";
                Click("import-project-confirm"); phase = 3; next = EditorApplication.timeSinceStartup + 1; return;
            }
            if ((bool)Get("busy")) return;
            if (importing)
            {
                if (phase == 3)
                {
                    Check(fakeTransfer.OpenCalls == 1, "actual import calls the stream picker once");
                    Check(ReferenceEquals(screen.Workspace.Session, originalSession) && screen.Workspace.ExportState() == expected,
                        "import does not replace old session before successful dirty transition");
                    Check(new ContentSnapshotStore(originalFolder).Read().Hash == originalHash, "import preserves original published JSON");
                    if (extendedMode == "ImportSuccess" || extendedMode == "ImportTransitionCancel")
                    {
                        Check(transferStream.Closed, "import closes input before asking to switch");
                        Check(Root.Q<Button>("discard-and-continue") != null, "validated import follows existing dirty transition contract");
                        if (extendedMode == "ImportTransitionCancel")
                        {
                            Click("cancel");
                            Check(ReferenceEquals(screen.Workspace.Session, originalSession) && screen.Workspace.ExportState() == expected,
                                "dirty transition cancel preserves draft selection undo and redo");
                            string root = (string)typeof(LevelToolScreen).GetProperty("InternalProjectsRoot", Flags).GetValue(screen);
                            Check(File.Exists(Path.Combine(root, "검증 가져오기", "project.json")), "validated import remains reopenable after transition cancel");
                            Finish(0); return;
                        }
                        Click("discard-and-continue"); phase = 4; next = EditorApplication.timeSinceStartup + 1; return;
                    }
                    Check(Root.Q<Button>("discard-and-continue") == null, "failed or cancelled import never offers a project transition");
                    string status = Root.Q<Label>("status").text;
                    if (extendedMode == "ImportCancel") Check(status.Contains("전송 취소"), "picker cancellation is distinct from failure");
                    else
                    {
                        Check(transferStream.Closed, "failed import still closes its input");
                        Check(status.Contains("가져오기 실패") && status.Contains(extendedMode == "ImportReadFailure" ? "read-fixture" : "close-fixture"),
                            "read or close failure is reported without replacing the session");
                    }
                    Finish(0); return;
                }
                if (phase == 4)
                {
                    Check(screen.Workspace.Session == null && screen.Workspace.Directory != null,
                        "import opens metadata listing without eagerly editing all levels");
                    Call("RequestOpenLevel", levelId, null);
                    phase = 5; next = EditorApplication.timeSinceStartup + 1; return;
                }
                Check(screen.Workspace.Session != null && !ReferenceEquals(screen.Workspace.Session, originalSession) && !screen.Workspace.Session.IsDirty,
                    "successful dirty discard opens imported saved session");
                Check(Path.GetFileName(screen.Workspace.Folder) == "검증 가져오기" &&
                    (string)screen.Workspace.Session.Get(levelId).Data["displayName"] != "이전 편집 초안",
                    "import opens saved archive rather than outgoing dirty draft");
                Check(new ContentSnapshotStore(originalFolder).Read().Hash == originalHash, "successful import leaves outgoing source JSON unchanged");
                Finish(0); return;
            }
            if (extendedMode == "ExportSaveFailure")
            {
                Check(fakeTransfer.CreateCalls == 0, "failed internal save never opens output picker");
                Check(screen.Workspace.Session.IsDirty && new ContentSnapshotStore(originalFolder).Read().Hash == originalHash, "failed save preserves unsaved draft and external changes");
                Check(Root.Q<Label>("status").text.Contains("내보내기 실패") && !Root.Q<Label>("status").text.Contains("ZIP 내보내기 완료"), "failed save never reports export success");
                Finish(0); return;
            }
            Check(fakeTransfer.CreateCalls == 1, "actual export calls output picker after internal save");
            if (extendedMode == "ExportOpenFailure" || extendedMode == "ExportSourceChanged")
            {
                string message = Root.Q<Label>("status").text;
                Check(message.Contains("내보내기 실패") && message.Contains("불완전") && !message.Contains("ZIP 내보내기 완료"), "provider open failure or source conflict warns without success");
                if (extendedMode == "ExportSourceChanged")
                    Check(transferStream.Closed && new ContentSnapshotStore(originalFolder).Read().Hash == originalHash, "source conflict closes output and preserves external data");
                Finish(0); return;
            }
            Check(ReferenceEquals(screen.Workspace.Session, originalSession) && screen.Workspace.Folder == originalFolder,
                "export failure does not replace the current session or folder");
            Check(!screen.Workspace.Session.IsDirty && new ContentSnapshotStore(originalFolder).Read().Hash != originalHash,
                "internal save succeeds independently of external output");
            Check(screen.Workspace.Session.CanUndo && screen.Workspace.Session.CanRedo,
                "external failure retains current undo and redo history");
            string resultStatus = Root.Q<Label>("status").text;
            if (extendedMode == "ExportCancel") Check(resultStatus.Contains("전송 취소") && resultStatus.Contains("내부 저장본 유지"), "export cancel retains saved internal project");
            else
            {
                Check(transferStream.Closed, "failed export disposes output");
                Check(resultStatus.Contains("내보내기 실패") && resultStatus.Contains("내부 저장본 유지") &&
                    resultStatus.Contains(extendedMode == "ExportWriteFailure" ? "write-fixture" : extendedMode == "ExportFlushFailure" ? "flush-fixture" : "close-fixture") && !resultStatus.Contains("ZIP 내보내기 완료"),
                    "flush or close failure cannot display ZIP export success");
            }
            Finish(0);
        }

        private static void Execute(string name)
        {
            Type commandType = typeof(LevelToolScreen).GetNestedType("ToolCommandId", BindingFlags.NonPublic);
            object command = Enum.Parse(commandType, name);
            Check((bool)typeof(LevelToolScreen).GetMethod("TryExecuteCommand", Flags).Invoke(screen, new[] { command }), name + " command connected");
        }

        private static void Click(string name)
        {
            Button button = Root.Q<Button>(name) ?? throw new Exception("Missing transfer control: " + name);
            typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
        }
        private static VisualElement Root => screen.GetComponent<UIDocument>().rootVisualElement;
        private static object Get(string name) => typeof(LevelToolScreen).GetField(name, Flags).GetValue(screen);
        private static void Set(string name, object value) => typeof(LevelToolScreen).GetField(name, Flags).SetValue(screen, value);
        private static void Call(string name, params object[] args) => typeof(LevelToolScreen).GetMethod(name, Flags).Invoke(screen, args);
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
        private static void OnExitRequested() { exitRaised = true; }
        private static void Finish(int code)
        {
            if (screen != null) screen.ExitRequested -= OnExitRequested;
            if (suiteRun && code == 0)
            {
                Debug.Log("PASS transfer/recovery UI case " + SuiteCases[suiteIndex]);
                suiteIndex++;
                if (suiteIndex < SuiteCases.Length)
                {
                    extendedMode = SuiteCases[suiteIndex];
                    transferRun = extendedMode == "Export";
                    if (extendedMode == "Export" || extendedMode == "Pause") extendedMode = null;
                    directory = Path.Combine(suiteDirectory, SuiteCases[suiteIndex]);
                    Directory.CreateDirectory(directory);
                    Call("CloseModal"); Call("DisposeFileTransfer");
                    Root.focusController?.focusedElement?.Blur();
                    Set("recoveryDirectory", directory);
                    Set("draftStore", new ToolDraftStore(directory));
                    Set("recentProjects", null);
                    Check(screen.Workspace.Create(Path.Combine(directory, "project"), LevelToolTestProject.Create(), "Discard"), "next isolated suite project");
                    Set("levelSettingsVisible", true); Call("Refresh");
                    phase = 1; next = EditorApplication.timeSinceStartup + 1;
                    return;
                }
                Debug.Log("PASS LevelToolTransferRecoveryVerification.RunAll: actual UI transfer cases, stream failure boundaries, live resume and fresh-screen recovery");
            }
            EditorApplication.update -= Tick;
            EditorSettings.enterPlayModeOptionsEnabled = priorEnabled; EditorSettings.enterPlayModeOptions = priorOptions;
            EditorApplication.Exit(code);
        }
        private sealed class FakeTransfer : ILevelToolFileTransfer
        {
            private readonly Stream output, input;
            private readonly Action onCreate;
            public int OpenCalls { get; private set; }
            public int CreateCalls { get; private set; }
            public string SuggestedName { get; private set; }
            public FakeTransfer(Stream output, Stream input = null, Action onCreate = null) { this.output = output; this.input = input; this.onCreate = onCreate; }
            public UniTask<Stream> OpenProject(CancellationToken token) { OpenCalls++; return UniTask.FromResult(input); }
            public UniTask<Stream> CreateProject(string suggestedName, CancellationToken token) { CreateCalls++; SuggestedName = suggestedName; onCreate?.Invoke(); return UniTask.FromResult(output); }
        }

        private sealed class FaultStream : MemoryStream
        {
            private readonly bool failRead, failFlush, failClose, failWrite;
            public bool Closed { get; private set; }
            public FaultStream(byte[] bytes, bool failRead, bool failFlush, bool failClose, bool failWrite = false)
            {
                this.failRead = failRead; this.failFlush = failFlush; this.failClose = failClose;
                Write(bytes, 0, bytes.Length); Position = 0;
                this.failWrite = failWrite;
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                if (failWrite) throw new IOException("write-fixture");
                base.Write(buffer, offset, count);
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (failRead) throw new IOException("read-fixture");
                return base.Read(buffer, offset, count);
            }
            public override void Flush()
            {
                if (failFlush) throw new IOException("flush-fixture");
                base.Flush();
            }
            protected override void Dispose(bool disposing)
            {
                Closed = true;
                base.Dispose(disposing);
                if (disposing && failClose) throw new IOException("close-fixture");
            }
        }
    }
}
