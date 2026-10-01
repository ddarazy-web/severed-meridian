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
    public static partial class FixedObstacleVerification
    {
        private const string Evidence = "Logs/FixedObstacleVerification";
        private static readonly List<string> Results = new List<string>();
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static object Invoke(Type type, string method, object owner, params object[] args) =>
            type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).Invoke(owner, args);
        private static void Set(object owner, string property, object value) => owner.GetType().GetProperty(property).GetSetMethod(true).Invoke(owner, new[] { value });
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", null, value);
        private static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static LevelDefinition Make() => (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make", null);
        private static TurnEffectContext Context() => (TurnEffectContext)Activator.CreateInstance(typeof(TurnEffectContext), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { 1, Array.Empty<MatchedBlockChange>() }, null);
        private static LevelRuntimeState Build(LevelDefinition level, int seed = 12345)
        {
            LevelStateBuildResult result = LevelStateBuilder.Build(level, seed);
            if (!result.IsBuilt) throw new InvalidOperationException(string.Join(" | ", result.Issues));
            return result.State;
        }
        private static void Place(LevelDefinition level, BoardCoordinate cell, InitialBlockKind kind, RocketDirection direction = RocketDirection.Horizontal, RabbitColor color = RabbitColor.Type1)
            => Invoke(typeof(PowerEffectVerification), "Place", null, level, cell, kind, direction, color);
        private static void Obstacle(LevelDefinition level, ObstacleKind kind, int durability, BoardCoordinate origin, RabbitColor color = RabbitColor.Type1)
        {
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, LevelPlacementRules.Footprint(origin, LevelPlacementRules.Size(kind)));
            PlacementEditResult result = LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)kind, Durability = durability, Color = color }, new[] { origin });
            if (result.Changed != 1) throw new InvalidOperationException(result.ToString());
        }
        private static List<EffectRecord> Hit(LevelRuntimeState state, BoardCoordinate target, TurnEffectContext context)
            => (List<EffectRecord>)Invoke(typeof(LayerVerification), "Hit", null, state, target, context);
        private static MissionKind Mission(ObstacleKind kind) => kind == ObstacleKind.Safe ? MissionKind.Safe : kind == ObstacleKind.ColorLock ? MissionKind.ColorLock : MissionKind.Appliance;
        private static void SetMission(LevelDefinition level, ObstacleKind kind) => JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)Mission(kind) + ",\"count\":1}]}", level);
        private static void Finish(BoardActionExecutor executor)
        {
            int steps = 0;
            while (executor.HasPendingCascade && steps++ < 500)
                if (!executor.AdvanceCascade().IsApplied) throw new InvalidOperationException(executor.LastCascadeStep?.Message);
            if (executor.HasPendingCascade) throw new InvalidOperationException("연쇄 한도");
        }
        public static void Data()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { DataChecks(); File.WriteAllLines(Evidence + "/data-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/data-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        public static void FinalData()
        {
            foreach (var suite in new[] { ("data", (Action)DataChecks), ("supplemental", (Action)(() => { MatchChecks(); MagnetChecks(); DroneChecks(); })), ("edge", (Action)EdgeChecks) })
            {
                Results.Clear();
                try { suite.Item2(); File.WriteAllLines(Evidence + "/" + suite.Item1 + "-results.txt", Results); }
                catch (Exception error)
                { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/" + suite.Item1 + "-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); return; }
            }
            EditorApplication.Exit(0);
        }
        private static void DataChecks()
        {
            foreach (ObstacleKind kind in new[] { ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance })
                for (int durability = 1; durability <= LevelPlacementRules.MaxDurability(kind); durability++)
                {
                    LevelDefinition level = Make(); Obstacle(level, kind, durability, C(4, 4)); SetMission(level, kind);
                    LevelRuntimeState state = Build(level); TurnEffectContext context = Context(); string before = Snapshot(state);
                    Check(!ActionQuery.Swap(state, C(4, 4), C(4, 3)).IsAllowed && !MovementQuery.Find(state).Any(m => m.Source.Equals(C(4, 4)) && m.IsAllowed), "고정 교환/이동 금지 " + kind + durability);
                    Check(MissionProgressRules.Query(state, C(4, 4), context).Single().Damage == 1 && Snapshot(state) == before, "피해/미션 조회 무변경 " + kind + durability);
                    foreach (RuntimeCell cell in state.Cells.Where(c => c.ObstacleIndex == 0)) Set(cell, "DustDurability", 2);
                    Hit(state, C(4, 4), context);
                    Check(state.Obstacles[0].Durability == durability - 1 && state.Missions[0].Progress == (durability == 1 ? 1 : 0), "직접 피해/본체 미션 " + kind + durability);
                    Check(state.Cells.Count(c => c.ObstacleIndex == 0) == (durability == 1 ? 0 : kind == ObstacleKind.Appliance ? 4 : 1) && state.CellAt(C(4, 4)).DustDurability == 2, "동시 점유/전체 제거/먼지 보존 " + kind + durability);
                    if (durability > 1)
                    {
                        Hit(state, C(4, 4), context);
                        Check(state.Obstacles[0].Durability == durability - (kind == ObstacleKind.Appliance ? 2 : 1), "턴 제한/폐가전 별도 반복 " + kind + durability);
                        if (kind != ObstacleKind.Appliance)
                        { Hit(state, C(4, 4), (TurnEffectContext)Invoke(typeof(TurnEffectContext), "NextTurn", context, 2)); Check(state.Obstacles[0].Durability == durability - 2, "다음 수 재피해 " + kind + durability); }
                    }
                    Check(SettlementResolution.Resolve(state).IsApplied, "정착 지원 " + kind + durability);
                }
            foreach (RabbitColor color in Enum.GetValues(typeof(RabbitColor)))
            {
                LevelDefinition level = Make(); Obstacle(level, ObstacleKind.ColorLock, 3, C(4, 4), color); LevelRuntimeState state = Build(level);
                Check(DamageReaction.Evaluate(state, C(4, 4), DamageCause.AdjacentMatch, C(4, 3), Context(), color).Response == DamageResponse.Damage &&
                    DamageReaction.Evaluate(state, C(4, 4), DamageCause.AdjacentMatch, C(4, 3), Context(), (RabbitColor)(((int)color + 1) % 5)).Response == DamageResponse.None, "자물쇠 원래 색 일치/불일치 " + color);
            }
            LevelDefinition safeLevel = Make(); Obstacle(safeLevel, ObstacleKind.Safe, 5, C(4, 4));
            Check(DamageReaction.Evaluate(Build(safeLevel), C(4, 4), DamageCause.AdjacentMatch, C(4, 3), Context()).Response == DamageResponse.None, "금고 일반 인접 무피해");
            PowerChecks();
        }
        private static void PowerChecks()
        {
            LevelDefinition level = Make(); Obstacle(level, ObstacleKind.Appliance, 9, C(4, 4)); SetMission(level, ObstacleKind.Appliance);
            Place(level, C(4, 0), InitialBlockKind.Rocket); Place(level, C(6, 4), InitialBlockKind.Bomb);
            BoardActionExecutor executor = new BoardActionExecutor(Build(level)); BoardActionResult rocket = executor.Activate(C(4, 0));
            Check(rocket.IsApplied && executor.State.Obstacles[0].Durability == 7 && executor.State.CellAt(C(4, 8)).Content == RuntimeContent.Empty, "로켓 두 칸2피해/끝까지 관통");
            Hit(executor.State, C(6, 4), executor.TurnEffects);
            Check(executor.State.Obstacles[0].Durability == 5, "별도 폭탄 두 칸 반복 피해");
            foreach (int durability in new[] { 1, 3, 9 })
            {
                LevelDefinition combo = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, 9, RocketDirection.Horizontal, null);
                Obstacle(combo, ObstacleKind.Appliance, durability, C(7, 7)); SetMission(combo, ObstacleKind.Appliance);
                BoardActionExecutor both = new BoardActionExecutor(Build(combo)); BoardActionResult result = both.Swap(C(4, 4), C(4, 5));
                Check(result.IsApplied && both.State.Obstacles[0].Durability == Math.Max(0, durability - 4) && both.State.Missions[0].Progress == (durability <= 4 ? 1 : 0), "자석자석4피해/과잉/본체미션 " + durability);
            }
            for (int pair = 0; pair < 10; pair++)
            {
                LevelDefinition combo = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, pair, RocketDirection.Horizontal, null);
                Obstacle(combo, ObstacleKind.Safe, 3, C(3, 3)); Obstacle(combo, ObstacleKind.ColorLock, 2, C(6, 6)); Obstacle(combo, ObstacleKind.Appliance, 9, C(7, 7));
                BoardActionExecutor both = new BoardActionExecutor(Build(combo)); Check(both.Swap(C(4, 4), C(4, 5)).IsApplied, "혼합 본체 파워10조합 " + pair); Finish(both);
                Check(both.State.Obstacles.All(b => b.Durability >= 0), "혼합 본체 연쇄/정착 " + pair);
            }
        }
    }
}
