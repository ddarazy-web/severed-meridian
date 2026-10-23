#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Runtime;
using Levels;
using Simulation;
using Tutorial;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private void DrawTutorialSummaries()
        {
            try
            {
                using var graph = new AuthoringObjectGraph(Session);
                LevelDefinition level = (LevelDefinition)graph.Resolve(Session.SelectedLevelId);
                var steps = TutorialFlowResolver.Resolve(level).steps;
                for (int i = 0; i < steps.Count; i++)
                {
                    int index = i;
                    TutorialStepDefinition step = steps[i];
                    var card = new VisualElement(); card.AddToClassList("tutorial-step-card"); inspector.Add(card);
                    card.Add(new Label((i + 1) + ". " + TutorialStepSummary(step)) { name = "tutorial-summary-" + i, userData = step, style = { whiteSpace = WhiteSpace.Normal } });
                    Button(card, "tutorial-locate-step-" + i, "위치 보기", () =>
                    {
                        tutorialStep = index;
                        var cells = step.highlights.ToList();
                        if (step.hasFirst && string.IsNullOrEmpty(step.firstBinding)) cells.Add(step.first);
                        if (step.hasSecond && string.IsNullOrEmpty(step.secondBinding)) cells.Add(step.second);
                        cells.AddRange(step.actionArea);
                        Session.SelectCells(cells.Select(cell => cell.Row * 9 + cell.Column).Distinct());
                        forceProperties = true; Refresh();
                        Show(string.IsNullOrEmpty(step.firstBinding) && string.IsNullOrEmpty(step.secondBinding) ? "선택 단계의 보드 위치를 표시했습니다." : "생성 결과 위치는 게임 시험에서 결정됩니다. 고정된 조작 칸만 표시했습니다.");
                    });
                    Button(card, "tutorial-edit-step-" + i, "이 단계 수정", () => { tutorialStep = index; tutorialAdvanced = true; RefreshProperties(); });
                    Button(card, "tutorial-trial-step-" + i, "여기까지 시험", () =>
                    {
                        if (tutorialPick != null || modal != null || !CommitCommandInput() || committingCommandInput) return;
                        BeginPlayCore(index).Forget();
                    }).tooltip = "현재 편집본 JSON의 초기 보드와 첫 단계부터 이 단계까지 시험합니다. 파일은 저장하지 않습니다.";
                }
                var issues = LevelTutorialValidator.Validate(level);
                if (issues.Count > 0)
                    inspector.Add(new Label("완료 전 확인 필요\n" + string.Join("\n", issues.Select(issue => issue.Message)))
                        { name = "tutorial-link-errors", style = { whiteSpace = WhiteSpace.Normal } });
            }
            catch (Exception error)
            {
                inspector.Add(new Label("연결 확인 필요: " + error.Message) { name = "tutorial-link-errors", style = { whiteSpace = WhiteSpace.Normal } });
            }
        }

        private void RefreshTutorialSummaryInstructions()
        {
            if (sharedTutorialDraft != null || !string.IsNullOrEmpty((string)displayedLevel["tutorial"]?["flowId"])) return;
            var steps = displayedLevel["tutorial"]?["steps"];
            if (steps == null) return;
            for (int i = 0; i < steps.Count(); i++)
            {
                Label label = inspector.Q<Label>("tutorial-summary-" + i);
                if (label?.userData is not TutorialStepDefinition step) continue;
                step.instructions = (string)steps[i]["instructions"] ?? "";
                label.text = (i + 1) + ". " + TutorialStepSummary(step);
            }
        }

        private string TutorialStepSummary(TutorialStepDefinition step)
        {
            string Definition(string id) => definitions.TryGetValue(id ?? "", out var definition) ? (string)definition["displayName"] : id + " (정의 확인 필요)";
            string ColorName(RabbitColor color) => color switch
            {
                RabbitColor.Type1 => "분홍", RabbitColor.Type2 => "노랑", RabbitColor.Type3 => "파랑",
                RabbitColor.Type4 => "초록", RabbitColor.Type5 => "보라", _ => color.ToString()
            };
            string Condition(TutorialConditionDefinition condition)
            {
                string text = TutorialAuthoringRules.ConditionLabel(condition.kind) + " " + condition.requiredCount +
                    (condition.kind == TutorialConditionKind.RemainingDurability ? " (정확히)" : "회/개");
                if (condition.kind == TutorialConditionKind.Match) text += " · " + condition.matchSize + "개 " +
                    (condition.sizeComparison == TutorialMatchSizeComparison.Exactly ? "정확히 매칭" : "이상 매칭") + " · " + (condition.origin == TutorialMatchOrigin.DirectSwap ? "직접 교환만" : "연쇄 포함") +
                    " · " + (condition.anyColor ? "색 무관" : ColorName(condition.color) + " 블록만");
                if (condition.kind == TutorialConditionKind.DurabilityDecrease)
                    text += condition.aggregation == TutorialDamageAggregation.Total ? " · 모든 대상 합계" : " · 각 대상마다";
                if (!string.IsNullOrEmpty(condition.powerDefinitionId)) text += " · " +
                    Definition(condition.powerDefinitionId);
                if (condition.kind == TutorialConditionKind.Generated || condition.kind == TutorialConditionKind.Activated)
                    text += " · " + (condition.anyDirection ? "방향 무관" : condition.rocketDirection == RocketDirection.Horizontal ? "가로 방향만" : "세로 방향만");
                if (condition.kind == TutorialConditionKind.Activated) text += " · 조합·연쇄 발동 제외";
                if (condition.kind == TutorialConditionKind.ItemUsed) text += " · " + Presentation.LevelToolPanelFields.ItemLabel(condition.item);
                if (condition.kind == TutorialConditionKind.MissionProgress) text += " · " + (condition.missionIndex + 1) + "번 미션";
                if (condition.allowedOrigins?.Count > 0) text += " · 인정 원인: " + string.Join(", ", condition.allowedOrigins.Select(TutorialSampleCatalog.OriginLabel));
                else if (condition.kind == TutorialConditionKind.DurabilityDecrease) text += " · 모든 피해 원인";
                if (condition.target != null)
                    text += " · 대상: " + (condition.target.kind == TutorialTargetKind.Board ? "보드 전체" : condition.target.kind == TutorialTargetKind.Entity ? condition.target.coordinate.ToString() :
                        condition.target.kind == TutorialTargetKind.Generated ? "생성 결과 ‘" + condition.target.binding + "’" : condition.target.kind == TutorialTargetKind.Area ? "선택 영역 " + condition.target.cells.Count + "칸" : Definition(condition.target.definitionId)) +
                        (condition.kind < TutorialConditionKind.Generated ? " / " + (condition.target.layer == TutorialTargetLayer.Cover ? "덮개" : condition.target.layer == TutorialTargetLayer.Floor ? "바닥" : "블록·장애물") : "");
                if (!string.IsNullOrEmpty(condition.bindGeneratedAs)) text += " · 다음 단계 연결: " + condition.bindGeneratedAs;
                return text;
            }
            string first = string.IsNullOrEmpty(step.firstBinding) ? step.hasFirst ? step.first.ToString() : "첫 칸 미지정" : "생성 결과 ‘" + step.firstBinding + "’";
            string second = string.IsNullOrEmpty(step.secondBinding) ? step.hasSecond ? step.second.ToString() : "둘째 칸 미지정" : "생성 결과 ‘" + step.secondBinding + "’";
            string action = step.kind == TutorialStepKind.Description ? "설명 확인" : step.kind == TutorialStepKind.Item && step.item == BoardItem.Shuffle ? "섞기" :
                (step.kind == TutorialStepKind.Item ? Presentation.LevelToolPanelFields.ItemLabel(step.item) : step.kind == TutorialStepKind.PowerSwap ? "파워 교환 · " + Definition(step.actionDefinitionId) : "블록 교환") + " · " +
                (step.actionArea.Count > 0 ? "선택 영역 " + step.actionArea.Count + "칸 안에서 자유 조작" : step.kind == TutorialStepKind.Item && step.item == BoardItem.Hammer ? first : first + " ↔ " + second);
            if (step.kind == TutorialStepKind.Item) action += " · 무료 체험 " + step.freeItemCount + "회";
            return step.instructions + "\n조작: " + action + (step.conditions.Count == 0 ? "" : "\n완료 (" + (step.combination == TutorialConditionCombination.All ? "모두 만족" : "하나 이상 만족") + "): " + string.Join(" / ", step.conditions.Select(Condition)));
        }
    }
}
#endif
