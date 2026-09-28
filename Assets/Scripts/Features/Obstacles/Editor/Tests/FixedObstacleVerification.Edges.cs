using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class FixedObstacleVerification
    {
        public static void Edges()
        {
            Results.Clear();
            try { EdgeChecks(); File.WriteAllLines(Evidence + "/edge-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/edge-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void EdgeChecks()
        {
            LevelDefinition two = Make(); Obstacle(two, ObstacleKind.Appliance, 1, C(4, 4)); Obstacle(two, ObstacleKind.Appliance, 1, C(7, 7));
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Appliance + ",\"count\":2}]}", two);
            LevelRuntimeState state = Build(two); TurnEffectContext context = Context(); DroneTargetManager manager = Manager(state, context);
            int first = (int)Invoke(typeof(DroneTargetManager), "Request", manager, C(0, 0));
            DroneTarget[] remaining = manager.Query().ToArray();
            Check(remaining.Length == 4 && remaining.Select(t => t.ObstacleIndex).Distinct().Count() == 1, "목표2에서도 이미 제거 예정 본체 네 칸 제외");
            int second = (int)Invoke(typeof(DroneTargetManager), "Request", manager, C(0, 1));
            Check(manager.ExpectedComplete == 2 && manager.ExpectedDamage == 2 && manager.Query().All(t => !t.IsMission), "서로 다른 본체 예약2/미션 예정2");
            DroneTarget target = (DroneTarget)Invoke(typeof(DroneTargetManager), "Land", manager, first, C(0, 0));
            Hit(state, target.Coordinate, context); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            Check(state.Missions[0].Progress == 1 && manager.ExpectedComplete == 1, "한 본체 제거 후 다른 본체 예약 보존");
            int live = remaining[0].ObstacleIndex.Value;
            Hit(state, state.Cells.First(c => c.ObstacleIndex == live).Coordinate, context); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            DroneTarget fallback = (DroneTarget)Invoke(typeof(DroneTargetManager), "Land", manager, second, C(0, 1));
            Check(fallback != null && fallback.Content == RuntimeContent.Normal && manager.ReservationCount == 0 && context.Targeting.Any(r => r.Event == TargetingEvent.Retargeted), "예약 본체 소실 후 미션 재탐색/일반 대체/해제");
            foreach (ObstacleKind kind in new[] { ObstacleKind.Safe, ObstacleKind.ColorLock })
            {
                LevelDefinition level = Make(); Obstacle(level, kind, 3, C(4, 4)); SetMission(level, kind);
                LevelRuntimeState current = Build(level); TurnEffectContext turn = Context(); DroneTargetManager single = Manager(current, turn);
                Check(single.Query().Count == 1 && single.Query()[0].IsMission, "단일 본체 드론 미션 후보 " + kind);
                Hit(current, C(4, 4), turn); Invoke(typeof(DroneTargetManager), "Invalidate", single);
                Check(single.Query().All(t => !t.IsMission), "같은 수 이미 피해 본체 후보 제외 " + kind);
                TurnEffectContext next = (TurnEffectContext)Invoke(typeof(TurnEffectContext), "NextTurn", turn, 2);
                Check(Manager(current, next).Query().Count == 1 && next.LastHit == 0, "다음 수 드론 후보/타격 번호 초기화 " + kind);
            }
            foreach (bool wall in new[] { false, true })
            {
                LevelDefinition level = Make(); Obstacle(level, ObstacleKind.Appliance, 9, C(4, 4)); Place(level, C(4, 0), InitialBlockKind.Rocket);
                if (wall) LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(4, 3), C(4, 4)) }, false);
                LevelRuntimeState current = Build(level); TurnEffectContext turn = Context();
                Check(DamageReaction.Evaluate(current, C(4, 4), DamageCause.AdjacentMatch, C(4, 3), turn).Response == (wall ? DamageResponse.Wall : DamageResponse.Damage), "폐가전 외곽 인접 벽 " + wall);
                Hit(current, C(4, 0), turn); Check(current.Obstacles[0].Durability == 7, "폐가전 파워는 외곽 벽 관통 " + wall);
            }
            LevelDefinition adjacent = Make(); Obstacle(adjacent, ObstacleKind.ColorLock, 3, C(4, 4)); Place(adjacent, C(3, 0), InitialBlockKind.Rocket);
            LevelRuntimeState adjacentState = Build(adjacent); Hit(adjacentState, C(3, 0), Context());
            Check(adjacentState.Obstacles[0].Durability == 3, "일반 파워의 옆 색 블록 제거는 자물쇠 인접 예외 아님");
            LevelDefinition failure = Make(); Obstacle(failure, ObstacleKind.Appliance, 9, C(1, 0)); Obstacle(failure, ObstacleKind.Safe, 3, C(1, 2));
            foreach (BoardCoordinate c in new[] { C(0, 0), C(0, 1), C(0, 2) }) Place(failure, c, InitialBlockKind.FixedNormal);
            BoardActionExecutor failed = (BoardActionExecutor)Invoke(typeof(CascadeVerification), "Automatic", null, failure, 12345);
            typeof(RuntimeObstacle).GetField("<Definition>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(failed.State.Obstacles[1], JsonUtility.FromJson<ObstaclePlacementDefinition>("{\"kind\":99,\"durability\":3}"));
            string before = Snapshot(failed.State) + ContextSnapshot(failed);
            Check(!failed.ResolveAutomaticMatch().IsApplied && Snapshot(failed.State) + ContextSnapshot(failed) == before && failed.CascadeHistory.Count == 0, "폐가전 피해 뒤 미지원 실패에서도 전체 상태/미션/타격 문맥 보존");
            BoardActionExecutor input = new BoardActionExecutor(failed.State); string original = Snapshot(input.State);
            Check(!input.Activate(C(0, 0)).IsApplied && Snapshot(input.State) == original && input.TurnEffects == null, "미지원 장애물 종류 사전 거절 원자성");
            AdditionalChecks();
        }
        private static void AdditionalChecks()
        {
            LevelDefinition swapLevel = Make(); Obstacle(swapLevel, ObstacleKind.Appliance, 9, C(4, 4));
            foreach (BoardCoordinate c in new[] { C(3, 3), C(3, 5), C(3, 6) }) Place(swapLevel, c, InitialBlockKind.FixedNormal);
            foreach (BoardCoordinate c in new[] { C(3, 4), C(4, 3), C(5, 3) }) Place(swapLevel, c, InitialBlockKind.FixedNormal, color: RabbitColor.Type2);
            BoardActionExecutor swap = new BoardActionExecutor(Build(swapLevel)); BoardActionResult swapped = swap.Swap(C(3, 3), C(3, 4));
            Check(swapped.IsApplied && swapped.Decisions.Count == 2 && swap.State.Obstacles[0].Durability == 5, "실제 한 교환의 독립 두 매칭이 같은 본체에 각각 피해");
            Check(swapped.Effects.Where(e => e.Response == DamageResponse.Damage).Select(e => e.HitGroup).Distinct().Count() == 2 && swap.State.MovesRemaining == 19, "두 매칭 타격 분리/이동 수는1회");
            LevelDefinition automatic = Make(); Obstacle(automatic, ObstacleKind.Appliance, 9, C(1, 0));
            foreach (BoardCoordinate c in new[] { C(0, 0), C(0, 1), C(0, 2) })
            {
                Place(automatic, c, InitialBlockKind.FixedNormal);
                Invoke(typeof(SettlementVerification), "Source", null, automatic, c, SupplyExhaustion.Stop, new[] { new SupplyItem(SupplyKind.FixedNormal) });
            }
            BoardActionExecutor cascade = (BoardActionExecutor)Invoke(typeof(CascadeVerification), "Automatic", null, automatic, 12345);
            Check(cascade.ResolveAutomaticMatch().IsApplied && cascade.State.Obstacles[0].Durability == 7, "첫 자동 매칭 폐가전 두 칸 피해");
            Check(cascade.Settle().IsApplied && cascade.ResolveAutomaticMatch().IsApplied && cascade.State.Obstacles[0].Durability == 5, "실제 공급/재매칭 후 같은 수 폐가전 반복 피해");
            foreach (bool exchange in new[] { false, true })
            {
                LevelDefinition level = Make(); Obstacle(level, ObstacleKind.ColorLock, 3, C(4, 4));
                Place(level, C(0, 0), InitialBlockKind.Magnet); Place(level, C(0, 1), InitialBlockKind.FixedNormal); Place(level, C(4, 3), InitialBlockKind.FixedNormal);
                BoardCoordinate[] hidden = level.InitialBlocks.Where(b => b.Kind == InitialBlockKind.FixedNormal && !b.Coordinate.Equals(C(0, 1)) && !b.Coordinate.Equals(C(4, 3))).Select(b => b.Coordinate).ToArray();
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Mold, Durability = 1 }, hidden);
                BoardActionExecutor action = new BoardActionExecutor(Build(level));
                BoardActionResult result = exchange ? action.Swap(C(0, 0), C(0, 1)) : action.Activate(C(0, 0));
                Check(result.IsApplied && action.State.Obstacles[0].Durability == 2 && action.State.Obstacles[0].Definition.Color == RabbitColor.Type1, "실제 자석 발동/교환 색 제거 인접 예외 " + exchange);
            }
            int vertical = 0;
            for (int seed = 1; seed <= 8; seed++)
            {
                LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, 6, RocketDirection.Horizontal, (BoardCoordinate?)C(0, 0));
                Obstacle(level, ObstacleKind.ColorLock, 3, C(4, 4)); Place(level, C(4, 3), InitialBlockKind.FixedNormal);
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Mold, Durability = 1 },
                    level.InitialBlocks.Where(b => b.Kind == InitialBlockKind.FixedNormal && !b.Coordinate.Equals(C(4, 3))).Select(b => b.Coordinate).ToArray());
                BoardActionExecutor action = new BoardActionExecutor(Build(level, seed)); BoardActionResult result = action.Swap(C(0, 0), C(0, 1));
                Check(result.IsApplied && action.TurnEffects.Combination.Transformations.Count == 1, "자석 로켓 단일 노출 변환 " + seed);
                if (action.TurnEffects.Combination.Transformations[0].Direction == RocketDirection.Vertical)
                { vertical++; Check(action.State.Obstacles[0].Durability == 3, "색 변환만으로 자물쇠 인접 피해 없음 " + seed); }
                else Check(action.State.Obstacles[0].Durability == 2, "변환 뒤 직접 로켓은 자물쇠 피해 " + seed);
            }
            Check(vertical > 0 && vertical < 8, "양 방향 변환 사례 확보");
        }
    }
}
