using System;
using System.Linq;
using UnityEditor;

namespace Levels.Editor
{
    public static class LevelMissionEditing
    {
        public static string Set(LevelDefinition level, int index, MissionKind kind, RabbitColor color, int count)
        {
            if (!LevelSupplyEditing.CanEdit(level) || index < 0 || index > level.Missions.Count) return "유효한 미션을 선택하세요.";
            if (index == level.Missions.Count && level.Missions.Count >= 4) return "미션은 최대 4개입니다.";
            if (!Enum.IsDefined(typeof(MissionKind), kind)) return "지원하지 않는 미션입니다.";
            if (kind != MissionKind.Mold && count <= 0) return "목표 수량은 양수여야 합니다.";
            if (kind == MissionKind.Color && (!Enum.IsDefined(typeof(RabbitColor), color) || level.Colors?.Contains(color) != true)) return "레벨에서 사용하는 색을 지정하세요.";
            if (level.Missions.Where((_, i) => i != index).Any(mission => mission.Kind == kind && (kind != MissionKind.Color || mission.Color == color)))
                return "같은 대상의 미션이 이미 있습니다.";
            using SerializedObject data = new SerializedObject(level);
            SerializedProperty list = data.FindProperty("missions");
            if (index == list.arraySize) list.arraySize++;
            SerializedProperty item = list.GetArrayElementAtIndex(index);
            item.FindPropertyRelative("kind").intValue = (int)kind;
            item.FindPropertyRelative("color").intValue = (int)color;
            item.FindPropertyRelative("count").intValue = kind == MissionKind.Mold ? 0 : count;
            LevelObstacleEditing.Commit(data, "미션 편집"); return null;
        }

        public static string Add(LevelDefinition level)
        {
            if (!LevelSupplyEditing.CanEdit(level)) return "편집할 수 없는 레벨입니다.";
            foreach (RabbitColor color in (level.Colors ?? Array.Empty<RabbitColor>()).Where(color => Enum.IsDefined(typeof(RabbitColor), color)).Distinct())
                if (!level.Missions.Any(mission => mission.Kind == MissionKind.Color && mission.Color == color))
                    return Set(level, level.Missions.Count, MissionKind.Color, color, 1);
            foreach (MissionKind kind in Enum.GetValues(typeof(MissionKind)))
                if (kind != MissionKind.Color && !level.Missions.Any(mission => mission.Kind == kind))
                    return Set(level, level.Missions.Count, kind, RabbitColor.Type1, 1);
            return "추가할 수 있는 미션이 없습니다.";
        }
    }
}
