using MemoryPack;
using Tutorial;

namespace Levels
{
    [MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedTutorialLevelPack
    {
        [MemoryPackOrder(0)] public int FormatVersion { get; set; } = 3;
        [MemoryPackOrder(1)] public int FirstLevel { get; set; }
        [MemoryPackOrder(2)] public byte[] ElementPack { get; set; }
        [MemoryPackOrder(3)] public PackedLevelTutorial[] Tutorials { get; set; }
    }

    [MemoryPackable(SerializeLayout.Explicit)]
    public partial class PackedLevelTutorial
    {
        [MemoryPackOrder(0)] public int LevelNumber { get; set; }
        [MemoryPackOrder(1)] public LevelTutorialDefinition Tutorial { get; set; }
    }
}
