#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using Tutorial;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private void DrawTutorialParameters(TutorialFlowDefinition flow)
        {
            var list = new Foldout { text = "레벨별로 바꿀 설정 항목", value = true }; inspector.Add(list);
            list.Add(new Label("단계와 조건을 먼저 만든 뒤, 레벨마다 다르게 지정할 값만 선언하세요. 키는 저장된 레벨과 연결되므로 적용 후에는 신중하게 바꾸세요.") { style = { whiteSpace = WhiteSpace.Normal } });
            Button(list, "tutorial-add-parameter", "설정 항목 추가", () => SharedFlowEdit(data =>
            {
                var step = data.steps[Math.Max(0, Math.Min(tutorialStep, data.steps.Count - 1))];
                int number = 1; while (data.parameters.Any(value => value.key == "setting" + number)) number++;
                data.parameters.Add(new TutorialFlowParameter { key = "setting" + number, label = "새 설정", stepId = step.authoringId,
                    conditionId = step.conditions.FirstOrDefault()?.authoringId ?? "", field = TutorialFlowField.First });
            })).SetEnabled(flow.steps.Count > 0);
            for (int i = 0; i < flow.parameters.Count; i++)
            {
                int index = i; var parameter = flow.parameters[i];
                var card = new Foldout { text = (i + 1) + ". " + parameter.label + " (" + parameter.key + ")", value = true }; list.Add(card);
                void Change(Action<TutorialFlowParameter> edit) => SharedFlowEdit(data => edit(data.parameters[index]));
                TutorialText(card, "tutorial-parameter-key-" + i, "연결 키", parameter.key, value => Change(data => data.key = value));
                TutorialText(card, "tutorial-parameter-label-" + i, "화면 이름", parameter.label, value => Change(data => data.label = value));
                TutorialText(card, "tutorial-parameter-help-" + i, "도움말", parameter.help, value => Change(data => data.help = value));
                Choice(card, "tutorial-parameter-step-" + i, "대상 단계", new[] { "" }.Concat(flow.steps.Select(value => value.authoringId)).ToArray(),
                    new[] { "선택하세요" }.Concat(flow.steps.Select((value, n) => (n + 1) + ". " + value.instructions)).ToArray(), parameter.stepId,
                    value => Change(data => { data.stepId = value; data.conditionId = ""; }));
                TutorialEnum(card, "tutorial-parameter-field-" + i, "설정 종류", parameter.field, value => Change(data => data.field = value), TutorialAuthoringRules.Label);
                if (parameter.field >= TutorialFlowField.RequiredCount && parameter.field <= TutorialFlowField.MissionIndex)
                {
                    var step = flow.steps.FirstOrDefault(value => value.authoringId == parameter.stepId);
                    var conditions = step?.conditions.ToArray() ?? Array.Empty<TutorialConditionDefinition>();
                    Choice(card, "tutorial-parameter-condition-" + i, "대상 조건", new[] { "" }.Concat(conditions.Select(value => value.authoringId)).ToArray(),
                        new[] { "선택하세요" }.Concat(conditions.Select((value, n) => (n + 1) + ". " + TutorialAuthoringRules.ConditionLabel(value.kind))).ToArray(), parameter.conditionId,
                        value => Change(data => data.conditionId = value));
                }
                Button(card, "tutorial-remove-parameter-" + i, "설정 항목 삭제", () => SharedFlowEdit(data => data.parameters.RemoveAt(index)));
            }
        }
    }
}
#endif
