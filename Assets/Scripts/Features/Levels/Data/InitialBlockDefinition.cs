using MemoryPack;
using System;
using Board;
using UnityEngine;

namespace Levels
{
    // 아트 색값과 독립된 저장 식별자다. 출시 후 값의 순서를 변경하지 않는다.
    public enum RabbitColor
    {
        [InspectorName("달토끼 1")] Type1 = 0,
        [InspectorName("달토끼 2")] Type2 = 1,
        [InspectorName("달토끼 3")] Type3 = 2,
        [InspectorName("달토끼 4")] Type4 = 3,
        [InspectorName("달토끼 5")] Type5 = 4
    }

    public enum InitialBlockKind
    {
        [InspectorName("무작위 일반 블록 (?)")] RandomNormal = 0,
        [InspectorName("고정 색 일반 블록")] FixedNormal = 1,
        [InspectorName("청소로켓")] Rocket = 2,
        [InspectorName("달폭탄")] Bomb = 3,
        [InspectorName("수거드론")] Drone = 4,
        [InspectorName("무지개 자석")] Magnet = 5
    }

    public enum RocketDirection
    {
        [InspectorName("가로 한 줄")] Horizontal = 0,
        [InspectorName("세로 한 줄")] Vertical = 1
    }

    [Serializable, MemoryPackable(SerializeLayout.Explicit)]
    public partial struct InitialBlockDefinition
    {
        [SerializeField, MemoryPackInclude, MemoryPackOrder(0)] private BoardCoordinate coordinate;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(1)] private InitialBlockKind kind;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(2)] private RabbitColor fixedColor;
        [SerializeField, MemoryPackInclude, MemoryPackOrder(3)] private RocketDirection rocketDirection;

        [MemoryPackIgnore] public BoardCoordinate Coordinate => coordinate;
        [MemoryPackIgnore] public InitialBlockKind Kind => kind;
        [MemoryPackIgnore] public RocketDirection RocketDirection => rocketDirection;

        // 무작위 유형에서 이전 고정 색을 보존하되 실행 의미를 부여하지 않는다.
        [MemoryPackIgnore] public RabbitColor? FixedColor => kind == InitialBlockKind.FixedNormal ? fixedColor : null;

        internal InitialBlockDefinition(BoardCoordinate coordinate, InitialBlockKind kind, RabbitColor color, RocketDirection direction)
        { this.coordinate = coordinate; this.kind = kind; fixedColor = color; rocketDirection = direction; }
    }
}
