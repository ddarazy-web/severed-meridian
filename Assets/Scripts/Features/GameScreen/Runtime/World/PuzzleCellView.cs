using Board;
using UnityEngine;

namespace GameScreen
{
    public sealed class PuzzleCellView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer floor;
        [SerializeField] private SpriteRenderer dust;
        [SerializeField] private SpriteRenderer content;
        [SerializeField] private SpriteRenderer cover;
        public SpriteRenderer ContentRenderer => content;

        public void Configure(SpriteRenderer floorRenderer, SpriteRenderer dustRenderer, SpriteRenderer contentRenderer, SpriteRenderer coverRenderer)
        { floor = floorRenderer; dust = dustRenderer; content = contentRenderer; cover = coverRenderer; }

        public void Draw(Sprite floorSprite, Sprite dustSprite, Sprite contentSprite, Sprite coverSprite)
        {
            Set(floor, floorSprite, 1); Set(dust, dustSprite, 1);
            float contentSize = 0.92f * BoardArtworkLayout.ContentScale(contentSprite);
            Set(content, contentSprite, contentSize); Set(cover, coverSprite, 1);
            content.transform.localPosition = new Vector3(BoardArtworkLayout.ContentOffsetX(contentSprite), BoardArtworkLayout.ContentOffsetY(contentSprite), 0) * contentSize;
        }

        public static void Set(SpriteRenderer renderer, Sprite sprite, float size)
        {
            renderer.sprite = sprite;
            renderer.enabled = sprite != null;
            if (sprite != null) renderer.transform.localScale = Vector3.one * (size / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
        }
    }
}
