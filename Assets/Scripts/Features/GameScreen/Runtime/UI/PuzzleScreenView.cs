using System.Threading;
using Cysharp.Threading.Tasks;
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
        private BoardOutcome shownOutcome;
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
        private void OnDisable() { Unsubscribe(); if (input != null) input.SetUIBlocked(false); }
        private void Unsubscribe()
        { if (session != null) session.Changed -= Refresh; if (input != null) input.SelectionChanged -= Refresh; }
        private void Bind()
        {
            Unsubscribe(); session.Changed += Refresh; input.SelectionChanged += Refresh;
            layout.Bind(session, input); items.Bind(input);
            hud.Describe = text => { input.CancelGesture(); input.CancelItemSelection(); descriptionText.text = text + "\n\n눌러서 닫기"; description.gameObject.SetActive(true); input.SetUIBlocked(true); };
            pauseButton.onClick.RemoveAllListeners(); pauseButton.onClick.AddListener(() =>
            { input.CancelGesture(); input.CancelItemSelection(); description.gameObject.SetActive(false); session.SetPaused(true); });
            cancel.onClick.RemoveAllListeners(); cancel.onClick.AddListener(input.CancelItemSelection);
            description.onClick.RemoveAllListeners(); description.onClick.AddListener(() => { description.gameObject.SetActive(false); Refresh(); });
            pause.Bind(() => session.SetPaused(false), Retry); result.Bind(Retry); Refresh();
        }
        private void Retry()
        { input.CancelGesture(); input.CancelItemSelection(); description.gameObject.SetActive(false); session.RestartAsync(CancellationToken.None).Forget(Debug.LogException); }
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
            pauseButton.interactable = session.IsReady && !session.IsRestarting && (session.Outcome == null || session.IsPresenting || session.HasProgressFeedback);
            pause.gameObject.SetActive(session.IsPaused);
            bool ended = session.ResultReady;
            input.SetUIBlocked(ended || session.IsPaused || description.gameObject.activeSelf);
            if (ended)
            {
                if (shownOutcome != session.Outcome)
                {
                    shownOutcome = session.Outcome;
                    bool won = session.Outcome.Kind == BoardOutcomeKind.Won;
                    result.Show(won ? "정리 완료!" : "다시 도전해요", "남은 이동 " + session.State.MovesRemaining + "\n" + session.Message);
                    session.PlayResultFeedback();
                }
            }
            else { shownOutcome = null; result.gameObject.SetActive(false); }
        }
    }
}
