using System;
using System.Collections.Generic;
using System.Linq;
using Board;

namespace Levels
{
    public static class LevelFlowRules
    {
        public static bool Active(LevelDefinition level, BoardCoordinate cell) =>
            level?.Board != null && level.Board.TryGetCell(cell, out CellDefinition value) && value.IsActive;

        public static bool HasWall(LevelDefinition level, BoardCoordinate a, BoardCoordinate b) =>
            level?.Flow?.Walls != null && level.Flow.Walls.Contains(new BoardEdge(a, b));

        public static GravityDirection GravityAt(LevelDefinition level, BoardCoordinate cell)
        {
            if (level?.Flow?.Gravity == null) return GravityDirection.Down;
            GravityDirection direction = GravityDirection.Down;
            bool found = false;
            for (int i = 0; i < level.Flow.Gravity.Count; i++)
            {
                GravityCell item = level.Flow.Gravity[i];
                if (!item.Coordinate.Equals(cell)) continue;
                if (found) return GravityDirection.Down;
                direction = item.Direction;
                found = true;
            }
            return direction;
        }

        public static bool Next(LevelDefinition level, BoardCoordinate cell, out BoardCoordinate next)
        {
            next = default;
            if (!Active(level, cell) || level.Flow == null || !level.Flow.ListsPresent) return false;
            FlowPortal[] portals = level.Flow.Portals.Where(item => item.Entrance.Equals(cell)).ToArray();
            if (portals.Length > 0)
            {
                if (portals.Length != 1 || !portals[0].HasExit || !Active(level, portals[0].Exit)) return false;
                next = portals[0].Exit;
                return true;
            }
            FlowPathCell[] paths = level.Flow.Paths.Where(item => item.Coordinate.Equals(cell)).ToArray();
            if (paths.Length > 0)
            {
                if (paths.Length != 1 || paths[0].IsEnd) return false;
                next = paths[0].Next;
                return new BoardEdge(cell, next).IsAdjacent && Active(level, next) && !HasWall(level, cell, next);
            }
            if (level.Flow.Gravity.Count(item => item.Coordinate.Equals(cell)) > 1) return false;
            GravityDirection direction = GravityAt(level, cell);
            if (!Enum.IsDefined(typeof(GravityDirection), direction)) return false;
            next = direction switch
            {
                GravityDirection.Up => new BoardCoordinate(cell.Row - 1, cell.Column),
                GravityDirection.Left => new BoardCoordinate(cell.Row, cell.Column - 1),
                GravityDirection.Right => new BoardCoordinate(cell.Row, cell.Column + 1),
                _ => new BoardCoordinate(cell.Row + 1, cell.Column)
            };
            return Active(level, next) && !HasWall(level, cell, next);
        }

        public static Dictionary<BoardCoordinate, BoardCoordinate> Graph(LevelDefinition level)
        {
            Dictionary<BoardCoordinate, BoardCoordinate> graph = new Dictionary<BoardCoordinate, BoardCoordinate>();
            // 현재 에디터의 지원 크기만 검사한다. 잘못된 크기로 거대한 탐색을 시작하지 않는다.
            if (level?.Board == null || level.Board.Rows != BoardDefinition.DefaultRows || level.Board.Columns != BoardDefinition.DefaultColumns) return graph;
            for (int row = 0; row < BoardDefinition.DefaultRows; row++)
                for (int column = 0; column < BoardDefinition.DefaultColumns; column++)
                {
                    BoardCoordinate cell = new BoardCoordinate(row, column);
                    if (Next(level, cell, out BoardCoordinate next)) graph[cell] = next;
                }
            return graph;
        }

        public static List<BoardCoordinate> Sources(LevelDefinition level, BoardCoordinate destination) =>
            Graph(level).Where(pair => pair.Value.Equals(destination)).Select(pair => pair.Key)
                .OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToList();

        public static bool InternalWall(BoardEdge wall, BoardCoordinate origin, int size) => size == 2 &&
            LevelPlacementRules.Footprint(origin, size).Contains(wall.A) && LevelPlacementRules.Footprint(origin, size).Contains(wall.B);

