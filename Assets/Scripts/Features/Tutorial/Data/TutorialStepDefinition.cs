using System;
using System.Collections.Generic;
using System.Linq;
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
        [MemoryPackIgnore] public string authoringId = "";
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
        // 팩3 레이아웃은 보존하고 새 제작 필드는 팩4 확장 영역에서 직렬화한다.
        [MemoryPackIgnore] public List<TutorialConditionDefinition> conditions = new List<TutorialConditionDefinition>();
        [MemoryPackIgnore] public TutorialConditionCombination combination;
        [MemoryPackIgnore] public bool automaticHighlights;
        [MemoryPackIgnore] public int freeItemCount = 1;
        [MemoryPackIgnore] public List<BoardCoordinate> actionArea = new List<BoardCoordinate>();
        [MemoryPackIgnore] public string firstBinding = "";
        [MemoryPackIgnore] public string secondBinding = "";
        public TutorialStepDefinition Copy() => new TutorialStepDefinition
        {
            authoringId = authoringId, kind = kind, instructions = instructions, hasFirst = hasFirst, first = first,
            hasSecond = hasSecond, second = second, item = item, actionDefinitionId = actionDefinitionId,
            highlights = highlights == null ? null : new List<BoardCoordinate>(highlights),
            combination = combination, automaticHighlights = automaticHighlights, freeItemCount = freeItemCount,
            actionArea = actionArea == null ? null : new List<BoardCoordinate>(actionArea),
            firstBinding = firstBinding, secondBinding = secondBinding,
            conditions = conditions?.Select(condition => condition?.Copy()).ToList(),
            results = results?.Select(result => result == null ? null : new TutorialResultDefinition
            { kind = result.kind, definitionId = result.definitionId, hasCoordinate = result.hasCoordinate, coordinate = result.coordinate, count = result.count }).ToList()
        };
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
