#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using LevelAuthoring.Editing;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private JObject Placement(JObject level, int cell)
        {
            return ((JArray)level["elements"]).OfType<JObject>().FirstOrDefault(item =>
            {
                if ((string)item["layer"] != layer) return false;
                int row = (int)item["coordinate"]["row"], column = (int)item["coordinate"]["column"];
                int size = definitions.TryGetValue((string)item["definitionId"], out JObject definition) ? LevelToolDocuments.Size(definition) : 1;
                return cell / 9 >= row && cell / 9 < row + size && cell % 9 >= column && cell % 9 < column + size;
            });
        }

        private void DrawBoard()
        {
            // 소수점 반올림으로 아홉 번째 셀이 다음 줄에 넘어가지 않도록 격자 크기를 맞춘다.
            cellSize = Mathf.Clamp(Mathf.Round(cellSize), 32, 144);
            RefreshBoardToolStatus();
            float viewportWidth = boardScroll.contentViewport.resolvedStyle.width;
            boardScroll.contentContainer.style.minWidth = Mathf.Max(float.IsNaN(viewportWidth) ? 0 : viewportWidth, cellSize * 9 + 32);
            boardView.Refresh(Session, Workspace.Folder, definitions, layer, cellSize, new Presentation.LevelToolBoardOverlayState
            {
                Page = inspectorPage, FlowFirst = flowFirst, FlowRoute = flowRoute.ToArray(),
                WireDrawing = wireDrawing, WireRoute = wireRoute.ToArray(), SelectedConnection = selectedConnection,
                Mobile = mobileNavigation != null
            });
        }

        private void SelectConnectionVertex(int vertex)
        {
            if (wireRoute.Contains(vertex)) { Show("이미 찍은 점입니다. ‘찍은 점 비우기’로 다시 그릴 수 있습니다."); return; }
            wireRoute.Add(vertex); Refresh();
        }
        private void ExecuteBoardCell(int cell, bool shift, bool replace, bool touch)
        {
            if (boardTouchTool == "erase" && sharedTutorialDraft != null) return;
            board.Focus();
            if (UseTutorialPicker(cell) || UseFlowTool(cell)) return;
            Edit(() =>
            {
                if (shift && anchor >= 0)
                    Session.SelectCells(Enumerable.Range(0, 81).Where(i => i / 9 >= Math.Min(cell / 9, anchor / 9) && i / 9 <= Math.Max(cell / 9, anchor / 9) &&
                        i % 9 >= Math.Min(cell % 9, anchor % 9) && i % 9 <= Math.Max(cell % 9, anchor % 9)));
                else { anchor = cell; if (touch) SelectBoardCells(new[] { cell }); else Session.SelectCells(new[] { cell }); }
                if (boardTouchTool == "erase" && !shift) { ElementClipboard.Delete(layer); return; }
                if (moveBody != null)
                {
                    Session.EditLevel("장애물 이동", data => LevelDocumentEditing.Move(data, definitions, moveBody, cell / 9, cell % 9));
                    moveBody = null; return;
                }
                if (brush != null && !shift)
                    Session.EditLevel("요소 배치", data =>
                    {
                        if (touch) LevelDocumentEditing.PlaceOnLayer(data, definitions, brush, cell, replace, layer, color, direction);
                        else LevelDocumentEditing.Place(data, definitions, brush, cell / 9, cell % 9, replace, color: color, direction: direction);
                    });
            });
        }

        private void DrawProperties()
        {
            JObject level = Session.Get(Session.SelectedLevelId).Data;
            var pages = new System.Collections.Generic.List<string> { "기본", "공급", "흐름", "연결", "모양", "튜토리얼", "튜토리얼 공급", "자동 시험", "시험 기록" };
            DropdownField page = new DropdownField("편집 항목", pages, Math.Max(0, pages.IndexOf(inspectorPage))) { name = "inspector-page" };
            page.RegisterValueChangedCallback(e => { inspectorPage = e.newValue; CancelFlowTool(); wireDrawing = false; brush = null; moveBody = null; Refresh(); }); inspector.Add(page);
            page.SetEnabled(sharedTutorialDraft == null);
            page.style.display = DisplayStyle.None;
            if ((int)level["schemaVersion"] != 5)
            {
                inspector.Add(new Label("이전 형식의 레벨입니다. 보드·게임 시험은 미리 볼 수 있으며 편집에는 형식 전환이 필요합니다."));
                Button(inspector, "upgrade-level", "편집 형식으로 전환 (Undo 가능)", () => Edit(() => LevelToolDocuments.UpgradeSelectedLevel(Session)));
                return;
            }
            if (inspectorPage == "튜토리얼 공급") { DrawSupply(level, true); return; }
            if (inspectorPage == "자동 시험") { DrawToolBot(); return; }
            if (inspectorPage == "시험 기록") { DrawToolHistory(); DrawToolRecords(); return; }
            if (inspectorPage == "공급") { DrawSupply(level); return; }
            if (inspectorPage == "흐름") { DrawFlow(level); return; }
            if (inspectorPage == "연결") { DrawConnections(level); return; }
            if (inspectorPage == "모양") { DrawShapes(level); return; }
            if (inspectorPage == "튜토리얼") { DrawTutorial(); return; }
            string[] selectedIds = SelectedBoardInstanceIds(out string selectionReason);
            propertiesPanel.Refresh(level, definitions, selectedIds, selectionReason);
            inspector.Add(propertiesPanel.Root);
        }
    }
}
#endif
