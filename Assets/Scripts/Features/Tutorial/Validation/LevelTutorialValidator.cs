using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Elements;
using Levels;
using Simulation;

namespace Tutorial
{
    /// <summary>제작 시 알 수 있는 정적 조건만 검사한다. 후속 보드 상태는 실행 재생 검사에서 판단한다.</summary>
    public static class LevelTutorialValidator
    {
        public static IEnumerable<string> References(LevelTutorialDefinition tutorial)
        {
            if (tutorial?.steps == null || tutorial.steps.Count == 0) yield break;
            foreach (TutorialStepDefinition step in tutorial.steps)
            {
                if (step == null) continue;
                if (step.kind == TutorialStepKind.PowerSwap) yield return step.actionDefinitionId;
                foreach (TutorialResultDefinition result in step.results ?? new List<TutorialResultDefinition>())
                    if (result != null) yield return result.definitionId;
            }
            foreach (ElementSupplySourceDefinition source in tutorial.supply?.sources ?? new List<ElementSupplySourceDefinition>())
                foreach (ElementSupplyItemDefinition item in source?.items ?? new List<ElementSupplyItemDefinition>())
                    if (item != null) yield return item.definitionId;
        }

        public static List<LevelValidationIssue> Validate(LevelDefinition level, ElementCatalog catalog = null)
        {
            List<LevelValidationIssue> issues = new List<LevelValidationIssue>();
            LevelTutorialDefinition tutorial = level?.Tutorial;
            if (tutorial == null) return issues;
            void Error(string path, string message, BoardCoordinate? coordinate = null) =>
                issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidTutorial, message, path, coordinate));
            if (tutorial.steps == null) { Error("tutorial.steps", "단계 목록이 없습니다."); return issues; }
            if (tutorial.steps.Count == 0) return issues;
            catalog ??= level.CreateElementCatalog();
            ElementDefinition Resolve(string id, string path, bool power = false)
            {
                try
                {
                    ElementDefinition definition = catalog.Get(new ElementId(id));
                    PackedElementDefinition.ValidateDefinition(definition);
                    if (power && definition.Supply?.Behavior != ElementSupplyBehavior.Power)
                        throw new ArgumentException("파워 생성 정의를 지정하세요.");
                    return definition;
                }
                catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is KeyNotFoundException)
                { Error(path, $"정의 '{id}': {error.Message}"); return null; }
            }
            void Cell(BoardCoordinate coordinate, string path)
            {
                string error = LevelPlacementRules.CellError(level, coordinate);
                if (error != null) Error(path, error, coordinate);
            }
            bool firstAction = true; int moves = 0;
            for (int i = 0; i < tutorial.steps.Count; i++)
            {
                TutorialStepDefinition step = tutorial.steps[i]; string path = $"tutorial.steps.Array.data[{i}]";
                if (step == null) { Error(path, "단계가 null입니다."); continue; }
                if (!Enum.IsDefined(typeof(TutorialStepKind), step.kind)) Error(path + ".kind", "단계 종류가 잘못됐습니다.");
                if (string.IsNullOrWhiteSpace(step.instructions)) Error(path + ".instructions", "안내 문구를 작성하세요.");
                if (step.highlights == null) Error(path + ".highlights", "강조 목록이 없습니다.");
                else for (int j = 0; j < step.highlights.Count; j++) Cell(step.highlights[j], path + $".highlights.Array.data[{j}]");
                bool pair = step.kind == TutorialStepKind.Swap || step.kind == TutorialStepKind.PowerSwap || step.kind == TutorialStepKind.Item && step.item == BoardItem.Swap;
                bool target = pair || step.kind == TutorialStepKind.Item && step.item == BoardItem.Hammer;
                if (target)
                {
                    if (!step.hasFirst) Error(path + ".hasFirst", "첫 번째 대상 칸이 필요합니다.");
                    else Cell(step.first, path + ".first");
                }
                else if (step.hasFirst || step.hasSecond) Error(path, "설명·섞기 단계는 행동 대상이 없습니다.");
                if (pair)
                {
                    if (!step.hasSecond) Error(path + ".hasSecond", "두 번째 대상 칸이 필요합니다.");
                    else Cell(step.second, path + ".second");
                    if (step.hasFirst && step.hasSecond)
                    {
                        BoardEdge edge = new BoardEdge(step.first, step.second);
                        if (!edge.IsAdjacent) Error(path + ".second", "교환 대상은 인접해야 합니다.", step.second);
                        else if (level.Flow?.Walls?.Contains(edge) == true) Error(path + ".second", "벽을 통과하는 교환입니다.", step.second);
                    }
                }
                else if (target && step.hasSecond) Error(path + ".hasSecond", "망치는 한 칸만 선택합니다.");
                if (step.kind == TutorialStepKind.Swap || step.kind == TutorialStepKind.PowerSwap) moves++;
                if (step.kind == TutorialStepKind.Item && !Enum.IsDefined(typeof(BoardItem), step.item)) Error(path + ".item", "아이템 종류가 잘못됐습니다.");
                ElementDefinition power = step.kind == TutorialStepKind.PowerSwap ? Resolve(step.actionDefinitionId, path + ".actionDefinitionId", true) : null;
                if (firstAction && step.kind != TutorialStepKind.Description)
                {
                    firstAction = false;
                    // 첫 조작의 초기 대상만 조회한다. 나중에 생성될 파워는 초기 배치를 요구하지 않는다.
                    IReadOnlyList<ElementPlacementDefinition> placements = level.Elements;
                    try { if (level.SchemaVersion == 4) placements = LegacyElementLevelAdapter.Preview(level); }
                    catch (Exception error) when (error is ArgumentException || error is NullReferenceException)
                    { Error(path, "초기 배치 데이터 오류: " + error.Message); placements = Array.Empty<ElementPlacementDefinition>(); }
                    foreach (BoardCoordinate coordinate in target ? pair ? new[] { step.first, step.second } : new[] { step.first } : Array.Empty<BoardCoordinate>())
                    {
                        ElementPlacementDefinition placement = placements?.FirstOrDefault(item => item?.layer == PlacementLayer.Block && item.coordinate.Equals(coordinate));
                        if (pair)
                        {
                            ElementDefinition definition = placement == null ? null : Resolve(placement.definitionId, path);
                            if (definition?.Supply?.Behavior != ElementSupplyBehavior.FixedNormal && definition?.Supply?.Behavior != ElementSupplyBehavior.Power)
                                Error(path, "첫 교환은 초기 고정 블록 또는 파워를 대상으로 지정하세요.", coordinate);
                            if (placements?.Any(item => item?.layer == PlacementLayer.Cover && item.coordinate.Equals(coordinate)) == true)
                                Error(path, "덮개가 있는 초기 블록은 교환할 수 없습니다.", coordinate);
                        }
                        else if (placement == null && placements?.Any(item => item != null && LevelPlacementRules.Footprint(item.coordinate,
                            Resolve(item.definitionId, path)?.Placement?.Size ?? Resolve(item.definitionId, path)?.ChargePlacement?.Size ?? 1).Contains(coordinate)) != true)
                            Error(path, "첫 망치 대상에 제거·피해를 받을 배치가 없습니다.", coordinate);
                    }
                    if (power != null && placements?.Any(item => item?.layer == PlacementLayer.Block && item.coordinate.Equals(step.first) && item.definitionId == power.Id.Value) != true)
                        Error(path + ".first", "첫 파워 교환의 첫 칸에 지정 파워가 없습니다.", step.first);
                }
                if (step.results == null) Error(path + ".results", "결과 조건 목록이 없습니다.");
                else for (int j = 0; j < step.results.Count; j++)
                {
                    TutorialResultDefinition condition = step.results[j]; string resultPath = path + $".results.Array.data[{j}]";
                    if (condition == null) { Error(resultPath, "결과 조건이 null입니다."); continue; }
                    if (!Enum.IsDefined(typeof(TutorialResultKind), condition.kind) || condition.count <= 0) Error(resultPath, "조건 종류·수량이 잘못됐습니다.");
                    Resolve(condition.definitionId, resultPath + ".definitionId", condition.kind != TutorialResultKind.Removed);
                    if (condition.hasCoordinate) Cell(condition.coordinate, resultPath + ".coordinate");
                }
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
    }
}
