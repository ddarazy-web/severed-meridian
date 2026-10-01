using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzleSettlementAnimationVerification
    {
        private static async UniTask VerifyOutcomesAsync(PuzzleGameSession session, PuzzleWorldBoard board, PuzzleScreenView screen)
        {
            PuzzleResultView panel = (PuzzleResultView)typeof(PuzzleScreenView).GetField("result", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(screen);
            PuzzleArtwork art = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            foreach (bool win in new[] { true, false })
            {
                LevelDefinition level = (LevelDefinition)(win ? typeof(RecoveryVerification).GetMethod("PlayFixture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null)
                    : typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null));
                try
                {
                    JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", level);
                    if (!win)
                    {
                        typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                            new object[] { level, new BoardCoordinate(8, 0), InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1 });
                        JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
                    }
                    LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                    Check(built.IsBuilt, "승패 fixture " + win);
                    await art.PrepareAsync(built.State, CancellationToken.None);
                    BoardActionExecutor direct = new BoardActionExecutor(built.State);
                    Check(direct.Activate(new BoardCoordinate(8, 0)).IsApplied, "승패 직접 실행기 행동 " + win);
                    for (int step = 0; step < 2000 && direct.HasPendingCascade; step++) direct.AdvanceCascade();
                    Check(direct.Outcome?.Kind == (win ? BoardOutcomeKind.Won : BoardOutcomeKind.MovesExhausted), "승패 기대 결과 " + win);
                    foreach (float speed in new[] { .5f, 2f })
                    {
                        Call(session, "ResetPresentation"); Set(session, "executor", new BoardActionExecutor(built.State));
                        Set(session, "removalSeconds", .12f * speed); Set(session, "fallSeconds", .12f * speed);
                        Set(session, "supplySeconds", .16f * speed); Set(session, "landingSeconds", .06f * speed);
                        board.Draw(session.State, art); session.enabled = true; Call(screen, "Refresh");
                        Check(session.TryActivate(new BoardCoordinate(8, 0)), "승패 애니메이션 행동 " + win + "/" + speed);
                        session.enabled = false;
                        bool paused = false, outcomeDuringPlayback = false;
                        int shownFrames = 0;
                        for (int frame = 0; frame < 20000; frame++)
                        {
                            await WaitForEffectResourcesAsync(session);
                            BoardActionExecutor executor = (BoardActionExecutor)typeof(PuzzleGameSession).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                            if (!session.IsPresenting && !executor.HasPendingCascade) break;
                            if (session.IsPresenting)
                            {
                                shownFrames++;
                                if (panel.gameObject.activeSelf) throw new System.Exception("승패 패널 조기 노출 " + win);
                                outcomeDuringPlayback |= session.Outcome != null;
                                if (!paused)
                                {
                                    string state = Snapshot(session.State);
                                    Check(session.SetPaused(true), "승패 재생 pause " + win + "/" + speed);
                                    Tick(session, 1);
                                    Check(session.IsPresenting && Snapshot(session.State) == state && !panel.gameObject.activeSelf, "pause 상태·결과 패널 유지");
                                    session.SetPaused(false); paused = true;
                                }
                                Tick(session, .017f);
                            }
                            else Call(session, "Advance");
                        }
                        Check(shownFrames > 0 && (!win || outcomeDuringPlayback), "승패 재생 중 패널 숨김·라스트팡 경계 " + win + "/" + speed);
                        Check(!session.IsPresenting && session.Phase == direct.Phase && Snapshot(session.Outcome) == Snapshot(direct.Outcome) &&
                            Snapshot(session.State) == Snapshot(direct.State), "속도·pause 변경 후 상태·난수·Phase·승패 동일 " + win + "/" + speed);
                        Check(panel.gameObject.activeSelf && !session.CanAcceptInput, "연출 완료 뒤 승패 패널 노출 " + win + "/" + speed);
                        await Shot("outcome-" + (win ? "won" : "lost") + "-" + speed);
                    }
                }
                finally { Object.Destroy(level); }
            }
            Set(session, "removalSeconds", .12f); Set(session, "fallSeconds", .12f);
            Set(session, "supplySeconds", .16f); Set(session, "landingSeconds", .06f);
            session.enabled = true;
        }
    }
}
