using System;
using System.Collections.Generic;
using Levels;
using Simulation;

namespace Elements
{
    /// <summary>현재/공급 상태에서 도달할 표시 상태를 native 로드 전에 검사한다. 최대 허용치로 현재 내구도를 바꾸지 않는다.</summary>
    internal static class ElementVisualPreparation
    {
        private static IEnumerable<string> RequiredEffects(ElementDefinition definition)
        {
            if (definition.Layer != null) { yield return "clear"; yield break; }
            if (definition.ChargePlacement != null) { yield return "charge"; yield return "damage"; yield break; }
            if (definition.DamageSourcePolicy != null) { yield return "damage"; yield break; }
            if (definition.Supply == null) yield break;
            switch (definition.Supply.Behavior)
            {
                case ElementSupplyBehavior.FixedNormal:
                case ElementSupplyBehavior.RandomNormal: yield return "match"; yield return "creation"; break;
                case ElementSupplyBehavior.Power:
                    switch (definition.Supply.Content)
                    {
                        case RuntimeContent.Rocket: yield return "trail"; yield return "impact"; break;
                        case RuntimeContent.Bomb: yield return "blast"; break;
                        case RuntimeContent.Drone: yield return "impact"; break;
                        case RuntimeContent.Magnet: yield return "pull"; yield return "transform"; break;
                    }
                    break;
            }
        }
        internal static void Validate(LevelRuntimeState state, ElementVisualCatalog visuals, IEnumerable<ElementId> reachable,
            Action<ElementDefinition, ElementVisualFrame> selected = null)
        {
            ElementVisualResolver resolver = new ElementVisualResolver(visuals);
            void Resolve(ElementDefinition definition, int color, int durability = 0, int charge = 0,
                int required = 0, int direction = 0, int frame = 0)
            {
                ElementVisualFrame visual = resolver.Resolve(definition.Id, new ElementVisualState(color, durability, charge, required,
                    direction, frame, ElementVisualLookup.Size(definition)));
                selected?.Invoke(definition, visual);
                if (definition.Supply?.Behavior != ElementSupplyBehavior.Power || frame == 0)
                    foreach (string key in RequiredEffects(definition))
                    {
                        if (!visual.EffectAnimations.TryGetValue(key, out var animation))
                            throw ElementVisualFrame.Error(definition.Id.Value, "등록되지 않은 필수 효과 키 '" + key + "'");
                        foreach (ElementVisualFrame effect in animation) selected?.Invoke(definition, effect);
                    }
            }
            void Durability(ElementDefinition definition, int color, int maximum)
            { for (int value = maximum; value > 0; value--) Resolve(definition, color, value); }
            void Power(ElementDefinition definition, int direction, bool randomDirection)
            {
                RuntimeContent content = definition.RequireSupply().Content;
                int maximumFrame = content == RuntimeContent.Rocket ? 3 : content == RuntimeContent.Drone ? 4 : 0;
                for (int axis = 0; axis < (randomDirection && content == RuntimeContent.Rocket ? 2 : 1); axis++)
                    for (int frame = 0; frame <= maximumFrame; frame++) Resolve(definition, -1, direction: randomDirection ? axis : direction, frame: frame);
            }
            void Supply(ElementDefinition definition, SupplyItem item, bool randomDirection = false)
            {
                ElementSupplyProfile profile = definition.RequireSupply();
                switch (profile.Behavior)
                {
                    case ElementSupplyBehavior.RandomNormal:
                        foreach (RabbitColor color in state.Colors) Resolve(definition, (int)color);
                        break;
                    case ElementSupplyBehavior.FixedNormal: Resolve(definition, (int)item.Color); break;
                    case ElementSupplyBehavior.Power: Power(definition, (int)item.Direction, randomDirection); break;
                    case ElementSupplyBehavior.Recovery: Resolve(definition, -1); break;
                    case ElementSupplyBehavior.Obstacle:
                        Durability(ElementSupplyBehaviorRegistry.ResolveBody(profile, state.ElementCatalog), (int)item.Color, item.Durability);
                        break;
                    case ElementSupplyBehavior.RandomPower:
                        foreach (ElementId id in profile.ChoiceDefinitionIds) Supply(state.ElementCatalog.Get(id), item, true);
                        foreach (SupplyKind kind in profile.Choices) Supply(state.ElementCatalog.Get(LegacyElementMap.Get(kind)), item, true);
                        break;
                }
            }
            foreach (RuntimeCell cell in state.Cells)
            {
                if (!cell.IsActive) continue;
                ElementDefinition content = cell.ContentElement ?? LegacyElementDefinitions.GetContent(cell.Content, state.ElementCatalog);
                if (content != null)
                {
                    if (content.Supply?.Behavior == ElementSupplyBehavior.Power)
                        Power(content, (int)(cell.RocketDirection ?? RocketDirection.Horizontal), true);
                    else Resolve(content, cell.Color.HasValue ? (int)cell.Color.Value : -1, direction: (int)(cell.RocketDirection ?? RocketDirection.Horizontal));
                }
                if (cell.Cover.HasValue)
                    Durability(cell.CoverElement ?? LegacyElementDefinitions.Get(cell.Cover.Value), -1, cell.CoverDurability);
                if (cell.DustDurability > 0)
                    Durability(cell.DustElement ?? LegacyElementDefinitions.GetDust(), -1, cell.DustDurability);
            }
            foreach (RuntimeObstacle body in state.Obstacles)
            {
                if (body.Element.ChargePlacement != null)
                {
                    for (int charge = body.Charge; charge <= body.Definition.RequiredCharge; charge++)
                        Resolve(body.Element, (int)body.Definition.Color, body.Durability, charge, body.Definition.RequiredCharge);
                }
                else Durability(body.Element, (int)body.Definition.Color, body.Durability);
            }
            foreach (RuntimeSource source in state.Supply.Sources)
            {
                if (source.Mode == SupplyMode.Fixed)
                    for (int i = 0; i < source.Items.Count; i++)
                        Supply(source.ItemDefinitions != null ? source.ItemDefinitions[i] : LegacyElementDefinitions.GetSupply(source.Items[i].Kind), source.Items[i]);
                if (source.Mode != SupplyMode.Fixed || source.Exhaustion == SupplyExhaustion.Random)
                    Supply(source.RandomDefinition ?? LegacyElementDefinitions.GetSupply(SupplyKind.RandomNormal), default);
                if (source.Mode == SupplyMode.MaintainScrap)
                    Supply(state.Supply.ScrapDefinition ?? LegacyElementDefinitions.GetSupply(SupplyKind.Scrap),
                        new SupplyItem(SupplyKind.Scrap, durability: state.Supply.ScrapDurability));
                if (source.Mode == SupplyMode.MaintainRecovery)
                    Supply(state.Supply.RecoveryDefinition ?? LegacyElementDefinitions.GetSupply(SupplyKind.Recovery), default);
            }
            foreach (SupplyKind kind in new[] { SupplyKind.RandomNormal, SupplyKind.Rocket, SupplyKind.Bomb, SupplyKind.Drone, SupplyKind.Magnet })
                Supply(state.ElementCatalog.Get(LegacyElementMap.Get(kind)), default, true);
            // 성공 후 공급은 레벨 팔레트와 별개로 기존 다섯 색을 사용한다.
            if (state.Supply.Sources.Count > 0)
            {
                ElementDefinition normal = LegacyElementDefinitions.GetContent(RuntimeContent.Normal, state.ElementCatalog);
                foreach (RabbitColor color in Enum.GetValues(typeof(RabbitColor))) Resolve(normal, (int)color);
            }
            HashSet<ElementId> generated = new HashSet<ElementId>();
            void Generated(ElementDefinition definition)
            {
                if (!generated.Add(definition.Id)) return;
                if (definition.Supply != null) Supply(definition, new SupplyItem(SupplyKind.RandomNormal), true);
                else if (definition.ChargePlacement != null)
                {
                    for (int required = definition.ChargePlacement.MinRequiredCharge; required <= definition.ChargePlacement.MaxRequiredCharge; required++)
                        for (int charge = 0; charge <= required; charge++)
                            foreach (RabbitColor color in Enum.GetValues(typeof(RabbitColor))) Resolve(definition, (int)color, charge: charge, required: required);
                }
                else if (definition.Placement != null)
                {
                    if (definition.Layer != null) Durability(definition, -1, definition.Placement.MaxDurability);
                    else foreach (RabbitColor color in Enum.GetValues(typeof(RabbitColor))) Durability(definition, (int)color, definition.Placement.MaxDurability);
                }
                if (definition.Supply?.Behavior != ElementSupplyBehavior.Obstacle && definition.Supply?.Behavior != ElementSupplyBehavior.RandomPower)
                    foreach (ElementId id in visuals.Get(definition.Id).Generates) Generated(state.ElementCatalog.Get(id));
            }
            foreach (ElementId id in reachable)
            {
                ElementDefinition definition = state.ElementCatalog.Get(id);
                if (definition.Turn != null) Durability(definition, -1, definition.Turn.InitialDurability);
                if (definition.Supply?.Behavior == ElementSupplyBehavior.Obstacle || definition.Supply?.Behavior == ElementSupplyBehavior.RandomPower) continue;
                foreach (ElementId target in visuals.Get(id).Generates) Generated(state.ElementCatalog.Get(target));
            }
        }
    }
}
