#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    // 제스처와 캡처·예약 작업만 소유한다. 문서와 선택은 외부 편집 세션에 위임한다.
    internal sealed class LevelToolBoardInput : IDisposable
    {
        internal struct Context
        {
            internal bool HasSession, Blocked, Busy, Picking, WireDrawing, Mobile, Replace;
            internal string Tool;
            internal float CellSize;
        }

        private readonly VisualElement root, board;
        private readonly ScrollView boardScroll;
        private readonly Func<Context> context;
        private readonly Action<int, bool, bool, bool> executeCell;
        private readonly Action<int> selectVertex;
        private readonly Action<IEnumerable<int>> selectCells;
        private readonly Action openActions, updateHint;
        private readonly Action<float> resizeBoard;
        private bool attached, disposed;
        private readonly Dictionary<int, Vector2> boardTouchPoints = new Dictionary<int, Vector2>();
        private int boardTouchPrimary = -1, boardTouchCell = -1, boardTouchVertex = -1, boardAreaStart = -1;
        private float boardTouchStarted, boardTouchZoomSize;
        private Vector2 boardTouchOrigin, boardTouchScroll;
        private bool boardTouchCancelled, boardViewGestureActive, boardZoomPending;
        private Vector2 boardZoomCoordinate, boardZoomPivot;
        private IVisualElementScheduledItem boardZoomAdjustment;
        private VisualElement boardTouchPreview;

        internal int AreaStart => boardAreaStart;
        internal bool HasPendingGesture => boardTouchPoints.Count != 0 || boardAreaStart >= 0;

        internal LevelToolBoardInput(VisualElement root, ScrollView scroll, VisualElement board,
            Func<Context> context, Action<int, bool, bool, bool> executeCell, Action<int> selectVertex,
            Action<IEnumerable<int>> selectCells, Action openActions, Action updateHint, Action<float> resizeBoard)
        {
            this.root = root; boardScroll = scroll; this.board = board; this.context = context;
            this.executeCell = executeCell; this.selectVertex = selectVertex; this.selectCells = selectCells;
            this.openActions = openActions; this.updateHint = updateHint; this.resizeBoard = resizeBoard;
        }

        internal void Attach()
        {
            if (disposed) throw new ObjectDisposedException(nameof(LevelToolBoardInput));
            if (attached) return;
            attached = true;
            boardScroll.RegisterCallback<WheelEvent>(OnBoardZoomWheel, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(OnBoardTouchDown, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerMoveEvent>(OnBoardTouchMove, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(OnBoardTouchUp, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCancelEvent>(OnBoardTouchCancel, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCaptureOutEvent>(OnBoardTouchCaptureOut);
            boardScroll.contentViewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometry);
        }

        private void OnViewportGeometry(GeometryChangedEvent evt)
        {
            boardScroll.contentContainer.style.minWidth = Mathf.Max(evt.newRect.width, context().CellSize * 9 + 32);
            if (!boardZoomPending) Cancel();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (attached)
            {
                boardScroll.UnregisterCallback<WheelEvent>(OnBoardZoomWheel, TrickleDown.TrickleDown);
                root.UnregisterCallback<PointerDownEvent>(OnBoardTouchDown, TrickleDown.TrickleDown);
                root.UnregisterCallback<PointerMoveEvent>(OnBoardTouchMove, TrickleDown.TrickleDown);
                root.UnregisterCallback<PointerUpEvent>(OnBoardTouchUp, TrickleDown.TrickleDown);
                root.UnregisterCallback<PointerCancelEvent>(OnBoardTouchCancel, TrickleDown.TrickleDown);
                root.UnregisterCallback<PointerCaptureOutEvent>(OnBoardTouchCaptureOut);
                boardScroll.contentViewport.UnregisterCallback<GeometryChangedEvent>(OnViewportGeometry);
                attached = false;
            }
            Reset();
        }

        private void OnBoardTouchCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.target != root || !boardTouchPoints.Remove(evt.pointerId)) return;
            Cancel();
            if (boardTouchPoints.Count == 0) boardTouchPrimary = -1;
        }

        private void OnBoardTouchDown(PointerDownEvent evt)
        {
            VisualElement target = evt.target as VisualElement;
            bool onBoard = target != null && boardScroll.Contains(target);
            // 마우스의 이동 도구도 같은 캡처·취소 경로를 사용하고 셀 편집을 실행하지 않는다.
            bool mousePan = evt.pointerType != UnityEngine.UIElements.PointerType.touch && onBoard && evt.button == 0 &&
                context().Tool == "pan" && !context().Blocked && !context().Busy && context().HasSession;
            if (evt.pointerType != UnityEngine.UIElements.PointerType.touch && !mousePan)
            {
                if (onBoard && evt.button == 1 && !context().Blocked && !context().Busy && context().HasSession)
                {
                    int cell = BoardCellAt(evt.position);
                    if (cell >= 0) { selectCells(new[] { cell }); openActions(); }
                    evt.StopImmediatePropagation(); evt.PreventDefault();
                }
                else if (onBoard && evt.button == 0 && !context().Blocked && !context().Busy && context().HasSession)
                {
                    // 셀의 자식만 처리한다. 연결 꼭짓점 오버레이는 자체 입력을 유지한다.
                    for (VisualElement current = target; current != null && current != board; current = current.parent)
                        if (current.name?.StartsWith("cell-", StringComparison.Ordinal) == true &&
                            int.TryParse(current.name.Substring(5), out int cell))
                        { executeCell(cell, evt.shiftKey, context().Replace || evt.clickCount >= 2, false); break; }
                }
                return;
            }
            if (boardTouchPoints.Count == 0 && (!onBoard || context().Blocked || context().Busy || !context().HasSession || !board.visible)) return;
            boardTouchPoints[evt.pointerId] = evt.position;
            if (boardTouchPoints.Count > 1)
            {
                root.CapturePointer(evt.pointerId);
                Cancel();
                boardViewGestureActive = boardTouchPoints.Count == 2 && onBoard && !context().Blocked;
                if (boardViewGestureActive) boardTouchZoomSize = context().CellSize;
                evt.StopImmediatePropagation(); evt.PreventDefault(); return;
            }
            if (boardTouchCancelled) return;
            boardTouchPrimary = evt.pointerId;
            boardTouchCell = BoardCellAt(evt.position); boardTouchVertex = -1;
            for (VisualElement current = target; current != null && current != board; current = current.parent)
                if (current.name?.StartsWith("wire-vertex-", StringComparison.Ordinal) == true)
                    int.TryParse(current.name.Substring(12), out boardTouchVertex);
            if (context().WireDrawing && context().Mobile)
            {
                // 겹친 터치 영역에서는 이벤트 대상보다 실제 위치에 가까운 꼭짓점을 고른다.
                Vector2 local = board.WorldToLocal(evt.position);
                int row = Mathf.RoundToInt(local.y / context().CellSize), column = Mathf.RoundToInt(local.x / context().CellSize);
                boardTouchVertex = row >= 0 && row <= 9 && column >= 0 && column <= 9 &&
                    boardScroll.contentViewport.worldBound.Contains(evt.position) &&
                    Vector2.Distance(local, new Vector2(column, row) * context().CellSize) <= 24 ? row * 10 + column : -1;
            }
            boardTouchOrigin = evt.position; boardTouchScroll = boardScroll.scrollOffset;
            boardTouchStarted = Time.unscaledTime;
            boardViewGestureActive = context().Tool == "pan";
            boardTouchPreview = board.Q("cell-" + boardTouchCell);
            boardTouchPreview?.AddToClassList("touch-preview");
            root.CapturePointer(evt.pointerId);
            evt.StopImmediatePropagation(); evt.PreventDefault();
        }

        private void OnBoardTouchMove(PointerMoveEvent evt)
        {
            if (!boardTouchPoints.TryGetValue(evt.pointerId, out Vector2 previous)) return;
            if (boardViewGestureActive && boardTouchPoints.Count == 2 && !context().Blocked)
            {
                Vector2 other = boardTouchPoints.First(pair => pair.Key != evt.pointerId).Value;
                float oldDistance = Vector2.Distance(previous, other), distance = Vector2.Distance(evt.position, other);
                Vector2 before = (previous + other) * .5f, after = ((Vector2)evt.position + other) * .5f;
                Vector2 coordinate = board.WorldToLocal(before) / (board.resolvedStyle.width / 9);
                if (oldDistance > 10)
                {
                    boardTouchZoomSize = Mathf.Clamp(boardTouchZoomSize * distance / oldDistance, 32, 144);
                    Zoom(boardTouchZoomSize, after, coordinate);
                }
                else boardScroll.scrollOffset -= after - before;
                boardViewGestureActive = true;
            }
            else if (boardViewGestureActive && !boardTouchCancelled && context().Tool == "pan" && boardTouchPoints.Count == 1 && !context().Blocked)
                boardScroll.scrollOffset = boardTouchScroll - ((Vector2)evt.position - boardTouchOrigin);
            else if (Vector2.Distance(evt.position, boardTouchOrigin) > 10) Cancel();
            boardTouchPoints[evt.pointerId] = evt.position;
            evt.StopImmediatePropagation(); evt.PreventDefault();
        }

        private void OnBoardTouchUp(PointerUpEvent evt)
        {
            if (!boardTouchPoints.ContainsKey(evt.pointerId)) return;
            bool commit = !boardTouchCancelled && boardTouchPoints.Count == 1 && evt.pointerId == boardTouchPrimary &&
                !context().Blocked && !context().Busy && context().Tool != "pan" && Vector2.Distance(evt.position, boardTouchOrigin) <= 10;
            int cell = boardTouchCell, vertex = boardTouchVertex;
            boardTouchPoints.Remove(evt.pointerId);
            boardViewGestureActive = false;
            boardTouchCancelled = true;
            boardTouchPreview?.RemoveFromClassList("touch-preview"); boardTouchPreview = null;
            root.ReleasePointer(evt.pointerId);
            if (commit && vertex >= 0) selectVertex(vertex);
            else if (commit && !context().WireDrawing && cell >= 0 && BoardCellAt(evt.position) == cell)
            {
                if (context().Tool == "area" && !context().Picking)
                {
                    if (boardAreaStart < 0) { boardAreaStart = cell; updateHint(); }
                    else
                    {
                        int start = boardAreaStart; boardAreaStart = -1;
                        selectCells(Enumerable.Range(0, 81).Where(i => i / 9 >= Math.Min(start / 9, cell / 9) && i / 9 <= Math.Max(start / 9, cell / 9) &&
                            i % 9 >= Math.Min(start % 9, cell % 9) && i % 9 <= Math.Max(start % 9, cell % 9)));
                        updateHint();
                    }
                }
                else executeCell(cell, false, context().Replace, true);
            }
            if (boardTouchPoints.Count == 0) { boardTouchCancelled = false; boardTouchPrimary = -1; }
            evt.StopImmediatePropagation(); evt.PreventDefault();
        }

        private void OnBoardTouchCancel(PointerCancelEvent evt)
        {
            if (!boardTouchPoints.Remove(evt.pointerId)) return;
            Cancel(); root.ReleasePointer(evt.pointerId);
            if (boardTouchPoints.Count == 0) boardTouchCancelled = false;
        }

        internal void Tick(float time)
        {
            if (disposed) return;
            if (context().Busy || context().Blocked) { Cancel(); return; }
            if (boardTouchCancelled || boardTouchPoints.Count != 1 || context().Tool == "pan" || time - boardTouchStarted < .6f) return;
            if (context().Picking || context().WireDrawing) { Cancel(); return; }
            if (boardTouchCell < 0) return;
            int cell = boardTouchCell;
            Cancel(); selectCells(new[] { cell }); openActions();
        }

        internal void Cancel()
        {
            boardViewGestureActive = false;
            boardTouchCancelled = boardTouchPoints.Count != 0;
            boardTouchPreview?.RemoveFromClassList("touch-preview"); boardTouchPreview = null;
            boardTouchCell = boardTouchVertex = boardAreaStart = -1;
            updateHint();
        }

        internal void Reset()
        {
            boardZoomAdjustment?.Pause(); boardZoomAdjustment = null; boardZoomPending = false;
            int[] pointers = boardTouchPoints.Keys.ToArray();
            boardTouchPoints.Clear(); boardTouchPrimary = -1;
            Cancel();
            // 중단 중 사라진 Up을 기다리지 않는다. 새 입력은 새 제스처로 시작한다.
            foreach (int pointer in pointers) root?.ReleasePointer(pointer);
        }

        private int BoardCellAt(Vector2 position)
        {
            if (!boardScroll.contentViewport.worldBound.Contains(position)) return -1;
            for (int cell = 0; cell < 81 && cell < board.childCount; cell++)
                if (board[cell].worldBound.Contains(position)) return cell;
            return -1;
        }

        private void OnBoardZoomWheel(WheelEvent evt)
        {
            if (!evt.ctrlKey || context().Busy || !context().HasSession || context().Blocked ||
                !boardScroll.contentViewport.worldBound.Contains(evt.mousePosition) || Mathf.Approximately(evt.delta.y, 0)) return;
            evt.StopImmediatePropagation();
            evt.PreventDefault();
            Zoom(context().CellSize * Mathf.Pow(1.2f, -Mathf.Clamp(evt.delta.y / 3f, -2f, 2f)), evt.mousePosition);
        }

        internal void Zoom(float size, Vector2 pivot, Vector2? sourceCoordinate = null)
        {
            if (disposed || !context().HasSession || context().Blocked) return;
            Cancel();
            if (!boardZoomPending)
                boardZoomCoordinate = sourceCoordinate ?? board.WorldToLocal(pivot) / (board.resolvedStyle.width / 9);
            boardZoomPivot = pivot;
            resizeBoard(Mathf.Clamp(Mathf.Round(size), 32, 144));
            if (boardZoomPending) return;
            boardZoomPending = true;
            int attempts = 0, corrections = 0;
            boardZoomAdjustment = board.schedule.Execute(() =>
            {
                if (board?.panel == null || ++attempts > 8)
                { boardZoomAdjustment.Pause(); boardZoomPending = false; return; }
                if (float.IsNaN(board.resolvedStyle.width) || Mathf.Abs(board.resolvedStyle.width - context().CellSize * 9) > 1.1f) return;
                boardScroll.scrollOffset += board.LocalToWorld(boardZoomCoordinate * context().CellSize) - boardZoomPivot;
                // 레이아웃과 스크롤 제한을 반영해 한 번 더 보정하되 경계에서는 끝없이 재시도하지 않는다.
                if (++corrections >= 2) { boardZoomAdjustment.Pause(); boardZoomPending = false; }
            });
            boardZoomAdjustment.Every(1);
        }
    }
}
#endif
