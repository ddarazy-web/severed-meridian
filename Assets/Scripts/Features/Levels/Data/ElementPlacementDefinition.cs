using System;
using Board;
using MemoryPack;

namespace Levels
{
    /// <summary>정의 ID와 판 내부 본체 ID를 분리한 신형 배치 값.</summary>
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class ElementPlacementDefinition
    {
        [MemoryPackOrder(0)] public string definitionId;
        [MemoryPackOrder(1)] public string instanceId;
        [MemoryPackOrder(2)] public PlacementLayer layer;
        [MemoryPackOrder(3)] public BoardCoordinate coordinate;
        [MemoryPackOrder(4)] public int durability;
        [MemoryPackOrder(5)] public bool hasColor;
        [MemoryPackOrder(6)] public RabbitColor color;
        [MemoryPackOrder(7)] public int requiredCharge;
        [MemoryPackOrder(8)] public RocketDirection rocketDirection;
    }
}
