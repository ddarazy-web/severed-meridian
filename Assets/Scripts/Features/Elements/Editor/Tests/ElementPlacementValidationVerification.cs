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
    /// <summary>카탈로그 직접 입력에서도 잘못된 프로필·점유·참조를 시작 전에 거절한다.</summary>
    public static class ElementPlacementValidationVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static ElementDefinition Define(string id, int size, ElementDamageSourcePolicy damage = null) =>
            new ElementDefinition(new ElementId(id), "경계 상자", new ElementPlacementProfile(size, 9), null, damage,
                null, new ElementDamageAggregationPolicy(true), new ElementRemovalMissionProfile(MissionKind.Crate), ElementReactionBehavior.Durability);
        private static string Placement(string id, int row, int column, string body = "body-1") =>
            "{\"definitionId\":\"" + id + "\",\"instanceId\":\"" + body + "\",\"layer\":1," +
            "\"coordinate\":{\"row\":" + row + ",\"column\":" + column + "},\"durability\":9}";
        private static LevelDefinition Input(string placements)
        {
            LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"initialBlocks\":[],\"obstacles\":[],\"covers\":[],\"dust\":[]," +
                "\"elements\":[" + placements + "],\"missions\":[{\"kind\":1,\"count\":1}]}", level);
            return level;
        }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0; List<LevelDefinition> levels = new List<LevelDefinition>();
            try
            {
                LevelDefinition level = Input(Placement("obstacle.invalid-profile", 2, 2)); levels.Add(level);
                ElementCatalog missingProfile = new ElementCatalog(new[] { Define("obstacle.invalid-profile", 2) });
                LevelStateBuildResult invalid = LevelStateBuilder.Build(level, 312, missingProfile);
                Check(!invalid.IsBuilt && invalid.Issues.Any(issue => issue.Message.Contains("obstacle.invalid-profile")),
                    "직접 카탈로그의 누락 피해 프로필을 시작 전에 거절");
                ElementCatalog catalog = new ElementCatalog(new[] { Define("obstacle.fixture.large", 2, new ElementDamageSourcePolicy(true, true, false, true)) });
                LevelDefinition valid = Input(Placement("obstacle.fixture.large", 2, 2)); levels.Add(valid);
                LevelStateBuildResult result = LevelStateBuilder.Build(valid, 312, catalog);
                Check(result.IsBuilt && result.State.Cells.Count(cell => cell.ObstacleIndex == 0) == 4,
                    "구형 표시 종류와 독립된 신규 정의2×2 점유");
                string pristine = JsonUtility.ToJson(valid);
                BoardEdge wall = new BoardEdge(new BoardCoordinate(2, 2), new BoardCoordinate(2, 3));
                JsonUtility.FromJsonOverwrite("{\"flow\":{\"walls\":[" + JsonUtility.ToJson(wall) + "]}}", valid);
                Check(!LevelStateBuilder.Build(valid, 312, catalog).IsBuilt, "신규 정의2×2 내부의 벽은 거절");
                JsonUtility.FromJsonOverwrite(pristine, valid);
                JsonUtility.FromJsonOverwrite("{\"elements\":[" + Placement("obstacle.fixture.large", 8, 8) + "]}", valid);
                Check(!LevelStateBuilder.Build(valid, 312, catalog).IsBuilt, "9×9 바깥으로 걸치는 신규 점유 거절");
                JsonUtility.FromJsonOverwrite(pristine, valid);
                JsonUtility.FromJsonOverwrite("{\"elements\":[" + Placement("obstacle.fixture.large", 2, 2) + "," + Placement("obstacle.fixture.large", 5, 5) + "]}", valid);
                Check(!LevelStateBuilder.Build(valid, 312, catalog).IsBuilt, "위치가 달라도 중복 본체 ID 거절");
                JsonUtility.FromJsonOverwrite(pristine, valid);
                JsonUtility.FromJsonOverwrite("{\"elements\":[" + Placement("obstacle.fixture.large", 2, 2) + "," + Placement("obstacle.fixture.large", 3, 3, "body-2") + "]}", valid);
                Check(!LevelStateBuilder.Build(valid, 312, catalog).IsBuilt, "본체 ID가 달라도 중첩된 점유 거절");
                JsonUtility.FromJsonOverwrite(pristine, valid);
                ElementPlacementDefinition colorLock = new ElementPlacementDefinition
                {
                    definitionId = LegacyElementDefinitions.Get(ObstacleKind.ColorLock).Id.Value,
                    instanceId = "color-1", layer = PlacementLayer.Obstacle, coordinate = new BoardCoordinate(2, 2), durability = 1
                };
                JsonUtility.FromJsonOverwrite("{\"elements\":[" + JsonUtility.ToJson(colorLock) +
                    "],\"missions\":[{\"kind\":6,\"count\":1}]}", valid);
                Check(!LevelStateBuilder.Build(valid, 312).IsBuilt && LevelDefinitionValidator.Validate(valid)
                    .Any(issue => issue.Message.Contains("배치 색")), "일치 색 정책의 필수 배치 색 누락 거절");
                colorLock.hasColor = true; colorLock.color = RabbitColor.Type1;
                JsonUtility.FromJsonOverwrite("{\"elements\":[" + JsonUtility.ToJson(colorLock) + "]}", valid);
                Check(LevelStateBuilder.Build(valid, 312).IsBuilt, "일치 색 정책의 유효 색은 정상 실행");
                ElementPlacementDefinition rocket = new ElementPlacementDefinition
                {
                    definitionId = LegacyElementDefinitions.GetSupply(SupplyKind.Rocket).Id.Value,
                    layer = PlacementLayer.Block, coordinate = new BoardCoordinate(2, 2), rocketDirection = (RocketDirection)99
                };
                JsonUtility.FromJsonOverwrite("{\"elements\":[" + JsonUtility.ToJson(rocket) +
                    "],\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", valid);
                Check(!LevelStateBuilder.Build(valid, 312).IsBuilt && LevelDefinitionValidator.Validate(valid)
                    .Any(issue => issue.Message.Contains("로켓 방향")), "신형 로켓의 미등록 방향 거절");
                rocket.rocketDirection = RocketDirection.Horizontal;
                JsonUtility.FromJsonOverwrite("{\"elements\":[" + JsonUtility.ToJson(rocket) + "]}", valid);
                Check(LevelStateBuilder.Build(valid, 312).IsBuilt, "신형 로켓의 유효 방향은 정상 실행");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                foreach (LevelDefinition level in levels) UnityEngine.Object.DestroyImmediate(level);
                File.WriteAllLines("Logs/ElementFramework/Phase03/placement-validation-results.txt", Results);
            }
            EditorApplication.Exit(exit);
        }
    }
}
