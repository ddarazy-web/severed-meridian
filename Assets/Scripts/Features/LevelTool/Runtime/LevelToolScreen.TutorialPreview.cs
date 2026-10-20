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
        private string tutorialPreviewKey;

        private void DrawTutorialPreview()
        {
            VisualElement existing = board.Q("tutorial-preview-overlay");
            if (!tutorialPreview || tutorialPick != null || inspectorPage != "튜토리얼")
            {
                existing?.RemoveFromHierarchy(); tutorialPreviewKey = null; return;
            }
            string key = DisplayContentKey() + "|" + tutorialStep;
            if (key == tutorialPreviewKey && existing != null)
            {
                existing.style.width = existing.style.height = cellSize * 9;
                foreach (VisualElement dim in existing.Children())
                {
                    int cell = (int)dim.userData;
                    dim.style.left = cell % 9 * cellSize; dim.style.top = cell / 9 * cellSize;
                    dim.style.width = dim.style.height = cellSize;
                }
                existing.BringToFront(); return;
            }
            existing?.RemoveFromHierarchy(); tutorialPreviewKey = null;
            try
            {
                // 표시 캐시의 격리된 문서를 사용한다. 공유 사본은 해당 흐름 문서만 대체한다.
                using var graph = new AuthoringObjectGraph(displayDocuments.Values.Select(document =>
                    sharedTutorialDraft != null && document.Id == sharedTutorialDraft.FlowId
                        ? sharedTutorialDraft.Session.Get(document.Id) : document));
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
                    dim.userData = index;
                    dim.style.position = Position.Absolute;
                    dim.style.left = index % 9 * cellSize; dim.style.top = index / 9 * cellSize;
                    dim.style.width = cellSize; dim.style.height = cellSize;
                    dim.style.backgroundColor = new Color(0, 0, 0, .65f);
                    overlay.Add(dim);
                }
                board.Add(overlay);
                tutorialPreviewKey = key;
            }
            catch (Exception error) { Show("강조 미리보기: " + error.Message); }
        }
    }
}
#endif
