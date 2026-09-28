using System;
using System.Collections.Generic;
using Board;
using UnityEngine;

namespace Levels
{
    public enum GravityDirection { Down, Up, Left, Right }

    [Serializable]
    public struct GravityCell
    {
        [SerializeField] private BoardCoordinate coordinate;
        [SerializeField] private GravityDirection direction;
        public BoardCoordinate Coordinate => coordinate;
        public GravityDirection Direction => direction;
    }

    [Serializable]
    public struct FlowPathCell
    {
        [SerializeField] private BoardCoordinate coordinate;
        [SerializeField] private bool isEnd;
        [SerializeField] private BoardCoordinate next;
        public BoardCoordinate Coordinate => coordinate;
        public bool IsEnd => isEnd;
        public BoardCoordinate Next => next;
    }

    [Serializable]
    public struct FlowMerge
    {
        [SerializeField] private BoardCoordinate coordinate;
        [SerializeField] private List<BoardCoordinate> sources;
        public BoardCoordinate Coordinate => coordinate;
        public IReadOnlyList<BoardCoordinate> Sources => sources;
    }

    [Serializable]
    public struct FlowPortal
    {
        [SerializeField] private BoardCoordinate entrance;
        [SerializeField] private bool hasExit;
        [SerializeField] private BoardCoordinate exit;
        public BoardCoordinate Entrance => entrance;
        public bool HasExit => hasExit;
        public BoardCoordinate Exit => exit;
    }

    [Serializable]
    public sealed class LevelFlowDefinition
    {
        [SerializeField] private List<GravityCell> gravity = new List<GravityCell>();
        [SerializeField] private List<FlowPathCell> paths = new List<FlowPathCell>();
        [SerializeField] private List<FlowMerge> merges = new List<FlowMerge>();
        [SerializeField] private List<BoardEdge> walls = new List<BoardEdge>();
        [SerializeField] private List<FlowPortal> portals = new List<FlowPortal>();
        [SerializeField] private List<BoardCoordinate> arrivals = new List<BoardCoordinate>();
        public IReadOnlyList<GravityCell> Gravity => gravity;
        public IReadOnlyList<FlowPathCell> Paths => paths;
        public IReadOnlyList<FlowMerge> Merges => merges;
        public IReadOnlyList<BoardEdge> Walls => walls;
        public IReadOnlyList<FlowPortal> Portals => portals;
        public IReadOnlyList<BoardCoordinate> Arrivals => arrivals;
        public bool ListsPresent => gravity != null && paths != null && merges != null && walls != null && portals != null && arrivals != null;
        public bool HasRecords => (gravity?.Count ?? 0) + (paths?.Count ?? 0) + (merges?.Count ?? 0) +
            (walls?.Count ?? 0) + (portals?.Count ?? 0) + (arrivals?.Count ?? 0) > 0;
    }
}
