using System;

namespace Elements
{
    /// <summary>배치 크기와 최대 내구도만 보유하는 불변 수치.</summary>
    public sealed class ElementPlacementProfile
    {
        public int Size { get; }
        public int MaxDurability { get; }

        /// <param name="size">양수인 정사각 점유 크기.</param><param name="maxDurability">양수인 최대 내구도.</param>
        public ElementPlacementProfile(int size, int maxDurability)
        {
            if (size < 1) throw new ArgumentOutOfRangeException(nameof(size), size, "배치 크기는 양수여야 합니다.");
            if (maxDurability < 1) throw new ArgumentOutOfRangeException(nameof(maxDurability), maxDurability, "최대 내구도는 양수여야 합니다.");
            Size = size;
            MaxDurability = maxDurability;
        }
    }
}
