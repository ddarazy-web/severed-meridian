using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class LayerVerification
    {
        public static void Supplemental()
        {
            Results.Clear();
            try { SupplementalChecks(); File.WriteAllLines(Evidence + "/supplemental-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/supplemental-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void SupplementalChecks()
        {
            foreach (RuntimeContent power in new[] { RuntimeContent.Rocket, RuntimeContent.Bomb, RuntimeContent.Drone, RuntimeContent.Magnet })
            {
                LevelDefinition level = Make(); Missions(level); LevelRuntimeState state = Build(level);
                RuntimeCell wrapped = state.CellAt(C(4, 4)); Set(wrapped, "Content", power); Set(wrapped, "Color", null); Web(wrapped, 1);
                DroneTargetManager manager = (DroneTargetManager)Invoke(typeof(TargetPowerVerification), "Manager", null, state, Context());
                Check(manager.Query().Any(t => t.Coordinate.Equals(wrapped.Coordinate) && t.Contributions.Any(c => c.MissionIndex == 2 && c.ExpectedComplete == 1)),
                    "거미줄 속 파워도 제거 미션 드론 후보 " + power);
            }
            LevelDefinition transform = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, 6, RocketDirection.Horizontal, null);
            Missions(transform); LevelRuntimeState transformState = Build(transform);
            foreach (RuntimeCell cell in transformState.Cells.Where(c => c.Content == RuntimeContent.Normal)) Set(cell, "DustDurability", 2);
            BoardActionExecutor transforming = new BoardActionExecutor(transformState); Check(transforming.Swap(C(4, 4), C(4, 5)).IsApplied &&
                transforming.TurnEffects.Combination.Transformations.All(t => transforming.State.CellAt(t.Coordinate).DustDurability == 1 && transforming.TurnEffects.HasDamagedDust(t.Coordinate)),
                "자석 선변환 칸 먼지1피해/후속 로켓 중복 피해 없음");

            LevelDefinition moving = Make(); LevelSupplyEditing.PlaceSources(moving, new[] { C(0, 1) });
            LevelRuntimeState source = Build(moving);
            foreach (RuntimeCell cell in source.Cells) { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
            Set(source.CellAt(C(0, 0)), "Content", RuntimeContent.Normal); Set(source.CellAt(C(0, 0)), "Color", RabbitColor.Type1); Web(source.CellAt(C(0, 0)), 2);
            Set(source.CellAt(C(0, 1)), "DustDurability", 2); string original = Snapshot(source);
            SettlementResult settled = SettlementResolution.Resolve(source, Context());
            Check(settled.IsApplied && settled.State.CellAt(C(0, 0)).CoverDurability == 2 && settled.State.CellAt(C(0, 0)).Content == RuntimeContent.Normal,
                "거미줄 고정 상태에서 실제 낙하/공급 완료");
            Check(settled.Records.Count > 0 && settled.State.CellAt(C(0, 1)).DustDurability == 2 && Snapshot(source) == original,
                "공급/이동은 먼지 손상·복사 없음/원본 보존");

            LevelDefinition fall = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", null,
                new Dictionary<BoardCoordinate, int> { [C(1, 0)] = 0, [C(1, 1)] = 0, [C(1, 2)] = 0, [C(0, 2)] = 0 }, 20);
            BoardActionExecutor repeated = (BoardActionExecutor)Invoke(typeof(CascadeVerification), "Automatic", null, fall, 12345);
            Web(repeated.State.CellAt(C(1, 0)), 3); Web(repeated.State.CellAt(C(1, 1)), 3);
            Check(repeated.ResolveAutomaticMatch().IsApplied && repeated.State.CellAt(C(1, 0)).CoverDurability == 2, "혼합 매칭 첫 덮개 피해");
            Check(repeated.Settle().IsApplied && repeated.State.CellAt(C(1, 2)).Content == RuntimeContent.Normal, "실제 낙하로 같은 색 재유입");
            CascadeStepResult second = repeated.ResolveAutomaticMatch();
            Check(second.Reason == CascadeStepReason.Matched && second.Changes.Count(c => c.IsConsumed) == 1 && repeated.State.CellAt(C(1, 0)).CoverDurability == 2,
                "실제 재유입 매칭 정상 소비/덮개 중복피해 없음");

            LevelDefinition closed = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", null,
                new Dictionary<BoardCoordinate, int> { [C(0, 0)] = 0, [C(0, 1)] = 0, [C(0, 2)] = 0 }, 20);
            BoardActionExecutor residual = (BoardActionExecutor)Invoke(typeof(CascadeVerification), "Automatic", null, closed, 12345);
            foreach (RuntimeCell cell in residual.State.Cells.Where(c => c.IsActive)) Web(cell, 3);
            residual.ResolveAutomaticMatch(); MatchPattern pattern = MatchQuery.Find(residual.State).Single();
            TurnEffectContext next = (TurnEffectContext)Invoke(typeof(TurnEffectContext), "NextTurn", residual.TurnEffects, 2);
            Check((bool)Invoke(typeof(TurnEffectContext), "WasProcessed", next, pattern) && !next.HasDamagedWeb(C(0, 0)), "다음 턴 잔존 이력 유지/피해만 초기화");
            Invoke(typeof(TurnEffectContext), "RecordArrival", next, C(9, 9), C(8, 8), 1, true);
            Check((bool)Invoke(typeof(TurnEffectContext), "WasProcessed", next, pattern), "무관한 칸 유입은 잔존 매칭 재처리 안 함");
            LevelObstacleEditing.Apply(closed, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 3 }, new[] { C(0, 0), C(0, 1), C(0, 2) });
            StartingBoardSearch invalidStart = new StartingBoardSearch(closed, 12345);
            Check(invalidStart.IsDone && invalidStart.Status != StartingBoardStatus.Success, "전체 고정 거미줄 완성 패턴은 정상 시작 불가");

            HashSet<BoardCoordinate> spawned = new HashSet<BoardCoordinate>();
            LevelDefinition line = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", null,
                new Dictionary<BoardCoordinate, int> { [C(0, 0)] = 0, [C(0, 1)] = 0, [C(0, 2)] = 0, [C(0, 3)] = 0 }, 20);
            for (int seed = 0; seed < 8; seed++)
            {
                BoardActionExecutor auto = (BoardActionExecutor)Invoke(typeof(CascadeVerification), "Automatic", null, line, seed);
                Web(auto.State.CellAt(C(0, 1)), 2); Invoke(typeof(TurnEffectContext), "RecordArrival", auto.TurnEffects, C(9, 9), C(0, 1), 1, true);
                CascadeStepResult result = auto.ResolveAutomaticMatch(); spawned.Add(result.Decisions[0].Spawn.Value);
                Check(result.IsApplied && result.RandomAfter - result.RandomBefore == 1 && result.Decisions[0].Spawn.Value.Column != 3, "최단거리 동률만 난수1 " + seed);
            }
            Check(spawned.SetEquals(new[] { C(0, 0), C(0, 2) }), "대체 생성 양쪽 동률 후보 도달");

            foreach (int pair in new[] { 6, 7, 8 })
            {
                LevelDefinition definition = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, pair, RocketDirection.Horizontal, null); Missions(definition);
                LevelRuntimeState state = Build(definition);
                foreach (RuntimeCell cell in state.Cells.Where(c => c.Content == RuntimeContent.Normal))
                {
                    if (cell.Coordinate.Equals(C(0, 0)) || cell.Coordinate.Equals(C(9, 9))) { Set(cell, "Color", RabbitColor.Type1); Web(cell, 2); Set(cell, "DustDurability", 1); }
                    else { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
                }
                BoardActionExecutor executor = new BoardActionExecutor(state); BoardActionResult action = executor.Swap(C(4, 4), C(4, 5));
                Check(action.IsApplied && action.RandomAfter == action.RandomBefore && executor.TurnEffects.Combination.Transformations.Count == 0 && executor.TurnEffects.Combination.CoveredTargets.Count == 2,
                    "묶인 색만 있는 자석 조합 허용/비변환/무난수 " + pair);
                Check(executor.State.CellAt(C(0, 0)).CoverDurability == 1 && executor.State.CellAt(C(9, 9)).CoverDurability == 1 && executor.State.Missions.All(m => m.Progress == 0),
                    "묶인 대상만 덮개 피해/내용물·먼지·완료 집계 보존 " + pair);
            }

            LevelDefinition last = CreateFixture(1); JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", last);
            BoardActionExecutor final = new BoardActionExecutor(Build(last)); Check(final.Swap(C(4, 4), C(4, 5)).IsApplied, "층 중첩 마지막 수 실행"); Finish(final);
            Check(final.LastCascadeStep.Reason == CascadeStepReason.MovesExhausted && final.State.CellAt(C(4, 6)).CoverDurability == 1, "층 중첩 마지막 수 효과/정착 뒤 종료");

            LevelDefinition magnetLevel = Make(); Missions(magnetLevel); LevelRuntimeState magnet = Build(magnetLevel);
            foreach (RuntimeCell cell in magnet.Cells.Where(c => c.Content == RuntimeContent.Normal)) Set(cell, "Color", RabbitColor.Type2);
            Set(magnet.CellAt(C(4, 4)), "Content", RuntimeContent.Magnet); Set(magnet.CellAt(C(4, 4)), "Color", null);
            Set(magnet.CellAt(C(4, 5)), "Color", RabbitColor.Type1); Set(magnet.CellAt(C(0, 0)), "Color", RabbitColor.Type1);
            Web(magnet.CellAt(C(0, 0)), 1); Set(magnet.CellAt(C(0, 0)), "DustDurability", 1);
            BoardActionExecutor single = new BoardActionExecutor(magnet); Set(single, "Turn", 1);
            Check(single.Swap(C(4, 4), C(4, 5)).IsApplied && single.State.CellAt(C(0, 0)).Cover == null && single.State.CellAt(C(0, 0)).Content == RuntimeContent.Normal &&
                single.State.CellAt(C(0, 0)).DustDurability == 1 && single.State.Missions[0].Progress == 1 && single.State.Missions[2].Progress == 1,
                "단독 자석 교환 색으로 거미줄만 제거/노출 상대만 색 수집");

            LevelDefinition crateDust = Make(); Invoke(typeof(PowerEffectVerification), "Crate", null, crateDust, C(4, 4), 1);
            LevelRuntimeState under = Build(crateDust); Set(under.CellAt(C(4, 4)), "DustDurability", 2); TurnEffectContext underContext = Context();
            Hit(under, C(4, 4), underContext); Hit(under, C(4, 4), underContext);
            Check(under.CellAt(C(4, 4)).Content == RuntimeContent.Empty && under.CellAt(C(4, 4)).DustDurability == 2, "상자 제거 및 빈칸 재타격은 먼지 무피해");

            LevelDefinition ranges = Make(); Missions(ranges); LevelRuntimeState rangeState = Build(ranges);
            Set(rangeState.Missions[0], "Progress", 100); Set(rangeState.Missions[1], "Progress", 100);
            Web(rangeState.CellAt(C(5, 5)), 1); Web(rangeState.CellAt(C(5, 6)), 2); Set(rangeState.CellAt(C(9, 9)), "DustDurability", 1);
            TurnEffectContext rangeContext = Context(); DroneTargetManager rangesManager = (DroneTargetManager)Invoke(typeof(TargetPowerVerification), "Manager", null, rangeState, rangeContext);
            Check(rangesManager.QueryArea(PowerArea.Horizontal).Any(t => t.Impacts.Count == 2) && rangesManager.QueryArea(PowerArea.Blast3).Any(t => t.Impacts.Count == 2),
                "층 미션의 줄/3x3 직접 범위 기여 조회");
            int rangeRequest = (int)Invoke(typeof(DroneTargetManager), "RequestArea", rangesManager, C(0, 0), PowerArea.Horizontal);
            BoardCoordinate anchor = rangeContext.Targeting.Last().Target.Value;
            DroneTarget reserved = rangesManager.QueryArea(PowerArea.Horizontal, rangeRequest).Single(t => t.Coordinate.Equals(anchor));
            Check(rangesManager.QueryArea(PowerArea.Blast3).All(t => t.Impacts.All(i => reserved.Impacts.All(p => !p.Coordinate.Equals(i.Coordinate)))), "층 범위 예약끼리 동일 목표 기여 중복 제외");
            Hit(rangeState, reserved.Impacts[0].Coordinate, rangeContext); Invoke(typeof(DroneTargetManager), "Invalidate", rangesManager);
            Check(rangesManager.ExpectedDamage < reserved.Impacts.Count, "층 범위 부분 피해/소실 후 예상 기여 감소");
            Invoke(typeof(DroneTargetManager), "Land", rangesManager, rangeRequest, C(0, 0));
            Check(rangesManager.ReservationCount == 0 && rangeContext.HasDamagedWeb(C(5, 5)) == (reserved.Impacts[0].Coordinate.Equals(C(5, 5))), "층 범위 착탄 해제는 실제 피해 기록 보존");

            // 실제 실패 경계에서 패턴/층 피해도 앞선 성공 단계와 독립이어야 한다.
            LevelDefinition failure = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", null,
                new Dictionary<BoardCoordinate, int> { [C(0, 0)] = 0, [C(0, 1)] = 0, [C(0, 2)] = 0, [C(1, 0)] = 1 }, 20);
            Invoke(typeof(PowerEffectVerification), "Crate", null, failure, C(1, 0), 2);
            BoardActionExecutor failed = (BoardActionExecutor)Invoke(typeof(CascadeVerification), "Automatic", null, failure, 12345);
            Web(failed.State.CellAt(C(0, 0)), 2); Set(failed.State.CellAt(C(0, 1)), "DustDurability", 2); typeof(RuntimeObstacle).GetField("<Definition>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(failed.State.Obstacles[0], UnityEngine.JsonUtility.FromJson<ObstaclePlacementDefinition>("{\"kind\":99,\"durability\":3}"));
            string before = Snapshot(failed.State) + ContextSnapshot(failed);
            Check(!failed.ResolveAutomaticMatch().IsApplied && Snapshot(failed.State) + ContextSnapshot(failed) == before,
                "덮개·먼지·패턴 이력 변경 뒤 실패 원자적 보존");
        }
    }
}

