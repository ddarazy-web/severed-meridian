using System.Collections.Generic;
using System.Linq;
using Board;

namespace Levels
{
    public readonly struct LevelConnectionBody
    {
        public readonly string Id;
        public readonly BoardCoordinate Coordinate;
        public readonly int Size;
        public readonly bool IsGenerator;
        public readonly bool IsTarget;
        public readonly Elements.ElementDefinition Definition;
        public LevelConnectionBody(string id, BoardCoordinate coordinate, int size, bool generator, bool target, Elements.ElementDefinition definition)
        { Id = id; Coordinate = coordinate; Size = size; IsGenerator = generator; IsTarget = target; Definition = definition; }
    }

    public static class LevelConnectionRules
    {
        public static IEnumerable<int> BodyIndices(LevelDefinition level) => level.SchemaVersion == LevelDefinition.CurrentSchemaVersion
            ? Enumerable.Range(0, level.Elements?.Count ?? 0).Where(index => level.Elements[index]?.layer == PlacementLayer.Obstacle)
            : Enumerable.Range(0, level.Obstacles?.Count ?? 0);

        public static LevelConnectionBody Body(LevelDefinition level, int index)
        {
            if (level.SchemaVersion == LevelDefinition.CurrentSchemaVersion)
            {
                ElementPlacementDefinition item = level.Elements[index];
                Elements.ElementDefinition definition = level.CreateElementCatalog().Get(new Elements.ElementId(item.definitionId));
                return new LevelConnectionBody(item.instanceId, item.coordinate, definition.ChargePlacement?.Size ?? definition.RequirePlacement().Size,
                    definition.ReactionBehavior == Elements.ElementReactionBehavior.GeneratorCharge,
                    definition.ReactionBehavior == Elements.ElementReactionBehavior.Durability && definition.RemovalMissionProfile?.Kind != MissionKind.Scrap, definition);
            }
            ObstaclePlacementDefinition body = level.Obstacles[index];
            return new LevelConnectionBody(body.Id, body.Coordinate, LevelPlacementRules.Size(body.Kind), body.Kind == ObstacleKind.Generator,
                IsTarget(body.Kind), System.Enum.IsDefined(typeof(ObstacleKind), body.Kind) ? Elements.LegacyElementDefinitions.Get(body.Kind) : null);
        }

        public static int FindAt(LevelDefinition level, BoardCoordinate coordinate)
        {
            int found = -1;
            foreach (int index in BodyIndices(level))
            {
                try
                {
                    LevelConnectionBody body = Body(level, index);
                    if (!LevelPlacementRules.Footprint(body.Coordinate, body.Size).Contains(coordinate)) continue;
                    if (found >= 0) return -2; found = index;
                }
                catch (System.ArgumentException) { if (level.Elements[index].coordinate.Equals(coordinate)) return -2; }
                catch (System.InvalidOperationException) { if (level.Elements[index].coordinate.Equals(coordinate)) return -2; }
                catch (KeyNotFoundException) { if (level.Elements[index].coordinate.Equals(coordinate)) return -2; }
            }
            return found;
        }

        public static int Find(LevelDefinition level, string id)
        {
            if (level != null && level.SchemaVersion == LevelDefinition.CurrentSchemaVersion)
            {
                if (string.IsNullOrEmpty(id)) return -1;
                int element = -1;
                foreach (int index in BodyIndices(level))
                    if (level.Elements[index].instanceId == id) { if (element >= 0) return -2; element = index; }
                return element;
            }
            if (string.IsNullOrEmpty(id) || level?.Obstacles == null) return -1;
            int found = -1;
            for (int i = 0; i < level.Obstacles.Count; i++)
                if (level.Obstacles[i].Id == id) { if (found >= 0) return -2; found = i; }
            return found;
        }

        public static bool IsTarget(ObstacleKind kind) => kind == ObstacleKind.Crate || kind == ObstacleKind.Safe ||
            kind == ObstacleKind.ColorLock || kind == ObstacleKind.Appliance;

        public static bool Terminal(ObstaclePlacementDefinition body, BoardCoordinate vertex)
            => Terminal(body, vertex, LevelPlacementRules.Size(body.Kind));

        private static bool Terminal(ObstaclePlacementDefinition body, BoardCoordinate vertex, int size)
            => Terminal(body.Coordinate, vertex, size);

