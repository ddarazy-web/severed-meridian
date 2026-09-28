using System;
using Board;
using UnityEngine;

namespace Levels
{
    [Serializable]
    public struct DustPlacementDefinition
    {
        [SerializeField] private BoardCoordinate coordinate;
        [SerializeField] private int durability;

        public BoardCoordinate Coordinate => coordinate;
        public int Durability => durability;
    }
}
