using MemoryPack;
using System;
using Board;
using UnityEngine;

namespace Levels
{
    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial struct DustPlacementDefinition
    {
        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private BoardCoordinate coordinate;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(1)] private int durability;

        [MemoryPackIgnore] public BoardCoordinate Coordinate => coordinate;
        [MemoryPackIgnore] public int Durability => durability;
    }
}
