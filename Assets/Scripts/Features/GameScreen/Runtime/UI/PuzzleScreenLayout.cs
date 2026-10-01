using UnityEngine;
using UnityEngine.UI;

namespace GameScreen
{
    public sealed class PuzzleScreenLayout : MonoBehaviour
    {
        [SerializeField] private CanvasScaler scaler;
        [SerializeField] private RectTransform safeRoot, hud, moves, missions, boardArea, items, prompt, pause, level;
        private PuzzleGameSession session;
        private PuzzleBoardInput input;
        private Rect lastSafe;
        private Vector2 lastSize;
        private int lastMissionCount = -1;
        public Rect BoardScreenRect { get; private set; }

        public void Configure(CanvasScaler canvasScaler, RectTransform safe, RectTransform hudRect,
            RectTransform movesRect, RectTransform missionRect, RectTransform board, RectTransform itemRect,
            RectTransform promptRect, RectTransform pauseRect, RectTransform levelRect)
        { scaler = canvasScaler; safeRoot = safe; hud = hudRect; moves = movesRect; missions = missionRect;
          boardArea = board; items = itemRect; prompt = promptRect; pause = pauseRect; level = levelRect; }

        public void Bind(PuzzleGameSession gameSession, PuzzleBoardInput boardInput)
        { session = gameSession; input = boardInput; session.HasScreenLayout = true; lastSize = Vector2.zero; }

        private void LateUpdate() => RefreshIfNeeded();

        public void RefreshIfNeeded()
        {
            Vector2 size = new Vector2(Screen.width, Screen.height);
            int count = 0;
            foreach (Transform child in missions) if (child.gameObject.activeSelf) count++;
            if (size != lastSize || Screen.safeArea != lastSafe || count != lastMissionCount)
            { lastMissionCount = count; ApplyLayout(Screen.safeArea, size); }
        }

        public static Rect CalculateBoard(Rect safeArea)
        {
            bool portrait = safeArea.height > safeArea.width;
            float scale = portrait ? safeArea.width / 450f : safeArea.height / 720f;
            float w = safeArea.width / scale, h = safeArea.height / scale;
            float side = portrait ? Mathf.Min(w - 38, h - 345) : Mathf.Min(562, w - 580);
            side = Mathf.Max(80, side);
            float x = (w - side) / 2;
            float top = portrait ? Mathf.Min(235, h - side - 140) : 86;
            return new Rect(safeArea.x + x * scale, safeArea.yMax - (top + side) * scale, side * scale, side * scale);
        }

        public void ApplyLayout(Rect safeArea, Vector2 screenSize)
        {
            if (safeRoot == null || screenSize.x <= 0 || screenSize.y <= 0) return;
            bool portrait = safeArea.height > safeArea.width;
            float scale = portrait ? safeArea.width / 450f : safeArea.height / 720f;
            scaler.referenceResolution = screenSize / scale;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.matchWidthOrHeight = 0;
            scaler.GetComponent<Canvas>().scaleFactor = scale;
            float w = safeArea.width / scale, h = safeArea.height / scale;
            Box(safeRoot, safeArea.x / scale, (screenSize.y - safeArea.yMax) / scale, w, h);
            Box(level, 24, 18, w - 100, 30); Box(pause, w - 66, 17, 44, 42);
            Box(hud, portrait ? 23 : 65, portrait ? 85 : 150, portrait ? w - 46 : 220, portrait ? 120 : 300);
            Box(moves, 0, 0, portrait ? 80 : 220, portrait ? 105 : 125);
            int columns = portrait ? Mathf.Clamp(lastMissionCount, 1, 4) : 2;
            int rows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, lastMissionCount) / (float)columns));
            Box(missions, portrait ? 95 : 0, portrait ? 0 : 145, portrait ? w - 141 : 220, portrait ? 110 : Mathf.Min(180, rows * 80 + 15));
            var grid = missions.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = columns;
                grid.cellSize = portrait ? new Vector2((w - 161 - 5 * (columns - 1)) / columns, 85) : new Vector2(92, 75);
                grid.spacing = new Vector2(5, 5);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(missions);
            Box(items, portrait ? (w - 273) / 2 : w - 166, portrait ? h - 104 : 199, portrait ? 273 : 86, portrait ? 68 : 286);
            for (int i = 0; i < items.childCount; i++)
                Box(items.GetChild(i) as RectTransform, portrait ? i * 101 : 0, portrait ? 0 : i * 102, portrait ? 71 : 86, portrait ? 67 : 82);
            Box(prompt, portrait ? 20 : w - 220, portrait ? h - 143 : 530, portrait ? w - 40 : 205, portrait ? 34 : 120);
            BoardScreenRect = CalculateBoard(safeArea);
            Box(boardArea, (BoardScreenRect.x - safeArea.x) / scale, (safeArea.yMax - BoardScreenRect.yMax) / scale, BoardScreenRect.width / scale, BoardScreenRect.height / scale);
            if (session != null && session.BoardCamera != null)
            {
                session.BoardCamera.rect = new Rect(BoardScreenRect.x / screenSize.x, BoardScreenRect.y / screenSize.y, BoardScreenRect.width / screenSize.x, BoardScreenRect.height / screenSize.y);
                session.BoardCamera.orthographicSize = PuzzleWorldBoard.HalfHeight + 0.3f;
            }
            lastSafe = safeArea; lastSize = screenSize;
            if (input != null) { input.CancelGesture(); input.CancelItemSelection(); }
        }

        public static void Box(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
    }
}
