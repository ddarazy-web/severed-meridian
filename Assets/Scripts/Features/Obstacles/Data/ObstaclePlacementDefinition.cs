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

        /// <summary>에셋을 만들지 않고 본체 값을 구성한다. 기존 공급 호출은 색·충전의 기본값을 유지한다.</summary>
        /// <param name="id">이 판 내부에서 연결을 구별할 ID.</param><param name="coordinate">본체 기준 칸.</param>
        /// <param name="kind">장애물 종류.</param><param name="durability">현재 내구도.</param>
        /// <param name="color">색 자물쇠의 공개 색.</param><param name="requiredCharge">발전기의 공개 충전 목표.</param>
        internal ObstaclePlacementDefinition(string id, BoardCoordinate coordinate, ObstacleKind kind, int durability,
            RabbitColor color = default, int requiredCharge = 0)
        {
            this.id = id; this.coordinate = coordinate; this.kind = kind; this.durability = durability;
            this.color = color; this.requiredCharge = requiredCharge;
        }
    }
}
