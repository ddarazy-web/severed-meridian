using System;
using System.Linq;
using Board;
using Simulation;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelInitialStateWindow
    {
        private Toggle[] boosterChoices;
        private Button[] itemButtons;
        private Button itemCancel;
        private Label boosterStatus;
        private VisualElement shuffleConfirmation;
        private BoardItem? selectedItem;
        private BoardCoordinate? itemFirst;
        private static string ItemName(BoardItem item) => item switch { BoardItem.Hammer => "고물 망치", BoardItem.Swap => "자리 바꾸기", _ => "섞기" };
        private static string BoosterName(StartBooster booster) => booster switch { StartBooster.Rocket => "청소로켓", StartBooster.Bomb => "달 폭탄", _ => "무지개 자석" };
        private StartBooster[] SelectedBoosters() => Enum.GetValues(typeof(StartBooster)).Cast<StartBooster>().Where(b => boosterChoices[(int)b].value).ToArray();

        private void CreateItemsUI(VisualElement root)
        {
            VisualElement boosters = new VisualElement { name = "booster-toolbar" }; boosters.AddToClassList("initial-toolbar"); root.Add(boosters);
            boosters.Add(new Label("시작 부스터")); boosterChoices = new Toggle[3];
            foreach (StartBooster booster in Enum.GetValues(typeof(StartBooster)))
            {
                Toggle toggle = new Toggle(BoosterName(booster)) { name = "booster-" + booster };
                boosterChoices[(int)booster] = toggle; boosters.Add(toggle);
            }
            VisualElement items = new VisualElement { name = "item-toolbar" }; items.AddToClassList("initial-toolbar"); root.Add(items);
            items.Add(new Label("시험용 · 수량 무제한")); itemButtons = new Button[3];
            foreach (BoardItem item in Enum.GetValues(typeof(BoardItem)))
            {
                Button button = new Button(() => SelectItem(item)) { name = "item-" + item, text = ItemName(item) };
                itemButtons[(int)item] = button; items.Add(button);
            }
            itemCancel = new Button(CancelItemSelection) { name = "item-cancel", text = "취소" }; items.Add(itemCancel);
            shuffleConfirmation = new VisualElement { name = "item-shuffle-confirmation" }; shuffleConfirmation.AddToClassList("initial-toolbar"); root.Add(shuffleConfirmation);
            shuffleConfirmation.Add(new Label("섞기를 사용할까요?"));
            shuffleConfirmation.Add(new Button(() => ExecuteItem(BoardItem.Shuffle)) { name = "item-shuffle-confirm", text = "사용" });
            shuffleConfirmation.Add(new Button(CancelItemSelection) { name = "item-shuffle-cancel", text = "취소" });
            boosterStatus = new Label { name = "booster-status" }; root.Add(boosterStatus);
        }

        private void SelectItem(BoardItem item)
        {
            CheckInput(); if (execution?.CanUseItems != true || cascadeRunning) return;
            if (selectedItem == item) { CancelItemSelection(); return; }
            if (selectedItem.HasValue) return;
            selectedItem = item; itemFirst = null; firstInput = secondInput = null;
            ClearItemHighlights();
            executionInfo.text = item == BoardItem.Hammer ? "망치로 칠 칸을 선택하세요" : item == BoardItem.Swap ? "바꿀 블록 두 개를 선택하세요" : "섞기 사용을 확인하세요";
            UpdateExecutionControls();
        }

        private void CancelItemSelection()
        {
            selectedItem = null; itemFirst = null; ClearItemHighlights(); UpdateExecutionControls();
        }
        private void ClearItemHighlights()
        {
            if (grid == null) return;
            foreach (Button cell in grid.Query<Button>().ToList()) { cell.RemoveFromClassList("execution-input"); cell.RemoveFromClassList("query-highlight"); }
        }
        private void SelectItemCell(BoardCoordinate coordinate)
        {
            CheckInput(); if (!selectedItem.HasValue || execution?.CanUseItems != true) return;
            if (selectedItem == BoardItem.Shuffle || !execution.CanSelectItemTarget(selectedItem.Value, coordinate)) return;
            if (selectedItem == BoardItem.Hammer) { ExecuteItem(BoardItem.Hammer, coordinate); return; }
            if (itemFirst.HasValue && itemFirst.Value.Equals(coordinate)) itemFirst = null;
            else if (itemFirst.HasValue && execution.CanSwapItemTargets(itemFirst.Value, coordinate)) { ExecuteItem(BoardItem.Swap, itemFirst, coordinate); return; }
            else if (itemFirst.HasValue && new BoardEdge(itemFirst.Value, coordinate).IsAdjacent) return;
            else itemFirst = coordinate;
            ClearItemHighlights();
            if (itemFirst.HasValue)
            {
                grid.Q<Button>($"initial-cell-{itemFirst.Value.Row}-{itemFirst.Value.Column}")?.AddToClassList("execution-input");
                foreach (RuntimeCell cell in execution.State.Cells.Where(c => execution.CanSwapItemTargets(itemFirst.Value, c.Coordinate)))
                    grid.Q<Button>($"initial-cell-{cell.Coordinate.Row}-{cell.Coordinate.Column}")?.AddToClassList("query-highlight");
            }
            executionInfo.text = itemFirst.HasValue ? "강조된 블록을 선택하면 교환해요" : "바꿀 블록 두 개를 선택하세요";
        }

        private void ExecuteItem(BoardItem item, BoardCoordinate? first = null, BoardCoordinate? second = null)
        {
            CheckInput(); if (execution == null || selectedItem != item) return;
            ItemUseResult result = execution.UseItem(item, first, second);
            if (result.IsApplied)
            {
                CancelItemSelection(); DisplayState(execution.State);
                details.text = ItemName(item) + " · 시험용 사용\n턴 " + result.Turn + " · 남은 이동 " + result.MovesRemaining +
                    "\n난수 " + result.RandomBefore + " → " + result.RandomAfter + "\n" + string.Join("\n", result.Effects.Select(e => e.Target + " " + e.Message));
            }
            else if (item == BoardItem.Shuffle) CancelItemSelection();
            executionInfo.text = result.Message; rootVisualElement.Q<Label>("initial-boundary").text = result.Message;
            UpdateExecutionControls();
        }

        private void UpdateItemControls()
        {
            if (itemButtons == null) return;
            bool ready = execution?.CanUseItems == true && !cascadeRunning;
            foreach (BoardItem item in Enum.GetValues(typeof(BoardItem))) itemButtons[(int)item].SetEnabled(ready && (!selectedItem.HasValue || selectedItem == item));
            itemCancel.style.display = selectedItem.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            shuffleConfirmation.style.display = selectedItem == BoardItem.Shuffle ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (Toggle toggle in boosterChoices) toggle.SetEnabled(execution == null);
            boosterStatus.text = execution == null ? "부스터를 선택한 뒤 실행 시험을 켜세요." :
                "부스터 배치 " + string.Join(", ", execution.BoosterPlacements.Select(p => BoosterName(p.Booster) + " " + p.Coordinate)) +
                " · 대기 " + (execution.PendingBoosters.Count == 0 ? "없음" : string.Join(", ", execution.PendingBoosters.Select(BoosterName)));
        }
        private void OnLostFocus() { if (selectedItem.HasValue) CancelItemSelection(); }
    }
}
