using System;
using System.Collections.Generic;
using Levels;
using MemoryPack;

namespace Tutorial
{
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class LevelTutorialDefinition
    {
        [MemoryPackIgnore] public TutorialFlowDefinition flow;
        [MemoryPackIgnore] public List<TutorialFlowBinding> bindings = new List<TutorialFlowBinding>();
        [MemoryPackIgnore] public string completionId = "";
        [MemoryPackIgnore] public List<int> previousLevelNumbers = new List<int>();
        [MemoryPackOrder(0)] public int seed = 1;
        [MemoryPackOrder(1)] public List<TutorialStepDefinition> steps = new List<TutorialStepDefinition>();
        [MemoryPackOrder(2)] public ElementLevelSupplyDefinition supply = new ElementLevelSupplyDefinition();
    }
}
