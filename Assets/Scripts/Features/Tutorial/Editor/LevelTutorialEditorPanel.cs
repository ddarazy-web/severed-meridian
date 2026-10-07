using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Tutorial.Editor
{
    /// <summary>레벨 설정의 제작 패널. 보드 대상 선택은 배치 도구와 독립적이며 두 칸 선택은 한 번에 확정한다.</summary>
    public sealed class LevelTutorialEditorPanel : VisualElement, IDisposable
    {
        private readonly LevelDefinition owner;
        private readonly LevelBoardView board;
        private readonly SerializedObject input;
        private readonly Action<int> selectionChanged;
        private int selected;
        private bool disposed;
        private Label pickStatus;
        private Action<BoardCoordinate> picker;
        private readonly VisualElement fields = new VisualElement();

        public LevelTutorialEditorPanel(LevelDefinition owner, LevelBoardView board, int selected, Action<int> selectionChanged)
        {
            this.owner = owner; this.board = board; this.selected = selected; this.selectionChanged = selectionChanged;
            input = new SerializedObject(owner); name = "tutorial-editor";
            Foldout foldout = new Foldout { text = "레벨 튜토리얼", value = true }; Add(foldout); foldout.Add(fields);
            board.Cancelled += CancelPicking;
            Rebuild();
        }

        private void Edit(Action<SerializedProperty> change)
        {
            if (disposed || owner == null) return;
            Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("레벨 튜토리얼 편집");
            input.Update(); change(input.FindProperty("tutorial")); input.ApplyModifiedProperties();
            Undo.IncrementCurrentGroup(); Rebuild();
        }

        private void Rebuild()
        {
            CancelPicking(); fields.Unbind(); fields.Clear(); input.Update();
            SerializedProperty tutorial = input.FindProperty("tutorial"), steps = tutorial.FindPropertyRelative("steps");
            selected = Math.Clamp(selected, 0, Math.Max(0, steps.arraySize - 1)); selectionChanged(selected);
            fields.Add(new HelpBox("정적 검사만 제공합니다. 지정 행동·후속 결과의 실행 재생은 다음 단계에서 검사합니다. 좌표 저장값은 0부터 시작합니다.", HelpBoxMessageType.Info));
            fields.Add(new PropertyField(tutorial.FindPropertyRelative("seed"), "튜토리얼 고정 시드"));
            fields.Add(new PropertyField(tutorial.FindPropertyRelative("supply"), "튜토리얼 고정 공급"));
            fields.Add(new Button(() => Edit(value =>
            {
                SerializedProperty list = value.FindPropertyRelative("steps"); selected = list.arraySize; list.arraySize++;
                SerializedProperty step = list.GetArrayElementAtIndex(selected);
                step.FindPropertyRelative("kind").enumValueIndex = 0;
                step.FindPropertyRelative("instructions").stringValue = "새 안내";
                step.FindPropertyRelative("highlights").ClearArray(); step.FindPropertyRelative("results").ClearArray();
                step.FindPropertyRelative("hasFirst").boolValue = step.FindPropertyRelative("hasSecond").boolValue = false;
                step.FindPropertyRelative("actionDefinitionId").stringValue = ""; step.FindPropertyRelative("item").enumValueIndex = 0;
                step.FindPropertyRelative("first").FindPropertyRelative("row").intValue = step.FindPropertyRelative("first").FindPropertyRelative("column").intValue = 0;
                step.FindPropertyRelative("second").FindPropertyRelative("row").intValue = step.FindPropertyRelative("second").FindPropertyRelative("column").intValue = 0;
            })) { text = "단계 추가", name = "tutorial-add" });
            if (steps.arraySize > 0)
            {
                PopupField<string> selector = new PopupField<string>("편집 단계", Enumerable.Range(0, steps.arraySize).Select(index => $"{index + 1}단계").ToList(), selected) { name = "tutorial-step" };
                selector.RegisterValueChangedCallback(_ => { selected = selector.index; Rebuild(); }); fields.Add(selector);
                VisualElement buttons = new VisualElement(); buttons.style.flexDirection = FlexDirection.Row; fields.Add(buttons);
                Button previous = new Button(() => Edit(value => { value.FindPropertyRelative("steps").MoveArrayElement(selected, selected - 1); selected--; })) { text = "위로", name = "tutorial-up" };
                previous.SetEnabled(selected > 0); buttons.Add(previous);
                Button next = new Button(() => Edit(value => { value.FindPropertyRelative("steps").MoveArrayElement(selected, selected + 1); selected++; })) { text = "아래로", name = "tutorial-down" };
                next.SetEnabled(selected + 1 < steps.arraySize); buttons.Add(next);
                buttons.Add(new Button(() => Edit(value => value.FindPropertyRelative("steps").DeleteArrayElementAtIndex(selected))) { text = "삭제", name = "tutorial-delete" });
                SerializedProperty current = steps.GetArrayElementAtIndex(selected);
                foreach ((string key, string label) in new[] { ("kind", "단계 종류"), ("instructions", "안내 문구"), ("highlights", "강조 칸"),
                    ("hasFirst", "첫 대상 지정"), ("first", "첫 대상 좌표"), ("hasSecond", "둘째 대상 지정"), ("second", "둘째 대상 좌표"),
                    ("item", "체험 아이템"), ("actionDefinitionId", "발동 파워 정의 ID"), ("results", "생성·발동·제거 조건") })
                    fields.Add(new PropertyField(current.FindPropertyRelative(key), label) { name = "tutorial-field-" + key });
                fields.Add(new Button(() => BeginPicking(true)) { text = "보드에서 교환 두 칸 선택", name = "tutorial-pick-pair" });
                fields.Add(new Button(() => BeginPicking(false)) { text = "보드에서 아이템 한 칸 선택", name = "tutorial-pick-item" });
                fields.Add(new Button(BeginHighlightPicking) { text = "보드에서 강조 칸 추가/제거", name = "tutorial-pick-highlight" });
                fields.Add(new Button(CancelPicking) { text = "대상 선택 취소", name = "tutorial-cancel-pick" });
            }
            pickStatus = new Label("대상 선택 중에는 블록을 칠하거나 교체하지 않습니다.") { name = "tutorial-pick-status" };
            pickStatus.style.whiteSpace = WhiteSpace.Normal; fields.Add(pickStatus);
            foreach (LevelValidationIssue issue in LevelTutorialValidator.Validate(owner))
                fields.Add(new HelpBox(issue.ToString(), HelpBoxMessageType.Error) { name = "tutorial-error" });
            fields.Bind(input); ShowTargets();
        }

        private void BeginPicking(bool pair)
        {
            board.CancelStroke(); BoardCoordinate? first = null;
            picker = coordinate =>
            {
                if (pair && !first.HasValue)
                {
                    first = coordinate; pickStatus.text = "두 번째 칸을 선택하세요. Esc로 취소할 수 있습니다.";
                    board.ShowTutorialTargets(owner.Tutorial.steps[selected].highlights, coordinate, null); return;
                }
                Edit(value =>
                {
                    SerializedProperty step = value.FindPropertyRelative("steps").GetArrayElementAtIndex(selected);
                    step.FindPropertyRelative("hasFirst").boolValue = true; step.FindPropertyRelative("hasSecond").boolValue = pair;
                    SetCoordinate(step.FindPropertyRelative("first"), first ?? coordinate);
                    if (pair) SetCoordinate(step.FindPropertyRelative("second"), coordinate);
                });
            };
            board.TutorialTargetPicked = picker; board.Focus(); pickStatus.text = pair ? "첫 번째 칸을 선택하세요." : "아이템 대상 칸을 선택하세요.";
        }

        private void BeginHighlightPicking()
        {
            board.CancelStroke();
            picker = coordinate => Edit(value =>
            {
                SerializedProperty list = value.FindPropertyRelative("steps").GetArrayElementAtIndex(selected).FindPropertyRelative("highlights");
                int index = owner.Tutorial.steps[selected].highlights.IndexOf(coordinate);
                if (index >= 0) list.DeleteArrayElementAtIndex(index);
                else { list.arraySize++; SetCoordinate(list.GetArrayElementAtIndex(list.arraySize - 1), coordinate); }
            });
            board.TutorialTargetPicked = picker; board.Focus(); pickStatus.text = "강조할 칸을 선택하세요. 이미 강조한 칸은 제거합니다.";
        }

        private static void SetCoordinate(SerializedProperty value, BoardCoordinate coordinate)
        { value.FindPropertyRelative("row").intValue = coordinate.Row; value.FindPropertyRelative("column").intValue = coordinate.Column; }

        private void ShowTargets()
        {
            TutorialStepDefinition step = owner.Tutorial.steps?.ElementAtOrDefault(selected);
            board.ShowTutorialTargets(step?.highlights ?? new List<BoardCoordinate>(), step?.hasFirst == true ? step.first : null, step?.hasSecond == true ? step.second : null);
        }

        private void CancelPicking()
        {
            if (board.TutorialTargetPicked == picker) board.TutorialTargetPicked = null;
            picker = null;
            if (pickStatus != null) pickStatus.text = "대상 선택 대기";
            if (!disposed && owner != null) ShowTargets();
        }

        public void Dispose()
        {
            if (disposed) return; disposed = true; CancelPicking(); board.Cancelled -= CancelPicking;
            board.ShowTutorialTargets(Array.Empty<BoardCoordinate>(), null, null); fields.Unbind(); input.Dispose();
        }
    }
}
