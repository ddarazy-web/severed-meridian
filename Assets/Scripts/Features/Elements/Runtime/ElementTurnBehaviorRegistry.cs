using System;
using System.Collections.Generic;
using Levels;
using Simulation;

namespace Elements
{
    /// <summary>턴 종료 행동만 직접 선택한다. 상태와 턴 기록은 기존 실행기가 소유한다.</summary>
    internal static class ElementTurnBehaviorRegistry
    {
        private delegate MoldSpreadRecord TurnAction(ElementDefinition definition, CoverKind cover, LevelRuntimeState state, TurnEffectContext context);
        private static readonly Dictionary<ElementTurnBehavior, TurnAction> Actions = new Dictionary<ElementTurnBehavior, TurnAction>
        {
            { ElementTurnBehavior.AdjacentCoverSpread, MoldRules.FinishTurnDefinition }
        };
        internal static MoldSpreadRecord Finish(ElementDefinition definition, CoverKind cover, LevelRuntimeState state, TurnEffectContext context)
        {
            ElementTurnProfile profile = definition.RequireTurn();
            if (profile.InitialDurability > definition.RequirePlacement().MaxDurability)
                throw new InvalidOperationException($"요소 '{definition.Id.Value}'의 번식 초기 내구도가 배치 상한을 초과합니다.");
            if (!Actions.TryGetValue(profile.Behavior, out TurnAction action))
                throw new InvalidOperationException($"요소 '{definition.Id.Value}'의 턴 행동 '{profile.Behavior}'는 지원하지 않습니다.");
            return action(definition, cover, state, context);
        }
    }
}
