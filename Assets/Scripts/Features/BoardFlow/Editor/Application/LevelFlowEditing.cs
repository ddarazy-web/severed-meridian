using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using UnityEditor;

namespace Levels.Editor
{
    public static class LevelFlowEditing
    {
        public static bool CanEdit(LevelDefinition level) => LevelBoardEditing.CanEdit(level) &&
            level.Flow != null && level.Flow.ListsPresent && level.Connections != null;

        public static void SetCoordinate(SerializedProperty property, BoardCoordinate value)
        {
            property.FindPropertyRelative("row").intValue = value.Row;
            property.FindPropertyRelative("column").intValue = value.Column;
        }

        public static void SetCoordinates(SerializedProperty list, IEnumerable<BoardCoordinate> values)
        {
            BoardCoordinate[] cells = values.ToArray();
            list.arraySize = cells.Length;
            for (int i = 0; i < cells.Length; i++) SetCoordinate(list.GetArrayElementAtIndex(i), cells[i]);
        }

        public static string SetGravity(LevelDefinition level, IEnumerable<BoardCoordinate> cells, GravityDirection? direction)
        {
            if (!CanEdit(level)) return "편집할 수 없는 레벨입니다.";
            if (direction.HasValue && !Enum.IsDefined(typeof(GravityDirection), direction.Value)) return "중력 방향 오류입니다.";
            BoardCoordinate[] targets = cells.Distinct().ToArray();
            if (targets.Any(cell => !LevelFlowRules.Active(level, cell))) return "활성 칸을 선택하세요.";
            if (targets.Any(cell => level.Flow.Gravity.Count(item => item.Coordinate.Equals(cell)) > 1)) return "중복 중력은 원본 Inspector에서 수정하세요.";
            using SerializedObject data = new SerializedObject(level);
            SerializedProperty list = data.FindProperty("flow.gravity");
            if (!direction.HasValue)
            {
                for (int i = level.Flow.Gravity.Count - 1; i >= 0; i--)
                    if (targets.Contains(level.Flow.Gravity[i].Coordinate)) list.DeleteArrayElementAtIndex(i);
            }
            else foreach (BoardCoordinate cell in targets)
            {
                int index = level.Flow.Gravity.ToList().FindIndex(item => item.Coordinate.Equals(cell));
                if (index < 0) index = list.arraySize++;
                SerializedProperty item = list.GetArrayElementAtIndex(index);
                SetCoordinate(item.FindPropertyRelative("coordinate"), cell);
                item.FindPropertyRelative("direction").intValue = (int)direction.Value;
            }
            LevelObstacleEditing.Commit(data, "구역 중력 편집");
            return null;
        }

        public static string PathError(LevelDefinition level, IReadOnlyList<BoardCoordinate> cells)
        {
            if (!CanEdit(level) || cells == null || cells.Count == 0) return "경로 칸을 지정하세요.";
            if (cells.Distinct().Count() != cells.Count || cells.Any(cell => !LevelFlowRules.Active(level, cell))) return "활성 칸을 중복 없이 연결하세요.";
            for (int i = 0; i < cells.Count; i++)
            {
                if (i < cells.Count - 1 && level.Flow.Portals.Any(portal => portal.Entrance.Equals(cells[i]))) return "통로 입구에는 직접 경로를 놓을 수 없습니다.";
                int count = level.Flow.Paths.Count(item => item.Coordinate.Equals(cells[i]));
                if (count > 0 && (i < cells.Count - 1 || count > 1)) return "기존 경로를 지운 뒤 수정하세요. 분기·덮어쓰기는 허용하지 않습니다.";
                if (i > 0 && (!new BoardEdge(cells[i - 1], cells[i]).IsAdjacent || LevelFlowRules.HasWall(level, cells[i - 1], cells[i])))
                    return "벽 없는 상하좌우 인접 칸으로 연결하세요.";
            }
            return null;
        }

        public static string SetPath(LevelDefinition level, IReadOnlyList<BoardCoordinate> cells)
        {
            string error = PathError(level, cells);
            if (error != null) return error;
            using SerializedObject data = new SerializedObject(level);
            SerializedProperty list = data.FindProperty("flow.paths");
            for (int i = 0; i < cells.Count; i++)
            {
                if (i == cells.Count - 1 && (level.Flow.Paths.Any(item => item.Coordinate.Equals(cells[i])) ||
                    level.Flow.Portals.Any(item => item.Entrance.Equals(cells[i])))) break;
                SerializedProperty item = list.GetArrayElementAtIndex(list.arraySize++);
                SetCoordinate(item.FindPropertyRelative("coordinate"), cells[i]);
                item.FindPropertyRelative("isEnd").boolValue = i == cells.Count - 1;
                SetCoordinate(item.FindPropertyRelative("next"), i < cells.Count - 1 ? cells[i + 1] : default);
            }
            LevelObstacleEditing.Commit(data, "직접 경로 설정");
            return null;
        }

