#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Elements;
using LevelAuthoring.Editing;
using Levels;
using Simulation;
using Tutorial;
using UnityEngine.UIElements;

namespace LevelTool.Presentation
{
    public sealed class LevelToolTutorialConditionsPanel : IDisposable
    {
        private readonly Func<AuthoringEditSession> currentSession;
        private readonly Action<string, Action<LevelDefinition>> TutorialEdit;
        private readonly Func<List<BoardCoordinate>> selectedCoordinates;
        private readonly Action<string, string> BeginTutorialPick;
        private readonly Func<TutorialConditionKind> readKind;
        private readonly Action<TutorialConditionKind> writeKind;
        private int tutorialStep, revision;
        private bool disposed;
        public VisualElement Root { get; } = new VisualElement();
        private VisualElement inspector => Root;
        private TutorialConditionKind newTutorialCondition { get => readKind(); set => writeKind(value); }

        public LevelToolTutorialConditionsPanel(Func<AuthoringEditSession> currentSession, Action<string, Action<LevelDefinition>> edit,
            Func<List<BoardCoordinate>> selectedCoordinates, Action<string, string> pick,
            Func<TutorialConditionKind> readKind, Action<TutorialConditionKind> writeKind)
        {
            this.currentSession = currentSession; TutorialEdit = edit; this.selectedCoordinates = selectedCoordinates;
            BeginTutorialPick = pick; this.readKind = readKind; this.writeKind = writeKind;
            Root.style.flexShrink = 0;
        }
        public void Clear() { revision++; Root.Clear(); }
        public void Dispose() { if (disposed) return; disposed = true; Clear(); }
        public void Refresh(LevelDefinition level, int stepIndex)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LevelToolTutorialConditionsPanel));
            Clear(); tutorialStep = stepIndex;
            DrawTutorialConditions(level, level.Tutorial.steps[stepIndex]);
        }
        private Func<bool> CaptureValidity()
        {
            int generation = revision;
            AuthoringEditSession session = currentSession();
            string levelId = session.SelectedLevelId;
            return () => !disposed && revision == generation && ReferenceEquals(session, currentSession()) && session.SelectedLevelId == levelId;
        }
        private Action<T> Guard<T>(Action<T> action)
        {
            Func<bool> active = CaptureValidity();
            return value => { if (active()) action(value); };
        }
        private Button Button(VisualElement parent, string name, string text, Action action)
        {
            Func<bool> active = CaptureValidity();
            return LevelToolPanelFields.Button(parent, name, text, () => { if (active()) action(); });
        }
        private void Choice(VisualElement parent, string name, string label, string[] values, string[] labels, string current, Action<string> changed) =>
            LevelToolPanelFields.Choice(parent, name, label, values, labels, current, Guard(changed));
        private void Number(VisualElement parent, string name, string label, int current, Action<int> changed) =>
            LevelToolPanelFields.Number(parent, name, label, current, Guard(changed));
        private void TutorialText(VisualElement host, string name, string label, string current, Action<string> changed, string hint = "") =>
            LevelToolPanelFields.TutorialText(host, name, label, current, Guard(changed), hint);
        private void TutorialToggle(VisualElement host, string name, string label, bool current, Action<bool> changed) =>
            LevelToolPanelFields.TutorialToggle(host, name, label, current, Guard(changed));
        private void TutorialEnum<T>(VisualElement host, string name, string label, T current, Action<T> changed, Func<T, string> display = null) where T : struct, Enum =>
            LevelToolPanelFields.TutorialEnum(host, name, label, current, Guard(changed), display);
        private void TutorialPower(VisualElement host, LevelDefinition level, string name, string label, string current, Action<string> changed) =>
            LevelToolPanelFields.TutorialPower(host, level, name, label, current, Guard(changed));
        private static string ItemLabel(BoardItem value) => LevelToolPanelFields.ItemLabel(value);
        private void StepEdit(Action<TutorialStepDefinition> edit) => TutorialEdit("튜토리얼 단계 편집", level => edit(level.Tutorial.steps[tutorialStep]));
        private void ConditionEdit(int index, Action<TutorialConditionDefinition> edit) => StepEdit(step => edit(step.conditions[index]));
        private void DrawTutorialTarget(VisualElement card, LevelDefinition level, TutorialConditionDefinition condition, int index)
        {
            Func<bool> active = CaptureValidity();
            LevelToolTutorialTargetFields.Draw(card, level, condition, index,
                edit => { if (active()) ConditionEdit(index, edit); }, selectedCoordinates,
                () => { if (active()) BeginTutorialPick("target", condition.authoringId); },
                level.Tutorial.steps.Take(tutorialStep).All(value => value.kind == TutorialStepKind.Description));
        }
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
    }
}
#endif
