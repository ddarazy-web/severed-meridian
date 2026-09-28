using System.Collections.Generic;
using System.Linq;
using Board;
using UnityEditor;

namespace Levels.Editor
{
    public static partial class LevelConnectionEditing
    {
        public static string FindWire(LevelDefinition level, string generatorId, string targetId, BoardCoordinate start, out List<BoardCoordinate> path, BoardCoordinate? end = null)
        {
            path = null;
            if (!LevelFlowEditing.CanEdit(level)) return "편집할 수 없는 레벨입니다.";
            string error = LevelConnectionRules.TargetError(level, generatorId, targetId);
            if (error != null) return error;
            ObstaclePlacementDefinition generator = level.Obstacles[LevelConnectionRules.Find(level, generatorId)];
            ObstaclePlacementDefinition target = level.Obstacles[LevelConnectionRules.Find(level, targetId)];
            if (!LevelConnectionRules.Terminal(generator, start)) return "발전기 연결점 위치를 확인하세요.";
            if (end.HasValue && !LevelConnectionRules.Terminal(target, end.Value)) return "장애물 연결점 위치를 확인하세요.";
            HashSet<BoardCoordinate> blocked = new HashSet<BoardCoordinate>(level.Connections.Where(c => c.Vertices != null).SelectMany(c => c.Vertices));
            for (int row = 0; row <= 10; row++)
                for (int column = 0; column <= 10; column++)
                {
                    BoardCoordinate vertex = new BoardCoordinate(row, column);
                    if (level.Obstacles.Any(body => LevelConnectionRules.Inside(body, vertex))) blocked.Add(vertex);
                }
            if (blocked.Contains(start)) return "이 연결점은 다른 전선이 지나갑니다. 다른 점을 선택하세요.";
            HashSet<BoardEdge> walls = new HashSet<BoardEdge>(level.Flow.Walls.Where(w => w.IsAdjacent).Select(LevelFlowRules.WallSegment));
            Queue<BoardCoordinate> queue = new Queue<BoardCoordinate>(); queue.Enqueue(start);
            Dictionary<BoardCoordinate, BoardCoordinate> previous = new Dictionary<BoardCoordinate, BoardCoordinate> { [start] = start };
            // 최대121개 꼭짓점만 탐색한다. 기존 전선/벽을 움직이지 않고 최단 유효 경로를 찾는다.
            while (queue.Count > 0)
            {
                BoardCoordinate current = queue.Dequeue();
                if (!current.Equals(start) && (end.HasValue ? current.Equals(end.Value) : LevelConnectionRules.Terminal(target, current)))
                {
                    path = new List<BoardCoordinate> { current };
                    while (!current.Equals(start)) { current = previous[current]; path.Add(current); }
                    path.Reverse();
                    return LevelConnectionRules.WireError(level, generatorId, targetId, path);
                }
                foreach (BoardCoordinate next in new[] { new BoardCoordinate(current.Row - 1, current.Column), new BoardCoordinate(current.Row, current.Column + 1),
                    new BoardCoordinate(current.Row + 1, current.Column), new BoardCoordinate(current.Row, current.Column - 1) })
                {
                    if (next.Row < 0 || next.Row > 10 || next.Column < 0 || next.Column > 10 || blocked.Contains(next) || previous.ContainsKey(next) || walls.Contains(new BoardEdge(current, next))) continue;
                    previous.Add(next, current); queue.Enqueue(next);
                }
            }
            return "벽과 기존 전선을 피해 연결할 경로가 없습니다. 다른 연결점이나 수동 전선 편집을 사용하세요.";
        }

        public static string ConnectAuto(LevelDefinition level, string generatorId, string targetId, BoardCoordinate start, BoardCoordinate? end = null)
        {
            string error = FindWire(level, generatorId, targetId, start, out List<BoardCoordinate> path, end);
            if (error != null) return error;
            using SerializedObject data = new SerializedObject(level);
            SerializedProperty list = data.FindProperty("connections");
            SerializedProperty item = list.GetArrayElementAtIndex(list.arraySize++);
            item.FindPropertyRelative("generatorId").stringValue = generatorId;
            item.FindPropertyRelative("targetId").stringValue = targetId;
            LevelFlowEditing.SetCoordinates(item.FindPropertyRelative("vertices"), path);
            LevelObstacleEditing.Commit(data, "발전기 드래그 연결");
            return null;
        }
    }
}
