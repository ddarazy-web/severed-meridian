using System;
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
    /// <summary>기존 종류가 아닌 검사용 정의가 등록 행동의 실제 판정/적용을 재사용하는지 검사한다.</summary>
    public static class CommonElementExtensionVerification
    {
        private const string Evidence = "Logs/ElementFramework/Phase01/Extension";
        private static readonly List<string> Results = new List<string>();
        private static object Invoke(Type type, string method, params object[] args) =>
            type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        private static void Check(bool pass, string name) => Results.Add((pass ? "PASS " : "FAIL ") + name);
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { Durable(); Supply(); LegacyRemove(); LegacyGeneratorDurability(); }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); }
            File.WriteAllLines(Evidence + "/results.txt", Results);
            EditorApplication.Exit(Results.Any(value => value.StartsWith("FAIL ")) ? 1 : 0);
        }
        private static void Durable()
        {
            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Crate);
            ElementDefinition alternate = new ElementDefinition(new ElementId("test.obstacle.alternate"), "검사용 장애물", original.Placement, null,
                original.DamageSourcePolicy, new ElementColorMatchPolicy(true), original.DamageAggregationPolicy,
                new ElementRemovalMissionProfile(MissionKind.Scrap), ElementReactionBehavior.Durability);
            ElementCatalog catalog = new ElementCatalog(new[] { alternate });
            LevelDefinition level = (LevelDefinition)Invoke(typeof(FixedObstacleVerification), "Make");
            try
            {
                BoardCoordinate target = new BoardCoordinate(4, 4);
                Invoke(typeof(FixedObstacleVerification), "Obstacle", level, ObstacleKind.Crate, 1, target, RabbitColor.Type1);
                Invoke(typeof(FixedObstacleVerification), "Obstacle", level, ObstacleKind.Scrap, 1, new BoardCoordinate(4, 6), RabbitColor.Type1);
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":1,\"count\":1},{\"kind\":3,\"count\":1}]}", level);
                LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
                TurnEffectContext context = (TurnEffectContext)Invoke(typeof(FixedObstacleVerification), "Context");
                Type rules = typeof(DamageReaction).Assembly.GetType("Simulation.ObstacleDamageRules");
                object registry = rules.GetField("reactionBehaviors", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                MethodInfo query = registry.GetType().GetMethod("Query", BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo apply = registry.GetType().GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Instance);
                ElementDefinition selected = catalog.Get(alternate.Id);
                DamageReaction rejected = (DamageReaction)query.Invoke(registry, new object[] { selected, state, state.CellAt(target), DamageCause.AdjacentMatch, RabbitColor.Type2, context, 1 });
                Check(rejected.Response == DamageResponse.None, "다른 ID 내구도 행동 정의 색 조건 실제 적용");
                DamageReaction accepted = (DamageReaction)query.Invoke(registry, new object[] { selected, state, state.CellAt(target), DamageCause.AdjacentMatch, RabbitColor.Type1, context, 1 });
                Check(accepted.Response == DamageResponse.Damage, "다른 ID 내구도 행동 허용 판정");
                apply.Invoke(registry, new object[] { selected, state, state.CellAt(target), context, 1 });
                Check(state.CellAt(target).Content == RuntimeContent.Empty && state.Missions[1].Progress == 1 && state.Missions[0].Progress == 0,
                    "다른 ID 내구도 행동 실제 제거/정의 미션");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }
        private static void Supply()
        {
            ElementDefinition alternate = ElementDefinition.CreateSupply(new ElementId("test.supply.alternate"), "검사용 공급",
                new ElementSupplyProfile(ElementSupplyBehavior.Power, RuntimeContent.Bomb));
            LevelDefinition level = (LevelDefinition)Invoke(typeof(FixedObstacleVerification), "Make");
            try
            {
                LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
                RuntimeCell cell = state.CellAt(new BoardCoordinate(4, 4));
                Type registry = typeof(ElementDefinition).Assembly.GetType("Elements.ElementSupplyBehaviorRegistry");
                Invoke(registry, "Apply", alternate, state, cell, new SupplyItem(SupplyKind.Rocket, direction: RocketDirection.Vertical));
                Check(cell.Content == RuntimeContent.Bomb && !cell.RocketDirection.HasValue && !cell.Color.HasValue,
                    "다른 ID 공급 행동의 정의 생성 종류 실제 적용");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }
        private static void LegacyGeneratorDurability()
        {
            foreach (bool perHitCell in new[] { true, false })
            {
                Invoke(typeof(ElementDurabilityApplyPolicyVerification), "PolicyCase", (bool?)perHitCell, false);
                Check(true, "구형 발전기 내구도 행동 교체/제거 미션 없음 " + perHitCell);
            }
        }
        private static void LegacyRemove()
        {
            foreach (int kind in new[] { -1, 0, 5, 6, 99 })
                foreach (int durability in new[] { -1, 0, 1 })
                {
                    LevelDefinition level = (LevelDefinition)Invoke(typeof(FixedObstacleVerification), "Make");
                    try
                    {
                        BoardCoordinate target = new BoardCoordinate(4, 4);
                        Invoke(typeof(FixedObstacleVerification), "Obstacle", level, ObstacleKind.Crate, 1, target, RabbitColor.Type1);
                        JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":1,\"count\":1}]}", level);
                        LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
                        RuntimeObstacle body = state.Obstacles[0];
                        ObstaclePlacementDefinition changed = (ObstaclePlacementDefinition)Activator.CreateInstance(typeof(ObstaclePlacementDefinition),
                            BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { body.Definition.Id, target, (ObstacleKind)kind, durability, RabbitColor.Type1, 0 }, null);
                        typeof(RuntimeObstacle).GetField("<Definition>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(body, changed);
                        typeof(RuntimeObstacle).GetProperty("Durability").GetSetMethod(true).Invoke(body, new object[] { durability });
                        Exception failure = null;
                        try { Invoke(typeof(DamageReaction).Assembly.GetType("Simulation.ObstacleDamageRules"), "Remove", state, 0); }
                        catch (TargetInvocationException error) { failure = error.InnerException; }
                        Check(failure == null && state.CellAt(target).Content == RuntimeContent.Empty &&
                            body.Durability == (kind == 5 ? durability : 0) && state.Missions[0].Progress == (kind == 5 ? 0 : 1),
                            "구형 직접 제거 미지원 종류/발전기 경계 " + kind + "/" + durability);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
        }
    }
}
