using System;
using System.Collections.Generic;
using Board;
using Elements;
using Levels;

namespace Tutorial
{
    /// <summary>제작 시 알 수 있는 정적 조건만 검사한다. 후속 보드 상태는 실행 재생 검사에서 판단한다.</summary>
    public static class LevelTutorialValidator
    {
        public static IEnumerable<string> References(LevelTutorialDefinition tutorial, TutorialHandlerRegistry registry = null)
        {
            if (tutorial?.steps == null || tutorial.steps.Count == 0) yield break;
            registry ??= TutorialHandlerRegistry.CreateDefault();
            registry.Freeze();
            foreach (TutorialStepDefinition step in tutorial.steps)
            {
                if (step == null) continue;
                if (!registry.TryGetStep(step, out ITutorialStepHandler handler)) throw new ArgumentException("등록되지 않은 튜토리얼 행동입니다.");
                foreach (string id in handler.References(step)) yield return id;
                foreach (TutorialResultDefinition result in step.results ?? new List<TutorialResultDefinition>())
                {
                    if (result == null) continue;
                    if (!registry.TryGetResult(result.kind, out ITutorialResultEvaluator evaluator)) throw new ArgumentException("등록되지 않은 튜토리얼 조건입니다.");
                    foreach (string id in evaluator.References(result)) yield return id;
                }
            }
            foreach (ElementSupplySourceDefinition source in tutorial.supply?.sources ?? new List<ElementSupplySourceDefinition>())
                foreach (ElementSupplyItemDefinition item in source?.items ?? new List<ElementSupplyItemDefinition>())
                    if (item != null) yield return item.definitionId;
        }

