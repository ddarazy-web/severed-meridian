using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;
using Simulation;

namespace AutoPlay
{
    /// <summary>
    /// 가정 전용 규칙 어댑터. 외부 입력은 공개 관찰뿐이며 실제 세션/정의/난수를 받지 않는다.
    /// 실행 상태는 private이고, 탐색 정책에는 공개 결과와 가치만 돌려준다.
    /// </summary>
    internal sealed class PlanningBranch
    {
        internal const string Version = "public-assumption-v1";
        internal const string Assumptions = "아래 중력·일반 5색 공급·숨은 내용은 가정 · 실제 미래 결과 아님";
        private readonly BoardActionExecutor executor;
        internal bool Pending => executor.HasPendingCascade && executor.Outcome == null;
        internal bool Terminal => executor.Outcome != null;
        internal bool Failed { get; private set; }
        internal string Failure { get; private set; }
        internal bool Won => executor.Outcome?.Kind == BoardOutcomeKind.Won;

        /// <param name="executor">이 어댑터가 새로 만든 가정 실행기만 허용한다.</param>
        private PlanningBranch(BoardActionExecutor executor) { this.executor = executor; }

        /// <summary>공개 값의 정규화된 정수열로 시드를 만든다. 시간·프로세스 해시·실제 시드는 쓰지 않는다.</summary>
        /// <param name="board">공개 값.</param><param name="sample">같은 공개 상태에서의 표본 번호.</param><returns>가정 시드.</returns>
        internal static int Seed(BotObservation board, int sample)
        {
            uint hash = 2166136261;
            void Put(int value) { unchecked { hash = (hash ^ (uint)value) * 16777619; } }
            Put(1); Put(board.Rows); Put(board.Columns); Put(board.MovesRemaining);
            foreach (BotCell cell in board.Cells.OrderBy(c => c.Coordinate.Row).ThenBy(c => c.Coordinate.Column))
            {
                Put(cell.Coordinate.Row); Put(cell.Coordinate.Column); Put(cell.IsActive ? 1 : 0); Put((int)cell.Content);
                Put(cell.Color.HasValue ? (int)cell.Color.Value : -1); Put(cell.RocketDirection.HasValue ? (int)cell.RocketDirection.Value : -1);
                Put(cell.Cover.HasValue ? (int)cell.Cover.Value : -1); Put(cell.CoverDurability); Put(cell.DustDurability); Put(cell.BodyKey ?? -1);
            }
            foreach (BotBody body in board.Bodies.OrderBy(b => b.Key))
            {
                Put(body.Key); Put((int)body.Kind); Put(body.Durability); Put(body.Color.HasValue ? (int)body.Color.Value : -1);
                Put(body.Charge); Put(body.RequiredCharge); Put(body.Cells.Count);
                foreach (BoardCoordinate at in body.Cells.OrderBy(c => c.Row).ThenBy(c => c.Column)) { Put(at.Row); Put(at.Column); }
                Put(body.ConnectedTargets.Count); foreach (int target in body.ConnectedTargets.OrderBy(t => t)) Put(target);
            }
            foreach (BotMission mission in board.Missions.OrderBy(m => m.Kind).ThenBy(m => m.Color))
            { Put((int)mission.Kind); Put(mission.Color.HasValue ? (int)mission.Color.Value : -1); Put(mission.Remaining); }
            foreach (var wall in board.Walls.Select(w => (Low: Math.Min(w.A.Row * board.Columns + w.A.Column, w.B.Row * board.Columns + w.B.Column),
                High: Math.Max(w.A.Row * board.Columns + w.A.Column, w.B.Row * board.Columns + w.B.Column))).OrderBy(w => w.Low).ThenBy(w => w.High))
            { Put(wall.Low); Put(wall.High); }
            foreach (FlowPortal portal in board.Portals.OrderBy(p => p.Entrance.Row).ThenBy(p => p.Entrance.Column))
            { Put(portal.Entrance.Row); Put(portal.Entrance.Column); Put(portal.HasExit ? 1 : 0); Put(portal.Exit.Row); Put(portal.Exit.Column); }
            foreach (BoardCoordinate at in board.Arrivals.OrderBy(c => c.Row).ThenBy(c => c.Column)) { Put(at.Row); Put(at.Column); }
            Put(sample); return unchecked((int)hash);
        }

