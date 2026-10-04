using Simulation;
using UnityEngine;

namespace GameScreen
{
    public sealed class PuzzleScreenView : MonoBehaviour
    {
        [SerializeField] private PuzzleGameSession session;
        [SerializeField] private PuzzleBoardInput input;
        [SerializeField] private PuzzleHudView hud;
        [SerializeField] private PuzzleItemBarView items;
        [SerializeField] private PuzzlePauseView pause;
        [SerializeField] private PuzzleResultView result;
        [SerializeField] private PuzzleScreenLayout layout;
        [SerializeField] private UnityEngine.UI.Button pauseButton, cancel, description;
        [SerializeField] private UnityEngine.UI.Text level, message, descriptionText;
        [SerializeField] private RectTransform selection;
        private PuzzlePopupBinding popupBinding;
        private UnityEngine.Events.UnityAction onPause, onCancel;
        private System.Action<string> onDescribe;
        private int bindRevision;
        public void SetSelectionView(RectTransform rect) => selection = rect;
        private void LateUpdate()
        {
            if (hud != null && session != null) hud.Frame(session);
            if (selection == null || session == null || input == null) return;
            Vector3? position = input.SelectedWorldPosition;
            selection.gameObject.SetActive(position.HasValue && session.CanAcceptInput);
            if (!position.HasValue || session.BoardCamera == null) return;
            Vector2 screen = session.BoardCamera.WorldToScreenPoint(position.Value);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(selection.parent as RectTransform, screen, null, out Vector2 local);
            selection.anchoredPosition = local;
            float size = session.BoardCamera.pixelHeight / (session.BoardCamera.orthographicSize * 2) / GetComponent<Canvas>().scaleFactor;
            selection.sizeDelta = Vector2.one * size * .96f;
        }
        public void Setup(PuzzleHudView h, PuzzleItemBarView i, PuzzlePauseView p, PuzzleResultView r,
            PuzzleScreenLayout l, UnityEngine.UI.Button pb, UnityEngine.UI.Button cb, UnityEngine.UI.Button db,
            UnityEngine.UI.Text levelLabel, UnityEngine.UI.Text status, UnityEngine.UI.Text explanation)
        { hud = h; items = i; pause = p; result = r; layout = l; pauseButton = pb; cancel = cb;
          description = db; level = levelLabel; message = status; descriptionText = explanation; }
        public void Configure(PuzzleGameSession gameSession, PuzzleBoardInput boardInput)
        {
            Unsubscribe(); session = gameSession; input = boardInput;
            if (isActiveAndEnabled) Bind();
        }
        private void OnEnable() { if (session != null && input != null) Bind(); }
        private void OnDisable() { Unsubscribe(); popupBinding?.Release(); if (input != null) input.SetScreenUIBlocked(false); }
        private void Unsubscribe()
        {
            bindRevision++;
            if (session != null) session.Changed -= Refresh; if (input != null) input.SelectionChanged -= Refresh;
            if (pauseButton != null && onPause != null) pauseButton.onClick.RemoveListener(onPause);
            if (cancel != null && onCancel != null) cancel.onClick.RemoveListener(onCancel);
            if (hud != null && hud.Describe == onDescribe) hud.Describe = null;
            onPause = onCancel = null; onDescribe = null;
        }
        private void Bind()
        {
            Unsubscribe(); session.Changed += Refresh; input.SelectionChanged += Refresh;
            layout.Bind(session, input); items.Bind(input);
            popupBinding = GetComponent<PuzzlePopupBinding>();
            if (popupBinding != null) popupBinding.Configure(session, input, null, null);
            int revision = bindRevision;
            onDescribe = text => { if (isActiveAndEnabled && revision == bindRevision && popupBinding != null && popupBinding.Service != null) popupBinding.OpenDescription(text); }; hud.Describe = onDescribe;
            onPause = () => { if (isActiveAndEnabled && revision == bindRevision && popupBinding != null && popupBinding.Service != null) popupBinding.OpenPause(); }; pauseButton.onClick.AddListener(onPause);
            onCancel = () => { if (isActiveAndEnabled && revision == bindRevision) input.CancelItemSelection(); }; cancel.onClick.AddListener(onCancel); Refresh();
        }
        private void Retry()
        { popupBinding?.Retry(); }
        private void NextLevel()
        {
            popupBinding?.NextLevel();
        }
        private void Refresh()
        {
            if (session == null) return;
            hud.Refresh(session); layout.RefreshIfNeeded(); items.Refresh(session, input);
            level.text = session.State == null ? "달토끼 고물상" : "LEVEL " + session.State.LevelNumber;
            bool selected = input.SelectedItem.HasValue;
            message.text = input.SelectionMessage ?? session.FeedbackStatus ?? (selected ? (input.SelectedItem == BoardItem.Hammer ? "제거할 칸을 고르세요" : input.Selected.HasValue ? "인접한 두 번째 칸을 고르세요" : "바꿀 두 칸을 고르세요")
                : session.IsReady ? "시험용 아이템 · 수량 무제한" : session.Message);
            cancel.gameObject.SetActive(selected);
            message.rectTransform.offsetMax = new Vector2(selected ? -75 : 0, 0);
            pauseButton.interactable = session.IsReady && !session.IsRestarting && !session.IsChangingLevel && (session.Outcome == null || session.IsPresenting || session.HasProgressFeedback);
            input.SetScreenUIBlocked(session.ResultReady || session.IsChangingLevel || session.IsRestarting || session.IsPaused || !session.IsReady);
            popupBinding?.Refresh();
        }
    }
}
