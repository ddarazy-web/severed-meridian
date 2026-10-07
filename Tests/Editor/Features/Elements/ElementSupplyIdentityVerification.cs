using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Levels;
using Levels.Editor;
using MemoryPack;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    /// <summary>신규 공급이 카탈로그의 본체/선택 ID를 그대로 사용하는지 검사한다.</summary>
    public static class ElementSupplyIdentityVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0; LevelDefinition level = null;
            try
            {
                MethodInfo obstacleFactory = typeof(ElementSupplyProfile).GetMethod("ForObstacle");
                MethodInfo randomFactory = typeof(ElementSupplyProfile).GetMethod("ForRandomPower");
                Check(obstacleFactory != null && randomFactory != null, "공급 프로필에 정의 ID 참조 계약 존재");
                ElementDefinition body = new ElementDefinition(new ElementId("obstacle.fixture.supplied"), "새 고철",
                    new ElementPlacementProfile(1, 9), null, new ElementDamageSourcePolicy(true, false, false, true), null,
                    new ElementDamageAggregationPolicy(false), new ElementRemovalMissionProfile(MissionKind.Scrap), ElementReactionBehavior.Durability);
                ElementSupplyProfile obstacleProfile = (ElementSupplyProfile)obstacleFactory.Invoke(null, new object[] { body.Id, "fixture-body-" });
                ElementDefinition obstacleSupply = ElementDefinition.CreateSupply(new ElementId("supply.fixture.obstacle"), "새 고철 공급", obstacleProfile);
                ElementDefinition bomb = ElementDefinition.CreateSupply(new ElementId("power.fixture.bomb"), "새 폭탄",
                    new ElementSupplyProfile(ElementSupplyBehavior.Power, RuntimeContent.Bomb));
                ElementDefinition rocket = ElementDefinition.CreateSupply(new ElementId("power.fixture.rocket"), "새 로켓",
                    new ElementSupplyProfile(ElementSupplyBehavior.Power, RuntimeContent.Rocket));
                ElementId[] choices = { bomb.Id, rocket.Id };
                ElementSupplyProfile randomProfile = (ElementSupplyProfile)randomFactory.Invoke(null, new object[] { choices });
                choices[0] = new ElementId("power.changed-source");
                ElementDefinition random = ElementDefinition.CreateSupply(new ElementId("supply.fixture.random"), "새 무작위 공급", randomProfile);
                ElementDefinition restoredObstacle = MemoryPackSerializer.Deserialize<PackedElementDefinition>(
                    MemoryPackSerializer.Serialize(PackedElementDefinition.FromDefinition(obstacleSupply))).ToDefinition();
                ElementDefinition restoredRandom = MemoryPackSerializer.Deserialize<PackedElementDefinition>(
                    MemoryPackSerializer.Serialize(PackedElementDefinition.FromDefinition(random))).ToDefinition();
                Check(Snapshot(restoredObstacle) == Snapshot(obstacleSupply) && Snapshot(restoredRandom) == Snapshot(random),
                    "새 공급의 본체/선택 정의 ID는 실제 MemoryPack 왕복 보존 및 원본 배열 독립");
                ElementCatalog catalog = new ElementCatalog(new[] { body, restoredObstacle, bomb, rocket, restoredRandom });
                level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                ElementPlacementDefinition placement = new ElementPlacementDefinition { definitionId = body.Id.Value, instanceId = "placed-1",
                    coordinate = new BoardCoordinate(3, 3), layer = PlacementLayer.Obstacle, durability = 1 };
                JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"elements\":[" + JsonUtility.ToJson(placement) +
                    "],\"missions\":[{\"kind\":3,\"count\":1}]}", level);
                string original = JsonUtility.ToJson(level);
                LevelStateBuildResult result = LevelStateBuilder.Build(level, 49713, catalog);
                Check(result.IsBuilt, "신규 공급 참조 카탈로그 실행 구성 " + string.Join(";", result.Issues));
                MethodInfo apply = typeof(LevelRuntimeState).Assembly.GetType("Elements.ElementSupplyBehaviorRegistry")
                    .GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic);
                BoardCoordinate at = new BoardCoordinate(0, 0);
                apply.Invoke(null, new object[] { restoredObstacle, result.State, result.State.CellAt(at), new SupplyItem(SupplyKind.Scrap, durability: 7) });
                RuntimeObstacle supplied = result.State.Obstacles[result.State.CellAt(at).ObstacleIndex.Value];
                Check(ReferenceEquals(supplied.Element, body) && supplied.Durability == 7 && supplied.Definition.Id.StartsWith("fixture-body-"),
                    "신규 공급은 구형 최대5 대신 선택 본체 최대9/현재7과 고유 ID 사용");
                Check(DamageReaction.Evaluate(result.State, at, DamageCause.Power, at).Response == DamageResponse.None,
                    "공급한 본체의 신규 파워 거절 정책 사용");
                PropertyInfo identity = typeof(RuntimeCell).GetProperty("ContentElement", BindingFlags.Instance | BindingFlags.NonPublic);
                HashSet<ElementId> observed = new HashSet<ElementId>();
                bool allSelected = true; int rocketCount = 0, draws = result.State.Random.DrawCount;
                for (int i = 0; i < 40; i++)
                {
                    apply.Invoke(null, new object[] { restoredRandom, result.State, result.State.CellAt(new BoardCoordinate(0, 1)), new SupplyItem(SupplyKind.RandomPower) });
                    ElementDefinition selected = (ElementDefinition)identity.GetValue(result.State.CellAt(new BoardCoordinate(0, 1)));
                    allSelected &= ReferenceEquals(selected, bomb) || ReferenceEquals(selected, rocket);
                    if (ReferenceEquals(selected, rocket)) rocketCount++;
                    observed.Add(selected.Id);
                }
                Check(allSelected && result.State.Random.DrawCount == draws + 40 + rocketCount,
                    "무작위 공급40회 실제 정의 유지와 선택/로켓 방향 난수 소비 일치");
                Check(observed.Count == 2, "두 신규 파워 ID 모두 실제 공급");
                Exception missing = null;
                try { new ElementCatalog(new[] { restoredObstacle }); }
                catch (ArgumentException error) { missing = error; }
                Check(missing != null && missing.Message.Contains(body.Id.Value), "미등록 공급 본체 참조는 카탈로그 구성에서 거절");
                Exception wrong = null;
                try { new ElementCatalog(new[] { restoredRandom, bomb, body }); }
                catch (ArgumentException error) { wrong = error; }
                Check(wrong != null && wrong.Message.Contains(rocket.Id.Value), "미등록 무작위 선택 ID는 카탈로그 구성에서 거절");
                Check(JsonUtility.ToJson(level) == original, "신규 공급 실행은 제작 입력 불변");
                Check(typeof(LevelDefinition).GetProperty("ElementSupply") != null, "신형 레벨 공급의 정의 ID 저장 계약 존재");
                JsonUtility.FromJsonOverwrite("{\"elementSupply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":0}," +
                    "\"mode\":1,\"exhaustion\":0,\"items\":[{\"definitionId\":\"supply.fixture.obstacle\",\"count\":1,\"durability\":7}]}]}," +
                    "\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":0},\"mode\":1,\"exhaustion\":0,\"items\":[{\"kind\":3,\"count\":1}]}]}}", level);
                result = LevelStateBuilder.Build(level, 49713, catalog);
                Check(result.IsBuilt, "신형 공급 목록의 최대9/현재7 배치 구성 " + string.Join(";", result.Issues));
                typeof(RuntimeCell).GetProperty("Content").SetValue(result.State.CellAt(at), RuntimeContent.Empty);
                SettlementResult actualSupply = SettlementResolution.Resolve(result.State);
                Check(actualSupply.IsApplied && actualSupply.State.CellAt(at).Content == RuntimeContent.Obstacle &&
                    ReferenceEquals(actualSupply.State.Obstacles[actualSupply.State.CellAt(at).ObstacleIndex.Value].Element, body),
                    "실제 고정 공급은 구형 폭탄 목록 대신 신형 공급 ID만 실행");
                JsonUtility.FromJsonOverwrite("{\"elementSupply\":{\"sources\":[]}}", level);
                Check(LevelStateBuilder.Build(level, 49713, catalog).State.Supply.Sources.Count == 0,
                    "빈 신형 공급 목록은 구형 공급 목록을 되살리지 않음");
                JsonUtility.FromJsonOverwrite("{\"elementSupply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":0},\"mode\":1," +
                    "\"exhaustion\":0,\"items\":[{\"definitionId\":\"supply.missing\",\"count\":1}]}]}}", level);
                result = LevelStateBuilder.Build(level, 49713, catalog);
                Check(!result.IsBuilt && result.Issues.Any(issue => issue.Message.Contains("supply.missing")),
                    "신형 고정 목록의 누락 공급 ID는 시작 전에 거절");
                JsonUtility.FromJsonOverwrite("{\"elements\":[],\"elementSupply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":0}," +
                    "\"mode\":1,\"exhaustion\":0,\"items\":[{\"definitionId\":\"supply.fixture.obstacle\",\"count\":1,\"durability\":7}]}]}}", level);
                result = LevelStateBuilder.Build(level, 49713, catalog);
                Check(result.IsBuilt, "신규 고정 공급만으로 내구도7 본체 미션 공급량 인정 " + string.Join(";", result.Issues));
                ElementLevelSupplyDefinition packedSupply = MemoryPackSerializer.Deserialize<ElementLevelSupplyDefinition>(MemoryPackSerializer.Serialize(level.ElementSupply));
                Check(packedSupply.sources[0].items[0].definitionId == restoredObstacle.Id.Value && packedSupply.sources[0].items[0].durability == 7,
                    "신형 레벨 공급 DTO의 실제 바이트 왕복에서 ID/내구도 보존");
                level.ElementSupply.sources[0].items[0].durability = 10;
                result = LevelStateBuilder.Build(level, 49713, catalog);
                Check(!result.IsBuilt && result.Issues.Any(issue => issue.Message.Contains(restoredObstacle.Id.Value) && issue.Message.Contains("1~9")),
                    "신형 공급 내구도 오류는 선택 본체의 실제 상한9와 공급 ID를 보고");
                level.ElementSupply.sources[0].items[0].durability = 7;
                ElementDefinition crate = new ElementDefinition(new ElementId("obstacle.fixture.supplied-crate"), "새 상자",
                    new ElementPlacementProfile(1, 9), null, new ElementDamageSourcePolicy(true, true, false, true), null,
                    new ElementDamageAggregationPolicy(false), new ElementRemovalMissionProfile(MissionKind.Crate), ElementReactionBehavior.Durability);
                ElementDefinition crateSupply = ElementDefinition.CreateSupply(new ElementId("supply.fixture.crate"), "새 상자 공급",
                    (ElementSupplyProfile)obstacleFactory.Invoke(null, new object[] { crate.Id, "fixture-crate-" }));
                ElementCatalog mixedCatalog = new ElementCatalog(new[] { body, restoredObstacle, crate, crateSupply,
                    LegacyElementDefinitions.GetSupply(SupplyKind.RandomNormal) });
                JsonUtility.FromJsonOverwrite("{\"elementSupply\":{\"scrapTarget\":1,\"scrapLimit\":1,\"scrapDurability\":7," +
                    "\"scrapDefinitionId\":\"supply.fixture.obstacle\",\"sources\":[{\"coordinate\":{\"row\":0,\"column\":0},\"mode\":1,\"exhaustion\":0," +
                    "\"items\":[{\"definitionId\":\"supply.fixture.crate\",\"count\":1,\"durability\":7}]},{\"coordinate\":{\"row\":0,\"column\":1}," +
                    "\"mode\":2,\"exhaustion\":0,\"items\":[],\"randomDefinitionId\":\"supply.normal.random\"}]}," +
                    "\"missions\":[{\"kind\":1,\"count\":1},{\"kind\":3,\"count\":1}]}", level);
                result = LevelStateBuilder.Build(level, 49713, mixedCatalog);
                Check(result.IsBuilt, "신규 상자 고정 공급과 고철 유지 공급의 미션을 독립 검사 " + string.Join(";", result.Issues));
                typeof(RuntimeCell).GetProperty("Content").SetValue(result.State.CellAt(at), RuntimeContent.Empty);
                typeof(RuntimeCell).GetProperty("Content").SetValue(result.State.CellAt(new BoardCoordinate(0, 1)), RuntimeContent.Empty);
                actualSupply = SettlementResolution.Resolve(result.State);
                Check(actualSupply.IsApplied && actualSupply.State.Obstacles.Count == 2 &&
                    actualSupply.State.Obstacles.Any(item => ReferenceEquals(item.Element, crate)) &&
                    actualSupply.State.Obstacles.Any(item => ReferenceEquals(item.Element, body)),
                    "실제 정착도 상자 고정/고철 유지 공급을 종류 분기 충돌 없이 생성");
                ElementDefinition colored = new ElementDefinition(new ElementId("obstacle.fixture.supplied-color"), "새 지정색 본체",
                    new ElementPlacementProfile(1, 3), null, new ElementDamageSourcePolicy(true, true, false, true), new ElementColorMatchPolicy(true),
                    new ElementDamageAggregationPolicy(false), new ElementRemovalMissionProfile(MissionKind.ColorLock), ElementReactionBehavior.Durability);
                ElementDefinition coloredSupply = ElementDefinition.CreateSupply(new ElementId("supply.fixture.color"), "지정색 본체 공급",
                    (ElementSupplyProfile)obstacleFactory.Invoke(null, new object[] { colored.Id, "fixture-color-" }));
                ElementCatalog coloredCatalog = new ElementCatalog(new[] { colored, coloredSupply });
                JsonUtility.FromJsonOverwrite("{\"elementSupply\":{\"scrapTarget\":0,\"scrapLimit\":0,\"scrapDurability\":1,\"sources\":[{" +
                    "\"coordinate\":{\"row\":0,\"column\":0},\"mode\":1,\"exhaustion\":0,\"items\":[{\"definitionId\":\"supply.fixture.color\"," +
                    "\"count\":1,\"color\":2,\"durability\":2}]}]},\"missions\":[{\"kind\":6,\"count\":1}]}", level);
                result = LevelStateBuilder.Build(level, 49713, coloredCatalog);
                Check(result.IsBuilt, "지정색 신규 공급 미션 구성 " + string.Join(";", result.Issues));
                level.ElementSupply.sources[0].items[0].definitionId = "supply.changed-after-build";
                level.ElementSupply.sources[0].items[0].color = (RabbitColor)99;
                typeof(RuntimeCell).GetProperty("Content").SetValue(result.State.CellAt(at), RuntimeContent.Empty);
                actualSupply = SettlementResolution.Resolve(result.State);
                Check(actualSupply.IsApplied && actualSupply.State.Obstacles[0].Definition.Color == RabbitColor.Type3 &&
                    ReferenceEquals(actualSupply.State.Obstacles[0].Element, colored), "원본 공급 수정 후에도 실행 사본의 정의/지정색 유지");
                level.ElementSupply.sources[0].items[0].definitionId = coloredSupply.Id.Value;
                result = LevelStateBuilder.Build(level, 49713, coloredCatalog);
                Check(!result.IsBuilt && result.Issues.Any(issue => issue.Message.Contains(coloredSupply.Id.Value) &&
                    issue.PropertyPath.StartsWith("elementSupply", StringComparison.Ordinal)), "잘못된 공급 지정색은 실제 신형 필드와 ID로 보고");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                if (level != null) UnityEngine.Object.DestroyImmediate(level);
                File.WriteAllLines("Logs/ElementFramework/Phase03/supply-identity-results.txt", Results);
            }
            EditorApplication.Exit(exit);
        }
        private static string Snapshot(object value) => (string)typeof(LevelInitialStateVerification)
            .GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { value });
    }
}
