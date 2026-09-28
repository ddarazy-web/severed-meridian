using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        private VisualElement levelNamePanel;

        internal void ShowLevelNamePanel(bool create)
        {
            board?.CancelStroke(); data?.ApplyModifiedProperties();
            levelNamePanel.Clear(); levelNamePanel.style.display = DisplayStyle.Flex;
            LevelDefinition target = level;
            // 이름 입력 중 레벨 선택이 바뀌어도 새 선택 대상의 파일을 바꾸면 안 된다.
            // 입력창을 열 때의 대상을 기억하고 확정 시 현재 선택과 같은지 다시 검사한다.
            string currentPath = AssetDatabase.GetAssetPath(target);
            string suggested = create ? Path.GetFileNameWithoutExtension(AssetDatabase.GenerateUniqueAssetPath(LevelAssetOperations.DefaultFolder + "/Level.asset")) : Path.GetFileNameWithoutExtension(currentPath);
            VisualElement row = new VisualElement(); row.style.flexDirection = FlexDirection.Row;
            TextField input = new TextField(create ? "새 파일 이름" : "파일 이름") { name = "level-file-name", value = suggested };
            input.style.flexGrow = 1; input.style.minWidth = 100;
            Label message = new Label(create ? "이름만 입력하세요. Assets/Data/Levels 폴더에 .asset 파일로 만듭니다." : "이름을 바꾸면 현재 레벨 내용도 함께 저장됩니다. 레벨 번호와 보드 배치는 그대로입니다.") { name = "level-name-message" };
            message.style.whiteSpace = WhiteSpace.Normal;
            row.Add(input);
            row.Add(new Button(() =>
            {
                string error = LevelAssetOperations.FileNameError(input.value);
                if (error != null) { message.text = error; return; }
                if (!create && (target == null || level != target)) { message.text = "이름을 바꿀 레벨을 다시 선택하세요."; return; }
                try
                {
                    if (create) SetLevel(LevelAssetOperations.CreateNamed(input.value));
                    else
                    {
                        error = LevelAssetOperations.Rename(target, input.value);
                        if (error != null) { message.text = error; return; }
                        workspaceLevel.SetValueWithoutNotify(null); workspaceLevel.SetValueWithoutNotify(level);
                        Refresh();
                    }
                    levelNamePanel.style.display = DisplayStyle.None;
                    operation.text = (create ? "새 레벨을 만들었습니다: " : "파일 이름을 바꿨습니다: ") + AssetDatabase.GetAssetPath(level);
                }
                catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is UnauthorizedAccessException || exception is UnityException)
                { message.text = "파일 작업을 완료하지 못했습니다. " + exception.Message; }
            }) { text = create ? "만들기" : "이름 변경·저장", name = "confirm-level-name" });
            row.Add(new Button(() => levelNamePanel.style.display = DisplayStyle.None) { text = "취소", name = "cancel-level-name" });
            levelNamePanel.Add(row); levelNamePanel.Add(message);
            input.Focus(); input.SelectAll();
        }

        [MenuItem("Match/사용 설명서")]
        public static void OpenManual()
        {
            LevelEditorHelp.Open("index.html");
        }
    }
}
