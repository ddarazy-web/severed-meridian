using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzleSettlementAnimationVerification
    {
        public static void RunScene()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Key + ".Scene", true);
            EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath);
            EditorApplication.EnterPlaymode();
        }

        private static async UniTask VerifySceneAsync()
        {
            results.Clear();
            int exit = 0;
            float originalTime = Time.timeScale;
            try
            {
                PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                for (int frame = 0; frame < 1800 && !session.IsReady && !session.HasFailed; frame++) await UniTask.Yield();
                Check(session.CanAcceptInput, "실제 씬 MemoryPack 준비");
                PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
                PuzzleScreenView screen = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenView>();
                UnityEngine.UI.Text moves = screen.transform.Find("SafeArea/PuzzleHUD/Moves/Number").GetComponent<UnityEngine.UI.Text>();
                foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(450, 800) })
                {
                    await session.RestartAsync(CancellationToken.None);
                    PuzzleUIRenderVerification.SetSize(size.x, size.y);
                    for (int frame = 0; frame < 15; frame++) await UniTask.Yield();
                    Time.timeScale = 0;
                    ActionCandidate action = ActionQuery.Find(session.State).First(candidate => candidate.Kind == QueryActionKind.SwapMatch);
                    int movesBefore = session.State.MovesRemaining;
                    BoardActionExecutor direct = new BoardActionExecutor(session.State); direct.Swap(action.First, action.Second.Value);
                    session.enabled = false; await Shot("scene-" + size.x + "-start");
                    session.enabled = true;
                    Check(session.TrySwap(action.First, action.Second.Value), "실제 씬 교환 " + size);
                    session.enabled = false;
                    Tick(session, .075f); await Shot("scene-" + size.x + "-swap-mid");
                    Tick(session, .075f); Tick(session, .06f);
                    Check(session.IsPresenting, "실제 씬 제거 재생 " + size);
                    Check(moves.text == movesBefore.ToString(), "제거 완료 전 HUD 조기 갱신 없음 " + size);
                    await Shot("scene-" + size.x + "-remove-mid");
                    Tick(session, .06f);
                    Check(moves.text == (movesBefore - 1).ToString(), "제거 완료 후 HUD 반영 " + size);
                    Call(session, "Advance");
                    Check(session.IsPresenting, "실제 씬 정착 재생 " + size);
                    await Shot("scene-" + size.x + "-fall-start");
                    Tick(session, .06f); await Shot("scene-" + size.x + "-fall-mid");
                    string rotatingState = Snapshot(session.State);
                    Vector3[] rotatingPositions = board.GetComponentsInChildren<SpriteRenderer>().Select(image => board.transform.InverseTransformPoint(image.transform.position)).ToArray();
                    PuzzleUIRenderVerification.SetSize(size.y, size.x);
                    for (int frame = 0; frame < 8; frame++) await UniTask.Yield();
                    Check(session.IsPresenting && Snapshot(session.State) == rotatingState && board.GetComponentsInChildren<SpriteRenderer>()
                        .Select(image => board.transform.InverseTransformPoint(image.transform.position)).SequenceEqual(rotatingPositions), "낙하 도중 화면 회전 상태·위치 유지 " + size);
                    await Shot("scene-" + size.x + "-rotated-mid");
                    PuzzleUIRenderVerification.SetSize(size.x, size.y);
                    for (int frame = 0; frame < 8; frame++) await UniTask.Yield();
                    bool supplied = false, landed = false;
                    for (int frame = 0; frame < 5000 && session.IsPresenting; frame++)
                    {
                        if (!supplied && board.GetComponentsInChildren<SpriteRenderer>().Any(image => image.name == "Supply-playback" && image.enabled))
                        {
                            await Shot("scene-" + size.x + "-supply-start");
                            Tick(session, .08f); await Shot("scene-" + size.x + "-supply-mid"); supplied = true;
                        }
                        object playback = typeof(PuzzleGameSession).GetField("settlementPlayback", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                        float elapsed = (float)playback.GetType().GetField("elapsed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(playback);
                        float duration = (float)playback.GetType().GetField("duration", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(playback);
                        if (!landed && elapsed >= duration)
                        { Tick(session, .03f); await Shot("scene-" + size.x + "-landing-mid"); landed = true; }
                        Tick(session, .02f);
                    }
                    Check(!session.IsPresenting && supplied && landed && !session.HasFailed, "실제 씬 공급·착지 완료 " + size);
                    await Shot("scene-" + size.x + "-settled");
                    BoardActionExecutor executor = (BoardActionExecutor)typeof(PuzzleGameSession).GetField("executor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                    for (int frame = 0; frame < 20000 && (session.IsPresenting || executor.HasPendingCascade); frame++)
                    { if (session.IsPresenting) Tick(session, .02f); else Call(session, "Advance"); }
                    for (int step = 0; step < 1000 && direct.HasPendingCascade; step++) direct.AdvanceCascade();
                    Check(!session.IsPresenting && !executor.HasPendingCascade && Snapshot(session.State) == Snapshot(direct.State), "실제 씬 전체 연쇄 상태 동일 " + size);
                    Check(executor.Phase == direct.Phase && Snapshot(executor.Outcome) == Snapshot(direct.Outcome), "실제 씬 Phase·승패 결과 동일 " + size);
                    await Shot("scene-" + size.x + "-cascade-end");
                    session.enabled = true; Time.timeScale = originalTime;
                }
                await VerifyAutomaticCascadeAsync(session, board);
                await VerifyOutcomesAsync(session, board, screen);
                await session.RestartAsync(CancellationToken.None);
                string initial = Snapshot(session.State);
                int objects = board.GetComponentsInChildren<Transform>(true).Length;
                for (int repeat = 0; repeat < 5; repeat++)
                {
                    ActionCandidate action = ActionQuery.Find(session.State).First(candidate => candidate.Kind == QueryActionKind.SwapMatch);
                    Check(session.TrySwap(action.First, action.Second.Value), "낙하 재시작 fixture " + repeat);
                    Tick(session, .15f); Tick(session, .12f); Call(session, "Advance"); Tick(session, .04f);
                    Check(session.IsPresenting && !session.CanUseItems, "낙하 중 다시하기 시작 " + repeat);
                    await session.RestartAsync(CancellationToken.None);
                    Check(session.CanAcceptInput && !session.IsPresenting && Snapshot(session.State) == initial, "낙하 다시하기 상태·잠금 복원 " + repeat);
                    Check(board.GetComponentsInChildren<Transform>(true).Length == objects, "낙하 다시하기 객체 누적 없음 " + repeat);
                    Check(!board.GetComponentsInChildren<SpriteRenderer>().Any(image => image.name == "Supply-playback") &&
                        board.GetComponentsInChildren<SpriteRenderer>().All(image => Mathf.Approximately(image.color.a, 1)), "낙하 다시하기 임시 표시·알파 복원 " + repeat);
                }
                ActionCandidate leaving = ActionQuery.Find(session.State).First(candidate => candidate.Kind == QueryActionKind.SwapMatch);
                session.TrySwap(leaving.First, leaving.Second.Value); Tick(session, .15f); Tick(session, .12f); Call(session, "Advance");
                Check(session.IsPresenting, "낙하 중 실제 씬 종료 fixture");
                PuzzleGameSession oldSession = session;
                await EditorSceneManager.LoadSceneAsyncInPlayMode(PuzzleGameAssets.ScenePath,
                    new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
                await UniTask.Yield();
                session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                for (int frame = 0; frame < 1800 && !session.IsReady && !session.HasFailed; frame++) await UniTask.Yield();
                Check(oldSession == null && UnityEngine.Object.FindObjectsByType<PuzzleGameSession>(FindObjectsSortMode.None).Length == 1, "낙하 중 씬 재진입 단일 세션");
                Check(session.CanAcceptInput && !session.IsPresenting, "씬 재진입 이전 표시 잠금 없음");
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally
            {
                Time.timeScale = originalTime; PuzzleUIRenderVerification.RestoreSize();
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