        /// <summary>
        /// 최대 10×10의 공개 판을 값으로 구성한다. 초기 레벨 생성기를 호출하지 않아
        /// 현재 빈칸·미션·발전기 충전·내구도가 초기값으로 바뀌지 않는다.
        /// 가정 생성구는 벽/비활성 칸으로 끊긴 각 세로 구간의 맨 위다. 통로 출구는 제외한다.
        /// </summary>
        /// <param name="board">안정 경계의 공개 관찰.</param><param name="sample">가정 표본 번호.</param><returns>독립 가정 분기.</returns>
        internal static PlanningBranch Create(BotObservation board, int sample)
        {
            int seed = Seed(board, sample);
            Random hidden = new Random(seed);
            RabbitColor[] colors = (RabbitColor[])Enum.GetValues(typeof(RabbitColor));
            BotBody[] bodies = board.Bodies.OrderBy(body => body.Key).ToArray();
            Dictionary<int, int> indices = bodies.Select((body, index) => (body.Key, index)).ToDictionary(item => item.Key, item => item.index);
            RuntimeObstacle[] obstacles = bodies.Select(body => new RuntimeObstacle(new ObstaclePlacementDefinition(
                "visible-" + body.Key, body.Cells.OrderBy(c => c.Row).ThenBy(c => c.Column).First(), body.Kind,
                body.Durability, body.Color ?? RabbitColor.Type1, body.RequiredCharge)) { Charge = body.Charge }).ToArray();
            RuntimeCell[] cells = board.Cells.OrderBy(c => c.Coordinate.Row).ThenBy(c => c.Coordinate.Column).Select(cell =>
            {
                RuntimeContent content = cell.Content switch {
                    BotContent.Normal or BotContent.Unknown => RuntimeContent.Normal, BotContent.Rocket => RuntimeContent.Rocket,
                    BotContent.Bomb => RuntimeContent.Bomb, BotContent.Drone => RuntimeContent.Drone, BotContent.Magnet => RuntimeContent.Magnet,
                    BotContent.Obstacle => RuntimeContent.Obstacle, BotContent.Recovery => RuntimeContent.Recovery, _ => RuntimeContent.Empty };
                return new RuntimeCell(cell.Coordinate, cell.IsActive) { Content = content,
                    Color = cell.Content == BotContent.Unknown ? colors[hidden.Next(colors.Length)] : cell.Color,
                    RocketDirection = cell.RocketDirection, Cover = cell.Cover, CoverDurability = cell.CoverDurability,
                    DustDurability = cell.DustDurability, ObstacleIndex = cell.BodyKey.HasValue ? indices[cell.BodyKey.Value] : (int?)null };
            }).ToArray();
            RuntimeMission[] missions = board.Missions.Select(mission => new RuntimeMission(new LevelMissionDefinition(
                mission.Kind, mission.Color ?? RabbitColor.Type1, mission.Remaining))).ToArray();
            List<BoardCoordinate> sources = new List<BoardCoordinate>();
            foreach (BotCell cell in board.Cells.Where(c => c.IsActive))
            {
                BoardCoordinate above = new BoardCoordinate(cell.Coordinate.Row - 1, cell.Coordinate.Column);
                if ((board.CellAt(above)?.IsActive != true || board.Walls.Contains(new BoardEdge(above, cell.Coordinate))) &&
                    !board.Portals.Any(p => p.HasExit && p.Exit.Equals(cell.Coordinate))) sources.Add(cell.Coordinate);
            }
            RuntimeConnection[] connections = bodies.Where(b => b.Kind == ObstacleKind.Generator)
                .SelectMany(body => body.ConnectedTargets.Select(key => new RuntimeConnection("visible-" + body.Key, "visible-" + key))).ToArray();
            LevelRuntimeState state = new LevelRuntimeState(board.Rows, board.Columns, board.MovesRemaining, cells, obstacles, missions,
                new RuntimeFlow(board.Walls, board.Portals, board.Arrivals), new RuntimeSupply(sources.OrderBy(c => c.Row).ThenBy(c => c.Column)), connections, seed);
            return new PlanningBranch(new BoardActionExecutor(state, 1, null));
        }

        /// <summary>가정 내부에서만 안정 분기를 복사한다. 공개 관찰로 시작한 계보를 벗어나지 않는다.</summary>
        /// <returns>가정 이력/난수까지 독립 복사한 분기.</returns>
        internal PlanningBranch Fork()
        {
            if (Pending || Terminal || Failed) throw new InvalidOperationException("안정된 미종료 가정만 분기할 수 있습니다.");
            return new PlanningBranch(new BoardActionExecutor(executor.State, executor.Turn, executor.TurnEffects));
        }

        /// <param name="action">공개 후보 좌표.</param><returns>가정 규칙이 수락했는지 여부.</returns>
        internal bool Apply(BotAction action)
        {
            BoardActionResult result = action.Kind == BotActionKind.Activate ? executor.Activate(action.First) : executor.Swap(action.First, action.Second.Value);
            if (!result.IsApplied) throw new InvalidOperationException("공개 후보와 가정 실행의 계약 불일치: " + result.Message);
            return true;
        }

        /// <summary>공통 후속 처리 한 단계만 실행한다. 예상의 처리 한도와 코드 오류를 분리한다.</summary>
        internal void Advance()
        {
            if (!Pending) return;
            CascadeStepResult step = executor.AdvanceCascade();
            if (!step.IsApplied || step.Reason == CascadeStepReason.Aborted)
            {
                if (step.Reason == CascadeStepReason.Unsupported || step.Reason == CascadeStepReason.WrongPhase)
                    throw new InvalidOperationException("가정 실행 오류: " + step.Message);
                Failed = true; Failure = "가정 처리 불가: " + step.Message;
            }
        }

        /// <returns>안정 가정의 공개 관찰만 반환한다.</returns>
        internal BotObservation Observe() => BotObservationBuilder.Capture(executor);

        /// <summary>첫 수부터 누적된 목표 감소와 이동 비용을 한 번만 계산한다. 라스트팡 보너스는 비교하지 않는다.</summary>
        /// <param name="initialRemaining">원래 공개 관찰의 남은 목표 합계.</param><param name="initialMoves">원래 이동 수.</param><returns>정수 비교 가치.</returns>
        internal long Value(int initialRemaining, int initialMoves)
        {
            int remaining = executor.State.Missions.Sum(m => m.Remaining);
            return (Won ? 1000000000L : 0) + (initialRemaining - remaining) * 10000L -
                (initialMoves - executor.State.MovesRemaining) * 10L;
        }
    }
}
