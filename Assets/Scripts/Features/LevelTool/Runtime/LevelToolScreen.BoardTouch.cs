#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Editing;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private LevelToolBoardInput boardInput;
        private bool boardTouchAdd, boardTouchReplace;
        private string boardTouchTool = "tap";
        private Label boardTouchHint;
        private AuthoringEditSession clipboardSession;
        private ElementSelectionEditing elementClipboard;

        private ElementSelectionEditing ElementClipboard
        {
            get
            {
                if (!ReferenceEquals(clipboardSession, Session))
                {
                    clipboardSession = Session;
                    AuthoringEditSession activeSession = Session;
                    elementClipboard = activeSession == null ? null : new ElementSelectionEditing(activeSession, () => LevelToolDocuments.Definitions(activeSession));
                }
                return elementClipboard;
            }
        }

        private string[] SelectedBoardInstanceIds(out string reason)
        {
            reason = null;
            try { return ElementClipboard.SelectedInstanceIds(layer); }
            catch (LevelAuthoring.Documents.ContentFormatException error)
            {
                reason = error.Message;
                return System.Array.Empty<string>();
            }
        }

        private void InitializeBoardTouch()
        {
            boardInput?.Dispose();
            boardInput = new LevelToolBoardInput(root, boardScroll, board,
                () => new LevelToolBoardInput.Context
                {
                    HasSession = Session != null, Blocked = modal != null, Busy = busy,
                    Picking = tutorialPick != null || flowTool != null, WireDrawing = wireDrawing,
                    Mobile = mobileViewportActive, Replace = boardTouchReplace, Tool = boardTouchTool, CellSize = cellSize
                },
                ExecuteBoardCell, SelectConnectionVertex,
                cells => { SelectBoardCells(cells); Refresh(); },
                OpenBoardActions, UpdateBoardTouchHint,
                size =>
                {
                    autoFitBoard = false; cellSize = size;
                    DrawBoard(); DrawTutorialPreview(); DrawTutorialPicking();
                });
            boardInput.Attach();
            boardScroll.tooltip = "Ctrl + 마우스 휠: 커서 위치를 기준으로 확대·축소 / 마우스 휠: 스크롤";
            ScrollView tools = new ScrollView(ScrollViewMode.Horizontal) { name = "board-touch-tools" };
            tools.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            tools.tooltip = "좌우로 밀어 나머지 보드 도구를 확인하세요.";
            tools.style.flexShrink = 0;
            tools.contentContainer.style.flexDirection = FlexDirection.Row;
            editor.Insert(editor.IndexOf(body), tools);
            Button(tools, "board-touch-select", "선택", () => SetBoardTouchTool("tap"));
            Button(tools, "board-touch-area", "영역", () => SetBoardTouchTool("area"));
            Button(tools, "board-touch-pan", "이동", () => SetBoardTouchTool("pan"));
            Toggle add = new Toggle("추가 선택") { name = "board-touch-add", value = boardTouchAdd };
            add.RegisterValueChangedCallback(e => { CancelBoardTouch(); boardTouchAdd = e.newValue; }); tools.Add(add);
            Toggle replace = new Toggle("교체") { name = "board-touch-replace", value = boardTouchReplace };
            replace.tooltip = "현재 도구로 점유된 칸을 교체합니다. 다른 층의 요소는 보존하며 충돌하면 거절합니다.";
            replace.RegisterValueChangedCallback(e => { CancelBoardTouch(); boardTouchReplace = e.newValue; }); tools.Add(replace);
            Button(tools, "board-zoom-out", "−", () => ZoomBoard(cellSize / 1.2f, boardScroll.contentViewport.worldBound.center));
            Button(tools, "board-zoom-in", "+", () => ZoomBoard(cellSize * 1.2f, boardScroll.contentViewport.worldBound.center));
            Button(tools, "board-fit", "화면 맞춤", () => { autoFitBoard = true; FitBoardToViewport(); });
            Button(tools, "board-focus-toggle", "보드 넓게", ToggleBoardFocus).tooltip = "상단과 좌우 편집 패널을 접어 보드 공간을 넓힙니다. Shift+Space로도 전환할 수 있습니다.";
            Button(tools, "board-actions", "작업", OpenBoardActions);
            Button(tools, "board-touch-cancel", "취소", () => { CancelBoardTouch(); Show("진행 중 터치와 영역 지정을 취소했습니다."); });
            foreach (VisualElement control in tools.contentContainer.Children())
            { control.style.minWidth = 48; control.style.flexShrink = 0; }
            boardTouchHint = new Label { name = "board-touch-hint" }; tools.Add(boardTouchHint);
            UpdateBoardTouchHint();
        }

        private void DisposeBoardTouch() { boardInput?.Dispose(); boardInput = null; }

        private void SetBoardTouchTool(string tool)
        {
            CancelBoardTouch(); boardTouchTool = tool; brush = null; moveBody = null;
            UpdateBoardTouchHint(); Refresh();
        }

        private void UpdateBoardTouchHint()
        {
            if (boardTouchHint == null) return;
            boardTouchHint.text = boardTouchTool == "pan" ? "끌어서 보드 이동" : boardTouchTool == "area" ?
                (boardInput == null || boardInput.AreaStart < 0) ? "영역 시작 칸을 선택" : "끝 칸을 선택 · 취소 가능" : "탭: 손을 떼면 확정 · 길게: 작업";
        }

        private void CancelBoardTouch() => boardInput?.Cancel();
        private void ResetBoardTouch() => boardInput?.Reset();
        private void ZoomBoard(float size, Vector2 pivot, Vector2? sourceCoordinate = null) => boardInput?.Zoom(size, pivot, sourceCoordinate);

        private void UpdateBoardTouch(float time)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true && !heldShortcutKeys.Contains(KeyCode.Escape))
            {
                heldShortcutKeys.Add(KeyCode.Escape);
                HandleAndroidBack();
            }
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.isPressed != true) heldShortcutKeys.Remove(KeyCode.Escape);
#endif
            boardInput?.Tick(time);
        }

        private void HandleAndroidBack()
        {
            if (fileTransferCancellation != null) { fileTransferCancellation.Cancel(); return; }
            if (modal != null) { CloseModal(); return; }
            if (playRoot != null) { HandleTrialBack(); return; }
            if (busy) return;
            if (CanExecuteCommand(ToolCommandId.Cancel, out _)) { TryExecuteCommand(ToolCommandId.Cancel); return; }
            if (mobilePanel != null)
            {
                // 패널을 닫아도 아직 확정하지 않은 입력은 기존 보류 계약으로 유지한다.
                SelectMobilePanel(mobilePanel); return;
            }
            ExitTool();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            ResetBoardTouch();
            CheckpointOnInterruption();
        }

        private void SelectBoardCells(IEnumerable<int> cells)
        {
            int[] previous = boardTouchAdd ? Session.SelectedCells : Array.Empty<int>();
            Session.SelectCells(previous.Concat(cells));
            // 본체가 포함된 선택은 점유 칸 전체로 확장한다. 빈 칸도 영역 선택에서 유지한다.
            Session.SelectCells(Session.SelectedCells.Concat(ElementClipboard.SelectedCells(layer)));
        }

        private void OpenBoardActions()
        {
            if (busy || Session == null || modal != null || tutorialPick != null || sharedTutorialDraft != null) return;
            if (!CommitCommandInput()) return;
            CancelBoardTouch();
            VisualElement panel = OpenModal("선택 작업 · " + layer); panel.name = "board-actions-menu"; commandMenuOpen = true;
            panel.Add(new Label("현재 층의 선택만 편집합니다. 붙여넣기는 첫 선택 칸을 기준으로 배치합니다."));
            Button(panel, "board-place", "현재 도구 배치", () => { CloseModal(); PlaceBoardSelection(false); });
            Button(panel, "board-replace", "현재 도구로 교체", () => { CloseModal(); PlaceBoardSelection(true); });
            foreach (ToolCommandId id in new[] { ToolCommandId.Copy, ToolCommandId.Cut, ToolCommandId.Paste, ToolCommandId.Delete })
                CommandButton(panel, id, "board-command-" + id);
            Button(panel, "board-properties", "선택 속성 수정", () =>
            { CloseModal(); levelSettingsVisible = false; if (mobileNavigation != null && mobilePanel != "properties") SelectMobilePanel("properties"); else UpdatePanelVisibility(); });
            Button(panel, "board-close-actions", "닫기", CloseModal);
            RefreshCommandStates();
        }

        private void PlaceBoardSelection(bool replace)
        {
            Change(replace ? "선택 교체" : "선택 배치", data =>
            {
                if (brush == null) throw new InvalidOperationException("먼저 소재에서 배치할 요소를 선택하세요.");
                LevelDocumentEditing.PlaceSelection(data, definitions, brush, Session.SelectedCells, replace, color, direction, layer);
            });
        }

    }
}
#endif