        public static string WallError(LevelDefinition level, BoardEdge wall)
        {
            if (!wall.IsAdjacent || !Active(level, wall.A) || !Active(level, wall.B)) return "벽은 활성 인접 칸 사이에 설치하세요.";
            if (level.Obstacles != null && level.Obstacles.Any(item => InternalWall(wall, item.Coordinate, LevelPlacementRules.Size(item.Kind))))
                return "2×2 본체 내부에는 벽을 놓을 수 없습니다.";
            if (level.Flow?.Paths != null && level.Flow.Paths.Any(item => !item.IsEnd && wall.Equals(new BoardEdge(item.Coordinate, item.Next))))
                return "직접 경로가 지나는 경계입니다.";
            BoardEdge segment = WallSegment(wall);
            if (level.Connections != null && level.Connections.Any(item => Segments(item.Vertices).Contains(segment)))
                return "전선이 지나는 경계입니다.";
            return null;
        }

        public static BoardEdge WallSegment(BoardEdge wall)
        {
            if (wall.A.Row == wall.B.Row)
            {
                int column = Math.Max(wall.A.Column, wall.B.Column);
                return new BoardEdge(new BoardCoordinate(wall.A.Row, column), new BoardCoordinate(wall.A.Row + 1, column));
            }
            int row = Math.Max(wall.A.Row, wall.B.Row);
            return new BoardEdge(new BoardCoordinate(row, wall.A.Column), new BoardCoordinate(row, wall.A.Column + 1));
        }

        public static IEnumerable<BoardEdge> Segments(IReadOnlyList<BoardCoordinate> vertices)
        {
            if (vertices == null) yield break;
            for (int i = 1; i < vertices.Count; i++) yield return new BoardEdge(vertices[i - 1], vertices[i]);
        }

        public static bool HasNewData(LevelDefinition level) => (level.Flow?.HasRecords ?? false) ||
            (level.Connections?.Count ?? 0) > 0 || (level.Obstacles?.Any(item => !string.IsNullOrEmpty(item.Id)) ?? false);

