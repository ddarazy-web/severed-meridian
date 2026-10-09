using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        private VisualElement duplicatePanel;
        private LevelDefinition duplicateSource;
        private string duplicateSnapshot;
        private IntegerField duplicateNumber;

        private void ShowDuplicatePanel()
        {
            if (BlockJsonDraftExternalAction() || editingTutorialFlow != null) return;
            board.CancelStroke();
            duplicatePanel.Clear();
            if (level == null) { operation.text = "복제할 레벨을 선택하세요."; return; }
            duplicateSource = level;
            duplicateSnapshot = JsonUtility.ToJson(level);
            duplicatePanel.style.display = DisplayStyle.Flex;
            duplicateNumber = new IntegerField("새 레벨 번호") { name = "duplicate-number", value = IsJsonMode ? jsonWorkspace.Session.Documents.Where(doc => doc.Kind == "level").Max(doc => (int)doc.Data["levelNumber"]) + 1 : LevelAssetOperations.SuggestDuplicateNumber(level) ?? 0, isDelayed = true };
            VisualElement actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row;
            duplicateNumber.style.width = 220;
            duplicateNumber.style.flexShrink = 0;
            actions.Add(duplicateNumber);
            actions.Add(new Button(() =>
            {
                if (duplicateNumber.value <= 0) { operation.text = "새 레벨 번호를 양수로 입력하세요."; return; }
                if (IsJsonMode) { JsonAction(() => DuplicateJsonLevel(duplicateNumber.value)); return; }
                string path = EditorUtility.SaveFilePanelInProject("레벨 복제", "Level_" + duplicateNumber.value, "asset", "새 레벨의 저장 위치를 선택하세요.");
                if (!string.IsNullOrEmpty(path)) DuplicateCurrentTo(path);
            }) { text = IsJsonMode ? "JSON 레벨 복제" : "경로 선택·복제", name = "confirm-duplicate" });
            actions.Add(new Button(() => { duplicatePanel.style.display = DisplayStyle.None; duplicateSource = null; })
                { text = "취소", name = "cancel-duplicate" });
            duplicatePanel.Add(actions);
            Label help = new Label(IsJsonMode ? "현재 JSON 내용을 새 문서 ID로 복제합니다. 저장 전에는 작업 폴더에 기록하지 않으며 실행 취소할 수 있습니다." : "미저장 내용을 포함합니다. 원본은 저장하지 않습니다. 생성한 파일 삭제는 Project 창에서 합니다.");
            help.style.whiteSpace = WhiteSpace.Normal;
            duplicatePanel.Add(help);
        }

        internal void DuplicateCurrentTo(string path)
        {
            board.CancelStroke();
            if (level != duplicateSource || level == null || JsonUtility.ToJson(level) != duplicateSnapshot)
            { operation.text = "원본이 변경되었습니다. 복제를 다시 시작하세요."; return; }
            try
            {
                LevelDefinition copy = LevelAssetOperations.DuplicateAtPath(level, path, duplicateNumber.value);
                SetLevel(copy);
                Selection.activeObject = copy;
                operation.text = $"레벨 {copy.LevelNumber} 복제 완료 · {path}";
            }
            catch (Exception exception) when (exception is ArgumentException || exception is System.IO.IOException || exception is UnauthorizedAccessException || exception is UnityException)
            { operation.text = exception.Message; }
        }
    }
}
