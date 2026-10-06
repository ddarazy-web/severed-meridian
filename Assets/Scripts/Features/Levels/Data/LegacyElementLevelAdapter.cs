using System;
using System.Collections.Generic;
using Elements;

namespace Levels
{
    // Editor 선택 변환과 팩 배포 변환이 같은 영구 ID 매핑을 사용한다. 원본은 수정하지 않는다.
    public static class LegacyElementLevelAdapter
    {
        public static ElementPlacementDefinition[] Preview(LevelDefinition level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (level.SchemaVersion != 4) throw new ArgumentException("선택 변환은 구형 스키마4 입력에만 적용합니다.");
            List<ElementPlacementDefinition> values = new List<ElementPlacementDefinition>();
            foreach (InitialBlockDefinition block in level.InitialBlocks)
            {
                SupplyKind kind = block.Kind switch
                {
                    InitialBlockKind.RandomNormal => SupplyKind.RandomNormal, InitialBlockKind.FixedNormal => SupplyKind.FixedNormal,
                    InitialBlockKind.Rocket => SupplyKind.Rocket, InitialBlockKind.Bomb => SupplyKind.Bomb,
                    InitialBlockKind.Drone => SupplyKind.Drone, InitialBlockKind.Magnet => SupplyKind.Magnet,
                    _ => throw new ArgumentException("등록하지 않은 구형 블록입니다.")
                };
                values.Add(new ElementPlacementDefinition { definitionId = LegacyElementMap.Get(kind).Value,
                    layer = PlacementLayer.Block, coordinate = block.Coordinate, hasColor = block.FixedColor.HasValue,
                    color = block.FixedColor.GetValueOrDefault(), rocketDirection = block.RocketDirection });
            }
            foreach (ObstaclePlacementDefinition body in level.Obstacles)
                values.Add(new ElementPlacementDefinition { definitionId = LegacyElementMap.Get(body.Kind).Value,
                    instanceId = body.Id, layer = PlacementLayer.Obstacle, coordinate = body.Coordinate, durability = body.Durability,
                    hasColor = body.Kind == ObstacleKind.ColorLock, color = body.Color, requiredCharge = body.RequiredCharge });
            foreach (CoverPlacementDefinition cover in level.Covers)
                values.Add(new ElementPlacementDefinition { definitionId = LegacyElementMap.Get(cover.Kind).Value,
                    layer = PlacementLayer.Cover, coordinate = cover.Coordinate, durability = cover.Durability });
            foreach (DustPlacementDefinition dust in level.Dust)
                values.Add(new ElementPlacementDefinition { definitionId = LegacyElementDefinitions.GetDust().Id.Value,
                    layer = PlacementLayer.Dust, coordinate = dust.Coordinate, durability = dust.Durability });
            foreach (Board.BoardCoordinate recovery in level.RecoveryParts)
                values.Add(new ElementPlacementDefinition { definitionId = LegacyElementMap.Get(SupplyKind.Recovery).Value,
                    layer = PlacementLayer.Block, coordinate = recovery });
            return values.ToArray();
        }
    }
}
