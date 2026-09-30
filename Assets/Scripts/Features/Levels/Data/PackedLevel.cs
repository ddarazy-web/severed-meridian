using System.Collections.Generic;
using Board;
using MemoryPack;

namespace Levels
{
    // 포맷 순서는 배포 계약이다. 변경 시 LevelPackCodec.FormatVersion도 갱신한다.
    [MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedLevel
    {
        [MemoryPackOrder(0)] public int SchemaVersion { get; set; }
        [MemoryPackOrder(1)] public int LevelNumber { get; set; }
        [MemoryPackOrder(2)] public int MoveCount { get; set; }
        [MemoryPackOrder(3)] public List<RabbitColor> Colors { get; set; }
        [MemoryPackOrder(4)] public BoardDefinition Board { get; set; }
        [MemoryPackOrder(5)] public List<InitialBlockDefinition> InitialBlocks { get; set; }
        [MemoryPackOrder(6)] public List<ObstaclePlacementDefinition> Obstacles { get; set; }
        [MemoryPackOrder(7)] public List<CoverPlacementDefinition> Covers { get; set; }
        [MemoryPackOrder(8)] public List<DustPlacementDefinition> Dust { get; set; }
        [MemoryPackOrder(9)] public LevelFlowDefinition Flow { get; set; }
        [MemoryPackOrder(10)] public List<LevelConnectionDefinition> Connections { get; set; }
        [MemoryPackOrder(11)] public LevelSupplyDefinition Supply { get; set; }
        [MemoryPackOrder(12)] public List<BoardCoordinate> RecoveryParts { get; set; }
        [MemoryPackOrder(13)] public List<LevelMissionDefinition> Missions { get; set; }
    }
}
