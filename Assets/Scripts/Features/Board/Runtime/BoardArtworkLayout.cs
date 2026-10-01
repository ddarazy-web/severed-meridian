using System;
using UnityEngine;

namespace Board
{
    public static class BoardArtworkLayout
    {
        // 원본의 투명 여백을 보정하되 2×2 점유 영역 안에 본체 여백을 남긴다.
        public const float LargeObstacleSize = 2.16f;

        // 가로 본체 185×129px을 세로 본체 152×200px과 비슷한 면적으로 표시한다.
        public static float ContentScale(Sprite sprite) => IsHorizontalRocket(sprite) ? 1.12f : 1;

        public static float ContentOffsetX(Sprite sprite) => IsHorizontalRocket(sprite) ? -28.5f / 256 : 0;

        // 가로 로켓의 본체 중심은 256px 원본 중앙보다 약 28px 아래에 있다.
        public static float ContentOffsetY(Sprite sprite)
            => IsHorizontalRocket(sprite) ? 28f / 256 : 0;

        private static bool IsHorizontalRocket(Sprite sprite)
            => sprite != null && sprite.name.StartsWith("cleaning-rocket-horizontal-v1", StringComparison.Ordinal);
    }
}
