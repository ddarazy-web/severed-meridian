using UnityEngine;
using PopupUI;
using UnityEngine.Events;

namespace GameScreen
{
    public sealed class PuzzlePauseView : PopupView
    {
        [SerializeField] private UnityEngine.UI.Button resume, retry;
        [SerializeField] private UnityEngine.UI.Button lobby;
        private UnityAction onResume, onRetry, onLobby;
        private PuzzlePauseState value = new PuzzlePauseState();
        public void Configure(UnityEngine.UI.Button resumeButton, UnityEngine.UI.Button retryButton)
        { resume = resumeButton; retry = retryButton; }
        public void Bind(System.Action resumeAction, System.Action retryAction, System.Action lobbyAction = null)
        {
            ReleaseRestore();
            onResume = () => { if (isActiveAndEnabled && resume.interactable) resumeAction?.Invoke(); };
            onRetry = () => { if (isActiveAndEnabled && retry.interactable) retryAction?.Invoke(); };
            resume.onClick.AddListener(onResume); retry.onClick.AddListener(onRetry);
            if (lobby != null)
            {
                lobby.gameObject.SetActive(lobbyAction != null);
                onLobby = () => { if (isActiveAndEnabled && lobby.interactable) lobbyAction?.Invoke(); };
                lobby.onClick.AddListener(onLobby);
            }
        }
        public override void ApplyState(PopupState state)
        {
            value = state == null ? new PuzzlePauseState() : (PuzzlePauseState)state.Copy();
            resume.interactable = retry.interactable = !value.Busy;
            if (lobby != null) lobby.interactable = !value.Busy;
        }
        public override PopupState CaptureState() => value.Copy();
        public override void PrepareRestore(PopupContext context)
        {
            PuzzlePopupBinding binding = GetComponentInParent<PuzzlePopupBinding>();
            if (binding == null) throw new System.InvalidOperationException("현재 게임 팝업 연결이 없습니다.");
            binding.ValidateRestoreConnection(this, context);
            binding.BindPause(this);
        }
        public override void ReleaseRestore()
        {
            if (resume != null && onResume != null) resume.onClick.RemoveListener(onResume);
            if (retry != null && onRetry != null) retry.onClick.RemoveListener(onRetry);
            if (lobby != null && onLobby != null) lobby.onClick.RemoveListener(onLobby);
            onResume = onRetry = onLobby = null;
        }
        private void OnDisable() { ReleaseRestore(); }
    }
}
