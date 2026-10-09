using System;
using System.IO;
using System.Reflection;
using GameScreen;
using GameScreen.Editor;
using LevelAuthoring.Storage;
using Levels;
using Levels.Editor;
using Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    [InitializeOnLoad]
    public static class JsonFlowDraftPlayVerification
    {
        private const string Key = "JsonStage03.FlowDraftPlay.";
        private const string Output = "Logs/GameAuthoringStage03/flow-draft-play-results.txt";
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static JsonFlowDraftPlayVerification() { EditorApplication.update += Tick; }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 검사에서 실행하세요.");
            try
            {
                File.WriteAllText(Output, "");
                SessionState.SetBool(Key + "optionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
                SessionState.SetInt(Key + "options", (int)EditorSettings.enterPlayModeOptions);
                SessionState.SetString(Key + "start", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                EditorSettings.enterPlayModeOptionsEnabled = false;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var source = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-export.txt")).Read();
                string root = Path.GetFullPath("ContentData/Trials/game-authoring-stage-03/flow-play-" + Guid.NewGuid().ToString("N"));
                var saved = new ContentSnapshotStore(root).Publish(source.Snapshot, null);
                var owner = ScriptableObject.CreateInstance<LevelEditorWindow>(); owner.Show(); owner.CreateGUI();
                SessionState.SetInt(Key + "owner", owner.GetInstanceID());
                Invoke(owner, "OpenJsonWorkspace", root);
                var workspace = Workspace(owner);
                TutorialFlowDefinition flow = null;
                string flowId = null;
                workspace.Execute("공통 흐름 준비", () =>
                {
                    flow = ScriptableObject.CreateInstance<TutorialFlowDefinition>(); flowId = workspace.Register(flow, "tutorialFlow");
                    flow.name = "재로드 공통";
                    flow.steps.Add(new TutorialStepDefinition { authoringId = "reload-step", instructions = "원본 안내", highlights = new System.Collections.Generic.List<Board.BoardCoordinate> { new Board.BoardCoordinate(3, 4) } });
                    Tutorial.Editor.TutorialFlowAuthoring.Connect(owner.CurrentLevel, flow);
                });
                Invoke(owner, "PersistJsonState");
                var child = LevelEditorWindow.OpenTutorialFlow(owner.CurrentLevel, flow, owner);
                SessionState.SetInt(Key + "child", child.GetInstanceID());
                child.CurrentLevel.Tutorial.steps[0].instructions = "사본 유지 안내";
                int moves = child.CurrentLevel.MoveCount + 3;
                JsonUtility.FromJsonOverwrite("{\"moveCount\":" + moves + "}", child.CurrentLevel);
                var draft = (TutorialFlowDefinition)typeof(LevelEditorWindow).GetField("tutorialFlowDraft", Flags).GetValue(child);
                draft.parameters.Add(new TutorialFlowParameter { key = "reload-param", stepId = "reload-step", field = TutorialFlowField.Highlights, label = "유지할 설정" });
                typeof(LevelEditorWindow).GetField("gameTutorialMode", Flags).SetValue(child, TutorialRunMode.Never);
                SessionState.SetString(Key + "root", root); SessionState.SetString(Key + "hash", saved.Hash);
                SessionState.SetString(Key + "flow", flowId); SessionState.SetInt(Key + "moves", moves);
                SessionState.SetFloat(Key + "deadline", (float)EditorApplication.timeSinceStartup + 180);
                SessionState.SetInt(Key + "phase", 1);
                Invoke(child, "LaunchSelectedGame");
                Check(PuzzleEditorLauncher.IsBusy, "JSON 자식 사본 게임 시험 시작: " + typeof(LevelEditorWindow).GetField("gameLaunchMessage", Flags).GetValue(child));
            }
            catch (Exception error) { Fail(error); }
        }
        private static void Tick()
        {
            int phase = SessionState.GetInt(Key + "phase", 0);
            if (phase == 0) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "deadline", 0)) throw new Exception("자식 사본 시험 진입/복귀 시간 초과");
                if (phase == 1 && EditorApplication.isPlaying)
                {
                    var game = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                    if (game == null) return;
                    if (game.HasFailed) throw new Exception(game.Message);
                    if (!game.IsReady || game.IsPresenting || game.HasProgressFeedback) return;
                    Check(game.State.MovesRemaining == SessionState.GetInt(Key + "moves", 0) && game.PlayContext.IsTest, "게임이 현재 사본과 시험 문맥 사용");
                    SessionState.SetInt(Key + "phase", 2); EditorApplication.ExitPlaymode();
                }
                else if (phase == 2 && !EditorApplication.isPlayingOrWillChangePlaymode && !PuzzleEditorLauncher.IsBusy)
                {
                    var owner = Window("owner"); var child = Window("child");
                    Check(owner != null && child != null, "부모와 사본 창 재로드 유지");
                    var workspace = Workspace(owner);
                    var flow = (TutorialFlowDefinition)workspace.Resolve(SessionState.GetString(Key + "flow", ""));
                    Check((TutorialFlowDefinition)typeof(LevelEditorWindow).GetField("editingTutorialFlow", Flags).GetValue(child) == flow, "stable ID로 공유 원본 재연결");
                    Check(child.CurrentLevel.ElementCatalog == owner.CurrentLevel.ElementCatalog, "현재 부모 카탈로그 재연결");
                    Check(child.CurrentLevel.MoveCount == SessionState.GetInt(Key + "moves", 0) && child.CurrentLevel.Tutorial.steps[0].instructions == "사본 유지 안내", "사본 레벨과 단계 보존");
                    var draft = (TutorialFlowDefinition)typeof(LevelEditorWindow).GetField("tutorialFlowDraft", Flags).GetValue(child);
                    Check(draft.parameters.Count == 1 && draft.parameters[0].key == "reload-param", "사본 파라미터 보존");
                    var apply = child.rootVisualElement.Q<Button>("tutorial-flow-apply");
                    Check(apply != null, "복귀 후 공통 적용 버튼 유지");
                    typeof(Clickable).GetMethod("Invoke", Flags).Invoke(apply.clickable, new object[] { null });
                    Check(flow.steps[0].instructions == "사본 유지 안내" && flow.parameters.Count == 1, "재로드 후 부모 JSON에 명시적 적용");
                    string folder = (string)typeof(LevelEditorWindow).GetField("jsonFolder", Flags).GetValue(owner);
                    typeof(LevelEditorWindow).GetField("jsonFolder", Flags).SetValue(owner, folder + "-different");
                    object[] args = { null };
                    Check(!(bool)Invoke(child, "TryPrepareJsonDraftTest", args), "다른 작업 폴더 시험 차단");
                    typeof(LevelEditorWindow).GetField("jsonFolder", Flags).SetValue(owner, folder);
                    Close(owner); args = new object[] { null };
                    Check(!(bool)Invoke(child, "TryPrepareJsonDraftTest", args), "부모 종료 후 기본 카탈로그 대체 시험 차단");
                    Check(new ContentSnapshotStore(SessionState.GetString(Key + "root", "")).Read().Hash == SessionState.GetString(Key + "hash", ""), "저장 원본 불변");
                    Close(child); Finish(0);
                }
            }
            catch (Exception error) { Fail(error); }
        }
        private static LevelEditorWindow Window(string name) => EditorUtility.InstanceIDToObject(SessionState.GetInt(Key + name, 0)) as LevelEditorWindow;
        private static JsonAuthoringWorkspace Workspace(LevelEditorWindow window) => (JsonAuthoringWorkspace)typeof(LevelEditorWindow).GetField("jsonWorkspace", Flags).GetValue(window);
        private static object Invoke(LevelEditorWindow window, string name, params object[] args) => typeof(LevelEditorWindow).GetMethod(name, Flags).Invoke(window, args);
        private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); File.AppendAllText(Output, "PASS " + name + "\n"); }
        private static void Fail(Exception error) { Debug.LogException(error); File.AppendAllText(Output, "FAIL " + error + "\n"); Finish(1); }
        private static void Close(LevelEditorWindow window)
        {
            if (window == null) return;
            typeof(EditorWindow).GetProperty("hasUnsavedChanges").GetSetMethod(true).Invoke(window, new object[] { false }); UnityEngine.Object.DestroyImmediate(window);
        }
        private static void Finish(int code)
        {
            SessionState.SetInt(Key + "phase", 0); Close(Window("child")); Close(Window("owner"));
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + "optionsEnabled", false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Key + "options", 0);
            string start = SessionState.GetString(Key + "start", "");
            EditorSceneManager.playModeStartScene = start == "" ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(start);
            EditorApplication.Exit(code);
        }
    }
}
