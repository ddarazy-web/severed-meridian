using UnityEngine;

namespace GameScreen
{
    public sealed class PuzzleBackdropView : MonoBehaviour
    {
        [SerializeField] private Camera backdropCamera;
        [SerializeField] private SpriteRenderer scenery;
        [SerializeField] private Sprite landscape, portrait;
        public void Configure(Camera camera, SpriteRenderer renderer, Sprite wide, Sprite tall)
        { backdropCamera = camera; scenery = renderer; landscape = wide; portrait = tall; }
        private void LateUpdate()
        {
            Sprite sprite = Screen.height > Screen.width ? portrait : landscape;
            if (sprite == null) return;
            scenery.sprite = sprite;
            float height = backdropCamera.orthographicSize * 2;
            scenery.transform.localScale = new Vector3(height * backdropCamera.aspect / sprite.bounds.size.x, height / sprite.bounds.size.y, 1);
        }
    }
}
