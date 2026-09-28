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
        private static void Set(object owner, string property, object value) => owner.GetType().GetProperty(property).GetSetMethod(true).Invoke(owner, new[] { value });
        private static DroneTargetManager Manager(LevelRuntimeState state, TurnEffectContext context) => (DroneTargetManager)Activator.CreateInstance(typeof(DroneTargetManager), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { state, context }, null);
        private static LevelDefinition Multiple()
        {
            LevelDefinition level = Make(ObstacleKind.Appliance, 3);
            foreach (BoardCoordinate cell in new[] { C(1, 4), C(7, 4) })
            {
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { cell });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = 1 }, new[] { cell });
                string error = LevelConnectionEditing.Add(level, level.Obstacles[0].Id, level.Obstacles.Last().Id);
                if (error != null) throw new InvalidOperationException(error);
            }
            string upper = LevelConnectionEditing.SetWire(level, 1, new[] { C(4, 4), C(3, 4), C(2, 4) });
            string lower = LevelConnectionEditing.SetWire(level, 2, new[] { C(6, 4), C(7, 4) });
            if (upper != null || lower != null) throw new InvalidOperationException(upper + lower);
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Appliance + ",\"count\":1},{\"kind\":" + (int)MissionKind.Crate + ",\"count\":2}]}", level);
            return level;
        }
        private static void EdgeChecks()
        {
            LevelDefinition level = Multiple(); LevelRuntimeState state = Build(level); TurnEffectContext context = Context();
            string original = Snapshot(state);
            LevelRuntimeState copy = new BoardActionExecutor(state).State; Hit(copy, C(4, 4), context);
            Check(Snapshot(state) == original && copy.Obstacles[0].Charge == 1 && GeneratorRules.ActiveConnections(state).Count == 3, "사본 충전/원본 연결 독립");
            TurnEffectContext copied = (TurnEffectContext)Invoke(typeof(TurnEffectContext), "Copy", context);
            Hit(copy, C(5, 5), copied); Check(copy.Obstacles[0].Charge == 1 && copied.HasCharged(0), "충전 이력 사본 보존");
            context = Context(); Hit(state, C(1, 4), context);
            Check(GeneratorRules.ActiveConnections(state).Count == 2 && state.Cells.Count(c => c.ObstacleIndex == 0) == 4 && state.Obstacles[0].Charge == 0, "직접 파괴는 해당 연결만 해제");
            foreach (RuntimeCell cell in state.Cells.Where(c => c.ObstacleIndex == 1)) Set(cell, "DustDurability", 2);
            string neighbor = Snapshot(state.CellAt(C(3, 8)));
            Set(state.Obstacles[0], "Charge", 2); Hit(state, C(4, 4), context);
            Check(state.Missions.All(m => m.Remaining == 0) && !state.Cells.Any(c => c.ObstacleIndex.HasValue), "남은 두 연결 대상 완충 제거/미션 합계");
            Check(state.CellAt(C(4, 8)).DustDurability == 2 && Snapshot(state.CellAt(C(3, 8))) == neighbor, "간접 제거 먼지/주변 블록 보존");
            Hit(state, C(4, 8), context); Check(state.Missions[0].Progress == 1, "파괴 후 대기 타격 미션 중복 없음");
            UnityEngine.Object.DestroyImmediate(level);

            foreach (PowerArea area in new[] { PowerArea.Point, PowerArea.Blast3 })
            {
                LevelDefinition targetLevel = Make(ObstacleKind.Appliance, 3); LevelRuntimeState targetState = Build(targetLevel); TurnEffectContext turn = Context();
                DroneTargetManager manager = Manager(targetState, turn); string before = Snapshot(targetState);
                DroneTarget generator = manager.QueryArea(area).First(t => t.ObstacleIndex == 0);
                Check(generator.IsMission && generator.Contributions.All(c => c.ExpectedComplete == 0) && Snapshot(targetState) == before, "드론 미완충 후보/무변경 " + area);
                Dictionary<int, DroneTarget> reservations = (Dictionary<int, DroneTarget>)typeof(DroneTargetManager).GetField("reservations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                reservations.Add(1, generator);
                Check(manager.ExpectedComplete == 0 && manager.ExpectedDamage == 0 && manager.ExpectedCharge == 1 && manager.QueryArea(area).All(t => t.ObstacleIndex != 0), "발전기 전체 단일 예약/미완충 예상 " + area);
                Check(manager.Query().Any(t => t.ObstacleIndex == 1), "미완충 예약은 연결 대상 직접 타격 허용 " + area);
                reservations.Clear(); Set(targetState.Obstacles[0], "Charge", 2); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
                generator = manager.QueryArea(area).First(t => t.ObstacleIndex == 0); reservations.Add(2, generator);
                Check(manager.ExpectedComplete == 1 && manager.QueryArea(area).All(t => t.ObstacleIndex != 0 && t.ObstacleIndex != 1), "완충 예약의 연결 본체 제거 예측 " + area);
                reservations.Clear(); Hit(targetState, C(4, 4), turn); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
                Check(manager.Query().All(t => t.Content == RuntimeContent.Normal) && manager.ReservationCount == 0, "목표 모두 해결 후 일반 후보 " + area);
                UnityEngine.Object.DestroyImmediate(targetLevel);
            }

            LevelDefinition limitLevel = Make(ObstacleKind.Crate, 3); LevelRuntimeState limited = Build(limitLevel); TurnEffectContext limitContext = Context();
            Hit(limited, C(4, 4), limitContext);
            Check(Manager(limited, limitContext).Query().All(t => t.ObstacleIndex != 0), "이번 수 충전 발전기 후보 제외");
            Check(Manager(limited, Next(limitContext, 2)).Query().Any(t => t.ObstacleIndex == 0), "다음 수 발전기 후보 복구");
            UnityEngine.Object.DestroyImmediate(limitLevel);
            AdditionalChecks();
        }
    }
}