        public static List<LevelValidationIssue> Validate(LevelDefinition level, ElementCatalog catalog = null, TutorialHandlerRegistry registry = null)
        {
            List<LevelValidationIssue> issues = new List<LevelValidationIssue>();
            LevelTutorialDefinition tutorial = level?.Tutorial;
            if (tutorial == null) return issues;
            void Error(string path, string message, BoardCoordinate? coordinate = null) =>
                issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidTutorial, message, path, coordinate));
            if (tutorial.steps == null) { Error("tutorial.steps", "단계 목록이 없습니다."); return issues; }
            if (tutorial.steps.Count == 0) return issues;
            catalog ??= level.CreateElementCatalog();
            registry ??= TutorialHandlerRegistry.CreateDefault(); registry.Freeze();
            TutorialValidationContext root = new TutorialValidationContext(level, catalog, "", Error);
            ElementDefinition Resolve(string id, string path, bool power = false) => root.Resolve(id, path, power);
            bool firstAction = true; int moves = 0;
            for (int i = 0; i < tutorial.steps.Count; i++)
            {
                TutorialStepDefinition step = tutorial.steps[i]; string path = $"tutorial.steps.Array.data[{i}]";
                TutorialValidationContext context = new TutorialValidationContext(level, catalog, path, Error, firstAction);
                ITutorialStepHandler handler = ValidateStep(step, registry, context);
                if (handler == null) continue;
                if (handler.ConsumesMove) moves++;
                if (!handler.IsDescription) firstAction = false;
            }
            if (moves > level.MoveCount) Error("tutorial.steps", "튜토리얼 교환 수가 레벨 이동 수를 초과합니다.");
            if (tutorial.supply?.sources == null) { Error("tutorial.supply", "고정 공급 목록이 없습니다."); return issues; }
            var seen = new HashSet<BoardCoordinate>();
            for (int i = 0; i < tutorial.supply.sources.Count; i++)
            {
                ElementSupplySourceDefinition source = tutorial.supply.sources[i]; string path = $"tutorial.supply.sources.Array.data[{i}]";
                if (source == null) { Error(path, "생성구가 null입니다."); continue; }
                string cellError = LevelSupplyRules.SourceCellError(level, source.coordinate);
                if (cellError != null) Error(path + ".coordinate", cellError, source.coordinate);
                if (!seen.Add(source.coordinate)) Error(path, "생성구가 중복입니다.");
                if (source.mode != SupplyMode.Fixed || source.exhaustion != SupplyExhaustion.Stop) Error(path, "튜토리얼은 고정 공급·소진 시 중단만 허용합니다.");
                if (source.items == null) { Error(path + ".items", "공급 항목이 없습니다."); continue; }
                for (int j = 0; j < source.items.Count; j++)
                {
                    ElementSupplyItemDefinition item = source.items[j]; string itemPath = path + $".items.Array.data[{j}]";
                    if (item == null) { Error(itemPath, "공급 항목이 null입니다."); continue; }
                    ElementDefinition definition = Resolve(item.definitionId, itemPath + ".definitionId");
                    if (item.count <= 0) Error(itemPath + ".count", "공급 수량은 양수입니다.");
                    if (definition?.Supply == null) Error(itemPath, "공급 생성 정의를 지정하세요.");
                    else if (definition.Supply.Behavior == ElementSupplyBehavior.RandomNormal || definition.Supply.Behavior == ElementSupplyBehavior.RandomPower) Error(itemPath, "무작위 공급은 사용할 수 없습니다.");
                }
            }
            // 기존 생성 정의의 색·방향·내구도 검사도 같은 규칙으로 수행한다.
            List<LevelValidationIssue> supplyIssues = new List<LevelValidationIssue>();
            ElementLevelSupplyLayout supply = new ElementLevelSupplyLayout(tutorial.supply, catalog, supplyIssues);
            for (int i = 0; i < supply.Value.Sources.Count; i++)
                for (int j = 0; j < supply.Value.Sources[i].Items.Count; j++)
                {
                    string error = supply.ItemError(level, i, j);
                    int sourceIndex = tutorial.supply.sources.FindIndex(source => source?.coordinate.Equals(supply.Value.Sources[i].Coordinate) == true);
                    if (error != null) Error($"tutorial.supply.sources.Array.data[{sourceIndex}].items.Array.data[{j}]", error);
                }
            foreach (LevelValidationIssue issue in supplyIssues)
                Error(issue.PropertyPath.Replace("elementSupply", "tutorial.supply"), issue.Message, issue.Coordinate);
            return issues;
        }

        // 저장 검사와 실행 준비가 같은 등록·설정 규칙을 사용한다.
        internal static ITutorialStepHandler ValidateStep(TutorialStepDefinition step, TutorialHandlerRegistry registry, TutorialValidationContext context)
        {
            if (step == null) { context.Error("", "단계가 null입니다."); return null; }
            if (string.IsNullOrWhiteSpace(step.instructions)) context.Error(".instructions", "안내 문구를 작성하세요.");
            if (step.highlights == null) context.Error(".highlights", "강조 목록이 없습니다.");
            else for (int j = 0; j < step.highlights.Count; j++) context.Cell(step.highlights[j], $".highlights.Array.data[{j}]");
            if (!registry.TryGetStep(step, out ITutorialStepHandler handler))
                context.Error(step.kind == TutorialStepKind.Item ? ".item" : ".kind", "등록되지 않은 단계·아이템 종류입니다.");
            else handler.Validate(step, context);
            if (step.results == null) context.Error(".results", "결과 조건 목록이 없습니다.");
            else for (int j = 0; j < step.results.Count; j++)
            {
                TutorialResultDefinition condition = step.results[j];
                TutorialValidationContext resultContext = context.ForChild($".results.Array.data[{j}]");
                if (condition == null) { resultContext.Error("", "결과 조건이 null입니다."); continue; }
                if (!registry.TryGetResult(condition.kind, out ITutorialResultEvaluator evaluator))
                    resultContext.Error("", "등록되지 않은 결과 조건 종류입니다.");
                else evaluator.Validate(condition, resultContext);
            }
            return handler;
        }
    }
}
