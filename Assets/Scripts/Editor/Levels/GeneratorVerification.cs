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
    public static partial class GeneratorVerification
    {
        private const string Evidence = "Logs/GeneratorVerification";
        private static readonly List<string> Results = new List<string>();
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static object Invoke(Type type, string method, object owner, params object[] args) =>
            type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).Invoke(owner, args);
        private static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static string Snapshot(object state) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", null, state);
        private static TurnEffectContext Context() => (TurnEffectContext)Invoke(typeof(FixedObstacleVerification), "Context", null);
        private static TurnEffectContext Next(TurnEffectContext context, int turn) => (TurnEffectContext)Invoke(typeof(TurnEffectContext), "NextTurn", context, turn);
        private static List<EffectRecord> Hit(LevelRuntimeState state, BoardCoordinate cell, TurnEffectContext context) =>
            (List<EffectRecord>)Invoke(typeof(LayerVerification), "Hit", null, state, cell, context);
        private static LevelDefinition Make(ObstacleKind kind, int charge)
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make", null);
            foreach (BoardCoordinate origin in new[] { C(4, 4), C(4, 8) })
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, LevelPlacementRules.Footprint(origin, 2));
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Generator, RequiredCharge = charge }, new[] { C(4, 4) });
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)kind, Durability = LevelPlacementRules.MaxDurability(kind), Color = RabbitColor.Type1 }, new[] { C(4, 8) });
            string error = LevelConnectionEditing.Add(level, level.Obstacles[0].Id, level.Obstacles[1].Id);
            if (error != null) throw new InvalidOperationException(error);
            error = LevelConnectionEditing.SetWire(level, 0, new[] { C(4, 6), C(4, 7), C(4, 8) });
            if (error != null) throw new InvalidOperationException(error);
            MissionKind mission = kind == ObstacleKind.Crate ? MissionKind.Crate : kind == ObstacleKind.Safe ? MissionKind.Safe : kind == ObstacleKind.ColorLock ? MissionKind.ColorLock : MissionKind.Appliance;
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)mission + ",\"count\":1}]}", level);
            return level;
        }
        private static LevelRuntimeState Build(LevelDefinition level)
        {
            LevelStateBuildResult result = LevelStateBuilder.Build(level, 12345);
            if (!result.IsBuilt) throw new InvalidOperationException(string.Join(" | ", result.Issues));
            return result.State;
        }
        public static void Data()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { DataChecks(); File.WriteAllLines(Evidence + "/data-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/data-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void DataChecks()
        {
            foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance })
                for (int required = 3; required <= 5; required++)
                {
                    LevelDefinition level = Make(kind, required); LevelRuntimeState state = Build(level); TurnEffectContext context = Context();
                    Check(GeneratorRules.ActiveConnections(state).Count == 1 && state.Obstacles[0].Charge == 0, "초기 활성 연결/충전 " + kind + required);
                    string before = Snapshot(state);
                    Check(MissionProgressRules.Query(state, C(4, 4), context).Single().ExpectedComplete == 0 && Snapshot(state) == before, "미완충 간접 기여/조회 무변경 " + kind + required);
                    Check(DamageReaction.Evaluate(state, C(4, 4), DamageCause.AdjacentMatch, C(4, 3), context).Response == DamageResponse.Charge &&
                        DamageReaction.Evaluate(state, C(4, 4), DamageCause.MagnetAdjacent, C(4, 3), context).Response == DamageResponse.None, "일반 인접/자석 인접 구분 " + kind + required);
                    for (int turn = 1; turn <= required; turn++)
                    {
                        context = Next(context, turn);
                        List<EffectRecord> records = Hit(state, C(4, 4), context);
                        Check(records.Single().ChargeBefore == turn - 1 && records.Single().ChargeAfter == turn, "충전 전후 기록 " + kind + required + "/" + turn);
                        foreach (BoardCoordinate cell in new[] { C(4, 4), C(4, 5), C(5, 4), C(5, 5) }) Hit(state, cell, context);
                        Check(state.Obstacles[0].Charge == turn, "본체 네 칸/반복 타격 최대1충전 " + kind + required + "/" + turn);
                        if (turn < required) Check(state.Missions[0].Progress == 0 && state.Cells.Count(c => c.ObstacleIndex == 0) == 4, "미완충 본체 유지 " + kind + turn);
                    }
                    Check(state.Missions[0].Progress == 1 && !state.Cells.Any(c => c.ObstacleIndex.HasValue) && GeneratorRules.ActiveConnections(state).Count == 0, "완충 전체 제거/미션1/연결 해제 " + kind + required);
                    Check(context.Generators.Count(g => g.Event == GeneratorEvent.Activated) == 1 && context.Generators.All(g => g.Event != GeneratorEvent.Retired), "완충 단일 작동 " + kind + required);
                    Check(SettlementResolution.Resolve(state).IsApplied, "제거 후 정착 " + kind + required);
                    UnityEngine.Object.DestroyImmediate(level);
                }
            LevelDefinition direct = Make(ObstacleKind.Appliance, 3); LevelRuntimeState work = Build(direct); TurnEffectContext turnContext = Context();
            for (int i = 0; i < 9; i++) Hit(work, C(4, 8), turnContext);
            Check(work.Obstacles[0].Charge == 0 && !work.Cells.Any(c => c.ObstacleIndex.HasValue) && work.Missions[0].Progress == 1, "마지막 대상 직접 파괴 자동 철거/무충전");
            Check(turnContext.Generators.Count(g => g.Event == GeneratorEvent.Retired) == 1, "자동 철거 단일 기록");
            UnityEngine.Object.DestroyImmediate(direct);
            EdgeChecks();
        }
    }
}
