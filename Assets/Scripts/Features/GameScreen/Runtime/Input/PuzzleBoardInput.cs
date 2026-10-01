using System.Collections.Generic;
using Board;
using Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace GameScreen
{
    public sealed class PuzzleBoardInput : MonoBehaviour
    {
        [SerializeField] private PuzzleGameSession session;
        [SerializeField] private PuzzleWorldBoard board;
        [SerializeField] private Camera boardCamera;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private int? pointer;
        private BoardCoordinate pressed;
        private Vector3 start;
        private bool uiBlocked;
        private bool committed;
        public void SetUIBlocked(bool blocked)
        { uiBlocked = blocked; if (blocked) CancelGesture(); }
        public BoardCoordinate? Selected { get; private set; }
        public BoardItem? SelectedItem { get; private set; }
        public string SelectionMessage { get; private set; }
        public Vector3? SelectedWorldPosition => Selected.HasValue && board != null
            ? board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(Selected.Value)) : (Vector3?)null;
        public event System.Action SelectionChanged;

        public void SelectItem(BoardItem item)
        {
            if (session == null || !session.CanUseItems) return;
            CancelGesture();
            SelectionMessage = null;
            if (item == BoardItem.Shuffle)
            {
                CancelItemSelection();
                if (!session.TryUseItem(item)) SelectionMessage = session.Message;
                SelectionChanged?.Invoke();
                return;
            }
            SelectedItem = SelectedItem == item ? null : item;
            SelectionChanged?.Invoke();
        }

        public void CancelItemSelection()
        {
            bool changed = SelectedItem.HasValue || Selected.HasValue || SelectionMessage != null;
            SelectedItem = null; Selected = null; SelectionMessage = null;
            if (changed) SelectionChanged?.Invoke();
        }

        public void Configure(PuzzleGameSession gameSession, PuzzleWorldBoard worldBoard, Camera camera)
        { session = gameSession; board = worldBoard; boardCamera = camera; }

        public bool TryGetCoordinate(Vector2 screenPosition, out BoardCoordinate coordinate)
        {
            coordinate = default;
            if (session == null || session.State == null || !TryLocalPoint(screenPosition, out Vector3 point)) return false;
            int column = Mathf.FloorToInt(point.x + PuzzleWorldBoard.HalfWidth), row = Mathf.FloorToInt(PuzzleWorldBoard.HalfHeight - point.y);
            if (row < 0 || row >= BoardDefinition.DefaultRows || column < 0 || column >= BoardDefinition.DefaultColumns) return false;
            coordinate = new BoardCoordinate(row, column);
            return session.State.CellAt(coordinate).IsActive;
        }

        private bool TryLocalPoint(Vector2 position, out Vector3 point)
        {
            point = default;
            if (boardCamera == null || board == null || !boardCamera.pixelRect.Contains(position)) return false;
            Ray ray = boardCamera.ScreenPointToRay(position);
            Plane plane = new Plane(board.transform.forward, board.transform.position);
            if (!plane.Raycast(ray, out float distance)) return false;
            point = board.transform.InverseTransformPoint(ray.GetPoint(distance)); return true;
        }

        private void Update()
        {
            if (session == null) { CancelGesture(); return; }
            if ((!session.CanAcceptInput || uiBlocked) && !committed) CancelGesture();
            bool touchFrame = false;
            foreach (EnhancedTouch touch in EnhancedTouch.activeTouches)
            {
                // 같은 입력 업데이트에서 시작과 종료가 와도 두 단계를 보존한다.
                UnityEngine.InputSystem.TouchPhase phase = touch.phase;
                touchFrame = true;
                int id = touch.touchId; Vector2 position = touch.screenPosition;
                if (phase == UnityEngine.InputSystem.TouchPhase.Began) BeginPointer(id, position);
                else if (phase == UnityEngine.InputSystem.TouchPhase.Moved) UpdatePointer(id, position);
                else if (phase == UnityEngine.InputSystem.TouchPhase.Ended) EndPointer(id, position);
                else if (phase == UnityEngine.InputSystem.TouchPhase.Canceled && pointer == id) CancelGesture();
            }
            // 터치의 합성 마우스 이벤트가 같은 행동을 다시 실행하지 않게 한다.
            if (touchFrame) return;
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.leftButton.wasPressedThisFrame) BeginPointer(-1, mouse.position.ReadValue());
            if (mouse.leftButton.isPressed) UpdatePointer(-1, mouse.position.ReadValue());
            if (mouse.leftButton.wasReleasedThisFrame) EndPointer(-1, mouse.position.ReadValue());
        }

        private void BeginPointer(int id, Vector2 position)
        {
            if (pointer.HasValue || !session.CanAcceptInput || uiBlocked) return;
            if (EventSystem.current != null)
            {
                uiHits.Clear();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
                if (uiHits.Count > 0) { CancelGesture(); return; }
            }
            if (!TryGetCoordinate(position, out pressed) || !TryLocalPoint(position, out start)) { CancelGesture(); return; }
            if (!SelectedItem.HasValue && SelectionMessage != null)
            { SelectionMessage = null; SelectionChanged?.Invoke(); }
            pointer = id;
            committed = false;
            if (!SelectedItem.HasValue && ActionQuery.Movable(session.State, session.State.CellAt(pressed))) board.Preview(pressed, Vector3.zero);
        }

        private void UpdatePointer(int id, Vector2 position)
        {
            if (pointer != id || committed) return;
            if (!session.CanAcceptInput || uiBlocked || !TryLocalPoint(position, out Vector3 end))
            { CancelGesture(); return; }
            if (EventSystem.current != null)
            {
                uiHits.Clear();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
                if (uiHits.Count > 0) { CancelGesture(); return; }
            }
            if (SelectedItem.HasValue) return;
            Vector3 delta = end - start;
            bool horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            float distance = horizontal ? delta.x : delta.y;
            Vector3 axis = horizontal ? Vector3.right : Vector3.up;
            // 화면→보드 좌표 왕복의 부동소수점 오차만 허용한다.
            if (Mathf.Abs(distance) + 0.00001f < 0.25f)
            {
                RuntimeCell cell = session.State.CellAt(pressed);
                if (ActionQuery.Movable(session.State, cell)) board.Preview(pressed, axis * Mathf.Clamp(distance, -0.24f, 0.24f));
                return;
            }
            BoardCoordinate next = new BoardCoordinate(pressed.Row + (horizontal ? 0 : distance > 0 ? -1 : 1),
                pressed.Column + (horizontal ? distance > 0 ? 1 : -1 : 0));
            // 실행 결과가 거절이어도 이 포인터의 행동은 소비한다.
            committed = true; Selected = null;
            session.TrySwap(pressed, next);
            SelectionChanged?.Invoke();
        }

        private void EndPointer(int id, Vector2 position)
        {
            if (pointer != id) return;
            if (!committed) UpdatePointer(id, position);
            if (pointer != id) return;
            pointer = null;
            session.EndSwipePreview();
            if (committed) { committed = false; return; }
            if (EventSystem.current != null)
            {
                uiHits.Clear();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
                if (uiHits.Count > 0) { CancelGesture(); return; }
            }
            if (!session.CanAcceptInput || !TryGetCoordinate(position, out BoardCoordinate released) ||
                !TryLocalPoint(position, out Vector3 end)) { CancelGesture(); return; }
            if (SelectedItem.HasValue)
            {
                BoardItem item = SelectedItem.Value;
                if (!session.CanSelectItemTarget(item, released))
                { SelectionMessage = "이 칸에는 사용할 수 없어요"; SelectionChanged?.Invoke(); return; }
                SelectionMessage = null;
                if (item == BoardItem.Hammer)
                {
                    if (session.TryUseItem(item, released)) CancelItemSelection();
                    else { SelectionMessage = session.Message; SelectionChanged?.Invoke(); }
                }
                else if (!Selected.HasValue) { Selected = released; SelectionChanged?.Invoke(); }
                else if (Selected.Value.Equals(released)) { Selected = null; SelectionChanged?.Invoke(); }
                else if (session.TryUseItem(item, Selected.Value, released)) CancelItemSelection();
                else { SelectionMessage = session.Message; SelectionChanged?.Invoke(); }
                return;
            }
            RuntimeContent content = session.State.CellAt(released).Content;
            if (content >= RuntimeContent.Rocket && content <= RuntimeContent.Magnet)
            { session.TryActivate(released); Selected = null; SelectionChanged?.Invoke(); return; }
            if (Selected.HasValue && Mathf.Abs(Selected.Value.Row - released.Row) + Mathf.Abs(Selected.Value.Column - released.Column) == 1)
            { session.TrySwap(Selected.Value, released); Selected = null; }
            else Selected = Selected.HasValue && Selected.Value.Equals(released) ? null : released;
            SelectionChanged?.Invoke();
        }

        public void CancelGesture()
        {
            pointer = null; committed = false;
            if (session != null) session.EndSwipePreview();
            else if (board != null) board.ClearPreview();
            Selected = null;
        }
        private void OnEnable() => EnhancedTouchSupport.Enable();
        private void OnDisable() { CancelGesture(); EnhancedTouchSupport.Disable(); }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelGesture(); }
    }
}
