using UnityEngine;

namespace Lobby
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class LobbySafeArea : MonoBehaviour
    {
        private Rect lastArea;
        private Vector2Int lastSize;

        private void LateUpdate()
        {
            Rect area = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size.x <= 0 || size.y <= 0 || (lastArea == area && lastSize == size)) return;
            lastArea = area;
            lastSize = size;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
            rect.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
        }
    }
}
