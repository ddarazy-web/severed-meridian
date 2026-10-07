using UnityEngine;

namespace Tutorial
{
    /// <summary>전달받은 안내 상태를 표시하며 진행과 보드 권한은 판정하지 않는다.</summary>
    public sealed class TutorialOverlayView : MonoBehaviour
    {
        [SerializeField] private RectTransform content, bubble, finger, freeBadge;
        [SerializeField] private UnityEngine.UI.Text instructions;
        [SerializeField] private UnityEngine.UI.Button next;
        [SerializeField] private UnityEngine.UI.Image[] cells;
        [SerializeField] private UnityEngine.UI.Outline[] outlines;
        private TutorialFocusGraphic focus;
        public UnityEngine.UI.Button Next => next;
        public RectTransform Content => content;
        public RectTransform Bubble => bubble;
        public RectTransform Finger => finger;
        public RectTransform FreeBadge => freeBadge;

        public void Configure(RectTransform body, RectTransform balloon, RectTransform hand, RectTransform badge,
            UnityEngine.UI.Text text, UnityEngine.UI.Button button, UnityEngine.UI.Image[] shades)
        {
            content = body; bubble = balloon; finger = hand; freeBadge = badge; instructions = text; next = button; cells = shades;
            outlines = new UnityEngine.UI.Outline[cells.Length];
            for (int i = 0; i < cells.Length; i++) outlines[i] = cells[i].GetComponent<UnityEngine.UI.Outline>();
        }

        public void Display(TutorialProgressSnapshot snapshot, bool visible, bool nextEnabled)
        {
            content.gameObject.SetActive(visible);
            if (!visible) return;
            instructions.text = snapshot.Instructions;
            next.gameObject.SetActive(snapshot.State == TutorialProgressState.AwaitDescription);
            next.interactable = nextEnabled;
            finger.gameObject.SetActive(snapshot.State == TutorialProgressState.AwaitAction && snapshot.First.HasValue && snapshot.Second.HasValue);
            freeBadge.gameObject.SetActive(snapshot.FreeItemAvailable && snapshot.Item.HasValue);
        }

        public void SetCell(int index, Vector2 center, Vector2 size, bool highlighted)
        {
            UnityEngine.UI.Image cell = cells[index];
            cell.rectTransform.anchoredPosition = center; cell.rectTransform.sizeDelta = size;
            cell.color = Color.clear;
            outlines[index].enabled = false;
            EnsureFocus();
            focus.SetHole(index, highlighted ? new Rect(center - size / 2, size) : default);
        }

        public void SetFocusBounds(Rect screen)
        {
            EnsureFocus(); focus.SetBounds(screen);
        }

        public void SetItemFocus(Rect? item)
        {
            EnsureFocus(); focus.SetHole(81, item ?? default);
        }

        private void EnsureFocus()
        {
            if (focus != null) return;
            // 기존 프리팹의 Content에 그려 말풍선/다음 버튼보다 뒤에 둔다.
            focus = content.gameObject.AddComponent<TutorialFocusGraphic>();
            focus.color = new Color(0, 0, 0, .55f);
            focus.raycastTarget = false;
        }
    }
}

