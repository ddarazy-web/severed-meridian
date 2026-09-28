using System.Collections.Generic;
using Levels;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    [CustomEditor(typeof(LevelDefinition))]
    public sealed class LevelDefinitionInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            root.Add(new Button(() => LevelEditorWindow.OpenLevel((LevelDefinition)target))
                { text = "보드 편집 창 열기", name = "open-level-editor" });
            root.Add(new HelpBox("레벨 데이터 편집. 구조·공급 수량 검사 통과는 플레이 가능 판정이 아닙니다. 실제 낙하·도달 가능성·시작 매칭·난이도는 검사하지 않습니다.\n" +
                "좌표는 행·열 1부터 표시합니다. 배치가 없는 활성 칸은 빈칸입니다.", HelpBoxMessageType.Info));

            PropertyField version = new PropertyField(serializedObject.FindProperty("schemaVersion"), "저장 형식 버전");
            version.SetEnabled(false);
            root.Add(version);
            VisualElement fields = new VisualElement();
            root.Add(fields);
            fields.Add(new PropertyField(serializedObject.FindProperty("levelNumber"), "레벨 번호"));
            fields.Add(new PropertyField(serializedObject.FindProperty("moveCount"), "이동 횟수"));
            fields.Add(new PropertyField(serializedObject.FindProperty("colors"), "사용할 달토끼 종류"));
            fields.Add(new PropertyField(serializedObject.FindProperty("board"), "보드"));
            fields.Add(new PropertyField(serializedObject.FindProperty("initialBlocks"), "초기 일반/파워 블록 배치"));
            fields.Add(new PropertyField(serializedObject.FindProperty("obstacles"), "장애물/장치"));
            fields.Add(new PropertyField(serializedObject.FindProperty("covers"), "덮개"));
            fields.Add(new PropertyField(serializedObject.FindProperty("dust"), "먼지"));
            fields.Add(new PropertyField(serializedObject.FindProperty("flow"), "바닥 흐름·벽·통로"));
            fields.Add(new PropertyField(serializedObject.FindProperty("connections"), "발전기 연결·전선"));
            fields.Add(new PropertyField(serializedObject.FindProperty("supply"), "생성구·공급 설정"));
            fields.Add(new PropertyField(serializedObject.FindProperty("recoveryParts"), "회수 부품"));
            fields.Add(new PropertyField(serializedObject.FindProperty("missions"), "미션"));
            fields.SetEnabled(((LevelDefinition)target).SchemaVersion == LevelDefinition.CurrentSchemaVersion);
            Label upgradeStatus = new Label();
            Button upgrade = new Button(() =>
            {
                LevelSchemaUpgrade.Upgrade((LevelDefinition)target, out string message);
                upgradeStatus.text = message;
            }) { text = "5단계 형식으로 전환", name = "upgrade-level" };
            upgrade.style.display = ((LevelDefinition)target).SchemaVersion >= 1 && ((LevelDefinition)target).SchemaVersion < LevelDefinition.CurrentSchemaVersion ? DisplayStyle.Flex : DisplayStyle.None;
            root.Add(upgrade);
            root.Add(upgradeStatus);

            VisualElement results = new VisualElement();
            Label status = new Label("검사 전입니다. 변경 후에는 다시 검사하세요.");
            root.Add(new Button(() =>
            {
                serializedObject.ApplyModifiedProperties();
                List<LevelValidationIssue> issues = LevelDefinitionValidator.Validate((LevelDefinition)target);
                issues.AddRange(LevelAssetOperations.FindNumberConflicts((LevelDefinition)target));
                results.Clear();
                status.text = issues.Count == 0 ? "구조 오류 없음 (현재 검사 기준)" : $"구조 오류 {issues.Count}개 (현재 검사 기준)";
                foreach (LevelValidationIssue issue in issues)
                    results.Add(new HelpBox(issue.ToString(), HelpBoxMessageType.Error));
            }) { text = "데이터 검사", name = "validate-level" });
            root.Add(new Button(() =>
            {
                serializedObject.ApplyModifiedProperties();
                AssetDatabase.SaveAssetIfDirty(target);
            }) { text = "레벨 저장 (오류 데이터도 보존)", name = "save-level" });
            root.Add(status);
            root.Add(results);
            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                fields.SetEnabled(((LevelDefinition)target).SchemaVersion == LevelDefinition.CurrentSchemaVersion);
                upgrade.style.display = ((LevelDefinition)target).SchemaVersion >= 1 && ((LevelDefinition)target).SchemaVersion < LevelDefinition.CurrentSchemaVersion ? DisplayStyle.Flex : DisplayStyle.None;
                results.Clear();
                status.text = "데이터가 변경되었습니다. 다시 검사하세요.";
            });
            return root;
        }
    }
}
