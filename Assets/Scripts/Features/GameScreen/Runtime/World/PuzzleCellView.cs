using UnityEngine;

namespace GameScreen
{
    public sealed class PuzzleCellView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer floor;
        [SerializeField] private SpriteRenderer dust;
        [SerializeField] private SpriteRenderer content;
        [SerializeField] private SpriteRenderer cover;

        public void Configure(SpriteRenderer floorRenderer, SpriteRenderer dustRenderer, SpriteRenderer contentRenderer, SpriteRenderer coverRenderer)
        { floor = floorRenderer; dust = dustRenderer; content = contentRenderer; cover = coverRenderer; }

        public void Draw(Sprite floorSprite, Sprite dustSprite, Sprite contentSprite, Sprite coverSprite)
        {
            Set(floor, floorSprite, 1); Set(dust, dustSprite, 1);
            Set(content, contentSprite, 0.92f); Set(cover, coverSprite, 1);
        }

        public static void Set(SpriteRenderer renderer, Sprite sprite, float size)
        {
            renderer.sprite = sprite;
            renderer.enabled = sprite != null;
            if (sprite != null) renderer.transform.localScale = Vector3.one * (size / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
        }
    }
}
