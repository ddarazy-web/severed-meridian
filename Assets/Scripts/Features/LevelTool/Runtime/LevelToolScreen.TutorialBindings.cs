#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using LevelAuthoring.Runtime;
using Levels;
using Tutorial;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private void BindingEdit(string key, TutorialFlowField field, Action<TutorialFlowBinding> edit) => Edit(() =>
            TutorialDraftEditing.EditLevel(Session, "레벨별 튜토리얼 값", level => edit(level.Tutorial.bindings.Single(value => value.key == key && value.field == field))));
        private void DrawTutorialBindings(LevelDefinition level)
        {
            var flow = level.Tutorial.flow;
            inspector.Add(new Label("이 레벨의 설정"));
            for (int i = 0; i < flow.parameters.Count; i++)
            {
                var parameter = flow.parameters[i]; string key = parameter.key; var field = parameter.field;
                var binding = level.Tutorial.bindings.FirstOrDefault(value => value.key == key && value.field == field);
                var card = new Foldout { text = string.IsNullOrWhiteSpace(parameter.label) ? TutorialAuthoringRules.Label(field) : parameter.label, value = true, tooltip = parameter.help }; inspector.Add(card);
                if (!string.IsNullOrWhiteSpace(parameter.help)) card.Add(new Label(parameter.help) { style = { whiteSpace = WhiteSpace.Normal } });
                if (binding == null) { card.Add(new Label("위 ‘레벨 설정 목록 동기화’를 누르면 기본값이 추가됩니다.")); continue; }
                if (field == TutorialFlowField.First || field == TutorialFlowField.Second || field == TutorialFlowField.ActionArea || field == TutorialFlowField.Highlights ||
                    field == TutorialFlowField.Target && (binding.target?.kind == TutorialTargetKind.Entity || binding.target?.kind == TutorialTargetKind.Area))
                    Button(card, "tutorial-binding-pick-" + key, "보드에서 레벨 설정 칸 선택", () => BeginTutorialPick("binding", bindingKey: key, bindingField: field.ToString()));
                switch (field)
                {
                    case TutorialFlowField.First:
                    case TutorialFlowField.Second:
                        card.Add(new Label("현재 칸: " + binding.coordinate));
                        Button(card, "tutorial-binding-cell-" + key, "선택한 한 칸 적용", () => BindingEdit(key, field, data =>
                        {
                            var cells = SelectedCoordinates(); if (cells.Count != 1) throw new ArgumentException("보드에서 한 칸을 선택하세요.");
                            data.coordinate = cells[0];
                        })); break;
                    case TutorialFlowField.ActionArea:
                    case TutorialFlowField.Highlights:
                        card.Add(new Label("현재 영역: " + binding.cells.Count + "칸"));
                        Button(card, "tutorial-binding-area-" + key, "선택 칸을 영역으로 적용", () => BindingEdit(key, field, data => data.cells = SelectedCoordinates()));
                        Button(card, "tutorial-binding-clear-" + key, "영역 비우기", () => BindingEdit(key, field, data => data.cells.Clear())); break;
                    case TutorialFlowField.FreeItemCount:
                    case TutorialFlowField.RequiredCount:
                    case TutorialFlowField.MatchSize:
                        Number(card, "tutorial-binding-number-" + key, TutorialAuthoringRules.Label(field), binding.number, value => BindingEdit(key, field, data => data.number = value)); break;
                    case TutorialFlowField.MissionIndex:
                        Choice(card, "tutorial-binding-mission-" + key, "미션", new[] { "-1" }.Concat(Enumerable.Range(0, level.Missions.Count).Select(n => n.ToString())).ToArray(),
                            new[] { "선택하세요" }.Concat(level.Missions.Select((mission, n) => (n + 1) + ". " + LevelMissionRules.Name(mission.Kind))).ToArray(), binding.number.ToString(),
                            value => BindingEdit(key, field, data => data.number = int.Parse(value))); break;
                    case TutorialFlowField.Color:
                        TutorialEnum(card, "tutorial-binding-color-" + key, "매칭 색", binding.color, value => BindingEdit(key, field, data => data.color = value)); break;
                    case TutorialFlowField.PowerDefinitionId:
                    case TutorialFlowField.ActionDefinitionId:
                        TutorialPower(card, level, "tutorial-binding-power-" + key, "파워 종류", binding.definitionId, value => BindingEdit(key, field, data => data.definitionId = value)); break;
                    case TutorialFlowField.Target:
                        var step = flow.steps.FirstOrDefault(value => value.authoringId == parameter.stepId);
                        var condition = step?.conditions.FirstOrDefault(value => value.authoringId == parameter.conditionId);
                        if (condition == null) { card.Add(new Label("공유 원본에서 대상 단계와 조건을 다시 지정하세요.")); break; }
                        var display = condition.Copy(); display.target = binding.target;
                        bool initial = flow.steps.TakeWhile(value => value != step).All(value => value.kind == TutorialStepKind.Description);
                        DrawTutorialTarget(card, level, display, i, change => BindingEdit(key, field, data =>
                        { var edited = new TutorialConditionDefinition { target = data.target }; change(edited); data.target = edited.target; }), initial);
                        break;
                }
            }
        }
    }
}
#endif
