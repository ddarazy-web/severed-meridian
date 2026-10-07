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
    /// <summary>선택 정의의 단자 크기·충전량과 본체 ID 연결을 실제 실행으로 검사한다.</summary>
    public static class ElementConnectionExecutionVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static TurnEffectContext Context() => (TurnEffectContext)Activator.CreateInstance(typeof(TurnEffectContext),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 1, Array.Empty<MatchedBlockChange>() }, null);
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0; LevelDefinition level = null;
            try
            {
                ElementDefinition generator = new ElementDefinition(new ElementId("device.fixture.generator"), "확장 발전기", null,
                    new ElementChargePlacementProfile(2, 2, 4, 2), new ElementDamageSourcePolicy(true, true, true, true),
                    null, null, null, ElementReactionBehavior.GeneratorCharge);
                ElementDefinition target = new ElementDefinition(new ElementId("obstacle.fixture.large-crate"), "확장 큰 상자",
                    new ElementPlacementProfile(2, 9), null, new ElementDamageSourcePolicy(true, true, false, true), null,
                    new ElementDamageAggregationPolicy(true), new ElementRemovalMissionProfile(MissionKind.Crate), ElementReactionBehavior.Durability);
                ElementCatalog catalog = new ElementCatalog(new[] { generator, target });
                level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"initialBlocks\":[],\"obstacles\":[],\"covers\":[],\"dust\":[],\"elements\":[" +
                    "{\"definitionId\":\"device.fixture.generator\",\"instanceId\":\"generator-body\",\"layer\":1,\"coordinate\":{\"row\":2,\"column\":2},\"requiredCharge\":2}," +
                    "{\"definitionId\":\"obstacle.fixture.large-crate\",\"instanceId\":\"crate-body\",\"layer\":1,\"coordinate\":{\"row\":2,\"column\":5},\"durability\":5}]," +
                    "\"connections\":[{\"generatorId\":\"generator-body\",\"targetId\":\"crate-body\",\"vertices\":[" +
                    "{\"row\":2,\"column\":4},{\"row\":1,\"column\":4},{\"row\":1,\"column\":5},{\"row\":1,\"column\":6},{\"row\":1,\"column\":7},{\"row\":2,\"column\":7}]}]," +
                    "\"missions\":[{\"kind\":1,\"count\":1}]}", level);
                string original = JsonUtility.ToJson(level);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 910, catalog);
                Check(built.IsBuilt, "신규2×2 상자 외곽 단자 연결 허용 " + string.Join(";", built.Issues));
                LevelRuntimeState state = built.State;
                Check(state.Cells.Count(cell => cell.ObstacleIndex == 0) == 4 && state.Cells.Count(cell => cell.ObstacleIndex == 1) == 4 &&
                    GeneratorRules.ActiveConnections(state).Single().TargetId == "crate-body", "정의 ID와 별개의 연결 본체 ID/점유 보존");
                BoardCoordinate origin = new BoardCoordinate(2, 2);
                Check(DamageReaction.Evaluate(state, origin, DamageCause.Power, origin, Context()).Amount == 2, "선택 발전기의 타격당 충전2");
                DroneTargetManager drones = (DroneTargetManager)Activator.CreateInstance(typeof(DroneTargetManager), BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { state, Context() }, null);
                Check(drones.Query().Any(item => item.Coordinate.Equals(origin) && item.Contributions.Any(c => c.ExpectedComplete == 1 && c.Charge == 2)),
                    "드론이 신규 발전기 충전량과 연결 미션을 예측");
                typeof(GeneratorRules).GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { state, 0, Context() });
                Check(state.Obstacles[0].Charge == 2 && state.Obstacles[1].Durability == 0 && state.Missions[0].Progress == 1 &&
                    GeneratorRules.ActiveConnections(state).Count == 0, "실제 충전2로 연결 대상 철거/미션 증가");
                Check(JsonUtility.ToJson(level) == original, "연결/충전/드론 실행에서 제작 원본 불변");
                JsonUtility.FromJsonOverwrite("{\"connections\":[{\"generatorId\":\"generator-body\",\"targetId\":\"unknown-body\",\"vertices\":[]}]}", level);
                Check(!LevelStateBuilder.Build(level, 910, catalog).IsBuilt, "누락 연결 본체 참조를 시작 전에 거절");
                JsonUtility.FromJsonOverwrite(original, level);
                JsonUtility.FromJsonOverwrite("{\"connections\":[{\"generatorId\":\"generator-body\",\"targetId\":\"crate-body\",\"vertices\":[" +
                    "{\"row\":2,\"column\":4},{\"row\":2,\"column\":5},{\"row\":3,\"column\":5},{\"row\":3,\"column\":6},{\"row\":3,\"column\":7}]}]}", level);
                Check(!LevelStateBuilder.Build(level, 910, catalog).IsBuilt, "신규2×2 본체 내부로 들어가는 전선 거절");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                if (level != null) UnityEngine.Object.DestroyImmediate(level);
                File.WriteAllLines("Logs/ElementFramework/Phase03/connection-execution-results.txt", Results);
            }
            EditorApplication.Exit(exit);
        }
    }
}
