using System;
using System.Collections.Generic;
using Levels;
using Simulation;

namespace Elements
{
    /// <summary>선택된 공급의 생성만 실행한다. 목록 소진·커서·공급 위치 선택은 정착 실행기가 소유한다.</summary>
    internal static class ElementSupplyBehaviorRegistry
    {
        private delegate void SupplyAction(ElementDefinition definition, LevelRuntimeState state, RuntimeCell cell, SupplyItem item);
        private static readonly Dictionary<ElementSupplyBehavior, SupplyAction> Actions = new Dictionary<ElementSupplyBehavior, SupplyAction>
        {
            { ElementSupplyBehavior.RandomNormal, (definition, state, cell, item) =>
                cell.Color = state.Colors[state.Random.Next(state.Colors.Count)] },
            { ElementSupplyBehavior.FixedNormal, (definition, state, cell, item) => cell.Color = item.Color },
            { ElementSupplyBehavior.Power, (definition, state, cell, item) =>
                cell.RocketDirection = definition.RequireSupply().Content == RuntimeContent.Rocket ? item.Direction : (RocketDirection?)null },
            { ElementSupplyBehavior.Recovery, (definition, state, cell, item) => { } },
            { ElementSupplyBehavior.Obstacle, (definition, state, cell, item) =>
                {
                    ElementSupplyProfile profile = definition.RequireSupply();
                    ElementDefinition body = ResolveBody(profile, state.ElementCatalog);
                    state.SupplyObstacle(cell, body, item.Durability, profile.BodyIdPrefix, item.Color);
                } },
            { ElementSupplyBehavior.RandomPower, (definition, state, cell, item) =>
                {
                    ElementSupplyProfile profile = definition.RequireSupply();
                    if (profile.ChoiceDefinitionIds.Count > 0 || state.SchemaVersion == 5)
                    {
                        ElementId chosenId = profile.ChoiceDefinitionIds.Count > 0
                            ? profile.ChoiceDefinitionIds[state.Random.Next(profile.ChoiceDefinitionIds.Count)]
                            : LegacyElementMap.Get(profile.Choices[state.Random.Next(profile.Choices.Count)]);
                        ElementDefinition choice = state.ElementCatalog.Get(chosenId);
                        RocketDirection selectedDirection = choice.RequireSupply().Content == RuntimeContent.Rocket
                            ? (RocketDirection)state.Random.Next(2) : RocketDirection.Horizontal;
                        Apply(choice, state, cell, new SupplyItem(SupplyKind.RandomPower, item.Count, direction: selectedDirection));
                        return;
                    }
                    SupplyKind chosen = profile.Choices[state.Random.Next(profile.Choices.Count)];
                    RocketDirection direction = chosen == SupplyKind.Rocket ? (RocketDirection)state.Random.Next(2) : RocketDirection.Horizontal;
                    Apply(LegacyElementDefinitions.GetSupply(chosen), state, cell, new SupplyItem(chosen, item.Count, direction: direction));
                } }
        };
        internal static ElementDefinition ResolveBody(ElementSupplyProfile profile, ElementCatalog catalog) =>
            catalog.Get(profile.ObstacleDefinitionId ?? LegacyElementMap.Get(profile.Obstacle.Value));

        internal static MissionKind? Mission(ElementDefinition definition, ElementCatalog catalog) => definition.RequireSupply().Behavior switch
        {
            ElementSupplyBehavior.Obstacle => ResolveBody(definition.RequireSupply(), catalog).RequireRemovalMissionProfile().Kind,
            ElementSupplyBehavior.Recovery => MissionKind.Recovery,
            _ => null
        };

        internal static void Apply(ElementDefinition definition, LevelRuntimeState state, RuntimeCell cell, SupplyItem item)
        {
            ElementSupplyProfile profile = definition.RequireSupply();
            if (!Actions.TryGetValue(profile.Behavior, out SupplyAction action))
                throw new InvalidOperationException($"요소 '{definition.Id.Value}'의 공급 행동 '{profile.Behavior}'는 지원하지 않습니다.");
            cell.Content = profile.Content; cell.Color = null; cell.RocketDirection = null; cell.ObstacleIndex = null;
            cell.ContentElement = definition;
            action(definition, state, cell, item);
        }
    }
}
