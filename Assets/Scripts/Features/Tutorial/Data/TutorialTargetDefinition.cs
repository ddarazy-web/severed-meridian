using System;
using System.Collections.Generic;
using Board;
using MemoryPack;

namespace Tutorial
{
    public enum TutorialTargetKind { Board, Entity, Definition, Area, Generated }

    /// <summary>조건의 대상 설정. 런타임 개체 식별자와 선택 결과는 저장하지 않는다.</summary>
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial class TutorialTargetDefinition
    {
        [MemoryPackOrder(0)] public TutorialTargetKind kind;
        [MemoryPackOrder(1)] public TutorialTargetLayer layer;
        [MemoryPackOrder(2)] public BoardCoordinate coordinate;
        [MemoryPackOrder(3)] public string definitionId = "";
        [MemoryPackOrder(4)] public List<BoardCoordinate> cells = new List<BoardCoordinate>();
        [MemoryPackOrder(5)] public string binding = "";
    }
}
