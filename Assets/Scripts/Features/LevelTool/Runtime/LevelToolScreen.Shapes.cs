#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private string selectedShape;
        private void DrawShapes(JObject level)
        {
            inspector.Add(new Label("보드에서 칸을 선택하고 활성 여부를 바꾸세요. 칸을 꺼도 장애물·연결은 삭제되지 않습니다."));
            Button(inspector, "activate-cells", "선택 칸 활성화", () => Change("칸 활성화", data => ShapeDocumentEditing.SetActive(data, Session.SelectedCells, true)));
            Button(inspector, "deactivate-cells", "선택 칸 비활성화", () => Change("칸 비활성화", data => ShapeDocumentEditing.SetActive(data, Session.SelectedCells, false)));
            TextField name = new TextField("등록할 모양 이름") { name = "shape-name", value = (string)level["displayName"] }; inspector.Add(name);
            Button(inspector, "shape-register", "현재 모양 등록", () => Edit(() =>
            {
                string[] history = level["elements"].OfType<JObject>().Where(item => (string)item["layer"] == "Obstacle")
                    .GroupBy(item => (string)item["definitionId"]).Select(group =>
                        (definitions.TryGetValue(group.Key, out JObject definition) ? (string)definition["displayName"] : group.Key) + " " + group.Count() + "개").ToArray();
                selectedShape = ShapeDocumentEditing.Register(Session, Session.SelectedLevelId, name.value, history);
            }));
            ContentDocument[] shapes = Session.Documents.Where(doc => doc.Kind == "shape").OrderBy(doc => (string)doc.Data["displayName"]).ToArray();
            if (shapes.Length == 0) { inspector.Add(new Label("아직 등록한 모양이 없습니다.")); return; }
            if (!shapes.Any(doc => doc.Id == selectedShape)) selectedShape = shapes[0].Id;
            Choice(inspector, "shape-choice", "등록한 모양", shapes.Select(doc => doc.Id).ToArray(),
                shapes.Select(doc => (string)doc.Data["displayName"] + " · " + doc.Id.Substring(Math.Max(0, doc.Id.Length - 6))).ToArray(), selectedShape,
                value => { selectedShape = value; Refresh(); });
            ContentDocument shape = Session.Get(selectedShape);
            TextField rename = new TextField("선택한 모양 이름") { value = (string)shape.Data["displayName"] }; inspector.Add(rename);
            Button(inspector, "shape-rename", "이름 변경", () => Edit(() =>
            {
                if (string.IsNullOrWhiteSpace(rename.value)) throw new InvalidOperationException("모양 이름을 입력하세요.");
                DocumentEditing.SetField(Session, selectedShape, "displayName", rename.value.Trim());
            }));
            string[] usage = ShapeDocumentEditing.Usage(Session, selectedShape);
            Button(inspector, "shape-delete", "미사용 모양 삭제", () => Edit(() => ShapeDocumentEditing.DeleteUnused(Session, selectedShape))).SetEnabled(usage.Length == 0);
            VisualElement preview = new VisualElement { name = "shape-preview", tooltip = "활성 칸 미리보기. 실제 블록·장애물 배치는 포함하지 않습니다." };
            inspector.Add(preview);
            for (int row = 0; row < 9; row++)
            {
                VisualElement line = new VisualElement(); line.style.flexDirection = FlexDirection.Row; preview.Add(line);
                for (int column = 0; column < 9; column++)
                {
                    VisualElement cell = new VisualElement(); cell.style.width = 14; cell.style.height = 14; cell.style.marginRight = 1; cell.style.marginBottom = 1;
                    cell.style.backgroundColor = (bool)shape.Data["cells"][row * 9 + column] ? new Color(.35f, .7f, .65f) : new Color(.15f, .18f, .22f); line.Add(cell);
                }
            }
            VisualElement used = new VisualElement { name = "shape-usage" }; inspector.Add(used);
            used.Add(new Label("같은 활성 칸 모양의 레벨 " + usage.Length + "개"));
            foreach (string id in usage)
                Button(used, "shape-usage-" + id, (string)Session.Get(id).Data["displayName"], () => Edit(() => { Session.SelectLevel(id); CancelFlowTool(); wireDrawing = false; }));
            inspector.Add(new Label("등록 당시: " + shape.Data["sourceName"] + " · " + string.Join(" / ", ((JArray)shape.Data["obstacleHistory"]).Values<string>())));
            IntegerField number = new IntegerField("새 레벨 번호") { value = Session.Documents.Where(doc => doc.Kind == "level").Max(doc => (int)doc.Data["levelNumber"]) + 1 };
            TextField newName = new TextField("새 레벨 이름") { value = "새 레벨" }; inspector.Add(number); inspector.Add(newName);
            Button(inspector, "shape-create-level", "이 모양으로 새 레벨 만들기", () => Edit(() =>
            {
                JObject template = LevelToolDocuments.NewLevel(); template["catalogId"] = level["catalogId"].DeepClone();
                ShapeDocumentEditing.ApplyToNewLevel(template, shape.Data);
                DocumentEditing.AddLevel(Session, template, number.value, newName.value);
                CancelFlowTool(); wireDrawing = false; anchor = -1;
            }));
            inspector.Add(new Label("새 레벨에는 활성 칸과 기본 생성구만 적용합니다. 장애물·미션·튜토리얼은 복사하지 않습니다."));
        }
    }
}
#endif
