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

    [Serializable]
    public struct CoverPlacementDefinition
    {
        [SerializeField] private BoardCoordinate coordinate;
        [SerializeField] private CoverKind kind;
        [SerializeField] private int durability;

        public BoardCoordinate Coordinate => coordinate;
        public CoverKind Kind => kind;
        public int Durability => durability;
    }
}
