#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using UnityEngine;

namespace LevelTool
{
    // 창 장식은 제외하고 편집 영역을 최소 1280×800, 16:10으로 맞춘다.
    internal static class LevelToolWindowGeometry
    {
        public static RectInt Resize(RectInt rect, int edge, int frameWidth, int frameHeight, RectInt previous)
        {
            bool vertical = edge == 3 || edge == 6;
            if (edge == 4 || edge == 5 || edge == 7 || edge == 8)
                vertical = Math.Abs(rect.height - previous.height) * 8L > Math.Abs(rect.width - previous.width) * 5L;
            int units = Math.Max(160, (int)Math.Round(vertical ? (rect.height - frameHeight) / 5d : (rect.width - frameWidth) / 8d));
            int width = units * 8 + frameWidth, height = units * 5 + frameHeight;
            bool left = edge == 1 || edge == 4 || edge == 7;
            bool top = edge == 3 || edge == 4 || edge == 5;
            return new RectInt(left ? rect.xMax - width : rect.x, top ? rect.yMax - height : rect.y, width, height);
        }

        public static RectInt? Fit(RectInt rect, RectInt workArea, int frameWidth, int frameHeight)
        {
            int maximum = Math.Min((workArea.width - frameWidth) / 8, (workArea.height - frameHeight) / 5);
            if (maximum < 160) return null;
            RectInt normalized = Resize(rect, 2, frameWidth, frameHeight, rect);
            int units = Math.Min(maximum, (normalized.width - frameWidth) / 8);
            int width = units * 8 + frameWidth, height = units * 5 + frameHeight;
            return new RectInt(Math.Max(workArea.x, Math.Min(rect.x, workArea.xMax - width)),
                Math.Max(workArea.y, Math.Min(rect.y, workArea.yMax - height)), width, height);
        }
    }
}
#endif
