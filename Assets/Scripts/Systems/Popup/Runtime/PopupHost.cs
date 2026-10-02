using UnityEngine;
using System;
using UnityEngine.EventSystems;

namespace PopupUI
{
    [DefaultExecutionOrder(-20000)]
    public sealed class PopupHost : MonoBehaviour
    {
        private PopupService service;
        private RectTransform surface;
        private GameObject blocker;
        private PopupService.Instance focused;
        private int cancelFrame = -1;
        private bool inputBlocked, pauseRequested;
        private int requestRevision;
        public PopupContext Context { get; private set; }
        public event Action<bool> InputBlockChanged;
        public event Action<bool> PauseRequestChanged;
        internal Transform Surface => surface == null ? transform : surface;
        public void Attach(PopupService owner, PopupContext context)
        {
            if (owner == null) throw new System.ArgumentNullException(nameof(owner));
            if (!isActiveAndEnabled) throw new System.InvalidOperationException("활성 표시 영역에만 연결할 수 있습니다.");
            if (service == owner) return;
            if (service != null) throw new System.InvalidOperationException("기존 표시 영역을 먼저 해제하세요.");
            owner.Attach(this); service = owner; Context = context;
            EnsureSurface(); Refresh();
        }
        public void Detach()
        {
            if (service == null) return;
            PopupService previous = service; service = null;
            previous.Detach(this);
            focused = null;
            if (blocker != null) blocker.SetActive(false);
            PublishRequests(false, false);
        }
        private void EnsureSurface()
        {
            if (blocker == null)
            {
                blocker = new GameObject("PopupInputBlocker", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                blocker.transform.SetParent(transform, false);
                UnityEngine.UI.Image image = blocker.GetComponent<UnityEngine.UI.Image>();
                image.color = Color.clear; image.raycastTarget = true;
                Fit(blocker.transform as RectTransform);
                blocker.transform.SetAsFirstSibling();
            }
            if (surface == null)
            {
                GameObject root = new GameObject("PopupSurface", typeof(RectTransform)); root.transform.SetParent(transform, false);
                surface = root.transform as RectTransform; Fit(surface);
            }
        }
        public void ApplySafeArea(Rect safe, Vector2 screenSize)
        {
            if (screenSize.x <= 0 || screenSize.y <= 0) throw new ArgumentOutOfRangeException(nameof(screenSize));
            EnsureSurface();
            surface.anchorMin = new Vector2(safe.xMin / screenSize.x, safe.yMin / screenSize.y);
            surface.anchorMax = new Vector2(safe.xMax / screenSize.x, safe.yMax / screenSize.y);
            surface.offsetMin = Vector2.zero; surface.offsetMax = Vector2.zero;
        }
        internal void Refresh()
        {
            if (service == null) return;
            PopupService.Instance next = service.Count == 0 ? null : service.Instances[service.Count - 1];
            EventSystem events = EventSystem.current;
            if (focused != next && focused != null && focused.View != null && events != null)
            {
                GameObject selected = events.currentSelectedGameObject;
                if (selected != null && selected.transform.IsChildOf(focused.View.transform)) focused.Selection = selected;
            }
            bool pause = false;
            foreach (PopupService.Instance instance in service.Instances)
            {
                bool top = instance == next;
                CanvasGroup group = instance.View.GetComponent<CanvasGroup>();
                group.interactable = top; group.blocksRaycasts = top; group.ignoreParentGroups = false;
                instance.View.transform.SetAsLastSibling();
                foreach (UnityEngine.UI.Selectable selectable in instance.View.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
                {
                    PopupInputGate gate = selectable.GetComponent<PopupInputGate>();
                    if (gate == null) gate = selectable.gameObject.AddComponent<PopupInputGate>();
                    gate.Configure(this, instance.Handle);
                }
                PopupInputGate rootGate = instance.View.GetComponent<PopupInputGate>();
                if (rootGate == null) rootGate = instance.View.gameObject.AddComponent<PopupInputGate>();
                rootGate.Configure(this, instance.Handle);
                pause |= instance.Entry.PauseGameplay;
            }
            focused = next;
            blocker.SetActive(next != null);
            MaintainFocus();
            PublishRequests(next != null, pause);
        }
        private void Update() { if (service != null && service.Count > 0) MaintainFocus(); }
        private void MaintainFocus()
        {
            EventSystem events = EventSystem.current;
            if (events == null) return;
            if (focused == null)
            {
                if (events.currentSelectedGameObject != null && events.currentSelectedGameObject.transform.IsChildOf(transform))
                    events.SetSelectedGameObject(null);
                return;
            }
            GameObject selected = events.currentSelectedGameObject;
            if (ValidSelection(selected)) { PrepareSelection(selected); return; }
            GameObject target = focused.Selection;
            if (!ValidSelection(target)) target = focused.View.DefaultSelection;
            if (!ValidSelection(target)) target = focused.View.gameObject;
            PrepareSelection(target);
            events.SetSelectedGameObject(target);
        }
        private bool ValidSelection(GameObject target)
        {
            if (target == null || !target.activeInHierarchy || focused == null || !target.transform.IsChildOf(focused.View.transform)) return false;
            UnityEngine.UI.Selectable selectable = target.GetComponent<UnityEngine.UI.Selectable>();
            return selectable == null || (selectable.isActiveAndEnabled && selectable.IsInteractable());
        }
        private void PrepareSelection(GameObject target)
        {
            PopupInputGate gate = target.GetComponent<PopupInputGate>();
            if (gate == null) gate = target.AddComponent<PopupInputGate>();
            gate.Configure(this, focused.Handle);
        }
        internal void Move(PopupHandle handle, GameObject origin, UnityEngine.UI.Navigation navigation, AxisEventData data)
        {
            data.Use();
            if (service == null || service.Top != handle || !ValidSelection(origin)) return;
            UnityEngine.UI.Selectable target = null;
            Vector3 direction = data.moveDir switch {
                MoveDirection.Up => Vector3.up, MoveDirection.Down => Vector3.down,
                MoveDirection.Left => Vector3.left, MoveDirection.Right => Vector3.right, _ => Vector3.zero
            };
            if (navigation.mode == UnityEngine.UI.Navigation.Mode.Explicit)
            {
                target = data.moveDir switch {
                    MoveDirection.Up => navigation.selectOnUp, MoveDirection.Down => navigation.selectOnDown,
                    MoveDirection.Left => navigation.selectOnLeft, MoveDirection.Right => navigation.selectOnRight, _ => null
                };
            }
            else
            {
                bool horizontal = data.moveDir == MoveDirection.Left || data.moveDir == MoveDirection.Right;
                UnityEngine.UI.Navigation.Mode axis = horizontal ? UnityEngine.UI.Navigation.Mode.Horizontal : UnityEngine.UI.Navigation.Mode.Vertical;
                if ((navigation.mode & axis) != 0)
                {
                    float best = float.NegativeInfinity, wrapScore = float.NegativeInfinity;
                    UnityEngine.UI.Selectable wrap = null;
                    foreach (UnityEngine.UI.Selectable candidate in focused.View.GetComponentsInChildren<UnityEngine.UI.Selectable>())
                    {
                        if (candidate.gameObject == origin || !ValidSelection(candidate.gameObject)) continue;
                        PopupInputGate gate = candidate.GetComponent<PopupInputGate>();
                        UnityEngine.UI.Navigation.Mode candidateMode = gate == null ? candidate.navigation.mode : gate.Navigation.mode;
                        if (candidateMode == UnityEngine.UI.Navigation.Mode.None) continue;
                        Vector3 delta = candidate.transform.position - origin.transform.position;
                        float dot = Vector3.Dot(direction, delta);
                        if (dot > 0 && delta.sqrMagnitude > 0 && dot / delta.sqrMagnitude > best)
                        { best = dot / delta.sqrMagnitude; target = candidate; }
                        if (navigation.wrapAround && dot < 0 && -dot * delta.sqrMagnitude > wrapScore)
                        { wrapScore = -dot * delta.sqrMagnitude; wrap = candidate; }
                    }
                    if (target == null) target = wrap;
                }
            }
            if (target != null && ValidSelection(target.gameObject))
            { PrepareSelection(target.gameObject); data.selectedObject = target.gameObject; }
        }
        internal void Cancel(PopupHandle handle, BaseEventData data)
        {
            data.Use();
            if (service == null || service.Top != handle || cancelFrame == Time.frameCount) return;
            cancelFrame = Time.frameCount;
            PopupService.Instance top = service.Instances[service.Count - 1];
            if (top.Entry.CloseOnCancel) top.View.HandleCancel();
        }
        private void PublishRequests(bool blocked, bool paused)
        {
            bool blockChanged = inputBlocked != blocked, pauseChanged = pauseRequested != paused;
            if (!blockChanged && !pauseChanged) return;
            int revision = ++requestRevision;
            // 콜백이 목록을 다시 변경해도 이전 호출의 정지 요청을 뒤늦게 발행하지 않는다.
            inputBlocked = blocked; pauseRequested = paused;
            if (blockChanged && InputBlockChanged != null)
            {
                foreach (Action<bool> handler in InputBlockChanged.GetInvocationList())
                {
                    if (requestRevision != revision) return;
                    handler(blocked);
                }
            }
            if (requestRevision != revision) return;
            if (pauseChanged && PauseRequestChanged != null)
            {
                foreach (Action<bool> handler in PauseRequestChanged.GetInvocationList())
                {
                    if (requestRevision != revision) return;
                    handler(paused);
                }
            }
        }
        private static void Fit(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
        private void OnDisable() { Detach(); }
        private void OnDestroy() { Detach(); }
    }
}
