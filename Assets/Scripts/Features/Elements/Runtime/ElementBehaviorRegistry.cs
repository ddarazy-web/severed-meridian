using System;
using System.Collections.Generic;
using Levels;
using Simulation;

namespace Elements
{
    /// <summary>명시적으로 준비한 행동 키의 조회/적용 짝 하나만 선택한다. 등록 후 변경 경로는 없다.</summary>
    internal sealed class ElementBehaviorRegistry
    {
        internal delegate DamageReaction ReactionQuery(ElementDefinition definition, LevelRuntimeState state, RuntimeCell cell,
            DamageCause cause, RabbitColor? sourceColor, TurnEffectContext context, int hit);
        internal delegate int ReactionApply(ElementDefinition definition, LevelRuntimeState state, RuntimeCell cell,
            TurnEffectContext context, int hit);
        private sealed class Behavior
        {
            internal readonly ReactionQuery Query;
            internal readonly ReactionApply Apply;
            internal Behavior(ReactionQuery query, ReactionApply apply) { Query = query; Apply = apply; }
        }
        private readonly Dictionary<ElementReactionBehavior, Behavior> queries;

        internal ElementBehaviorRegistry(ReactionQuery durability, ReactionApply applyDurability, ReactionQuery charge, ReactionApply applyCharge)
        {
            queries = new Dictionary<ElementReactionBehavior, Behavior>
            {
                { ElementReactionBehavior.Durability, new Behavior(durability, applyDurability) },
                { ElementReactionBehavior.GeneratorCharge, new Behavior(charge, applyCharge) }
            };
        }

        internal DamageReaction Query(ElementDefinition definition, LevelRuntimeState state, RuntimeCell cell,
            DamageCause cause, RabbitColor? sourceColor, TurnEffectContext context, int hit)
        {
            return Get(definition).Query(definition, state, cell, cause, sourceColor, context, hit);
        }

        internal int Apply(ElementDefinition definition, LevelRuntimeState state, RuntimeCell cell, TurnEffectContext context, int hit)
        {
            return Get(definition).Apply(definition, state, cell, context, hit);
        }

        private Behavior Get(ElementDefinition definition)
        {
            ElementReactionBehavior key = definition.RequireReactionBehavior();
            if (!queries.TryGetValue(key, out Behavior behavior))
                throw new InvalidOperationException($"요소 '{definition.Id.Value}'의 반응 행동 '{key}'는 지원하지 않습니다.");
            return behavior;
        }
    }
}
