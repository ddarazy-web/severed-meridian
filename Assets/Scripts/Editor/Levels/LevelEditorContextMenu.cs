using System;
using System.Linq;
using Board;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        private void BuildBoardContextMenu(ContextualMenuPopulateEvent evt)
        {
            if (!LevelBoardEditing.CanEdit(level) || board.IsDragging) return;
            Vector2 point = board.WorldToLocal(evt.mousePosition);
            if (point.x < 0 || point.y < 0 || point.x >= 400 || point.y >= 400) return;
            BoardCoordinate coordinate = new BoardCoordinate((int)(point.y / LevelBoardView.CellSize), (int)(point.x / LevelBoardView.CellSize));
            if (board.Brush == LevelBrush.SourceSelect || board.Brush == LevelBrush.Source || board.Brush == LevelBrush.SourceErase)
            {
                board.Brush = LevelBrush.SourceSelect;
                if (!selectedSources.Contains(coordinate)) SelectSource(coordinate, false);
            }
            else if (board.Brush == LevelBrush.Select)
            {
                if (!LevelCommonEditing.TrySelect(level, board.Layer, coordinate, out PlacementSelection target) || !selectedPlacements.Contains(target))
                    SelectPlacements(coordinate, false);
            }
            else if (board.Brush == LevelBrush.Flow && flowTool == FlowTool.Select) SelectCell(coordinate);
            else return;
            BuildSelectionContextMenu(evt);
        }

        private void BuildSelectionContextMenu(ContextualMenuPopulateEvent evt)
        {
            if (!LevelBoardEditing.CanEdit(level)) return;
            LevelDefinition owner = level;
            string snapshot = JsonUtility.ToJson(level);
            LevelBrush brush = board.Brush;
            PlacementLayer layer = board.Layer;
            BoardCoordinate? cell = selected;
            int supplyItem = selectedSupplyItem;
            PlacementSelection[] placements = selectedPlacements.ToArray();
            BoardCoordinate[] sources = selectedSources.ToArray();
            bool Current()
            {
                if (!SupplyViewCurrent(owner, snapshot)) return false;
                if (board.Brush == brush && board.Layer == layer && Nullable.Equals(selected, cell) && selectedSupplyItem == supplyItem && selectedPlacements.SetEquals(placements) && selectedSources.SetEquals(sources)) return true;
                operation.text = "선택이 변경되었습니다. 다시 선택하세요.";
                return false;
            }
            void Add(string title, bool enabled, Action action)
            {
                evt.menu.AppendAction(title, _ => { if (Current()) action(); },
                    enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }
            // 기존 버튼의 콜백을 그대로 사용해 메뉴와 버튼의 검증·Undo 동작을 일치시킨다.
            void Existing(string name, string title, bool enabled = true)
            {
                Button button = rootVisualElement.Q<Button>(name);
                if (button == null) return;
                Add(title, enabled && button.enabledInHierarchy, () =>
                {
                    if (button.panel == null) { operation.text = "화면이 갱신되었습니다. 메뉴를 다시 여세요."; return; }
                    using NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled();
                    submit.target = button;
                    button.SendEvent(submit);
                });
            }
            if (brush == LevelBrush.SourceSelect || brush == LevelBrush.Source || brush == LevelBrush.SourceErase)
            {
                int[] indices = sources.Select(cell => LevelSupplyRules.FindSource(level, cell)).ToArray();
                if (indices.Length == 0) return;
                bool fixedSources = indices.All(index => index >= 0 && level.Supply.Sources[index].Mode == SupplyMode.Fixed);
                Add("공급 목록 복사", fixedSources && indices.Length == 1, () =>
                {
                    EditorGUIUtility.systemCopyBuffer = LevelSupplyEditing.Copy(level, indices[0]);
                    operation.text = "공급 목록을 복사했습니다.";
                });
                Add("공급 목록 교체 붙여넣기", fixedSources, () => SupplyAction(() => LevelSupplyEditing.Paste(level, indices, EditorGUIUtility.systemCopyBuffer, false)));
                Add("공급 목록 뒤에 추가", fixedSources, () => SupplyAction(() => LevelSupplyEditing.Paste(level, indices, EditorGUIUtility.systemCopyBuffer, true)));
                if (fixedSources && indices.Length == 1)
                {
                    int count = level.Supply.Sources[indices[0]].Items?.Count ?? 0;
                    bool item = supplyItem >= 0 && supplyItem < count;
                    evt.menu.AppendSeparator();
                    Existing("add-supply-item", "공급 항목/추가");
                    Existing("duplicate-supply-item", "공급 항목/선택 항목 복제", item);
                    Existing("supply-item-later", "공급 항목/나중에 공급 ↑", item && supplyItem < count - 1);
                    Existing("supply-item-earlier", "공급 항목/먼저 공급 ↓", item && supplyItem > 0);
                    Existing("delete-supply-item", "공급 항목/선택 항목 삭제", item);
                    Existing("clear-supply", "공급 목록 비우기", count > 0);
                }
                evt.menu.AppendSeparator();
                Existing("delete-sources", "선택 생성구 삭제");
            }
            else if (brush == LevelBrush.Select && placements.Length > 0)
            {
                Add("설정 복사", placements.Length == 1 && LevelCommonEditing.Fields(placements[0]).Length > 0, () =>
                {
                    string text = LevelCommonEditing.Copy(level, placements[0]);
                    if (text != null) { EditorGUIUtility.systemCopyBuffer = text; operation.text = "선택 요소의 설정을 복사했습니다."; }
                });
                Add("설정 붙여넣기", placements.Any(item => LevelCommonEditing.Fields(item).Length > 0), () =>
                {
                    board.CancelStroke();
                    string error = LevelCommonEditing.Paste(level, placements, EditorGUIUtility.systemCopyBuffer, out int changed, out int excluded);
                    operation.text = error ?? $"설정 적용: 변경 {changed}개 · 변경 없음 {placements.Length - changed - excluded}개 · 제외 {excluded}개";
                    Refresh();
                });
            }
            if (brush == LevelBrush.Select)
            {
                evt.menu.AppendSeparator();
                Existing("move-obstacle", "2×2 본체 이동");
                Existing("delete-placement", "선택한 층의 요소 삭제");
                Existing("delete-recovery", "회수 부품 삭제");
            }
            if ((brush == LevelBrush.Select && placements.Length <= 1) || (brush == LevelBrush.Flow && flowTool == FlowTool.Select))
            {
                // 여러 대상의 의미가 섞이지 않도록 바닥 작업은 단일 선택에서만 제공한다.
                foreach (Button button in flowProperties.Query<Button>().ToList())
                    if (!button.name.StartsWith("merge-") && button.name != "confirm-merge")
                        Existing(button.name, "바닥·연결/" + button.text);
            }
        }
    }
}


