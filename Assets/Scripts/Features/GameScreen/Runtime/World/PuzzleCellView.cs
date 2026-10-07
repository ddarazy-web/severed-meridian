using Board;
using UnityEngine;
using Elements;

namespace GameScreen
{
    public sealed class PuzzleCellView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer floor;
        [SerializeField] private SpriteRenderer dust;
        [SerializeField] private SpriteRenderer content;
        [SerializeField] private SpriteRenderer cover;
        public SpriteRenderer ContentRenderer => content;
        internal int HighestSortingOrder => Mathf.Max(Mathf.Max(floor.sortingOrder, dust.sortingOrder), Mathf.Max(content.sortingOrder, cover.sortingOrder));

        public void Configure(SpriteRenderer floorRenderer, SpriteRenderer dustRenderer, SpriteRenderer contentRenderer, SpriteRenderer coverRenderer)
        { floor = floorRenderer; dust = dustRenderer; content = contentRenderer; cover = coverRenderer; }

        public void Draw(Sprite floorSprite, Sprite dustSprite, Sprite contentSprite, Sprite coverSprite)
        {
            Set(floor, floorSprite, 1); Set(dust, dustSprite, 1);
            float contentSize = 0.92f * BoardArtworkLayout.ContentScale(contentSprite);
            Set(content, contentSprite, contentSize); Set(cover, coverSprite, 1);
            content.transform.localPosition = new Vector3(BoardArtworkLayout.ContentOffsetX(contentSprite), BoardArtworkLayout.ContentOffsetY(contentSprite), 0) * contentSize;
        }

        public void Draw(PuzzleArtwork artwork, ElementVisualFrame dustFrame, ElementVisualFrame contentFrame, ElementVisualFrame coverFrame)
        {
            SetVisual(floor, artwork.Get(PuzzleArtworkPaths.Floor), null);
            SetVisual(dust, artwork.GetVisual(dustFrame), dustFrame);
            SetVisual(content, artwork.GetVisual(contentFrame), contentFrame, .92f);
            SetVisual(cover, artwork.GetVisual(coverFrame), coverFrame);
        }

        internal void Clear()
        {
            ResetImage(floor); ResetImage(dust); ResetImage(content); ResetImage(cover);
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
        }

        internal static void ResetImage(SpriteRenderer renderer)
        {
            renderer.sprite = null;
            renderer.enabled = false;
            renderer.color = Color.white;
            renderer.flipX = renderer.flipY = false;
            renderer.maskInteraction = SpriteMaskInteraction.None;
            renderer.sortingOrder = 0;
            renderer.transform.localPosition = Vector3.zero;
            renderer.transform.localScale = Vector3.one;
            renderer.transform.localRotation = Quaternion.identity;
        }

        internal static void SetVisual(SpriteRenderer renderer, Sprite sprite, ElementVisualFrame frame, float multiplier = 1)
        {
            float size = (frame?.Size ?? 1) * multiplier;
            Set(renderer, sprite, size);
            renderer.color = Color.white;
            renderer.flipX = renderer.flipY = false;
            renderer.maskInteraction = SpriteMaskInteraction.None;
            renderer.sortingOrder = frame?.Order ?? 0;
            renderer.transform.localRotation = Quaternion.Euler(0, 0, frame?.Angle ?? 0);
            Vector2 offset = (frame?.Offset ?? Vector2.zero) * size;
            if (sprite != null && frame != null)
            {
                Vector2 nativePivot = sprite.pivot / sprite.rect.size;
                Vector2 bounds = (Vector2)sprite.bounds.size * (size / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
                offset += Vector2.Scale(nativePivot - frame.Pivot, bounds);
            }
            renderer.transform.localPosition = new Vector3(offset.x, offset.y, 0);
        }

        public static void Set(SpriteRenderer renderer, Sprite sprite, float size)
        {
            renderer.sprite = sprite;
            renderer.enabled = sprite != null;
            renderer.transform.localScale = sprite == null ? Vector3.one : Vector3.one * (size / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
        }
    }
}