        public static string RemovePath(LevelDefinition level, BoardCoordinate cell)
        {
            if (!CanEdit(level)) return "편집할 수 없는 레벨입니다.";
            int index = level.Flow.Paths.ToList().FindIndex(item => item.Coordinate.Equals(cell));
            if (level.Flow.Paths.Count(item => item.Coordinate.Equals(cell)) > 1) return "중복 경로는 Inspector에서 수정하세요.";
            if (index < 0) return null;
            using SerializedObject data = new SerializedObject(level);
            data.FindProperty("flow.paths").DeleteArrayElementAtIndex(index);
            LevelObstacleEditing.Commit(data, "선택 칸 경로 삭제");
            return null;
        }

        public static List<BoardCoordinate> MergeOrder(LevelDefinition level, BoardCoordinate cell)
        {
            List<BoardCoordinate> candidates = LevelFlowRules.Sources(level, cell);
            FlowMerge[] saved = level.Flow.Merges.Where(item => item.Coordinate.Equals(cell)).ToArray();
            return saved.Length == 1 && saved[0].Sources != null && saved[0].Sources.Count == candidates.Count &&
                saved[0].Sources.Distinct().Count() == candidates.Count && new HashSet<BoardCoordinate>(candidates).SetEquals(saved[0].Sources)
                ? saved[0].Sources.ToList() : candidates;
        }

        public static string SetMerge(LevelDefinition level, BoardCoordinate cell, IReadOnlyList<BoardCoordinate> sources)
        {
            if (!CanEdit(level)) return "편집할 수 없는 레벨입니다.";
            List<BoardCoordinate> candidates = LevelFlowRules.Sources(level, cell);
            if (sources == null || candidates.Count < 2 || sources.Count != candidates.Count || sources.Distinct().Count() != candidates.Count || !new HashSet<BoardCoordinate>(sources).SetEquals(candidates))
                return "현재 합류 후보를 빠짐없이 한 번씩 지정하세요.";
            if (level.Flow.Merges.Count(item => item.Coordinate.Equals(cell)) > 1) return "중복 합류는 Inspector에서 수정하세요.";
            using SerializedObject data = new SerializedObject(level);
            SerializedProperty list = data.FindProperty("flow.merges");
            int index = level.Flow.Merges.ToList().FindIndex(item => item.Coordinate.Equals(cell));
            if (index < 0) index = list.arraySize++;
            SerializedProperty merge = list.GetArrayElementAtIndex(index);
            SetCoordinate(merge.FindPropertyRelative("coordinate"), cell);
            SetCoordinates(merge.FindPropertyRelative("sources"), sources);
            LevelObstacleEditing.Commit(data, "합류 우선순위 설정");
            return null;
        }

        public static string RemoveMerge(LevelDefinition level, BoardCoordinate cell)
        {
            if (!CanEdit(level)) return "편집할 수 없는 레벨입니다.";
            using SerializedObject data = new SerializedObject(level);
            SerializedProperty list = data.FindProperty("flow.merges");
            for (int i = level.Flow.Merges.Count - 1; i >= 0; i--)
                if (level.Flow.Merges[i].Coordinate.Equals(cell)) list.DeleteArrayElementAtIndex(i);
            LevelObstacleEditing.Commit(data, "합류 순서 초기화");
            return null;
        }

        public static string SetWalls(LevelDefinition level, IEnumerable<BoardEdge> edges, bool erase)
        {
            if (!CanEdit(level)) return "편집할 수 없는 레벨입니다.";
            BoardEdge[] targets = edges.Distinct().ToArray();
            if (!erase)
                foreach (BoardEdge edge in targets)
                {
                    string error = LevelFlowRules.WallError(level, edge);
                    if (error != null) return error;
                }
            using SerializedObject data = new SerializedObject(level);
            SerializedProperty list = data.FindProperty("flow.walls");
            if (erase)
            {
                for (int i = level.Flow.Walls.Count - 1; i >= 0; i--)
                    if (targets.Contains(level.Flow.Walls[i])) list.DeleteArrayElementAtIndex(i);
            }
            else foreach (BoardEdge edge in targets)
            {
                if (level.Flow.Walls.Contains(edge)) continue;
                SerializedProperty item = list.GetArrayElementAtIndex(list.arraySize++);
                SetCoordinate(item.FindPropertyRelative("a"), edge.A);
                SetCoordinate(item.FindPropertyRelative("b"), edge.B);
            }
            LevelObstacleEditing.Commit(data, "고철 벽 편집");
            return null;
        }

