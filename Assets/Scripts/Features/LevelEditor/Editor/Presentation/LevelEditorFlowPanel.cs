using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        private LevelFlowOverlay flowOverlay;
        private LevelConnectionGraph connectionGraph;
        private FlowTool flowTool;
        private VisualElement flowProperties;
        private bool flowListExpanded;

        private void SetupFlowOverlay()
        {
            flowOverlay = new LevelFlowOverlay();
            board.Add(flowOverlay);
            board.Cancelled += flowOverlay.CancelInput;
            flowOverlay.CellSelected += SelectCell;
            flowOverlay.Edited += message => { operation.text = message; Refresh(); };
            connectionGraph = new LevelConnectionGraph(); board.Add(connectionGraph);
            board.Cancelled += connectionGraph.Cancel;
            connectionGraph.Edited += message => { operation.text = message; Refresh(); };
        }

        private void SetFlowTool(FlowTool tool)
        {
            board.CancelStroke();
            board.Brush = LevelBrush.Flow;
            toolPage = 1;
            flowTool = tool;
            operation.text = tool switch
            {
                FlowTool.Path => "인접 칸을 드래그 → 놓아서 확정. 초록 칸: 연결 가능. 끝 또는 기존 경로 합류. Esc: 취소. 클릭/Enter도 가능.",
                FlowTool.Wire => "발전기 외곽 꼭짓점부터 대상 외곽까지 틈을 따라 클릭 → 완료/Enter. Backspace: 한 점 취소.",
                FlowTool.Wall or FlowTool.WallErase => "두 칸 사이의 경계를 클릭/드래그하세요. Esc: 미완료 입력 취소.",
                FlowTool.Portal => "입구 칸을 클릭한 뒤 오른쪽에서 출구 지정을 누르세요. 미연결 상태도 저장됩니다.",
                FlowTool.PortalExit => "선택한 입구와 연결할 출구 칸을 클릭하세요.",
                FlowTool.Connect => "발전기에 연결할 장애물을 클릭하세요.",
                _ => "흐름 도구: 칸을 선택/칠하세요. Esc: 미완료 입력 취소."
            };
            Refresh();
        }

        private void BuildFlowMenus(bool editable)
        {
            AddFlowMenu("흐름·통로", "menu-flow", new[]
            {
                ("흐름 선택", FlowTool.Select), ("중력 ↓", FlowTool.Down), ("중력 ↑", FlowTool.Up),
                ("중력 ←", FlowTool.Left), ("중력 →", FlowTool.Right), ("중력 초기화", FlowTool.GravityClear),
                ("직접 경로", FlowTool.Path), ("경로 칸 삭제", FlowTool.PathErase), ("통로 입구", FlowTool.Portal),
                ("도착 바닥", FlowTool.Arrival), ("도착 바닥 삭제", FlowTool.ArrivalErase)
            }, editable);
            AddFlowMenu("지형", "menu-terrain", new[] { ("고철 벽", FlowTool.Wall), ("고철 벽 삭제", FlowTool.WallErase) }, editable);
            if (board.Brush == LevelBrush.Flow)
            {
                if (flowTool >= FlowTool.Down && flowTool <= FlowTool.GravityClear)
                {
                    Toggle rectangle = new Toggle("사각 구역 칠하기") { name = "gravity-rectangle", value = flowOverlay.RectangleGravity };
                    rectangle.RegisterValueChangedCallback(evt => { flowOverlay.CancelInput(); flowOverlay.RectangleGravity = evt.newValue; });
                    tools.Add(rectangle);
                }
                if (flowTool == FlowTool.Path || flowTool == FlowTool.Wire)
                    tools.Add(new Button(flowOverlay.Complete) { text = "경로 완료 (Enter)", name = "complete-flow" });
                if (flowTool == FlowTool.Wire)
                    tools.Add(new Button(flowOverlay.SavePartialWire) { text = "전선 중간 경로 기록", name = "save-partial-wire" });
                tools.Add(new Button(flowOverlay.CancelInput) { text = "입력 취소 (Esc)", name = "cancel-flow" });
            }
        }

        private void AddFlowMenu(string title, string name, (string Label, FlowTool Tool)[] choices, bool editable)
        {
            List<string> labels = new List<string> { "선택…" };
            labels.AddRange(choices.Select(choice => choice.Label));
            int selectedTool = board.Brush == LevelBrush.Flow ? Array.FindIndex(choices, choice => choice.Tool == flowTool) + 1 : 0;
            PopupField<string> menu = new PopupField<string>(title, labels, selectedTool) { name = name };
            menu.AddToClassList("compact-menu");
            menu.SetEnabled(editable && LevelFlowEditing.CanEdit(level));
            menu.RegisterValueChangedCallback(evt =>
            {
                int index = labels.IndexOf(evt.newValue) - 1;
                SetFlowTool(index < 0 ? FlowTool.Select : choices[index].Tool);
            });
            tools.Add(menu);
        }

        private void BuildConnectionList()
        {
            if (level.Flow == null || !level.Flow.ListsPresent || level.Connections == null) return;
            Foldout list = new Foldout { text = "흐름·연결 목록", name = "flow-connections", value = flowListExpanded };
            list.RegisterValueChangedCallback(evt => flowListExpanded = evt.newValue);
            tools.Add(list);
            List<LevelValidationIssue> flowIssues = new List<LevelValidationIssue>();
            LevelFlowRules.Validate(level, flowIssues);
            foreach (LevelValidationIssue issue in flowIssues)
                list.Add(FlowLink("! " + issue.Message + " " + issue.Coordinate, issue.Coordinate, () => HighlightFlowIssue(issue), "flow-issue-" + flowIssues.IndexOf(issue)));
            foreach (FlowPathCell path in level.Flow.Paths)
                if (path.IsEnd) list.Add(FlowLink("끝 " + path.Coordinate, path.Coordinate, null, "flow-end-" + path.Coordinate));
            for (int i = 0; i < level.Flow.Walls.Count; i++)
            {
                BoardEdge wall = level.Flow.Walls[i];
                list.Add(FlowLink("벽 " + wall, wall.A, () => flowOverlay.HighlightWall = wall, "flow-wall-" + i));
            }
            for (int i = 0; i < level.Flow.Portals.Count; i++)
            {
                FlowPortal portal = level.Flow.Portals[i];
                list.Add(FlowLink($"통로 {i + 1}: {portal.Entrance} → " + (portal.HasExit ? portal.Exit.ToString() : "미연결 !"), portal.Entrance, null, "flow-portal-" + i));
            }
            foreach (BoardCoordinate cell in level.Flow.Arrivals)
                list.Add(FlowLink("도착 " + cell, cell, null, "flow-arrival-" + cell));
            for (int i = 0; i < level.Connections.Count; i++)
            {
                int index = i;
                LevelConnectionDefinition connection = level.Connections[i];
                int generator = LevelConnectionRules.Find(level, connection.GeneratorId), target = LevelConnectionRules.Find(level, connection.TargetId);
                string from = generator >= 0 ? level.Obstacles[generator].Coordinate.ToString() : "ID 오류";
                string to = target >= 0 ? level.Obstacles[target].Coordinate.ToString() : "ID 오류";
                string invalid = LevelConnectionRules.TargetError(level, connection.GeneratorId, connection.TargetId, i) ??
                    LevelConnectionRules.WireError(level, connection.GeneratorId, connection.TargetId, connection.Vertices, i);
                list.Add(FlowLink($"◆{i + 1} {from} → {to}" + (invalid == null ? "" : " !"), generator >= 0 ? level.Obstacles[generator].Coordinate : null,
                    () => flowOverlay.HighlightConnection = index, "flow-connection-" + i));
            }
        }

        private void HighlightFlowIssue(LevelValidationIssue issue)
        {
            flowOverlay.HighlightWall = null;
            flowOverlay.HighlightConnection = -1;
            int start = issue.PropertyPath.IndexOf('['), end = issue.PropertyPath.IndexOf(']');
            if (start < 0 || end <= start || !int.TryParse(issue.PropertyPath.Substring(start + 1, end - start - 1), out int index)) return;
            if (issue.PropertyPath.StartsWith("flow.walls") && index >= 0 && index < (level.Flow?.Walls?.Count ?? 0))
                flowOverlay.HighlightWall = level.Flow.Walls[index];
            if (issue.PropertyPath.StartsWith("connections") && index >= 0 && index < (level.Connections?.Count ?? 0))
                flowOverlay.HighlightConnection = index;
            flowOverlay.MarkDirtyRepaint();
        }

        private Button FlowLink(string label, BoardCoordinate? coordinate, Action highlight, string name)
        {
            LevelDefinition owner = level;
            string snapshot = JsonUtility.ToJson(level);
            Button link = new Button(() =>
            {
                if (level != owner || JsonUtility.ToJson(level) != snapshot) { Refresh(); return; }
                SetFlowTool(FlowTool.Select);
                flowOverlay.HighlightWall = null;
                flowOverlay.HighlightConnection = -1;
                highlight?.Invoke();
                if (coordinate.HasValue && level.Board != null && level.Board.Contains(coordinate.Value))
                {
                    SelectCell(coordinate.Value);
                    boardScroll.ScrollTo(board.CellAt(coordinate.Value));
                }
                else operation.text = label + " · 보드 밖/ID 오류입니다. 기존 Inspector에서 확인하세요.";
                flowOverlay.MarkDirtyRepaint();
            }) { text = label, name = name };
            link.style.whiteSpace = WhiteSpace.Normal;
            return link;
        }

        private void BuildFlowProperties()
        {
            if (flowProperties == null) return;
            flowProperties.Clear();
            if (!selected.HasValue || !LevelFlowEditing.CanEdit(level)) return;
            BoardCoordinate cell = selected.Value;
            if (!level.Board.Contains(cell)) return;
            LevelDefinition owner = level;
            string snapshot = JsonUtility.ToJson(level);
            Foldout settings = new Foldout { text = "바닥 흐름·연결 " + cell, value = board.Brush == LevelBrush.Flow, name = "flow-properties" };
            flowProperties.Add(settings);
            Label gravity = new Label("현재 중력: " + new[] { "아래 ↓", "위 ↑", "왼쪽 ←", "오른쪽 →" }.ElementAtOrDefault((int)LevelFlowRules.GravityAt(level, cell)));
            settings.Add(gravity);
            void ActionButton(string text, string name, Func<string> action)
            {
                settings.Add(new Button(() =>
                {
                    board.CancelStroke();
                    if (level != owner || JsonUtility.ToJson(level) != snapshot) { Refresh(); return; }
                    operation.text = action() ?? "설정을 적용했습니다.";
                    Refresh();
                }) { text = text, name = name });
            }
            ActionButton("중력 기본값으로", "reset-gravity", () => LevelFlowEditing.SetGravity(level, new[] { cell }, null));
            if (level.Flow.Paths.Any(path => path.Coordinate.Equals(cell)))
                ActionButton("이 칸의 직접 경로 삭제", "remove-flow-path", () => LevelFlowEditing.RemovePath(level, cell));
            List<BoardCoordinate> candidates = LevelFlowRules.Sources(level, cell);
            FlowMerge[] saved = level.Flow.Merges.Where(item => item.Coordinate.Equals(cell)).ToArray();
            if (candidates.Count > 1 || saved.Length > 0)
            {
                List<BoardCoordinate> ordered = LevelFlowEditing.MergeOrder(level, cell);
                BuildMergeList(settings, cell, ordered);
                if (candidates.Count > 1) ActionButton("표시 순서 확정", "confirm-merge", () => LevelFlowEditing.SetMerge(level, cell, ordered));
                ActionButton("합류 우선순위 초기화", "reset-merge", () => LevelFlowEditing.RemoveMerge(level, cell));
            }
            for (int i = 0; i < level.Flow.Portals.Count; i++)
            {
                FlowPortal portal = level.Flow.Portals[i];
                if (!portal.Entrance.Equals(cell) && !(portal.HasExit && portal.Exit.Equals(cell))) continue;
                settings.Add(new Label($"통로 {i + 1}: {portal.Entrance} → " + (portal.HasExit ? portal.Exit.ToString() : "미연결")));
                ActionButton("출구 지정 / 이동", "select-portal-exit", () => { SetFlowTool(FlowTool.PortalExit); flowOverlay.PortalEntrance = portal.Entrance; return "출구 칸을 클릭하세요."; });
                ActionButton("출구만 삭제", "remove-portal-exit", () => LevelFlowEditing.SetPortal(level, portal.Entrance, null));
                ActionButton("통로 쌍 삭제", "remove-portal", () => LevelFlowEditing.RemovePortal(level, portal.Entrance));
            }
            if (level.Flow.Arrivals.Contains(cell)) ActionButton("도착 바닥 삭제", "remove-arrival", () => LevelFlowEditing.SetArrival(level, cell, true));
            int body = LevelPlacementRules.Find(level, PlacementLayer.Obstacle, cell);
            if (body >= 0)
            {
                ObstaclePlacementDefinition obstacle = level.Obstacles[body];
                if (obstacle.Kind == ObstacleKind.Generator)
                    ActionButton("연결 대상 추가", "add-generator-target", () => { SetFlowTool(FlowTool.Connect); flowOverlay.GeneratorId = obstacle.Id; return "연결할 장애물을 클릭하세요."; });
                for (int i = 0; i < level.Connections.Count; i++)
                {
                    int index = i;
                    LevelConnectionDefinition connection = level.Connections[i];
                    if (connection.GeneratorId != obstacle.Id && connection.TargetId != obstacle.Id) continue;
                    Label status = new Label($"◆{i + 1}: " + (LevelConnectionRules.WireError(level, connection.GeneratorId, connection.TargetId, connection.Vertices, i) ?? "전선 연결됨"));
                    status.style.whiteSpace = WhiteSpace.Normal; settings.Add(status);
                    ActionButton($"◆{i + 1} 전선 그리기 / 재지정", "edit-wire-" + i, () => { SetFlowTool(FlowTool.Wire); flowOverlay.WireIndex = index; flowOverlay.HighlightConnection = index; return "외곽 꼭짓점을 순서대로 선택하세요."; });
                    ActionButton($"◆{i + 1} 연결 삭제", "remove-connection-" + i, () => LevelConnectionEditing.Remove(level, index));
                }
            }
        }
    }
}
