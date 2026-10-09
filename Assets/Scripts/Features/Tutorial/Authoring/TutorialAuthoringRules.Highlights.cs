using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;

namespace Tutorial
{
    public static partial class TutorialAuthoringRules
    {
        public static IEnumerable<BoardCoordinate> PreviewTargetHighlights(LevelDefinition owner, TutorialStepDefinition step, bool initialAction)
        {
            Elements.ElementCatalog catalog = owner.CreateElementCatalog();
            IReadOnlyList<ElementPlacementDefinition> placements = owner.SchemaVersion == LevelDefinition.LegacySchemaVersion ? LegacyElementLevelAdapter.Preview(owner) : owner.Elements;
            var result = new HashSet<BoardCoordinate>();
            foreach (TutorialConditionDefinition condition in step.conditions)
            {
                TutorialTargetDefinition target = condition.target;
                if (target == null || condition.kind < TutorialConditionKind.DurabilityDecrease || condition.kind > TutorialConditionKind.Combined) continue;
                if (target.kind == TutorialTargetKind.Area) { result.UnionWith(target.cells); continue; }
                if (target.kind == TutorialTargetKind.Entity) result.Add(target.coordinate);
                if (!initialAction || target.kind != TutorialTargetKind.Entity && target.kind != TutorialTargetKind.Definition) continue;
                foreach (ElementPlacementDefinition placement in placements)
                {
                    if (placement == null || string.IsNullOrWhiteSpace(placement.definitionId)) continue;
                    Elements.ElementDefinition definition = catalog.Definitions.FirstOrDefault(value => value.Id.Value == placement.definitionId);
                    if (definition == null) continue;
                    TutorialTargetLayer layer = placement.layer == PlacementLayer.Cover ? TutorialTargetLayer.Cover : placement.layer == PlacementLayer.Dust ? TutorialTargetLayer.Floor : TutorialTargetLayer.Content;
                    if (target.layer != layer) continue;
                    var footprint = LevelPlacementRules.Footprint(placement.coordinate, definition.Placement?.Size ?? definition.ChargePlacement?.Size ?? 1).ToArray();
                    if (target.kind == TutorialTargetKind.Entity ? footprint.Contains(target.coordinate) : target.definitionId == placement.definitionId)
                        result.UnionWith(footprint);
                }
            }
            return result;
        }

    }
}
