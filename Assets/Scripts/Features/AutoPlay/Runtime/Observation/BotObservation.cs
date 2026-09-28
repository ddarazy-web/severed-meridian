using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Levels;

namespace AutoPlay
{
    // Unknown은 빈칸이 아니다. 곰팡이 아래에 무엇이 있는지는 전략이 판단할 수 없다.
    public enum BotContent { Unknown, Empty, Normal, Rocket, Bomb, Drone, Magnet, Obstacle, Recovery }
    public enum BotActionKind { MatchSwap, PowerSwap, CombinationSwap, Activate }
    public enum BotMatchKind { Three, Drone, Rocket, Bomb, Magnet }

    /// <summary>화면에 드러난 한 칸의 값. 실행 상태나 정의 객체를 참조하지 않는다.</summary>
    public sealed class BotCell
    {
        public BoardCoordinate Coordinate { get; }
        public bool IsActive { get; }
        public BotContent Content { get; }
        public RabbitColor? Color { get; }
        public RocketDirection? RocketDirection { get; }
        public CoverKind? Cover { get; }
        public int CoverDurability { get; }
        public int DustDurability { get; }
        public int? BodyKey { get; }

        /// <param name="coordinate">보드 좌표.</param><param name="active">활성 칸 여부.</param>
        /// <param name="content">공개된 점유 종류.</param><param name="color">노출 색.</param>
        /// <param name="rocket">노출 로켓 방향.</param><param name="cover">덮개 종류.</param>
        /// <param name="coverDurability">덮개 잔여 내구도.</param><param name="dust">먼지 내구도.</param>
        /// <param name="bodyKey">보이는 본체의 좌표 기반 키.</param>
        internal BotCell(BoardCoordinate coordinate, bool active, BotContent content, RabbitColor? color,
            RocketDirection? rocket, CoverKind? cover, int coverDurability, int dust, int? bodyKey)
        {
            Coordinate = coordinate; IsActive = active; Content = content; Color = color;
            RocketDirection = rocket; Cover = cover; CoverDurability = coverDurability;
            DustDurability = dust; BodyKey = bodyKey;
        }
    }

    /// <summary>여러 칸에 걸친 본체도 한 번만 공개한다. 내부 배열 인덱스와 정의 ID는 포함하지 않는다.</summary>
    public sealed class BotBody
    {
        public int Key { get; }
        public ObstacleKind Kind { get; }
        public int Durability { get; }
        public RabbitColor? Color { get; }
        public int Charge { get; }
        public int RequiredCharge { get; }
        public ReadOnlyCollection<BoardCoordinate> Cells { get; }
        public ReadOnlyCollection<int> ConnectedTargets { get; }

        /// <param name="key">좌표 기반 공개 키.</param><param name="kind">본체 종류.</param>
        /// <param name="durability">현재 내구도.</param><param name="color">자물쇠의 지정 색.</param>
        /// <param name="charge">현재 충전.</param><param name="required">발전기의 표시된 충전 목표.</param>
        /// <param name="cells">화면에 보이는 점유 칸.</param><param name="targets">활성 전선의 공개 대상 키.</param>
        internal BotBody(int key, ObstacleKind kind, int durability, RabbitColor? color, int charge,
            int required, IEnumerable<BoardCoordinate> cells, IEnumerable<int> targets)
        {
            Key = key; Kind = kind; Durability = durability; Color = color; Charge = charge; RequiredCharge = required;
            Cells = Array.AsReadOnly(cells.ToArray()); ConnectedTargets = Array.AsReadOnly(targets.ToArray());
        }
    }

    public sealed class BotMission
    {
        public MissionKind Kind { get; }
        public RabbitColor? Color { get; }
        public int Remaining { get; }
        /// <param name="kind">목표 종류.</param><param name="color">색 수집 목표의 색.</param><param name="remaining">화면에 표시되는 남은 목표량.</param>
        internal BotMission(MissionKind kind, RabbitColor? color, int remaining)
        { Kind = kind; Color = color; Remaining = remaining; }
    }

