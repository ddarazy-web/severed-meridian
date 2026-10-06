using System;

namespace Elements
{
    /// <summary>내구도와 독립된 충전형 요소의 배치 크기·충전 허용 범위.</summary>
    public sealed class ElementChargePlacementProfile
    {
        public int Size { get; }
        public int MinRequiredCharge { get; }
        public int MaxRequiredCharge { get; }
        public int ChargePerHit { get; }

        /// <param name="size">양수인 정사각 점유 크기.</param><param name="minRequiredCharge">양수인 최소 필요 충전량.</param>
        /// <param name="maxRequiredCharge">최소값 이상인 최대 필요 충전량.</param>
        public ElementChargePlacementProfile(int size, int minRequiredCharge, int maxRequiredCharge)
            : this(size, minRequiredCharge, maxRequiredCharge, 1) { }

        public ElementChargePlacementProfile(int size, int minRequiredCharge, int maxRequiredCharge, int chargePerHit)
        {
            if (size < 1) throw new ArgumentOutOfRangeException(nameof(size), size, "배치 크기는 양수여야 합니다.");
            if (minRequiredCharge < 1) throw new ArgumentOutOfRangeException(nameof(minRequiredCharge), minRequiredCharge, "최소 필요 충전량은 양수여야 합니다.");
            if (maxRequiredCharge < minRequiredCharge) throw new ArgumentOutOfRangeException(nameof(maxRequiredCharge), maxRequiredCharge, "최대 필요 충전량은 최소값 이상이어야 합니다.");
            if (chargePerHit < 1) throw new ArgumentOutOfRangeException(nameof(chargePerHit), chargePerHit, "충전량은 양수여야 합니다.");
            Size = size;
            MinRequiredCharge = minRequiredCharge;
            MaxRequiredCharge = maxRequiredCharge;
            ChargePerHit = chargePerHit;
        }
    }
}
