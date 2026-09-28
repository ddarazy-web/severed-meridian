using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    public enum QueryActionKind { SwapMatch, SwapPower, SwapCombination, Activate }
    public enum ActionReason { Allowed, InvalidCoordinates, Wall, Immovable, NoNewMatch, MagnetCannotSwap, NoMagnetTarget }

    public sealed class ActionCandidate
    {
        public BoardCoordinate First { get; }
        public BoardCoordinate? Second { get; }
        public QueryActionKind Kind { get; }
        public ActionReason Reason { get; }
        public bool IsAllowed => Reason == ActionReason.Allowed;
        public RuntimeContent FirstContent { get; }
        public RuntimeContent? SecondContent { get; }
        public ReadOnlyCollection<MatchPattern> Matches { get; }
        public string Message => Reason switch
        {
            ActionReason.Allowed => Kind == QueryActionKind.SwapMatch ? "새 매칭 교환" : Kind == QueryActionKind.SwapCombination ? "파워 조합 교환" : Kind == QueryActionKind.Activate ? "제자리 파워 발동" : "파워 발동 교환",
            ActionReason.InvalidCoordinates => "인접한 활성 두 칸을 선택하세요.", ActionReason.Wall => "벽을 넘어 교환할 수 없습니다.",
            ActionReason.Immovable => "빈칸·고정 장애물·덮인 블록은 조작할 수 없습니다.", ActionReason.NoNewMatch => "교환으로 새 매칭이 생기지 않습니다.",
            ActionReason.MagnetCannotSwap => "자석은 고철·회수 부품과 교환할 수 없습니다.", _ => "자석이 선택할 일반 블록이 없습니다."
        };
        internal ActionCandidate(BoardCoordinate first, BoardCoordinate? second, QueryActionKind kind, ActionReason reason,
            RuntimeContent firstContent, RuntimeContent? secondContent, IEnumerable<MatchPattern> matches = null)
        {
            First = first; Second = second; Kind = kind; Reason = reason; FirstContent = firstContent; SecondContent = secondContent;
            Matches = Array.AsReadOnly(matches?.ToArray() ?? Array.Empty<MatchPattern>());
        }
    }

    public static class ActionQuery
    {
        public const string Version = "action-query-v1";
        private static bool Power(RuntimeContent content) => content >= RuntimeContent.Rocket && content <= RuntimeContent.Magnet;
        internal static bool Movable(LevelRuntimeState state, RuntimeCell cell) => cell.IsActive && !cell.Cover.HasValue &&
            (cell.Content == RuntimeContent.Normal || Power(cell.Content) || cell.Content == RuntimeContent.Recovery ||
             (cell.Content == RuntimeContent.Obstacle && state.Obstacles[cell.ObstacleIndex.Value].Definition.Kind == ObstacleKind.Scrap));
        public static bool HasMagnetTarget(LevelRuntimeState state) => state.Cells.Any(cell => cell.IsActive && cell.Content == RuntimeContent.Normal && cell.Color.HasValue && cell.Cover != CoverKind.Mold);

        public static ActionCandidate Swap(LevelRuntimeState state, BoardCoordinate first, BoardCoordinate second)
            => Swap(state, first, second, new HashSet<string>(MatchQuery.Find(state).Select(match => match.Key)));

        private static ActionCandidate Swap(LevelRuntimeState state, BoardCoordinate first, BoardCoordinate second, HashSet<string> before)
        {
            BoardQueryView view = new BoardQueryView(state);
            RuntimeContent a = RuntimeContent.Empty, b = RuntimeContent.Empty;
            ActionCandidate Result(ActionReason reason, QueryActionKind kind = QueryActionKind.SwapMatch, IEnumerable<MatchPattern> matches = null)
                => new ActionCandidate(first, second, kind, reason, a, b, matches);
            if (!view.Contains(first) || !view.Contains(second) || !new BoardEdge(first, second).IsAdjacent) return Result(ActionReason.InvalidCoordinates);
            RuntimeCell left = state.CellAt(first), right = state.CellAt(second); a = left.Content; b = right.Content;
            if (!left.IsActive || !right.IsActive) return Result(ActionReason.InvalidCoordinates);
            if (view.Wall(first, second)) return Result(ActionReason.Wall);
            if (!Movable(state, left) || !Movable(state, right)) return Result(ActionReason.Immovable);
            if (a == RuntimeContent.Magnet || b == RuntimeContent.Magnet)
            {
                RuntimeContent other = a == RuntimeContent.Magnet ? b : a;
                if (other == RuntimeContent.Recovery || other == RuntimeContent.Obstacle) return Result(ActionReason.MagnetCannotSwap);
                if (other != RuntimeContent.Magnet && !HasMagnetTarget(state)) return Result(ActionReason.NoMagnetTarget);
            }
            if (Power(a) || Power(b)) return Result(ActionReason.Allowed, Power(a) && Power(b) ? QueryActionKind.SwapCombination : QueryActionKind.SwapPower);
            if (a == b && (a != RuntimeContent.Normal || left.Color == right.Color)) return Result(ActionReason.NoNewMatch);
            BoardQueryView swapped = new BoardQueryView(state, first, second);
            if (!MatchQuery.HasMatchAt(swapped, first) && !MatchQuery.HasMatchAt(swapped, second)) return Result(ActionReason.NoNewMatch);
            MatchPattern[] created = MatchQuery.Find(swapped).Where(match => (match.Cells.Contains(first) || match.Cells.Contains(second)) && !before.Contains(match.Key)).ToArray();
            return created.Length == 0 ? Result(ActionReason.NoNewMatch) : Result(ActionReason.Allowed, QueryActionKind.SwapMatch, created);
        }

        public static ActionCandidate Activate(LevelRuntimeState state, BoardCoordinate coordinate)
        {
            BoardQueryView view = new BoardQueryView(state);
            if (!view.Contains(coordinate)) return new ActionCandidate(coordinate, null, QueryActionKind.Activate, ActionReason.InvalidCoordinates, RuntimeContent.Empty, null);
            RuntimeCell cell = state.CellAt(coordinate);
            ActionReason reason = !Movable(state, cell) || !Power(cell.Content) ? ActionReason.Immovable :
                cell.Content == RuntimeContent.Magnet && !HasMagnetTarget(state) ? ActionReason.NoMagnetTarget : ActionReason.Allowed;
            return new ActionCandidate(coordinate, null, QueryActionKind.Activate, reason, cell.Content, null);
        }

        public static ReadOnlyCollection<ActionCandidate> Find(LevelRuntimeState state)
        {
            List<ActionCandidate> result = new List<ActionCandidate>();
            HashSet<string> before = new HashSet<string>(MatchQuery.Find(state).Select(match => match.Key));
            foreach (RuntimeCell cell in state.Cells)
            {
                foreach (BoardCoordinate next in new[] { new BoardCoordinate(cell.Coordinate.Row, cell.Coordinate.Column + 1), new BoardCoordinate(cell.Coordinate.Row + 1, cell.Coordinate.Column) })
                { ActionCandidate swap = Swap(state, cell.Coordinate, next, before); if (swap.IsAllowed) result.Add(swap); }
                ActionCandidate activate = Activate(state, cell.Coordinate); if (activate.IsAllowed) result.Add(activate);
            }
            return result.AsReadOnly();
        }
    }
}
