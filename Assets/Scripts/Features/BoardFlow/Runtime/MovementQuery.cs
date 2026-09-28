using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    public enum MovementKind { Gravity, Path, Portal, Diagonal, Supply }
    public enum MovementReason { Allowed, Immovable, End, Outside, Wall, Occupied, PrimaryFirst, DifferentRegion }

    public sealed class MovementCandidate
    {
        public BoardCoordinate Source { get; }
        public BoardCoordinate Target { get; }
        public MovementKind Kind { get; }
        public MovementReason Reason { get; }
        public bool IsAllowed => Reason == MovementReason.Allowed;
        public string Message => Reason switch
        {
            MovementReason.Allowed => "이동 가능", MovementReason.Immovable => "이동 불가 점유자",
            MovementReason.End => "경로 끝칸", MovementReason.Outside => "보드 밖 또는 비활성 칸",
            MovementReason.Wall => "벽 차단", MovementReason.Occupied => "목적지 점유 중",
            MovementReason.PrimaryFirst => "직선·경로·통로 이동 우선", _ => "대각선 구역 제한"
        };
        internal MovementCandidate(BoardCoordinate source, BoardCoordinate target, MovementKind kind, MovementReason reason)
        { Source = source; Target = target; Kind = kind; Reason = reason; }
    }

    public static class MovementQuery
    {
        public const string Version = "movement-query-recovery-v3";
        internal static bool Active(LevelRuntimeState state, BoardCoordinate coordinate) => coordinate.Row >= 0 && coordinate.Row < state.Rows &&
            coordinate.Column >= 0 && coordinate.Column < state.Columns && state.CellAt(coordinate).IsActive;
        private static bool Wall(LevelRuntimeState state, BoardCoordinate a, BoardCoordinate b) => state.Flow.Walls.Contains(new BoardEdge(a, b));
        private static bool Movable(LevelRuntimeState state, RuntimeCell cell) => cell.IsActive && !cell.Cover.HasValue &&
            ((cell.Content >= RuntimeContent.Normal && cell.Content <= RuntimeContent.Magnet) || cell.Content == RuntimeContent.Recovery ||
             (cell.Content == RuntimeContent.Obstacle && cell.ObstacleIndex.HasValue && state.Obstacles[cell.ObstacleIndex.Value].Definition.Kind == ObstacleKind.Scrap));

        // 정의의 Next와 같은 통로→직접 경로→중력 순서로 점유를 제외한 목적지를 읽는다.
        internal static MovementCandidate Next(LevelRuntimeState state, BoardCoordinate source)
        {
            BoardCoordinate target = source; MovementKind kind = MovementKind.Gravity;
            FlowPortal[] portals = state.Flow.Portals.Where(p => p.Entrance.Equals(source)).ToArray();
            FlowPathCell[] paths = state.Flow.Paths.Where(p => p.Coordinate.Equals(source)).ToArray();
            if (portals.Length > 0) { target = portals[0].Exit; kind = MovementKind.Portal; }
            else if (paths.Length > 0)
            {
                if (paths[0].IsEnd) return new MovementCandidate(source, source, MovementKind.Path, MovementReason.End);
                target = paths[0].Next; kind = MovementKind.Path;
            }
            else
            {
                GravityDirection gravity = state.CellAt(source).Gravity;
                target = gravity switch
                {
                    GravityDirection.Up => new BoardCoordinate(source.Row - 1, source.Column),
                    GravityDirection.Left => new BoardCoordinate(source.Row, source.Column - 1),
                    GravityDirection.Right => new BoardCoordinate(source.Row, source.Column + 1),
                    _ => new BoardCoordinate(source.Row + 1, source.Column)
                };
            }
            return new MovementCandidate(source, target, kind, !Active(state, target) ? MovementReason.Outside :
                kind != MovementKind.Portal && Wall(state, source, target) ? MovementReason.Wall : MovementReason.Allowed);
        }

        public static ReadOnlyCollection<MovementCandidate> Find(LevelRuntimeState state, bool diagonal = false)
        {
            List<MovementCandidate> found = new List<MovementCandidate>();
            HashSet<BoardCoordinate> primaryTargets = diagonal ? new HashSet<BoardCoordinate>(Find(state).Where(c => c.IsAllowed).Select(c => c.Target)) : null;
            foreach (RuntimeCell cell in state.Cells.Where(c => c.IsActive && c.Content != RuntimeContent.Empty))
            {
                MovementCandidate next = Next(state, cell.Coordinate);
                bool canMove = Movable(state, cell);
                if (!diagonal)
                {
                    found.Add(new MovementCandidate(next.Source, next.Target, next.Kind, !canMove ? MovementReason.Immovable :
                        !next.IsAllowed ? next.Reason : state.CellAt(next.Target).Content != RuntimeContent.Empty ? MovementReason.Occupied : MovementReason.Allowed));
                    continue;
                }
                int dr = cell.Gravity == GravityDirection.Up ? -1 : cell.Gravity == GravityDirection.Down ? 1 : 0;
                int dc = cell.Gravity == GravityDirection.Left ? -1 : cell.Gravity == GravityDirection.Right ? 1 : 0;
                foreach (int side in new[] { -1, 1 })
                {
                    BoardCoordinate target = new BoardCoordinate(cell.Coordinate.Row + dr + dc * side, cell.Coordinate.Column + dc + dr * side);
                    MovementReason reason;
                    if (!canMove) reason = MovementReason.Immovable;
                    else if (next.Kind != MovementKind.Gravity || (next.IsAllowed && state.CellAt(next.Target).Content == RuntimeContent.Empty) || primaryTargets.Contains(target)) reason = MovementReason.PrimaryFirst;
                    else if (!Active(state, target)) reason = MovementReason.Outside;
                    else if (state.CellAt(target).Gravity != cell.Gravity || state.Flow.Paths.Any(p => p.Coordinate.Equals(target))) reason = MovementReason.DifferentRegion;
                    else if (state.CellAt(target).Content != RuntimeContent.Empty) reason = MovementReason.Occupied;
                    else
                    {
                        BoardCoordinate horizontal = new BoardCoordinate(cell.Coordinate.Row, target.Column), vertical = new BoardCoordinate(target.Row, cell.Coordinate.Column);
                        bool routeA = !Wall(state, cell.Coordinate, horizontal) && !Wall(state, horizontal, target);
                        bool routeB = !Wall(state, cell.Coordinate, vertical) && !Wall(state, vertical, target);
                        reason = routeA || routeB ? MovementReason.Allowed : MovementReason.Wall;
                    }
                    found.Add(new MovementCandidate(cell.Coordinate, target, MovementKind.Diagonal, reason));
                }
            }
            return found.AsReadOnly();
        }

        internal static string ValidateFlow(LevelRuntimeState state)
        {
            if (state.Flow.Paths.GroupBy(p => p.Coordinate).Any(g => g.Count() != 1) || state.Flow.Portals.GroupBy(p => p.Entrance).Any(g => g.Count() != 1)) return "중복 경로/통로";
            foreach (FlowPathCell path in state.Flow.Paths)
                if (!Active(state, path.Coordinate) || (!path.IsEnd && (!new BoardEdge(path.Coordinate, path.Next).IsAdjacent || !Next(state, path.Coordinate).IsAllowed))) return "잘못된 직접 경로";
            foreach (FlowPortal portal in state.Flow.Portals)
                if (!portal.HasExit || !Active(state, portal.Entrance) || !Active(state, portal.Exit)) return "잘못된 통로";
            if (state.Cells.Any(c => c.IsActive && !Enum.IsDefined(typeof(GravityDirection), c.Gravity))) return "잘못된 중력";
            Dictionary<BoardCoordinate, BoardCoordinate> graph = state.Cells.Where(c => c.IsActive).Select(c => Next(state, c.Coordinate))
                .Where(c => c.IsAllowed).ToDictionary(c => c.Source, c => c.Target);
            foreach (BoardCoordinate start in graph.Keys)
            {
                HashSet<BoardCoordinate> chain = new HashSet<BoardCoordinate>(); BoardCoordinate current = start;
                while (graph.TryGetValue(current, out BoardCoordinate next))
                { if (!chain.Add(current)) return "중력·경로·통로 순환"; current = next; }
            }
            var incoming = graph.GroupBy(p => p.Value).ToDictionary(g => g.Key, g => g.Select(p => p.Key).ToArray());
            foreach (var pair in incoming.Where(p => p.Value.Length > 1))
            {
                RuntimeMerge[] merges = state.Flow.Merges.Where(m => m.Coordinate.Equals(pair.Key)).ToArray();
                if (merges.Length != 1 || merges[0].Sources.Count != pair.Value.Length || !new HashSet<BoardCoordinate>(pair.Value).SetEquals(merges[0].Sources)) return "합류 우선순위 누락/불일치";
            }
            if (state.Flow.Merges.Any(m => !incoming.TryGetValue(m.Coordinate, out BoardCoordinate[] sources) || sources.Length < 2)) return "유효하지 않은 합류";
            return null;
        }
    }
}
