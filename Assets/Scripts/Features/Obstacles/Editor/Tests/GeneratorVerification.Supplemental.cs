using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Board;
using Simulation;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class GeneratorVerification
    {
        private static void AdditionalChecks()
        {
            LevelDefinition level = Make(ObstacleKind.Appliance, 3);
            LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(4, 3), C(4, 4)) }, false);
            LevelRuntimeState state = Build(level); TurnEffectContext context = Context();
            Check(DamageReaction.Evaluate(state, C(4, 4), DamageCause.AdjacentMatch, C(4, 3), context).Response == DamageResponse.Wall &&
                DamageReaction.Evaluate(state, C(4, 4), DamageCause.Power, C(4, 3), context).Response == DamageResponse.Charge, "벽은 인접 충전만 차단/파워 관통");
            Check(!ActionQuery.Swap(state, C(4, 4), C(4, 3)).IsAllowed && !MovementQuery.Find(state).Any(m => m.IsAllowed && state.CellAt(m.Source).ObstacleIndex == 0), "발전기 교환/낙하 불가");
            BoardCoordinate[] cells = { C(3, 3), C(3, 4), C(3, 5) };
            foreach (BoardCoordinate cell in cells) Set(state.CellAt(cell), "Color", RabbitColor.Type1);
            Invoke(typeof(FixedObstacleVerification), "Matches", null, state, context, cells, C(3, 4));
            Check(state.Obstacles[0].Charge == 1 && context.Generators.Count(r => r.Event == GeneratorEvent.Charged) == 1, "실제 일반 매칭 여러 외곽 접촉1충전");
            Hit(state, C(5, 5), context); Check(state.Obstacles[0].Charge == 1, "매칭 뒤 파워 같은 수 중복 충전 없음");
            UnityEngine.Object.DestroyImmediate(level);

            LevelDefinition twice = Make(ObstacleKind.Appliance, 3);
            foreach (BoardCoordinate origin in new[] { C(0, 0), C(0, 3) })
                LevelObstacleEditing.Apply(twice, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, LevelPlacementRules.Footprint(origin, 2));
            LevelObstacleEditing.Apply(twice, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Generator, RequiredCharge = 5 }, new[] { C(0, 0) });
            LevelObstacleEditing.Apply(twice, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = 1 }, new[] { C(0, 3) });
            Check(LevelConnectionEditing.Add(twice, twice.Obstacles[2].Id, twice.Obstacles[3].Id) == null &&
                LevelConnectionEditing.SetWire(twice, 1, new[] { C(0, 2), C(0, 3) }) == null, "두 발전기 연결 설정");
            state = Build(twice); context = Context(); Hit(state, C(4, 4), context); Hit(state, C(0, 0), context); Hit(state, C(1, 1), context);
            Check(state.Obstacles[0].Charge == 1 && state.Obstacles[2].Charge == 1, "서로 다른 발전기 독립1충전");
            Hit(state, C(0, 3), context);
            Check(GeneratorRules.ActiveConnections(state).Count == 1 && state.Cells.Count(c => c.ObstacleIndex == 0) == 4, "다른 발전기 자동 철거 영향 없음");
            UnityEngine.Object.DestroyImmediate(twice);

            LevelDefinition overlap = Make(ObstacleKind.Appliance, 3); state = Build(overlap); context = Context();
            Set(state.Obstacles[0], "Charge", 2); Set(state.Obstacles[1], "Durability", 1);
            DroneTargetManager manager = Manager(state, context);
            Dictionary<int, DroneTarget> reservations = (Dictionary<int, DroneTarget>)typeof(DroneTargetManager).GetField("reservations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            DroneTarget generator = manager.Query().First(t => t.ObstacleIndex == 0), target = manager.Query().First(t => t.ObstacleIndex == 1);
            reservations.Add(1, target);
            Check(manager.Query().All(t => t.ObstacleIndex != 0), "유일한 연결 목표가 직접 제거 예약되면 발전기 우선 후보 제외");
            reservations.Add(2, generator);
            Check(manager.ExpectedComplete == 1 && manager.ExpectedCharge == 1 && manager.ExpectedDamage == 1, "직접/간접 같은 본체 예상 완료 중복 제거");
            reservations.Remove(1); reservations.Remove(2);
            Check(manager.ExpectedComplete == 0 && manager.ExpectedCharge == 0 && manager.Query().Any(t => t.ObstacleIndex == 0), "예약 취소 예상량/후보 복구");
            reservations.Add(3, generator); Hit(state, C(4, 8), context); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            DroneTarget landed = (DroneTarget)Invoke(typeof(DroneTargetManager), "Land", manager, 3, C(0, 0));
            Check(landed.Content == RuntimeContent.Normal && manager.ReservationCount == 0 && context.Targeting.Any(r => r.Event == TargetingEvent.Retargeted), "후보 소멸 후 재탐색/일반 대체/해제");
            UnityEngine.Object.DestroyImmediate(overlap);

            LevelDefinition retry = Make(ObstacleKind.Appliance, 3); state = Build(retry); context = Context(); manager = Manager(state, context);
            reservations = (Dictionary<int, DroneTarget>)typeof(DroneTargetManager).GetField("reservations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            reservations.Add(4, manager.Query().First(t => t.ObstacleIndex == 0)); Hit(state, C(4, 4), context); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            landed = (DroneTarget)Invoke(typeof(DroneTargetManager), "Land", manager, 4, C(0, 0));
            Check(landed.ObstacleIndex == 1 && landed.IsMission && manager.ReservationCount == 0, "충전으로 무효화된 예약은 남은 미션부터 재탐색");
            UnityEngine.Object.DestroyImmediate(retry);

            LevelDefinition failure = Make(ObstacleKind.Safe, 3);
            for (int column = 3; column <= 8; column++) Invoke(typeof(PowerEffectVerification), "Place", null, failure, C(3, column), InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type1);
            BoardActionExecutor failed = (BoardActionExecutor)Invoke(typeof(CascadeVerification), "Automatic", null, failure, 12345);
            typeof(RuntimeObstacle).GetField("<Definition>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(failed.State.Obstacles[1], JsonUtility.FromJson<ObstaclePlacementDefinition>("{\"kind\":99,\"durability\":3}"));
            string before = Snapshot(failed.State) + Snapshot(failed.TurnEffects);
            Check(!failed.ResolveAutomaticMatch().IsApplied && before == Snapshot(failed.State) + Snapshot(failed.TurnEffects) && failed.State.Obstacles[0].Charge == 0, "충전 뒤 미지원 반응 실패 전체 원자성");
            UnityEngine.Object.DestroyImmediate(failure);
            LevelDefinition automatic = Make(ObstacleKind.Appliance, 3);
            for (int column = 3; column <= 5; column++) Invoke(typeof(PowerEffectVerification), "Place", null, automatic, C(3, column), InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type1);
            BoardActionExecutor cascade = (BoardActionExecutor)Invoke(typeof(CascadeVerification), "Automatic", null, automatic, 12345);
            string key = (string)Invoke(typeof(BoardActionExecutor), "CascadeKey", cascade);
            Hit(cascade.State, C(4, 4), cascade.TurnEffects);
            Check(key != (string)Invoke(typeof(BoardActionExecutor), "CascadeKey", cascade), "충전 진행은 무진행 반복 상태와 구분");
            Check(cascade.ResolveAutomaticMatch().IsApplied && cascade.State.Obstacles[0].Charge == 1 && cascade.TurnEffects.HasCharged(0), "실제 자동 연쇄에서 이전 충전 이력 유지");
            UnityEngine.Object.DestroyImmediate(automatic);
            foreach (PowerArea area in new[] { PowerArea.Horizontal, PowerArea.Vertical })
            {
                LevelDefinition areaLevel = Make(ObstacleKind.Appliance, 3); state = Build(areaLevel); context = Context(); manager = Manager(state, context);
                reservations = (Dictionary<int, DroneTarget>)typeof(DroneTargetManager).GetField("reservations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                reservations.Add(1, manager.QueryArea(area).First(t => t.ObstacleIndex == 0));
                Check(manager.ExpectedCharge == 1 && manager.QueryArea(area).All(t => t.ObstacleIndex != 0), "로켓 범위 네 칸 본체 단일 충전 예약 " + area);
                Set(state.Obstacles[0], "Charge", 2); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
                Check(manager.ExpectedComplete == 1 && manager.ExpectedCharge == 1, "로켓 직접 피해/완충 간접 제거 중복 없음 " + area);
                UnityEngine.Object.DestroyImmediate(areaLevel);
            }
            foreach (InitialBlockKind power in new[] { InitialBlockKind.Rocket, InitialBlockKind.Bomb, InitialBlockKind.Magnet })
            {
                LevelDefinition combo = Make(ObstacleKind.Appliance, 3);
                Invoke(typeof(PowerEffectVerification), "Place", null, combo, C(3, 2), power, RocketDirection.Horizontal, RabbitColor.Type1);
                Invoke(typeof(PowerEffectVerification), "Place", null, combo, C(3, 3), power == InitialBlockKind.Rocket ? InitialBlockKind.Bomb : power, RocketDirection.Horizontal, RabbitColor.Type1);
                BoardActionExecutor executor = new BoardActionExecutor(Build(combo)); BoardActionResult action = executor.Swap(C(3, 2), C(3, 3));
                Check(action.IsApplied && executor.State.Obstacles[0].Charge == 1 && action.Effects.Count(e => e.Response == DamageResponse.Charge) == 1, "여러 칸 조합 타격 최대1충전 " + power);
                UnityEngine.Object.DestroyImmediate(combo);
            }
        }
    }
}
