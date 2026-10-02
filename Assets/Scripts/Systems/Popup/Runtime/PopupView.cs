using UnityEngine;

namespace PopupUI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class PopupView : MonoBehaviour
    {
        [SerializeField] private GameObject defaultSelection;
        private PopupService service;
        public PopupHandle Handle { get; private set; }
        public virtual GameObject DefaultSelection => defaultSelection;
        public void SetDefaultSelection(GameObject value) { defaultSelection = value; }
        internal void Bind(PopupService owner, PopupHandle handle) { service = owner; Handle = handle; }
        public void Close() { service?.Close(Handle); }
        public virtual void ApplyState(PopupState state) { }
        public virtual PopupState CaptureState() => null;
        public virtual void HandleCancel() { Close(); }
    }
}
