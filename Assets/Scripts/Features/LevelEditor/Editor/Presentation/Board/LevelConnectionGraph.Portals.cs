using Board;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System.Collections.Generic;

namespace Levels.Editor
{
    // 입력 캡처·취소·메뉴는 공통 연결 화면이 소유하고 통로의 역할/방향만 여기서 처리한다.
    public sealed partial class LevelConnectionGraph
    {
        private BoardCoordinate? portalSource, portalTarget;
        private bool portalFromEntrance;
        private readonly List<BoardCoordinate> portalCandidates = new List<BoardCoordinate>();
        private static Vector2 CellPoint(BoardCoordinate cell) => Point(cell) + Vector2.one * (LevelBoardView.CellSize / 2f);
        private bool SelectedPortal(FlowPortal portal) => selection.HasValue &&
            (portal.Entrance.Equals(selection.Value) || (portal.HasExit && portal.Exit.Equals(selection.Value)));

        private void BuildPortalPorts()
        {
            if (sourceBody >= 0 || level.Flow?.Portals == null) return;
            foreach (BoardCoordinate cell in portalCandidates)
            {
                bool hovered = portalTarget.HasValue && cell.Equals(portalTarget.Value);
                Add(CreateCandidate("portal-candidate-" + cell.Row + "-" + cell.Column, cell, 1, hovered));
            }
            foreach (FlowPortal portal in level.Flow.Portals)
            {
                bool selected = SelectedPortal(portal);
                if (selected || (portalSource.HasValue && !portalFromEntrance))
                    AddPortalPort(portal.Entrance, true, portal.HasExit, portal.Entrance);
                if (selected && portal.HasExit)
                {
                    AddPortalPort(portal.Exit, false, true, portal.Entrance);
                    var line = new PortalLineHit(CellPoint(portal.Entrance), CellPoint(portal.Exit)) { name = "portal-line-" + portal.Entrance.Row + "-" + portal.Entrance.Column };
                    AddDeleteMenu(line, () => LevelFlowEditing.SetPortal(level, portal.Entrance, null));
                    Insert(0, line);
                }
            }
            if (portalSource.HasValue)
            {
                if (!portalFromEntrance) AddPortalPort(portalSource.Value, false, false, null);
                else if (portalTarget.HasValue && !level.Flow.Portals.Any(p => p.Entrance.Equals(portalTarget.Value) || (p.HasExit && p.Exit.Equals(portalTarget.Value))))
                    AddPortalPort(portalTarget.Value, false, false, null);
            }
            else if (selection.HasValue && LevelFlowRules.Active(level, selection.Value) &&
                !level.Flow.Arrivals.Contains(selection.Value) && level.Flow.Portals.Any(p => !p.HasExit) &&
                !level.Flow.Portals.Any(p => SelectedPortal(p)))
                AddPortalPort(selection.Value, false, false, null);
        }

