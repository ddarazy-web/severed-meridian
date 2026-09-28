using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class RoundEndVerification
    {
        public static void Edges()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { EdgeChecks(); File.WriteAllLines(Evidence + "/edge-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/edge-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static void EdgeChecks()
        {
            LevelDefinition arrival = (LevelDefinition)Invoke(typeof(RecoveryVerification), "Make", 1);
            Invoke(typeof(RecoveryVerification), "Place", arrival, C(9, 0));
            LevelRuntimeState rawArrival = Build(arrival); BoardActionExecutor initialized = new BoardActionExecutor(rawArrival);
            Finish(initialized, true);
            Check(initialized.Outcome?.Kind == BoardOutcomeKind.Won && initialized.Outcome.Turn == 0 && initialized.Outcome.MovesRemaining == 20 &&
                initialized.State.Recoveries.Count == 1 && rawArrival.Recoveries.Count == 0, "초기 도착 회수·정착 후 무소모 성공·원시 사본 보존");
            UnityEngine.Object.DestroyImmediate(arrival);

            LevelDefinition deadlock = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make",
                new Dictionary<BoardCoordinate, int> { [C(9, 0)] = 0, [C(9, 1)] = 0, [C(9, 2)] = 1, [C(9, 3)] = 1, [C(9, 4)] = 0 }, 20);
            BoardActionExecutor repaired = new BoardActionExecutor(Build(deadlock));
            Check(MatchQuery.Find(repaired.State).Count == 0 && ActionQuery.Find(repaired.State).Count == 0, "자동 재배치 진입용 무매칭·무행동 보드");
            Set(repaired, "TurnEffects", Activator.CreateInstance(typeof(TurnEffectContext), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { 0, Array.Empty<MatchedBlockChange>() }, null));
            Set(repaired, "Phase", BoardActionPhase.WaitingForAutomaticMatch);
            Check(repaired.AdvanceCascade().Reason == CascadeStepReason.Shuffled && repaired.Phase == BoardActionPhase.Ready && repaired.Outcome == null &&
                repaired.State.MovesRemaining == 20 && repaired.Turn == 0 && ActionQuery.Find(repaired.State).Count > 0, "안정 경계 자동 재배치 성공·입력 재개·턴과 이동 무소모");
            UnityEngine.Object.DestroyImmediate(deadlock);

            foreach (int count in new[] { 2, 9 })
            {
                Dictionary<BoardCoordinate, int> colors = Enumerable.Range(0, count).ToDictionary(i => C(i / 5 * 2, i % 5 * 2), i => i % 5);
                LevelDefinition level = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", colors, 20);
                LevelRuntimeState state = Build(level); string before = Snapshot(state);
                ShuffleResult result = ShuffleResolution.Resolve(state);
                Check(result.Reason == (count == 2 ? ShuffleReason.Impossible : ShuffleReason.LimitReached), "고립 배치 완전 탐색과 제한 탐색 구분 " + count);
                Check(result.State == null && Snapshot(state) == before && result.RandomAfter > result.RandomBefore, "실패 후보 미커밋·탐색 난수 별도 기록 " + count);
                BoardActionExecutor executor = new BoardActionExecutor(state);
                Set(executor, "TurnEffects", Activator.CreateInstance(typeof(TurnEffectContext), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { 0, Array.Empty<MatchedBlockChange>() }, null));
                Set(executor, "Phase", BoardActionPhase.WaitingForAutomaticMatch);
                executor.AdvanceCascade();
                Check(executor.Outcome?.Kind == (count == 2 ? BoardOutcomeKind.Blocked : BoardOutcomeKind.Aborted), "안정 경계 종료 이유 구분 " + count);
                string stopped = Snapshot(executor.State), history = Snapshot(executor.CascadeHistory);
                Check(!executor.Swap(C(0, 0), C(0, 2)).IsApplied && !executor.Activate(C(0, 0)).IsApplied &&
                    Snapshot(executor.State) == stopped && Snapshot(executor.CascadeHistory) == history, "종료 후 교환·발동 원자적 거절 " + count);
                UnityEngine.Object.DestroyImmediate(level);
            }

            LevelDefinition fixture = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
            LevelRuntimeState source = Build(fixture);
            Set(source.CellAt(C(0, 0)), "Cover", (CoverKind?)CoverKind.Web); Set(source.CellAt(C(0, 0)), "CoverDurability", 2);
            Set(source.CellAt(C(0, 1)), "Cover", (CoverKind?)CoverKind.Mold); Set(source.CellAt(C(0, 1)), "CoverDurability", 1);
            Set(source.CellAt(C(0, 2)), "Content", RuntimeContent.Recovery); Set(source.CellAt(C(0, 2)), "Color", null);
            Set(source.CellAt(C(0, 3)), "Content", RuntimeContent.Rocket); Set(source.CellAt(C(0, 3)), "Color", null);
            Set(source.CellAt(C(0, 3)), "RocketDirection", (RocketDirection?)RocketDirection.Horizontal);
            Set(source.CellAt(C(0, 4)), "DustDurability", 3);
            string[] excluded = source.Cells.Take(3).Select(Snapshot).ToArray();
            string ContentKey(RuntimeCell cell) => cell.Content + ":" + cell.Color + ":" + cell.RocketDirection;
            ShuffleResult shuffled = ShuffleResolution.Resolve(source);
            Check(shuffled.Reason == ShuffleReason.Applied, "혼합 파워·덮개·회수 보드 재배치");
            Check(excluded.SequenceEqual(shuffled.State.Cells.Take(3).Select(Snapshot)), "거미줄·곰팡이 내용물과 회수 부품 보존");
            Check(source.Cells.Select(ContentKey).OrderBy(s => s).SequenceEqual(shuffled.State.Cells.Select(ContentKey).OrderBy(s => s)), "색·파워 수량·로켓 방향 보존");
            Check(source.Cells.Select(c => c.DustDurability).SequenceEqual(shuffled.State.Cells.Select(c => c.DustDurability)) &&
                Snapshot(source.Flow) == Snapshot(shuffled.State.Flow) && Snapshot(source.Supply) == Snapshot(shuffled.State.Supply) &&
                Snapshot(source.Missions) == Snapshot(shuffled.State.Missions) && source.MovesRemaining == shuffled.State.MovesRemaining, "바닥·흐름·공급·미션·이동 보존");

            foreach (BoardCoordinate[] pattern in new[]
            {
                new[] { C(0, 0), C(0, 1), C(0, 2) },
                new[] { C(0, 0), C(0, 1), C(0, 2), C(0, 3) },
                new[] { C(0, 0), C(0, 1), C(0, 2), C(0, 3), C(0, 4) },
                new[] { C(0, 0), C(0, 1), C(0, 2), C(1, 2), C(2, 2) },
                new[] { C(0, 0), C(0, 1), C(0, 2), C(1, 1), C(2, 1) },
                new[] { C(0, 0), C(0, 1), C(1, 0), C(1, 1) }
            })
            {
                LevelDefinition matched = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", pattern.ToDictionary(c => c, c => 0), 20);
                LevelRuntimeState matching = Build(matched);
                Check(MatchQuery.Find(matching).Count > 0 && ShuffleResolution.Resolve(matching).Reason == ShuffleReason.Impossible,
                    "완성 매칭 배치는 재배치 성공으로 인정하지 않음 " + string.Join(",", pattern));
                UnityEngine.Object.DestroyImmediate(matched);
            }
            LevelDefinition divided = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
            LevelFlowEditing.SetWalls(divided, Enumerable.Range(0, 10).Select(row => new BoardEdge(C(row, 4), C(row, 5))).ToArray(), false);
            Invoke(typeof(PowerEffectVerification), "Place", divided, C(0, 0), InitialBlockKind.Magnet, RocketDirection.Horizontal, RabbitColor.Type1);
            bool crossed = false;
            for (int seed = 1; seed <= 16 && !crossed; seed++)
            {
                LevelRuntimeState split = LevelStateBuilder.Build(divided, seed).State;
                ShuffleResult mixed = ShuffleResolution.Resolve(split);
                Check(mixed.Reason == ShuffleReason.Applied && Snapshot(split.Flow) == Snapshot(mixed.State.Flow), "벽 보존 재배치 " + seed);
                crossed = mixed.State.Cells.Single(c => c.Content == RuntimeContent.Magnet).Coordinate.Column >= 5;
            }
            Check(crossed, "벽으로 분리된 구역을 가로질러 파워 재배치 가능");
            UnityEngine.Object.DestroyImmediate(divided);

            foreach (int remaining in new[] { 0, 1, 200 })
            {
                LevelRuntimeState win = Build(fixture); Set(win, "MovesRemaining", remaining);
                foreach (RuntimeMission mission in win.Missions) Set(mission, "Progress", mission.Target);
                Set(win.CellAt(C(0, 0)), "Cover", (CoverKind?)CoverKind.Web); Set(win.CellAt(C(0, 0)), "CoverDurability", 2);
                Set(win.CellAt(C(0, 1)), "Cover", (CoverKind?)CoverKind.Mold); Set(win.CellAt(C(0, 1)), "CoverDurability", 1);
                int candidates = win.Cells.Count(c => c.Content == RuntimeContent.Normal && !c.Cover.HasValue);
                string web = Snapshot(win.CellAt(C(0, 0))), mold = Snapshot(win.CellAt(C(0, 1)));
                BoardActionExecutor executor = new BoardActionExecutor(win); Finish(executor, true);
                Check(executor.Outcome?.Kind == BoardOutcomeKind.Won && executor.LastPangConversions == Math.Min(remaining, candidates), "초기 완료·로켓 변환 상한 " + remaining);
                Check(Snapshot(executor.State.CellAt(C(0, 0))) == web && Snapshot(executor.State.CellAt(C(0, 1))) == mold, "라스트팡 변환에서 덮인 내용물 제외 " + remaining);
                Check(executor.State.Cells.Count(c => c.Content == RuntimeContent.Rocket) == executor.LastPangConversions, "서로 다른 일반 칸 변환 " + remaining);
                if (remaining == 200) Check(executor.State.Cells.Where(c => c.Content == RuntimeContent.Rocket).Select(c => c.RocketDirection).Distinct().Count() == 2, "시드에 따른 가로·세로 로켓 방향 생성");
                if (remaining > 0)
                {
                    BoardOutcome outcome = executor.Outcome; string before = Snapshot(executor.State);
                    Set(executor, "LastPangWaves", executor.CascadeLimit);
                    CascadeStepResult result = executor.AdvanceCascade();
                    Check(result.Reason == CascadeStepReason.LimitReached && ReferenceEquals(outcome, executor.Outcome) &&
                        !executor.HasPendingCascade && Snapshot(executor.State) == before && executor.LastPangMessage.Contains("한도"), "라스트팡 한도는 성공 유지·별도 진단 " + remaining);
                }
                else
                {
                    Finish(executor); Check(executor.Phase == BoardActionPhase.Stopped && executor.LastPangWaves == 0, "파워 없는 라스트팡 즉시 정상 완료");
                }
            }
            LevelDefinition crateLevel = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
            Invoke(typeof(PowerEffectVerification), "Crate", crateLevel, C(4, 4), 3);
            LevelRuntimeState crateState = Build(crateLevel); ShuffleResult crateShuffle = ShuffleResolution.Resolve(crateState);
            Check(crateShuffle.Reason == ShuffleReason.Applied && Snapshot(crateState.Obstacles) == Snapshot(crateShuffle.State.Obstacles) &&
                Snapshot(crateState.CellAt(C(4, 4))) == Snapshot(crateShuffle.State.CellAt(C(4, 4))), "재배치 고정 장애물 위치·내구도 보존");
            UnityEngine.Object.DestroyImmediate(crateLevel);
            LevelRuntimeState hiddenPowers = Build(fixture);
            foreach (RuntimeCell cell in hiddenPowers.Cells) { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
            foreach (RuntimeMission mission in hiddenPowers.Missions) Set(mission, "Progress", mission.Target);
            foreach (CoverKind cover in new[] { CoverKind.Web, CoverKind.Mold })
            {
                RuntimeCell cell = hiddenPowers.CellAt(C(0, cover == CoverKind.Web ? 0 : 1));
                Set(cell, "Content", RuntimeContent.Rocket); Set(cell, "Color", null);
                Set(cell, "RocketDirection", (RocketDirection?)RocketDirection.Horizontal);
                Set(cell, "Cover", (CoverKind?)cover); Set(cell, "CoverDurability", 1);
            }
            string[] hiddenBefore = hiddenPowers.Cells.Take(2).Select(Snapshot).ToArray();
            BoardActionExecutor hiddenEnd = new BoardActionExecutor(hiddenPowers); Finish(hiddenEnd);
            Check(hiddenEnd.Outcome.Kind == BoardOutcomeKind.Won && hiddenEnd.LastPangWaves == 0 && hiddenEnd.LastPangConversions == 0 &&
                hiddenBefore.SequenceEqual(hiddenEnd.State.Cells.Take(2).Select(Snapshot)), "거미줄·곰팡이 속 파워 일괄 발동 제외");
            LevelRuntimeState chain = Build(fixture);
            Set(chain, "MovesRemaining", 0);
            foreach (RuntimeMission mission in chain.Missions) Set(mission, "Progress", mission.Target);
            BoardActionExecutor lastPang = new BoardActionExecutor(chain); Finish(lastPang, true);
            // 성공 확정 뒤 첫 발동 직전의 효과 사례: 첫 로켓과 떨어져 있는 4매칭을 다음 안정 경계에서 처리한다.
            foreach (RuntimeCell cell in lastPang.State.Cells)
            {
                Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); Set(cell, "RocketDirection", null);
            }
            Set(lastPang.State.CellAt(C(0, 0)), "Content", RuntimeContent.Rocket);
            Set(lastPang.State.CellAt(C(0, 0)), "RocketDirection", (RocketDirection?)RocketDirection.Horizontal);
            for (int column = 2; column <= 5; column++)
            {
                Set(lastPang.State.CellAt(C(9, column)), "Content", RuntimeContent.Normal);
                Set(lastPang.State.CellAt(C(9, column)), "Color", (RabbitColor?)RabbitColor.Type1);
            }
            Check(lastPang.AdvanceCascade().Reason == CascadeStepReason.LastPang && lastPang.LastPangWaves == 1, "첫 라스트팡 발동 묶음");
            Check(lastPang.AdvanceCascade().Reason == CascadeStepReason.Settled && lastPang.AdvanceCascade().Reason == CascadeStepReason.Matched, "라스트팡도 정상 정착 후 매칭");
            RuntimeCell generated = lastPang.State.Cells.Single(c => c.Content == RuntimeContent.Rocket);
            Check(lastPang.TurnEffects.IsProtected(generated.Coordinate) && lastPang.LastPangWaves == 1, "새 파워 현재 효과에서 보호·즉시 재폭발 금지");
            Finish(lastPang);
            Check(lastPang.Outcome.Kind == BoardOutcomeKind.Won && lastPang.LastPangWaves == 2 && !lastPang.State.Cells.Any(c => c.Content == RuntimeContent.Rocket), "새 파워는 후속 안정 경계에서 재발동");
            UnityEngine.Object.DestroyImmediate(fixture);
        }
    }
}
