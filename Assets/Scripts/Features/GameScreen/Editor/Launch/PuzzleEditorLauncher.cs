using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Levels;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static class PuzzleEditorLauncher
    {
        private const string Key = "Puzzle.EditorLaunch.";
        public static bool IsBusy => SessionState.GetString(Key + "id", "") != "";
        public static event Action<int, string> Finished;

        static PuzzleEditorLauncher()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += CheckCancelledEntry;
        }

        public static void Launch(PuzzleEditorLaunchRequest request, int ownerWindowId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (IsBusy || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("컴파일 또는 게임 실행·전환 중에는 새 게임을 실행할 수 없습니다.");
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PuzzleGameAssets.ScenePath);
            if (scene == null) throw new InvalidOperationException("PuzzleGame 씬을 찾을 수 없습니다: " + PuzzleGameAssets.ScenePath);
            SessionState.SetString(Key + "previousScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetString(Key + "id", Guid.NewGuid().ToString("N"));
            SessionState.SetString(Key + "bytes", request.EncodedBytes);
            SessionState.SetInt(Key + "number", request.LevelNumber);
            SessionState.SetInt(Key + "seed", request.Seed);
            SessionState.SetInt(Key + "source", (int)request.Source);
            SessionState.SetInt(Key + "owner", ownerWindowId);
            SessionState.SetString(Key + "message", "게임을 종료하고 편집으로 돌아왔습니다.");
            SessionState.SetFloat(Key + "requestedAt", (float)EditorApplication.timeSinceStartup);
            try
            {
                EditorSceneManager.playModeStartScene = scene;
                EditorApplication.EnterPlaymode();
            }
            catch
            {
                Finish("게임 진입에 실패했습니다.");
                throw;
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsBusy || scene.path != PuzzleGameAssets.ScenePath) return;
            string encoded = SessionState.GetString(Key + "bytes", "");
            if (encoded == "") return;
            // Start보다 앞서 요청을 소비한다. 씬을 다시 로드해도 이전 입력을 재사용하지 않는다.
            SessionState.EraseString(Key + "bytes");
            LevelDefinition definition = null;
            try
            {
                PuzzleGameSession session = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PuzzleGameSession>(true)).Single();
                session.SetLevelAdvanceEnabled(SessionState.GetInt(Key + "source", (int)PuzzleEditorLevelSource.Asset) == (int)PuzzleEditorLevelSource.MemoryPack);
                definition = LevelPackCodec.ReadLevel(Convert.FromBase64String(encoded), SessionState.GetInt(Key + "number", 0));
                LevelDefinition owned = definition; definition = null;
                session.InitializeAsync(owned, SessionState.GetInt(Key + "seed", 12345), CancellationToken.None).Forget(error =>
                {
                    SessionState.SetString(Key + "message", "게임 초기화 실패: " + error.Message);
                    Debug.LogException(error);
                    EditorApplication.ExitPlaymode();
                });
            }
            catch (Exception error)
            {
                if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
                SessionState.SetString(Key + "message", "게임 초기화 실패: " + error.Message);
                Debug.LogException(error);
                EditorApplication.ExitPlaymode();
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!IsBusy) return;
            if (change == PlayModeStateChange.EnteredEditMode)
                Finish(SessionState.GetString(Key + "message", "편집으로 돌아왔습니다."));
            else if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(Key + "bytes", "") != "")
            {
                SessionState.SetString(Key + "message", "게임 씬에서 실행 요청을 전달하지 못했습니다.");
                EditorApplication.ExitPlaymode();
            }
        }

        private static void CheckCancelledEntry()
        {
            // 이미 소비된 요청은 정상 종료의 EnteredEditMode에서 정리한다.
            // 종료 리로드 중 잠깐 편집 상태로 보이는 구간을 진입 취소로 오인하지 않는다.
            if (IsBusy && SessionState.GetString(Key + "bytes", "") != "" &&
                !EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode &&
                EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "requestedAt", 0) > 1)
                Finish("게임 진입이 취소되었습니다.");
        }

        private static void Finish(string message)
        {
            int owner = SessionState.GetInt(Key + "owner", 0);
            string previous = SessionState.GetString(Key + "previousScene", "");
            EditorSceneManager.playModeStartScene = previous == "" ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            foreach (string field in new[] { "id", "bytes", "previousScene", "message" }) SessionState.EraseString(Key + field);
            foreach (string field in new[] { "number", "seed", "owner", "source" }) SessionState.EraseInt(Key + field);
            SessionState.EraseFloat(Key + "requestedAt");
            Finished?.Invoke(owner, message);
        }
    }
}
