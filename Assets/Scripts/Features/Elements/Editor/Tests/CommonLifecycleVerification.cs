using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    /// <summary>번식·충전·연결 수명주기의 기존 결과와 정의 소비를 검사한다.</summary>
    public static class CommonLifecycleVerification
    {
        private const string Evidence = "Logs/ElementFramework/Phase01/Lifecycle";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Values = new List<string>();
        private static object Invoke(Type type, string name, params object[] args) =>
            type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static void Check(bool pass, string name) => Results.Add((pass ? "PASS " : "FAIL ") + name);
        private static IDictionary Definitions() => (IDictionary)typeof(ElementCatalog).GetField("definitions", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(typeof(LegacyElementDefinitions).GetField("Catalog", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null));
        public static void Before() => Execute(false);
        public static void Run() => Execute(true);
        private static void Execute(bool connected)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear(); Values.Clear();
            try
            {
                foreach (Type suite in new[] { typeof(MoldVerification), typeof(GeneratorVerification) })
                {
                    List<string> results = (List<string>)suite.GetField("Results", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                    results.Clear(); Invoke(suite, "DataChecks"); Results.AddRange(results);
                }
                Capture(connected);
                if (!connected) File.WriteAllLines(Evidence + "/baseline-values.txt", Values);
                else
                {
                    Check(File.ReadAllLines(Evidence + "/baseline-values.txt").SequenceEqual(Values), "번식/발전기 실제 상태·문맥·효과 전후 동일");
                    ConnectionChecks();
                    GrowthChecks();
                }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); }
            string mode = connected ? "after" : "before";
            File.WriteAllLines(Evidence + "/" + mode + "-results.txt", Results);
            File.WriteAllLines(Evidence + "/" + mode + "-values.txt", Values);
            EditorApplication.Exit(Results.Any(value => value.StartsWith("FAIL ")) ? 1 : 0);
        }
        private static LevelDefinition Input(string name, bool connected, Func<LevelDefinition> make)
        {
            string path = Evidence + "/input-" + name + ".json";
            if (!connected)
            {
                LevelDefinition level = make(); File.WriteAllText(path, JsonUtility.ToJson(level)); return level;
            }
            LevelDefinition copy = ScriptableObject.CreateInstance<LevelDefinition>();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(path), copy); return copy;
        }
        private static void Capture(bool connected)
        {
            foreach (int seed in new[] { 1, 42, 12345 })
            {
                LevelDefinition level = Input("mold-" + seed, connected, () =>
                {
                    LevelDefinition created = (LevelDefinition)Invoke(typeof(MoldVerification), "Make");
                    Invoke(typeof(MoldVerification), "Mold", created, new[] { new BoardCoordinate(4, 4), new BoardCoordinate(4, 5) }); return created;
                });
                try
                {
                    LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(MoldVerification), "Build", level, seed);
                    TurnEffectContext context = (TurnEffectContext)Invoke(typeof(MoldVerification), "Context", 1);
                    object spread = Invoke(typeof(MoldVerification), "End", state, context);
                    Values.Add("mold:" + seed + ":" + Snapshot(state) + ":" + Snapshot(context) + ":" + Snapshot(spread));
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
            foreach (ObstacleKind kind in new[] { ObstacleKind.Crate, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance })
            {
                LevelDefinition level = Input("generator-" + kind, connected, () => (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", kind, 3));
                try
                {
                    LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(GeneratorVerification), "Build", level);
                    for (int turn = 0; turn < 3; turn++)
                    {
                        TurnEffectContext context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Context");
                        object effects = Invoke(typeof(GeneratorVerification), "Hit", state, new BoardCoordinate(4, 4), context);
                        Values.Add("generator:" + kind + ":" + turn + ":" + Snapshot(state) + ":" + Snapshot(context) + ":" + Snapshot(effects));
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
        }
        private static void ConnectionChecks()
        {
            PropertyInfo turn = typeof(ElementDefinition).GetProperty("Turn");
            Check(turn?.GetValue(LegacyElementDefinitions.Get(CoverKind.Mold)) != null, "곰팡이 명시적 턴 종료 행동 등록");
            ConstructorInfo charge = typeof(ElementChargePlacementProfile).GetConstructor(new[] { typeof(int), typeof(int), typeof(int), typeof(int) });
            Check(charge != null, "발전기 충전량 정의 존재");
            if (charge == null) return;
            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Generator);
            ElementChargePlacementProfile placement = (ElementChargePlacementProfile)charge.Invoke(new object[] { 2, 3, 5, 2 });
            ElementDefinition changed = new ElementDefinition(original.Id, original.DisplayName, null, placement,
                original.DamageSourcePolicy, original.ColorMatchPolicy, original.DamageAggregationPolicy, original.RemovalMissionProfile, original.ReactionBehavior);
            IDictionary definitions = Definitions(); definitions[original.Id] = changed;
            LevelDefinition level = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", ObstacleKind.Crate, 3);
            try
            {
                LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(GeneratorVerification), "Build", level);
                BoardCoordinate target = new BoardCoordinate(4, 4);
                TurnEffectContext context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Context");
                DamageReaction reaction = DamageReaction.Evaluate(state, target, DamageCause.Power, target, context);
                Check(reaction.Amount == 2, "발전기 정의 충전량 실제 조회");
                Check(MissionProgressRules.Query(state, target, context).Any(value => value.Charge == 2), "발전기 정의 충전량 미션 예측");
                Invoke(typeof(GeneratorVerification), "Hit", state, target, context);
                Check(state.Obstacles[0].Charge == 2, "발전기 정의 충전량 실제 적용");
                Invoke(typeof(GeneratorVerification), "Hit", state, target, context);
                Check(state.Obstacles[0].Charge == 2, "발전기 같은 턴 중복 충전 억제");
                context = (TurnEffectContext)Invoke(typeof(GeneratorVerification), "Context");
                Invoke(typeof(GeneratorVerification), "Hit", state, target, context);
                Check(context.Generators.Any(value => value.Event == GeneratorEvent.Activated) && state.Missions[0].Progress == 1,
                    "발전기 정의 충전량 활성화/연결 제거 미션");
            }
            finally { definitions[original.Id] = original; UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void GrowthChecks()
        {
            ElementDefinition original = LegacyElementDefinitions.Get(CoverKind.Mold);
            ElementDefinition changed = new ElementDefinition(original.Id, original.DisplayName, new ElementPlacementProfile(1, 2),
                null, null, null, null, null, null, new ElementLayerProfile(ElementLayerBehavior.CoverRemoval, MissionKind.Dust),
                new ElementTurnProfile(ElementTurnBehavior.AdjacentCoverSpread, 2));
            IDictionary definitions = Definitions(); definitions[original.Id] = changed;
            LevelDefinition level = null;
            try
            {
                level = (LevelDefinition)Invoke(typeof(MoldVerification), "Make");
                Invoke(typeof(MoldVerification), "Mold", level, new[] { new BoardCoordinate(4, 4) });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 1 }, new[] { new BoardCoordinate(2, 2) });
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":100},{\"kind\":8,\"count\":0},{\"kind\":4,\"count\":1}]}", level);
                LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(MoldVerification), "Build", level, 12345);
                TurnEffectContext context = (TurnEffectContext)Invoke(typeof(MoldVerification), "Context", 1);
                MoldSpreadRecord record = (MoldSpreadRecord)Invoke(typeof(MoldVerification), "End", state, context);
                Check(record.Reason == MoldSpreadReason.Spread && state.CellAt(record.Target.Value).CoverDurability == 2, "턴 정의 초기 내구도 실제 생성");
                Check(state.Missions[2].Target == 2, "턴 정의 미션 실제 목표 증가");
                string after = Snapshot(state) + Snapshot(context);
                Check(ReferenceEquals(record, Invoke(typeof(MoldVerification), "End", state, context)) && Snapshot(state) + Snapshot(context) == after,
                    "비기본 번식 동일 턴 재실행 무변경");
            }
            finally { definitions[original.Id] = original; if (level != null) UnityEngine.Object.DestroyImmediate(level); }
        }
    }
}