        public static bool Terminal(LevelConnectionBody body, BoardCoordinate vertex) => Terminal(body.Coordinate, vertex, body.Size);

        private static bool Terminal(BoardCoordinate origin, BoardCoordinate vertex, int size)
        {
            int top = origin.Row, left = origin.Column;
            return vertex.Row >= top && vertex.Row <= top + size && vertex.Column >= left && vertex.Column <= left + size &&
                (vertex.Row == top || vertex.Row == top + size || vertex.Column == left || vertex.Column == left + size);
        }

        public static bool Inside(ObstaclePlacementDefinition body, BoardCoordinate vertex)
            => Inside(body, vertex, LevelPlacementRules.Size(body.Kind));

        private static bool Inside(ObstaclePlacementDefinition body, BoardCoordinate vertex, int size)
            => Inside(body.Coordinate, vertex, size);

        public static bool Inside(LevelConnectionBody body, BoardCoordinate vertex) => Inside(body.Coordinate, vertex, body.Size);

        private static bool Inside(BoardCoordinate origin, BoardCoordinate vertex, int size)
        {
            return vertex.Row > origin.Row && vertex.Row < origin.Row + size &&
                vertex.Column > origin.Column && vertex.Column < origin.Column + size;
        }

        public static string TargetError(LevelDefinition level, string generatorId, string targetId, int self = -1)
        {
            int generator = Find(level, generatorId), target = Find(level, targetId);
            if (generator < 0 || target < 0) return "연결 대상 ID가 없거나 중복되었습니다.";
            try { if (!Body(level, generator).IsGenerator || !Body(level, target).IsTarget) return "발전기 또는 연결 대상 종류가 잘못되었습니다."; }
            catch (System.ArgumentException error) { return error.Message; }
            catch (System.InvalidOperationException error) { return error.Message; }
            catch (KeyNotFoundException error) { return error.Message; }
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
            => WireError(level, generatorId, targetId, vertices, self, allowIncomplete, body => LevelPlacementRules.Size(body.Kind));

        private static string WireError(LevelDefinition level, string generatorId, string targetId, IReadOnlyList<BoardCoordinate> vertices,
            int self, bool allowIncomplete, System.Func<ObstaclePlacementDefinition, int> size)
        {
            int generator = Find(level, generatorId), target = Find(level, targetId);
            if (generator < 0 || target < 0) return "전선의 본체 ID를 확인하세요.";
            if (vertices == null || vertices.Count < (allowIncomplete ? 1 : 2)) return "전선 경로가 미완성입니다.";
            int SizeAt(int index) => level.SchemaVersion == LevelDefinition.CurrentSchemaVersion ? Body(level, index).Size : size(level.Obstacles[index]);
            if (!Terminal(Body(level, generator).Coordinate, vertices[0], SizeAt(generator)) ||
                (!allowIncomplete && !Terminal(Body(level, target).Coordinate, vertices[vertices.Count - 1], SizeAt(target))))
                return "본체 위치와 단자가 맞지 않습니다. 전선 경로를 재지정하세요.";
            HashSet<BoardCoordinate> visited = new HashSet<BoardCoordinate>();
            foreach (BoardCoordinate vertex in vertices)
            {
                if (vertex.Row < 0 || vertex.Row > BoardDefinition.DefaultRows || vertex.Column < 0 || vertex.Column > BoardDefinition.DefaultColumns) return "전선 꼭짓점이 보드 밖입니다.";
                if (!visited.Add(vertex)) return "전선은 같은 꼭짓점을 반복할 수 없습니다.";
                if (BodyIndices(level).Any(index => Inside(Body(level, index).Coordinate, vertex, SizeAt(index)))) return "전선이 2×2 본체 내부를 지납니다.";
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
            => Validate(level, issues, null);

        internal static void Validate(LevelDefinition level, List<LevelValidationIssue> issues,
            IReadOnlyDictionary<string, Elements.ElementDefinition> selected)
        {
            int Size(ObstaclePlacementDefinition body) => selected != null && body.Id != null &&
                selected.TryGetValue(body.Id, out Elements.ElementDefinition definition) ?
                definition.ChargePlacement?.Size ?? definition.RequirePlacement().Size : LevelPlacementRules.Size(body.Kind);
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
                error = WireError(level, item.GeneratorId, item.TargetId, item.Vertices, i, false, Size);
                if (error != null) issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidWire, error, $"connections.Array.data[{i}].vertices", cell));
            }
        }
    }
}
