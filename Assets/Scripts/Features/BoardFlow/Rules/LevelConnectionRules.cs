using System.Collections.Generic;
using System.Linq;
using Board;

namespace Levels
{
    public static class LevelConnectionRules
    {
        public static int Find(LevelDefinition level, string id)
        {
            if (string.IsNullOrEmpty(id) || level?.Obstacles == null) return -1;
            int found = -1;
            for (int i = 0; i < level.Obstacles.Count; i++)
                if (level.Obstacles[i].Id == id) { if (found >= 0) return -2; found = i; }
            return found;
        }

        public static bool IsTarget(ObstacleKind kind) => kind == ObstacleKind.Crate || kind == ObstacleKind.Safe ||
            kind == ObstacleKind.ColorLock || kind == ObstacleKind.Appliance;

        public static bool Terminal(ObstaclePlacementDefinition body, BoardCoordinate vertex)
        {
            int top = body.Coordinate.Row, left = body.Coordinate.Column, size = LevelPlacementRules.Size(body.Kind);
            return vertex.Row >= top && vertex.Row <= top + size && vertex.Column >= left && vertex.Column <= left + size &&
                (vertex.Row == top || vertex.Row == top + size || vertex.Column == left || vertex.Column == left + size);
        }

        public static bool Inside(ObstaclePlacementDefinition body, BoardCoordinate vertex)
        {
            int size = LevelPlacementRules.Size(body.Kind);
            return vertex.Row > body.Coordinate.Row && vertex.Row < body.Coordinate.Row + size &&
                vertex.Column > body.Coordinate.Column && vertex.Column < body.Coordinate.Column + size;
        }

        public static string TargetError(LevelDefinition level, string generatorId, string targetId, int self = -1)
        {
            int generator = Find(level, generatorId), target = Find(level, targetId);
            if (generator < 0 || target < 0) return "연결 대상 ID가 없거나 중복되었습니다.";
            if (level.Obstacles[generator].Kind != ObstacleKind.Generator || !IsTarget(level.Obstacles[target].Kind)) return "발전기 또는 연결 대상 종류가 잘못되었습니다.";
            if (level.Connections == null) return "연결 목록이 없습니다.";
            int count = 0;
            for (int i = 0; i < level.Connections.Count; i++)
            {
                if (i == self) continue;
                if (level.Connections[i].TargetId == targetId) return "하나의 대상은 한 발전기에만 연결할 수 있습니다.";
                if (level.Connections[i].GeneratorId == generatorId) count++;
            }
            return count >= 3 ? "발전기는 최대 3개 대상까지 연결합니다." : null;
        }

        public static string WireError(LevelDefinition level, string generatorId, string targetId, IReadOnlyList<BoardCoordinate> vertices, int self = -1, bool allowIncomplete = false)
        {
            int generator = Find(level, generatorId), target = Find(level, targetId);
            if (generator < 0 || target < 0) return "전선의 본체 ID를 확인하세요.";
            if (vertices == null || vertices.Count < (allowIncomplete ? 1 : 2)) return "전선 경로가 미완성입니다.";
            if (!Terminal(level.Obstacles[generator], vertices[0]) || (!allowIncomplete && !Terminal(level.Obstacles[target], vertices[vertices.Count - 1])))
                return "본체 위치와 단자가 맞지 않습니다. 전선 경로를 재지정하세요.";
            HashSet<BoardCoordinate> visited = new HashSet<BoardCoordinate>();
            foreach (BoardCoordinate vertex in vertices)
            {
                if (vertex.Row < 0 || vertex.Row > BoardDefinition.DefaultRows || vertex.Column < 0 || vertex.Column > BoardDefinition.DefaultColumns) return "전선 꼭짓점이 보드 밖입니다.";
                if (!visited.Add(vertex)) return "전선은 같은 꼭짓점을 반복할 수 없습니다.";
                if (level.Obstacles.Any(body => Inside(body, vertex))) return "전선이 2×2 본체 내부를 지납니다.";
            }
            foreach (BoardEdge segment in LevelFlowRules.Segments(vertices))
            {
                if (!segment.IsAdjacent) return "전선은 인접 격자 꼭짓점 사이로 연결하세요.";
                if (level.Flow?.Walls != null && level.Flow.Walls.Any(wall => wall.IsAdjacent && LevelFlowRules.WallSegment(wall).Equals(segment)))
                    return "전선은 고철 벽을 통과할 수 없습니다.";
            }
            if (level.Connections != null)
                for (int i = 0; i < level.Connections.Count; i++)
                    if (i != self && level.Connections[i].Vertices != null && level.Connections[i].Vertices.Any(visited.Contains))
                        return "다른 전선과 꼭짓점·단자·선분을 공유할 수 없습니다.";
            return null;
        }

        public static void Validate(LevelDefinition level, List<LevelValidationIssue> issues)
        {
            if (level.SchemaVersion < 3) return;
            HashSet<string> ids = new HashSet<string>();
            if (level.Obstacles != null)
                for (int i = 0; i < level.Obstacles.Count; i++)
                {
                    ObstaclePlacementDefinition body = level.Obstacles[i];
                    if (string.IsNullOrWhiteSpace(body.Id) || !ids.Add(body.Id))
                        issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidObstacleId, "장애물 ID가 없거나 중복되었습니다.", $"obstacles.Array.data[{i}].id", body.Coordinate));
                    if (body.Kind == ObstacleKind.Generator && !(level.Connections?.Any(item => item.GeneratorId == body.Id) ?? false))
                        issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidConnection, "발전기에 연결 대상이 없습니다.", $"obstacles.Array.data[{i}]", body.Coordinate));
                }
            if (level.Connections == null)
            {
                issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidConnection, "연결 목록이 누락되었습니다.", "connections"));
                return;
            }
            for (int i = 0; i < level.Connections.Count; i++)
            {
                LevelConnectionDefinition item = level.Connections[i];
                int generator = Find(level, item.GeneratorId);
                BoardCoordinate? cell = generator >= 0 ? level.Obstacles[generator].Coordinate : null;
                string error = TargetError(level, item.GeneratorId, item.TargetId, i);
                if (error != null) issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidConnection, error, $"connections.Array.data[{i}]", cell));
                error = WireError(level, item.GeneratorId, item.TargetId, item.Vertices, i);
                if (error != null) issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidWire, error, $"connections.Array.data[{i}].vertices", cell));
            }
        }
    }
}