        public static void Validate(LevelDefinition level, List<LevelValidationIssue> issues)
        {
            if (level.SchemaVersion < 3)
            {
                if (HasNewData(level)) Add(issues, LevelValidationCode.UnexpectedLegacyData, "구버전에 4단계 데이터가 있습니다. 자동 전환하지 않습니다.", "flow");
                return;
            }
            LevelFlowDefinition flow = level.Flow;
            if (flow == null || !flow.ListsPresent)
            {
                Add(issues, LevelValidationCode.InvalidFlow, "흐름 설정/목록이 누락되었습니다.", "flow");
                return;
            }
            HashSet<BoardCoordinate> used = new HashSet<BoardCoordinate>();
            for (int i = 0; i < flow.Gravity.Count; i++)
            {
                GravityCell item = flow.Gravity[i];
                if (!Active(level, item.Coordinate) || !Enum.IsDefined(typeof(GravityDirection), item.Direction) || !used.Add(item.Coordinate))
                    Add(issues, LevelValidationCode.InvalidFlow, "중력 좌표·방향·중복을 확인하세요.", $"flow.gravity.Array.data[{i}]", item.Coordinate);
            }
            used.Clear();
            for (int i = 0; i < flow.Paths.Count; i++)
            {
                FlowPathCell item = flow.Paths[i];
                if (!Active(level, item.Coordinate) || !used.Add(item.Coordinate) ||
                    (!item.IsEnd && (!Active(level, item.Next) || !new BoardEdge(item.Coordinate, item.Next).IsAdjacent || HasWall(level, item.Coordinate, item.Next))))
                    Add(issues, LevelValidationCode.InvalidFlow, "경로 좌표·분기/중복·인접·벽 통과를 확인하세요.", $"flow.paths.Array.data[{i}]", item.Coordinate);
            }
            HashSet<BoardEdge> walls = new HashSet<BoardEdge>();
            for (int i = 0; i < flow.Walls.Count; i++)
            {
                BoardEdge wall = flow.Walls[i];
                string error = WallError(level, wall);
                if (error != null || !walls.Add(wall))
                    Add(issues, LevelValidationCode.InvalidWall, error ?? "벽이 중복되었습니다.", $"flow.walls.Array.data[{i}]", wall.A);
            }
            used.Clear();
            for (int i = 0; i < flow.Portals.Count; i++)
            {
                FlowPortal item = flow.Portals[i];
                string path = $"flow.portals.Array.data[{i}]";
                if (!Active(level, item.Entrance) || !used.Add(item.Entrance) || flow.Paths.Any(value => value.Coordinate.Equals(item.Entrance)))
                    Add(issues, LevelValidationCode.InvalidPortal, "입구 좌표·역할 중복·직접 경로 중첩을 확인하세요.", path, item.Entrance);
                if (!item.HasExit) Add(issues, LevelValidationCode.InvalidPortal, "통로 출구가 미연결입니다.", path, item.Entrance);
                else if (!Active(level, item.Exit) || !used.Add(item.Exit))
                    Add(issues, LevelValidationCode.InvalidPortal, "출구 좌표·역할 중복·자기 연결을 확인하세요.", path, item.Exit);
            }
            HashSet<BoardCoordinate> arrivals = new HashSet<BoardCoordinate>();
            for (int i = 0; i < flow.Arrivals.Count; i++)
                if (!Active(level, flow.Arrivals[i]) || !arrivals.Add(flow.Arrivals[i]) || used.Contains(flow.Arrivals[i]))
                    Add(issues, LevelValidationCode.InvalidArrival, "도착 바닥 좌표·중복·통로 중첩을 확인하세요.", $"flow.arrivals.Array.data[{i}]", flow.Arrivals[i]);

            Dictionary<BoardCoordinate, BoardCoordinate> graph = Graph(level);
            HashSet<BoardCoordinate> done = new HashSet<BoardCoordinate>();
            foreach (BoardCoordinate start in graph.Keys)
            {
                HashSet<BoardCoordinate> chain = new HashSet<BoardCoordinate>();
                BoardCoordinate current = start;
                while (!done.Contains(current) && graph.TryGetValue(current, out BoardCoordinate next))
                {
                    if (!chain.Add(current))
                    {
                        Add(issues, LevelValidationCode.FlowCycle, "중력·직접 경로·통로를 합친 순환입니다.", "flow", current);
                        break;
                    }
                    current = next;
                }
                done.UnionWith(chain);
            }
            Dictionary<BoardCoordinate, List<BoardCoordinate>> incoming = graph.GroupBy(pair => pair.Value)
                .ToDictionary(group => group.Key, group => group.Select(pair => pair.Key).ToList());
            used.Clear();
            for (int i = 0; i < flow.Merges.Count; i++)
            {
                FlowMerge item = flow.Merges[i];
                incoming.TryGetValue(item.Coordinate, out List<BoardCoordinate> sources);
                if (!used.Add(item.Coordinate) || sources == null || sources.Count < 2 || item.Sources == null ||
                    item.Sources.Count != sources.Count || item.Sources.Distinct().Count() != sources.Count || !new HashSet<BoardCoordinate>(sources).SetEquals(item.Sources))
                    Add(issues, LevelValidationCode.InvalidMerge, "합류 후보가 변경되었거나 우선순위가 누락·중복되었습니다. 재설정하세요.", $"flow.merges.Array.data[{i}]", item.Coordinate);
            }
            foreach (KeyValuePair<BoardCoordinate, List<BoardCoordinate>> pair in incoming)
                if (pair.Value.Count > 1 && !used.Contains(pair.Key))
                    Add(issues, LevelValidationCode.InvalidMerge, "합류 유입 순서를 지정하세요.", "flow.merges", pair.Key);
        }

        private static void Add(List<LevelValidationIssue> issues, LevelValidationCode code, string message, string path, BoardCoordinate? cell = null) =>
            issues.Add(new LevelValidationIssue(code, message, path, cell));
    }
}
