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
    /// <summary>ID 배치가 선택 정의를 실제 타격·미션·드론 조회까지 전달하는지 검사한다.</summary>
    public static class ElementLevelMigrationVerification
    {
        private const string Evidence = "Logs/ElementFramework/Phase03/migration-results.txt";
        private static readonly List<string> Results = new List<string>();
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static TurnEffectContext Context() => (TurnEffectContext)Activator.CreateInstance(typeof(TurnEffectContext),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 1, Array.Empty<MatchedBlockChange>() }, null);

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Directory.CreateDirectory(Path.GetDirectoryName(Evidence)); Results.Clear(); int exit = 0;
            LevelDefinition level = null;
            ElementCatalogAsset authoringCatalog = null;
            ElementDefinitionAsset woodAsset = null, metalAsset = null;
            try
            {
                Type placement = typeof(LevelDefinition).Assembly.GetType("Levels.ElementPlacementDefinition");
                Type migration = typeof(ElementLevelMigrationVerification).Assembly.GetType("Elements.Editor.LevelElementMigration");
                MethodInfo build = typeof(LevelStateBuilder).GetMethod("Build", new[] { typeof(LevelDefinition), typeof(int), typeof(ElementCatalog) });
                Check(placement != null && migration != null && build != null, "ID 배치·읽기 전용 변환·카탈로그 실행 계약 존재");
                level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                string before = JsonUtility.ToJson(level);
                object preview = migration.GetMethod("Preview").Invoke(null, new object[] { level });
                Check(preview != null && JsonUtility.ToJson(level) == before && level.SchemaVersion == 4, "구형 미리보기는 원본을 변경하지 않음");
                MethodInfo select = migration.GetMethod("Apply", new[] { typeof(LevelDefinition) });
                Check(select != null, "선택 적용은 미리보기와 별도 명령");
                select.Invoke(null, new object[] { level });
                Check(level.SchemaVersion == 5 && level.Elements.Count == ((Array)preview).Length,
                    "선택한 레벨에만 ID 목록 적용");
                string converted = JsonUtility.ToJson(level);
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Check(JsonUtility.ToJson(level) == before, "선택 변환 Undo 원문 복원");
                Undo.PerformRedo(); Check(JsonUtility.ToJson(level) == converted, "선택 변환 Redo 정확한 복원");
                Undo.PerformUndo(); Undo.ClearUndo(level);
                JsonUtility.FromJsonOverwrite(converted, level);
                Check(LevelStateBuilder.Build(level, 9981).IsBuilt,
                    "선택 변환한 기본 정의 레벨은 일반 플레이 구성으로 실행");
                Check(LevelDefinitionValidator.Validate(level).Count == 0,
                    "일반 검사도 신형 목록과 기본 정의를 사용");
                JsonUtility.FromJsonOverwrite(before, level);

                ElementDefinition wood = Define("obstacle.fixture.wood", 7, true);
                ElementDefinition metal = Define("obstacle.fixture.metal", 13, false);
                ElementCatalog catalog = new ElementCatalog(new[] { wood, metal });
                JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"elements\":[" +
                    "{\"definitionId\":\"obstacle.fixture.wood\",\"instanceId\":\"wood-1\",\"layer\":1,\"coordinate\":{\"row\":2,\"column\":2},\"durability\":7}," +
                    "{\"definitionId\":\"obstacle.fixture.metal\",\"instanceId\":\"metal-1\",\"layer\":1,\"coordinate\":{\"row\":2,\"column\":4},\"durability\":13}]," +
                    "\"initialBlocks\":[],\"obstacles\":[{\"id\":\"ignored-legacy\",\"coordinate\":{\"row\":3,\"column\":3},\"kind\":0,\"durability\":1}]," +
                    "\"covers\":[],\"dust\":[],\"missions\":[{\"kind\":1,\"count\":2}]}", level);
                string input = JsonUtility.ToJson(level);
                LevelStateBuildResult result = (LevelStateBuildResult)build.Invoke(null, new object[] { level, 9981, catalog });
                Check(result.IsBuilt, "v5 신규 ID 최대 내구도7/13으로 구성 " + string.Join(";", result.Issues.Select(issue => issue.Message)));
                LevelRuntimeState state = result.State;
                Check(state.Obstacles.Count == 2 && state.Obstacles[0].Definition.Id == "wood-1" && state.Obstacles[1].Definition.Id == "metal-1" &&
                    state.Obstacles[0].Durability == 7 && state.Obstacles[1].Durability == 13, "구형 목록 중복 없이 정의 ID와 본체 ID 분리");
                PropertyInfo element = typeof(RuntimeObstacle).GetProperty("Element");
                Check(element != null && ReferenceEquals(element.GetValue(state.Obstacles[0]), wood) &&
                    ReferenceEquals(element.GetValue(state.Obstacles[1]), metal), "실행 본체가 카탈로그의 선택 정의를 보유");
                BoardCoordinate first = new BoardCoordinate(2, 2), second = new BoardCoordinate(2, 4);
                Check(DamageReaction.Evaluate(state, first, DamageCause.Power, first, Context()).Response == DamageResponse.Damage &&
                    DamageReaction.Evaluate(state, second, DamageCause.Power, second, Context()).Response == DamageResponse.None,
                    "동일 행동 두 ID의 서로 다른 파워 허용 정책 사용");
                DroneTargetManager drones = (DroneTargetManager)Activator.CreateInstance(typeof(DroneTargetManager),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { state, Context() }, null);
                Check(drones.Query().Any(target => target.Coordinate.Equals(first) && target.IsMission) &&
                    !drones.Query().Any(target => target.Coordinate.Equals(second)), "신규 정의의 미션/피해 정책으로 드론 후보 조회");
                Type damage = typeof(LevelRuntimeState).Assembly.GetType("Simulation.ObstacleDamageRules");
                MethodInfo apply = damage.GetMethod("ApplyReaction", BindingFlags.Static | BindingFlags.NonPublic);
                for (int i = 0; i < 7; i++) apply.Invoke(null, new object[] { state, state.CellAt(first), Context(), i + 1 });
                Check(state.Obstacles[0].Durability == 0 && state.Missions[0].Progress == 1 && state.Obstacles[1].Durability == 13,
                    "신규 ID의 실제 내구도 제거와 미션 증가");
                Check(JsonUtility.ToJson(level) == input, "구성/타격/드론 조회는 제작 원본 불변");
                woodAsset = ScriptableObject.CreateInstance<ElementDefinitionAsset>();
                metalAsset = ScriptableObject.CreateInstance<ElementDefinitionAsset>();
                typeof(ElementDefinitionAsset).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(woodAsset, PackedElementDefinition.FromDefinition(wood));
                typeof(ElementDefinitionAsset).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(metalAsset, PackedElementDefinition.FromDefinition(metal));
                authoringCatalog = ScriptableObject.CreateInstance<ElementCatalogAsset>();
                SerializedObject catalogObject = new SerializedObject(authoringCatalog);
                SerializedProperty definitions = catalogObject.FindProperty("definitions"); definitions.arraySize = 2;
                definitions.GetArrayElementAtIndex(0).objectReferenceValue = woodAsset;
                definitions.GetArrayElementAtIndex(1).objectReferenceValue = metalAsset;
                catalogObject.ApplyModifiedPropertiesWithoutUndo();
                SerializedObject levelObject = new SerializedObject(level);
                levelObject.FindProperty("elementCatalog").objectReferenceValue = authoringCatalog;
                levelObject.ApplyModifiedPropertiesWithoutUndo();
                string withCatalog = JsonUtility.ToJson(level);
                LevelStateBuildResult standard = LevelStateBuilder.Build(level, 9981);
                Check(standard.IsBuilt && standard.State.Obstacles[0].Element.Id.Equals(wood.Id) &&
                    standard.State.Obstacles[1].Element.Id.Equals(metal.Id) && LevelDefinitionValidator.Validate(level).Count == 0,
                    "연결된 제작 카탈로그를 일반 구성과 검사에 사용");
                Check(JsonUtility.ToJson(level) == withCatalog, "일반 진입도 제작 카탈로그/레벨 원본 불변");
                definitions.GetArrayElementAtIndex(1).objectReferenceValue = woodAsset;
                catalogObject.ApplyModifiedPropertiesWithoutUndo();
                Check(!LevelStateBuilder.Build(level, 9981).IsBuilt &&
                    LevelDefinitionValidator.Validate(level).Any(issue => issue.Message.Contains(wood.Id.Value)),
                    "일반 진입의 중복 제작 ID는 예외 탈출 없이 오류로 보고");
                Check(standard.State.Obstacles[1].Element.Id.Equals(metal.Id) && standard.State.Obstacles[1].Durability == 13,
                    "제작 카탈로그 수정은 이미 구성한 실행 상태에 영향 없음");
                levelObject.Update(); levelObject.FindProperty("elementCatalog").objectReferenceValue = null;
                levelObject.ApplyModifiedPropertiesWithoutUndo();
                JsonUtility.FromJsonOverwrite("{\"elements\":[{\"definitionId\":\"obstacle.missing\",\"instanceId\":\"missing-1\",\"layer\":1,\"coordinate\":{\"row\":2,\"column\":2},\"durability\":1}]}", level);
                LevelStateBuildResult missing = (LevelStateBuildResult)build.Invoke(null, new object[] { level, 9981, catalog });
                Check(!missing.IsBuilt && missing.Issues.Any(issue => issue.Message.Contains("obstacle.missing")), "누락 ID는 일반 블록 대체 없이 오류");
                Check(!LevelStateBuilder.Build(level, 9981).IsBuilt &&
                    LevelDefinitionValidator.Validate(level).Any(issue => issue.Message.Contains("obstacle.missing")),
                    "일반 구성과 검사도 미등록 ID를 명시적으로 거절");
                JsonUtility.FromJsonOverwrite("{\"elements\":[],\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", level);
                LevelStateBuildResult empty = (LevelStateBuildResult)build.Invoke(null, new object[] { level, 9981, catalog });
                Check(empty.IsBuilt && empty.State.Obstacles.Count == 0, "빈 신형 목록이 구형 목록을 되살리지 않음 " +
                    string.Join(";", empty.Issues.Select(issue => issue.Message)));
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                if (level != null) UnityEngine.Object.DestroyImmediate(level);
                if (authoringCatalog != null) UnityEngine.Object.DestroyImmediate(authoringCatalog);
                if (woodAsset != null) UnityEngine.Object.DestroyImmediate(woodAsset);
                if (metalAsset != null) UnityEngine.Object.DestroyImmediate(metalAsset);
                File.WriteAllLines(Evidence, Results);
            }
            EditorApplication.Exit(exit);
        }

        private static ElementDefinition Define(string id, int maximum, bool power) =>
            new ElementDefinition(new ElementId(id), "검사 상자", new ElementPlacementProfile(1, maximum), null,
                new ElementDamageSourcePolicy(true, power, false, true), null, new ElementDamageAggregationPolicy(false),
                new ElementRemovalMissionProfile(MissionKind.Crate), ElementReactionBehavior.Durability);
    }
}
