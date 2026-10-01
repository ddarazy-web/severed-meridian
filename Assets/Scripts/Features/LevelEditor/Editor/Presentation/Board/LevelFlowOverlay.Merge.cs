using System.Collections.Generic;
using Board;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelFlowOverlay
    {
        private BoardCoordinate? mergeCell;
        private List<BoardCoordinate> mergeOrder = new List<BoardCoordinate>();
        public IReadOnlyList<BoardCoordinate> MergeSources => mergeOrder;

        public void DisplayMerge(BoardCoordinate? selected, bool show)
        {
            mergeCell = show && LevelFlowEditing.CanEdit(level) ? selected : null;
            mergeOrder = mergeCell.HasValue ? LevelFlowEditing.MergeOrder(level, mergeCell.Value) : new List<BoardCoordinate>();
            if (mergeOrder.Count < 2) { mergeCell = null; mergeOrder.Clear(); }
            RefreshLabels(); MarkDirtyRepaint();
        }
        private void MergeLabels()
        {
            for (int i = 0; i < mergeOrder.Count; i++)
            {
                BoardCoordinate cell = mergeOrder[i];
                var label = new Label((i + 1).ToString()) { name = "merge-rank-" + cell.Row + "-" + cell.Column, pickingMode = PickingMode.Ignore };
                label.style.position = Position.Absolute; label.style.left = cell.Column * LevelBoardView.CellSize + 2; label.style.top = cell.Row * LevelBoardView.CellSize + 18;
                label.style.width = label.style.height = 20; label.style.fontSize = 13;
                label.style.unityTextAlign = TextAnchor.MiddleCenter; label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.color = Color.black; label.style.backgroundColor = new Color(0.4f, 0.92f, 0.7f);
                label.style.borderTopLeftRadius = label.style.borderTopRightRadius = label.style.borderBottomLeftRadius = label.style.borderBottomRightRadius = 10;
                Add(label);
            }
        }
        private void DrawMerge(Painter2D painter)
        {
            if (!mergeCell.HasValue) return;
            Color color = new Color(0.4f, 0.92f, 0.7f);
            Vector2 target = Point(mergeCell.Value);
            foreach (BoardCoordinate cell in mergeOrder) Arrow(painter, Point(cell), target, color);
            painter.lineWidth = 3; painter.strokeColor = Color.yellow;
            Vector2 a = target - Vector2.one * 18, b = target + Vector2.one * 18;
            painter.BeginPath(); painter.MoveTo(a); painter.LineTo(new Vector2(b.x, a.y)); painter.LineTo(b);
            painter.LineTo(new Vector2(a.x, b.y)); painter.ClosePath(); painter.Stroke();
        }
    }
}
