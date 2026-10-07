using System;
using System.Collections.Generic;
using Levels;
using MemoryPack;

namespace Tutorial
{
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class LevelTutorialDefinition
    {
        [MemoryPackOrder(0)] public int seed = 1;
        [MemoryPackOrder(1)] public List<TutorialStepDefinition> steps = new List<TutorialStepDefinition>();
        [MemoryPackOrder(2)] public ElementLevelSupplyDefinition supply = new ElementLevelSupplyDefinition();
    }
}
