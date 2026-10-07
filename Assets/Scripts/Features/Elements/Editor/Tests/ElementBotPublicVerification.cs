using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    public static class ElementBotPublicVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static bool layers;
        public static void RunLayers() { layers = true; Run(); }
        private static void Check(bool pass, string message)
        { if (!pass) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static string Snapshot(object value) => (string)typeof(LevelInitialStateVerification)
            .GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { value });
        private static object Trait(BotBody body, string name)
        {
            PropertyInfo property = typeof(BotBody).GetProperty(name);
            Check(property != null && property.SetMethod == null, "공개 행동 값 존재·쓰기 금지 " + name);
            return property.GetValue(body);
        }
        private static ElementDefinition Definition(string id, int maximum) => new ElementDefinition(new ElementId(id), id,
            new ElementPlacementProfile(1, maximum), null, new ElementDamageSourcePolicy(false, true, false, false),
            new ElementColorMatchPolicy(true), new ElementDamageAggregationPolicy(true),
            new ElementRemovalMissionProfile(MissionKind.Crate), ElementReactionBehavior.Durability);
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0; LevelDefinition level = null;
            try
            {
                if (layers) { VerifyLayers(); return; }
                level = ScriptableObject.CreateInstance<LevelDefinition>();
                ElementDefinition first = Definition("fixture.bot.public-a", 13), second = Definition("fixture.bot.public-b", 25);
                ElementCatalog catalog = new ElementCatalog(LegacyElementDefinitions.DefaultCatalog.Definitions.Concat(new[] { first, second }));
                BoardActionExecutor Build(ElementDefinition definition, string instance)
                {
                    JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"missions\":[{\"kind\":1,\"count\":1}],\"elements\":[" +
                        JsonUtility.ToJson(new ElementPlacementDefinition { definitionId = definition.Id.Value, instanceId = instance,
                            layer = PlacementLayer.Obstacle, coordinate = new BoardCoordinate(4, 4), durability = 2,
                            hasColor = true, color = RabbitColor.Type1 }) + "," + JsonUtility.ToJson(new ElementPlacementDefinition {
                                definitionId = "supply.normal.fixed", layer = PlacementLayer.Block, coordinate = new BoardCoordinate(4, 3),
                                hasColor = true, color = RabbitColor.Type1 }) + "," + JsonUtility.ToJson(new ElementPlacementDefinition {
                                    definitionId = "power.rocket", layer = PlacementLayer.Block, coordinate = new BoardCoordinate(0, 0) }) + "]}", level);
                    LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345, catalog);
                    Check(built.IsBuilt, "공개 특성이 같은 신규 ID 입력 유효 " + definition.Id.Value);
                    return new BoardActionExecutor(built.State);
                }
                BoardActionExecutor a = Build(first, "history-a"), b = Build(second, "history-b");
                ObstaclePlacementDefinition removedPlacement = (ObstaclePlacementDefinition)Activator.CreateInstance(typeof(ObstaclePlacementDefinition),
                    BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { "removed-history", new BoardCoordinate(8, 8), ObstacleKind.Crate, 1, RabbitColor.Type1, 0 }, null);
                RuntimeObstacle removed = (RuntimeObstacle)Activator.CreateInstance(typeof(RuntimeObstacle), BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { removedPlacement, first }, null);
                typeof(RuntimeObstacle).GetProperty("Durability").GetSetMethod(true).Invoke(removed, new object[] { 0 });
                ((List<RuntimeObstacle>)typeof(LevelRuntimeState).GetField("obstacleBodies", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(b.State)).Insert(0, removed);
                typeof(RuntimeCell).GetProperty("ObstacleIndex").GetSetMethod(true).Invoke(b.State.CellAt(new BoardCoordinate(4, 4)), new object[] { 1 });
                Check(a.State.CellAt(new BoardCoordinate(4, 4)).ObstacleIndex != b.State.CellAt(new BoardCoordinate(4, 4)).ObstacleIndex &&
                    b.State.Obstacles[0].Durability == 0, "제거 이력과 내부 본체 인덱스가 실제로 다른 비교 입력");
                typeof(SimulationRandom).GetMethod("Next", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(b.State.Random, new object[] { 100 });
                string beforeA = Snapshot(a.State), beforeB = Snapshot(b.State);
                int drawsA = a.State.Random.DrawCount, drawsB = b.State.Random.DrawCount;
                BotObservation publicA = BotObservationBuilder.Capture(a), publicB = BotObservationBuilder.Capture(b);
                BotBody body = publicA.Bodies.Single();
                Check((bool)Trait(body, "AcceptsAdjacentMatch") == false && (bool)Trait(body, "AcceptsPower") &&
                    (bool)Trait(body, "AcceptsMagnetAdjacent") == false && (bool)Trait(body, "AcceptsHammer") == false,
                    "구형 Crate와 다른 네 피해 허용 값 복사");
                Check((bool)Trait(body, "RequiresAdjacentColor") && (bool)Trait(body, "PerHitCell") &&
                    (MissionKind?)Trait(body, "RemovalMission") == MissionKind.Crate, "색·칸별 피해·제거 미션 공개 값 복사");
                Check(Snapshot(publicA) == Snapshot(publicB), "ID·제작명·최대치·인스턴스 이력·난수만 다른 공개 관찰/후보 동일");
                Check(publicA.Actions.Count > 0, "동일 입력 쌍의 실제 파워 후보 존재");
                BotAction adjacent = (BotAction)Activator.CreateInstance(typeof(BotAction), BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { BotActionKind.Activate, new BoardCoordinate(4, 3), null, Array.Empty<BotMatch>() }, null);
                MethodInfo score = typeof(BotObservation).Assembly.GetType("AutoPlay.BotMissionEvaluation")
                    .GetMethod("Score", BindingFlags.Static | BindingFlags.NonPublic);
                Check((int)score.Invoke(null, new object[] { publicA, adjacent, new HashSet<BoardCoordinate> { new BoardCoordinate(4, 3) },
                    true, false, null }) == 0, "후보 미션 평가는 등록된 인접 매칭 거부 사용");
                Type branchType = typeof(BotObservation).Assembly.GetType("AutoPlay.PlanningBranch");
                MethodInfo seed = branchType.GetMethod("Seed", BindingFlags.Static | BindingFlags.NonPublic);
                Check((int)seed.Invoke(null, new object[] { publicA, 0 }) == (int)seed.Invoke(null, new object[] { publicB, 0 }),
                    "공개 동일 쌍은 실제 ID·이력·난수와 무관한 가정 시드 사용");
                object branchA = branchType.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { publicA, 0 });
                object branchB = branchType.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { publicB, 0 });
                BotObservation plannedA = (BotObservation)branchType.GetMethod("Observe", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(branchA, null);
                BotObservation plannedB = (BotObservation)branchType.GetMethod("Observe", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(branchB, null);
                Check(Snapshot(plannedA.Bodies) == Snapshot(publicA.Bodies), "가정 실행은 새 ID의 공개 행동 값 보존");
                Check(Snapshot(plannedA) == Snapshot(plannedB), "공개 동일 쌍의 가정 실행 관찰·후보 동일");
                Check(Snapshot(BasicBotStrategy.Choose(publicA)) == Snapshot(BasicBotStrategy.Choose(publicB)), "공개 동일 쌍의 선택/이유 동일");
                Check(string.Join("\n", publicA.Actions.Select(action => Snapshot(BasicBotStrategy.Evaluate(publicA, action)))) ==
                    string.Join("\n", publicB.Actions.Select(action => Snapshot(BasicBotStrategy.Evaluate(publicB, action)))), "공개 동일 쌍의 모든 후보 점수 동일");
                Check(beforeA == Snapshot(a.State) && beforeB == Snapshot(b.State) && drawsA == a.State.Random.DrawCount &&
                    drawsB == b.State.Random.DrawCount, "관찰·전체 평가 후 원본/정의/난수 무변경");
                Check(!Snapshot(publicA).Contains(first.Id.Value) && !Snapshot(publicB).Contains(second.Id.Value), "공개 값에 실제 정의 ID/이름 없음");
                foreach (FieldInfo field in typeof(BotBody).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    Check(field.FieldType != typeof(ElementDefinition) && field.FieldType != typeof(ElementId) &&
                        !field.FieldType.Namespace.StartsWith("Elements", StringComparison.Ordinal), "본체 관찰은 정의 객체 참조 없음 " + field.Name);
                string retained = Snapshot(publicA); typeof(RuntimeObstacle).GetProperty("Durability").GetSetMethod(true).Invoke(a.State.Obstacles[0], new object[] { 1 });
                Check(Snapshot(publicA) == retained && Snapshot(BotObservationBuilder.Capture(a)) != retained, "이전 공개 관찰은 원본 수정과 독립");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally { if (level != null) UnityEngine.Object.DestroyImmediate(level); Finish(exit); }
        }
        private static void Finish(int exit)
        {
            Directory.CreateDirectory("Logs/ElementFramework/Phase04");
            File.WriteAllLines("Logs/ElementFramework/Phase04/" + (layers ? "bot-public-layer-results.txt" : "bot-public-results.txt"), Results);
            EditorApplication.Exit(exit);
        }
        private static void VerifyLayers()
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                ElementDefinition Layer(string id, ElementLayerBehavior behavior, MissionKind mission, int damage, bool spread = false)
                    => new ElementDefinition(new ElementId(id), id, new ElementPlacementProfile(1, 3), null, null, null, null, null, null,
                        new ElementLayerProfile(behavior, mission, damage), spread ? new ElementTurnProfile(ElementTurnBehavior.AdjacentCoverSpread, 2) : null);
                ElementDefinition[] definitions = new[] {
                    Layer("fixture.bot.web-a", ElementLayerBehavior.CoverDurability, MissionKind.Web, 2),
                    Layer("fixture.bot.web-b", ElementLayerBehavior.CoverDurability, MissionKind.Web, 2),
                    Layer("fixture.bot.dust-a", ElementLayerBehavior.NormalConsumption, MissionKind.Dust, 2),
                    Layer("fixture.bot.dust-b", ElementLayerBehavior.NormalConsumption, MissionKind.Dust, 2),
                    Layer("fixture.bot.mold-a", ElementLayerBehavior.CoverRemoval, MissionKind.Mold, 1, true),
                    Layer("fixture.bot.mold-b", ElementLayerBehavior.CoverRemoval, MissionKind.Mold, 1, true) };
                ElementCatalog catalog = new ElementCatalog(LegacyElementDefinitions.DefaultCatalog.Definitions.Concat(definitions));
                BoardCoordinate dust = new BoardCoordinate(0, 0), web = new BoardCoordinate(0, 1), mold = new BoardCoordinate(2, 2), rocket = new BoardCoordinate(0, 8);
                BoardActionExecutor Build(string suffix)
                {
                    ElementPlacementDefinition[] placements = new[] {
                        new ElementPlacementDefinition { definitionId = "fixture.bot.dust-" + suffix, layer = PlacementLayer.Dust, coordinate = dust, durability = 2 },
                        new ElementPlacementDefinition { definitionId = "fixture.bot.web-" + suffix, layer = PlacementLayer.Cover, coordinate = web, durability = 2 },
                        new ElementPlacementDefinition { definitionId = "fixture.bot.mold-" + suffix, layer = PlacementLayer.Cover, coordinate = mold, durability = 1 },
                        new ElementPlacementDefinition { definitionId = "power.rocket", layer = PlacementLayer.Block, coordinate = rocket,
                            rocketDirection = RocketDirection.Horizontal } };
                    placements = placements.Concat(new[] { dust, web, mold }.Select(at => new ElementPlacementDefinition {
                        definitionId = "supply.normal.fixed", layer = PlacementLayer.Block, coordinate = at, hasColor = true, color = RabbitColor.Type1 })).ToArray();
                    JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"missions\":[" + string.Join(",", new[] { MissionKind.Web, MissionKind.Dust, MissionKind.Mold }
                        .Select(kind => "{\"kind\":" + (int)kind + ",\"count\":1}")) + "],\"elements\":[" +
                        string.Join(",", placements.Select(item => JsonUtility.ToJson(item))) + "]}", level);
                    LevelStateBuildResult built = null;
                    for (int seed = 12345; seed < 12445; seed++)
                    {
                        built = LevelStateBuilder.Build(level, seed, catalog);
                        if (built.IsBuilt && new StartConditionReport(built.State).IsSatisfied) break;
                    }
                    Check(built.IsBuilt, "동일 공개 층 행동의 별도 ID 입력 유효 " + suffix + " " + string.Join(";", built.Issues));
                    Check(new StartConditionReport(built.State).IsSatisfied, "층 피해 검사의 실제 시작 조건 만족 " + suffix);
                    return new BoardActionExecutor(built.State);
                }
                BoardActionExecutor a = Build("a"), b = Build("b");
                string beforeA = Snapshot(a.State), beforeB = Snapshot(b.State);
                BotObservation publicA = BotObservationBuilder.Capture(a), publicB = BotObservationBuilder.Capture(b);
                object Value(BoardCoordinate at, string name)
                {
                    PropertyInfo property = typeof(BotCell).GetProperty(name);
                    Check(property != null && property.SetMethod == null, "공개 층 값 존재·쓰기 금지 " + name);
                    return property.GetValue(publicA.CellAt(at));
                }
                Check((int)Value(web, "CoverDamage") == 2 && (bool)Value(web, "CoverUsesDurability") &&
                    (MissionKind?)Value(web, "CoverMission") == MissionKind.Web, "덮개 피해량·제거 방식·미션 복사");
                Check((int)Value(dust, "DustDamage") == 2 && (MissionKind?)Value(dust, "DustMission") == MissionKind.Dust, "먼지 피해량·미션 복사");
                Check((bool)Value(mold, "CoverSpreads") && (int)Value(mold, "SpreadDurability") == 2 &&
                    publicA.CellAt(mold).Content == BotContent.Unknown && publicA.CellAt(mold).Color == null, "번식 초기값 공개·곰팡이 내부 은폐 유지");
                Check(Snapshot(publicA) == Snapshot(publicB), "층의 ID·제작명만 다른 공개 관찰/후보 동일");
                BotAction action = publicA.Actions.Single(candidate => candidate.Kind == BotActionKind.Activate && candidate.First.Equals(rocket));
                Type branchType = typeof(BotObservation).Assembly.GetType("AutoPlay.PlanningBranch");
                object branch = branchType.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { publicA, 0 });
                BotObservation planned = (BotObservation)branchType.GetMethod("Observe", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(branch, null);
                Check(Snapshot(planned) == Snapshot(publicA), "가정 실행은 층의 공개 피해·번식·미션을 보존");
                MethodInfo seedMethod = branchType.GetMethod("Seed", BindingFlags.Static | BindingFlags.NonPublic);
                Check((int)seedMethod.Invoke(null, new object[] { publicA, 0 }) == (int)seedMethod.Invoke(null, new object[] { publicB, 0 }),
                    "정의 ID가 다른 동일 공개 층의 가정 시드 동일");
                foreach (FieldInfo field in typeof(BotCell).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                    Check(field.FieldType.Namespace != "Elements", "층 관찰에 정의 객체 참조 없음 " + field.Name);
                MethodInfo score = typeof(BotObservation).Assembly.GetType("AutoPlay.BotMissionEvaluation").GetMethod("Score", BindingFlags.Static | BindingFlags.NonPublic);
                Check((int)score.Invoke(null, new object[] { publicA, action, new HashSet<BoardCoordinate> { dust, web }, false, false, null }) == 16,
                    "등록 피해량2로 덮개/먼지 내구도2 완료 기여 평가");
                Check(beforeA == Snapshot(a.State) && beforeB == Snapshot(b.State), "공개 층 조회·가정 생성·평가가 원본/난수 무변경");
                branchType.GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(branch, new object[] { action });
                BoardActionExecutor assumed = (BoardActionExecutor)branchType.GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(branch);
                BoardActionResult actual = a.Activate(rocket);
                Check(actual.IsApplied, "실제 로켓 수락 " + actual.Reason + " " + actual.Message);
                Results.Add("INFO actual dust=" + a.State.CellAt(dust).DustDurability + " cover=" + a.State.CellAt(web).Cover +
                    " assumed dust=" + assumed.State.CellAt(dust).DustDurability + " cover=" + assumed.State.CellAt(web).Cover);
                Check(a.State.CellAt(dust).DustDurability == 0 && a.State.CellAt(web).Cover == null &&
                    assumed.State.CellAt(dust).DustDurability == 0 && assumed.State.CellAt(web).Cover == null,
                    "가정의 실제 로켓 피해도 등록된 덮개/먼지 피해량2 사용");
                MethodInfo finishTurn = typeof(LevelRuntimeState).Assembly.GetType("Simulation.MoldRules")
                    .GetMethod("FinishTurn", BindingFlags.Static | BindingFlags.NonPublic);
                foreach (BoardActionExecutor execution in new[] { a, assumed })
                {
                    TurnEffectContext context = (TurnEffectContext)Activator.CreateInstance(typeof(TurnEffectContext),
                        BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 1, Array.Empty<MatchedBlockChange>() }, null);
                    MoldSpreadRecord spread = (MoldSpreadRecord)finishTurn.Invoke(null, new object[] { execution.State, context });
                    Check(spread.Reason == MoldSpreadReason.Spread && execution.State.CellAt(spread.Target.Value).CoverDurability == 2,
                        "실제/가정 턴 종료는 공개 번식 초기 내구도2 적용");
                    RuntimeCell spawned = execution.State.CellAt(spread.Target.Value);
                    ElementDefinition spawnedDefinition = (ElementDefinition)typeof(RuntimeCell)
                        .GetProperty("CoverElement", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(spawned);
                    Check(spawnedDefinition.RequireLayer().Mission == MissionKind.Mold && spawnedDefinition.RequireTurn().InitialDurability == 2,
                        "번식한 층에 제거 미션과 다음 번식 값 유지");
                }
                Check(Snapshot(publicA) == Snapshot(publicB) && Snapshot(b.State) == beforeB,
                    "실제/가정 턴 실행 후 이전 공개 관찰과 비교 원본 유지");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }
    }
}
