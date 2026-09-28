using System.Collections.Generic;
using Board;
using UnityEditor;

namespace Levels.Editor
{
    public static partial class LevelConnectionEditing
    {
        public static string Add(LevelDefinition level, string generatorId, string targetId)
        {
            if (!LevelFlowEditing.CanEdit(level)) return "편집할 수 없는 레벨입니다.";
            string error = LevelConnectionRules.TargetError(level, generatorId, targetId);
            if (error != null) return error;
            using SerializedObject data = new SerializedObject(level);
            SerializedProperty list = data.FindProperty("connections");
            SerializedProperty item = list.GetArrayElementAtIndex(list.arraySize++);
            item.FindPropertyRelative("generatorId").stringValue = generatorId;
            item.FindPropertyRelative("targetId").stringValue = targetId;
            item.FindPropertyRelative("vertices").arraySize = 0;
            LevelObstacleEditing.Commit(data, "발전기 연결 대상 추가");
            return null;
        }

        public static string SetWire(LevelDefinition level, int index, IReadOnlyList<BoardCoordinate> vertices, bool allowIncomplete = false)
        {
            if (!LevelFlowEditing.CanEdit(level) || index < 0 || index >= level.Connections.Count) return "연결을 선택하세요.";
            LevelConnectionDefinition connection = level.Connections[index];
            string error = LevelConnectionRules.WireError(level, connection.GeneratorId, connection.TargetId, vertices, index, allowIncomplete);
            if (error != null) return error;
            using SerializedObject data = new SerializedObject(level);
            LevelFlowEditing.SetCoordinates(data.FindProperty($"connections.Array.data[{index}].vertices"), vertices);
            LevelObstacleEditing.Commit(data, "전선 경로 지정");
            return null;
        }

        public static string Remove(LevelDefinition level, int index)
        {
            if (!LevelFlowEditing.CanEdit(level) || index < 0 || index >= level.Connections.Count) return "연결을 선택하세요.";
            using SerializedObject data = new SerializedObject(level);
            data.FindProperty("connections").DeleteArrayElementAtIndex(index);
            LevelObstacleEditing.Commit(data, "발전기 연결 삭제");
            return null;
        }
    }
}
