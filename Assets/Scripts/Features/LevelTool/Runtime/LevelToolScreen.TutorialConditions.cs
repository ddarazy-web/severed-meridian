#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using Board;
using Elements;
using Levels;
using Simulation;
using Tutorial;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private void DrawTutorialConditions(LevelDefinition level, TutorialStepDefinition step)
        {
            var registry = TutorialHandlerRegistry.CreateDefault();
            var kinds = Enum.GetValues(typeof(TutorialConditionKind)).Cast<TutorialConditionKind>().Where(kind => registry.TryGetCondition(kind, out _)).ToArray();
            Choice(inspector, "tutorial-new-condition", "추가할 조건", kinds.Select(kind => kind.ToString()).ToArray(), kinds.Select(TutorialAuthoringRules.ConditionLabel).ToArray(),
                newTutorialCondition.ToString(), value => newTutorialCondition = (TutorialConditionKind)Enum.Parse(typeof(TutorialConditionKind), value));
            Button(inspector, "tutorial-add-condition", "조건 추가", () => TutorialEdit("완료 조건 추가", data => data.Tutorial.steps[tutorialStep].conditions.Add(
                TutorialAuthoringRules.CreateCondition(newTutorialCondition, data.CreateElementCatalog(), data.Tutorial.steps[tutorialStep].item))));
            for (int i = 0; i < step.conditions.Count; i++)
            {
                int index = i;
                var condition = step.conditions[i];
                var card = new Foldout { text = (i + 1) + ". " + TutorialAuthoringRules.ConditionLabel(condition.kind), value = true }; inspector.Add(card);
                Button(card, "tutorial-remove-condition-" + i, "이 조건 삭제", () => StepEdit(data => data.conditions.RemoveAt(index)));
                string countLabel = condition.kind == TutorialConditionKind.RemainingDurability ? "남은 내구도: 정확히" : condition.kind == TutorialConditionKind.DurabilityDecrease ? "줄일 내구도" : "필요 횟수 / 개수";
                Number(card, "tutorial-condition-count-" + i, countLabel, condition.requiredCount, value => ConditionEdit(index, data => data.requiredCount = value));
                if (condition.kind == TutorialConditionKind.Match)
                {
                    Number(card, "tutorial-match-size-" + i, "매칭 블록 수", condition.matchSize, value => ConditionEdit(index, data => data.matchSize = value));
                    TutorialEnum(card, "tutorial-match-comparison-" + i, "크기 비교", condition.sizeComparison, value => ConditionEdit(index, data => data.sizeComparison = value), value => value == TutorialMatchSizeComparison.Exactly ? "정확히" : "이상");
                    TutorialEnum(card, "tutorial-match-origin-" + i, "인정할 매칭", condition.origin, value => ConditionEdit(index, data => data.origin = value), value => value == TutorialMatchOrigin.DirectSwap ? "직접 교환으로 만든 매칭만" : "연쇄 매칭 포함");
                    TutorialToggle(card, "tutorial-any-color-" + i, "색 무관", condition.anyColor, value => ConditionEdit(index, data => data.anyColor = value));
                    if (!condition.anyColor) TutorialEnum(card, "tutorial-match-color-" + i, "색", condition.color, value => ConditionEdit(index, data => data.color = value));
                }
                if (condition.kind >= TutorialConditionKind.DurabilityDecrease && condition.kind <= TutorialConditionKind.Combined)
                    DrawTutorialTarget(card, level, condition, index);
                if (condition.kind == TutorialConditionKind.DurabilityDecrease)
                    TutorialEnum(card, "tutorial-aggregation-" + i, "대상 집계", condition.aggregation, value => ConditionEdit(index, data => data.aggregation = value), value => value == TutorialDamageAggregation.Total ? "합계" : "각 대상");
                if (condition.kind == TutorialConditionKind.Generated || condition.kind == TutorialConditionKind.Activated)
                {
                    TutorialPower(card, level, "tutorial-power-" + i, "파워 종류", condition.powerDefinitionId, value => ConditionEdit(index, data => { data.powerDefinitionId = value; data.anyDirection = true; }));
                    if (level.CreateElementCatalog().Definitions.FirstOrDefault(value => value.Id.Value == condition.powerDefinitionId)?.Supply?.Content == RuntimeContent.Rocket)
                    {
                        TutorialToggle(card, "tutorial-any-direction-" + i, "방향 무관", condition.anyDirection, value => ConditionEdit(index, data => data.anyDirection = value));
                        if (!condition.anyDirection) TutorialEnum(card, "tutorial-power-direction-" + i, "로켓 방향", condition.rocketDirection, value => ConditionEdit(index, data => data.rocketDirection = value), value => value == RocketDirection.Horizontal ? "가로" : "세로");
                    }
                    if (condition.kind == TutorialConditionKind.Generated) TutorialText(card, "tutorial-generated-binding-" + i, "생성 결과 연결 이름", condition.bindGeneratedAs, value => ConditionEdit(index, data => data.bindGeneratedAs = value), "예: rocket1. 이후 단계에서 이 이름으로 생성된 블록을 지정할 수 있습니다.");
                    else card.Add(new Label("직접 단독 발동만 인정합니다. 조합과 연쇄 발동은 제외합니다.") { style = { whiteSpace = WhiteSpace.Normal } });
                }
                if (condition.kind == TutorialConditionKind.ItemUsed) TutorialEnum(card, "tutorial-condition-item-" + i, "아이템", condition.item, value => ConditionEdit(index, data => data.item = value), ItemLabel);
                if (condition.kind == TutorialConditionKind.MissionProgress)
                {
                    var missions = level.Missions.ToArray();
                    Choice(card, "tutorial-mission-" + i, "레벨 미션", new[] { "-1" }.Concat(Enumerable.Range(0, missions.Length).Select(n => n.ToString())).ToArray(),
                        new[] { "선택하세요" }.Concat(missions.Select((mission, n) => (n + 1) + ". " + LevelMissionRules.Name(mission.Kind))).ToArray(), condition.missionIndex.ToString(),
                        value => TutorialEdit("조건 미션 선택", data =>
                        {
                            var target = data.Tutorial.steps[tutorialStep].conditions[index]; int selected = int.Parse(value);
                            if (selected < 0) { target.missionIndex = -1; target.missionKind = (MissionKind)(-1); }
                            else target.SelectMission(selected, data.Missions[selected]);
                        }));
                }
                if (condition.kind == TutorialConditionKind.DurabilityDecrease || condition.kind == TutorialConditionKind.Removed || condition.kind == TutorialConditionKind.Combined)
                {
                    var target = TutorialTargetCapabilities.Resolve(level, condition.target, level.Tutorial.steps.Take(tutorialStep).All(value => value.kind == TutorialStepKind.Description));
                    card.Add(new Label(condition.kind == TutorialConditionKind.DurabilityDecrease ? "허용 원인 (비우면 모든 피해)" : "허용 원인 (하나 이상 선택)"));
                    foreach (EffectOrigin origin in Enum.GetValues(typeof(EffectOrigin)))
                    {
                        if (origin == EffectOrigin.Unknown || condition.kind == TutorialConditionKind.Combined && origin < EffectOrigin.RocketRocket) continue;
                        string error = condition.kind == TutorialConditionKind.Combined ? null : TutorialTargetCapabilities.OriginError(target, origin);
                        string key = "tutorial-origin-" + i + "-" + origin;
                        TutorialToggle(card, key, TutorialSampleCatalog.OriginLabel(origin), condition.allowedOrigins.Contains(origin), value => ConditionEdit(index, data =>
                        { if (value) { if (!data.allowedOrigins.Contains(origin)) data.allowedOrigins.Add(origin); } else data.allowedOrigins.Remove(origin); }));
                        if (error != null) { card.Q<Toggle>(key).SetEnabled(false); card.Q<Toggle>(key).tooltip = error; }
                    }
                    if (condition.allowedOrigins.Any(origin => TutorialTargetCapabilities.OriginError(target, origin) != null) && condition.kind != TutorialConditionKind.Combined)
                        Button(card, "tutorial-clear-origins-" + i, "대상에 맞지 않는 원인 제거", () => ConditionEdit(index, data => data.allowedOrigins.RemoveAll(origin => TutorialTargetCapabilities.OriginError(target, origin) != null)));
                }
            }
        }
        private static void TutorialPower(VisualElement host, LevelDefinition level, string name, string label, string selected, Action<string> changed)
        {
            var powers = level.CreateElementCatalog().Definitions.Where(value => value.Supply?.Behavior == ElementSupplyBehavior.Power).ToArray();
            Choice(host, name, label, new[] { "" }.Concat(powers.Select(value => value.Id.Value)).ToArray(), new[] { "선택하세요" }.Concat(powers.Select(value => value.DisplayName)).ToArray(), selected, changed);
        }
        private void DrawTutorialTarget(VisualElement card, LevelDefinition level, TutorialConditionDefinition condition, int index, Action<Action<TutorialConditionDefinition>> customChange = null, bool? initialAction = null)
        {
            void Change(Action<TutorialConditionDefinition> edit) { if (customChange != null) customChange(edit); else ConditionEdit(index, edit); }
            if (condition.target == null)
            { Button(card, "tutorial-create-target-" + index, "조건 대상 지정", () => Change(data => data.target = new TutorialTargetDefinition())); return; }
            var target = condition.target;
            var kinds = condition.kind == TutorialConditionKind.Generated ? new[] { TutorialTargetKind.Board, TutorialTargetKind.Area } : Enum.GetValues(typeof(TutorialTargetKind)).Cast<TutorialTargetKind>().ToArray();
            string[] names = { "보드 전체", "특정 개체", "종류", "선택 영역", "이전 생성 결과" };
            Choice(card, "tutorial-target-kind-" + index, "조건 대상", kinds.Select(value => value.ToString()).ToArray(), kinds.Select(value => names[(int)value]).ToArray(), target.kind.ToString(),
                value => Change(data => data.target.kind = (TutorialTargetKind)Enum.Parse(typeof(TutorialTargetKind), value)));
            if (condition.kind < TutorialConditionKind.Generated) TutorialEnum(card, "tutorial-target-layer-" + index, "대상 층", target.layer, value => Change(data => data.target.layer = value),
                value => value == TutorialTargetLayer.Content ? "블록 / 장애물" : value == TutorialTargetLayer.Cover ? "덮개" : "바닥");
            if (target.kind == TutorialTargetKind.Area || target.kind == TutorialTargetKind.Entity)
            {
                if (customChange == null) Button(card, "tutorial-target-select-" + index, "보드에서 조건 대상 선택", () => BeginTutorialPick("target", condition.authoringId));
                card.Add(new Label(target.kind == TutorialTargetKind.Entity ? "대상 위치: " + target.coordinate : "대상 영역: " + target.cells.Count + "칸"));
                Button(card, "tutorial-target-pick-" + index, "현재 선택 칸을 조건 대상으로", () => Change(data =>
                {
                    var cells = SelectedCoordinates();
                    if (cells.Count == 0 || data.target.kind == TutorialTargetKind.Entity && cells.Count != 1) throw new ArgumentException("개체는 한 칸, 영역은 한 칸 이상 선택하세요.");
                    if (data.target.kind == TutorialTargetKind.Entity) data.target.coordinate = cells[0]; else data.target.cells = cells;
                }));
            }
            if (target.kind == TutorialTargetKind.Generated) TutorialText(card, "tutorial-target-binding-" + index, "이전 생성 연결 이름", target.binding, value => Change(data => data.target.binding = value));
            if (target.kind == TutorialTargetKind.Definition)
            {
                var definitions = level.CreateElementCatalog().Definitions.ToArray();
                Choice(card, "tutorial-target-definition-" + index, "대상 종류", new[] { "" }.Concat(definitions.Select(value => value.Id.Value)).ToArray(), new[] { "선택하세요" }.Concat(definitions.Select(value => value.DisplayName)).ToArray(), target.definitionId,
                    value => Change(data => data.target.definitionId = value));
            }
            string warning = TutorialTargetCapabilities.ConditionError(TutorialTargetCapabilities.Resolve(level, target,
                initialAction ?? level.Tutorial.steps.Take(tutorialStep).All(value => value.kind == TutorialStepKind.Description)), condition.kind);
            if (warning != null) card.Add(new Label(warning) { style = { whiteSpace = WhiteSpace.Normal } });
        }
    }
}
#endif
