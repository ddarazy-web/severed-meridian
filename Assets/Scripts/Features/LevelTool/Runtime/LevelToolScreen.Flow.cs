#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Editing;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private string flowTool;
        private readonly List<int> flowRoute = new List<int>();
        private int flowFirst = -1;
        private void CancelFlowTool() { flowTool = null; flowFirst = -1; flowRoute.Clear(); }
        private void StartFlowTool(string tool)
        { CancelFlowTool(); flowTool = tool; brush = null; moveBody = null; Refresh(); }

        private bool UseFlowTool(int cell)
        {
            if (flowTool == null) return false;
            if (flowTool == "path")
            {
                if (flowRoute.Contains(cell)) { Show("이미 찍은 칸입니다. 입력 취소 후 다시 지정할 수 있습니다."); return true; }
                flowRoute.Add(cell); Session.SelectCells(flowRoute); Refresh(); return true;
            }
            if (flowFirst < 0)
            {
                if (flowTool == "portal")
                {
                    Change("포털 입구", level => { FlowDocumentEditing.SetPortal(level, cell, null); flowFirst = cell; });
                }
                else { flowFirst = cell; Session.SelectCells(new[] { cell }); Refresh(); }
            }
            else
            {
                Change(flowTool == "portal" ? "포털 출구" : "벽 편집", level =>
                {
                    if (flowTool == "portal") FlowDocumentEditing.SetPortal(level, flowFirst, cell);
                    else FlowDocumentEditing.SetWall(level, definitions, flowFirst, cell, flowTool == "wall-erase");
                });
                // 실패 시에도 다음 칸으로 재시도할 수 있도록 첫 칸은 취소 전까지 유지한다.
                if (flowTool == "portal" && Session.Get(Session.SelectedLevelId).Data["flow"]["portals"].Any(p => Cell(p["entrance"]) == flowFirst && (bool)p["hasExit"])) CancelFlowTool();
                else if (flowTool != "portal") flowFirst = -1;
                Refresh();
            }
            return true;
        }

        private static int Cell(JToken coordinate) => (int)coordinate["row"] * 9 + (int)coordinate["column"];
        private static string CellName(int cell) => (cell / 9 + 1) + "행 " + (cell % 9 + 1) + "열";
        private void SelectFlowCells(params int[] cells)
        { CancelFlowTool(); brush = null; Session.SelectCells(cells); Refresh(); }

        private void DrawFlow(JObject level)
        {
            inspector.Add(new Label("선택 영역의 중력 또는 보드를 찍는 도구를 고르세요."));
            foreach (var option in new[] { ("Down", "아래", "down"), ("Up", "위", "up"), ("Left", "왼쪽", "left"), ("Right", "오른쪽", "right") })
                Button(inspector, "gravity-" + option.Item3, "선택 칸 중력: " + option.Item2,
                    () => Change("영역 중력", data => FlowDocumentEditing.SetGravity(data, Session.SelectedCells, option.Item1)));
            Button(inspector, "gravity-reset", "선택 칸 기본 중력 복원", () => Change("중력 복원", data => FlowDocumentEditing.SetGravity(data, Session.SelectedCells, null)));
            Button(inspector, "path-start", "직접 경로 찍기", () => StartFlowTool("path"));
            Button(inspector, "path-finish", "마지막 칸을 끝으로 경로 확정", () => Change("직접 경로", data =>
            { FlowDocumentEditing.SetPath(data, flowRoute); CancelFlowTool(); })).SetEnabled(flowTool == "path" && flowRoute.Count > 0);
            Button(inspector, "path-remove", "선택 칸 직접 경로 지우기", () => Change("경로 삭제", data =>
            { foreach (int cell in Session.SelectedCells) FlowDocumentEditing.RemovePath(data, cell); }));
            Button(inspector, "portal-start", "포털 입구 → 출구 찍기", () => StartFlowTool("portal"));
            Button(inspector, "portal-remove", "선택 칸 포털 입구 지우기", () => Change("포털 삭제", data =>
            { foreach (int cell in Session.SelectedCells) FlowDocumentEditing.RemovePortal(data, cell); }));
            Button(inspector, "wall-start", "벽 추가: 인접한 두 칸 찍기", () => StartFlowTool("wall"));
            Button(inspector, "wall-erase", "벽 삭제: 인접한 두 칸 찍기", () => StartFlowTool("wall-erase"));
            Button(inspector, "arrival-add", "선택 칸을 도착점으로", () => Change("도착점 추가", data =>
            { foreach (int cell in Session.SelectedCells) FlowDocumentEditing.SetArrival(data, cell, false); }));
            Button(inspector, "arrival-remove", "선택 칸 도착점 지우기", () => Change("도착점 삭제", data =>
            { foreach (int cell in Session.SelectedCells) FlowDocumentEditing.SetArrival(data, cell, true); }));
            Button(inspector, "tool-cancel", "찍기 종료 / 미확정 경로 취소", () => { CancelFlowTool(); Refresh(); });
            if (flowTool != null)
                inspector.Add(new Label(flowTool == "path" ? "순서: " + string.Join(" → ", flowRoute.Select(CellName)) : flowFirst < 0 ? "첫 칸을 선택하세요." : CellName(flowFirst) + "에서 연결할 다음 칸을 선택하세요."));
            inspector.Add(new Label("포털 입구는 즉시 초안에 남습니다. 출구를 지정하지 않았다면 목록에서 이어서 지정하세요."));
            foreach (JObject portal in level["flow"]["portals"])
            {
                int entrance = Cell(portal["entrance"]);
                string label = CellName(entrance) + " → " + ((bool)portal["hasExit"] ? CellName(Cell(portal["exit"])) : "출구 미지정");
                Button(inspector, "portal-select-" + entrance, label, () => SelectFlowCells(entrance));
                Button(inspector, "portal-exit-" + entrance, "이 포털의 출구 지정", () => { StartFlowTool("portal"); flowFirst = entrance; Refresh(); });
            }
            foreach (JObject path in level["flow"]["paths"])
            {
                int cell = Cell(path["coordinate"]);
                Button(inspector, "path-select-" + cell, "경로 " + CellName(cell) + ((bool)path["isEnd"] ? " (끝)" : " → " + CellName(Cell(path["next"]))), () => SelectFlowCells(cell));
            }
            if (Session.SelectedCells.Length == 1)
            {
                int target = Session.SelectedCells[0];
                int[] candidates = FlowDocumentEditing.Sources(level, target);
                if (candidates.Length > 1)
                {
                    JObject merge = level["flow"]["merges"].OfType<JObject>().FirstOrDefault(value => Cell(value["coordinate"]) == target);
                    int[] ordered = merge == null ? candidates : merge["sources"].Select(Cell).ToArray();
                    inspector.Add(new Label("합류 순서 · 먼저 들어올 칸을 위로 옮기세요."));
                    for (int i = 0; i < ordered.Length; i++)
                    {
                        int index = i;
                        Button(inspector, "merge-up-" + i, (i + 1) + ". " + CellName(ordered[i]) + " ↑", () => Change("합류 순서", data =>
                        {
                            int[] next = (int[])ordered.Clone(); (next[index - 1], next[index]) = (next[index], next[index - 1]);
                            FlowDocumentEditing.SetMerge(data, target, next);
                        })).SetEnabled(i > 0);
                    }
                }
                Button(inspector, "merge-reset", "선택 칸 합류 순서 초기화", () => Change("합류 초기화", data => FlowDocumentEditing.RemoveMerge(data, target)));
            }
        }
    }
}
#endif
