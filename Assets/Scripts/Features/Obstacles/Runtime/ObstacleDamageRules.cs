using System.Linq;
using Elements;
using Levels;

namespace Simulation
{
    internal static class ObstacleDamageRules
    {
        private static readonly ElementBehaviorRegistry reactionBehaviors = new ElementBehaviorRegistry(QueryDurability, ApplyDurability, QueryCharge, ApplyCharge,
            QueryEvenTurnDurability);
        internal static bool Supports(ObstacleKind kind) => kind >= ObstacleKind.Crate && kind <= ObstacleKind.Generator;
        internal static int ReservedDamage(RuntimeObstacle body, int cells)
        {
            ObstacleKind kind = body.Definition.Kind;
            bool perHitCell = kind >= ObstacleKind.Crate && kind <= ObstacleKind.Appliance &&
                body.Element.RequireDamageAggregationPolicy().PerHitCell;
            return System.Math.Min(body.Durability, perHitCell ? cells : System.Math.Min(1, cells));
        }
        internal static MissionKind Mission(ObstacleKind kind)
        {
            return kind >= ObstacleKind.Crate && kind <= ObstacleKind.Appliance ?
                LegacyElementDefinitions.Get(kind).RequireRemovalMissionProfile().Kind : MissionKind.Crate;
        }

        internal static MissionKind MissionForBody(RuntimeObstacle body) => body.Element.ChargePlacement != null && body.Element.RemovalMissionProfile == null
            ? MissionKind.Crate : body.Element.RequireRemovalMissionProfile().Kind;

        internal static DamageReaction Query(LevelRuntimeState state, RuntimeCell cell, DamageCause cause,
            RabbitColor? sourceColor, TurnEffectContext context, int hit)
        {
            int index = cell.ObstacleIndex.Value;
            RuntimeObstacle body = state.Obstacles[index];
            ObstacleKind kind = body.Definition.Kind;
            if (!Supports(kind)) return new DamageReaction(DamageResponse.Unsupported, "장애물 반응 미지원");
            ElementDefinition definition = body.Element;
            if (kind == ObstacleKind.Crate || kind == ObstacleKind.Scrap || kind == ObstacleKind.Safe || kind == ObstacleKind.ColorLock || kind == ObstacleKind.Appliance || kind == ObstacleKind.Generator)
            {
                ElementDamageSourcePolicy policy = definition.RequireDamageSourcePolicy();
                // 미정의 원인 값의 기존 조회 의미는 유지하며 네 원인만 정책에 연결한다.
                if (cause >= DamageCause.AdjacentMatch && cause <= DamageCause.Hammer && !policy.Allows(cause))
                    return new DamageReaction(DamageResponse.None, "인접 피해 대상 아님");
            }
            return reactionBehaviors.Query(definition, state, cell, cause, sourceColor, context, hit);
        }

        private static DamageReaction QueryCharge(ElementDefinition definition, LevelRuntimeState state, RuntimeCell cell,
            DamageCause cause, RabbitColor? sourceColor, TurnEffectContext context, int hit)
        {
            definition.RequireChargePlacement();
            if (definition.Placement != null)
                throw new System.InvalidOperationException($"요소 '{definition.Id.Value}'의 충전 행동에 내구도 배치가 함께 있습니다.");
            return GeneratorRules.QueryDefinition(definition, state, cell.ObstacleIndex.Value, context);
        }

        // 추가 행동은 조회 조건만 변경하고 기존 내구도 적용/제거/미션 실행을 공유한다.
        private static DamageReaction QueryEvenTurnDurability(ElementDefinition definition, LevelRuntimeState state, RuntimeCell cell,
            DamageCause cause, RabbitColor? sourceColor, TurnEffectContext context, int hit)
        {
            DamageReaction reaction = QueryDurability(definition, state, cell, cause, sourceColor, context, hit);
            return reaction.Response == DamageResponse.Damage && context != null && context.Turn % 2 != 0
                ? new DamageReaction(DamageResponse.Protected, "홀수 턴 보호") : reaction;
        }

        private static DamageReaction QueryDurability(ElementDefinition definition, LevelRuntimeState state, RuntimeCell cell,
            DamageCause cause, RabbitColor? sourceColor, TurnEffectContext context, int hit)
        {
            definition.RequirePlacement();
            if (definition.ChargePlacement != null)
                throw new System.InvalidOperationException($"요소 '{definition.Id.Value}'의 내구도 행동에 충전 배치가 함께 있습니다.");
            int index = cell.ObstacleIndex.Value;
            RuntimeObstacle body = state.Obstacles[index];
            ObstacleKind kind = body.Definition.Kind;
            if (cause != DamageCause.Power && cause != DamageCause.Hammer)
            {
                if (kind == ObstacleKind.Safe && cause != DamageCause.AdjacentMatch && cause != DamageCause.MagnetAdjacent)
                    return new DamageReaction(DamageResponse.None, "인접 피해 대상 아님");
                if (kind == ObstacleKind.ColorLock) definition.RequireColorMatchPolicy();
                if (definition.ColorMatchPolicy != null && !definition.ColorMatchPolicy.Allows(sourceColor, body.Definition.Color))
                    return new DamageReaction(DamageResponse.None, "자물쇠 지정 색 불일치");
            }
            if (body.Durability <= 0) return new DamageReaction(DamageResponse.None, "제거된 본체");
            ElementDamageAggregationPolicy aggregation = definition.RequireDamageAggregationPolicy();
            bool alreadyApplied = aggregation.AlreadyApplied(context, index, cell.Coordinate, hit);
            if (aggregation.PerHitCell)
                return alreadyApplied ? new DamageReaction(DamageResponse.AlreadyDamaged, "같은 타격의 같은 폐가전 칸") :
                    new DamageReaction(DamageResponse.Damage, "폐가전 칸 피해 · 별도 타격 반복 가능", 1);
            return alreadyApplied ? new DamageReaction(DamageResponse.AlreadyDamaged, "본체별 턴당 최대 1 피해") :
                new DamageReaction(DamageResponse.Damage, "본체 내구도 감소", 1);
        }

