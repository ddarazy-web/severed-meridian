using System;
using System.Linq;
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
    public static partial class PuzzleProgressFeedbackVerification
    {
        private static async UniTask RecoveryAndItemChecks()
        {
            PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
            PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
            PuzzleScreenView screen = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenView>();
            PuzzleArtwork art = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            foreach (int fixture in Enumerable.Range(0, 4))
            {
                bool recovery = fixture == 0;
                LevelDefinition level = (LevelDefinition)(recovery ? typeof(RecoveryVerification).GetMethod("PlayFixture", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null)
                    : typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null));
                try
                {
                    if (!recovery) JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
                    LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                    Check(built.IsBuilt, "회수·아이템 실제 레벨 유효 " + fixture);
                    await art.PrepareAsync(built.State, CancellationToken.None);
                    Invoke(session, "ResetPresentation");
                    typeof(PuzzleGameSession).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, new BoardActionExecutor(built.State));
                    Invoke(session, "InitializeProgress"); board.Draw(session.State, art); Invoke(screen, "Refresh");
                    BoardActionExecutor direct = new BoardActionExecutor(built.State);
                    session.enabled = true;
                    if (recovery)
                    {
                        Check(direct.Activate(new BoardCoordinate(8, 0)).IsApplied && session.TryActivate(new BoardCoordinate(8, 0)), "실제 회수 낙하·공급 시작");
                    }
                    else
                    {
                        BoardItem item = fixture == 1 ? BoardItem.Hammer : fixture == 2 ? BoardItem.Swap : BoardItem.Shuffle;
                        BoardCoordinate? first = item == BoardItem.Shuffle ? null : new BoardCoordinate(4, 4);
                        BoardCoordinate? second = item == BoardItem.Swap ? new BoardCoordinate(4, 5) : null;
                        int moves = session.State.MovesRemaining;
                        Check(direct.UseItem(item, first, second).IsApplied && session.TryUseItem(item, first, second), "실제 아이템 적용 " + item);
                        Check(session.State.MovesRemaining == moves && session.MovesPulse == 0, "아이템은 이동 수 가짜 소비·강조 없음 " + item);
                    }
                    session.enabled = false;
                    bool sawRecoveryFlight = false, checkedBefore = false;
                    for (int frame = 0; frame < 20000; frame++)
                    {
                        await ProgressFrame(session, .01f);
                        if (recovery && session.State.Recoveries.Count > 0)
                        {
                            object playback = typeof(PuzzleGameSession).GetField("settlementPlayback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                            float elapsed = (float)playback.GetType().GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playback);
                            float? at = (float?)playback.GetType().GetMethod("CollectionTime", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { new BoardCoordinate(8, 0) });
                            if (!checkedBefore && at.HasValue && elapsed < at.Value - .01f)
                            {
                                checkedBefore = true;
                                Check(session.DisplayedMissionProgress(0) == 0 && session.ProgressFeedback.Flights.Count == 0,
                                    "실제 회수 이동 종료 이전에는 비행·HUD 진행 없음");
                            }
                            if (session.ProgressFeedback.Flights.Count > 0)
                            {
                                sawRecoveryFlight = true;
                                Check(!at.HasValue || elapsed >= at.Value, "실제 회수 도착 시간 이후에만 미션 비행");
                            }
                        }
                        BoardActionExecutor executor = (BoardActionExecutor)typeof(PuzzleGameSession).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                        if (!session.IsPresenting && !executor.HasPendingCascade && !session.HasProgressFeedback) break;
                        if (frame == 19999) throw new InvalidOperationException("회수·아이템 종료 timeout " + fixture);
                    }
                    while (direct.HasPendingCascade) direct.AdvanceCascade();
                    Check(!session.HasFailed && StateSnapshot(session.State) == StateSnapshot(direct.State) && session.Phase == direct.Phase && StateSnapshot(session.Outcome) == StateSnapshot(direct.Outcome),
                        "회수·아이템 최종 보드·미션·공급·난수·승패 동등 " + fixture);
                    Check(session.State.Missions.Select((mission, index) => session.DisplayedMissionProgress(index) == mission.Progress).All(value => value),
                        "회수·아이템 실제 진행과 최종 HUD 표시 일치 " + fixture);
                    if (recovery) Check(checkedBefore && sawRecoveryFlight && session.ProgressFeedback.CompletionCount(0) == 1 && session.ResultReady,
                        "실제 이동·공급 회수 2개·완료 강조 한 번·최종 패널 경계");
                }
                finally { UnityEngine.Object.Destroy(level); }
            }
            session.enabled = true;
        }
    }
}
