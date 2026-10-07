using MemoryPack;

namespace Elements
{
    /// <summary>제작 값만 배포하며 Unity 에셋을 참조하지 않는다.</summary>
    [MemoryPackable]
    public partial class ElementContentPack
    {
        public int FormatVersion = 1;
        public PackedElementDefinition[] Definitions;
        public ElementVisualCatalogDto Visuals;
    }
}
