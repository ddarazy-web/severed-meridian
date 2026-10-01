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
        internal FlowPathCell(BoardCoordinate end) { coordinate = end; isEnd = true; next = default; }
        internal FlowPathCell AsEnd() => new FlowPathCell { coordinate = coordinate, isEnd = true, next = default };
    }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial struct FlowMerge
    {
        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private BoardCoordinate coordinate;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(1)] private List<BoardCoordinate> sources;
        [MemoryPackIgnore] public BoardCoordinate Coordinate => coordinate;
        [MemoryPackIgnore] public IReadOnlyList<BoardCoordinate> Sources => sources;
        internal void CropSources(BoardDefinition board) => sources?.RemoveAll(cell => !board.Contains(cell));
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

        internal void CropTo(BoardDefinition board)
        {
            gravity?.RemoveAll(item => !board.Contains(item.Coordinate));
            paths?.RemoveAll(item => !board.Contains(item.Coordinate));
            if (paths != null)
                for (int i = 0; i < paths.Count; i++)
                    if (!paths[i].IsEnd && !board.Contains(paths[i].Next)) paths[i] = paths[i].AsEnd();
            walls?.RemoveAll(item => !board.Contains(item.A) || !board.Contains(item.B));
            if (portals != null)
            {
                // 출구만 잘린 통로는 입구에서 멈춘다. 기본 중력으로 새 합류/순환이 생기는 것을 막는다.
                foreach (FlowPortal portal in portals)
                    if (board.Contains(portal.Entrance) && portal.HasExit && !board.Contains(portal.Exit) && paths != null &&
                        !paths.Exists(path => path.Coordinate.Equals(portal.Entrance)))
                        paths.Add(new FlowPathCell(portal.Entrance));
                portals.RemoveAll(item => !board.Contains(item.Entrance) || (item.HasExit && !board.Contains(item.Exit)));
            }
            arrivals?.RemoveAll(item => !board.Contains(item));
            if (merges != null)
            {
                foreach (FlowMerge merge in merges) merge.CropSources(board);
                merges.RemoveAll(item => !board.Contains(item.Coordinate) || (item.Sources != null && item.Sources.Count < 2));
            }
        }
    }
}
