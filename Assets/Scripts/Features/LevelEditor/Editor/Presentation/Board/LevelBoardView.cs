using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed class LevelBoardView : VisualElement
    {
        public const int CellSize = 40;
        private readonly Label[] cells = new Label[100];
        private readonly VisualElement bodies = new VisualElement { name = "large-bodies", pickingMode = PickingMode.Ignore };
        private readonly VisualElement supplyMarks = new VisualElement { name = "supply-markers", pickingMode = PickingMode.Ignore };
        private readonly HashSet<BoardCoordinate> visited = new HashSet<BoardCoordinate>();
        private readonly HashSet<BoardCoordinate> errors = new HashSet<BoardCoordinate>();
        private LevelDefinition level;
        private BoardCoordinate? selected;
        private Vector2? previous;
        private int pointerId;
        private bool dragging;
        private string strokeSource;
        private BoardCoordinate? hover;
        private BoardCoordinate? pressed;
        private int moveIndex = -1;
        private string moveSource;

        public LevelBrush Brush { get; set; }
        public RabbitColor Color { get; set; }
        public PlacementLayer Layer { get; set; }
        public PlacementBrush Placement { get; set; }
        public int MoveIndex => moveIndex;
        public bool IsDragging => dragging;
        public event Action<BoardCoordinate> Selected;
        public event Action<BoardCoordinate, bool> SourceSelected;
        public event Action<BoardCoordinate, bool> PlacementSelected;
        public IReadOnlyCollection<BoardCoordinate> PlacementSelection { get; set; } = Array.Empty<BoardCoordinate>();
        public IReadOnlyCollection<BoardCoordinate> SourceSelection { get; set; } = Array.Empty<BoardCoordinate>();
        public event Action<LevelBrush, RabbitColor, IReadOnlyCollection<BoardCoordinate>> Committed;
        public event Action<int, BoardCoordinate> MoveCommitted;
        public event Action Cancelled;

        internal static readonly Color[] Swatches =
        {
            new Color32(255, 134, 151, 255), new Color32(255, 211, 92, 255),
            new Color32(106, 198, 255, 255), new Color32(119, 216, 171, 255), new Color32(194, 154, 255, 255)
        };

        public LevelBoardView()
        {
            name = "level-board";
            style.width = style.minWidth = CellSize * 10;
            style.height = style.minHeight = CellSize * 10;
            style.flexShrink = 0;
            style.flexDirection = FlexDirection.Row;
            style.flexWrap = Wrap.Wrap;
            focusable = true;
            for (int i = 0; i < cells.Length; i++)
            {
                Label cell = new Label { name = "cell-" + i, pickingMode = PickingMode.Ignore };
                cell.style.width = cell.style.height = CellSize;
                cell.style.unityTextAlign = TextAnchor.MiddleCenter;
                cell.style.fontSize = 18;
                cell.style.unityFontStyleAndWeight = FontStyle.Bold;
                cell.style.borderLeftWidth = cell.style.borderRightWidth = 1;
                cell.style.borderTopWidth = cell.style.borderBottomWidth = 1;
                cells[i] = cell;
                Add(cell);
            }
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            bodies.style.position = Position.Absolute;
            bodies.style.left = bodies.style.top = 0;
            bodies.style.width = bodies.style.height = CellSize * 10;
            Add(bodies);
            supplyMarks.style.position = Position.Absolute;
            supplyMarks.style.left = supplyMarks.style.top = 0;
            supplyMarks.style.width = supplyMarks.style.height = CellSize * 10;
            Add(supplyMarks);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => CancelStroke());
            RegisterCallback<DetachFromPanelEvent>(_ => CancelStroke());
            RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Escape) CancelStroke(); });
            RegisterCallback<PointerLeaveEvent>(_ => { if (!dragging) { hover = null; Redraw(); } });
        }

        public void Display(LevelDefinition target, BoardCoordinate? selection)
        {
            level = target;
            selected = selection;
            Redraw();
        }

        public void SetErrors(IEnumerable<BoardCoordinate> coordinates)
        {
            errors.Clear();
            foreach (BoardCoordinate coordinate in coordinates)
                errors.Add(coordinate);
            Redraw();
        }

        public VisualElement CellAt(BoardCoordinate coordinate) => cells[coordinate.Row * 10 + coordinate.Column];

        public void BeginMove(int index)
        {
            CancelStroke();
            moveIndex = index;
            moveSource = JsonUtility.ToJson(level);
            Brush = LevelBrush.Move;
            Focus();
            Redraw();
        }

        public void CancelStroke()
        {
            Cancelled?.Invoke();
            bool wasDragging = dragging;
            dragging = false;
            visited.Clear();
            previous = null;
            pressed = null;
            hover = null;
            moveIndex = -1;
            if (Brush == LevelBrush.Move) Brush = LevelBrush.Select;
            if (wasDragging && this.HasPointerCapture(pointerId))
                this.ReleasePointer(pointerId);
            Redraw();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || level == null || dragging)
                return;
            Vector2 local = this.WorldToLocal(evt.position);
            if (!TryCoordinate(local, out BoardCoordinate coordinate))
                return;
            if (Brush == LevelBrush.Move && JsonUtility.ToJson(level) != moveSource) { CancelStroke(); return; }
            Focus();
            if (Brush == LevelBrush.SourceSelect)
            {
                SourceSelected?.Invoke(coordinate, evt.ctrlKey || evt.commandKey);
                evt.StopPropagation();
                return;
            }
            if (Brush == LevelBrush.Select && PlacementSelected != null)
            {
                PlacementSelected.Invoke(coordinate, evt.ctrlKey || evt.commandKey);
                evt.StopPropagation();
                return;
            }
            Selected?.Invoke(coordinate);
            if (Brush == LevelBrush.Select || !LevelBoardEditing.CanEdit(level))
                return;
            dragging = true;
            strokeSource = JsonUtility.ToJson(level);
            pointerId = evt.pointerId;
            visited.Add(coordinate);
            pressed = coordinate;
            hover = coordinate;
            previous = local;
            this.CapturePointer(pointerId);
            Redraw();
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!dragging)
            {
                hover = TryCoordinate(this.WorldToLocal(evt.position), out BoardCoordinate cell) ? cell : null;
                Redraw();
                return;
            }
            if (!dragging || evt.pointerId != pointerId)
                return;
            if (!this.HasPointerCapture(pointerId)) { CancelStroke(); return; }
            if (JsonUtility.ToJson(level) != strokeSource) { CancelStroke(); return; }
            if (Brush == LevelBrush.Move || (Brush == LevelBrush.Placement && Placement.Size == 2))
            {
                hover = TryCoordinate(this.WorldToLocal(evt.position), out BoardCoordinate cell) ? cell : null;
                Redraw();
            }
            else Visit(this.WorldToLocal(evt.position));
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!dragging || evt.pointerId != pointerId || evt.button != 0)
                return;
            if (!this.HasPointerCapture(pointerId)) { CancelStroke(); return; }
            if (JsonUtility.ToJson(level) != strokeSource) { CancelStroke(); return; }
            if (Brush == LevelBrush.Move || (Brush == LevelBrush.Placement && Placement.Size == 2))
            {
                bool click = TryCoordinate(this.WorldToLocal(evt.position), out BoardCoordinate target) && pressed.HasValue && target.Equals(pressed.Value);
                int moving = moveIndex;
                LevelBrush mode = Brush;
                CancelStroke();
                if (click)
                {
                    if (mode == LevelBrush.Move) MoveCommitted?.Invoke(moving, target);
                    else Committed?.Invoke(mode, Color, new[] { target });
                }
                evt.StopPropagation();
                return;
            }
            Visit(this.WorldToLocal(evt.position));
            BoardCoordinate[] completed = new BoardCoordinate[visited.Count];
            visited.CopyTo(completed);
            CancelStroke();
            Committed?.Invoke(Brush, Color, completed);
            evt.StopPropagation();
        }

        private void Visit(Vector2 position)
        {
            // 보드 밖으로 나갔을 때는 경로를 끊는다. 재진입 시 내부를 가로지르는 선을 만들지 않는다.
            if (TryCoordinate(position, out BoardCoordinate current))
                visited.Add(current);
            if (previous.HasValue)
            {
                Vector2 start = previous.Value;
                Vector2 delta = position - start;
                // 격자 경계 통과 시점을 분할해 빠른 이동 중 지나간 모든 칸을 방문한다.
                List<float> cuts = new List<float> { 0, 1 };
                for (int boundary = 0; boundary <= 10; boundary++)
                {
                    float x = boundary * CellSize;
                    if (delta.x != 0)
                    {
                        float t = (x - start.x) / delta.x;
                        if (t > 0 && t < 1) cuts.Add(t);
                    }
                    if (delta.y != 0)
                    {
                        float t = (x - start.y) / delta.y;
                        if (t > 0 && t < 1) cuts.Add(t);
                    }
                }
                cuts.Sort();
                for (int i = 1; i < cuts.Count; i++)
                {
                    Vector2 point = start + delta * ((cuts[i - 1] + cuts[i]) * 0.5f);
                    if (TryCoordinate(point, out BoardCoordinate coordinate))
                        visited.Add(coordinate);
                }
            }
            previous = TryCoordinate(position, out _) ? position : null;
            Redraw();
        }

        private static bool TryCoordinate(Vector2 position, out BoardCoordinate coordinate)
        {
            coordinate = new BoardCoordinate(Mathf.FloorToInt(position.y / CellSize), Mathf.FloorToInt(position.x / CellSize));
            return position.x >= 0 && position.y >= 0 && position.x < CellSize * 10 && position.y < CellSize * 10;
        }

        private void Redraw()
        {
            int selectedObstacle = Layer == PlacementLayer.Obstacle && selected.HasValue
                ? LevelPlacementRules.Find(level, PlacementLayer.Obstacle, selected.Value) : -1;
            HashSet<BoardCoordinate> selectedArea = selectedObstacle >= 0
                ? new HashSet<BoardCoordinate>(LevelPlacementRules.Footprint(level.Obstacles[selectedObstacle].Coordinate, LevelPlacementRules.Size(level.Obstacles[selectedObstacle].Kind)))
                : new HashSet<BoardCoordinate>();
            int previewSize = Brush == LevelBrush.Move ? 2 : Brush == LevelBrush.Placement ? Placement.Size : 1;
            HashSet<BoardCoordinate> previewArea = hover.HasValue && previewSize == 2
                ? new HashSet<BoardCoordinate>(LevelPlacementRules.Footprint(hover.Value, 2)) : new HashSet<BoardCoordinate>();
            string previewError = previewArea.Count == 0 ? null : Brush == LevelBrush.Move && moveIndex >= 0 && moveIndex < (level?.Obstacles?.Count ?? 0)
                ? LevelPlacementRules.ObstacleSpaceError(level, hover.Value, level.Obstacles[moveIndex].Kind, moveIndex)
                : LevelObstacleEditing.PlacementError(level, Placement, hover.Value);
            for (int i = 0; i < cells.Length; i++)
            {
                BoardCoordinate coordinate = new BoardCoordinate(i / 10, i % 10);
                Label cell = cells[i];
                CellDefinition definition = default;
                bool hasCell = level != null && level.Board != null && level.Board.TryGetCell(coordinate, out definition);
                bool active = hasCell && definition.IsActive;
                int index = LevelBoardEditing.FindBlock(level, coordinate);
                bool colored = false;
                Texture2D rabbitTexture = null;
                string text = active ? "·" : "×";
                Color background = active ? new Color32(66, 72, 84, 255) : new Color32(35, 38, 45, 255);
                if (index == -2) text = "!";
                if (index >= 0)
                {
                    InitialBlockDefinition block = level.InitialBlocks[index];
                    if (block.Kind == InitialBlockKind.RandomNormal) text = "?";
                    else if (block.FixedColor.HasValue && (int)block.FixedColor.Value >= 0 && (int)block.FixedColor.Value < 5)
                    {
                        text = ((int)block.FixedColor.Value + 1).ToString();
                        background = Swatches[(int)block.FixedColor.Value];
                        colored = true;
                        rabbitTexture = RabbitBlockArtwork.Get(block.FixedColor.Value);
                    }
                    else text = block.Kind switch
                    {
                        InitialBlockKind.Rocket => block.RocketDirection == RocketDirection.Horizontal ? "↔" : block.RocketDirection == RocketDirection.Vertical ? "↕" : "!",
                        InitialBlockKind.Bomb => "폭", InitialBlockKind.Drone => "드", InitialBlockKind.Magnet => "자", _ => "!"
                    };
                }
                int obstacleIndex = LevelPlacementRules.Find(level, PlacementLayer.Obstacle, coordinate);
                int coverIndex = LevelPlacementRules.Find(level, PlacementLayer.Cover, coordinate);
                int dustIndex = LevelPlacementRules.Find(level, PlacementLayer.Dust, coordinate);
                bool conflict = obstacleIndex == -2 || coverIndex == -2 || dustIndex == -2 ||
                    (obstacleIndex >= 0 && (index != -1 || coverIndex != -1));
                if (obstacleIndex >= 0)
                {
                    ObstaclePlacementDefinition obstacle = level.Obstacles[obstacleIndex];
                    string symbol = obstacle.Kind switch
                    {
                        ObstacleKind.Crate => "상", ObstacleKind.Scrap => "철", ObstacleKind.Safe => "금",
                        ObstacleKind.ColorLock => "잠", ObstacleKind.Appliance => "폐", ObstacleKind.Generator => "발", _ => "!"
                    };
                    text = symbol + (obstacle.Kind == ObstacleKind.Generator ? obstacle.RequiredCharge : obstacle.Durability);
                    background = new Color32(96, 83, 72, 255);
                    colored = false;
                    if (obstacle.Kind == ObstacleKind.ColorLock && (int)obstacle.Color >= 0 && (int)obstacle.Color < 5)
                    {
                        background = Swatches[(int)obstacle.Color];
                        colored = true;
                    }
                    if (LevelPlacementRules.Size(obstacle.Kind) == 2) text = "";
                }
                if (coverIndex >= 0)
                {
                    CoverPlacementDefinition cover = level.Covers[coverIndex];
                    text = (cover.Kind == CoverKind.Web ? "줄" : cover.Kind == CoverKind.Mold ? "곰" : "!") + cover.Durability + "\n" + text;
                }
                if (conflict) text += "!";
                if (!hasCell) text = "—";
                bool preview = previewArea.Contains(coordinate) || visited.Contains(coordinate);
                if (LevelSupplyRules.HasRecovery(level, coordinate))
                {
                    conflict |= index != -1 || obstacleIndex != -1 || coverIndex != -1;
                    text = "부";
                    background = new Color32(77, 103, 117, 255);
                }
                bool isSelected = PlacementSelection.Contains(coordinate) || selectedArea.Contains(coordinate) || (selected.HasValue && selected.Value.Equals(coordinate)) ||
                    (Brush == LevelBrush.SourceSelect && SourceSelection.Contains(coordinate));
                Color border = preview ? previewError != null ? new Color32(255, 78, 78, 255) : new Color32(255, 225, 90, 255) :
                    isSelected ? UnityEngine.Color.white : errors.Contains(coordinate) || index == -2 || conflict ? new Color32(255, 78, 78, 255) : new Color32(24, 27, 32, 255);
                // 본체 그림은 정상적인 일반 블록에만 표시한다. 곰팡이·장애물·회수 부품이나
                // 잘못된 중복 배치에 토끼가 비치지 않도록 표시 가능 여부를 매번 새로 계산한다.
                bool showRabbit = rabbitTexture != null && active && obstacleIndex == -1 && !conflict &&
                    !LevelSupplyRules.HasRecovery(level, coordinate) &&
                    (coverIndex == -1 || (coverIndex >= 0 && level.Covers[coverIndex].Kind == CoverKind.Web));
                cell.style.backgroundImage = showRabbit ? new StyleBackground(rabbitTexture) : new StyleBackground(StyleKeyword.None);
                cell.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                cell.style.unityTextAlign = showRabbit ? TextAnchor.UpperLeft : TextAnchor.MiddleCenter;
                cell.style.unityTextOutlineWidth = showRabbit ? 1 : 0;
                cell.style.unityTextOutlineColor = (Color)new Color32(24, 27, 32, 255);
                if (showRabbit)
                {
                    background = new Color32(66, 72, 84, 255);
                    colored = false;
                    // 토끼의 번호 대신 이미지를 쓰되 거미줄 내구도와 오류 표시는 남긴다.
                    text = coverIndex >= 0 ? "줄" + level.Covers[coverIndex].Durability : "";
                }
                cell.text = text + (errors.Contains(coordinate) && index != -2 ? "!" : "");
                Label supplyMark = supplyMarks.Q<Label>("supply-mark-" + i);
                if (supplyMark == null)
                {
                    supplyMark = new Label("생") { name = "supply-mark-" + i, pickingMode = PickingMode.Ignore };
                    supplyMark.style.position = Position.Absolute;
                    supplyMark.style.left = coordinate.Column * CellSize + 1; supplyMark.style.top = coordinate.Row * CellSize;
                    supplyMark.style.fontSize = 9;
                    supplyMark.style.color = (Color)new Color32(135, 234, 206, 255);
                    supplyMarks.Add(supplyMark);
                }
                supplyMark.style.display = LevelSupplyRules.FindSource(level, coordinate) != -1 ? DisplayStyle.Flex : DisplayStyle.None;
                cell.style.fontSize = coverIndex >= 0 ? 11 : 18;
                cell.tooltip = coordinate + (active ? " 활성" : " 비활성") + (index == -2 ? " / 중복 배치" : "") +
                    (obstacleIndex >= 0 ? " / " + LevelPlacementRules.Name(level.Obstacles[obstacleIndex].Kind) + " 기준 " + level.Obstacles[obstacleIndex].Coordinate : "") +
                    (coverIndex >= 0 ? " / 덮개 아래 블록 보존" : "") +
                    (dustIndex >= 0 ? " / 먼지 " + level.Dust[dustIndex].Durability : "") + (preview && previewError != null ? " / " + previewError : "");
                cell.style.backgroundColor = background;
                cell.style.opacity = active || (index < 0 && obstacleIndex < 0 && coverIndex < 0 && dustIndex < 0) ? 1 : 0.55f;
                cell.style.color = colored
                    ? (Color)new Color32(22, 28, 40, 255) : UnityEngine.Color.white;
                cell.style.borderLeftColor = cell.style.borderRightColor = border;
                cell.style.borderTopColor = cell.style.borderBottomColor = border;
                float width = preview || isSelected || errors.Contains(coordinate) ? 3 : 1;
                cell.style.borderLeftWidth = cell.style.borderRightWidth = width;
                cell.style.borderTopWidth = cell.style.borderBottomWidth = width;
                VisualElement coverOutline = cell.Q<VisualElement>("cover-outline");
                if (coverOutline == null)
                {
                    coverOutline = new VisualElement { name = "cover-outline", pickingMode = PickingMode.Ignore };
                    coverOutline.style.position = Position.Absolute;
                    coverOutline.style.left = coverOutline.style.right = coverOutline.style.top = coverOutline.style.bottom = 1;
                    coverOutline.style.borderLeftWidth = coverOutline.style.borderRightWidth = coverOutline.style.borderTopWidth = coverOutline.style.borderBottomWidth = 1;
                    cell.Add(coverOutline);
                }
                coverOutline.style.display = coverIndex >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
                Color coverColor = coverIndex >= 0 && level.Covers[coverIndex].Kind == CoverKind.Mold
                    ? new Color32(189, 153, 224, 255) : new Color32(230, 235, 245, 255);
                coverOutline.style.borderLeftColor = coverOutline.style.borderRightColor = coverOutline.style.borderTopColor = coverOutline.style.borderBottomColor = coverColor;
                if (obstacleIndex >= 0 && LevelPlacementRules.Size(level.Obstacles[obstacleIndex].Kind) == 2 && !preview && !errors.Contains(coordinate))
                {
                    BoardCoordinate origin = level.Obstacles[obstacleIndex].Coordinate;
                    cell.style.borderLeftWidth = coordinate.Column == origin.Column ? 3 : 0;
                    cell.style.borderRightWidth = coordinate.Column == origin.Column + 1 ? 3 : 0;
                    cell.style.borderTopWidth = coordinate.Row == origin.Row ? 3 : 0;
                    cell.style.borderBottomWidth = coordinate.Row == origin.Row + 1 ? 3 : 0;
                }
                Label dustLabel = cell.Q<Label>("dust-mark");
                if (dustLabel == null)
                {
                    dustLabel = new Label { name = "dust-mark", pickingMode = PickingMode.Ignore };
                    dustLabel.style.position = Position.Absolute;
                    dustLabel.style.bottom = 0;
                    dustLabel.style.right = 1;
                    dustLabel.style.width = 9;
                    dustLabel.style.height = 10;
                    dustLabel.style.fontSize = 8;
                    dustLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    dustLabel.style.color = UnityEngine.Color.white;
                    dustLabel.style.backgroundColor = (Color)new Color32(89, 69, 41, 255);
                    cell.Add(dustLabel);
                }
                dustLabel.text = dustIndex >= 0 ? level.Dust[dustIndex].Durability.ToString() : "";
                dustLabel.style.display = dustIndex >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            bodies.Clear();
            if (level?.Obstacles == null) return;
            for (int index = 0; index < level.Obstacles.Count; index++)
            {
                ObstaclePlacementDefinition obstacle = level.Obstacles[index];
                if (LevelPlacementRules.Size(obstacle.Kind) != 2 || obstacle.Coordinate.Row < 0 || obstacle.Coordinate.Column < 0 ||
                    obstacle.Coordinate.Row > 8 || obstacle.Coordinate.Column > 8) continue;
                Label label = new Label((obstacle.Kind == ObstacleKind.Generator ? "고장 난\n발전기" : LevelPlacementRules.Name(obstacle.Kind)) + "\n" +
                    (obstacle.Kind == ObstacleKind.Generator ? "충전 " + obstacle.RequiredCharge : "내구 " + obstacle.Durability))
                    { pickingMode = PickingMode.Ignore };
                label.style.position = Position.Absolute;
                label.style.left = obstacle.Coordinate.Column * CellSize + 5;
                label.style.top = obstacle.Coordinate.Row * CellSize + 8;
                label.style.width = CellSize * 2 - 10;
                label.style.height = CellSize * 2 - 16;
                label.style.fontSize = 12;
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.color = UnityEngine.Color.white;
                label.style.opacity = LevelPlacementRules.Footprint(obstacle.Coordinate, 2).All(cell =>
                    level.Board != null && level.Board.TryGetCell(cell, out CellDefinition definition) && definition.IsActive) ? 1 : 0.55f;
                bodies.Add(label);
            }
        }
    }
}
