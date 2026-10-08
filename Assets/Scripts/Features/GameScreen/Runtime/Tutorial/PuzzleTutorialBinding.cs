using Board;
using System.Collections.Generic;
using Tutorial;
using UnityEngine;

namespace GameScreen
{
    /// <summary>세션 상태와 보드/아이템의 실제 화면 좌표를 안내 View에 연결한다.</summary>
    public sealed class PuzzleTutorialBinding : MonoBehaviour
    {
        [SerializeField] private TutorialOverlayView view;
        private PuzzleGameSession session;
        private PuzzleBoardInput input;
        private PuzzleItemBarView items;
        private TutorialProgressSnapshot snapshot;
        private Canvas canvas;
        private readonly Vector3[] itemCorners = new Vector3[4];
        private readonly List<Rect> focusAreas = new List<Rect>(82);
        public void Configure(TutorialOverlayView overlay) => view = overlay;
        public void Bind(PuzzleGameSession game, PuzzleBoardInput boardInput, PuzzleItemBarView itemBar)
        {
            Release(); session = game; input = boardInput; items = itemBar;
            canvas = GetComponentInParent<Canvas>();
            session.Changed += Refresh; view.Next.onClick.AddListener(Advance); Refresh();
        }
        public void Release()
        {
            if (!ReferenceEquals(session, null)) session.Changed -= Refresh;
            if (view != null) { view.Next.onClick.RemoveListener(Advance); view.Display(null, false, false); }
            session = null; snapshot = null; input = null; items = null;
        }
        private void OnDisable() => Release();
        private void Advance() { if (session != null && input != null && !input.IsUIBlocked) session.TryAdvanceTutorial(); }
        private void Refresh() => snapshot = session?.TutorialState;
        private Vector2 Local(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(view.Content, screen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector2 local);
            return local;
        }
        private void LateUpdate()
        {
            bool visible = session != null && session.CanShowTutorial && snapshot != null &&
                snapshot.State != TutorialProgressState.Completed && snapshot.State != TutorialProgressState.Cancelled && snapshot.State != TutorialProgressState.Error && !input.IsUIBlocked;
            view.Display(snapshot, visible, visible && !session.IsPresenting && !session.HasProgressFeedback);
            if (!visible) return;
            Vector2 screenLow = Local(Vector2.zero), screenHigh = Local(new Vector2(Screen.width, Screen.height));
            view.SetFocusBounds(Rect.MinMaxRect(screenLow.x, screenLow.y, screenHigh.x, screenHigh.y));
            Camera camera = session.BoardCamera;
            Vector3 cellOffset = session.TutorialCellWorldOffset;
            Vector2 first = Vector2.zero, second = Vector2.zero, cellSize = Vector2.zero;
            focusAreas.Clear();
            for (int row = 0; row < 9; row++) for (int column = 0; column < 9; column++)
            {
                BoardCoordinate at = new BoardCoordinate(row, column);
                Vector3 world = session.TutorialCellWorldPosition(at);
                Vector2 center = Local(camera.WorldToScreenPoint(world));
                Vector2 size = Local(camera.WorldToScreenPoint(world + cellOffset)) - center;
                size = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
                cellSize = size;
                bool highlighted = snapshot.First?.Equals(at) == true || snapshot.Second?.Equals(at) == true;
                for (int index = 0; index < snapshot.Highlights.Count; index++) highlighted |= snapshot.Highlights[index].Equals(at);
                view.SetCell(row * 9 + column, center, size, highlighted);
                if (highlighted) focusAreas.Add(new Rect(center - size / 2, size));
                if (snapshot.First?.Equals(at) == true) first = center;
                if (snapshot.Second?.Equals(at) == true) second = center;
            }
            float phase = Mathf.Repeat(Time.unscaledTime, 1.5f) / 1.5f;
            // 지정 칸과 교환 경로를 가리지 않도록 경로 옆에서 같은 방향으로 움직인다.
            view.Finger.sizeDelta = new Vector2(cellSize.x * .55f, cellSize.y * .8f);
            Vector2 fingerOffset = Mathf.Abs(second.x - first.x) > Mathf.Abs(second.y - first.y)
                ? new Vector2(0, -cellSize.y) : new Vector2(cellSize.x, 0);
            view.Finger.anchoredPosition = Vector2.Lerp(first, second, Mathf.SmoothStep(0, 1, Mathf.Clamp01(phase / .7f))) + fingerOffset;
            Rect viewport = camera.pixelRect;
            Vector2 low = Local(new Vector2(viewport.xMin, viewport.yMin)), high = Local(new Vector2(viewport.xMax, viewport.yMax));
            Rect area = view.Content.rect;
            float width = Mathf.Min(high.x - low.x, area.width - 24);
            float y = low.y - 62;
            float x = (low.x + high.x) / 2;
            if (area.width > area.height && low.x - area.xMin > 210)
            {
                width = Mathf.Min(300, low.x - area.xMin - 24);
                x = area.xMin + width / 2 + 12; y = area.yMin + 140;
            }
            else if (y - 54 < area.yMin + 90)
            {
                // 세로 화면의 여백이 짧으면 지정 대상 반대쪽에 배치한다.
                float targetY = snapshot.First.HasValue ? first.y : (low.y + high.y) / 2;
                if (!snapshot.First.HasValue && snapshot.Highlights.Count > 0)
                {
                    targetY = 0;
                    for (int index = 0; index < snapshot.Highlights.Count; index++)
                        targetY += Local(camera.WorldToScreenPoint(session.TutorialCellWorldPosition(snapshot.Highlights[index]))).y;
                    targetY /= snapshot.Highlights.Count;
                }
                y = targetY > (low.y + high.y) / 2 ? low.y + 58 : high.y - 58;
            }
            view.Bubble.sizeDelta = new Vector2(width, 108);
            view.Bubble.anchoredPosition = new Vector2(Mathf.Clamp(x, area.xMin + width / 2, area.xMax - width / 2), Mathf.Clamp(y, area.yMin + 54, area.yMax - 54));
            Rect? itemFocus = null;
            if (snapshot.Item.HasValue)
            {
                RectTransform button = items.ButtonRect(snapshot.Item.Value);
                button.GetWorldCorners(itemCorners);
                Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 itemLow = Local(RectTransformUtility.WorldToScreenPoint(uiCamera, itemCorners[0]));
                Vector2 itemHigh = Local(RectTransformUtility.WorldToScreenPoint(uiCamera, itemCorners[2]));
                itemFocus = Rect.MinMaxRect(itemLow.x, itemLow.y, itemHigh.x, itemHigh.y);
                Vector2 buttonScreen = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, button.position);
                view.FreeBadge.anchoredPosition = Local(buttonScreen) + new Vector2(0, -34);
            }
            view.SetItemFocus(itemFocus);
            if (itemFocus.HasValue) focusAreas.Add(itemFocus.Value);
            PlaceBubbleOutsideFocus(area);
        }

        private void PlaceBubbleOutsideFocus(Rect area)
        {
            Vector2 preferred = view.Bubble.anchoredPosition, size = view.Bubble.sizeDelta;
            float bestY = preferred.y, bestDistance = float.PositiveInfinity;
            void TryPosition(float candidate)
            {
                float y = Mathf.Clamp(candidate, area.yMin + size.y / 2, area.yMax - size.y / 2);
                float distance = Mathf.Abs(y - preferred.y);
                if (distance >= bestDistance) return;
                Rect bubble = new Rect(new Vector2(preferred.x - size.x / 2, y - size.y / 2), size);
                foreach (Rect focus in focusAreas) if (bubble.Overlaps(focus)) return;
                bestY = y; bestDistance = distance;
            }
            // 첫 조작 칸뿐 아니라 조건 대상과 아이템도 피하는 가장 가까운 세로 여백을 사용한다.
            TryPosition(preferred.y);
            foreach (Rect focus in focusAreas)
            {
                TryPosition(focus.yMin - size.y / 2 - 8);
                TryPosition(focus.yMax + size.y / 2 + 8);
            }
            view.Bubble.anchoredPosition = new Vector2(preferred.x, bestY);
        }
    }
}
