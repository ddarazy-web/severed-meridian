using Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    // 플레이·초기 후보·사례 재생에서 같은 실행 상태를 같은 그림으로 표시한다.
    internal static class RuntimeBoardArtwork
    {
        internal static void Bind(TextElement target, RuntimeCell cell, LevelRuntimeState state)
        {
            VisualElement art = new VisualElement { name = "runtime-art", pickingMode = PickingMode.Ignore };
            art.style.position = Position.Absolute;
            art.style.left = art.style.right = art.style.top = art.style.bottom = 0;
            art.style.overflow = Overflow.Hidden;
            target.Insert(0, art);
            VisualElement dust = Layer(art, "runtime-dust");
            VisualElement content = Layer(art, "runtime-content");
            VisualElement cover = Layer(art, "runtime-cover");
            string original = target.text;
            void Refresh()
            {
                if (!cell.IsActive) return;
                Sprite sprite = null;
                string token = null;
                if (cell.Cover != CoverKind.Mold)
                {
                    switch (cell.Content)
                    {
                        case RuntimeContent.Normal:
                            if (cell.Color.HasValue) { sprite = LevelBoardArtwork.Rabbit(cell.Color.Value); token = "토" + ((int)cell.Color.Value + 1); }
                            break;
                        case RuntimeContent.Rocket:
                            sprite = LevelBoardArtwork.Block(InitialBlockKind.Rocket, cell.RocketDirection ?? RocketDirection.Horizontal);
                            token = cell.RocketDirection == RocketDirection.Horizontal ? "로↔" : "로↕";
                            break;
                        case RuntimeContent.Bomb: sprite = LevelBoardArtwork.Block(InitialBlockKind.Bomb, default); token = "폭탄"; break;
                        case RuntimeContent.Drone: sprite = LevelBoardArtwork.Block(InitialBlockKind.Drone, default); token = "드론"; break;
                        case RuntimeContent.Magnet: sprite = LevelBoardArtwork.Block(InitialBlockKind.Magnet, default); token = "자석"; break;
                        case RuntimeContent.Recovery: sprite = LevelBoardArtwork.Recovery; token = "회수"; break;
                        case RuntimeContent.Obstacle:
                            RuntimeObstacle body = state.Obstacles[cell.ObstacleIndex.Value];
                            sprite = LevelBoardArtwork.Obstacle(body.Definition, body.Durability, body.Charge);
                            if (LevelPlacementRules.Size(body.Definition.Kind) == 2)
                            {
                                content.style.width = content.style.height = Length.Percent(200);
                                content.style.left = Length.Percent(-100 * (cell.Coordinate.Column - body.Definition.Coordinate.Column));
                                content.style.top = Length.Percent(-100 * (cell.Coordinate.Row - body.Definition.Coordinate.Row));
                            }
                            break;
                    }
                }
                content.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
                Sprite dustSprite = LevelBoardArtwork.Dust(cell.DustDurability);
                dust.style.backgroundImage = dustSprite != null ? new StyleBackground(dustSprite) : new StyleBackground(StyleKeyword.None);
                Sprite coverSprite = cell.Cover.HasValue ? LevelBoardArtwork.Cover(cell.Cover.Value, cell.CoverDurability) : null;
                cover.style.backgroundImage = coverSprite != null ? new StyleBackground(coverSprite) : new StyleBackground(StyleKeyword.None);
                target.text = sprite != null && token != null ? original.Replace(token, "").Trim() : original;
                target.tooltip = cell.Coordinate + " / " + original.Replace("\n", " / ");
                target.AddToClassList("rabbit-artwork");
            }
            target.RegisterCallback<AttachToPanelEvent>(_ => { LevelBoardArtwork.Loaded += Refresh; LevelBoardArtwork.Acquire(false); Refresh(); });
            target.RegisterCallback<DetachFromPanelEvent>(_ => { LevelBoardArtwork.Loaded -= Refresh; LevelBoardArtwork.Release(); });
            Refresh();
        }

        private static VisualElement Layer(VisualElement parent, string name)
        {
            VisualElement layer = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = layer.style.top = 0;
            layer.style.width = layer.style.height = Length.Percent(100);
            layer.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            parent.Add(layer);
            return layer;
        }
    }
}
