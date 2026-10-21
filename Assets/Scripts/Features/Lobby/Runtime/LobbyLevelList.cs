using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lobby
{
    public sealed class LobbyLevelList : MonoBehaviour
    {
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private LobbyLevelCell cellPrefab;
        [SerializeField, Min(1)] private float rowHeight = 80;
        private readonly List<LobbyLevelCell> cells = new List<LobbyLevelCell>();
        private IReadOnlyList<LobbyLevel> levels;
        private Action<LobbyLevel> selected;
        private float viewportHeight = -1;
        private int first = -1;

        public int CellCount => cells.Count;
        private void Awake() => scroll.onValueChanged.AddListener(OnScroll);
        private void OnDestroy() => scroll.onValueChanged.RemoveListener(OnScroll);
        private void OnScroll(Vector2 value) => Refresh(false);

        public void SetItems(IReadOnlyList<LobbyLevel> items, Action<LobbyLevel> onSelected)
        {
            levels = items;
            selected = onSelected;
            scroll.StopMovement();
            scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, items.Count * rowHeight);
            scroll.content.anchoredPosition = Vector2.zero;
            first = -1;
            Refresh(true);
        }

        private void LateUpdate()
        {
            if (levels != null && !Mathf.Approximately(viewportHeight, scroll.viewport.rect.height)) Refresh(true);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || levels == null || levels.Count == 0 || EventSystem.current == null) return;
            bool down = keyboard.downArrowKey.wasPressedThisFrame;
            bool up = keyboard.upArrowKey.wasPressedThisFrame;
            bool home = keyboard.homeKey.wasPressedThisFrame;
            bool end = keyboard.endKey.wasPressedThisFrame;
            if (!down && !up && !home && !end) return;
            GameObject focus = EventSystem.current.currentSelectedGameObject;
            if (focus != null && !focus.transform.IsChildOf(transform)) return;
            LobbyLevelCell current = focus == null ? null : focus.GetComponentInParent<LobbyLevelCell>();
            int index = current == null ? 0 : Mathf.Clamp(current.BoundIndex + (down ? 1 : up ? -1 : 0), 0, levels.Count - 1);
            if (home) index = 0;
            if (end) index = levels.Count - 1;
            float top = index * rowHeight;
            float offset = scroll.content.anchoredPosition.y;
            if (top < offset) offset = top;
            else if (top + rowHeight > offset + viewportHeight) offset = top + rowHeight - viewportHeight;
            scroll.StopMovement();
            scroll.content.anchoredPosition = new Vector2(0, Mathf.Max(0, offset));
            Refresh(false);
            cells[index % cells.Count].Focus();
        }

        private void Refresh(bool resize)
        {
            if (levels == null) return;
            resize |= !Mathf.Approximately(viewportHeight, scroll.viewport.rect.height);
            viewportHeight = scroll.viewport.rect.height;
            LobbyListWindow window = LobbyListWindow.Calculate(levels.Count, rowHeight, viewportHeight, scroll.content.anchoredPosition.y);
            if (resize)
            {
                while (cells.Count < window.Count) cells.Add(Instantiate(cellPrefab, scroll.content));
                while (cells.Count > window.Count)
                {
                    int last = cells.Count - 1;
                    cells[last].gameObject.SetActive(false);
                    Destroy(cells[last].gameObject);
                    cells.RemoveAt(last);
                }
            }
            if (!resize && first == window.First) return;
            first = window.First;
            // 나머지 연산으로 같은 슬롯을 재사용하여 새로 나타난 행만 다시 연결한다.
            for (int index = first; index < first + window.Count; index++)
            {
                LobbyLevelCell cell = cells[index % cells.Count];
                if (!resize && cell.BoundIndex == index) continue;
                RectTransform rect = (RectTransform)cell.transform;
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1);
                rect.sizeDelta = new Vector2(0, rowHeight);
                rect.anchoredPosition = new Vector2(0, -index * rowHeight);
                cell.Bind(index, levels[index], selected);
            }
        }
    }
}
