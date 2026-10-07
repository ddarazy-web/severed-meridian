using System.Linq;
using Elements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    // 편집·시험 보드의 UI 좌표로 같은 시각 프레임을 옮긴다. y/회전만 화면 좌표로 반전한다.
    internal static class ElementVisualStyle
    {
        internal static void Apply(VisualElement image, Sprite sprite, ElementVisualFrame frame, float scale)
        {
            Vector2 nativePivot = sprite != null ? sprite.pivot / sprite.rect.size : Vector2.one * .5f;
            Vector2 aspect = sprite != null ? (Vector2)sprite.bounds.size / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) : Vector2.one;
            Vector2 shift = (frame?.Offset ?? Vector2.zero) + Vector2.Scale(nativePivot - (frame?.Pivot ?? nativePivot), aspect);
            image.style.scale = new Scale(Vector3.one * scale);
            image.style.translate = new Translate(Length.Percent(100 * scale * shift.x), Length.Percent(-100 * scale * shift.y));
            image.style.rotate = new Rotate(new Angle(-(frame?.Angle ?? 0), AngleUnit.Degree));
            image.style.transformOrigin = new TransformOrigin(Length.Percent(50 + 100 * aspect.x * (nativePivot.x - .5f)),
                Length.Percent(50 - 100 * aspect.y * (nativePivot.y - .5f)));
            image.userData = frame;
            if (image.parent == null) return;
            foreach (VisualElement layer in image.parent.Children().Where(child => child.userData is ElementVisualFrame)
                .OrderBy(child => ((ElementVisualFrame)child.userData).Order).ToArray()) layer.BringToFront();
        }
    }
}
