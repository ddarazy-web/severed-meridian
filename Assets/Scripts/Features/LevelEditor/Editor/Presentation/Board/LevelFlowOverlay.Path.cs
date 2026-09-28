using System.Collections.Generic;
using System.Linq;
using Board;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelFlowOverlay
    {
        private bool pathDragged, pathBlocked;
        public IReadOnlyList<BoardCoordinate> PathCandidates
        {
            get
            {
                if (tool != FlowTool.Path || draft.Count == 0) return new BoardCoordinate[0];
                BoardCoordinate last = draft[draft.Count - 1];
                return new[] { new BoardCoordinate(last.Row - 1, last.Column), new BoardCoordinate(last.Row + 1, last.Column),
                    new BoardCoordinate(last.Row, last.Column - 1), new BoardCoordinate(last.Row, last.Column + 1) }
                    .Where(cell => LevelFlowEditing.PathError(level, draft.Concat(new[] { cell }).ToArray()) == null).ToArray();
            }
        }

        private void StartPath(BoardCoordinate cell, Vector2 point, int pointerId)
        {
            if (draft.Count == 0 || !draft[draft.Count - 1].Equals(cell))
            {
                string error = LevelFlowEditing.PathError(level, draft.Concat(new[] { cell }).ToArray());
                if (error != null) { Edited?.Invoke(error); return; }
                draft.Add(cell);
            }
            pathDragged = pathBlocked = false; previous = point; pointer = pointerId; dragging = true;
            this.CapturePointer(pointer); MarkDirtyRepaint();
        }

        private void VisitPath(Vector2 point)
        {
            if (!Cell(point, out BoardCoordinate cell)) { previous = null; pathBlocked = true; MarkDirtyRepaint(); return; }
            if (draft.Count == 0) { CancelInput(); return; }
            // 보드 밖이나 막힌 칸을 거친 뒤에는 현재 끝 칸으로 돌아와야 이어 그릴 수 있다.
            if (!previous.HasValue || pathBlocked)
            {
                if (cell.Equals(draft[draft.Count - 1])) { previous = point; pathBlocked = false; }
                MarkDirtyRepaint(); return;
            }
            Vector2 start = previous.Value;
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, point) / 4));
            for (int i = 1; i <= count; i++)
            {
                Cell(Vector2.Lerp(start, point, (float)i / count), out BoardCoordinate crossed);
                if (crossed.Equals(draft[draft.Count - 1])) continue;
                pathDragged = true;
                if (LevelFlowEditing.PathError(level, draft.Concat(new[] { crossed }).ToArray()) != null) { pathBlocked = true; break; }
                draft.Add(crossed);
            }
            previous = point; MarkDirtyRepaint();
        }

        private void FinishPath(Vector2 point)
        {
            VisitPath(point);
            if (pathBlocked)
            {
                CancelInput(); Edited?.Invoke("연결할 수 없는 곳에 놓아 경로를 취소했습니다."); return;
            }
            // 움직이지 않은 클릭은 기존 클릭/Enter 편집으로 남긴다.
            if (!pathDragged)
            {
                dragging = false; previous = null;
                if (this.HasPointerCapture(pointer)) this.ReleasePointer(pointer);
                MarkDirtyRepaint(); return;
            }
            Complete();
        }

        private void DrawPathCandidates(Painter2D painter)
        {
            foreach (BoardCoordinate cell in PathCandidates)
            {
                Vector2 a = Point(cell) - Vector2.one * 18, b = a + Vector2.one * 36;
                painter.fillColor = new Color(0.4f, 0.92f, 0.7f, 0.14f);
                painter.strokeColor = new Color(0.4f, 0.92f, 0.7f); painter.lineWidth = 2;
                painter.BeginPath(); painter.MoveTo(a); painter.LineTo(new Vector2(b.x, a.y)); painter.LineTo(b);
                painter.LineTo(new Vector2(a.x, b.y)); painter.ClosePath(); painter.Fill(); painter.Stroke();
            }
            if (draft.Count > 0)
            {
                Vector2 end = Point(draft[draft.Count - 1]);
                Line(painter, end + new Vector2(-8, 9), end + new Vector2(8, 9), pathBlocked ? Color.red : Color.yellow, 3);
            }
        }
    }
}
