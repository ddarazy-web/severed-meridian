using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    public enum MatchKind { Three, Drone, Rocket, Bomb, Magnet }

    public sealed class MatchPattern
    {
        public MatchKind Kind { get; }
        public RabbitColor Color { get; }
        public ReadOnlyCollection<BoardCoordinate> Cells { get; }
        public RocketDirection? RocketDirection { get; }
        public string Key { get; }
        internal MatchPattern(MatchKind kind, RabbitColor color, IEnumerable<BoardCoordinate> cells, RocketDirection? rocket = null)
        {
            Kind = kind; Color = color; RocketDirection = rocket;
            Cells = Array.AsReadOnly(cells.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToArray());
            Key = kind + ":" + (int)color + ":" + string.Join(";", Cells.Select(cell => cell.Row + "," + cell.Column));
        }
    }

    // 교환을 적용하지 않고 해당 좌표에서 보이는 점유자만 바꿔 읽는다. 바닥/벽은 원래 좌표에 남는다.
    internal sealed class BoardQueryView
    {
        internal readonly LevelRuntimeState State;
        private readonly BoardCoordinate? a, b;
        internal BoardQueryView(LevelRuntimeState state, BoardCoordinate? a = null, BoardCoordinate? b = null)
        { State = state ?? throw new ArgumentNullException(nameof(state)); this.a = a; this.b = b; }
        internal bool Contains(BoardCoordinate cell) => cell.Row >= 0 && cell.Row < State.Rows && cell.Column >= 0 && cell.Column < State.Columns;
        internal RuntimeCell At(BoardCoordinate cell) => State.CellAt(a.HasValue && cell.Equals(a.Value) ? b.Value : b.HasValue && cell.Equals(b.Value) ? a.Value : cell);
        internal RabbitColor? ColorAt(BoardCoordinate cell) => !Contains(cell) ? null :
            At(cell).IsActive && At(cell).Content == RuntimeContent.Normal && At(cell).Cover != CoverKind.Mold ? At(cell).Color : null;
        internal bool Wall(BoardCoordinate first, BoardCoordinate second) => State.Flow.Walls.Contains(new BoardEdge(first, second));
        internal bool Same(BoardCoordinate first, BoardCoordinate second) => ColorAt(first).HasValue && ColorAt(first) == ColorAt(second) && !Wall(first, second);
    }

    public static class MatchQuery
    {
        public const string Version = "match-query-v1";
        private static readonly (int, int)[] Square = { (0, 0), (0, 1), (1, 0), (1, 1) };
        private static readonly (int, int)[][] Bombs =
        {
            new[] { (0, 0), (0, 1), (0, 2), (1, 2), (2, 2) },
            new[] { (0, 2), (1, 2), (2, 0), (2, 1), (2, 2) },
            new[] { (0, 0), (1, 0), (2, 0), (2, 1), (2, 2) },
            new[] { (0, 0), (0, 1), (0, 2), (1, 0), (2, 0) },
            new[] { (0, 0), (0, 1), (0, 2), (1, 1), (2, 1) },
            new[] { (0, 2), (1, 0), (1, 1), (1, 2), (2, 2) },
            new[] { (0, 1), (1, 1), (2, 0), (2, 1), (2, 2) },
            new[] { (0, 0), (1, 0), (1, 1), (1, 2), (2, 0) }
        };
        public static ReadOnlyCollection<MatchPattern> Find(LevelRuntimeState state) => Find(new BoardQueryView(state));

        internal static ReadOnlyCollection<MatchPattern> Find(BoardQueryView view)
        {
            List<MatchPattern> found = new List<MatchPattern>();
            for (int row = 0; row < view.State.Rows; row++)
                for (int column = 0; column < view.State.Columns; column++)
                {
                    BoardCoordinate origin = new BoardCoordinate(row, column);
                    for (int axis = 0; axis < 2 && view.ColorAt(origin).HasValue; axis++)
                    {
                        int dr = axis, dc = 1 - axis;
                        if (view.Same(origin, new BoardCoordinate(row - dr, column - dc))) continue;
                        List<BoardCoordinate> run = new List<BoardCoordinate> { origin };
                        BoardCoordinate next = new BoardCoordinate(row + dr, column + dc);
                        while (view.Same(run[run.Count - 1], next)) { run.Add(next); next = new BoardCoordinate(next.Row + dr, next.Column + dc); }
                        if (run.Count >= 3) found.Add(new MatchPattern(run.Count >= 5 ? MatchKind.Magnet : run.Count == 4 ? MatchKind.Rocket : MatchKind.Three,
                            view.ColorAt(origin).Value, run, run.Count == 4 ? (axis == 0 ? Levels.RocketDirection.Vertical : Levels.RocketDirection.Horizontal) : (RocketDirection?)null));
                    }
                    AddShape(view, found, row, column, Square, MatchKind.Drone);
                    foreach ((int, int)[] shape in Bombs) AddShape(view, found, row, column, shape, MatchKind.Bomb);
                }
            return Array.AsReadOnly(found.GroupBy(pattern => pattern.Key).Select(group => group.First())
                .OrderByDescending(pattern => pattern.Kind).ThenBy(pattern => pattern.Key, StringComparer.Ordinal).ToArray());
        }

        private static void AddShape(BoardQueryView view, List<MatchPattern> found, int row, int column, (int, int)[] offsets, MatchKind kind)
        {
            RabbitColor? color = view.ColorAt(new BoardCoordinate(row + offsets[0].Item1, column + offsets[0].Item2));
            if (!color.HasValue) return;
            foreach ((int dr, int dc) in offsets)
                if (view.ColorAt(new BoardCoordinate(row + dr, column + dc)) != color) return;
            BoardCoordinate[] cells = offsets.Select(offset => new BoardCoordinate(row + offset.Item1, column + offset.Item2)).ToArray();
            for (int i = 0; i < cells.Length; i++)
                for (int j = i + 1; j < cells.Length; j++)
                    if (new BoardEdge(cells[i], cells[j]).IsAdjacent && view.Wall(cells[i], cells[j])) return;
            found.Add(new MatchPattern(kind, color.Value, cells));
        }

        // 모든 폭탄/장직선 패턴에는 직선 3개가 있다. 탐색 가지치기에는 3개와 2×2의 존재만 검사한다.
        internal static bool HasMatchAt(LevelRuntimeState state, BoardCoordinate cell)
            => HasMatchAt(new BoardQueryView(state), cell);

        internal static bool HasMatchAt(BoardQueryView view, BoardCoordinate cell)
        {
            if (!view.ColorAt(cell).HasValue) return false;
            for (int axis = 0; axis < 2; axis++)
                for (int offset = -2; offset <= 0; offset++)
                {
                    BoardCoordinate a = new BoardCoordinate(cell.Row + axis * offset, cell.Column + (1 - axis) * offset);
                    BoardCoordinate b = new BoardCoordinate(a.Row + axis, a.Column + 1 - axis), c = new BoardCoordinate(b.Row + axis, b.Column + 1 - axis);
                    if (view.Same(a, b) && view.Same(b, c)) return true;
                }
            for (int dr = -1; dr <= 0; dr++)
                for (int dc = -1; dc <= 0; dc++)
                {
                    BoardCoordinate a = new BoardCoordinate(cell.Row + dr, cell.Column + dc), b = new BoardCoordinate(a.Row, a.Column + 1);
                    BoardCoordinate c = new BoardCoordinate(a.Row + 1, a.Column), d = new BoardCoordinate(a.Row + 1, a.Column + 1);
                    if (view.Same(a, b) && view.Same(a, c) && view.Same(b, d) && view.Same(c, d)) return true;
                }
            return false;
        }
    }
}
