using MemoryPack;
using System;
using Board;
using UnityEngine;

namespace Levels
{
    public enum CoverKind
    {
        [InspectorName("거미줄")] Web = 0,
        [InspectorName("우주 곰팡이")] Mold = 1
    }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial struct CoverPlacementDefinition
    {
        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private BoardCoordinate coordinate;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(1)] private CoverKind kind;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(2)] private int durability;

        [MemoryPackIgnore] public BoardCoordinate Coordinate => coordinate;
        [MemoryPackIgnore] public CoverKind Kind => kind;
        [MemoryPackIgnore] public int Durability => durability;
    }
}
