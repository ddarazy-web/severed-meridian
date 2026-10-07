using System;
using System.Collections.Generic;
using Board;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelBoardView
    {
        private VisualElement tutorialMarks;
        public Action<BoardCoordinate> TutorialTargetPicked { get; set; }

        private void OnTutorialPointerDown(PointerDownEvent evt)
        {
            if (TutorialTargetPicked == null) return;
            if (evt.button == 0 && level != null && TryCoordinate(this.WorldToLocal(evt.position), out BoardCoordinate coordinate))
                TutorialTargetPicked.Invoke(coordinate);
            evt.StopImmediatePropagation();
        }

        public void ShowTutorialTargets(IEnumerable<BoardCoordinate> highlights, BoardCoordinate? first, BoardCoordinate? second)
        {
            if (tutorialMarks == null)
            {
                tutorialMarks = new VisualElement { name = "tutorial-targets", pickingMode = PickingMode.Ignore };
                tutorialMarks.style.position = Position.Absolute; tutorialMarks.style.left = tutorialMarks.style.top = 0;
                tutorialMarks.style.width = tutorialMarks.style.height = CellSize * BoardDefinition.DefaultRows; Add(tutorialMarks);
            }
            tutorialMarks.Clear();
            void Mark(BoardCoordinate coordinate, string text)
            {
                if (coordinate.Row < 0 || coordinate.Column < 0 || coordinate.Row >= BoardDefinition.DefaultRows || coordinate.Column >= BoardDefinition.DefaultColumns) return;
                Label mark = new Label(text) { pickingMode = PickingMode.Ignore, name = "tutorial-mark" };
                mark.style.position = Position.Absolute; mark.style.left = coordinate.Column * CellSize + 2; mark.style.top = coordinate.Row * CellSize + 2;
                mark.style.width = mark.style.height = CellSize - 4; mark.style.color = UnityEngine.Color.white;
                mark.style.unityTextAlign = TextAnchor.UpperLeft; mark.style.backgroundColor = new Color(0.3f, 0.9f, 1f, 0.15f);
                mark.style.borderBottomWidth = mark.style.borderTopWidth = mark.style.borderLeftWidth = mark.style.borderRightWidth = 2;
                mark.style.borderBottomColor = mark.style.borderTopColor = mark.style.borderLeftColor = mark.style.borderRightColor = UnityEngine.Color.cyan;
                tutorialMarks.Add(mark);
            }
            foreach (BoardCoordinate coordinate in highlights) Mark(coordinate, "");
            if (first.HasValue) Mark(first.Value, "1"); if (second.HasValue) Mark(second.Value, "2");
            if (first.HasValue && second.HasValue && new BoardEdge(first.Value, second.Value).IsAdjacent)
            {
                Label arrow = new Label(first.Value.Row == second.Value.Row ? first.Value.Column < second.Value.Column ? "→" : "←" : first.Value.Row < second.Value.Row ? "↓" : "↑")
                    { name = "tutorial-swap-arrow", pickingMode = PickingMode.Ignore };
                arrow.style.position = Position.Absolute; arrow.style.left = (first.Value.Column + second.Value.Column) * CellSize / 2f;
                arrow.style.top = (first.Value.Row + second.Value.Row) * CellSize / 2f; arrow.style.width = arrow.style.height = CellSize;
                arrow.style.unityTextAlign = TextAnchor.MiddleCenter; arrow.style.color = UnityEngine.Color.white; arrow.style.fontSize = 26; tutorialMarks.Add(arrow);
            }
        }
    }
}
