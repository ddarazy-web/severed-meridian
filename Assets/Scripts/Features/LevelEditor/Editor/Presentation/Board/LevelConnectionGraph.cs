using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    // 연결점은 기존 전선의 시작 꼭짓점으로 식별한다. 저장 형식에 슬롯 상태를 중복 저장하지 않는다.
    public sealed partial class LevelConnectionGraph : VisualElement
    {
        private LevelDefinition level;
        private string snapshot;
        private BoardCoordinate? selection;
        private bool enabled;
        private int sourceBody = -1, sourceSlot, pointer;
        private int targetBody = -1, targetSlot;
        private Vector2 cursor, sourcePoint;
        private string routeError;
        private List<BoardCoordinate> preview;
        private readonly HashSet<int> connectionCandidates = new HashSet<int>();
        public bool IsDragging => sourceBody >= 0 || portalSource.HasValue;
        public event Action<string> Edited;
        private static readonly Color Unconnected = new Color(0.19f, 0.24f, 0.3f);
        private static readonly Color Connected = new Color(0.4f, 0.92f, 0.7f);

        public LevelConnectionGraph()
        {
            name = "connection-graph"; pickingMode = PickingMode.Ignore; focusable = true;
            style.position = Position.Absolute; style.left = style.top = 0; style.width = style.height = 400;
            generateVisualContent += DrawPreview;
            RegisterCallback<PointerMoveEvent>(Move);
            RegisterCallback<PointerUpEvent>(Up);
            RegisterCallback<PointerCaptureOutEvent>(_ => Cancel());
            RegisterCallback<DetachFromPanelEvent>(_ => Cancel());
            RegisterCallback<AttachToPanelEvent>(_ => { LevelBoardArtwork.Loaded += RefreshPortArtwork; LevelBoardArtwork.Acquire(); });
            RegisterCallback<DetachFromPanelEvent>(_ => { LevelBoardArtwork.Loaded -= RefreshPortArtwork; LevelBoardArtwork.Release(); });
            RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Escape && IsDragging) { Cancel(); evt.StopImmediatePropagation(); } });
        }
        public void Display(LevelDefinition value, BoardCoordinate? selected, bool show)
        {
            // 연결을 끄는 도중 원본이나 선택이 바뀌면 임시 선을 취소한다.
            // snapshot은 배열 인덱스를 기준으로 잡은 드래그 대상이 아직 같은 대상인지 확인하는 기준이다.
            string current = value == null ? null : JsonUtility.ToJson(value);
            if (level != value || snapshot != current || !Nullable.Equals(selection, selected) || enabled != show) Cancel();
            level = value; snapshot = current; selection = selected; enabled = show;
            Rebuild();
        }
        private static BoardCoordinate Slot(ObstaclePlacementDefinition body, int slot) => slot == 0 ? new BoardCoordinate(body.Coordinate.Row, body.Coordinate.Column + 1) :
            slot == 1 ? new BoardCoordinate(body.Coordinate.Row + 1, body.Coordinate.Column + 2) : new BoardCoordinate(body.Coordinate.Row + 2, body.Coordinate.Column + 1);
        private static Vector2 Point(BoardCoordinate vertex) => new Vector2(vertex.Column * 40, vertex.Row * 40);
        private BoardCoordinate TargetTerminal(int body)
        {
            Vector2 point = PortPoint(body, 0);
            return new BoardCoordinate(Mathf.RoundToInt(point.y / 40), Mathf.RoundToInt(point.x / 40));
        }
        private int[] Connections(int body)
        {
            // 발전기의 세 점은 저장 데이터에 별도 슬롯 번호를 만들지 않고 전선의 시작 꼭짓점으로 찾는다.
            // 기존 전선에 표준 꼭짓점이 없으면 빈 슬롯에 순서대로 배정하여 이전 데이터도 표시한다.
            // 반환 배열의 -1은 연결되지 않은 점이다. 일반 장애물은 한 연결점만 가진다.
            ObstaclePlacementDefinition obstacle = level.Obstacles[body];
            if (obstacle.Kind != ObstacleKind.Generator) return new[] { level.Connections.ToList().FindIndex(c => c.TargetId == obstacle.Id) };
            int[] slots = { -1, -1, -1 }; List<int> other = new List<int>();
            for (int i = 0; i < level.Connections.Count; i++)
            {
                LevelConnectionDefinition connection = level.Connections[i];
                if (connection.GeneratorId != obstacle.Id) continue;
                int slot = Array.FindIndex(new[] { 0, 1, 2 }, s => connection.Vertices?.Count > 0 && Slot(obstacle, s).Equals(connection.Vertices[0]));
                if (slot >= 0 && slots[slot] < 0) slots[slot] = i; else other.Add(i);
            }
            foreach (int index in other) { int slot = Array.IndexOf(slots, -1); if (slot >= 0) slots[slot] = index; }
            return slots;
        }
        private Vector2 PortPoint(int body, int slot)
        {
            ObstaclePlacementDefinition obstacle = level.Obstacles[body]; int connection = Connections(body)[slot];
            if (connection >= 0 && level.Connections[connection].Vertices?.Count > 0)
                return Point(obstacle.Kind == ObstacleKind.Generator ? level.Connections[connection].Vertices[0] : level.Connections[connection].Vertices.Last());
            return Point(obstacle.Kind == ObstacleKind.Generator ? Slot(obstacle, slot) : new BoardCoordinate(obstacle.Coordinate.Row + LevelPlacementRules.Size(obstacle.Kind), obstacle.Coordinate.Column + LevelPlacementRules.Size(obstacle.Kind)));
        }
        private void Rebuild()
        {
            Clear();
            MarkDirtyRepaint();
            if (!enabled || level == null || !LevelFlowEditing.CanEdit(level) || level.Connections == null) return;
            BuildPortalPorts();
            if (portalSource.HasValue) return;
            int selected = selection.HasValue ? LevelPlacementRules.Find(level, PlacementLayer.Obstacle, selection.Value) : -1;
            if (selected < 0) return;
            string selectedId = level.Obstacles[selected].Id;
            foreach (int body in connectionCandidates)
                Add(CreateCandidate("connection-candidate-" + body, level.Obstacles[body].Coordinate, LevelPlacementRules.Size(level.Obstacles[body].Kind), false));
            // 선의 작은 히트 영역은 선택된 본체의 연결에만 만든다. 보드 칠하기는 가로채지 않는다.
            for (int index = 0; index < level.Connections.Count; index++)
            {
                LevelConnectionDefinition connection = level.Connections[index];
                if (connection.GeneratorId != selectedId && connection.TargetId != selectedId) continue;
                if (connection.Vertices == null) continue;
                foreach (BoardEdge edge in LevelFlowRules.Segments(connection.Vertices))
                {
                    Vector2 a = Point(edge.A), b = Point(edge.B);
                    VisualElement line = new VisualElement { name = "connection-line-" + index };
                    line.style.position = Position.Absolute; line.style.left = Mathf.Min(a.x, b.x) - 4; line.style.top = Mathf.Min(a.y, b.y) - 4;
                    line.style.width = Mathf.Abs(a.x - b.x) + 8; line.style.height = Mathf.Abs(a.y - b.y) + 8;
                    int connectionIndex = index;
                    AddDeleteMenu(line, () => LevelConnectionEditing.Remove(level, connectionIndex)); Add(line);
                }
            }
            for (int body = 0; body < level.Obstacles.Count; body++)
            {
                ObstaclePlacementDefinition obstacle = level.Obstacles[body];
                if (obstacle.Kind != ObstacleKind.Generator && !LevelConnectionRules.IsTarget(obstacle.Kind)) continue;
                bool partner = IsDragging && (obstacle.Kind == ObstacleKind.Generator) != (level.Obstacles[sourceBody].Kind == ObstacleKind.Generator);
                bool linked = level.Connections.Any(c => (c.GeneratorId == selectedId && c.TargetId == obstacle.Id) || (c.TargetId == selectedId && c.GeneratorId == obstacle.Id));
                if (body != selected && body != sourceBody && !partner && !linked) continue;
                int[] connections = Connections(body);
                for (int slot = 0; slot < connections.Length; slot++) AddPort(body, slot, connections[slot]);
            }
        }
        private void AddPort(int body, int slot, int connection)
        {
            Vector2 point = PortPoint(body, slot);
            VisualElement port = CreatePort("connection-port-" + body + "-" + slot, point, connection >= 0,
                connection >= 0 ? "연결됨 · 우클릭으로 해제" : "드래그해서 발전기와 장애물을 연결");
            port.style.left = point.x - 10; port.style.top = point.y - 10; port.style.width = port.style.height = 20;
            SetPortArtwork(port, body, slot, connection);
            port.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                evt.StopImmediatePropagation();
                if (connection >= 0) { Edited?.Invoke("이미 연결된 점입니다. 우클릭으로 먼저 해제하세요."); return; }
                if (!Current()) return;
                sourceBody = body; sourceSlot = slot;
                connectionCandidates.Clear();
                bool fromGenerator = level.Obstacles[body].Kind == ObstacleKind.Generator;
                for (int candidate = 0; candidate < level.Obstacles.Count; candidate++)
                {
                    ObstacleKind kind = level.Obstacles[candidate].Kind;
                    if (candidate == body || (fromGenerator ? !LevelConnectionRules.IsTarget(kind) : kind != ObstacleKind.Generator)) continue;
                    int count = fromGenerator ? 1 : 3;
                    for (int candidateSlot = 0; candidateSlot < count; candidateSlot++)
                        if (FindConnectionWire(candidate, candidateSlot, out _) == null) { connectionCandidates.Add(candidate); break; }
                }
                BeginDrag(point, evt.pointerId);
            });
            if (connection >= 0) AddDeleteMenu(port, () => LevelConnectionEditing.Remove(level, connection));
            Add(port);
        }
        private void RefreshPortArtwork()
        {
            // 비동기 이미지 로드로 입력 대상을 다시 만들면 진행 중인 포인터 적중 정보가 낡을 수 있다.
            foreach (VisualElement port in Children())
            {
                if (port.name == null || !port.name.StartsWith("connection-port-")) continue;
                string[] parts = port.name.Split('-'); int body = int.Parse(parts[2]), slot = int.Parse(parts[3]);
                SetPortArtwork(port, body, slot, Connections(body)[slot]);
            }
        }
        private void SetPortArtwork(VisualElement port, int body, int slot, int connection)
        {
            int artSlot = slot;
            if (connection >= 0 && level.Obstacles[body].Kind != ObstacleKind.Generator)
            {
                int generator = LevelConnectionRules.Find(level, level.Connections[connection].GeneratorId);
                if (generator >= 0) artSlot = Mathf.Max(0, System.Array.IndexOf(Connections(generator), connection));
            }
            Sprite art = LevelBoardArtwork.Terminal(artSlot, connection >= 0);
            if (art == null) return;
            port.style.backgroundImage = new StyleBackground(art);
            port.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            port.style.backgroundColor = Color.clear;
            port.style.borderLeftWidth = port.style.borderRightWidth = port.style.borderTopWidth = port.style.borderBottomWidth = 0;
        }
        private static VisualElement CreatePort(string name, Vector2 point, bool connected, string tooltip)
        {
            VisualElement port = new VisualElement { name = name, tooltip = tooltip };
            port.style.position = Position.Absolute; port.style.left = point.x - 8; port.style.top = point.y - 8; port.style.width = port.style.height = 16;
            port.style.borderTopLeftRadius = port.style.borderTopRightRadius = port.style.borderBottomLeftRadius = port.style.borderBottomRightRadius = 8;
            port.style.borderTopWidth = port.style.borderBottomWidth = port.style.borderLeftWidth = port.style.borderRightWidth = 2;
            port.style.borderTopColor = port.style.borderBottomColor = port.style.borderLeftColor = port.style.borderRightColor = new Color(0.75f, 0.84f, 0.94f);
            port.style.backgroundColor = connected ? Connected : Unconnected;
            return port;
        }
        private void BeginDrag(Vector2 point, int pointerId)
        {
            sourcePoint = cursor = point; pointer = pointerId;
            Focus(); this.CapturePointer(pointer); Rebuild(); MarkDirtyRepaint();
        }
        private static VisualElement CreateCandidate(string name, BoardCoordinate cell, int size, bool hovered)
        {
            var highlight = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            highlight.style.position = Position.Absolute;
            highlight.style.left = cell.Column * 40 + 2; highlight.style.top = cell.Row * 40 + 2;
            highlight.style.width = highlight.style.height = size * 40 - 4;
            ColorCandidate(highlight, hovered);
            return highlight;
        }
        private static void ColorCandidate(VisualElement highlight, bool hovered)
        {
            Color color = hovered ? Color.yellow : Connected;
            highlight.style.backgroundColor = new Color(color.r, color.g, color.b, hovered ? 0.3f : 0.14f);
            highlight.style.borderTopWidth = highlight.style.borderBottomWidth = highlight.style.borderLeftWidth = highlight.style.borderRightWidth = hovered ? 3 : 2;
            highlight.style.borderTopColor = highlight.style.borderBottomColor = highlight.style.borderLeftColor = highlight.style.borderRightColor = color;
        }
        private string FindConnectionWire(int body, int portSlot, out List<BoardCoordinate> path)
        {
            path = null;
            bool fromGenerator = level.Obstacles[sourceBody].Kind == ObstacleKind.Generator;
            int generator = fromGenerator ? sourceBody : body, target = fromGenerator ? body : sourceBody;
            int slot = fromGenerator ? sourceSlot : portSlot;
            return Connections(generator)[slot] >= 0 ? "이 발전기 연결점은 사용 중입니다." : LevelConnectionEditing.FindWire(level,
                level.Obstacles[generator].Id, level.Obstacles[target].Id, Slot(level.Obstacles[generator], slot), out path, TargetTerminal(target));
        }
        private void AddDeleteMenu(VisualElement element, Func<string> remove)
        {
            LevelDefinition owner = level; string version = snapshot;
            element.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.StopPropagation();
                evt.menu.AppendAction("연결 해제", _ =>
                {
                    if (level != owner || version != snapshot || version != JsonUtility.ToJson(owner))
                    { Cancel(); Edited?.Invoke("레벨이 변경되었습니다. 연결을 다시 선택하세요."); return; }
                    if (!Current()) return;
                    Cancel(); string error = remove();
                    Edited?.Invoke(error ?? "연결을 해제했습니다. Undo로 복구할 수 있습니다.");
                });
            }));
        }
        private bool Current()
        {
            if (level != null && snapshot == JsonUtility.ToJson(level)) return true;
            Cancel(); Edited?.Invoke("레벨이 변경되었습니다. 다시 선택하세요."); return false;
        }
        private void Move(PointerMoveEvent evt)
        {
            if (!IsDragging || evt.pointerId != pointer) return;
            evt.StopImmediatePropagation(); if (!Current()) return;
            FindTarget(this.WorldToLocal(evt.position)); MarkDirtyRepaint();
        }
        private void FindTarget(Vector2 position)
        {
            if (portalSource.HasValue) { FindPortalTarget(position); return; }
            cursor = position; targetBody = -1; preview = null; routeError = null;
            bool fromGenerator = level.Obstacles[sourceBody].Kind == ObstacleKind.Generator;
            float closest = 22;
            for (int body = 0; body < level.Obstacles.Count; body++)
            {
                ObstaclePlacementDefinition obstacle = level.Obstacles[body];
                if (body == sourceBody || (obstacle.Kind == ObstacleKind.Generator) == fromGenerator || (!fromGenerator && obstacle.Kind != ObstacleKind.Generator) ||
                    (fromGenerator && !LevelConnectionRules.IsTarget(obstacle.Kind))) continue;
                int[] connections = Connections(body);
                for (int slot = 0; slot < connections.Length; slot++)
                {
                    float distance = Vector2.Distance(position, PortPoint(body, slot));
                    if (distance >= closest) continue;
                    closest = distance; targetBody = body; targetSlot = slot;
                }
            }
            if (targetBody >= 0)
            {
                routeError = FindConnectionWire(targetBody, targetSlot, out preview);
            }
            foreach (int body in connectionCandidates)
                ColorCandidate(this.Q("connection-candidate-" + body), body == targetBody && routeError == null);
            foreach (VisualElement port in Children().Where(c => c.name.StartsWith("connection-port-")))
            {
                string[] parts = port.name.Split('-'); int body = int.Parse(parts[2]), slot = int.Parse(parts[3]);
                port.style.backgroundColor = body == targetBody && slot == targetSlot ? (routeError == null ? Color.yellow : new Color(1, 0.3f, 0.3f)) : Connections(body)[slot] >= 0 ? Connected : Unconnected;
            }
        }
        private void Up(PointerUpEvent evt)
        {
            if (!IsDragging || evt.pointerId != pointer || evt.button != 0) return;
            evt.StopImmediatePropagation(); if (!Current()) return;
            FindTarget(this.WorldToLocal(evt.position));
            if (portalSource.HasValue) { CompletePortal(); return; }
            string message = "연결을 취소했습니다.";
            if (targetBody >= 0)
            {
                bool fromGenerator = level.Obstacles[sourceBody].Kind == ObstacleKind.Generator;
                int generator = fromGenerator ? sourceBody : targetBody, target = fromGenerator ? targetBody : sourceBody;
                int slot = fromGenerator ? sourceSlot : targetSlot;
                message = routeError ?? LevelConnectionEditing.ConnectAuto(level, level.Obstacles[generator].Id, level.Obstacles[target].Id, Slot(level.Obstacles[generator], slot), TargetTerminal(target)) ?? "연결 완료 · 전선 경로를 자동으로 만들었습니다. Undo 한 번으로 취소합니다.";
            }
            Cancel(); Edited?.Invoke(message);
        }
        public void Cancel()
        {
            bool captured = IsDragging; sourceBody = targetBody = -1; preview = null; routeError = null;
            portalSource = portalTarget = null;
            portalCandidates.Clear();
            connectionCandidates.Clear();
            if (captured && this.HasPointerCapture(pointer)) this.ReleasePointer(pointer);
            if (captured && panel != null) Rebuild();
            MarkDirtyRepaint();
        }
        private void DrawPreview(MeshGenerationContext context)
        {
            if (!enabled || level == null || !LevelFlowEditing.CanEdit(level)) return;
            Painter2D painter = context.painter2D;
            DrawPortals(painter);
            if (portalSource.HasValue) return;
            int selected = selection.HasValue ? LevelPlacementRules.Find(level, PlacementLayer.Obstacle, selection.Value) : -1;
            if (selected >= 0)
            {
                string id = level.Obstacles[selected].Id;
                painter.lineWidth = 3; painter.strokeColor = Connected;
                foreach (LevelConnectionDefinition connection in level.Connections)
                {
                    if ((connection.GeneratorId != id && connection.TargetId != id) || connection.Vertices == null || connection.Vertices.Count < 2) continue;
                    painter.BeginPath(); painter.MoveTo(Point(connection.Vertices[0]));
                    foreach (BoardCoordinate vertex in connection.Vertices.Skip(1)) painter.LineTo(Point(vertex));
                    painter.Stroke();
                }
            }
            if (!IsDragging) return;
            painter.lineWidth = 3; painter.strokeColor = routeError == null ? Color.yellow : new Color(1, 0.3f, 0.3f);
            painter.BeginPath();
            if (preview != null && routeError == null)
            { painter.MoveTo(Point(preview[0])); foreach (BoardCoordinate point in preview.Skip(1)) painter.LineTo(Point(point)); }
            else { painter.MoveTo(sourcePoint); painter.LineTo(cursor); }
            painter.Stroke();
        }
    }
}
