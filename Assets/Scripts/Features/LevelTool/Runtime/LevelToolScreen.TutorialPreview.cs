#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using LevelAuthoring.Runtime;
using Levels;
using Tutorial;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private bool tutorialPreview;

        private void DrawTutorialPreview()
        {
            board.Q("tutorial-preview-overlay")?.RemoveFromHierarchy();
            if (!tutorialPreview || tutorialPick != null || inspectorPage != "튜토리얼") return;
            try
            {
                using var graph = new AuthoringObjectGraph(ValidationDocuments());
                var level = (LevelDefinition)graph.Resolve(Session.SelectedLevelId);
                var tutorial = TutorialFlowResolver.Resolve(level);
                var step = tutorial.steps?.ElementAtOrDefault(tutorialStep);
                if (step == null) return;
                bool initial = tutorial.steps.Take(tutorialStep).All(value => value.kind == TutorialStepKind.Description);
                var cells = new HashSet<BoardCoordinate>(step.automaticHighlights ?
                    TutorialAuthoringRules.PreviewTargetHighlights(level, step, initial) : step.highlights);
                if (step.actionArea.Count > 0) cells.UnionWith(step.actionArea);
                else
                {
                    if (step.hasFirst && string.IsNullOrEmpty(step.firstBinding)) cells.Add(step.first);
                    if (step.hasSecond && string.IsNullOrEmpty(step.secondBinding)) cells.Add(step.second);
                }
                var overlay = new VisualElement { name = "tutorial-preview-overlay", pickingMode = PickingMode.Ignore };
                overlay.style.position = Position.Absolute; overlay.style.left = 0; overlay.style.top = 0;
                overlay.style.width = cellSize * 9; overlay.style.height = cellSize * 9;
                for (int index = 0; index < 81; index++)
                {
                    if (cells.Contains(new BoardCoordinate(index / 9, index % 9))) continue;
                    var dim = new VisualElement { name = "tutorial-dim-" + index, pickingMode = PickingMode.Ignore };
                    dim.style.position = Position.Absolute;
                    dim.style.left = index % 9 * cellSize; dim.style.top = index / 9 * cellSize;
                    dim.style.width = cellSize; dim.style.height = cellSize;
                    dim.style.backgroundColor = new Color(0, 0, 0, .65f);
                    overlay.Add(dim);
                }
                board.Add(overlay);
            }
            catch (Exception error) { Show("강조 미리보기: " + error.Message); }
        }
    }
}
#endif
