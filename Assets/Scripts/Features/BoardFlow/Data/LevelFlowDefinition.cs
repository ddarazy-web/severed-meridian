using MemoryPack;
using System;
using System.Collections.Generic;
using Board;
using UnityEngine;

namespace Levels
{
    public enum GravityDirection { Down, Up, Left, Right }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial struct GravityCell
    {
        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private BoardCoordinate coordinate;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(1)] private GravityDirection direction;
        [MemoryPackIgnore] public BoardCoordinate Coordinate => coordinate;
        [MemoryPackIgnore] public GravityDirection Direction => direction;
    }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial struct FlowPathCell
    {
        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private BoardCoordinate coordinate;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(1)] private bool isEnd;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(2)] private BoardCoordinate next;
        [MemoryPackIgnore] public BoardCoordinate Coordinate => coordinate;
        [MemoryPackIgnore] public bool IsEnd => isEnd;
        [MemoryPackIgnore] public BoardCoordinate Next => next;
    }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial struct FlowMerge
    {
        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private BoardCoordinate coordinate;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(1)] private List<BoardCoordinate> sources;
        [MemoryPackIgnore] public BoardCoordinate Coordinate => coordinate;
        [MemoryPackIgnore] public IReadOnlyList<BoardCoordinate> Sources => sources;
    }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial struct FlowPortal
    {
        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private BoardCoordinate entrance;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(1)] private bool hasExit;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(2)] private BoardCoordinate exit;
        [MemoryPackIgnore] public BoardCoordinate Entrance => entrance;
        [MemoryPackIgnore] public bool HasExit => hasExit;
        [MemoryPackIgnore] public BoardCoordinate Exit => exit;
    }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public sealed partial class LevelFlowDefinition
    {
        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private List<GravityCell> gravity = new List<GravityCell>();
        [SerializeField, MemoryPackInclude, MemoryPackOrder(1)] private List<FlowPathCell> paths = new List<FlowPathCell>();
        [SerializeField, MemoryPackInclude, MemoryPackOrder(2)] private List<FlowMerge> merges = new List<FlowMerge>();
        [SerializeField, MemoryPackInclude, MemoryPackOrder(3)] private List<BoardEdge> walls = new List<BoardEdge>();
        [SerializeField, MemoryPackInclude, MemoryPackOrder(4)] private List<FlowPortal> portals = new List<FlowPortal>();
        [SerializeField, MemoryPackInclude, MemoryPackOrder(5)] private List<BoardCoordinate> arrivals = new List<BoardCoordinate>();
        [MemoryPackIgnore] public IReadOnlyList<GravityCell> Gravity => gravity;
        [MemoryPackIgnore] public IReadOnlyList<FlowPathCell> Paths => paths;
        [MemoryPackIgnore] public IReadOnlyList<FlowMerge> Merges => merges;
        [MemoryPackIgnore] public IReadOnlyList<BoardEdge> Walls => walls;
        [MemoryPackIgnore] public IReadOnlyList<FlowPortal> Portals => portals;
        [MemoryPackIgnore] public IReadOnlyList<BoardCoordinate> Arrivals => arrivals;
        [MemoryPackIgnore] public bool ListsPresent => gravity != null && paths != null && merges != null && walls != null && portals != null && arrivals != null;
        [MemoryPackIgnore] public bool HasRecords => (gravity?.Count ?? 0) + (paths?.Count ?? 0) + (merges?.Count ?? 0) +
            (walls?.Count ?? 0) + (portals?.Count ?? 0) + (arrivals?.Count ?? 0) > 0;
    }
}
