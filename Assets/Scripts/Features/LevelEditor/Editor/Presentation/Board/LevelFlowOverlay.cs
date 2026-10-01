using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public enum FlowTool { None, Select, Down, Up, Left, Right, GravityClear, Path, PathErase, Wall, WallErase, Portal, PortalExit, Arrival, ArrivalErase, Connect, Wire }

    // 기존 블록 입력과 분리된 흐름 도구다. 미리보기만 소유하며 저장은 편집 API가 담당한다.
    public sealed partial class LevelFlowOverlay : VisualElement
    {
        private LevelDefinition level;
        private FlowTool tool;
        private string source;
        private readonly List<BoardCoordinate> draft = new List<BoardCoordinate>();
        private readonly HashSet<BoardCoordinate> cells = new HashSet<BoardCoordinate>();
        private readonly HashSet<BoardEdge> edges = new HashSet<BoardEdge>();
        private BoardCoordinate? dragStart;
        private Vector2? previous;
        private int pointer;
        private bool dragging;
        private readonly IVisualElementScheduledItem pulse;
        public bool RectangleGravity { get; set; }
        public BoardCoordinate? PortalEntrance { get; set; }
        public string GeneratorId { get; set; }
        public int WireIndex { get; set; } = -1;
        public int HighlightConnection { get; set; } = -1;
        public BoardEdge? HighlightWall { get; set; }
        public FlowTool Tool => tool;
        public int DraftCount => draft.Count;
        public bool IsDragging => dragging;
        public event Action<BoardCoordinate> CellSelected;
        public event Action<string> Edited;

        public LevelFlowOverlay()
        {
            name = "flow-overlay";
            pickingMode = PickingMode.Ignore;
            focusable = true;
            style.position = Position.Absolute;
            style.left = style.top = 0;
            style.width = style.height = (LevelBoardView.CellSize * BoardDefinition.DefaultColumns);
            generateVisualContent += Draw;
            RegisterCallback<PointerDownEvent>(Down);
            RegisterCallback<PointerMoveEvent>(Move);
            RegisterCallback<PointerUpEvent>(Up);
            RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                // 이 도구의 캡처 해제가 상위 보드의 칠하기 취소로 전파되지 않게 한다.
                if (evt.target != this) return;
                evt.StopPropagation();
                if (dragging) CancelInput();
            });
            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape) { CancelInput(); evt.StopImmediatePropagation(); }
                else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) { Complete(); evt.StopImmediatePropagation(); }
                else if (evt.keyCode == KeyCode.Backspace && draft.Count > 0) { draft.RemoveAt(draft.Count - 1); MarkDirtyRepaint(); evt.StopImmediatePropagation(); }
            });
            pulse = schedule.Execute(() => { AnimateArtwork(); MarkDirtyRepaint(); }).Every(100);
            RegisterCallback<DetachFromPanelEvent>(_ => { CancelInput(); pulse.Pause(); });
            RegisterCallback<AttachToPanelEvent>(_ => pulse.Resume());
            RegisterCallback<AttachToPanelEvent>(_ => { LevelBoardArtwork.Loaded += RefreshLabels; LevelBoardArtwork.Acquire(); });
            RegisterCallback<DetachFromPanelEvent>(_ => { LevelBoardArtwork.Loaded -= RefreshLabels; LevelBoardArtwork.Release(); });
        }

        public void Display(LevelDefinition target, FlowTool active)
        {
            // 미리보기 경로는 원본 레벨에 즉시 쓰지 않는다. 대상이나 도구가 달라지면 초안을 버리고
            // 완료 동작에서만 검증·기록한다. 원본 변경 시 남아 있는 연결 인덱스도 함께 초기화한다.
            string current = target == null ? null : JsonUtility.ToJson(target);
            if (level != target || source != current)
            {
                WireIndex = -1; PortalEntrance = null; GeneratorId = null;
                HighlightConnection = -1; HighlightWall = null;
            }
            if (level != target || source != current || tool != active) CancelInput();
            level = target;
            source = current;
            tool = active;
            pickingMode = active == FlowTool.None ? PickingMode.Ignore : PickingMode.Position;
            RefreshLabels();
            MarkDirtyRepaint();
        }

        public void CancelInput()
        {
            bool captured = dragging;
            dragging = false;
            draft.Clear(); cells.Clear(); edges.Clear(); dragStart = null; previous = null;
            pathDragged = pathBlocked = false;
            if (captured && this.HasPointerCapture(pointer)) this.ReleasePointer(pointer);
            MarkDirtyRepaint();
        }

        public void Complete()
        {
            if (!Current()) return;
            string error = tool == FlowTool.Path ? LevelFlowEditing.SetPath(level, draft.ToArray()) :
                tool == FlowTool.Wire ? LevelConnectionEditing.SetWire(level, WireIndex, draft.ToArray()) : "경로 또는 전선 도구에서 완료하세요.";
            if (error == null) CancelInput();
            source = JsonUtility.ToJson(level);
            Edited?.Invoke(error ?? "경로를 확정했습니다. Undo 한 번으로 복구합니다.");
        }

        public void SavePartialWire()
        {
            if (!Current() || tool != FlowTool.Wire) return;
            string error = LevelConnectionEditing.SetWire(level, WireIndex, draft.ToArray(), true);
            if (error == null) CancelInput();
            source = JsonUtility.ToJson(level);
            Edited?.Invoke(error ?? "전선 중간 경로를 기록했습니다. 대상에 닿기 전까지 연결 오류로 표시합니다.");
        }

        private bool Current()
        {
            if (level == null || !LevelFlowEditing.CanEdit(level)) return false;
            if (source == JsonUtility.ToJson(level)) return true;
            CancelInput();
            Edited?.Invoke("원본이 변경되었습니다. 다시 선택하세요.");
            return false;
        }

        private static bool Cell(Vector2 point, out BoardCoordinate cell)
        {
            cell = new BoardCoordinate(Mathf.FloorToInt(point.y / LevelBoardView.CellSize), Mathf.FloorToInt(point.x / LevelBoardView.CellSize));
            return point.x >= 0 && point.y >= 0 && point.x < (LevelBoardView.CellSize * BoardDefinition.DefaultColumns) && point.y < (LevelBoardView.CellSize * BoardDefinition.DefaultRows);
        }

        private static BoardCoordinate Vertex(Vector2 point) => new BoardCoordinate(Mathf.Clamp(Mathf.RoundToInt(point.y / LevelBoardView.CellSize), 0, BoardDefinition.DefaultRows), Mathf.Clamp(Mathf.RoundToInt(point.x / LevelBoardView.CellSize), 0, BoardDefinition.DefaultColumns));
        private static bool Edge(Vector2 point, out BoardEdge edge)
        {
            edge = default;
            if (!Cell(point, out BoardCoordinate cell)) return false;
            int column = Mathf.RoundToInt(point.x / LevelBoardView.CellSize), row = Mathf.RoundToInt(point.y / LevelBoardView.CellSize);
            float dx = Mathf.Abs(point.x - column * LevelBoardView.CellSize), dy = Mathf.Abs(point.y - row * LevelBoardView.CellSize);
            if (dx <= dy && column > 0 && column < BoardDefinition.DefaultColumns && dx <= 12)
                edge = new BoardEdge(new BoardCoordinate(cell.Row, column - 1), new BoardCoordinate(cell.Row, column));
            else if (row > 0 && row < BoardDefinition.DefaultRows && dy <= 12)
                edge = new BoardEdge(new BoardCoordinate(row - 1, cell.Column), new BoardCoordinate(row, cell.Column));
            else return false;
            return true;
        }

        private void Down(PointerDownEvent evt)
        {
            if (tool == FlowTool.None || evt.button != 0) return;
            evt.StopImmediatePropagation();
            Focus();
            if (!Current()) return;
            Vector2 point = this.WorldToLocal(evt.position);
            if (tool == FlowTool.Wire)
            {
                if (point.x < 0 || point.y < 0 || point.x > (LevelBoardView.CellSize * BoardDefinition.DefaultColumns) || point.y > (LevelBoardView.CellSize * BoardDefinition.DefaultRows)) return;
                AddDraft(Vertex(point));
                return;
            }
            if (!Cell(point, out BoardCoordinate cell)) return;
            CellSelected?.Invoke(cell);
            if (tool == FlowTool.Path) { StartPath(cell, point, evt.pointerId); return; }
            if (tool == FlowTool.Select) return;
            if (tool == FlowTool.Down || tool == FlowTool.Up || tool == FlowTool.Left || tool == FlowTool.Right || tool == FlowTool.GravityClear || tool == FlowTool.Wall || tool == FlowTool.WallErase)
            {
                dragging = true;
                pointer = evt.pointerId;
                dragStart = cell;
                Visit(point);
                this.CapturePointer(pointer);
                return;
            }
            string error = null;
            switch (tool)
            {
                case FlowTool.PathErase: error = LevelFlowEditing.RemovePath(level, cell); break;
                case FlowTool.Portal: error = LevelFlowEditing.SetPortal(level, cell, null); break;
                case FlowTool.PortalExit:
                    error = PortalEntrance.HasValue ? LevelFlowEditing.SetPortal(level, PortalEntrance.Value, cell) : "먼저 통로 입구를 선택하세요."; break;
                case FlowTool.Arrival: error = LevelFlowEditing.SetArrival(level, cell, false); break;
                case FlowTool.ArrivalErase: error = LevelFlowEditing.SetArrival(level, cell, true); break;
                case FlowTool.Connect:
                    int target = LevelPlacementRules.Find(level, PlacementLayer.Obstacle, cell);
                    error = target >= 0 ? LevelConnectionEditing.Add(level, GeneratorId, level.Obstacles[target].Id) : "연결할 장애물을 선택하세요."; break;
            }
            source = JsonUtility.ToJson(level);
            Edited?.Invoke(error ?? "설정을 적용했습니다.");
        }

        private void AddDraft(BoardCoordinate coordinate)
        {
            if (draft.Count > 0 && (!new BoardEdge(draft[draft.Count - 1], coordinate).IsAdjacent || draft.Contains(coordinate))) return;
            draft.Add(coordinate);
            MarkDirtyRepaint();
        }

        private void Move(PointerMoveEvent evt)
        {
            if (tool == FlowTool.None) return;
            evt.StopImmediatePropagation();
            if (!dragging || evt.pointerId != pointer) return;
            if (!this.HasPointerCapture(pointer) || !Current()) { CancelInput(); return; }
            if (tool == FlowTool.Path) { VisitPath(this.WorldToLocal(evt.position)); return; }
            Visit(this.WorldToLocal(evt.position));
        }

        private void Visit(Vector2 point)
        {
            bool wall = tool == FlowTool.Wall || tool == FlowTool.WallErase;
            if (!Cell(point, out BoardCoordinate current)) { previous = null; return; }
            if (RectangleGravity && !wall && dragStart.HasValue)
            {
                cells.Clear();
                for (int row = Math.Min(current.Row, dragStart.Value.Row); row <= Math.Max(current.Row, dragStart.Value.Row); row++)
                    for (int column = Math.Min(current.Column, dragStart.Value.Column); column <= Math.Max(current.Column, dragStart.Value.Column); column++)
                        cells.Add(new BoardCoordinate(row, column));
            }
            else
            {
                Vector2 start = previous ?? point;
                int count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(start, point) / 4));
                for (int i = 0; i <= count; i++)
                {
                    Vector2 sample = Vector2.Lerp(start, point, (float)i / count);
                    if (wall) { if (Edge(sample, out BoardEdge edge)) edges.Add(edge); }
                    else if (Cell(sample, out BoardCoordinate cell)) cells.Add(cell);
                }
            }
            previous = point;
            MarkDirtyRepaint();
        }

        private void Up(PointerUpEvent evt)
        {
            if (tool == FlowTool.None) return;
            evt.StopImmediatePropagation();
            if (!dragging || evt.pointerId != pointer || evt.button != 0) return;
            if (!this.HasPointerCapture(pointer) || !Current()) { CancelInput(); return; }
            if (tool == FlowTool.Path) { FinishPath(this.WorldToLocal(evt.position)); return; }
            Visit(this.WorldToLocal(evt.position));
            BoardCoordinate[] completedCells = cells.ToArray();
            BoardEdge[] completedEdges = edges.ToArray();
            FlowTool completedTool = tool;
            CancelInput();
            GravityDirection? gravity = completedTool == FlowTool.GravityClear ? null : (GravityDirection)((int)completedTool - (int)FlowTool.Down);
            string error = completedTool == FlowTool.Wall || completedTool == FlowTool.WallErase
                ? LevelFlowEditing.SetWalls(level, completedEdges, completedTool == FlowTool.WallErase)
                : LevelFlowEditing.SetGravity(level, completedCells, gravity);
            source = JsonUtility.ToJson(level);
            Edited?.Invoke(error ?? "영역 설정을 적용했습니다. Undo 한 번으로 복구합니다.");
        }

        private static Vector2 Point(BoardCoordinate cell, bool vertex = false) => new Vector2(cell.Column * LevelBoardView.CellSize + (vertex ? 0 : LevelBoardView.CellSize / 2f), cell.Row * LevelBoardView.CellSize + (vertex ? 0 : LevelBoardView.CellSize / 2f));
        private static bool Visible(BoardCoordinate cell, bool vertex = false) => cell.Row >= 0 && cell.Column >= 0 && cell.Row < (BoardDefinition.DefaultRows + (vertex ? 1 : 0)) && cell.Column < (BoardDefinition.DefaultColumns + (vertex ? 1 : 0));
        private static void Line(Painter2D painter, Vector2 a, Vector2 b, Color color, float width)
        {
            painter.strokeColor = color; painter.lineWidth = width;
            painter.BeginPath(); painter.MoveTo(a); painter.LineTo(b); painter.Stroke();
        }
        private static void Arrow(Painter2D painter, Vector2 a, Vector2 b, Color color)
        {
            Line(painter, a, b, color, 1.5f);
            Vector2 direction = (b - a).normalized, side = new Vector2(-direction.y, direction.x);
            Line(painter, b, b - direction * 5 + side * 3, color, 1.5f);
            Line(painter, b, b - direction * 5 - side * 3, color, 1.5f);
        }
        private static Color WireColor(int index) => LevelBoardView.Swatches[index % LevelBoardView.Swatches.Length];

        private void Draw(MeshGenerationContext context)
        {
            if (level?.Flow == null || !level.Flow.ListsPresent) return;
            Painter2D painter = context.painter2D;
            double time = EditorApplication.timeSinceStartup;
            bool flowPulse = ((int)(time / 3) % 2) == 0;
            float phase = (float)(time % 3 / 3);
            Color flowColor = new Color(0.4f, 0.88f, 1, flowPulse ? 0.85f : 0.45f);
            for (int row = 0; row < BoardDefinition.DefaultRows; row++)
                for (int column = 0; column < BoardDefinition.DefaultColumns; column++)
                {
                    BoardCoordinate cell = new BoardCoordinate(row, column);
                    if (!LevelFlowRules.Active(level, cell)) continue;
                    GravityDirection direction = LevelFlowRules.GravityAt(level, cell);
                    Vector2 delta = direction switch { GravityDirection.Up => Vector2.up * -1, GravityDirection.Left => Vector2.left, GravityDirection.Right => Vector2.right, _ => Vector2.up };
                    Vector2 center = Point(cell) + new Vector2(12, 11);
                    Arrow(painter, center - delta * 4, center + delta * 4, new Color(flowColor.r, flowColor.g, flowColor.b, flowColor.a * 0.6f));
                }
            foreach (FlowPathCell path in level.Flow.Paths)
            {
                if (!Visible(path.Coordinate)) continue;
                if (path.IsEnd) Line(painter, Point(path.Coordinate) + new Vector2(-7, 8), Point(path.Coordinate) + new Vector2(7, 8), flowColor, 3);
                else if (Visible(path.Next))
                {
                    Arrow(painter, Point(path.Coordinate), Point(path.Next), flowColor);
                    if (flowPulse) Mark(painter, Vector2.Lerp(Point(path.Coordinate), Point(path.Next), phase), flowColor, 3);
                }
            }
            foreach (BoardEdge wall in level.Flow.Walls)
            {
                if (!wall.IsAdjacent || !Visible(wall.A) || !Visible(wall.B)) continue;
                BoardEdge segment = LevelFlowRules.WallSegment(wall);
                if (LevelBoardArtwork.Wall(segment.A.Column == segment.B.Column) == null || HighlightWall.HasValue && HighlightWall.Value.Equals(wall))
                    Line(painter, Point(segment.A, true), Point(segment.B, true), HighlightWall.HasValue && HighlightWall.Value.Equals(wall) ? Color.white : new Color(0.75f, 0.78f, 0.84f), 8);
            }
            if (level.Connections != null)
                for (int i = 0; i < level.Connections.Count; i++)
                {
                    LevelConnectionDefinition connection = level.Connections[i];
                    if (connection.Vertices == null) continue;
                    Color color = WireColor(i); color.a = i == HighlightConnection ? 1 : flowPulse ? 0.5f : 0.95f;
                    foreach (BoardEdge segment in LevelFlowRules.Segments(connection.Vertices))
                    {
                        if (!Visible(segment.A, true) || !Visible(segment.B, true)) continue;
                        if (LevelBoardArtwork.Wire(segment.A.Column == segment.B.Column) == null || i == HighlightConnection)
                            Line(painter, Point(segment.A, true), Point(segment.B, true), color, i == HighlightConnection ? 6 : 2);
                    }
                    if (connection.Vertices.Count > 0)
                    {
                        foreach (BoardCoordinate terminal in new[] { connection.Vertices[0], connection.Vertices[connection.Vertices.Count - 1] })
                            if (Visible(terminal, true) && LevelBoardArtwork.Terminal(ConnectionSlot(connection), true) == null) Mark(painter, Point(terminal, true), color, 5);
                    }
                }
            foreach (BoardCoordinate cell in cells) Mark(painter, Point(cell), Color.yellow, 9);
            foreach (BoardEdge wall in edges)
            {
                BoardEdge segment = LevelFlowRules.WallSegment(wall);
                Line(painter, Point(segment.A, true), Point(segment.B, true), Color.yellow, 5);
            }
            DrawMerge(painter);
            if (tool == FlowTool.Path) DrawPathCandidates(painter);
            for (int i = 0; i < draft.Count; i++)
            {
                Mark(painter, Point(draft[i], tool == FlowTool.Wire), Color.yellow, 4);
                if (i > 0)
                {
                    if (tool == FlowTool.Path) Arrow(painter, Point(draft[i - 1]), Point(draft[i]), Color.yellow);
                    else Line(painter, Point(draft[i - 1], true), Point(draft[i], true), Color.yellow, 3);
                }
            }
        }

        private static void Mark(Painter2D painter, Vector2 point, Color color, float radius)
        {
            painter.fillColor = color;
            painter.BeginPath(); painter.Arc(point, radius, 0, 360); painter.Fill();
        }

        private void RefreshLabels()
        {
            Clear();
            artworkPulses.Clear();
            if (level?.Flow == null || !level.Flow.ListsPresent) return;
            RefreshArtwork();
            for (int i = 0; i < level.Flow.Portals.Count; i++)
            {
                FlowPortal portal = level.Flow.Portals[i];
                AddLabel(portal.Entrance, "입" + (i + 1), WireColor(i));
                if (portal.HasExit) AddLabel(portal.Exit, "출" + (i + 1), WireColor(i));
            }
            foreach (BoardCoordinate arrival in level.Flow.Arrivals) AddLabel(arrival, "◎", Color.yellow);
            MergeLabels();
            if (level.Connections == null) return;
            for (int i = 0; i < level.Connections.Count; i++)
            {
                LevelConnectionDefinition connection = level.Connections[i];
                int generator = LevelConnectionRules.Find(level, connection.GeneratorId), target = LevelConnectionRules.Find(level, connection.TargetId);
                if (generator >= 0) AddLabel(level.Obstacles[generator].Coordinate, "◆" + (i + 1), WireColor(i), i * 12);
                if (target >= 0) AddLabel(level.Obstacles[target].Coordinate, "◆" + (i + 1), WireColor(i));
            }
        }

        private void AddLabel(BoardCoordinate cell, string text, Color color, int offset = 0)
        {
            if (!Visible(cell)) return;
            Label label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute;
            label.style.left = cell.Column * LevelBoardView.CellSize + 1;
            label.style.top = cell.Row * LevelBoardView.CellSize + offset;
            label.style.fontSize = 10;
            label.style.color = color;
            label.style.backgroundColor = new Color(0.08f, 0.1f, 0.12f, 0.8f);
            Add(label);
        }
    }
}
