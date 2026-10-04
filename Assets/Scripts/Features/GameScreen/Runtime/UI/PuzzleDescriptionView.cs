using PopupUI;
using UnityEngine;
using UnityEngine.UI;

namespace GameScreen
{
    public sealed class PuzzleDescriptionView : PopupView
    {
        [SerializeField] private Text label;
        [SerializeField] private Button closeButton;
        private PuzzleDescriptionState value = new PuzzleDescriptionState();
        public void Configure(Text text, Button button) { label = text; closeButton = button; SetDefaultSelection(button.gameObject); }
        public override void ApplyState(PopupState state)
        { value = state == null ? new PuzzleDescriptionState() : (PuzzleDescriptionState)state.Copy(); label.text = value.Text; }
        public override PopupState CaptureState() => value.Copy();
        public override void PrepareRestore(PopupContext context)
        {
            PuzzlePopupBinding binding = GetComponentInParent<PuzzlePopupBinding>();
            if (binding == null) throw new System.InvalidOperationException("현재 게임 팝업 연결이 없습니다.");
            binding.ValidateRestoreConnection(this, context);
        }
        private void OnEnable() { if (closeButton != null) { closeButton.onClick.RemoveListener(Close); closeButton.onClick.AddListener(Close); } }
        public override void ReleaseRestore() { if (closeButton != null) closeButton.onClick.RemoveListener(Close); }
        private void OnDisable() { ReleaseRestore(); }
    }
}
