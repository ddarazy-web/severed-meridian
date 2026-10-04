using System.Linq;
using Elements;
using Levels;

namespace Simulation
{
    internal static class ObstacleDamageRules
    {
        internal static bool Supports(ObstacleKind kind) => kind >= ObstacleKind.Crate && kind <= ObstacleKind.Generator;
        internal static int ReservedDamage(RuntimeObstacle body, int cells) =>
            System.Math.Min(body.Durability, body.Definition.Kind == ObstacleKind.Appliance ? cells : System.Math.Min(1, cells));
        internal static MissionKind Mission(ObstacleKind kind) => kind switch
        {
            ObstacleKind.Scrap => MissionKind.Scrap, ObstacleKind.Safe => MissionKind.Safe,
            ObstacleKind.ColorLock => MissionKind.ColorLock, ObstacleKind.Appliance => MissionKind.Appliance, _ => MissionKind.Crate
        };

        internal static DamageReaction Query(LevelRuntimeState state, RuntimeCell cell, DamageCause cause,
            RabbitColor? sourceColor, TurnEffectContext context, int hit)
        {
            int index = cell.ObstacleIndex.Value;
            RuntimeObstacle body = state.Obstacles[index];
            ObstacleKind kind = body.Definition.Kind;
            if (!Supports(kind)) return new DamageReaction(DamageResponse.Unsupported, "장애물 반응 미지원");
            if (kind == ObstacleKind.Generator) return GeneratorRules.Query(state, index, context);
            if (kind == ObstacleKind.Crate || kind == ObstacleKind.Scrap || kind == ObstacleKind.Safe || kind == ObstacleKind.ColorLock || kind == ObstacleKind.Appliance)
            {
                ElementDamageSourcePolicy policy = LegacyElementDefinitions.Get(kind).RequireDamageSourcePolicy();
                // 미정의 원인 값의 기존 조회 의미는 유지하며 네 원인만 정책에 연결한다.
                if (cause >= DamageCause.AdjacentMatch && cause <= DamageCause.Hammer && !policy.Allows(cause))
                    return new DamageReaction(DamageResponse.None, "인접 피해 대상 아님");
            }
            if (cause != DamageCause.Power && cause != DamageCause.Hammer)
            {
                if (kind == ObstacleKind.Safe || (cause == DamageCause.MagnetAdjacent && kind != ObstacleKind.ColorLock))
                    return new DamageReaction(DamageResponse.None, "인접 피해 대상 아님");
                if (kind == ObstacleKind.ColorLock && sourceColor != body.Definition.Color)
                    return new DamageReaction(DamageResponse.None, "자물쇠 지정 색 불일치");
            }
            if (body.Durability <= 0) return new DamageReaction(DamageResponse.None, "제거된 본체");
            if (kind == ObstacleKind.Appliance)
                return context?.HasHit(hit, cell.Coordinate) == true ? new DamageReaction(DamageResponse.AlreadyDamaged, "같은 타격의 같은 폐가전 칸") :
                    new DamageReaction(DamageResponse.Damage, "폐가전 칸 피해 · 별도 타격 반복 가능", 1);
            return context?.HasDamaged(index) == true ? new DamageReaction(DamageResponse.AlreadyDamaged, "본체별 턴당 최대 1 피해") :
                new DamageReaction(DamageResponse.Damage, "본체 내구도 감소", 1);
        }

        internal static int Apply(LevelRuntimeState state, RuntimeCell cell, TurnEffectContext context, int hit)
        {
            int index = cell.ObstacleIndex.Value;
            RuntimeObstacle body = state.Obstacles[index];
            if (body.Definition.Kind == ObstacleKind.Appliance) context.RegisterHit(hit, cell.Coordinate);
            else context.RegisterDamage(index);
            body.Durability--;
            if (body.Durability == 0)
            {
                Remove(state, index);
                GeneratorRules.TargetRemoved(state, index, context);
            }
            return body.Durability;
        }

        internal static void Remove(LevelRuntimeState state, int index)
        {
            if (!GeneratorRules.Alive(state, index)) return;
            RuntimeObstacle body = state.Obstacles[index];
            if (body.Definition.Kind != ObstacleKind.Generator)
            { body.Durability = 0; MissionProgressRules.Complete(state, Mission(body.Definition.Kind), body.Definition.Coordinate, index); }
            foreach (RuntimeCell occupied in state.Cells.Where(c => c.ObstacleIndex == index))
            { occupied.Content = RuntimeContent.Empty; occupied.Color = null; occupied.RocketDirection = null; occupied.ObstacleIndex = null; }
        }
    }
}
