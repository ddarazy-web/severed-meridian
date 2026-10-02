using Levels;

namespace Simulation
{
    // 덮개 손상은 내용물 소비와 별개다. 벗기는 타격은 안의 블록까지 실행하지 않는다.
    internal static class WebRules
    {
        internal static DamageReaction Query(RuntimeCell cell, TurnEffectContext context)
            => context?.HasDamagedWeb(cell.Coordinate) == true ?
                new DamageReaction(DamageResponse.AlreadyDamaged, "거미줄 턴 피해 완료 · 내용물 보존") :
                new DamageReaction(DamageResponse.CoverDamage, "거미줄 1피해 · 내용물 보존", 1);

        internal static void Apply(LevelRuntimeState state, RuntimeCell cell, TurnEffectContext context)
        {
            if (cell.Cover != CoverKind.Web || context.HasDamagedWeb(cell.Coordinate)) return;
            context.RegisterWeb(cell.Coordinate);
            if (--cell.CoverDurability == 0)
            { cell.Cover = null; MissionProgressRules.Complete(state, MissionKind.Web, cell.Coordinate); }
        }
    }

    // 실제 일반 블록 소비/변환에서만 호출한다. 바닥에 파워가 닿았다는 이유로 호출하지 않는다.
    internal static class DustRules
    {
        internal static bool CanDamage(RuntimeCell cell, TurnEffectContext context)
            => cell.DustDurability > 0 && context?.HasDamagedDust(cell.Coordinate) != true;

        internal static void ConsumeNormal(LevelRuntimeState state, RuntimeCell cell, TurnEffectContext context)
        {
            if (!CanDamage(cell, context)) return;
            context.RegisterDust(cell.Coordinate);
            if (--cell.DustDurability == 0) MissionProgressRules.Complete(state, MissionKind.Dust, cell.Coordinate);
        }
    }
}
