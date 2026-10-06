using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Elements;
using MemoryPack;

namespace Levels
{
    /// <summary>스키마5 공급 값. 구형 공급 DTO의 순서와 필드는 변경하지 않는다.</summary>
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class ElementLevelSupplyDefinition
    {
        [MemoryPackOrder(0)] public List<ElementSupplySourceDefinition> sources = new List<ElementSupplySourceDefinition>();
        [MemoryPackOrder(1)] public int scrapTarget;
        [MemoryPackOrder(2)] public int scrapLimit;
        [MemoryPackOrder(3)] public int scrapDurability = 1;
        [MemoryPackOrder(4)] public int recoveryTarget;
        [MemoryPackOrder(5)] public string scrapDefinitionId = "supply.scrap";
        [MemoryPackOrder(6)] public string recoveryDefinitionId = "supply.recovery";

        public static ElementLevelSupplyDefinition FromLegacy(LevelSupplyDefinition source)
        {
            if (source?.Sources == null) throw new ArgumentException("구형 공급 목록이 없습니다.");
            return new ElementLevelSupplyDefinition
            {
                scrapTarget = source.ScrapTarget, scrapLimit = source.ScrapLimit,
                scrapDurability = source.ScrapDurability, recoveryTarget = source.RecoveryTarget,
                sources = source.Sources.Select(value => new ElementSupplySourceDefinition
                {
                    coordinate = value.Coordinate, mode = value.Mode, exhaustion = value.Exhaustion,
                    items = value.Items?.Select(item => new ElementSupplyItemDefinition
                    {
                        definitionId = LegacyElementMap.Get(item.Kind).Value, count = item.Count,
                        color = item.Color, direction = item.Direction, durability = item.Durability
                    }).ToList()
                }).ToList()
            };
        }
    }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class ElementSupplySourceDefinition
    {
        [MemoryPackOrder(0)] public BoardCoordinate coordinate;
        [MemoryPackOrder(1)] public SupplyMode mode;
        [MemoryPackOrder(2)] public SupplyExhaustion exhaustion;
        [MemoryPackOrder(3)] public List<ElementSupplyItemDefinition> items = new List<ElementSupplyItemDefinition>();
        [MemoryPackOrder(4)] public string randomDefinitionId = "supply.normal.random";
    }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class ElementSupplyItemDefinition
    {
        [MemoryPackOrder(0)] public string definitionId;
        [MemoryPackOrder(1)] public int count = 1;
        [MemoryPackOrder(2)] public RabbitColor color;
        [MemoryPackOrder(3)] public RocketDirection direction;
        [MemoryPackOrder(4)] public int durability = 1;
    }
}
