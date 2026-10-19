#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using Levels;
using Simulation;
using Tutorial;
using UnityEngine.UIElements;
namespace LevelTool.Presentation
{
    public sealed partial class LevelToolTutorialPanel
    {
        private Func<bool> CaptureValidity()
        {
            int generation = revision;
            AuthoringEditSession session = Session;
            SharedTutorialDraft draft = sharedTutorialDraft;
            string levelId = session.SelectedLevelId;
            // 단계 이동은 화면 갱신 세대로 구분한다. 렌더 중 범위 보정은 새 버튼을 무효화하지 않는다.
            return () => !disposed && revision == generation && ReferenceEquals(session, Session) && session.SelectedLevelId == levelId && ReferenceEquals(draft, sharedTutorialDraft);
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
        private void DrawTutorialTarget(VisualElement card, LevelDefinition level, TutorialConditionDefinition condition, int index, Action<Action<TutorialConditionDefinition>> customChange = null, bool? initialAction = null)
        {
            Func<bool> active = CaptureValidity();
            LevelToolTutorialTargetFields.Draw(card, level, condition, index,
                edit => { if (active()) { if (customChange != null) customChange(edit); else ConditionEdit(index, edit); } }, SelectedCoordinates,
                customChange == null ? (Action)(() => { if (active()) BeginTutorialPick("target", condition.authoringId); }) : null,
                initialAction ?? level.Tutorial.steps.Take(tutorialStep).All(value => value.kind == TutorialStepKind.Description));
        }
    }
}
#endif
