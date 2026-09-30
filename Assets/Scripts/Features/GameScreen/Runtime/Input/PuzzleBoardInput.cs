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
        public BoardCoordinate? Selected { get; private set; }

        public void Configure(PuzzleGameSession gameSession, PuzzleWorldBoard worldBoard, Camera camera)
        { session = gameSession; board = worldBoard; boardCamera = camera; }

        public bool TryGetCoordinate(Vector2 screenPosition, out BoardCoordinate coordinate)
        {
            coordinate = default;
            if (session == null || session.State == null || !TryLocalPoint(screenPosition, out Vector3 point)) return false;
            int column = Mathf.FloorToInt(point.x + 5), row = Mathf.FloorToInt(5 - point.y);
            if (row < 0 || row >= 10 || column < 0 || column >= 10) return false;
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
            if (session == null || !session.CanAcceptInput) { CancelGesture(); return; }
            bool touchFrame = false;
            foreach (EnhancedTouch touch in EnhancedTouch.activeTouches)
            {
                // 같은 입력 업데이트에서 시작과 종료가 와도 두 단계를 보존한다.
                UnityEngine.InputSystem.TouchPhase phase = touch.phase;
                touchFrame = true;
                int id = touch.touchId; Vector2 position = touch.screenPosition;
                if (phase == UnityEngine.InputSystem.TouchPhase.Began) BeginPointer(id, position);
                else if (phase == UnityEngine.InputSystem.TouchPhase.Ended) EndPointer(id, position);
                else if (phase == UnityEngine.InputSystem.TouchPhase.Canceled && pointer == id) CancelGesture();
            }
            // 터치의 합성 마우스 이벤트가 같은 행동을 다시 실행하지 않게 한다.
            if (touchFrame) return;
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.leftButton.wasPressedThisFrame) BeginPointer(-1, mouse.position.ReadValue());
            if (mouse.leftButton.wasReleasedThisFrame) EndPointer(-1, mouse.position.ReadValue());
        }

        private void BeginPointer(int id, Vector2 position)
        {
            if (pointer.HasValue || !session.CanAcceptInput) return;
            if (EventSystem.current != null)
            {
                uiHits.Clear();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
                if (uiHits.Count > 0) { CancelGesture(); return; }
            }
            if (!TryGetCoordinate(position, out pressed) || !TryLocalPoint(position, out start)) { CancelGesture(); return; }
            pointer = id;
        }

        private void EndPointer(int id, Vector2 position)
        {
            if (pointer != id) return;
            pointer = null;
            if (!session.CanAcceptInput || !TryGetCoordinate(position, out BoardCoordinate released) ||
                !TryLocalPoint(position, out Vector3 end)) { CancelGesture(); return; }
            Vector3 delta = end - start;
            if (Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) >= 0.25f)
            {
                int dr = 0, dc = 0;
                if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)) dc = delta.x > 0 ? 1 : -1;
                else dr = delta.y > 0 ? -1 : 1;
                BoardCoordinate next = new BoardCoordinate(pressed.Row + dr, pressed.Column + dc);
                if (next.Row >= 0 && next.Row < 10 && next.Column >= 0 && next.Column < 10 && session.State.CellAt(next).IsActive)
                    session.TrySwap(pressed, next);
                Selected = null; return;
            }
            RuntimeContent content = session.State.CellAt(released).Content;
            if (content >= RuntimeContent.Rocket && content <= RuntimeContent.Magnet)
            { session.TryActivate(released); Selected = null; return; }
            if (Selected.HasValue && Mathf.Abs(Selected.Value.Row - released.Row) + Mathf.Abs(Selected.Value.Column - released.Column) == 1)
            { session.TrySwap(Selected.Value, released); Selected = null; }
            else Selected = Selected.HasValue && Selected.Value.Equals(released) ? null : released;
        }

        public void CancelGesture() { pointer = null; Selected = null; }
        private void OnEnable() => EnhancedTouchSupport.Enable();
        private void OnDisable() { CancelGesture(); EnhancedTouchSupport.Disable(); }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelGesture(); }
    }
}
