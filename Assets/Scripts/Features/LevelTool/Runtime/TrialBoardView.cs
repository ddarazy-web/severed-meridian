#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Elements;
using GameScreen;
using Levels;
using Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    // 편집 보드와 분리한 읽기 전용 실행 상태 뷰. 시험 사본의 시각 카탈로그만 사용한다.
    internal sealed class TrialBoardView : IDisposable
    {
        private const float Side = 36;
        internal VisualElement Root { get; }
        private readonly VisualElement grid, images;
        private readonly Label notice;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly Dictionary<BoardCoordinate, Label> cells = new Dictionary<BoardCoordinate, Label>();
        private List<(BoardCoordinate cell, int size, ElementVisualFrame frame)> frames = new List<(BoardCoordinate, int, ElementVisualFrame)>();
        private LevelRuntimeState lastState;
        private LevelDefinition lastDefinition;
        private PuzzleArtwork artwork;
        private bool disposed, loading;
        private int revision, rows, columns;

        internal TrialBoardView(string name, string title)
        {
            Root = new Foldout { name = name, text = title + " · 읽기 전용", value = true };
            Root.style.display = DisplayStyle.None;
            notice = new Label("시험 상태 준비 중") { style = { whiteSpace = WhiteSpace.Normal } }; Root.Add(notice);
            var scroll = new ScrollView(ScrollViewMode.Horizontal); Root.Add(scroll);
            grid = new VisualElement { style = { position = Position.Relative } }; scroll.Add(grid);
            images = new VisualElement { pickingMode = PickingMode.Ignore, style = { position = Position.Absolute, left = 0, top = 0, right = 0, bottom = 0 } };
            Root.RegisterCallback<DetachFromPanelEvent>(_ => Dispose());
        }

        internal void Show(LevelRuntimeState state, LevelDefinition definition)
        {
            if (disposed || state == lastState && definition == lastDefinition) return;
            lastState = state; revision++;
            if (definition != lastDefinition)
            {
                artwork?.Dispose(); artwork = definition == null ? null : new PuzzleArtwork(ElementVisualLookup.ForLevel(definition));
                lastDefinition = definition;
            }
            images.Clear(); frames.Clear();
            if (state == null) { Root.style.display = DisplayStyle.None; notice.text = "시험 상태 준비 중"; return; }
            Root.style.display = DisplayStyle.Flex;
            grid.style.display = DisplayStyle.Flex;
            if (rows != state.Rows || columns != state.Columns)
            {
                rows = state.Rows; columns = state.Columns; grid.Clear(); cells.Clear();
                grid.style.width = columns * Side; grid.style.height = rows * Side;
                for (int row = 0; row < rows; row++)
                    for (int column = 0; column < columns; column++)
                    {
                        var coordinate = new BoardCoordinate(row, column);
                        var tile = new Label { pickingMode = PickingMode.Ignore };
                        tile.AddToClassList("trial-cell");
                        tile.style.position = Position.Absolute; tile.style.left = column * Side; tile.style.top = row * Side;
                        tile.style.width = Side - 1; tile.style.height = Side - 1; tile.style.fontSize = 10;
                        tile.style.unityTextAlign = TextAnchor.MiddleCenter; grid.Add(tile); cells.Add(coordinate, tile);
                    }
                grid.Add(images);
            }
            notice.text = "남은 이동 " + state.MovesRemaining;
            try
            {
                foreach (var pair in cells)
                {
                    RuntimeCell cell = state.CellAt(pair.Key);
                    pair.Value.text = cell.IsActive ? CellText(cell) : "";
                    pair.Value.tooltip = pair.Key + " · " + pair.Value.text + " · 덮개 " + cell.CoverDurability + " · 바닥 " + cell.DustDurability;
                    pair.Value.style.backgroundColor = cell.IsActive ? new Color(.20f, .24f, .30f) : new Color(.08f, .10f, .13f);
                    if (!cell.IsActive || artwork == null) continue;
                    Add(pair.Key, 1, artwork.Visuals.Dust(cell));
                    if (cell.Content == RuntimeContent.Obstacle && cell.ObstacleIndex.HasValue)
                    {
                        var obstacle = state.Obstacles[cell.ObstacleIndex.Value];
                        if (pair.Key.Equals(obstacle.Definition.Coordinate)) Add(pair.Key, ElementVisualLookup.Size(obstacle.Element), artwork.Visuals.Obstacle(obstacle));
                    }
                    else Add(pair.Key, 1, artwork.Visuals.Content(cell));
                    Add(pair.Key, 1, artwork.Visuals.Cover(cell));
                }
                if (!loading && artwork != null) LoadImages().Forget();
            }
            catch (Exception error) { notice.text += "\n이미지 정의 오류: " + error.Message; }
        }

        private void Add(BoardCoordinate cell, int size, ElementVisualFrame frame)
        { if (frame != null) frames.Add((cell, size, frame)); }

        private async UniTask LoadImages()
        {
            loading = true;
            try
            {
                // 이전 상태를 로드하는 동안 바뀐 보드는 최신 요청만 다시 그린다.
                while (!disposed && artwork != null)
                {
                    int version = revision; PuzzleArtwork owner = artwork;
                    var pending = frames.OrderBy(item => item.frame.Order).ToArray();
                    try { await owner.PrepareEffectsAsync(pending.Select(item => item.frame.Path), lifetime.Token); }
                    catch (OperationCanceledException) when (disposed || owner != artwork) { if (disposed) return; continue; }
                    if (disposed) return;
                    if (version != revision || owner != artwork) continue;
                    images.Clear();
                    foreach (var item in pending)
                    {
                        Sprite sprite = owner.GetVisual(item.frame); if (sprite == null) continue;
                        float side = Side * item.frame.Size;
                        float width = side * sprite.rect.width / Mathf.Max(sprite.rect.width, sprite.rect.height);
                        float height = side * sprite.rect.height / Mathf.Max(sprite.rect.width, sprite.rect.height);
                        var image = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                        image.style.position = Position.Absolute;
                        image.style.left = (item.cell.Column + item.size / 2f) * Side + item.frame.Offset.x * side - width * item.frame.Pivot.x;
                        image.style.top = (item.cell.Row + item.size / 2f) * Side - item.frame.Offset.y * side - height * (1 - item.frame.Pivot.y);
                        image.style.width = width; image.style.height = height; image.style.rotate = new Rotate(new Angle(-item.frame.Angle));
                        images.Add(image);
                        for (int row = 0; row < item.size; row++)
                            for (int column = 0; column < item.size; column++)
                                if (cells.TryGetValue(new BoardCoordinate(item.cell.Row + row, item.cell.Column + column), out var tile)) tile.text = "";
                    }
                    return;
                }
            }
            catch (Exception error) { if (!disposed) notice.text += "\n이미지 로드 실패: " + error.Message; }
            finally { loading = false; }
        }

        private static string CellText(RuntimeCell cell) => cell.Content switch {
            RuntimeContent.Normal => "토" + ((int?)cell.Color + 1), RuntimeContent.Rocket => cell.RocketDirection == RocketDirection.Horizontal ? "로↔" : "로↕",
            RuntimeContent.Bomb => "폭탄", RuntimeContent.Drone => "드론", RuntimeContent.Magnet => "자석", RuntimeContent.Recovery => "회수",
            RuntimeContent.Obstacle => "장애물", _ => "" };

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; revision++; lifetime.Cancel(); lifetime.Dispose(); artwork?.Dispose(); artwork = null;
        }
    }
}
#endif
