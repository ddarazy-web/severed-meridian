using System;
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

namespace GameScreen.Editor
{
    public static partial class PuzzlePowerAnimationVerification
    {
        public static void RunScene()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            Directory.CreateDirectory(Output); SessionState.SetBool(PlayKey + ".Scene", true);
            EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath); EditorApplication.EnterPlaymode();
        }

        private static async UniTask PowerSceneAsync()
        {
            results.Clear(); int exit = 0; float previousTime = Time.timeScale;
            LevelDefinition level = null;
            try
            {
                PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                float deadline = Time.realtimeSinceStartup + 30;
                while (!session.IsReady && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                Check(session.CanAcceptInput, "실제 게임 씬 MemoryPack 시작");
                string original = SceneSnapshot(session.State);
                PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
                PuzzleScreenView screen = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenView>();
                PuzzleResultView resultPanel = (PuzzleResultView)typeof(PuzzleScreenView).GetField("result", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(screen);
                Time.timeScale = 0;
                BoardCoordinate origin = new BoardCoordinate(4, 4), second = new BoardCoordinate(4, 5);
                for (int fixture = 0; fixture < 14; fixture++)
                {
                    bool combination = fixture >= 4;
                    level = combination ? (LevelDefinition)typeof(CombinationVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null, new object[] { fixture - 4, RocketDirection.Horizontal, null }) :
                        (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                    if (!combination)
                        typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                            new object[] { level, origin, new[] { InitialBlockKind.Rocket, InitialBlockKind.Bomb, InitialBlockKind.Drone, InitialBlockKind.Magnet }[fixture], RocketDirection.Horizontal, RabbitColor.Type1 });
                    if (fixture == 0)
                    {
                        BoardCoordinate obstacle = new BoardCoordinate(4, 6);
                        LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { obstacle });
                        LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = 3 }, new[] { obstacle });
                    }
                    LevelRuntimeState initial = LevelStateBuilder.Build(level, 12345).State;
                    BoardActionExecutor direct = new BoardActionExecutor(initial), executor = new BoardActionExecutor(initial);
                    SceneCall(session, "ResetPresentation"); SceneSet(session, "executor", executor);
                    PuzzleArtwork art = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                    await art.PrepareAsync(initial, CancellationToken.None); board.Draw(initial, art);
                    session.enabled = true; SceneCall(screen, "Refresh");
                    int width = fixture % 2 == 0 ? 1280 : 450, height = fixture % 2 == 0 ? 720 : 800;
                    PuzzleUIRenderVerification.SetSize(width, height);
                    for (int frame = 0; frame < 5; frame++) await UniTask.Yield();
                    bool applied = combination ? session.TrySwap(origin, second) : session.TryActivate(origin);
                    BoardActionResult action = combination ? direct.Swap(origin, second) : direct.Activate(origin);
                    Check(applied && action.IsApplied, "실제 씬 파워/조합 발동 " + fixture);
                    Check(!session.CanAcceptInput && !session.CanUseItems, "실제 씬 효과 준비 입력 잠금 " + fixture);
                    session.enabled = false;
                    if (combination) SceneCall(session, "AdvancePresentation", .15f);
                    await AwaitSceneEffects(session);
                    SceneCall(session, "AdvancePresentation", .14f);
                    Check(session.IsPresenting && !resultPanel.gameObject.activeSelf, "실제 씬 공격 중 결과 패널 차단 " + fixture);
                    await PowerShot("scene-power-" + fixture + "-mid");
                    string pausedState = SceneSnapshot(session.State);
                    Vector3[] positions = board.GetComponentsInChildren<SpriteRenderer>().Select(image => image.transform.localPosition).ToArray();
                    Check(session.SetPaused(true), "실제 씬 파워 일시정지 " + fixture);
                    SceneCall(session, "AdvancePresentation", 1f);
                    PuzzleUIRenderVerification.SetSize(height, width);
                    for (int frame = 0; frame < 5; frame++) await UniTask.Yield();
                    Check(SceneSnapshot(session.State) == pausedState && board.GetComponentsInChildren<SpriteRenderer>()
                        .Select(image => image.transform.localPosition).SequenceEqual(positions), "파워 정지·회전 중 상태와 상대 위치 유지 " + fixture);
                    session.SetPaused(false);
                    object playback = typeof(PuzzleGameSession).GetField("powerPlayback", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                    PuzzleEffectTimeline schedule = (PuzzleEffectTimeline)playback.GetType().GetField("timeline", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(playback);
                    var flight = schedule.Attacks.FirstOrDefault(attack => attack.Record.IsFlight);
                    float captureTime = fixture == 0 ? .20f : flight == null ? .38f : flight.Start + Mathf.Clamp(
                        Vector3.Distance(PuzzleWorldBoard.CellPosition(flight.Record.Origin), PuzzleWorldBoard.CellPosition(flight.Record.Center)) * .045f, .18f, .38f) + .01f;
                    SceneCall(session, "AdvancePresentation", captureTime - .14f);
                    await PowerShot("scene-power-" + fixture + "-contact");
                    for (int frame = 0; frame < 20000 && (session.IsPresenting || executor.HasPendingCascade); frame++)
                    {
                        await AwaitSceneEffects(session);
                        if (session.IsPresenting)
                        {
                            Check(!resultPanel.gameObject.activeSelf, "효과 종료 전 결과 숨김 " + fixture + "/" + frame);
                            SceneCall(session, "AdvancePresentation", fixture % 2 == 0 ? .017f : .04f);
                        }
                        else SceneCall(session, "Advance");
                    }
                    for (int step = 0; step < 2000 && direct.HasPendingCascade; step++) direct.AdvanceCascade();
                    Check(!session.HasFailed && !session.IsPresenting && !executor.HasPendingCascade && !direct.HasPendingCascade,
                        "실제 씬 파워·연쇄 종료 " + fixture);
                    Check(SceneSnapshot(session.State) == SceneSnapshot(direct.State), "보드·난수·이동·공급·미션·회수 동등 " + fixture);
                    Check(session.Phase == direct.Phase && SceneSnapshot(session.Outcome) == SceneSnapshot(direct.Outcome), "Phase·승패 동등 " + fixture);
                    Check(board.GetComponentsInChildren<SpriteRenderer>().All(image => image.name != "Effect-playback"), "실제 씬 효과 잔상 없음 " + fixture);
                    UnityEngine.Object.Destroy(level); level = null;
                }
                session.enabled = true;
                await session.RestartAsync(CancellationToken.None);
                Check(SceneSnapshot(session.State) == original && session.CanAcceptInput, "파워 fixture 이후 원래 MemoryPack 다시하기");
                int warmObjects = board.GetComponentsInChildren<Transform>(true).Length;
                for (int repeat = 0; repeat < 5; repeat++)
                {
                    ActionCandidate action = ActionQuery.Find(session.State).First(candidate => candidate.Kind == QueryActionKind.SwapMatch);
                    Check(session.TrySwap(action.First, action.Second.Value), "효과 취소 다시하기 시작 " + repeat);
                    SceneCall(session, "AdvancePresentation", .15f);
                    // 일부 회차는 로드 완료 전에, 나머지는 효과 중간에 취소한다.
                    if (repeat % 2 != 0) { await AwaitSceneEffects(session); SceneCall(session, "AdvancePresentation", .06f); }
                    await session.RestartAsync(CancellationToken.None);
                    for (int frame = 0; frame < 5; frame++) await UniTask.Yield();
                    Check(SceneSnapshot(session.State) == original && session.CanAcceptInput && !session.IsPresenting && !session.HasFailed,
                        "효과 로드/재생 취소 후 상태·잠금 복원 " + repeat);
                    Check(board.GetComponentsInChildren<Transform>(true).Length == warmObjects &&
                        board.GetComponentsInChildren<SpriteRenderer>().All(image => image.name != "Effect-playback"), "재시작 효과 객체 누적·잔상 없음 " + repeat);
                }
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally
            {
                if (level != null) UnityEngine.Object.Destroy(level);
                Time.timeScale = previousTime; PuzzleUIRenderVerification.RestoreSize();
                File.WriteAllLines(Output + "scene-results.txt", results); EditorApplication.Exit(exit);
            }
        }

        private static async UniTask AwaitSceneEffects(PuzzleGameSession session)
        {
            FieldInfo field = typeof(PuzzleGameSession).GetField("preparingEffects", BindingFlags.NonPublic | BindingFlags.Instance);
            float deadline = Time.realtimeSinceStartup + 20;
            while ((bool)field.GetValue(session) && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
            if ((bool)field.GetValue(session) || session.HasFailed) throw new InvalidOperationException("효과 준비 미완료: " + session.Message);
        }

        private static async UniTask PowerShot(string name)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            ScreenCapture.CaptureScreenshot(Output + name + ".png");
            await UniTask.Delay(150, ignoreTimeScale: true);
        }
        private static void SceneCall(object target, string name, params object[] args)
            => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
        private static void SceneSet(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static string SceneSnapshot(object state)
            => (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { state });
    }
}
