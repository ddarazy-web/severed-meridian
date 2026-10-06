using System;

namespace Elements
{
    public enum ElementTurnBehavior { AdjacentCoverSpread = 1 }

    /// <summary>턴 종료 시 생성할 덮개의 초기 상태와 등록 행동.</summary>
    public sealed class ElementTurnProfile
    {
        public ElementTurnBehavior Behavior { get; }
        public int InitialDurability { get; }
        public ElementTurnProfile(ElementTurnBehavior behavior, int initialDurability)
        {
            if (!Enum.IsDefined(typeof(ElementTurnBehavior), behavior)) throw new ArgumentOutOfRangeException(nameof(behavior));
            if (initialDurability < 1) throw new ArgumentOutOfRangeException(nameof(initialDurability));
            Behavior = behavior; InitialDurability = initialDurability;
        }
    }
}
