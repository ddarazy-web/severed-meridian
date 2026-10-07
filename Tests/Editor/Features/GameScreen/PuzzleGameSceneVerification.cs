using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Levels;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static class PuzzleGameSceneVerification
    {
        private const string Key = "StageTwo.SceneVerify";
        private const string Output = "Logs/PuzzleGameplayVerification/";
        private static readonly List<string> results = new List<string>();
        static PuzzleGameSceneVerification()
        {
            SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (!Application.isPlaying || !SessionState.GetBool(Key + ".Ordinary", false)) return;
                foreach (PuzzleGameSession session in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PuzzleGameSession>(true)))
                    session.ConfigureTutorial(Tutorial.TutorialExecutionContext.CreateTest(2));
            };
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredEditMode) SessionState.EraseBool(Key + ".Ordinary");
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                { SessionState.SetBool(Key, false); VerifyAsync().Forget(Debug.LogException); }
            };
        }
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            PuzzleGameAssets.GenerateGameplay(); PuzzleGameAssets.GenerateGameplay();
            foreach (string path in Directory.GetFiles(PuzzleGameAssets.Folder, "*.prefab").Append(PuzzleGameAssets.ScenePath))
                if (AssetDatabase.GetDependencies(path, true).Any(p => AssetDatabase.LoadAssetAtPath<LevelDefinition>(p) != null))
                    throw new Exception("레벨 에셋 직접 참조: " + path);
            EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath);
            SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
        }
        public static void OpenInteractive()
        {
            // 기존 화면/팝업 회귀는 일반 플레이를 검사한다. 출시 안내/기록은 전용 검사에서 다룬다.
            SessionState.SetBool(Key + ".Ordinary", true);
            EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.EnterPlaymode();
        }
        private static async UniTask VerifyAsync()
        {
            InputSettings previous = InputSystem.settings;
            InputSettings settings = UnityEngine.Object.Instantiate(previous);
            Mouse mouse = null; int exitCode = 0;
            results.Clear();
            try
            {
                PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                await Ready(session);
                Check(UnityEngine.Object.FindObjectsByType<PuzzleGameSession>(FindObjectsSortMode.None).Length == 1, "생성 2회 후 세션 하나");
                Check(!UnityEngine.Object.FindFirstObjectByType<PuzzleBoardPreview>().enabled, "Preview 중복 실행 차단");
                Check(session.State.LevelNumber == 1, "실제 씬 MemoryPack 레벨 1");
                Camera camera = session.BoardCamera;
                PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
                PuzzleBoardInput input = session.GetComponent<PuzzleBoardInput>();
                Capture(camera, "gameplay-landscape.png", 1280, 720);
                Capture(camera, "gameplay-portrait.png", 720, 1280);
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = settings; mouse = InputSystem.AddDevice<Mouse>();
                ActionCandidate action = ActionQuery.Find(session.State).First(a => a.Kind == QueryActionKind.SwapMatch && a.Matches.Any(m => m.Kind == MatchKind.Rocket));
                int initialMoves = session.State.MovesRemaining;
                int moves = initialMoves;
                Tap(action.First); Tap(action.Second.Value);
                Check(session.State.MovesRemaining == moves - 1, "실제 씬 마우스 두 탭 교환 1회");
                typeof(PuzzleGameSession).GetMethod("AdvancePresentation", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(session, new object[] { 1f });
                typeof(PuzzleGameSession).GetMethod("AdvancePresentation", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(session, new object[] { 1f });
                Check(board.GetComponentsInChildren<SpriteRenderer>().Any(r => r.sprite != null && r.sprite.name.StartsWith("cleaning-rocket")), "실제 매칭 생성 로켓 이미지");
                Capture(camera, "gameplay-created-rocket.png", 1280, 720);
                await Ready(session);
                ActionCandidate power = ActionQuery.Find(session.State).First(a => a.Kind == QueryActionKind.Activate);
                moves = session.State.MovesRemaining; Tap(power.First);
                Check(session.State.MovesRemaining == moves - 1, "실제 씬 파워 탭 발동");
                await Ready(session);
                Capture(camera, "gameplay-after-cascade.png", 1280, 720);
                UnityEngine.Object oldSession = session;
                await EditorSceneManager.LoadSceneAsyncInPlayMode(PuzzleGameAssets.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                await UniTask.Yield();
                session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>(); await Ready(session);
                Check(oldSession == null && UnityEngine.Object.FindObjectsByType<PuzzleGameSession>(FindObjectsSortMode.None).Length == 1, "씬 종료 및 재진입 세션 소유권");
                Check(session.State.MovesRemaining == initialMoves, "재진입 초기 이동 수 복원");

                void Tap(Board.BoardCoordinate at)
                {
                    Vector2 position = camera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(at)));
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = 1 }); InputSystem.Update(); Poll();
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); InputSystem.Update(); Poll();
                }
                void Poll() => typeof(PuzzleBoardInput).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(input, null);
            }
            catch (Exception error) { results.Add("FAIL " + error); exitCode = 1; }
            finally
            {
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                InputSystem.settings = previous; UnityEngine.Object.Destroy(settings);
                File.WriteAllLines(Output + "scene-results.txt", results);
                if (Application.isBatchMode) EditorApplication.Exit(exitCode); else EditorApplication.ExitPlaymode();
            }
        }
        private static async UniTask Ready(PuzzleGameSession session)
        {
            await UniTask.WaitUntil(() => session.CanAcceptInput || session.Outcome != null || session.Message.Contains("실패") || session.Message.Contains("중단")).Timeout(TimeSpan.FromSeconds(60));
            Check(session.CanAcceptInput, "실제 씬 입력 준비: " + session.Message);
        }
        private static void Capture(Camera camera, string file, int width, int height)
        {
            RenderTexture render = RenderTexture.GetTemporary(width, height, 24);
            RenderTexture previous = RenderTexture.active, target = camera.targetTexture;
            float size = camera.orthographicSize;
            Rect oldRect = camera.rect;
            float oldAspect = camera.aspect;
            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = render; camera.orthographicSize = (PuzzleWorldBoard.HalfHeight + 0.7f) / Mathf.Min(1, (float)width / height);
                camera.rect = new Rect(0, 0, 1, 1); camera.aspect = (float)width / height;
                camera.Render(); RenderTexture.active = render; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(Output + file, image.EncodeToPNG());
                Check(image.GetPixels32().Count(p => p.r > 180 && p.b > 100 && p.g < 190) > 500, "화면에 실제 토끼 픽셀: " + file);
            }
            finally
            { camera.targetTexture = target; camera.orthographicSize = size; camera.rect = oldRect; camera.aspect = oldAspect; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(render); UnityEngine.Object.Destroy(image); }
        }
        private static void Check(bool pass, string name) { if (!pass) throw new Exception(name); results.Add("PASS " + name); }
    }
}
