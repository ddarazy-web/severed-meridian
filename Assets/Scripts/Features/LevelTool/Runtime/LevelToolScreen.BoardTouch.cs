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
        private readonly Dictionary<int, Vector2> boardTouchPoints = new Dictionary<int, Vector2>();
        private int boardTouchPrimary = -1, boardTouchCell = -1, boardTouchVertex = -1, boardAreaStart = -1;
        private float boardTouchStarted;
        private float boardTouchZoomSize;
        private Vector2 boardTouchOrigin, boardTouchScroll;
        private bool boardTouchCancelled, boardTouchAdd, boardTouchReplace;
        private bool boardViewGestureActive;
        private bool boardZoomPending;
        private Vector2 boardZoomCoordinate, boardZoomPivot;
        private IVisualElementScheduledItem boardZoomAdjustment;
        private string boardTouchTool = "tap";
        private VisualElement boardTouchPreview;
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
            boardScroll.RegisterCallback<WheelEvent>(OnBoardZoomWheel, TrickleDown.TrickleDown);
            boardScroll.tooltip = "Ctrl + 마우스 휠: 커서 위치를 기준으로 확대·축소 / 마우스 휠: 스크롤";
            root.RegisterCallback<PointerDownEvent>(OnBoardTouchDown, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerMoveEvent>(OnBoardTouchMove, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(OnBoardTouchUp, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCancelEvent>(OnBoardTouchCancel, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCaptureOutEvent>(OnBoardTouchCaptureOut);
            boardScroll.contentViewport.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                boardScroll.contentContainer.style.minWidth = Mathf.Max(evt.newRect.width, cellSize * 9 + 32);
                if (!boardZoomPending) CancelBoardTouch();
            });

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

        private void OnBoardTouchCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.target != root || !boardTouchPoints.Remove(evt.pointerId)) return;
            CancelBoardTouch();
            if (boardTouchPoints.Count == 0) boardTouchPrimary = -1;
        }

        private void DisposeBoardTouch()
        {
            boardScroll?.UnregisterCallback<WheelEvent>(OnBoardZoomWheel, TrickleDown.TrickleDown);
            root?.UnregisterCallback<PointerDownEvent>(OnBoardTouchDown, TrickleDown.TrickleDown);
            root?.UnregisterCallback<PointerMoveEvent>(OnBoardTouchMove, TrickleDown.TrickleDown);
            root?.UnregisterCallback<PointerUpEvent>(OnBoardTouchUp, TrickleDown.TrickleDown);
            root?.UnregisterCallback<PointerCancelEvent>(OnBoardTouchCancel, TrickleDown.TrickleDown);
            root?.UnregisterCallback<PointerCaptureOutEvent>(OnBoardTouchCaptureOut);
        }

        private void SetBoardTouchTool(string tool)
        {
            CancelBoardTouch(); boardTouchTool = tool; brush = null; moveBody = null;
            UpdateBoardTouchHint(); Refresh();
        }

        private void UpdateBoardTouchHint()
        {
            if (boardTouchHint == null) return;
            boardTouchHint.text = boardTouchTool == "pan" ? "끌어서 보드 이동" : boardTouchTool == "area" ?
                boardAreaStart < 0 ? "영역 시작 칸을 선택" : "끝 칸을 선택 · 취소 가능" : "탭: 손을 떼면 확정 · 길게: 작업";
        }

        private void OnBoardTouchDown(PointerDownEvent evt)
        {
            VisualElement target = evt.target as VisualElement;
            bool onBoard = target != null && boardScroll.Contains(target);
            if (evt.pointerType != UnityEngine.UIElements.PointerType.touch)
            {
                if (onBoard && evt.button == 1 && modal == null && !busy && Session != null)
                {
                    int cell = BoardCellAt(evt.position);
                    if (cell >= 0) { SelectBoardCells(new[] { cell }); Refresh(); OpenBoardActions(); }
                    evt.StopImmediatePropagation(); evt.PreventDefault();
                }
                return;
            }
            if (boardTouchPoints.Count == 0 && (!onBoard || modal != null || busy || Session == null || !board.visible)) return;
            boardTouchPoints[evt.pointerId] = evt.position;
            if (boardTouchPoints.Count > 1)
            {
                root.CapturePointer(evt.pointerId);
                CancelBoardTouch();
                boardViewGestureActive = boardTouchPoints.Count == 2 && onBoard && modal == null;
                if (boardViewGestureActive) boardTouchZoomSize = cellSize;
                evt.StopImmediatePropagation(); evt.PreventDefault(); return;
            }
            if (boardTouchCancelled) return;
            boardTouchPrimary = evt.pointerId;
            boardTouchCell = BoardCellAt(evt.position); boardTouchVertex = -1;
            for (VisualElement current = target; current != null && current != board; current = current.parent)
                if (current.name?.StartsWith("wire-vertex-", StringComparison.Ordinal) == true)
                    int.TryParse(current.name.Substring(12), out boardTouchVertex);
            if (wireDrawing && mobileViewportActive)
            {
                // 겹친 터치 영역에서는 이벤트 대상보다 실제 위치에 가까운 꼭짓점을 고른다.
                Vector2 local = board.WorldToLocal(evt.position);
                int row = Mathf.RoundToInt(local.y / cellSize), column = Mathf.RoundToInt(local.x / cellSize);
                boardTouchVertex = row >= 0 && row <= 9 && column >= 0 && column <= 9 &&
                    boardScroll.contentViewport.worldBound.Contains(evt.position) &&
                    Vector2.Distance(local, new Vector2(column, row) * cellSize) <= 24 ? row * 10 + column : -1;
            }
            boardTouchOrigin = evt.position; boardTouchScroll = boardScroll.scrollOffset;
            boardTouchStarted = Time.unscaledTime;
            boardViewGestureActive = boardTouchTool == "pan";
            boardTouchPreview = board.Q("cell-" + boardTouchCell);
            boardTouchPreview?.AddToClassList("touch-preview");
            root.CapturePointer(evt.pointerId);
            evt.StopImmediatePropagation(); evt.PreventDefault();
        }

        private void OnBoardTouchMove(PointerMoveEvent evt)
        {
            if (evt.pointerType != UnityEngine.UIElements.PointerType.touch || !boardTouchPoints.TryGetValue(evt.pointerId, out Vector2 previous)) return;
            if (boardViewGestureActive && boardTouchPoints.Count == 2 && modal == null)
            {
                Vector2 other = boardTouchPoints.First(pair => pair.Key != evt.pointerId).Value;
                float oldDistance = Vector2.Distance(previous, other), distance = Vector2.Distance(evt.position, other);
                Vector2 before = (previous + other) * .5f, after = ((Vector2)evt.position + other) * .5f;
                Vector2 coordinate = board.WorldToLocal(before) / (board.resolvedStyle.width / 9);
                if (oldDistance > 10)
                {
                    boardTouchZoomSize = Mathf.Clamp(boardTouchZoomSize * distance / oldDistance, 32, 144);
                    ZoomBoard(boardTouchZoomSize, after, coordinate);
                }
                else boardScroll.scrollOffset -= after - before;
                boardViewGestureActive = true;
            }
            else if (boardViewGestureActive && !boardTouchCancelled && boardTouchTool == "pan" && boardTouchPoints.Count == 1 && modal == null)
                boardScroll.scrollOffset = boardTouchScroll - ((Vector2)evt.position - boardTouchOrigin);
            else if (Vector2.Distance(evt.position, boardTouchOrigin) > 10) CancelBoardTouch();
            boardTouchPoints[evt.pointerId] = evt.position;
            evt.StopImmediatePropagation(); evt.PreventDefault();
        }

        private void OnBoardTouchUp(PointerUpEvent evt)
        {
            if (evt.pointerType != UnityEngine.UIElements.PointerType.touch || !boardTouchPoints.ContainsKey(evt.pointerId)) return;
            bool commit = !boardTouchCancelled && boardTouchPoints.Count == 1 && evt.pointerId == boardTouchPrimary &&
                modal == null && !busy && boardTouchTool != "pan" && Vector2.Distance(evt.position, boardTouchOrigin) <= 10;
            int cell = boardTouchCell, vertex = boardTouchVertex;
            boardTouchPoints.Remove(evt.pointerId);
            boardViewGestureActive = false;
            boardTouchCancelled = true;
            boardTouchPreview?.RemoveFromClassList("touch-preview"); boardTouchPreview = null;
            root.ReleasePointer(evt.pointerId);
            if (commit && vertex >= 0) SelectConnectionVertex(vertex);
            else if (commit && !wireDrawing && cell >= 0 && BoardCellAt(evt.position) == cell)
            {
                if (boardTouchTool == "area" && tutorialPick == null && flowTool == null)
                {
                    if (boardAreaStart < 0) { boardAreaStart = cell; UpdateBoardTouchHint(); }
                    else
                    {
                        int start = boardAreaStart; boardAreaStart = -1;
                        SelectBoardCells(Enumerable.Range(0, 81).Where(i => i / 9 >= Math.Min(start / 9, cell / 9) && i / 9 <= Math.Max(start / 9, cell / 9) &&
                            i % 9 >= Math.Min(start % 9, cell % 9) && i % 9 <= Math.Max(start % 9, cell % 9)));
                        Refresh(); UpdateBoardTouchHint();
                    }
                }
                else ExecuteBoardCell(cell, false, boardTouchReplace, true);
            }
            if (boardTouchPoints.Count == 0) { boardTouchCancelled = false; boardTouchPrimary = -1; }
            evt.StopImmediatePropagation(); evt.PreventDefault();
        }

        private void OnBoardTouchCancel(PointerCancelEvent evt)
        {
            if (!boardTouchPoints.Remove(evt.pointerId)) return;
            CancelBoardTouch(); root.ReleasePointer(evt.pointerId);
            if (boardTouchPoints.Count == 0) boardTouchCancelled = false;
        }

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
            if (boardTouchCancelled || boardTouchPoints.Count != 1 || boardTouchTool == "pan" || time - boardTouchStarted < .6f) return;
            if (tutorialPick != null || flowTool != null || wireDrawing) { CancelBoardTouch(); return; }
            if (boardTouchCell < 0) return;
            int cell = boardTouchCell;
            CancelBoardTouch(); SelectBoardCells(new[] { cell }); Refresh(); OpenBoardActions();
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

        private void CancelBoardTouch()
        {
            boardViewGestureActive = false;
            boardTouchCancelled = boardTouchPoints.Count != 0;
            boardTouchPreview?.RemoveFromClassList("touch-preview"); boardTouchPreview = null;
            boardTouchCell = boardTouchVertex = boardAreaStart = -1;
            UpdateBoardTouchHint();
        }

        private void ResetBoardTouch()
        {
            boardZoomAdjustment?.Pause(); boardZoomAdjustment = null; boardZoomPending = false;
            int[] pointers = boardTouchPoints.Keys.ToArray();
            boardTouchPoints.Clear(); boardTouchPrimary = -1;
            CancelBoardTouch();
            // 중단 중 사라진 Up을 기다리지 않는다. 새 입력은 새 제스처로 시작한다.
            foreach (int pointer in pointers) root?.ReleasePointer(pointer);
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            ResetBoardTouch();
            CheckpointOnInterruption();
        }

        private int BoardCellAt(Vector2 position)
        {
            if (!boardScroll.contentViewport.worldBound.Contains(position)) return -1;
            for (int cell = 0; cell < 81 && cell < board.childCount; cell++)
                if (board[cell].worldBound.Contains(position)) return cell;
            return -1;
        }

        private void SelectBoardCells(IEnumerable<int> cells)
        {
            int[] previous = boardTouchAdd ? Session.SelectedCells : Array.Empty<int>();
            Session.SelectCells(previous.Concat(cells));
            // 본체가 포함된 선택은 점유 칸 전체로 확장한다. 빈 칸도 영역 선택에서 유지한다.
            Session.SelectCells(Session.SelectedCells.Concat(ElementClipboard.SelectedCells(layer)));
        }

        private void OnBoardZoomWheel(WheelEvent evt)
        {
            if (!evt.ctrlKey || busy || Session == null || modal != null ||
                !boardScroll.contentViewport.worldBound.Contains(evt.mousePosition) || Mathf.Approximately(evt.delta.y, 0)) return;
            evt.StopImmediatePropagation();
            evt.PreventDefault();
            ZoomBoard(cellSize * Mathf.Pow(1.2f, -Mathf.Clamp(evt.delta.y / 3f, -2f, 2f)), evt.mousePosition);
        }

        private void ZoomBoard(float size, Vector2 pivot, Vector2? sourceCoordinate = null)
        {
            if (Session == null || modal != null) return;
            CancelBoardTouch();
            if (!boardZoomPending)
                boardZoomCoordinate = sourceCoordinate ?? board.WorldToLocal(pivot) / (board.resolvedStyle.width / 9);
            boardZoomPivot = pivot;
            autoFitBoard = false; cellSize = Mathf.Clamp(Mathf.Round(size), 32, 144);
            board.Clear(); DrawBoard(); DrawTutorialPreview(); DrawTutorialPicking();
            if (boardZoomPending) return;
            boardZoomPending = true;
            int attempts = 0, corrections = 0;
            boardZoomAdjustment = board.schedule.Execute(() =>
            {
                if (board?.panel == null || ++attempts > 8)
                { boardZoomAdjustment.Pause(); boardZoomPending = false; return; }
                if (float.IsNaN(board.resolvedStyle.width) || Mathf.Abs(board.resolvedStyle.width - cellSize * 9) > 1.1f) return;
                boardScroll.scrollOffset += board.LocalToWorld(boardZoomCoordinate * cellSize) - boardZoomPivot;
                // 레이아웃과 스크롤 제한을 반영해 한 번 더 보정하되 경계에서는 끝없이 재시도하지 않는다.
                if (++corrections >= 2) { boardZoomAdjustment.Pause(); boardZoomPending = false; }
            });
            boardZoomAdjustment.Every(1);
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
                var edited = new HashSet<string>();
                var targets = new List<int>();
                foreach (int selectedCell in Session.SelectedCells)
                {
                    int cell = selectedCell;
                    var existing = Placement(data, cell);
                    if (replace && existing != null)
                    {
                        if (!edited.Add((string)existing["instanceId"])) continue;
                        cell = (int)existing["coordinate"]["row"] * 9 + (int)existing["coordinate"]["column"];
                    }
                    targets.Add(cell);
                }
                // 원본 선택에서 대상 기준 칸을 먼저 고정한다. 작은 요소로 바뀌어도 나머지 점유 칸을 새 배치로 해석하지 않는다.
                foreach (int cell in targets) PlaceOnCurrentLayer(data, cell, replace);
            });
        }

        private void PlaceOnCurrentLayer(Newtonsoft.Json.Linq.JObject data, int cell, bool replace)
        {
            if (!replace && Placement(data, cell) != null)
                throw new InvalidOperationException("이미 요소가 있는 칸입니다. 교체를 켜거나 작업 메뉴에서 교체하세요.");
            // 교체도 다른 층을 지우지 않는다. 장애물과 블록 간 충돌은 기존 검증이 거절한다.
            Newtonsoft.Json.Linq.JArray before = new Newtonsoft.Json.Linq.JArray(data["elements"].Where(item => (string)item["layer"] != layer).Select(item => item.DeepClone()));
            LevelDocumentEditing.Place(data, definitions, brush, cell / 9, cell % 9, replace, color: color, direction: direction);
            Newtonsoft.Json.Linq.JArray after = new Newtonsoft.Json.Linq.JArray(data["elements"].Where(item => (string)item["layer"] != layer).Select(item => item.DeepClone()));
            if (!Newtonsoft.Json.Linq.JToken.DeepEquals(before, after))
                throw new InvalidOperationException("다른 층의 요소와 겹칩니다. 해당 층에서 먼저 편집하세요.");
        }
    }
}
#endif
