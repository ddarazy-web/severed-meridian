using System.Collections.Generic;
using Board;
using Elements;
using MemoryPack;

namespace Levels
{
    // 팩1의 PackedLevel은 변경하지 않는다. 새 배치·공급만 이 계약에 저장한다.
    [MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedElementLevel
    {
        [MemoryPackOrder(0)] public int SchemaVersion { get; set; } = 5;
        [MemoryPackOrder(1)] public int LevelNumber { get; set; }
        [MemoryPackOrder(2)] public int MoveCount { get; set; }
        [MemoryPackOrder(3)] public List<RabbitColor> Colors { get; set; }
        [MemoryPackOrder(4)] public BoardDefinition Board { get; set; }
        [MemoryPackOrder(5)] public LevelFlowDefinition Flow { get; set; }
        [MemoryPackOrder(6)] public List<LevelConnectionDefinition> Connections { get; set; }
        [MemoryPackOrder(7)] public List<LevelMissionDefinition> Missions { get; set; }
        [MemoryPackOrder(8)] public List<ElementPlacementDefinition> Elements { get; set; }
        [MemoryPackOrder(9)] public ElementLevelSupplyDefinition Supply { get; set; }
    }

    [MemoryPackable(SerializeLayout.Explicit)]
    public partial class ElementLevelPack
    {
        [MemoryPackOrder(0)] public int FormatVersion { get; set; } = 2;
        [MemoryPackOrder(1)] public int FirstLevel { get; set; }
        [MemoryPackOrder(2)] public PackedElementDefinition[] Definitions { get; set; }
        [MemoryPackOrder(3)] public PackedElementLevel[] Levels { get; set; }
    }

    public sealed class LevelWithCatalog
    {
        public LevelDefinition Level { get; }
        public ElementCatalog Catalog { get; }
        internal LevelWithCatalog(LevelDefinition level, ElementCatalog catalog) { Level = level; Catalog = catalog; }
    }
}
