using System;
using Board;
using UnityEngine;

namespace Levels
{
    public enum ObstacleKind
    {
        [InspectorName("나무상자")] Crate = 0,
        [InspectorName("고철 뭉치")] Scrap = 1,
        [InspectorName("잠긴 고물 금고")] Safe = 2,
        [InspectorName("색깔 자물쇠")] ColorLock = 3,
        [InspectorName("대형 폐가전")] Appliance = 4,
        [InspectorName("고장 난 발전기")] Generator = 5
    }

    [Serializable]
    public struct ObstaclePlacementDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private BoardCoordinate coordinate;
        [SerializeField] private ObstacleKind kind;
        [SerializeField] private int durability;
        [SerializeField] private RabbitColor color;
        [SerializeField] private int requiredCharge;

        public BoardCoordinate Coordinate => coordinate;
        public string Id => id;
        public ObstacleKind Kind => kind;
        public int Durability => durability;
        public RabbitColor Color => color;
        public int RequiredCharge => requiredCharge;

        internal ObstaclePlacementDefinition(string id, BoardCoordinate coordinate, ObstacleKind kind, int durability)
        {
            this.id = id; this.coordinate = coordinate; this.kind = kind; this.durability = durability;
            color = default; requiredCharge = 0;
        }
    }
}
