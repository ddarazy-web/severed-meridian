using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static partial class PuzzlePresentationAcceptanceVerification
    {
        private const string PlayKey = "Stage12.Capture";
        static PuzzlePresentationAcceptanceVerification()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PlayKey, false))
                { SessionState.SetBool(PlayKey, false); CaptureAsync().Forget(Debug.LogException); }
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Stage12.Motion", false))
                { SessionState.SetBool("Stage12.Motion", false); MotionAsync().Forget(Debug.LogException); }
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Stage12.Interaction", false))
                { SessionState.SetBool("Stage12.Interaction", false); InteractionAsync().Forget(Debug.LogException); }
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Stage12.Settlement", false))
                { SessionState.SetBool("Stage12.Settlement", false); SettlementRegressionAsync().Forget(Debug.LogException); }
            };
        }
        public static void RunScene()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            Directory.CreateDirectory(Output + "captures"); PuzzleUIRenderVerification.RememberSize(); SessionState.SetBool(PlayKey, true);
            EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath); EditorApplication.EnterPlaymode();
        }
        private static async UniTask CaptureAsync()
        {
            results.Clear(); int exit = 0; float previous = Time.timeScale;
            List<string> manifest = new List<string> { "file,width,height,safeX,safeY,safeWidth,safeHeight,state,source,level,seed,utc" };
            try
            {
                Application.runInBackground = true; Time.timeScale = 0;
                Vector2Int[] sizes = { new Vector2Int(1280, 720), new Vector2Int(450, 800), new Vector2Int(450, 975), new Vector2Int(600, 800) };
                for (int index = 0; index < sizes.Length; index++)
                {
                    Vector2Int size = sizes[index]; PuzzleUIRenderVerification.SetSize(size.x, size.y);
                    for (int frame = 0; frame < 12; frame++) await UniTask.NextFrame();
                    Check(Screen.width == size.x && Screen.height == size.y, "실제 Game View 해상도 " + size);
                    PuzzleEditorLevelSource mode = index % 2 == 0 ? PuzzleEditorLevelSource.Asset : PuzzleEditorLevelSource.MemoryPack;
                    PuzzleGameSession session = await LoadOriginal(mode);
                    await CaptureState(session, size, false, "play", mode.ToString(), manifest);
                    Click("Pause"); Check(session.IsPaused, "실제 UI pause " + size);
                    await CaptureState(session, size, false, "pause", mode.ToString(), manifest);
                    Check(session.SetPaused(false), "pause 재개");
                    await CaptureState(session, size, true, "inset-play", mode.ToString(), manifest);
                    foreach (bool won in new[] { true, false })
                    {
                        LevelDefinition level = await (UniTask<LevelDefinition>)typeof(PuzzleStabilityVerification)
                            .GetMethod("PrepareFixture", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { session, won ? 3 : 0 });
                        try
                        {
                            if (!won)
                            {
                                JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", level);
                                typeof(PuzzleGameSession).GetField("initialBytes", BindingFlags.NonPublic | BindingFlags.Instance)
                                    .SetValue(session, LevelPackCodec.Encode(new[] { level }));
                                await session.RestartAsync(CancellationToken.None); Invoke(session, "TickProgress", .7f);
                            }
                            BoardActionExecutor direct = (BoardActionExecutor)typeof(PuzzleStabilityVerification).GetMethod("BeginFixture", BindingFlags.NonPublic | BindingFlags.Static)
                                .Invoke(null, new object[] { session, won ? 3 : 0, true });
                            await (UniTask)typeof(PuzzleStabilityVerification).GetMethod("Finish", BindingFlags.NonPublic | BindingFlags.Static)
                                .Invoke(null, new object[] { session, direct, null });
                            Check(session.ResultReady && session.Outcome.Kind == (won ? BoardOutcomeKind.Won : BoardOutcomeKind.MovesExhausted), "실제 판 처리 결과 " + won);
                            await CaptureState(session, size, false, won ? "won" : "lost", "Fixture", manifest);
                        }
                        finally { UnityEngine.Object.Destroy(level); }
                    }
                }
                Check(manifest.Count == 21, "20장 저장 및 픽셀 확인"); BaselineChecks();
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally
            {
                File.WriteAllLines(Output + "capture-manifest.csv", manifest);
                Time.timeScale = previous; FinishGameView(exit, "scene-results.txt");
            }
        }
        private static async UniTask<PuzzleGameSession> LoadOriginal(PuzzleEditorLevelSource mode)
        {
            LevelDefinition source = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
            string original = JsonUtility.ToJson(source);
            PuzzleEditorLaunchRequest request = PuzzleEditorLaunchRequest.Capture(source, mode, 12345);
            void Load(Scene scene, LoadSceneMode loadMode)
            {
                if (scene.path != PuzzleGameAssets.ScenePath) return;
                PuzzleGameSession loaded = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PuzzleGameSession>(true)).Single();
                loaded.InitializeAsync(request.CreateDefinition(), 12345, CancellationToken.None).Forget(Debug.LogException);
            }
            SceneManager.sceneLoaded += Load;
            try { await EditorSceneManager.LoadSceneAsyncInPlayMode(PuzzleGameAssets.ScenePath, new LoadSceneParameters(LoadSceneMode.Single)); }
            finally { SceneManager.sceneLoaded -= Load; }
            PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
            float deadline = Time.realtimeSinceStartup + 30;
            while (!session.IsReady && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.NextFrame();
            Check(session.IsReady && !session.HasFailed && JsonUtility.ToJson(source) == original, mode + " 원본 사본 실제 씬 준비·에셋 불변");
            Invoke(session, "TickProgress", .7f);
            Check(session.CanAcceptInput, mode + " 선택 맵 입력 준비"); return session;
        }
        private static async UniTask CaptureState(PuzzleGameSession session, Vector2Int size, bool inset, string state, string source, List<string> manifest)
        {
            PuzzleScreenLayout layout = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenLayout>();
            Rect safe = inset ? new Rect(24, 36, size.x - 48, size.y - 72) : new Rect(0, 0, size.x, size.y);
            layout.RefreshIfNeeded();
            bool layoutEnabled = layout.enabled; layout.enabled = false;
            try
            {
                layout.ApplyLayout(safe, size); Canvas.ForceUpdateCanvases();
                PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
                BoardCoordinate at = session.State.Cells.First(cell => cell.IsActive).Coordinate;
                Vector2 point = session.BoardCamera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(at)));
                PuzzleBoardInput input = UnityEngine.Object.FindFirstObjectByType<PuzzleBoardInput>();
                Check(input.TryGetCoordinate(point, out BoardCoordinate actual) && actual.Equals(at), "촬영 보드 입력 좌표 " + size + "/" + state);
                Check(safe.Contains(layout.BoardScreenRect.min) && safe.Contains(layout.BoardScreenRect.max - Vector2.one * .01f), "촬영 보드 안전 영역 " + size + "/" + state);
                string path = Output + "captures/" + size.x + "x" + size.y + "-" + state + ".png";
                if (File.Exists(path)) File.Delete(path);
                float settle = Time.realtimeSinceStartup + .2f;
                while (Time.realtimeSinceStartup < settle) await UniTask.NextFrame();
                Check((Rect)Field(layout, "lastSafe") == safe, "렌더 직전 실제 적용 안전 영역 유지 " + state);
                ScreenCapture.CaptureScreenshot(path);
                float deadline = Time.realtimeSinceStartup + 10;
                while ((!File.Exists(path) || new FileInfo(path).Length < 1000) && Time.realtimeSinceStartup < deadline) await UniTask.NextFrame();
                ValidatePng(path, size.x, size.y);
                manifest.Add(string.Join(",", path, size.x, size.y, safe.x, safe.y, safe.width, safe.height, state, source, session.State.LevelNumber, 12345, DateTime.UtcNow.ToString("O")));
                }
            finally { layout.enabled = layoutEnabled; }
        }
        private static void Click(string path)
        {
            Transform safe = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenView>().transform.Find("SafeArea");
            Button button = safe.Find(path).GetComponent<Button>();
            Check(button.isActiveAndEnabled && button.interactable, "실제 UI 버튼 사용 가능 " + path);
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        }
    }
}
