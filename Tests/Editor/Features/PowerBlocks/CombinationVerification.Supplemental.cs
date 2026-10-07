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
    public static partial class CombinationVerification
    {
        public static void Supplemental()
        {
            Results.Clear();
            try { SupplementalChecks(); File.WriteAllLines(Evidence + "/supplemental-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/supplemental-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void SupplementalChecks()
        {
            LevelRuntimeState onlyCombination = Build(Make(9));
            foreach (RuntimeCell cell in onlyCombination.Cells.Where(c => c.Content == RuntimeContent.Normal))
            { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
            Check(ActionQuery.Find(onlyCombination).Count == 1 && ActionQuery.Find(onlyCombination).All(a => a.Kind == QueryActionKind.SwapCombination),
                "단독 발동/일반 매칭 없이 자석 조합만 가능한 조회");
            BoardActionExecutor onlyExecutor = new BoardActionExecutor(onlyCombination);
            Set(onlyExecutor, "Phase", BoardActionPhase.WaitingForAutomaticMatch);
            Set(onlyExecutor, "TurnEffects", Context());
            Check(onlyExecutor.ResolveAutomaticMatch().Reason == CascadeStepReason.Stable && onlyExecutor.Phase == BoardActionPhase.Ready,
                "조합만 가능한 안정 보드는 미지원/재배치 아닌 다음 수 대기");
            Check(onlyExecutor.Swap(C(4, 4), C(4, 5)).IsApplied, "조합만 가능한 다음 수 실제 실행");

            BoardActionExecutor shared = new BoardActionExecutor(Build(Make(5)));
            Check(shared.Swap(C(4, 4), C(4, 5)).IsApplied, "실제 드론 조합 공유 예약 입력");
            TargetingRecord[] reservations = shared.TurnEffects.Targeting.Where(r => r.Event == TargetingEvent.Reserved).ToArray();
            Check(reservations.Length == 3 && reservations.Select(r => r.Target).Distinct().Count() == 3 && reservations.Max(r => r.ReservedCount) == 3,
                "실제 드론3대 착탄 전 서로 다른 목표 동시 예약");
            Check(reservations.All(r => r.Message.Contains("남은 미션")) && shared.TurnEffects.Targeting.Last().ReservedCount == 0,
                "실제 드론3대 미션 우선/최종 공유 예약0");

            foreach (int count in new[] { 0, 1 })
            {
                LevelRuntimeState state = Build(Make(5));
                foreach (RuntimeCell cell in state.Cells.Where(c => c.Content == RuntimeContent.Normal && (!c.Coordinate.Equals(C(8, 8)) || count == 0)))
                { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
                BoardActionExecutor executor = new BoardActionExecutor(state);
                Check(executor.Swap(C(4, 4), C(4, 5)).IsApplied, "드론 조합 목표 부족 입력 " + count);
                Check(executor.TurnEffects.Targeting.Count(r => r.Event == TargetingEvent.Landed) == count && executor.TurnEffects.Targeting.Count(r => r.Event == TargetingEvent.NoTarget) == 3 - count,
                    "드론3대 목표 부족/없음 종료 " + count);
            }
            foreach (int pair in new[] { 6, 7, 8 })
            {
                LevelDefinition level = Make(pair); LevelRuntimeState state = Build(level);
                foreach (RuntimeCell cell in state.Cells.Where(c => c.Content == RuntimeContent.Normal)) Set(cell, "Color", RabbitColor.Type1);
                // 단일색 대량 변환은 시작 보드 조건과 별개인 효과 단계 입력으로 구성한다.
                BoardActionExecutor executor = new BoardActionExecutor(state); Set(executor, "Turn", 1);
                BoardActionResult result = executor.Swap(C(4, 4), C(4, 5));
                Check(result.IsApplied && executor.TurnEffects.Combination.Transformations.Count == BoardDefinition.DefaultRows * BoardDefinition.DefaultColumns - 2 && executor.State.Missions[0].Progress == BoardDefinition.DefaultRows * BoardDefinition.DefaultColumns - 2, "단일색 79개 일괄 변환/집계 " + pair);
                Check(result.RandomAfter == (pair == 6 ? BoardDefinition.DefaultRows * BoardDefinition.DefaultColumns - 2 : 0), "단일색 선택 난수 없음/방향만 난수 " + pair);
                if (pair == 8) Check(executor.TurnEffects.Targeting.Count(r => r.Event == TargetingEvent.NoTarget) == BoardDefinition.DefaultRows * BoardDefinition.DefaultColumns - 2, "선변환 완료 후 일반 대상 없는 드론79대 종료");
            }
            HashSet<RocketDirection> directions = new HashSet<RocketDirection>();
            for (int seed = 0; seed < 12; seed++)
            {
                LevelRuntimeState state = Build(Make(6), seed);
                foreach (RuntimeCell cell in state.Cells.Where(c => c.Content == RuntimeContent.Normal)) Set(cell, "Color", RabbitColor.Type1);
                Set(state.CellAt(C(8, 8)), "Color", RabbitColor.Type2);
                BoardActionExecutor executor = new BoardActionExecutor(state); Set(executor, "Turn", 1); executor.Swap(C(4, 4), C(4, 5));
                PowerCombination combination = executor.TurnEffects.Combination; System.Random expected = new System.Random(seed);
                RabbitColor color = (RabbitColor)expected.Next(2);
                Check(combination.Color == color, "색 개수에 비례하지 않는 균등 선택 " + seed);
                foreach (PowerTransformation transformation in combination.Transformations)
                { RocketDirection direction = (RocketDirection)expected.Next(2); if (transformation.Direction != direction) throw new InvalidOperationException("방향 난수 순서"); directions.Add(direction); }
                Check(true, "좌표순 변환별 독립 방향 난수 " + seed);
            }
            Check(directions.Count == 2, "두 로켓 방향 모두 생성");

            // 조준 칸을 남겨 둔 채 직접 범위의 미션 대상만 소실시킨다.
            LevelDefinition partial = Make(); JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":1,\"count\":2}]}", partial);
            Invoke(typeof(PowerEffectVerification), "Crate", null, partial, C(5, 5), 1); Invoke(typeof(PowerEffectVerification), "Crate", null, partial, C(8, 8), 1);
            LevelRuntimeState partialState = Build(partial); TurnEffectContext context = Context(); DroneTargetManager manager = Manager(partialState, context);
            int request = (int)Invoke(typeof(DroneTargetManager), "RequestArea", manager, C(0, 0), PowerArea.Horizontal);
            BoardCoordinate anchor = context.Targeting.Last().Target.Value;
            DroneTarget target = manager.QueryArea(PowerArea.Horizontal, request).Single(t => t.Coordinate.Equals(anchor));
            Check(target.Content == RuntimeContent.Normal, "부분 무효화 시험의 조준 일반 칸 유지");
            foreach (DroneImpact impact in target.Impacts)
            { RuntimeCell cell = partialState.CellAt(impact.Coordinate); Set(cell, "Content", RuntimeContent.Empty); Set(cell, "ObstacleIndex", null); }
            Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            Check(partialState.CellAt(anchor).Content == RuntimeContent.Normal && manager.ExpectedComplete == 0, "조준 칸 생존/범위 미션 소실 예상0");
            DroneTarget retarget = (DroneTarget)Invoke(typeof(DroneTargetManager), "Land", manager, request, C(0, 0));
            Check(retarget != null && retarget.IsMission && retarget.Coordinate.Row != anchor.Row && retarget.Area == PowerArea.Horizontal && context.Targeting.Any(r => r.Event == TargetingEvent.Retargeted), "미션 다른 줄 재탐색/방향 유지");
            Check(manager.ExpectedComplete == 0 && manager.ReservationCount == 0, "범위 재탐색 예약 해제");

            LevelDefinition durability = Make(9); JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":1,\"count\":2}]}", durability);
            Invoke(typeof(PowerEffectVerification), "Crate", null, durability, C(2, 2), 1); Invoke(typeof(PowerEffectVerification), "Crate", null, durability, C(2, 3), 3);
            BoardActionExecutor crates = new BoardActionExecutor(Build(durability)); crates.Swap(C(4, 4), C(4, 5));
            Check(crates.State.Missions[0].Progress == 1 && crates.State.Obstacles[0].Durability == 0 && crates.State.Obstacles[1].Durability == 2, "전판 조합 완전 제거만 미션/다층 상자1피해");

            LevelDefinition gaps = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", null,
                new Dictionary<BoardCoordinate, int> { [C(0, 0)] = 0, [C(0, 1)] = 0, [C(0, 8)] = 1, [C(8, 1)] = 2, [C(8, 8)] = 3 }, 20);
            Place(gaps, C(0, 0), InitialBlockKind.Rocket); Place(gaps, C(0, 1), InitialBlockKind.Rocket);
            BoardActionExecutor holes = new BoardActionExecutor(Build(gaps)); BoardActionResult holeAction = holes.Swap(C(0, 0), C(0, 1));
            Check(holeAction.IsApplied && holes.State.CellAt(C(0, 8)).Content == RuntimeContent.Empty && holes.State.CellAt(C(8, 1)).Content == RuntimeContent.Empty && holes.State.CellAt(C(8, 8)).Content == RuntimeContent.Normal,
                "비활성 구간 관통/분리 구역 정확한 십자 범위");

            LevelDefinition last = Make(5); JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", last);
            BoardActionExecutor final = new BoardActionExecutor(Build(last)); final.Swap(C(4, 4), C(4, 5)); Finish(final);
            Check(final.State.MovesRemaining == 0 && final.LastCascadeStep.Reason == CascadeStepReason.MovesExhausted && final.TurnEffects.Targeting.Count(r => r.Event == TargetingEvent.Landed) == 3, "마지막 수 조합 드론3대 후 전체 연쇄 완료");

            // 공통 실행기의 실패 작업 사본을 검사한다. 실제 입력은 미지원 보드를 사전 거절한다.
            LevelDefinition failure = Make(9);
            Invoke(typeof(PowerEffectVerification), "Crate", null, failure, C(8, 8), 1);
            LevelRuntimeState source = Build(failure); string original = Snapshot(source);
            LevelRuntimeState work = new BoardActionExecutor(source).State;
            typeof(RuntimeObstacle).GetField("<Definition>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(work.Obstacles[0],
                JsonUtility.FromJson<ObstaclePlacementDefinition>("{\"kind\":99,\"durability\":1}"));
            TurnEffectContext failedContext = Context(); List<EffectRecord> effects = new List<EffectRecord>();
            object[] args = { work, C(4, 4), C(4, 5), failedContext, effects, null };
            Check(!(bool)Invoke(typeof(PowerEffectResolution), "ApplyCombination", null, args) && effects.Count > 0 && ((string)args[5]).Contains("전체 취소"), "전판 조합 후반 미지원 타격 실패");
            Check(Snapshot(source) == original, "실패 작업 사본이 원본/난수/미션 미변경");
            BoardActionExecutor unsupported = new BoardActionExecutor(work); string before = Snapshot(unsupported.State);
            Check(!unsupported.Swap(C(4, 4), C(4, 5)).IsApplied && Snapshot(unsupported.State) == before, "미지원 보드 사용자 입력 원자적 거절");
        }
    }
}
