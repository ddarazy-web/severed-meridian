using System;
using System.Linq;
using UnityEditor;

namespace Levels.Editor
{
    public static class LevelSchemaUpgrade
    {
        public static string Check(LevelDefinition level)
        {
            if (level == null || level.SchemaVersion < 1 || level.SchemaVersion > 3) return "버전 1·2·3 에셋만 전환할 수 있습니다.";
            if (level.Supply?.Sources == null || level.RecoveryParts == null || level.Missions == null) return "신규 설정 구조가 누락되었습니다. 원본을 확인하세요.";
            if (LevelSupplyRules.HasNewData(level)) return "구버전에 예상 밖 5단계 데이터가 있습니다. 원본을 확인하세요.";
            if (level.SchemaVersion < 3 && LevelFlowRules.HasNewData(level)) return "구버전에 예상 밖 4단계 데이터가 있습니다. 원본을 확인하세요.";
            if (level.SchemaVersion == 1 && ((level.Obstacles?.Count ?? 0) != 0 || (level.Covers?.Count ?? 0) != 0 || (level.Dust?.Count ?? 0) != 0 ||
                (level.InitialBlocks != null && level.InitialBlocks.Any(block =>
                    ((int)block.Kind >= 2 && (int)block.Kind <= 5) || block.RocketDirection != RocketDirection.Horizontal))))
                return "버전 1에 예상 밖 신규 데이터가 있습니다. 원본을 확인하세요.";
            return null;
        }

        public static bool Upgrade(LevelDefinition level, out string message)
        {
            message = Check(level);
            if (message != null) return false;
            using SerializedObject data = new SerializedObject(level);
            data.FindProperty("schemaVersion").intValue = LevelDefinition.CurrentSchemaVersion;
            if (level.SchemaVersion == 1)
            {
                data.FindProperty("obstacles").arraySize = 0;
                data.FindProperty("covers").arraySize = 0;
                data.FindProperty("dust").arraySize = 0;
            }
            if (level.SchemaVersion < 3)
            {
                foreach (string list in new[] { "gravity", "paths", "merges", "walls", "portals", "arrivals" })
                    data.FindProperty("flow." + list).arraySize = 0;
                data.FindProperty("connections").arraySize = 0;
                SerializedProperty obstacles = data.FindProperty("obstacles");
                for (int i = 0; i < obstacles.arraySize; i++)
                    obstacles.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue = Guid.NewGuid().ToString("N");
            }
            data.FindProperty("supply.sources").arraySize = 0;
            data.FindProperty("supply.scrapTarget").intValue = 0;
            data.FindProperty("supply.scrapLimit").intValue = 0;
            data.FindProperty("supply.scrapDurability").intValue = 1;
            data.FindProperty("supply.recoveryTarget").intValue = 0;
            data.FindProperty("recoveryParts").arraySize = 0;
            data.FindProperty("missions").arraySize = 0;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("5단계 레벨 형식으로 전환");
            data.ApplyModifiedProperties();
            level.OnAfterDeserialize();
            Undo.IncrementCurrentGroup();
            message = "버전 4로 전환했습니다. 생성구·미션을 명시적으로 설정하고 저장하세요.";
            return true;
        }
    }
}
