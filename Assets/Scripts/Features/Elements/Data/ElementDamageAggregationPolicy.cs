using Board;
using Simulation;

namespace Elements
{
    /// <summary>기존 본체·칸별 턴 기록을 변경하지 않고 읽는 불변 집계 조건.</summary>
    public sealed class ElementDamageAggregationPolicy
    {
        public bool PerHitCell { get; }

        public ElementDamageAggregationPolicy(bool perHitCell) { PerHitCell = perHitCell; }

        public bool AlreadyApplied(TurnEffectContext context, int bodyIndex, BoardCoordinate cell, int hit) =>
            PerHitCell ? context?.HasHit(hit, cell) == true : context?.HasDamaged(bodyIndex) == true;
    }
}