    /// <summary>실행 전 매칭 후보. 겹치는 패턴은 후보이며 모두 제거된다고 보장하지 않는다.</summary>
    public sealed class BotMatch
    {
        public BotMatchKind Kind { get; }
        public RabbitColor Color { get; }
        public ReadOnlyCollection<BoardCoordinate> Cells { get; }
        /// <param name="kind">생성 가능한 파워 종류.</param><param name="color">매칭 색.</param><param name="cells">공개된 매칭 좌표.</param>
        internal BotMatch(BotMatchKind kind, RabbitColor color, IEnumerable<BoardCoordinate> cells)
        { Kind = kind; Color = color; Cells = Array.AsReadOnly(cells.ToArray()); }
    }

    /// <summary>전략이 선택할 수 있는 조작. 실행 엔진의 결과·난수·상태 참조를 보유하지 않는다.</summary>
    public sealed class BotAction
    {
        public BotActionKind Kind { get; }
        public BoardCoordinate First { get; }
        public BoardCoordinate? Second { get; }
        public ReadOnlyCollection<BotMatch> Matches { get; }
        /// <param name="kind">조작 종류.</param><param name="first">첫 칸.</param><param name="second">교환할 칸. 제자리 발동은 없음.</param><param name="matches">공개 매칭 후보.</param>
        internal BotAction(BotActionKind kind, BoardCoordinate first, BoardCoordinate? second, IEnumerable<BotMatch> matches)
        { Kind = kind; First = first; Second = second; Matches = Array.AsReadOnly(matches.ToArray()); }
    }

    /// <summary>기본 전략의 유일한 입력. 과거 관찰은 실행 판이 변해도 변하지 않는다.</summary>
    public sealed class BotObservation
    {
        public int Rows { get; }
        public int Columns { get; }
        public int MovesRemaining { get; }
        public ReadOnlyCollection<BotCell> Cells { get; }
        public ReadOnlyCollection<BotBody> Bodies { get; }
        public ReadOnlyCollection<BotMission> Missions { get; }
        public ReadOnlyCollection<BotAction> Actions { get; }
        public ReadOnlyCollection<BoardEdge> Walls { get; }
        public ReadOnlyCollection<BoardCoordinate> Arrivals { get; }
        // FlowPortal은 참조 없는 값 형식이다. 공급 목록이나 합류 우선순위는 관찰에 없다.
        public ReadOnlyCollection<FlowPortal> Portals { get; }

        /// <param name="rows">행 수.</param><param name="columns">열 수.</param><param name="moves">남은 이동.</param>
        /// <param name="cells">공개 칸.</param><param name="bodies">공개 본체.</param><param name="missions">공개 목표.</param>
        /// <param name="actions">안전한 후보.</param><param name="walls">눈에 보이는 벽.</param>
        /// <param name="arrivals">도착 바닥.</param><param name="portals">통로 입출구.</param>
        internal BotObservation(int rows, int columns, int moves, IEnumerable<BotCell> cells, IEnumerable<BotBody> bodies,
            IEnumerable<BotMission> missions, IEnumerable<BotAction> actions, IEnumerable<BoardEdge> walls,
            IEnumerable<BoardCoordinate> arrivals, IEnumerable<FlowPortal> portals)
        {
            Rows = rows; Columns = columns; MovesRemaining = moves;
            Cells = Array.AsReadOnly(cells.ToArray()); Bodies = Array.AsReadOnly(bodies.ToArray());
            Missions = Array.AsReadOnly(missions.ToArray()); Actions = Array.AsReadOnly(actions.ToArray());
            Walls = Array.AsReadOnly(walls.ToArray()); Arrivals = Array.AsReadOnly(arrivals.ToArray());
            Portals = Array.AsReadOnly(portals.ToArray());
        }

        /// <summary>관찰 내부의 칸을 조회한다. 보드 밖은 null이다.</summary>
        /// <param name="coordinate">조회 좌표.</param><returns>공개 칸 또는 null.</returns>
        public BotCell CellAt(BoardCoordinate coordinate) => coordinate.Row < 0 || coordinate.Row >= Rows ||
            coordinate.Column < 0 || coordinate.Column >= Columns ? null : Cells[coordinate.Row * Columns + coordinate.Column];
    }
}
