using Board;
using Simulation;
using UnityEngine;
using UnityEngine.UIElements;
using Elements;

namespace Levels.Editor
{
    // 플레이·초기 후보·사례 재생에서 같은 실행 상태를 같은 그림으로 표시한다.
    internal static class RuntimeBoardArtwork
    {
        internal static void Bind(TextElement target, RuntimeCell cell, LevelRuntimeState state)
            => Bind(target, cell, state, LegacyElementVisuals.Catalog);

        internal static void Bind(TextElement target, RuntimeCell cell, LevelRuntimeState state, ElementVisualCatalog catalog)
        {
            ElementVisualLookup lookup = new ElementVisualLookup(catalog);
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
                content.style.width = content.style.height = Length.Percent(100);
                content.style.left = content.style.top = 0;
                Sprite sprite = null;
                string token = null;
                ElementVisualFrame frame = null;
                if (cell.Cover != CoverKind.Mold)
                {
                    switch (cell.Content)
                    {
                        case RuntimeContent.Normal:
                            if (cell.Color.HasValue) { frame = lookup.Content(cell); token = "토" + ((int)cell.Color.Value + 1); }
                            break;
                        case RuntimeContent.Rocket:
                            frame = lookup.Content(cell);
                            token = cell.RocketDirection == RocketDirection.Horizontal ? "로↔" : "로↕";
                            break;
                        case RuntimeContent.Bomb: frame = lookup.Content(cell); token = "폭탄"; break;
                        case RuntimeContent.Drone: frame = lookup.Content(cell); token = "드론"; break;
                        case RuntimeContent.Magnet: frame = lookup.Content(cell); token = "자석"; break;
                        case RuntimeContent.Recovery: frame = lookup.Content(cell); token = "회수"; break;
                        case RuntimeContent.Obstacle:
                            RuntimeObstacle body = state.Obstacles[cell.ObstacleIndex.Value];
                            frame = lookup.Obstacle(body);
                            int size = ElementVisualLookup.Size(body.Element);
                            if (size > 1 && frame != null)
                            {
                                float inset = (size - frame.Size) * 50;
                                content.style.width = content.style.height = Length.Percent(frame.Size * 100);
                                content.style.left = Length.Percent(inset - 100 * (cell.Coordinate.Column - body.Definition.Coordinate.Column));
                                content.style.top = Length.Percent(inset - 100 * (cell.Coordinate.Row - body.Definition.Coordinate.Row));
                            }
                            break;
                    }
                }
                sprite = LevelBoardArtwork.Visual(frame);
                content.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
                bool extended = cell.Content == RuntimeContent.Obstacle && cell.ObstacleIndex.HasValue &&
                    ElementVisualLookup.Size(state.Obstacles[cell.ObstacleIndex.Value].Element) > 1;
                ElementVisualStyle.Apply(content, sprite, frame, extended ? 1 : frame?.Size ?? 1);
                ElementVisualFrame dustFrame = lookup.Dust(cell);
                Sprite dustSprite = LevelBoardArtwork.Visual(dustFrame);
                dust.style.backgroundImage = dustSprite != null ? new StyleBackground(dustSprite) : new StyleBackground(StyleKeyword.None);
                ElementVisualStyle.Apply(dust, dustSprite, dustFrame, dustFrame?.Size ?? 1);
                ElementVisualFrame coverFrame = lookup.Cover(cell);
                Sprite coverSprite = LevelBoardArtwork.Visual(coverFrame);
                cover.style.backgroundImage = coverSprite != null ? new StyleBackground(coverSprite) : new StyleBackground(StyleKeyword.None);
                ElementVisualStyle.Apply(cover, coverSprite, coverFrame, coverFrame?.Size ?? 1);
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
