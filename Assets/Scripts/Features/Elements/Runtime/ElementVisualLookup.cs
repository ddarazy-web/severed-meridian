using System;
using Levels;
using Simulation;

namespace Elements
{
    /// <summary>세 보드가 동일한 실제 정의/상태를 조회기에 전달한다. 구형 ID 보충은 이 경계에만 둔다.</summary>
    public sealed class ElementVisualLookup
    {
        public ElementVisualResolver Resolver { get; }
        public ElementVisualLookup(ElementVisualCatalog catalog) { Resolver = new ElementVisualResolver(catalog); }
        public static ElementVisualCatalog ForLevel(LevelDefinition level)
            => level != null && level.ElementCatalog != null ? level.ElementCatalog.CreateVisualCatalog() : LegacyElementVisuals.Catalog;

        public ElementVisualFrame Content(RuntimeCell cell)
        {
            if (!cell.IsActive || cell.Cover == CoverKind.Mold || cell.Content == RuntimeContent.Empty || cell.Content == RuntimeContent.Obstacle) return null;
            ElementDefinition definition = cell.ContentElement ?? LegacyElementDefinitions.GetContent(cell.Content);
            if (definition == null) throw new ArgumentException("내용물의 시각 정의 ID가 없습니다: " + cell.Content);
            return Resolver.Resolve(definition.Id, new ElementVisualState(cell.Color.HasValue ? (int)cell.Color.Value : -1,
                0, 0, 0, (int)(cell.RocketDirection ?? RocketDirection.Horizontal), 0, 1));
        }
        public ElementVisualFrame Obstacle(RuntimeObstacle body)
        {
            if (body.Durability <= 0 && body.Element.ChargePlacement == null) return null;
            return Resolver.Resolve(body.Element.Id, new ElementVisualState((int)body.Definition.Color, body.Durability,
                body.Charge, body.Definition.RequiredCharge, 0, 0, Size(body.Element)));
        }
        public ElementVisualFrame Animation(RuntimeCell origin, RuntimeContent content, RocketDirection direction, int frame)
        {
            ElementDefinition definition = origin.Content == content ? origin.ContentElement : null;
            definition ??= LegacyElementDefinitions.GetContent(content);
            if (definition == null) throw new ArgumentException("애니메이션의 시각 정의 ID가 없습니다: " + content);
            return Resolver.Resolve(definition.Id, new ElementVisualState(-1, 0, 0, 0, (int)direction, frame, 1));
        }
        public ElementVisualFrame Supply(SettlementRecord record, LevelRuntimeState state)
        {
            if (record.Content == RuntimeContent.Obstacle && record.ObstacleIndex.HasValue)
                return Obstacle(state.Obstacles[record.ObstacleIndex.Value]);
            ElementDefinition definition = record.ContentElement ?? LegacyElementDefinitions.GetContent(record.Content);
            if (definition == null) throw new ArgumentException("공급 내용물의 시각 정의 ID가 없습니다: " + record.Content);
            return Resolver.Resolve(definition.Id, new ElementVisualState(record.Color.HasValue ? (int)record.Color.Value : -1,
                0, 0, 0, (int)(record.Direction ?? RocketDirection.Horizontal), 0, 1));
        }
        public ElementVisualFrame Cover(RuntimeCell cell)
            => !cell.IsActive || !cell.Cover.HasValue ? null : Resolver.Resolve(
                (cell.CoverElement ?? LegacyElementDefinitions.Get(cell.Cover.Value)).Id,
                new ElementVisualState(-1, cell.CoverDurability, 0, 0, 0, 0, 1));
        public ElementVisualFrame Dust(RuntimeCell cell)
            => !cell.IsActive || cell.DustDurability <= 0 ? null : Resolver.Resolve(
                (cell.DustElement ?? LegacyElementDefinitions.GetDust()).Id,
                new ElementVisualState(-1, cell.DustDurability, 0, 0, 0, 0, 1));
        public ElementVisualFrame Placement(ElementDefinition definition, ElementPlacementDefinition placement)
        {
            // 미확정 색은 편집기의 '?' 표시를 유지한다. 게임에서는 확정된 실제 색으로 조회한다.
            if (definition.Supply?.Behavior == ElementSupplyBehavior.RandomNormal) return null;
            return Resolver.Resolve(definition.Id, new ElementVisualState((int)placement.color, placement.durability,
                0, placement.requiredCharge, (int)placement.rocketDirection, 0, Size(definition)));
        }
        public static int Size(ElementDefinition definition) => definition.Placement?.Size ?? definition.ChargePlacement?.Size ?? 1;
    }
}
