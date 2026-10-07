using UnityEngine;

namespace Tutorial
{
    /// <summary>별도 이미지 없이 검지를 편 손 안내를 그린다.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TutorialFingerGraphic : UnityEngine.UI.MaskableGraphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            Vector2[] shape = { new Vector2(-.12f,-.5f), new Vector2(.30f,-.5f), new Vector2(.46f,-.03f),
                new Vector2(.42f,.16f), new Vector2(.12f,.22f), new Vector2(.10f,.62f), new Vector2(-.06f,.68f),
                new Vector2(-.22f,.60f), new Vector2(-.22f,.05f), new Vector2(-.38f,.16f), new Vector2(-.50f,.04f), new Vector2(-.36f,-.20f) };
            Rect rect = rectTransform.rect;
            mesh.AddVert(rect.center, color, Vector2.zero);
            for (int i = 0; i < shape.Length; i++)
                mesh.AddVert(rect.center + Vector2.Scale(shape[i], rect.size), color, Vector2.zero);
            for (int i = 0; i < shape.Length; i++) mesh.AddTriangle(0, i + 1, (i + 1) % shape.Length + 1);
        }
    }
}
