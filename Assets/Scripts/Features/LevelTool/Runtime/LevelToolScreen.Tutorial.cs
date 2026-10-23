#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using Levels;
using Tutorial;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private readonly Presentation.LevelToolTutorialState tutorialState = new Presentation.LevelToolTutorialState();
        private Presentation.LevelToolTutorialPanel tutorialPanel;
        private int tutorialStep { get => tutorialState.Step; set => tutorialState.Step = value; }
        private TutorialConditionKind newTutorialCondition { get => tutorialState.NewCondition; set => tutorialState.NewCondition = value; }
        private void TutorialEdit(string label, Action<LevelDefinition> edit) => tutorialPanel.EditTutorial(label, edit);
        private void DrawTutorial()
        {
            if (tutorialWork == null) { DrawTutorialOverview(); return; }
            DrawTutorialPurposes();
            DrawTutorialSummaries();
            var details = new UnityEngine.UIElements.Foldout { name = "tutorial-composition-details", text = "직접 조립 · 단계 / 조건 / 공통 구성", value = tutorialAdvanced };
            details.RegisterValueChangedCallback(evt => tutorialAdvanced = evt.newValue);
            inspector.Add(details);
            tutorialPanel.RefreshView(); details.Add(tutorialPanel.Root);
        }
    }
}
#endif
