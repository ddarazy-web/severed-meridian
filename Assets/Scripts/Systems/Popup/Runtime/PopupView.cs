using UnityEngine;
using System.Collections.Generic;

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
        public virtual string CaptureFocusKey(GameObject selection)
        {
            if (selection == null || !selection.transform.IsChildOf(transform)) return null;
            List<int> path = new List<int>();
            for (Transform child = selection.transform; child != transform; child = child.parent) path.Add(child.GetSiblingIndex());
            path.Reverse(); return string.Join("/", path);
        }
        public virtual GameObject ResolveFocusKey(string key)
        {
            if (key == null) return null;
            Transform selected = transform;
            if (key.Length == 0) return gameObject;
            foreach (string part in key.Split('/'))
            {
                if (!int.TryParse(part, out int index) || index < 0 || index >= selected.childCount) return null;
                selected = selected.GetChild(index);
            }
            return selected.gameObject;
        }
        public virtual void PrepareRestore(PopupContext context) { }
        // 활성화되지 않은 후보도 해제할 수 있어야 하며 반복 호출에 안전해야 한다.
        public virtual void ReleaseRestore() { }
    }
}
