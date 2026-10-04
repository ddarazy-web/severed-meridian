using UnityEngine;
using PopupUI;
using UnityEngine.Events;

namespace GameScreen
{
    public sealed class PuzzleResultView : PopupView
    {
        [SerializeField] private UnityEngine.UI.Text title, detail;
        [SerializeField] private UnityEngine.UI.Button retry;
        [SerializeField] private UnityEngine.UI.Button nextLevel;
        private UnityAction onRetry, onNext;
        private PuzzleResultState value = new PuzzleResultState();
        public void Configure(UnityEngine.UI.Text heading, UnityEngine.UI.Text body, UnityEngine.UI.Button button)
        { title = heading; detail = body; retry = button; }
        public void Bind(System.Action action)
            => Bind(action, null);
        public void ConfigureNextButton(UnityEngine.UI.Button button) => nextLevel = button;
        public void Bind(System.Action retryAction, System.Action nextAction)
        {
            ReleaseRestore();
            onRetry = () => { if (isActiveAndEnabled && retry.interactable) retryAction?.Invoke(); };
            retry.onClick.AddListener(onRetry);
            if (nextLevel == null) return;
            onNext = () => { if (isActiveAndEnabled && nextLevel.interactable) nextAction?.Invoke(); };
            if (nextAction != null) nextLevel.onClick.AddListener(onNext);
        }
        public void SetTransition(bool visible, bool canAdvance, bool changing, string body)
        {
            value.Body = body; value.NextVisible = visible; value.NextEnabled = canAdvance; value.Busy = changing;
            ApplyState(value);
        }
        public void Show(string heading, string body)
        { value.Title = heading; value.Body = body; ApplyState(value); gameObject.SetActive(true); }
        public override void ApplyState(PopupState state)
        {
            value = state == null ? new PuzzleResultState() : (PuzzleResultState)state.Copy();
            title.text = value.Title; detail.text = value.Body; retry.interactable = !value.Busy;
            if (nextLevel == null) return;
            nextLevel.gameObject.SetActive(value.NextVisible); nextLevel.interactable = value.NextEnabled && !value.Busy;
        }
        public override PopupState CaptureState() => value.Copy();
        public override void PrepareRestore(PopupContext context)
        {
            PuzzlePopupBinding binding = GetComponentInParent<PuzzlePopupBinding>();
            if (binding == null) throw new System.InvalidOperationException("현재 게임 팝업 연결이 없습니다.");
            binding.ValidateRestoreConnection(this, context);
            binding.BindResult(this);
        }
        public override void ReleaseRestore()
        {
            if (retry != null && onRetry != null) retry.onClick.RemoveListener(onRetry);
            if (nextLevel != null && onNext != null) nextLevel.onClick.RemoveListener(onNext);
            onRetry = onNext = null;
        }
        private void OnDisable() { ReleaseRestore(); }
    }
}
