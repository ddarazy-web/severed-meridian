using System;
using System.Collections.Generic;
using Board;
using MemoryPack;
using Simulation;

namespace Tutorial
{
    public enum TutorialStepKind { Description, Swap, PowerSwap, Item }
    public enum TutorialResultKind { Generated, Activated, Removed }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class TutorialStepDefinition
    {
        [MemoryPackOrder(0)] public TutorialStepKind kind;
        [MemoryPackOrder(1)] public string instructions = "";
        [MemoryPackOrder(2)] public List<BoardCoordinate> highlights = new List<BoardCoordinate>();
        [MemoryPackOrder(3)] public bool hasFirst;
        [MemoryPackOrder(4)] public BoardCoordinate first;
        [MemoryPackOrder(5)] public bool hasSecond;
        [MemoryPackOrder(6)] public BoardCoordinate second;
        [MemoryPackOrder(7)] public BoardItem item;
        [MemoryPackOrder(8)] public string actionDefinitionId = "";
        [MemoryPackOrder(9)] public List<TutorialResultDefinition> results = new List<TutorialResultDefinition>();
    }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class TutorialResultDefinition
    {
        [MemoryPackOrder(0)] public TutorialResultKind kind;
        [MemoryPackOrder(1)] public string definitionId = "";
        [MemoryPackOrder(2)] public bool hasCoordinate;
        [MemoryPackOrder(3)] public BoardCoordinate coordinate;
        [MemoryPackOrder(4)] public int count = 1;
    }
}
