using System;
using System.Linq;
using LevelAuthoring.Editing;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        private void ShowJsonShapes()
        {
            refreshShapeUsage = null;
            board?.CancelStroke(); data?.ApplyModifiedPropertiesWithoutUndo();
            jsonWorkspace.Capture("맵 모양 입력 확정"); PersistJsonState();
            recommendationPanel.Clear(); recommendationPanel.style.display = DisplayStyle.Flex;
            recommendationPanel.Add(new Label("JSON 맵 모양 · 현재 작업 폴더에서 등록하고 재사용"));
            recommendationPanel.Add(new Button(() => recommendationPanel.style.display = DisplayStyle.None) { text = "닫기" });
            var name = new TextField("등록할 모양 이름") { value = level.name, name = "json-shape-name" };
            recommendationPanel.Add(name);
            recommendationPanel.Add(new Button(() => JsonAction(() =>
            {
                ShapeDocumentEditing.Register(jsonWorkspace.Session, jsonWorkspace.Session.SelectedLevelId, name.value,
                    LevelShapeRecommendations.DescribeObstacles(level));
                PersistJsonState(); ShowJsonShapes();
            })) { text = "현재 모양 등록", name = "register-json-shape", tooltip = "현재 활성 칸과 장애물 설명을 등록합니다. JSON 저장 버튼으로 파일에 확정하세요." });
            var shapes = jsonWorkspace.Session.Documents.Where(doc => doc.Kind == "shape").OrderBy(doc => (string)doc.Data["displayName"]).ToArray();
            if (shapes.Length == 0) { recommendationPanel.Add(new Label("등록한 JSON 모양이 없습니다.")); return; }
            var choice = new PopupField<string>("모양", shapes.Select(doc => (string)doc.Data["displayName"] + " · " + doc.Id.Substring(Math.Max(0, doc.Id.Length - 6))).ToList(), 0);
            recommendationPanel.Add(choice);
            var rename = new TextField("선택한 모양 이름"); recommendationPanel.Add(rename);
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row;
            row.Add(new Button(() => JsonAction(() =>
            {
                if (string.IsNullOrWhiteSpace(rename.value)) throw new ArgumentException("모양 이름을 입력하세요.");
                DocumentEditing.SetField(jsonWorkspace.Session, shapes[choice.index].Id, "displayName", new JValue(rename.value.Trim()));
                PersistJsonState(); ShowJsonShapes();
            })) { text = "이름 변경" });
            var delete = new Button(() => JsonAction(() =>
            {
                ShapeDocumentEditing.DeleteUnused(jsonWorkspace.Session, shapes[choice.index].Id);
                PersistJsonState(); ShowJsonShapes();
            })) { text = "미사용 모양 삭제", tooltip = "현재 JSON 레벨에서 사용하지 않는 모양만 삭제합니다. 실행 취소로 되돌릴 수 있습니다." };
            row.Add(delete); recommendationPanel.Add(row);
            var detail = new ScrollView { name = "json-shape-detail" }; detail.style.maxHeight = 180; recommendationPanel.Add(detail);
            var number = new IntegerField("새 레벨 번호") { value = jsonWorkspace.Session.Documents.Where(doc => doc.Kind == "level").Max(doc => (int)doc.Data["levelNumber"]) + 1 };
            var levelName = new TextField("새 레벨 이름") { value = "새 레벨" };
            recommendationPanel.Add(number); recommendationPanel.Add(levelName);
            recommendationPanel.Add(new Button(() => JsonAction(() =>
            {
                JObject template = CreateJsonLevelTemplate();
                ShapeDocumentEditing.ApplyToNewLevel(template, shapes[choice.index].Data);
                AddJsonLevel(template, number.value, levelName.value);
                recommendationPanel.style.display = DisplayStyle.None; SelectWorkspaceTab(0);
            })) { text = "이 모양으로 새 JSON 레벨", name = "create-json-shape-level", tooltip = "활성 칸과 기본 생성구만 적용합니다. 장애물·미션·튜토리얼을 복사하지 않습니다." });
            choice.RegisterValueChangedCallback(_ => ShowDetail());
            refreshShapeUsage = ShowDetail; ShowDetail();
            void ShowDetail()
            {
                detail.Clear(); var shape = shapes[choice.index];
                rename.SetValueWithoutNotify((string)shape.Data["displayName"]);
                string[] usage = ShapeDocumentEditing.Usage(jsonWorkspace.Session, shape.Id); delete.SetEnabled(usage.Length == 0);
                detail.Add(new Label($"사용 레벨 {usage.Length}개 · 현재 JSON 편집 내용 기준"));
                foreach (string id in usage) detail.Add(new Label((string)jsonWorkspace.Session.Get(id).Data["displayName"]));
                var mask = (JArray)shape.Data["cells"];
                for (int r = 0; r < 9; r++)
                {
                    var line = new VisualElement(); line.style.flexDirection = FlexDirection.Row;
                    for (int c = 0; c < 9; c++)
                    {
                        var cell = new VisualElement(); cell.style.width = 12; cell.style.height = 12; cell.style.marginRight = 1; cell.style.marginBottom = 1;
                        cell.style.backgroundColor = (Color)((bool)mask[r * 9 + c] ? new Color32(92,184,168,255) : new Color32(55,59,65,255)); line.Add(cell);
                    }
                    detail.Add(line);
                }
                detail.Add(new Label("등록 당시: " + shape.Data["sourceName"] + " · " + string.Join(" / ", (shape.Data["obstacleHistory"] as JArray ?? new JArray()).Values<string>())));
            }
        }
    }
}