        private void AddPortalPort(BoardCoordinate cell, bool entrance, bool connected, BoardCoordinate? owner)
        {
            VisualElement port = CreatePort("portal-" + (entrance ? "entrance-" : "exit-") + cell.Row + "-" + cell.Column,
                CellPoint(cell), connected, connected ? "통로 연결됨 · 우클릭으로 해제" : entrance ? "입구 · 출구로 쓸 칸까지 드래그" : "출구 후보 · 미연결 입구까지 드래그");
            // 입구는 원, 출구는 사각형으로 구별한다. 색을 구별하기 어려워도 역할을 읽을 수 있다.
            if (!entrance) port.style.borderTopLeftRadius = port.style.borderTopRightRadius = port.style.borderBottomLeftRadius = port.style.borderBottomRightRadius = 2;
            if (portalSource.HasValue && portalTarget.HasValue && cell.Equals(portalTarget.Value))
                port.style.backgroundColor = routeError == null ? Color.yellow : new Color(1, 0.3f, 0.3f);
            port.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                evt.StopImmediatePropagation();
                if (!Current()) return;
                if (connected) { Edited?.Invoke("이미 연결된 통로입니다. 우클릭으로 먼저 해제하세요."); return; }
                portalSource = cell; portalFromEntrance = entrance;
                portalCandidates.Clear();
                // 후보는 시작 시 한 번만 계산하고 원본 변경 시 공통 취소 처리에서 비운다.
                for (int row = 0; row < BoardDefinition.DefaultRows; row++)
                    for (int column = 0; column < BoardDefinition.DefaultColumns; column++)
                    {
                        var candidate = new BoardCoordinate(row, column);
                        if (PortalTargetError(candidate) == null) portalCandidates.Add(candidate);
                    }
                BeginDrag(CellPoint(cell), evt.pointerId);
            });
            if (connected && owner.HasValue) AddDeleteMenu(port, () => LevelFlowEditing.SetPortal(level, owner.Value, null));
            Add(port);
        }

        private void FindPortalTarget(Vector2 position)
        {
            cursor = position; portalTarget = null; routeError = null;
            if (position.x >= 0 && position.y >= 0 && position.x < (LevelBoardView.CellSize * BoardDefinition.DefaultColumns) && position.y < (LevelBoardView.CellSize * BoardDefinition.DefaultRows))
            {
                var cell = new BoardCoordinate(Mathf.FloorToInt(position.y / LevelBoardView.CellSize), Mathf.FloorToInt(position.x / LevelBoardView.CellSize));
                if (LevelFlowRules.Active(level, cell))
                {
                    portalTarget = cell;
                    routeError = PortalTargetError(cell);
                }
            }
            Rebuild();
        }

        private string PortalTargetError(BoardCoordinate cell)
        {
            if (portalFromEntrance) return LevelFlowEditing.PortalError(level, portalSource.Value, cell);
            int index = level.Flow.Portals.ToList().FindIndex(p => p.Entrance.Equals(cell));
            return index < 0 ? "미연결 통로 입구에 놓으세요." : level.Flow.Portals[index].HasExit ? "이미 연결된 입구입니다." :
                LevelFlowEditing.PortalError(level, cell, portalSource.Value);
        }

        private void CompletePortal()
        {
            string message = "통로 연결을 취소했습니다.";
            if (portalTarget.HasValue)
                message = routeError ?? RunEdit("통로 연결", () => LevelFlowEditing.SetPortal(level,
                    portalFromEntrance ? portalSource.Value : portalTarget.Value,
                    portalFromEntrance ? portalTarget.Value : portalSource.Value)) ?? "통로 연결 완료 · 화살표는 입구에서 출구 방향입니다.";
            Cancel(); Edited?.Invoke(message);
        }

        private void DrawPortals(Painter2D painter)
        {
            if (sourceBody >= 0) return;
            foreach (FlowPortal portal in level.Flow.Portals)
                if (portal.HasExit && SelectedPortal(portal)) PortalArrow(painter, CellPoint(portal.Entrance), CellPoint(portal.Exit), Connected);
            if (!portalSource.HasValue) return;
            Vector2 end = portalTarget.HasValue ? CellPoint(portalTarget.Value) : cursor;
            PortalArrow(painter, portalFromEntrance ? sourcePoint : end, portalFromEntrance ? end : sourcePoint,
                routeError == null ? Color.yellow : new Color(1, 0.3f, 0.3f));
        }

        private static void PortalArrow(Painter2D painter, Vector2 from, Vector2 to, Color color)
        {
            Vector2 direction = (to - from).normalized, side = new Vector2(-direction.y, direction.x);
            Vector2 tip = to - direction * 11;
            painter.lineWidth = 3; painter.strokeColor = color;
            painter.BeginPath(); painter.MoveTo(from); painter.LineTo(tip); painter.Stroke();
            painter.BeginPath(); painter.MoveTo(tip - direction * 8 + side * 5); painter.LineTo(tip);
            painter.LineTo(tip - direction * 8 - side * 5); painter.Stroke();
        }

        private sealed class PortalLineHit : VisualElement
        {
            private readonly Vector2 start, end;
            public PortalLineHit(Vector2 a, Vector2 b)
            {
                Vector2 origin = Vector2.Min(a, b) - Vector2.one * 6;
                start = a - origin; end = b - origin;
                style.position = Position.Absolute; style.left = origin.x; style.top = origin.y;
                style.width = Mathf.Abs(a.x - b.x) + 12; style.height = Mathf.Abs(a.y - b.y) + 12;
            }
            public override bool ContainsPoint(Vector2 localPoint)
            {
                Vector2 delta = end - start;
                float t = delta.sqrMagnitude == 0 ? 0 : Mathf.Clamp01(Vector2.Dot(localPoint - start, delta) / delta.sqrMagnitude);
                return Vector2.Distance(localPoint, start + delta * t) <= 6;
            }
        }
    }
}
