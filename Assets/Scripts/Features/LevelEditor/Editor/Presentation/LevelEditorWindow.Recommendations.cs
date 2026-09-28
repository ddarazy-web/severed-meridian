using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        private VisualElement recommendationPanel;

        /// <summary>등록된 모양 목록을 표시한다. 선택과 미리보기만으로는 레벨을 변경하지 않는다.</summary>
        internal void ShowShapeRecommendations()
        {
            board?.CancelStroke(); data?.ApplyModifiedProperties();
            recommendationPanel.Clear(); recommendationPanel.style.display = DisplayStyle.Flex;
            Label heading = new Label("맵 모양 목록 · 등록하고 다시 사용하기");
            LevelEditorHelp.Link(heading, "저장한 레벨의 모양과 사용 장애물 기록을 등록합니다. 목록에서 고른 모양으로 새 레벨을 만듭니다.", "recommendations.html#choose");
            VisualElement top = new VisualElement(); top.style.flexDirection = FlexDirection.Row;
            heading.style.flexGrow = 1; top.Add(heading);
            top.Add(new Button(() => recommendationPanel.style.display = DisplayStyle.None) { text = "닫기", name = "close-shape-recommendations" });
            recommendationPanel.Add(top);
            Label status = new Label { name = "shape-status" }; status.style.whiteSpace = WhiteSpace.Normal;
            Button register = new Button(() =>
            {
                board?.CancelStroke(); data?.ApplyModifiedProperties();
                try
                {
                    string error = LevelShapeRecommendations.Register(level, out LevelShapePreset saved);
                    if (saved == null) { status.text = error; return; }
                    ShowShapeRecommendations();
                    var refreshed = recommendationPanel.Q<PopupField<string>>("shape-choice");
                    if (refreshed != null) refreshed.value = saved.name;
                    recommendationPanel.Q<Label>("shape-status").text = error ?? "등록했습니다: " + saved.name;
                }
                catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is UnityException || exception is UnauthorizedAccessException)
                { status.text = "등록하지 못했습니다. " + exception.Message; }
            }) { text = "현재 레벨 모양 등록", name = "register-shape", tooltip = "레벨을 저장한 뒤 등록하세요. 활성 칸 모양이 같으면 중복으로 안내하고 기존 기록을 유지합니다." };
            recommendationPanel.Add(register);
            // 목록을 열 때만 읽는다. 임의 모양 생성이나 사용 횟수에 따른 자동 정렬은 하지 않는다.
            var registered = LevelShapeRecommendations.LoadAll();
            if (registered.Count == 0)
            {
                recommendationPanel.Add(new Label("등록한 모양이 없습니다. 마음에 드는 레벨을 저장하고 위 버튼으로 등록하세요.") { name = "shape-empty" });
                recommendationPanel.Add(status);
                return;
            }
            IntegerField number = new IntegerField("새 레벨 번호") { name = "shape-level-number", value = Math.Max(1, level?.LevelNumber + 1 ?? 1), isDelayed = true };
            number.tooltip = "새로 만들 레벨의 번호입니다. 같은 번호가 있으면 생성 전에 안내합니다.";
            VisualElement options = new VisualElement(); options.style.flexDirection = FlexDirection.Row;
            number.style.width = 230; options.Add(number); recommendationPanel.Add(options);
            PopupField<string> choices = new PopupField<string>("등록 모양", registered.Select(item => item.name).ToList(), 0) { name = "shape-choice" };
            choices.tooltip = "직접 등록한 모양을 이름순으로 표시합니다. 원하는 모양을 반복해서 사용할 수 있습니다.";
            choices.style.flexGrow = 1; options.Add(choices);
            ScrollView detail = new ScrollView { name = "shape-detail" }; detail.style.maxHeight = 265; detail.style.flexShrink = 1; recommendationPanel.Add(detail);
            TextField filename = new TextField("새 파일 이름") { name = "shape-file-name", value = Path.GetFileNameWithoutExtension(AssetDatabase.GenerateUniqueAssetPath(LevelAssetOperations.DefaultFolder + "/Level.asset")) };
            recommendationPanel.Add(filename);
            VisualElement actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row;
            actions.Add(new Button(() =>
            {
                string error = LevelAssetOperations.FileNameError(filename.value);
                if (error != null) { status.text = error; return; }
                if (number.value < 1) { status.text = "레벨 번호는 1 이상으로 입력하세요."; return; }
                LevelShapePreset selectedShape = registered[choices.index];
                if (selectedShape == null || !selectedShape.IsValid) { status.text = "등록 항목이 변경되었습니다. 목록을 닫고 다시 여세요."; return; }
                // 열린 뒤 다른 창에서 레벨을 만들었을 수 있으므로 확정 직전에 다시 확인한다.
                bool duplicate = AssetDatabase.FindAssets("t:LevelDefinition", new[] { "Assets" }).Any(guid =>
                    AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid))?.LevelNumber == number.value);
                if (duplicate) { status.text = "같은 레벨 번호가 있습니다. 새 레벨 번호를 바꿔 주세요."; return; }
                try
                {
                    LevelDefinition created = LevelAssetOperations.CreateNamed(filename.value);
                    LevelShapeRecommendations.Initialize(created, selectedShape, number.value);
                    SetLevel(created);
                    recommendationPanel.style.display = DisplayStyle.None;
                    SelectWorkspaceTab(0);
                    operation.text = "등록 모양으로 새 레벨을 만들었습니다. 사용 장애물 기록을 참고하여 직접 배치한 뒤 검사·플레이 테스트를 진행하세요.";
                }
                catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is UnityException || exception is InvalidOperationException || exception is UnauthorizedAccessException)
                { status.text = "만들기를 완료하지 못했습니다. " + exception.Message; }
            }) { text = "이 모양으로 새 레벨 만들기", name = "create-shape-level", tooltip = "현재 레벨을 덮어쓰지 않습니다. 활성 칸과 기본 생성구만 설정한 새 파일을 만듭니다." });
            recommendationPanel.Add(actions); recommendationPanel.Add(status);
            choices.RegisterValueChangedCallback(_ => ShowDetail());
            ShowDetail();

            // 선택된 모양의 100칸 미리보기와 설명은 일치해야 하므로 같은 결과 객체에서 함께 그린다.
            void ShowDetail()
            {
                detail.Clear();
                var shape = registered[choices.index];
                if (shape == null || !shape.IsValid) { detail.Add(new Label("등록 항목이 변경되었습니다. 목록을 다시 여세요.")); return; }
                Label description = new Label($"등록 당시 레벨: {shape.SourceName} · 레벨 {shape.SourceLevelNumber}\n사용 칸 {shape.Cells.Count(active => active)}개 · 장애물 기록은 참고용입니다.");
                description.style.whiteSpace = WhiteSpace.Normal; detail.Add(description);
                VisualElement preview = new VisualElement { name = "shape-preview", tooltip = "색 칸은 사용할 칸, 어두운 칸은 비활성 칸입니다. 생성 후 칸을 더 편집할 수 있습니다." };
                for (int r = 0; r < 10; r++)
                {
                    VisualElement row = new VisualElement(); row.style.flexDirection = FlexDirection.Row;
                    for (int c = 0; c < 10; c++)
                    {
                        VisualElement cell = new VisualElement(); cell.style.width = 14; cell.style.height = 14; cell.style.marginRight = 1; cell.style.marginBottom = 1;
                        cell.style.backgroundColor = (Color)(shape.Cells[r*10+c] ? new Color32(92,184,168,255) : new Color32(55,59,65,255)); row.Add(cell);
                    }
                    preview.Add(row);
                }
                VisualElement comparison = new VisualElement(); comparison.style.flexDirection = FlexDirection.Row;
                preview.style.flexShrink = 0; preview.style.marginRight = 12; comparison.Add(preview); detail.Add(comparison);
                Foldout candidates = new Foldout { text = "이 레벨에서 사용했던 장애물", value = true, name = "shape-candidates" };
                candidates.tooltip = "등록 당시 사용한 종류와 개수입니다. 원본을 수정해도 이 기록은 바뀌지 않으며 새 레벨에 자동 배치하지 않습니다.";
                if (shape.ObstacleHistory == null || shape.ObstacleHistory.Count == 0) candidates.Add(new Label("등록 당시 사용한 장애물이 없습니다."));
                foreach (string advice in shape.ObstacleHistory ?? Array.Empty<string>())
                {
                    Label item = new Label(advice); item.style.whiteSpace = WhiteSpace.Normal; candidates.Add(item);
                }
                candidates.style.flexGrow = 1; candidates.style.flexShrink = 1; candidates.style.minWidth = 0;
                comparison.Add(candidates);
                Label note = new Label("모양과 기본 생성구만 적용합니다. 장애물·미션·특수 중력·연결은 복사하지 않습니다. 원본 전체가 필요하면 레벨 복제를 사용하세요.");
                note.style.whiteSpace = WhiteSpace.Normal; detail.Add(note);
            }
        }
    }
}
