using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lobby
{
    public sealed class LobbyLevelCell : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Text title;
        [SerializeField] private Button playButton;
        private LobbyLevel level;
        private Action<LobbyLevel> selected;
        private ScrollRect scroll;
        private bool dragging;
        private int suppressClickFrame = -1;
        private string fullTitle;
        private float titleWidth = -1;
        public int BoundIndex { get; private set; } = -1;

        private void Awake()
        {
            playButton.onClick.AddListener(Select);
            playButton.navigation = new Navigation { mode = Navigation.Mode.None };
        }
        private void OnDestroy() => playButton.onClick.RemoveListener(Select);

        public void Bind(int index, LobbyLevel item, Action<LobbyLevel> onSelected)
        {
            if (BoundIndex != index && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == playButton.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            BoundIndex = index;
            level = item;
            selected = onSelected;
            fullTitle = string.IsNullOrEmpty(item.Name) ? $"레벨 {item.Number}" : $"{item.Number} · {item.Name}";
            FitTitle();
        }

        internal void Focus() => playButton.Select();

        private void LateUpdate()
        {
            if (fullTitle != null && !Mathf.Approximately(titleWidth, title.rectTransform.rect.width)) FitTitle();
        }

        private void FitTitle()
        {
            titleWidth = title.rectTransform.rect.width;
            var settings = title.GetGenerationSettings(Vector2.zero);
            float available = Mathf.Max(0, titleWidth) * title.pixelsPerUnit;
            if (title.cachedTextGeneratorForLayout.GetPreferredWidth(fullTitle, settings) <= available)
            {
                title.text = fullTitle;
                return;
            }
            int low = 0, high = fullTitle.Length;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                string candidate = fullTitle.Substring(0, middle) + "…";
                if (title.cachedTextGeneratorForLayout.GetPreferredWidth(candidate, settings) <= available) low = middle;
                else high = middle - 1;
            }
            title.text = low == 0 ? "…" : fullTitle.Substring(0, low) + "…";
        }

        private void Select()
        {
            if (!dragging && Time.frameCount != suppressClickFrame && level != null) selected?.Invoke(level);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            dragging = false;
            suppressClickFrame = -1;
            scroll = GetComponentInParent<ScrollRect>();
            scroll?.OnInitializePotentialDrag(eventData);
        }
        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = true;
            scroll = GetComponentInParent<ScrollRect>();
            scroll?.OnBeginDrag(eventData);
        }
        public void OnDrag(PointerEventData eventData) => scroll?.OnDrag(eventData);
        public void OnEndDrag(PointerEventData eventData)
        {
            scroll?.OnEndDrag(eventData);
            dragging = false;
            // 손을 뗀 프레임의 클릭만 막고 이후 키보드 선택은 허용한다.
            suppressClickFrame = Time.frameCount;
        }
    }
}
