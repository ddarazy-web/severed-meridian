using UnityEngine;
using UnityEngine.Rendering;

namespace GameScreen
{
    // 프레임 시트는 원본 전체를 아틀라스에 두고 한 사분면만 마스크 안으로 이동한다.
    internal sealed class PuzzleEffectSprite
    {
        private readonly Transform root;
        private readonly SpriteRenderer image;
        private readonly SpriteMask mask;
        private readonly SortingGroup group;
        private readonly Sprite clippingSprite;
        private readonly int imageLayer;
        internal PuzzleEffectSprite(Transform board, SpriteRenderer prefab, Sprite maskSprite)
        {
            group = new GameObject("Power-effect", typeof(SortingGroup)).GetComponent<SortingGroup>();
            root = group.transform; root.SetParent(board, false); group.sortingOrder = 40;
            image = Object.Instantiate(prefab, root); image.name = "Effect-playback"; image.sortingOrder = 0;
            imageLayer = image.sortingLayerID; clippingSprite = maskSprite;
            mask = new GameObject("Frame-mask", typeof(SpriteMask)).GetComponent<SpriteMask>();
            mask.transform.SetParent(root, false); mask.sprite = maskSprite; mask.isCustomRangeActive = false;
            Hide();
        }
        internal void Paint(Sprite sprite, Vector3 position, float size, float angle, bool sheet, int frame, string label)
        {
            root.gameObject.SetActive(true); root.name = label; root.localPosition = position;
            // 궤적/표적 오버레이가 이동하는 본체의 식별 가능한 그림을 덮지 않게 한다.
            group.sortingOrder = label == "Rocket-flight" || label == "Drone-flight" || label == "Drone-hover" ? 45 : 40;
            root.localRotation = Quaternion.Euler(0, 0, angle); root.localScale = Vector3.one;
            image.color = Color.white; image.enabled = true;
            image.sortingLayerID = imageLayer; image.sortingOrder = 0; image.flipX = image.flipY = false;
            image.transform.localRotation = Quaternion.identity;
            PuzzleCellView.Set(image, sprite, size * (sheet ? 2 : 1));
            image.maskInteraction = sheet ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
            mask.gameObject.SetActive(sheet);
            mask.sprite = sheet ? clippingSprite : null;
            mask.isCustomRangeActive = false; mask.alphaCutoff = .5f;
            mask.transform.localPosition = Vector3.zero; mask.transform.localRotation = Quaternion.identity;
            mask.transform.localScale = new Vector3(size, size, 1);
            image.transform.localPosition = sheet ? new Vector3((.5f - frame % 2) * size, (frame / 2 - .5f) * size, 0) : Vector3.zero;
        }
        internal void PaintVisual(Sprite sprite, Elements.ElementVisualFrame frame, Vector3 position,
            float multiplier, float angle, string label)
        {
            Paint(sprite, position, frame.Size * multiplier, angle, false, 0, label);
            PuzzleCellView.SetVisual(image, sprite, frame, multiplier);
            group.sortingOrder = label == "Rocket-flight" || label == "Drone-flight" || label == "Drone-hover"
                ? Mathf.Max(45, frame.Order) : frame.Order;
            image.sortingOrder = 0;
        }
        internal void Hide()
        {
            // 슬롯 반환은 그림의 소유자를 해제하지 않고 이전 표시 참조와 변환만 비운다.
            root.gameObject.SetActive(false); root.name = "Power-effect";
            root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity; root.localScale = Vector3.one;
            group.sortingLayerID = 0; group.sortingOrder = 40;
            image.sprite = null; image.enabled = false; image.color = Color.white;
            image.sortingLayerID = imageLayer; image.sortingOrder = 0; image.flipX = image.flipY = false;
            image.maskInteraction = SpriteMaskInteraction.None;
            image.transform.localPosition = Vector3.zero; image.transform.localRotation = Quaternion.identity;
            image.transform.localScale = Vector3.one;
            mask.gameObject.SetActive(false); mask.sprite = null; mask.isCustomRangeActive = false; mask.alphaCutoff = .5f;
            mask.transform.localPosition = Vector3.zero; mask.transform.localRotation = Quaternion.identity;
            mask.transform.localScale = Vector3.one;
        }
    }
}
