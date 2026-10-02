using UnityEngine;
using UnityEngine.EventSystems;

namespace PopupUI
{
    // EventSystem의 선택 오브젝트에서 취소 입력을 한 경로로만 전달한다.
    public sealed class PopupInputGate : MonoBehaviour, ICancelHandler, IMoveHandler
    {
        private PopupHost host;
        private PopupHandle handle;
        private UnityEngine.UI.Selectable selectable;
        private UnityEngine.UI.Navigation navigation;
        private bool prepared;
        internal UnityEngine.UI.Navigation Navigation => prepared ? navigation : GetComponent<UnityEngine.UI.Selectable>().navigation;
        internal void Configure(PopupHost value, PopupHandle owner)
        {
            host = value; handle = owner;
            if (selectable == null) selectable = GetComponent<UnityEngine.UI.Selectable>();
            if (selectable == null) return;
            if (!prepared || selectable.navigation.mode != UnityEngine.UI.Navigation.Mode.None) navigation = selectable.navigation;
            prepared = true;
            // 기본 Selectable의 전역 탐색을 막고 최상위 내부 탐색만 수행한다.
            UnityEngine.UI.Navigation restricted = selectable.navigation;
            restricted.mode = UnityEngine.UI.Navigation.Mode.None; selectable.navigation = restricted;
        }
        public void OnCancel(BaseEventData data) { if (host != null) host.Cancel(handle, data); else data.Use(); }
        public void OnMove(AxisEventData data)
        { if (host != null) host.Move(handle, gameObject, navigation, data); else data.Use(); }
    }
}
