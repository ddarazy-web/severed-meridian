#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using Levels;
using Tutorial;

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
            tutorialPanel.RefreshView(); inspector.Add(tutorialPanel.Root);
        }
    }
}
#endif