using System;
using System.IO;
using System.Linq;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GameScreen.Editor
{
    public static partial class PuzzleSwapAnimationVerification
    {
        [MenuItem("Tools/Match/게임 화면 스와이프 검증")]
        public static void RunScene()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존: 검사를 시작하지 않습니다.");
            Directory.CreateDirectory(Output);
            PuzzleUIRenderVerification.RememberSize();
            SessionState.SetBool(Key + ".Scene", true);
            PuzzleGameSceneVerification.OpenInteractive();
        }

        private static async UniTask VerifySceneAsync()
        {
            results.Clear();
            float scale = Time.timeScale;
            bool background = Application.runInBackground;
            InputSettings previous = InputSystem.settings;
            InputSettings settings = UnityEngine.Object.Instantiate(previous);
            Mouse mouse = null;
            int exit = 0;
            try
            {
                Application.runInBackground = true;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = settings;
                mouse = InputSystem.AddDevice<Mouse>();
                PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                for (int i = 0; i < 1800 && !session.CanAcceptInput && !session.HasFailed; i++) await UniTask.Yield();
                Check(session.CanAcceptInput, "실제 게임 씬 준비");
                PuzzleBoardInput input = session.GetComponent<PuzzleBoardInput>();
                PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
                PuzzleScreenView screen = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenView>();
                UnityEngine.UI.Text moves = screen.transform.Find("SafeArea/PuzzleHUD/Moves/Number").GetComponent<UnityEngine.UI.Text>();
                UnityEngine.UI.Button[] itemButtons = screen.GetComponentInChildren<PuzzleItemBarView>().GetComponentsInChildren<UnityEngine.UI.Button>();
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(450, 800) })
                {
                    await session.RestartAsync(CancellationToken.None);
                    Call(session, "TickProgress", .7f);
                    PuzzleUIRenderVerification.SetSize(size.x, size.y);
                    for (int i = 0; i < 15; i++) await UniTask.Yield();
                    Time.timeScale = 0;
                    ActionCandidate action = ActionQuery.Find(session.State).First(c => c.Kind == QueryActionKind.SwapMatch);
                    SpriteRenderer image = board.OccupantAt(action.First);
                    Vector3 origin = image.transform.position;
                    int before = session.State.MovesRemaining;
                    Vector2 from = session.BoardCamera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(action.First)));
                    Vector2 to = session.BoardCamera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(action.Second.Value)));
                    await Shot("scene-" + size.x + "-start");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = from, buttons = 1 }); InputSystem.Update(); Call(input, "Update");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.Lerp(from, to, .2f), buttons = 1 }); InputSystem.Update(); Call(input, "Update");
                    Check(image.transform.position != origin && session.State.MovesRemaining == before, "게임 화면 미리보기 " + size);
                    await Shot("scene-" + size.x + "-preview");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.Lerp(from, to, .3f), buttons = 1 }); InputSystem.Update(); Call(input, "Update");
                    Check(session.IsPresenting && session.State.MovesRemaining == before - 1, "게임 화면 놓기 전 교환 " + size);
                    Check(moves.text == (before - 1).ToString() && session.MovesPulse > 0, "유효 교환 실제 이동 소비 즉시 HUD 반영·강조 " + size);
                    Tick(session, .075f); await Shot("scene-" + size.x + "-mid");
                    session.SetPaused(true); Tick(session, 1);
                    Check(moves.text == (before - 1).ToString() && session.IsPresenting && session.MovesPulse > 0, "pause 중 이동 수·강조·교환 표시 유지 " + size);
                    session.SetPaused(false); Tick(session, .075f);
                    await FinishPresentation(session);
                    Check(!session.IsPresenting && moves.text == (before - 1).ToString(), "HUD 교환·제거 완료 후 갱신 " + size);
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = to }); InputSystem.Update(); Call(input, "Update");
                    // 결과 한 프레임을 캡처하는 동안 다음 연쇄만 정지한다.
                    session.enabled = false;
                    await Shot("scene-" + size.x + "-end");
                    session.enabled = true;
                    await session.RestartAsync(CancellationToken.None);
                    Call(session, "TickProgress", .7f);
                    ActionCandidate invalid = session.State.Cells.Where(c => c.IsActive)
                        .SelectMany(c => new[] { new BoardCoordinate(c.Coordinate.Row, c.Coordinate.Column + 1), new BoardCoordinate(c.Coordinate.Row + 1, c.Coordinate.Column) }
                            .Select(next => ActionQuery.Swap(session.State, c.Coordinate, next)))
                        .First(c => c.Reason == ActionReason.NoNewMatch && session.State.CellAt(c.First).Color != session.State.CellAt(c.Second.Value).Color);
                    string invalidBefore = Snapshot(session.State);
                    from = session.BoardCamera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(invalid.First)));
                    to = session.BoardCamera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(invalid.Second.Value)));
                    await Shot("scene-" + size.x + "-invalid-start");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = from, buttons = 1 }); InputSystem.Update(); Call(input, "Update");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = to, buttons = 1 }); InputSystem.Update(); Call(input, "Update");
                    Check(session.IsPresenting && Snapshot(session.State) == invalidBefore, "게임 화면 무효 교환 상태 보존 " + size);
                    Check(itemButtons.Length == 3 && itemButtons.All(button => !button.interactable), "무효 교환 중 실제 아이템 버튼 잠금 " + size);
                    Tick(session, .15f); await Shot("scene-" + size.x + "-invalid-far");
                    Tick(session, .075f); await Shot("scene-" + size.x + "-invalid-return");
                    Tick(session, .075f);
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = to }); InputSystem.Update(); Call(input, "Update");
                    Check(session.CanAcceptInput && Snapshot(session.State) == invalidBefore, "게임 화면 실패 복귀/입력 복원 " + size);
                    Check(itemButtons.All(button => button.interactable), "무효 복귀 후 실제 아이템 버튼 복원 " + size);
                    await Shot("scene-" + size.x + "-invalid-end");
                    Time.timeScale = scale;
                }
                Time.timeScale = scale;
                await session.RestartAsync(CancellationToken.None);
                    Call(session, "TickProgress", .7f);
                Check(session.CanAcceptInput, "화면 검사 후 재시작 잠금 없음");
                ActionCandidate leaving = ActionQuery.Find(session.State).First(c => c.Second.HasValue);
                session.TrySwap(leaving.First, leaving.Second.Value); Tick(session, .075f);
                PuzzleGameSession oldSession = session;
                await EditorSceneManager.LoadSceneAsyncInPlayMode(PuzzleGameAssets.ScenePath,
                    new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
                await UniTask.Yield();
                session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                for (int i = 0; i < 1800 && !session.CanAcceptInput && !session.HasFailed; i++) await UniTask.Yield();
                Check(oldSession == null && UnityEngine.Object.FindObjectsByType<PuzzleGameSession>(FindObjectsSortMode.None).Length == 1, "교환 중 실제 씬 재진입 단일 세션");
                Check(session.CanAcceptInput && !session.IsPresenting, "씬 재진입 이전 연출/잠금 없음");
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally
            {
                Time.timeScale = scale; Application.runInBackground = background;
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                InputSystem.settings = previous; UnityEngine.Object.Destroy(settings);
                PuzzleUIRenderVerification.RestoreSize();
                File.WriteAllLines(Output + "scene-results.txt", results);
                if (Application.isBatchMode) EditorApplication.Exit(exit); else EditorApplication.ExitPlaymode();
            }
        }

        private static async UniTask Shot(string name)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            ScreenCapture.CaptureScreenshot(Output + name + ".png");
            await UniTask.Delay(150, ignoreTimeScale: true);
        }
    }
}
