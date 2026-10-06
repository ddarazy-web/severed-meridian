using System;
using System.Collections.Generic;
using Simulation;

namespace Elements
{
    /// <summary>층의 행동 키로 조회/적용 짝만 선택한다. 전체 행동 탐색이나 종류 추론을 하지 않는다.</summary>
    internal static class ElementLayerBehaviorRegistry
    {
        private delegate DamageReaction LayerQuery(ElementDefinition definition, RuntimeCell cell, TurnEffectContext context);
        private delegate void LayerApply(ElementDefinition definition, LevelRuntimeState state, RuntimeCell cell, TurnEffectContext context);
        private sealed class Behavior
        {
            internal readonly LayerQuery Query;
            internal readonly LayerApply Apply;
            internal Behavior(LayerQuery query, LayerApply apply) { Query = query; Apply = apply; }
        }
        private static readonly Dictionary<ElementLayerBehavior, Behavior> Behaviors = new Dictionary<ElementLayerBehavior, Behavior>
        {
            { ElementLayerBehavior.CoverDurability, new Behavior(WebRules.QueryDefinition, WebRules.ApplyDefinition) },
            { ElementLayerBehavior.CoverRemoval, new Behavior((definition, cell, context) =>
                new DamageReaction(DamageResponse.CoverDamage, "곰팡이 제거 · 내용물 보존", definition.RequireLayer().Damage),
                (definition, state, cell, context) => MoldRules.RemoveDefinition(definition, state, cell, context)) },
            { ElementLayerBehavior.NormalConsumption, new Behavior((definition, cell, context) =>
                new DamageReaction(DamageResponse.None, "일반 블록 소비에서만 먼지 피해"), DustRules.ApplyDefinition) }
        };
        private static Behavior Get(ElementDefinition definition)
        {
            ElementLayerBehavior key = definition.RequireLayer().Behavior;
            if (!Behaviors.TryGetValue(key, out Behavior behavior))
                throw new InvalidOperationException($"요소 '{definition.Id.Value}'의 층 행동 '{key}'는 지원하지 않습니다.");
            return behavior;
        }
        internal static DamageReaction Query(ElementDefinition definition, RuntimeCell cell, TurnEffectContext context)
            => Get(definition).Query(definition, cell, context);
        internal static void Apply(ElementDefinition definition, LevelRuntimeState state, RuntimeCell cell, TurnEffectContext context)
            => Get(definition).Apply(definition, state, cell, context);
    }
}
