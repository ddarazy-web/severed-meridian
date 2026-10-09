using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using GameScreen;
using GameScreen.Editor;
using LevelAuthoring.Storage;
using Levels.Editor;
using Levels;
using Newtonsoft.Json.Linq;
using Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    // 실제 도메인 재로드와 게임 씬 진입을 거쳐 편집 초안 복원을 검사한다.
    [InitializeOnLoad]
    public static class JsonWindowPlayVerification
    {
        private const string Key = "JsonStage03.WindowPlay.";
        private const string Output = "Logs/GameAuthoringStage03/window-play-results.txt";
        private const string Baseline = "Logs/GameAuthoringStage03/window-play-baseline.json";
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static JsonWindowPlayVerification() { EditorApplication.update += Tick; }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 검사에서 실행하세요.");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Output)); File.WriteAllText(Output, "");
                SessionState.SetBool(Key + "optionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
                SessionState.SetInt(Key + "options", (int)EditorSettings.enterPlayModeOptions);
                SessionState.SetString(Key + "start", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                EditorSettings.enterPlayModeOptionsEnabled = false;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var source = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-export.txt")).Read();
                string root = Path.GetFullPath("ContentData/Trials/game-authoring-stage-03/play-" + Guid.NewGuid().ToString("N"));
                var saved = new ContentSnapshotStore(root).Publish(source.Snapshot, null);
                var window = ScriptableObject.CreateInstance<LevelEditorWindow>(); window.Show(); window.CreateGUI();
                Invoke(window, "OpenJsonWorkspace", root);
                var workspace = Workspace(window);
                int moves = window.CurrentLevel.MoveCount;
                ((LevelBoardView)typeof(LevelEditorWindow).GetField("board", Flags).GetValue(window)).Layer = PlacementLayer.Obstacle;
                Invoke(window, "SelectCell", new Board.BoardCoordinate(3, 4));
                Invoke(window, "EditLevel", "미저장 플레이 검사", (Action)(() =>
                    JsonUtility.FromJsonOverwrite("{\"moveCount\":" + (moves + 7) + "}", window.CurrentLevel)));
                Check(workspace.Session.IsDirty, "미저장 초안 생성");
                typeof(LevelEditorWindow).GetField("gameTutorialMode", Flags).SetValue(window, TutorialRunMode.Never);
                typeof(LevelEditorWindow).GetField("gameLevelSeed", Flags).SetValue(window, 481);
                var files = new JObject();
                foreach (var asset in LegacyContentExporter.Discover())
                {
                    string path = AssetDatabase.GetAssetPath(asset.Asset);
                    files[path] = Hash(path);
                }
                string levelKey = "MoonRabbit.Tutorial.Completed." + window.CurrentLevel.LevelNumber;
                string identityKey = "MoonRabbit.Tutorial.Identity." + window.CurrentLevel.Tutorial.completionId;
                File.WriteAllText(Baseline, new JObject
                {
                    ["root"] = root, ["hash"] = saved.Hash, ["files"] = files,
                    ["id"] = workspace.Session.SelectedLevelId, ["moves"] = moves + 7,
                    ["number"] = window.CurrentLevel.LevelNumber, ["document"] = workspace.Session.Get(workspace.Session.SelectedLevelId).Data,
                    ["levelKey"] = levelKey, ["identityKey"] = identityKey,
                    ["levelHad"] = PlayerPrefs.HasKey(levelKey), ["identityHad"] = PlayerPrefs.HasKey(identityKey),
                    ["levelValue"] = PlayerPrefs.GetInt(levelKey), ["identityValue"] = PlayerPrefs.GetInt(identityKey)
                }.ToString());
                SessionState.SetInt(Key + "window", window.GetInstanceID());
                SessionState.SetFloat(Key + "deadline", (float)EditorApplication.timeSinceStartup + 180);
                SessionState.SetInt(Key + "phase", 1);
                Invoke(window, "LaunchSelectedGame");
                Check(PuzzleEditorLauncher.IsBusy, "실제 게임 플레이 명령 실행");
            }
            catch (Exception error) { Fail(error); }
        }

        private static void Tick()
        {
            int phase = SessionState.GetInt(Key + "phase", 0);
            if (phase == 0) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "deadline", 0)) throw new Exception("게임 진입/복귀 시간 초과");
                if (phase == 1 && EditorApplication.isPlaying)
                {
                    var session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                    if (session == null) return;
                    if (session.HasFailed) throw new Exception(session.Message);
                    if (!session.IsReady || session.IsPresenting || session.HasProgressFeedback) return;
                    var baseline = JObject.Parse(File.ReadAllText(Baseline));
                    Check(session.State.LevelNumber == (int)baseline["number"] && session.State.MovesRemaining == (int)baseline["moves"], "게임 씬이 미저장 JSON 현재값 사용");
                    Check(session.PlayContext.IsTest && !session.PlayContext.AllowLevelAdvance && !session.PlayContext.Tutorial.UsesPlayerProgress, "시험 전용 진행/저장 정책");
                    SessionState.SetInt(Key + "phase", 2);
                    EditorApplication.ExitPlaymode();
                }
                else if (phase == 2 && !EditorApplication.isPlayingOrWillChangePlaymode && !PuzzleEditorLauncher.IsBusy)
                {
                    var baseline = JObject.Parse(File.ReadAllText(Baseline));
                    var window = EditorUtility.InstanceIDToObject(SessionState.GetInt(Key + "window", 0)) as LevelEditorWindow;
                    Check(window != null, "도메인 재로드 후 편집 창 유지");
                    var workspace = Workspace(window);
                    Check(workspace != null && workspace.Session.IsDirty && workspace.Session.SelectedLevelId == (string)baseline["id"], "선택 레벨과 미저장 상태 복원");
                    Check(JToken.DeepEquals(workspace.Session.Get(workspace.Session.SelectedLevelId).Data, baseline["document"]), "편집 문서 전체 보존");
                    Check(workspace.Session.SelectedCells.SequenceEqual(new[] { 31 }), "선택 셀 복원");
                    Check(((LevelBoardView)typeof(LevelEditorWindow).GetField("board", Flags).GetValue(window)).Layer == PlacementLayer.Obstacle, "편집 층 복원");
                    Invoke(window, "UndoJson", false);
                    Check(window.CurrentLevel.MoveCount == (int)baseline["moves"] - 7, "PlayMode 복귀 후 Undo 복원");
                    Invoke(window, "UndoJson", true);
                    Check(window.CurrentLevel.MoveCount == (int)baseline["moves"], "PlayMode 복귀 후 Redo 복원");
                    Check(new ContentSnapshotStore((string)baseline["root"]).Read().Hash == (string)baseline["hash"], "JSON 디스크 원본 불변");
                    foreach (var file in ((JObject)baseline["files"]).Properties()) Check(Hash(file.Name) == (string)file.Value, "SO 원본 불변 " + file.Name);
                    foreach (string prefix in new[] { "level", "identity" })
                    {
                        string key = (string)baseline[prefix + "Key"];
                        Check(PlayerPrefs.HasKey(key) == (bool)baseline[prefix + "Had"] && PlayerPrefs.GetInt(key) == (int)baseline[prefix + "Value"], "정식 튜토리얼 기록 불변 " + prefix);
                    }
                    typeof(EditorWindow).GetProperty("hasUnsavedChanges").GetSetMethod(true).Invoke(window, new object[] { false });
                    UnityEngine.Object.DestroyImmediate(window);
                    Finish(0);
                }
            }
            catch (Exception error) { Fail(error); }
        }
        private static JsonAuthoringWorkspace Workspace(LevelEditorWindow window) => (JsonAuthoringWorkspace)typeof(LevelEditorWindow).GetField("jsonWorkspace", Flags).GetValue(window);
        private static object Invoke(LevelEditorWindow window, string method, params object[] args) => typeof(LevelEditorWindow).GetMethod(method, Flags).Invoke(window, args);
        private static string Hash(string path) { using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
        private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); File.AppendAllText(Output, "PASS " + name + "\n"); }
        private static void Fail(Exception error) { Debug.LogException(error); File.AppendAllText(Output, "FAIL " + error + "\n"); Finish(1); }
        private static void Finish(int code)
        {
            SessionState.SetInt(Key + "phase", 0);
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + "optionsEnabled", false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Key + "options", 0);
            string start = SessionState.GetString(Key + "start", "");
            EditorSceneManager.playModeStartScene = start == "" ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(start);
            EditorApplication.Exit(code);
        }
    }
}
