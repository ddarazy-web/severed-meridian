using System.Collections.Generic;
using System.Linq;
using Elements;
using Levels;
using Simulation;

namespace Tutorial
{
    /// <summary>제작 UI와 검사가 같은 정의의 내구도·피해 정책을 조회한다. 후속 보드 상태는 추측하지 않는다.</summary>
    public static class TutorialTargetCapabilities
    {
        public static ElementDefinition Resolve(LevelDefinition level, TutorialTargetDefinition target, bool initialAction)
        {
            if (level == null || target == null) return null;
            ElementCatalog catalog = level.CreateElementCatalog();
            if (target.kind == TutorialTargetKind.Definition)
            {
                if (string.IsNullOrWhiteSpace(target.definitionId)) return null;
                return catalog.TryGet(new ElementId(target.definitionId), out ElementDefinition found) ? found : null;
            }
            if (!initialAction || target.kind != TutorialTargetKind.Entity) return null;
            IReadOnlyList<ElementPlacementDefinition> placements = level.SchemaVersion == LevelDefinition.LegacySchemaVersion ? LegacyElementLevelAdapter.Preview(level) : level.Elements;
            foreach (ElementPlacementDefinition placement in placements)
            {
                if (placement == null || string.IsNullOrWhiteSpace(placement.definitionId) || !catalog.TryGet(new ElementId(placement.definitionId), out ElementDefinition definition)) continue;
                TutorialTargetLayer layer = placement.layer == PlacementLayer.Cover ? TutorialTargetLayer.Cover :
                    placement.layer == PlacementLayer.Dust ? TutorialTargetLayer.Floor : TutorialTargetLayer.Content;
                if (layer == target.layer && LevelPlacementRules.Footprint(placement.coordinate, definition.Placement?.Size ?? definition.ChargePlacement?.Size ?? 1).Contains(target.coordinate)) return definition;
            }
            return null;
        }

        public static string ConditionError(ElementDefinition definition, TutorialConditionKind kind)
        {
            if (definition == null) return null;
            if ((kind == TutorialConditionKind.DurabilityDecrease || kind == TutorialConditionKind.RemainingDurability) && definition.Placement == null)
                return definition.DisplayName + "에는 내구도 값이 없습니다.";
            if ((kind == TutorialConditionKind.Activated || kind == TutorialConditionKind.Combined) && definition.Supply?.Behavior != ElementSupplyBehavior.Power)
                return definition.DisplayName + "은 발동할 파워가 아닙니다.";
            return null;
        }

        public static string OriginError(ElementDefinition definition, EffectOrigin origin)
        {
            ElementDamageSourcePolicy policy = definition?.DamageSourcePolicy;
            if (policy == null) return null;
            bool allowed = origin switch
            {
                EffectOrigin.AdjacentMatch => policy.AdjacentMatch,
                EffectOrigin.Hammer => policy.Hammer,
                EffectOrigin.Magnet => policy.MagnetAdjacent,
                _ => policy.Power
            };
            return allowed ? null : definition.DisplayName + "의 피해 설정은 이 원인을 허용하지 않습니다.";
        }
    }
}
