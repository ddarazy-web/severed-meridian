using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using Levels;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    public static class ElementVisualPreparationVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static void Check(bool pass, string message)
        { if (!pass) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static void Reject(Action action, string id, string state)
        {
            Exception found = null;
            try { action(); } catch (ArgumentException error) { found = error; }
            Check(found != null && found.Message.Contains(id) && found.Message.Contains(state), "준비 전 ID/누락 상태 오류 " + id + " " + state);
        }
        public static void RunEffects()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0; LevelDefinition level = null;
            try
            {
                level = ScriptableObject.CreateInstance<LevelDefinition>();
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", level);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                Check(built.IsBuilt, "필수 효과 준비 검사 입력 유효");
                ElementVisualCatalogDto missing = LegacyElementVisuals.Catalog.ToDto();
                ElementVisualDefinitionDto rocket = missing.definitions.Single(value => value.key == "legacy.rocket");
                foreach (ElementVisualFrameDto frame in rocket.states)
                    frame.effectAnimations = frame.effectAnimations.Where(effect => effect.key != "impact").ToArray();
                Reject(() => ElementResourcePlan.Create(built.State, ElementVisualCatalog.FromDto(missing)), "power.rocket", "impact");
                ElementVisualCatalogDto extra = LegacyElementVisuals.Catalog.ToDto();
                foreach (ElementVisualFrameDto frame in extra.definitions.Single(value => value.key == "legacy.rocket").states)
                    frame.effectAnimations = frame.effectAnimations.Concat(new[] { new ElementVisualEffectDto { key = "unused",
                        frames = new[] { new ElementVisualFrameDto { path = "Obstacles/Generator/unused-fixture" } } } }).ToArray();
                ElementResourcePlan plan = ElementResourcePlan.Create(built.State, ElementVisualCatalog.FromDto(extra));
                Check(!plan.Paths.Any(path => path.Contains("unused-fixture")) && !plan.Addresses.Any(address => address.Contains("Generator")),
                    "필요하지 않은 등록 효과의 별도 종류 주소 요청0");
                Check(plan.Paths.Any(path => path.Contains("rocket-impact")) && plan.Frames.Any(frame => frame.Path.Contains("rocket-impact")),
                    "실제 행동의 효과 주소와 프레임 모두 준비");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally { if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            File.WriteAllLines("Logs/ElementFramework/Phase04/required-effect-preparation-results.txt", Results);
            EditorApplication.Exit(exit);
        }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0; LevelDefinition level = null;
            try
            {
                string id = "fixture.preparation.body";
                ElementDefinition body = new ElementDefinition(new ElementId(id), id, new ElementPlacementProfile(1, 13), null,
                    new ElementDamageSourcePolicy(true, true, false, true), null, new ElementDamageAggregationPolicy(false),
                    new ElementRemovalMissionProfile(MissionKind.Crate), ElementReactionBehavior.Durability);
                ElementCatalog rules = new ElementCatalog(LegacyElementDefinitions.DefaultCatalog.Definitions.Concat(new[] { body }));
                ElementVisualCatalog alias = LegacyElementVisuals.WithOverrides(new ElementVisualCatalogDto
                { bindings = new[] { new ElementVisualBindingDto { id = id, visualKey = "legacy.crate" } } });
                level = ScriptableObject.CreateInstance<LevelDefinition>();
                void Place(int durability) => JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"missions\":[{\"kind\":1,\"count\":1}],\"elements\":[" +
                    JsonUtility.ToJson(new ElementPlacementDefinition { definitionId = id, instanceId = "body", layer = PlacementLayer.Obstacle,
                        coordinate = new BoardCoordinate(4, 4), durability = durability }) + "]}", level);
                Place(13);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345, rules);
                Check(built.IsBuilt, "규칙이 허용하는 내구도13 입력 유효");
                Reject(() => ElementResourcePlan.Create(built.State, alias), id, "내구도=13");
                Place(2); built = LevelStateBuilder.Build(level, 12345, rules);
                Check(built.IsBuilt, "동일 최대13 정의의 실제 내구도2 입력 유효");
                string source = JsonUtility.ToJson(level); int draws = built.State.Random.DrawCount;
                ElementResourcePlan.Create(built.State, alias);
                Check(JsonUtility.ToJson(level) == source && built.State.Random.DrawCount == draws, "실제 도달 범위1~2만 검증·원본/난수 무변경");
                ElementVisualCatalogDto bounded = new ElementVisualCatalogDto
                {
                    definitions = new[] { new ElementVisualDefinitionDto { key = "fixture.preparation.bounded", states = new[] {
                        new ElementVisualFrameDto { durability = 1, path = "Obstacles/Crate/crate-durability-1-v1-256", effectAnimations = ElementVisualVerification.FixtureEffects("legacy.crate") },
                        new ElementVisualFrameDto { durability = 2, path = "Obstacles/Crate/crate-durability-2-v1-256", effectAnimations = ElementVisualVerification.FixtureEffects("legacy.crate") },
                        new ElementVisualFrameDto { durability = 13, path = "Obstacles/Mold/unreachable-fixture", effects = new[] { "Obstacles/Web/unreachable-effect" } } } } },
                    bindings = new[] { new ElementVisualBindingDto { id = id, visualKey = "fixture.preparation.bounded" } }
                };
                ElementResourcePlan boundedPlan = ElementResourcePlan.Create(built.State, LegacyElementVisuals.WithOverrides(bounded));
                Check(!boundedPlan.Paths.Any(path => path.Contains("unreachable")) &&
                    !boundedPlan.Frames.Any(frame => frame.Path.Contains("unreachable")), "도달하지 않는 내구도13 이미지·효과·시트 준비 제외");
                Check(boundedPlan.Paths.Contains("Obstacles/Crate/crate-durability-1-v1-256") &&
                    boundedPlan.Paths.Contains("Obstacles/Crate/crate-durability-2-v1-256"), "현재와 피해 후 상태의 두 이미지 모두 준비");
                ElementVisualCatalogDto mapping = new ElementVisualCatalogDto
                {
                    definitions = new[] { new ElementVisualDefinitionDto { key = "fixture.preparation.partial", states = new[] {
                        new ElementVisualFrameDto { durability = 2, path = "Obstacles/Crate/crate-durability-2-v1-256", effectAnimations = ElementVisualVerification.FixtureEffects("legacy.crate") } } } },
                    bindings = new[] { new ElementVisualBindingDto { id = id, visualKey = "fixture.preparation.partial" } }
                };
                Reject(() => ElementResourcePlan.Create(built.State, LegacyElementVisuals.WithOverrides(mapping)), id, "내구도=1");
                mapping.definitions[0].states[0].durability = -1;
                ElementResourcePlan.Create(built.State, LegacyElementVisuals.WithOverrides(mapping));
                Check(true, "명시적 공유 프레임은 실제 내구도 전체를 표현 가능");
                ElementVisualCatalogDto powers = LegacyElementVisuals.Catalog.ToDto();
                powers.bindings = powers.bindings.Concat(new[] { new ElementVisualBindingDto { id = id, visualKey = "legacy.crate" } }).ToArray();
                ElementVisualDefinitionDto rocket = powers.definitions.Single(definition => definition.key == "legacy.rocket");
                rocket.states = rocket.states.Where(frame => frame.frame != 3).ToArray();
                Reject(() => ElementResourcePlan.Create(built.State, ElementVisualCatalog.FromDto(powers)), "power.rocket", "프레임=3");
                powers = LegacyElementVisuals.Catalog.ToDto();
                ElementVisualDefinitionDto drone = powers.definitions.Single(definition => definition.key == "legacy.drone");
                drone.states = drone.states.Where(frame => frame.frame != 4).ToArray();
                powers.bindings = powers.bindings.Concat(new[] { new ElementVisualBindingDto { id = id, visualKey = "legacy.crate" } }).ToArray();
                Reject(() => ElementResourcePlan.Create(built.State, ElementVisualCatalog.FromDto(powers)), "power.drone", "프레임=4");
                ElementLevelSupplyDefinition lastPangSupply = new ElementLevelSupplyDefinition
                { sources = new List<ElementSupplySourceDefinition> { new ElementSupplySourceDefinition {
                    coordinate = new BoardCoordinate(0, 0), mode = SupplyMode.Random, randomDefinitionId = "supply.normal.random" } } };
                JsonUtility.FromJsonOverwrite("{\"colors\":[0,1,2,3],\"elementSupply\":" + JsonUtility.ToJson(lastPangSupply) + "}", level);
                built = LevelStateBuilder.Build(level, 12345, rules);
                Check(built.IsBuilt && built.State.Colors.Count == 4 && built.State.Supply.Sources.Count == 1, "라스트팡 공급이 가능한 네 색 레벨 입력 구성");
                powers = LegacyElementVisuals.Catalog.ToDto();
                powers.bindings = powers.bindings.Concat(new[] { new ElementVisualBindingDto { id = id, visualKey = "legacy.crate" } }).ToArray();
                ElementVisualDefinitionDto normal = powers.definitions.Single(definition => definition.key == "legacy.normal");
                normal.states = normal.states.Where(frame => frame.color != 4).ToArray();
                Reject(() => ElementResourcePlan.Create(built.State, ElementVisualCatalog.FromDto(powers)), "supply.normal.random", "색=4");
                ElementResourcePlan lastPangPlan = ElementResourcePlan.Create(built.State, alias);
                Check(lastPangPlan.Paths.Contains("Blocks/rabbit-purple-v1-256") &&
                    lastPangPlan.Frames.Any(frame => frame.Path == "Blocks/rabbit-purple-v1-256"), "네 색 레벨도 종료 공급의 다섯 번째 이미지·프레임 준비");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally { if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            Directory.CreateDirectory("Logs/ElementFramework/Phase04");
            File.WriteAllLines("Logs/ElementFramework/Phase04/visual-preparation-results.txt", Results);
            EditorApplication.Exit(exit);
        }
    }
}
