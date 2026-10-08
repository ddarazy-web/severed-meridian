using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEngine;

namespace Tutorial.Editor
{
    public static partial class TutorialComposerConditionsVerification
    {
        private static void VerifyGeneratedBindings()
        {
            Check(typeof(TutorialConditionDefinition).GetField("bindGeneratedAs") != null, "생성 조건의 연결 이름 제공");
            object Invoke(string method, params object[] args) => typeof(FixedObstacleVerification).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            LevelDefinition level = (LevelDefinition)Invoke("Make");
            try
            {
                Invoke("Place", level, new BoardCoordinate(4, 0), InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1);
                Invoke("Place", level, new BoardCoordinate(4, 3), InitialBlockKind.Rocket, RocketDirection.Vertical, RabbitColor.Type1);
                LevelRuntimeState state = (LevelRuntimeState)Invoke("Build", level, 12345);
                var entities = TutorialTargetQuery.Capture(state);
                TutorialTargetEntity[] rockets = entities.Where(value => value.Definition.Supply?.Content == RuntimeContent.Rocket).ToArray();
                foreach (string scenario in new[] { "single", "ambiguous", "lost" })
                {
                    var generated = JsonUtility.FromJson<TutorialConditionDefinition>("{\"kind\":5,\"requiredCount\":1,\"powerDefinitionId\":\"" + rockets[0].Definition.Id.Value + "\",\"anyDirection\":true,\"target\":{\"kind\":0},\"bindGeneratedAs\":\"새 로켓\"}");
                    var first = TutorialSampleCatalog.CreateSwap(new BoardCoordinate(0, 0), new BoardCoordinate(0, 1), false);
                    first.conditions.Clear(); first.conditions.Add(generated);
                    var next = TutorialSampleCatalog.CreateSwap(new BoardCoordinate(1, 0), new BoardCoordinate(1, 1), false);
                    next.conditions.Clear(); next.conditions.Add(new TutorialConditionDefinition { kind = TutorialConditionKind.Activated,
                        powerDefinitionId = rockets[0].Definition.Id.Value,
                        target = new TutorialTargetDefinition { kind = TutorialTargetKind.Generated, binding = "새 로켓" } });
                    var definition = new LevelTutorialDefinition(); definition.steps.Add(first); definition.steps.Add(next);
                    if (scenario == "single")
                    {
                        int writes = 0;
                        var context = new TutorialConditionContext(entities, new System.Collections.Generic.Dictionary<string, long>(), (_, __) => writes++);
                        ITutorialConditionState original = new TutorialPowerConditionEvaluator(TutorialConditionKind.Generated).Create(generated, context);
                        ITutorialConditionState fork = original.Fork();
                        fork.Accept(new TutorialConditionEvent(TutorialConditionKind.Generated, "preview", rockets[0], rockets[0].Cells[0], 1, EffectOrigin.Unknown)); fork.Refresh(entities);
                        Check(fork.IsSatisfied && original.Count == 0 && writes == 0, "생성 안내 사본은 원본 집계나 생성 연결 이름을 변경하지 않음");
                        level.Tutorial.steps.Clear(); level.Tutorial.steps.AddRange(definition.steps);
                        LevelDefinition packed = LevelPackCodec.ReadLevel(LevelPackCodec.Snapshot(level), level.LevelNumber);
                        try { Check(JsonUtility.ToJson(packed.Tutorial.steps[0]) == JsonUtility.ToJson(first), "생성 연결 이름 팩 왕복"); }
                        finally { UnityEngine.Object.DestroyImmediate(packed); }
                        next.conditions[0].target.binding = "없는 이름";
                        Check(LevelTutorialValidator.Validate(level).Any(issue => issue.PropertyPath.EndsWith(".target.binding")), "이전 단계에 없는 생성 연결은 정적 오류");
                        next.conditions[0].target.binding = "새 로켓";
                        next.conditions.Add(generated.Copy());
                        Check(LevelTutorialValidator.Validate(level).Any(issue => issue.PropertyPath.EndsWith(".bindGeneratedAs")), "생성 연결 이름 중복은 정적 오류");
                        next.conditions.RemoveAt(1);
                    }
                    bool lost = false;
                    using TutorialProgress progress = new TutorialProgress(definition, readTargets: () => lost ? entities.Where(value => value.Occurrence != rockets[0].Occurrence).ToArray() : entities);
                    Check(progress.TryApprove(TutorialInput.Swap(first.first, first.second), out TutorialActionTicket ticket), "생성 연결 단계 승인 " + scenario);
                    progress.ReportCompletion(ticket, true);
                    progress.ReportConditionEvents(ticket, new[] { new TutorialConditionEvent(TutorialConditionKind.Generated, "generated1", rockets[0], rockets[0].Cells[0], 1, EffectOrigin.Unknown) });
                    if (scenario == "ambiguous")
                        progress.ReportConditionEvents(ticket, new[] { new TutorialConditionEvent(TutorialConditionKind.Generated, "generated2", rockets[1], rockets[1].Cells[0], 1, EffectOrigin.Unknown) });
                    lost = scenario == "lost";
                    progress.ReportResultsComplete(ticket); progress.ReportPresentationComplete(ticket);
                    if (scenario != "single")
                    { Check(progress.State == TutorialProgressState.Error, "모호한 생성 또는 연쇄 소실 개체를 임의로 연결하지 않음 " + scenario); continue; }
                    Check(progress.StepIndex == 1 && progress.State == TutorialProgressState.AwaitAction, "생성 결과 이름으로 다음 단계 같은 개체 선택");
                    Check(progress.Snapshot.Highlights.Contains(rockets[0].Cells[0]), "생성 연결 조건 대상도 다음 단계에서 자동 강조");
                    progress.TryApprove(TutorialInput.Swap(next.first, next.second), out ticket); progress.ReportCompletion(ticket, true);
                    progress.ReportConditionEvents(ticket, new[] { new TutorialConditionEvent(TutorialConditionKind.Activated, "other", rockets[1], new BoardCoordinate(5, 0), 1, EffectOrigin.Rocket, true) });
                    Check(progress.Snapshot.ConditionCounts[0] == 0, "다른 로켓은 연결 대상 대신 집계하지 않음");
                    progress.ReportConditionEvents(ticket, new[] { new TutorialConditionEvent(TutorialConditionKind.Activated, "bound", rockets[0], new BoardCoordinate(5, 0), 1, EffectOrigin.Rocket, true) });
                    progress.ReportResultsComplete(ticket); progress.ReportPresentationComplete(ticket);
                    Check(progress.State == TutorialProgressState.Completed, "연결 개체는 사건 위치가 달라져도 추적");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void VerifyPowerConditions()
        {
            Check(Enum.GetNames(typeof(TutorialConditionKind)).Contains("Generated"), "공통 파워 생성 조건 등록");
            object Invoke(Type owner, string method, params object[] args) => owner.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", 6, RocketDirection.Horizontal, null);
            LevelDefinition restored = null;
            try
            {
                string rocket = level.CreateElementCatalog().Definitions.First(value => value.Supply?.Content == RuntimeContent.Rocket).Id.Value;
                var step = TutorialSampleCatalog.CreateSwap(new BoardCoordinate(4, 4), new BoardCoordinate(4, 5), false);
                step.conditions.Clear(); level.Tutorial.steps.Clear(); level.Tutorial.steps.Add(step);
                foreach (string condition in new[] {
                    "{\"kind\":5,\"requiredCount\":1,\"powerDefinitionId\":\"" + rocket + "\",\"anyDirection\":true,\"target\":{\"kind\":0}}",
                    "{\"kind\":6,\"requiredCount\":1,\"powerDefinitionId\":\"" + rocket + "\",\"anyDirection\":true,\"target\":{\"kind\":0}}",
                    "{\"kind\":7,\"requiredCount\":1,\"allowedOrigins\":[13],\"target\":{\"kind\":0}}" })
                    step.conditions.Add(JsonUtility.FromJson<TutorialConditionDefinition>(condition));
                for (int direction = 0; direction < 2; direction++)
                {
                    TutorialConditionDefinition directed = step.conditions[0].Copy();
                    directed.anyDirection = false; directed.rocketDirection = (RocketDirection)direction; step.conditions.Add(directed);
                }
                TutorialConditionDefinition area = step.conditions[0].Copy();
                area.target.kind = TutorialTargetKind.Area; area.target.cells.Add(new BoardCoordinate(0, 0)); step.conditions.Add(area);
                restored = LevelPackCodec.ReadLevel(LevelPackCodec.Snapshot(level), level.LevelNumber);
                Check(JsonUtility.ToJson(restored.Tutorial.steps[0]) == JsonUtility.ToJson(step), "파워 조건 세부값 팩 왕복");
                LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(CombinationVerification), "Build", restored, 12345);
                using TutorialBoardAdapter adapter = new TutorialBoardAdapter(restored, new BoardActionExecutor(state));
                Check(adapter.TryBegin(TutorialInput.Swap(step.first, step.second)), "생성/발동/조합 조건 실제 교환 승인");
                BoardActionResult action = adapter.Executor.Swap(step.first, step.second); adapter.ReportAction(action.IsApplied);
                Check(adapter.Progress.Snapshot.ConditionCounts[0] > 0 && adapter.Progress.Snapshot.ConditionCounts[1] == 0 && adapter.Progress.Snapshot.ConditionCounts[2] == 1,
                    "변환 생성은 집계하고 조합을 단독 발동으로 중복 집계하지 않음");
                var transformations = adapter.Executor.TurnEffects.Combination.Transformations;
                for (int direction = 0; direction < 2; direction++)
                    Check(adapter.Progress.Snapshot.ConditionCounts[3 + direction] == transformations.Count(value => value.Direction == (RocketDirection)direction),
                        "생성 시점 방향별 로켓 수 실제 결과와 일치 " + (RocketDirection)direction);
                Check(adapter.Progress.Snapshot.ConditionCounts[5] == transformations.Count(value => value.Coordinate.Equals(new BoardCoordinate(0, 0))),
                    "생성 조건 영역은 실제 생성 위치로 집계");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); if (restored != null) UnityEngine.Object.DestroyImmediate(restored); }
            level = (LevelDefinition)Invoke(typeof(FixedObstacleVerification), "Make");
            try
            {
                Invoke(typeof(FixedObstacleVerification), "Place", level, new BoardCoordinate(4, 0), InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1);
                Invoke(typeof(FixedObstacleVerification), "Place", level, new BoardCoordinate(4, 3), InitialBlockKind.Bomb, RocketDirection.Horizontal, RabbitColor.Type1);
                var step = TutorialSampleCatalog.CreateSwap(new BoardCoordinate(4, 0), new BoardCoordinate(4, 1), false);
                step.conditions.Clear(); level.Tutorial.steps.Clear(); level.Tutorial.steps.Add(step);
                foreach (RuntimeContent content in new[] { RuntimeContent.Rocket, RuntimeContent.Bomb })
                    step.conditions.Add(new TutorialConditionDefinition { kind = TutorialConditionKind.Activated,
                        powerDefinitionId = level.CreateElementCatalog().Definitions.First(value => value.Supply?.Content == content).Id.Value,
                        target = new TutorialTargetDefinition { kind = TutorialTargetKind.Board } });
                LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", level, 12345);
                using TutorialBoardAdapter adapter = new TutorialBoardAdapter(level, new BoardActionExecutor(state));
                Check(adapter.TryBegin(TutorialInput.Swap(step.first, step.second)), "직접 파워 교환 승인");
                BoardActionResult action = adapter.Executor.Swap(step.first, step.second); adapter.ReportAction(action.IsApplied);
                Check(action.IsApplied && action.Effects.Any(value => value.Origin == EffectOrigin.Bomb), "실제 로켓이 기존 폭탄을 연쇄 발동");
                Check(adapter.Progress.Snapshot.ConditionCounts.SequenceEqual(new[] { 1, 0 }), "직접 로켓 발동만1회 인정하며 피격 폭탄 연쇄 발동은0회");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void VerifyDamageConditions()
        {
            Check(Enum.GetNames(typeof(TutorialConditionKind)).Contains("DurabilityDecrease"), "내구도 감소 조건 등록");
            object Invoke(Type owner, string method, params object[] args) => owner.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            foreach (int kind in new[] { 2, 3, 4 })
            {
                LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", 1, RocketDirection.Horizontal, null);
                LevelDefinition restored = null;
                try
                {
                    Invoke(typeof(FixedObstacleVerification), "Obstacle", level, ObstacleKind.Appliance, 4, new BoardCoordinate(3, 6), RabbitColor.Type1);
                    Invoke(typeof(FixedObstacleVerification), "SetMission", level, ObstacleKind.Appliance);
                    string condition = "{\"kind\":" + kind + ",\"requiredCount\":" + (kind == 2 ? 4 : kind == 3 ? 2 : 1) +
                        ",\"target\":{\"kind\":1,\"coordinate\":{\"row\":3,\"column\":6}},\"allowedOrigins\":[8]}";
                    JsonUtility.FromJsonOverwrite("{\"tutorial\":{\"seed\":12345,\"steps\":[{\"kind\":1,\"instructions\":\"교환\",\"hasFirst\":true,\"first\":{\"row\":4,\"column\":4},\"hasSecond\":true,\"second\":{\"row\":4,\"column\":5},\"conditions\":[" + condition + "]}]}}", level);
                    if (kind == 3)
                    {
                        level.Tutorial.steps[0].conditions[0].requiredCount = 4;
                        level.Tutorial.steps.Add(JsonUtility.FromJson<TutorialStepDefinition>(JsonUtility.ToJson(level.Tutorial.steps[0])));
                        JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", level);
                        Check(!LevelTutorialValidator.Validate(level).Any(issue => issue.Message.Contains("교환 수")), "이미 만족한 상태 조건 두 단계에 교환2회를 요구하지 않음");
                        level.Tutorial.steps.RemoveAt(1); level.Tutorial.steps[0].conditions[0].requiredCount = 2;
                        JsonUtility.FromJsonOverwrite("{\"moveCount\":20}", level);
                    }
                    if (kind == 4)
                        foreach (EffectOrigin single in new[] { EffectOrigin.Rocket, EffectOrigin.Bomb })
                        {
                            TutorialConditionDefinition standalone = level.Tutorial.steps[0].conditions[0].Copy();
                            standalone.allowedOrigins.Clear(); standalone.allowedOrigins.Add(single);
                            level.Tutorial.steps[0].conditions.Add(standalone);
                        }
                    byte[] bytes = LevelPackCodec.Snapshot(level);
                    restored = LevelPackCodec.ReadLevel(bytes, level.LevelNumber);
                    Check(bytes[4] == 5 && JsonUtility.ToJson(restored.Tutorial.steps[0]) == JsonUtility.ToJson(level.Tutorial.steps[0]), "대상·원인 조건 팩5 왕복 " + kind);
                    LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(FixedObstacleVerification), "Build", restored, 12345);
                    using TutorialBoardAdapter adapter = new TutorialBoardAdapter(restored, new BoardActionExecutor(state));
                    Check(adapter.TryBegin(TutorialInput.Swap(new BoardCoordinate(4, 4), new BoardCoordinate(4, 5))), "새 조건 실제 조합 교환 승인 " + kind);
                    BoardActionResult action = adapter.Executor.Swap(new BoardCoordinate(4, 4), new BoardCoordinate(4, 5));
                    adapter.ReportAction(action.IsApplied);
                    Check(adapter.Progress.Snapshot.ConditionCounts[0] == (kind == 2 ? 4 : kind == 3 ? 0 : 1), "실제 감소/남은 값/제거 집계 " + kind);
                    if (kind == 4)
                        Check(adapter.Progress.Snapshot.ConditionCounts.SequenceEqual(new[] { 1, 0, 0 }), "로켓+폭탄 제거는 조합만 인정하고 단독 로켓·폭탄 조건에 중복 반영하지 않음");
                    Check(adapter.Progress.State == TutorialProgressState.AwaitPresentation, "조건 집계 후에도 실제 연출 완료 전 진행 금지 " + kind);
                }
                finally { UnityEngine.Object.DestroyImmediate(level); if (restored != null) UnityEngine.Object.DestroyImmediate(restored); }
            }
        }

        private static void VerifyConditionStateBoundaries()
        {
            byte[] legacy = File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-v4.bytes");
            LevelDefinition restored = LevelPackCodec.ReadLevel(legacy, 1);
            try { Check(legacy.SequenceEqual(LevelPackCodec.Snapshot(restored)), "팩4 fixture 바이트 그대로 재저장"); }
            finally { UnityEngine.Object.DestroyImmediate(restored); }
            object Invoke(string method, params object[] args) => typeof(FixedObstacleVerification).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            LevelDefinition level = (LevelDefinition)Invoke("Make");
            try
            {
                Invoke("Obstacle", level, ObstacleKind.Appliance, 3, new BoardCoordinate(4, 4), RabbitColor.Type1);
                Invoke("Obstacle", level, ObstacleKind.Appliance, 3, new BoardCoordinate(7, 7), RabbitColor.Type1);
                Invoke("SetMission", level, ObstacleKind.Appliance);
                LevelRuntimeState state = (LevelRuntimeState)Invoke("Build", level, 12345);
                TutorialTargetEntity[] entities = TutorialTargetQuery.Capture(state).Where(value => value.Durability.HasValue).ToArray();
                LevelTutorialDefinition Definition(TutorialConditionDefinition condition) => new LevelTutorialDefinition
                {
                    steps = new System.Collections.Generic.List<TutorialStepDefinition> { new TutorialStepDefinition
                    { kind = TutorialStepKind.Swap, instructions = "교환", hasFirst = true, first = new BoardCoordinate(0, 0), hasSecond = true, second = new BoardCoordinate(0, 1),
                        conditions = new System.Collections.Generic.List<TutorialConditionDefinition> { condition } } }
                };
                foreach (TutorialDamageAggregation mode in Enum.GetValues(typeof(TutorialDamageAggregation)))
                {
                    var condition = new TutorialConditionDefinition { kind = TutorialConditionKind.DurabilityDecrease, requiredCount = 2, aggregation = mode,
                        target = new TutorialTargetDefinition { kind = TutorialTargetKind.Definition, definitionId = entities[0].Definition.Id.Value } };
                    ITutorialConditionState original = new TutorialDurabilityConditionEvaluator().Create(condition,
                        new TutorialConditionContext(entities, new System.Collections.Generic.Dictionary<string, long>()));
                    ITutorialConditionState fork = original.Fork();
                    fork.Accept(new TutorialConditionEvent(TutorialConditionKind.DurabilityDecrease, "preview", entities[0], entities[0].Cells[0], 2, EffectOrigin.Rocket));
                    Check(original.ProgressValue == 0 && fork.ProgressValue == 2, "감소 안내 사본과 원본 집계 분리 " + mode);
                    using TutorialProgress progress = new TutorialProgress(Definition(condition), readTargets: () => TutorialTargetQuery.Capture(state));
                    progress.TryApprove(TutorialInput.Swap(new BoardCoordinate(0, 0), new BoardCoordinate(0, 1)), out TutorialActionTicket ticket);
                    progress.ReportCompletion(ticket, true);
                    progress.ReportConditionEvents(ticket, new[] { new TutorialConditionEvent(TutorialConditionKind.DurabilityDecrease, "hit1", entities[0], entities[0].Cells[0], 2, EffectOrigin.Rocket) });
                    progress.ReportResultsComplete(ticket); progress.ReportPresentationComplete(ticket);
                    Check(progress.State == (mode == TutorialDamageAggregation.Total ? TutorialProgressState.Completed : TutorialProgressState.AwaitAction), "합계/각 대상마다 집계 구분 " + mode);
                    if (mode == TutorialDamageAggregation.Each)
                    {
                        progress.TryApprove(TutorialInput.Swap(new BoardCoordinate(0, 0), new BoardCoordinate(0, 1)), out ticket); progress.ReportCompletion(ticket, true);
                        progress.ReportConditionEvents(ticket, new[] { new TutorialConditionEvent(TutorialConditionKind.DurabilityDecrease, "hit2", entities[1], entities[1].Cells[0], 2, EffectOrigin.Rocket) });
                        progress.ReportResultsComplete(ticket); progress.ReportPresentationComplete(ticket);
                        Check(progress.State == TutorialProgressState.Completed, "다음 행동에서 남은 개체 감소량을 누적하여 완료");
                    }
                    condition.requiredCount = mode == TutorialDamageAggregation.Total ? 7 : 4;
                    using TutorialProgress invalid = new TutorialProgress(Definition(condition), readTargets: () => TutorialTargetQuery.Capture(state));
                    Check(invalid.State == TutorialProgressState.Error, "현재 내구도보다 큰 감소 요구 진단 " + mode);
                }
                var exact = new TutorialConditionDefinition { kind = TutorialConditionKind.RemainingDurability, requiredCount = 3,
                    target = new TutorialTargetDefinition { kind = TutorialTargetKind.Entity, coordinate = new BoardCoordinate(4, 4) } };
                using (TutorialProgress already = new TutorialProgress(Definition(exact), readTargets: () => TutorialTargetQuery.Capture(state)))
                    Check(already.State == TutorialProgressState.Completed, "이미 정확한 내구도를 만족하면 추가 조작 없이 완료");
                exact.requiredCount = 2;
                using TutorialProgress remaining = new TutorialProgress(Definition(exact), readTargets: () => TutorialTargetQuery.Capture(state));
                remaining.TryApprove(TutorialInput.Swap(new BoardCoordinate(0, 0), new BoardCoordinate(0, 1)), out TutorialActionTicket action);
                remaining.ReportCompletion(action, true);
                typeof(RuntimeObstacle).GetProperty("Durability").SetValue(state.Obstacles[state.CellAt(new BoardCoordinate(4, 4)).ObstacleIndex.Value], 1);
                TutorialTargetEntity current = TutorialTargetQuery.Capture(state).Single(value => value.Occurrence == entities[0].Occurrence);
                remaining.ReportConditionEvents(action, new[] { new TutorialConditionEvent(TutorialConditionKind.DurabilityDecrease, "overshoot", current, current.Cells[0], 2, EffectOrigin.Rocket) });
                remaining.ReportResultsComplete(action); remaining.ReportPresentationComplete(action);
                Check(remaining.State == TutorialProgressState.AwaitAction && remaining.Snapshot.ConditionCounts.Single() == 1, "3→1은 정확히2 조건을 충족하지 않음");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }
    }
}