        public static string PortalError(LevelDefinition level, BoardCoordinate entrance, BoardCoordinate? exit)
        {
            if (!CanEdit(level)) return "편집할 수 없는 레벨입니다.";
            if (!LevelFlowRules.Active(level, entrance) || (exit.HasValue && !LevelFlowRules.Active(level, exit.Value))) return "통로는 활성 칸에 놓으세요.";
            if (exit.HasValue && entrance.Equals(exit.Value)) return "입구와 출구는 다른 칸이어야 합니다.";
            if (level.Flow.Paths.Any(item => item.Coordinate.Equals(entrance))) return "직접 경로와 입구가 겹칩니다.";
            if (level.Flow.Arrivals.Contains(entrance) || (exit.HasValue && level.Flow.Arrivals.Contains(exit.Value))) return "도착 바닥과 통로가 겹칩니다.";
            int index = level.Flow.Portals.ToList().FindIndex(item => item.Entrance.Equals(entrance));
            if (level.Flow.Portals.Count(item => item.Entrance.Equals(entrance)) > 1) return "중복 통로는 Inspector에서 수정하세요.";
            for (int i = 0; i < level.Flow.Portals.Count; i++)
            {
                if (i == index) continue;
                FlowPortal other = level.Flow.Portals[i];
                if (other.Entrance.Equals(entrance) || (other.HasExit && other.Exit.Equals(entrance)) ||
                    (exit.HasValue && (other.Entrance.Equals(exit.Value) || (other.HasExit && other.Exit.Equals(exit.Value))))) return "통로의 역할/쌍이 중복됩니다.";
            }
            return null;
        }

        public static string SetPortal(LevelDefinition level, BoardCoordinate entrance, BoardCoordinate? exit)
        {
            string error = PortalError(level, entrance, exit);
            if (error != null) return error;
            int index = level.Flow.Portals.ToList().FindIndex(item => item.Entrance.Equals(entrance));
            using SerializedObject data = new SerializedObject(level);
            SerializedProperty list = data.FindProperty("flow.portals");
            if (index < 0) index = list.arraySize++;
            SerializedProperty itemProperty = list.GetArrayElementAtIndex(index);
            SetCoordinate(itemProperty.FindPropertyRelative("entrance"), entrance);
            itemProperty.FindPropertyRelative("hasExit").boolValue = exit.HasValue;
            SetCoordinate(itemProperty.FindPropertyRelative("exit"), exit ?? default);
            LevelObstacleEditing.Commit(data, "이동 통로 설정");
            return null;
        }

        public static string RemovePortal(LevelDefinition level, BoardCoordinate entrance)
        {
            if (!CanEdit(level)) return "편집할 수 없는 레벨입니다.";
            if (level.Flow.Portals.Count(item => item.Entrance.Equals(entrance)) > 1) return "중복 통로는 Inspector에서 수정하세요.";
            int index = level.Flow.Portals.ToList().FindIndex(item => item.Entrance.Equals(entrance));
            if (index < 0) return null;
            using SerializedObject data = new SerializedObject(level);
            data.FindProperty("flow.portals").DeleteArrayElementAtIndex(index);
            LevelObstacleEditing.Commit(data, "통로 쌍 삭제");
            return null;
        }

        public static string SetArrival(LevelDefinition level, BoardCoordinate cell, bool erase)
        {
            if (!CanEdit(level)) return "편집할 수 없는 레벨입니다.";
            if (!erase && LevelSupplyRules.FindSource(level, cell) != -1) return "생성구와 도착 바닥은 겹칠 수 없습니다.";
            if (!erase && (!LevelFlowRules.Active(level, cell) || level.Flow.Portals.Any(item => item.Entrance.Equals(cell) || (item.HasExit && item.Exit.Equals(cell)))))
                return "통로와 겹치지 않는 활성 칸을 선택하세요.";
            using SerializedObject data = new SerializedObject(level);
            SerializedProperty list = data.FindProperty("flow.arrivals");
            if (erase)
            {
                for (int i = level.Flow.Arrivals.Count - 1; i >= 0; i--) if (level.Flow.Arrivals[i].Equals(cell)) list.DeleteArrayElementAtIndex(i);
            }
            else if (!level.Flow.Arrivals.Contains(cell)) SetCoordinate(list.GetArrayElementAtIndex(list.arraySize++), cell);
            LevelObstacleEditing.Commit(data, "도착 바닥 편집");
            return null;
        }
    }
}
