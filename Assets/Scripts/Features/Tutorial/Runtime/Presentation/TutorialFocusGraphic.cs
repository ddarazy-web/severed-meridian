using System.Collections.Generic;
using UnityEngine;

namespace Tutorial
{
    /// <summary>지정 영역을 제외한 화면 전체에 입력을 차단하지 않는 어둠 막을 그린다.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TutorialFocusGraphic : UnityEngine.UI.MaskableGraphic
    {
        private readonly Rect[] holes = new Rect[82];
        private readonly List<Rect> regions = new List<Rect>(256);
        private readonly List<Rect> remaining = new List<Rect>(256);
        private Rect bounds;

        public void SetBounds(Rect screen)
        {
            if (bounds.Equals(screen)) return;
            bounds = screen;
            SetVerticesDirty();
        }

        public void SetHole(int index, Rect hole)
        {
            if (holes[index].Equals(hole)) return;
            holes[index] = hole;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear(); regions.Clear(); regions.Add(bounds);
            // 겹치거나 인접한 강조 영역도 한 번만 뚫어 어둠 막이 중복되지 않게 한다.
            foreach (Rect hole in holes)
            {
                if (hole.width <= 0 || hole.height <= 0) continue;
                remaining.Clear();
                foreach (Rect region in regions)
                {
                    float left = Mathf.Max(region.xMin, hole.xMin), right = Mathf.Min(region.xMax, hole.xMax);
                    float bottom = Mathf.Max(region.yMin, hole.yMin), top = Mathf.Min(region.yMax, hole.yMax);
                    if (left >= right || bottom >= top) { remaining.Add(region); continue; }
                    if (bottom > region.yMin) remaining.Add(Rect.MinMaxRect(region.xMin, region.yMin, region.xMax, bottom));
                    if (top < region.yMax) remaining.Add(Rect.MinMaxRect(region.xMin, top, region.xMax, region.yMax));
                    if (left > region.xMin) remaining.Add(Rect.MinMaxRect(region.xMin, bottom, left, top));
                    if (right < region.xMax) remaining.Add(Rect.MinMaxRect(right, bottom, region.xMax, top));
                }
                regions.Clear(); regions.AddRange(remaining);
            }
            foreach (Rect region in regions)
            {
                int start = mesh.currentVertCount;
                mesh.AddVert(new Vector3(region.xMin, region.yMin), color, Vector2.zero);
                mesh.AddVert(new Vector3(region.xMin, region.yMax), color, Vector2.zero);
                mesh.AddVert(new Vector3(region.xMax, region.yMax), color, Vector2.zero);
                mesh.AddVert(new Vector3(region.xMax, region.yMin), color, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}