        internal static int ApplyReaction(LevelRuntimeState state, RuntimeCell cell, TurnEffectContext context, int hit)
        {
            ElementDefinition definition = state.Obstacles[cell.ObstacleIndex.Value].Element;
            return reactionBehaviors.Apply(definition, state, cell, context, hit);
        }

        private static int ApplyDurability(ElementDefinition definition, LevelRuntimeState state, RuntimeCell cell, TurnEffectContext context, int hit)
        {
            definition.RequirePlacement();
            if (definition.ChargePlacement != null)
                throw new System.InvalidOperationException($"요소 '{definition.Id.Value}'의 내구도 행동에 충전 배치가 함께 있습니다.");
            return ApplyCore(state, cell, context, hit, definition.RequireDamageAggregationPolicy().PerHitCell, definition);
        }

        private static int ApplyCharge(ElementDefinition definition, LevelRuntimeState state, RuntimeCell cell, TurnEffectContext context, int hit)
        {
            definition.RequireChargePlacement();
            if (definition.Placement != null)
                throw new System.InvalidOperationException($"요소 '{definition.Id.Value}'의 충전 행동에 내구도 배치가 함께 있습니다.");
            int index = cell.ObstacleIndex.Value;
            GeneratorRules.ApplyDefinition(definition, state, index, context);
            return state.Obstacles[index].Durability;
        }

        internal static int Apply(LevelRuntimeState state, RuntimeCell cell, TurnEffectContext context, int hit)
        {
            int index = cell.ObstacleIndex.Value;
            RuntimeObstacle body = state.Obstacles[index];
            ObstacleKind kind = body.Definition.Kind;
            bool perHitCell = kind >= ObstacleKind.Crate && kind <= ObstacleKind.Appliance &&
                body.Element.RequireDamageAggregationPolicy().PerHitCell;
            return ApplyCore(state, cell, context, hit, perHitCell, state.SchemaVersion == LevelDefinition.CurrentSchemaVersion ? body.Element : null);
        }

        private static int ApplyCore(LevelRuntimeState state, RuntimeCell cell, TurnEffectContext context, int hit, bool perHitCell, ElementDefinition definition = null)
        {
            int index = cell.ObstacleIndex.Value;
            RuntimeObstacle body = state.Obstacles[index];
            if (perHitCell) context.RegisterHit(hit, cell.Coordinate);
            else context.RegisterDamage(index);
            body.Durability--;
            if (body.Durability == 0)
            {
                if (definition == null) Remove(state, index);
                else RemoveDefinition(state, index, definition);
                GeneratorRules.TargetRemoved(state, index, context);
            }
            return body.Durability;
        }

        internal static void Remove(LevelRuntimeState state, int index)
        {
            if (!GeneratorRules.Alive(state, index)) return;
            ObstacleKind kind = state.Obstacles[index].Definition.Kind;
            RemoveCore(state, index, kind == ObstacleKind.Generator ? (MissionKind?)null :
                state.SchemaVersion == LevelDefinition.CurrentSchemaVersion ? MissionForBody(state.Obstacles[index]) : Mission(kind));
        }

        private static void RemoveDefinition(LevelRuntimeState state, int index, ElementDefinition definition)
        {
            if (!GeneratorRules.Alive(state, index)) return;
            bool noMission = definition.ReactionBehavior == ElementReactionBehavior.GeneratorCharge ||
                (state.Obstacles[index].Definition.Kind == ObstacleKind.Generator && definition.RemovalMissionProfile == null);
            RemoveCore(state, index, noMission ?
                (MissionKind?)null : definition.RequireRemovalMissionProfile().Kind);
        }

        private static void RemoveCore(LevelRuntimeState state, int index, MissionKind? mission)
        {
            RuntimeObstacle body = state.Obstacles[index];
            if (mission.HasValue)
            { body.Durability = 0; MissionProgressRules.Complete(state, mission.Value, body.Definition.Coordinate, index); }
            foreach (RuntimeCell occupied in state.Cells.Where(c => c.ObstacleIndex == index))
            { occupied.Content = RuntimeContent.Empty; occupied.Color = null; occupied.RocketDirection = null; occupied.ObstacleIndex = null; }
        }
    }
}
