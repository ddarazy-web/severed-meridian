using System;
using System.Collections.Generic;
using Levels;
using UnityEditor;

namespace Elements.Editor
{
    /// <summary>원본 저장 없이 변환할 ID 배치 값을 생성한다. 선택 적용은 편집 경계가 맡는다.</summary>
    public static class LevelElementMigration
    {
        public static void Apply(LevelDefinition level)
        {
            ElementPlacementDefinition[] preview = Preview(level);
            ElementLevelSupplyDefinition supplyPreview = PreviewSupply(level);
            List<LevelValidationIssue> issues = LevelDefinitionValidator.Validate(level);
            if (issues.Count > 0) throw new ArgumentException("변환 원본의 검사 오류: " + string.Join(";", issues));
            SerializedObject serialized = new SerializedObject(level);
            SerializedProperty entries = serialized.FindProperty("elements");
            entries.arraySize = preview.Length;
            for (int i = 0; i < preview.Length; i++)
            {
                ElementPlacementDefinition value = preview[i]; SerializedProperty item = entries.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("definitionId").stringValue = value.definitionId;
                item.FindPropertyRelative("instanceId").stringValue = value.instanceId;
                item.FindPropertyRelative("layer").intValue = (int)value.layer;
                SerializedProperty coordinate = item.FindPropertyRelative("coordinate");
                coordinate.FindPropertyRelative("row").intValue = value.coordinate.Row;
                coordinate.FindPropertyRelative("column").intValue = value.coordinate.Column;
                item.FindPropertyRelative("durability").intValue = value.durability;
                item.FindPropertyRelative("hasColor").boolValue = value.hasColor;
                item.FindPropertyRelative("color").intValue = (int)value.color;
                item.FindPropertyRelative("requiredCharge").intValue = value.requiredCharge;
                item.FindPropertyRelative("rocketDirection").intValue = (int)value.rocketDirection;
            }
            serialized.FindProperty("schemaVersion").intValue = 5;
            WriteSupply(serialized.FindProperty("elementSupply"), supplyPreview);
            Undo.SetCurrentGroupName("레벨 ID 배치 선택 변환");
            serialized.ApplyModifiedProperties();
        }

        public static ElementLevelSupplyDefinition PreviewSupply(LevelDefinition level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (level.SchemaVersion != 4) throw new ArgumentException("공급 선택 변환은 구형 스키마4 입력에만 적용합니다.");
            return ElementLevelSupplyDefinition.FromLegacy(level.Supply);
        }

        private static void WriteSupply(SerializedProperty property, ElementLevelSupplyDefinition value)
        {
            property.FindPropertyRelative("scrapTarget").intValue = value.scrapTarget;
            property.FindPropertyRelative("scrapLimit").intValue = value.scrapLimit;
            property.FindPropertyRelative("scrapDurability").intValue = value.scrapDurability;
            property.FindPropertyRelative("recoveryTarget").intValue = value.recoveryTarget;
            property.FindPropertyRelative("scrapDefinitionId").stringValue = value.scrapDefinitionId;
            property.FindPropertyRelative("recoveryDefinitionId").stringValue = value.recoveryDefinitionId;
            SerializedProperty sources = property.FindPropertyRelative("sources"); sources.arraySize = value.sources.Count;
            for (int i = 0; i < value.sources.Count; i++)
            {
                ElementSupplySourceDefinition source = value.sources[i]; SerializedProperty entry = sources.GetArrayElementAtIndex(i);
                SerializedProperty coordinate = entry.FindPropertyRelative("coordinate");
                coordinate.FindPropertyRelative("row").intValue = source.coordinate.Row;
                coordinate.FindPropertyRelative("column").intValue = source.coordinate.Column;
                entry.FindPropertyRelative("mode").intValue = (int)source.mode;
                entry.FindPropertyRelative("exhaustion").intValue = (int)source.exhaustion;
                entry.FindPropertyRelative("randomDefinitionId").stringValue = source.randomDefinitionId;
                SerializedProperty items = entry.FindPropertyRelative("items"); items.arraySize = source.items.Count;
                for (int j = 0; j < source.items.Count; j++)
                {
                    ElementSupplyItemDefinition item = source.items[j]; SerializedProperty target = items.GetArrayElementAtIndex(j);
                    target.FindPropertyRelative("definitionId").stringValue = item.definitionId;
                    target.FindPropertyRelative("count").intValue = item.count;
                    target.FindPropertyRelative("color").intValue = (int)item.color;
                    target.FindPropertyRelative("direction").intValue = (int)item.direction;
                    target.FindPropertyRelative("durability").intValue = item.durability;
                }
            }
        }

        public static ElementPlacementDefinition[] Preview(LevelDefinition level) => LegacyElementLevelAdapter.Preview(level);
    }
}
