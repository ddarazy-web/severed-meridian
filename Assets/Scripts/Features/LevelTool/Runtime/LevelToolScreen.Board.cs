#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using LevelAuthoring.Editing;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private JObject Placement(JObject level, int cell)
        {
            return ((JArray)level["elements"]).OfType<JObject>().FirstOrDefault(item =>
            {
                if ((string)item["layer"] != layer) return false;
                int row = (int)item["coordinate"]["row"], column = (int)item["coordinate"]["column"];
                int size = definitions.TryGetValue((string)item["definitionId"], out JObject definition) ? LevelToolDocuments.Size(definition) : 1;
                return cell / 9 >= row && cell / 9 < row + size && cell % 9 >= column && cell % 9 < column + size;
            });
        }

        private void DrawBoard()
        {
            JObject level = LevelToolDocuments.UpgradePreview(Session.Get(Session.SelectedLevelId).Data);
            board.style.width = cellSize * 9; board.style.height = cellSize * 9;
            for (int index = 0; index < 81; index++)
            {
                int cell = index;
                bool active = (bool)level["board"]["cells"][index]["isActive"];
                Label tile = new Label { name = "cell-" + index };
                tile.AddToClassList("cell"); tile.style.width = cellSize; tile.style.height = cellSize;
                tile.EnableInClassList("inactive", !active);
                tile.EnableInClassList("selected", Session.SelectedCells.Contains(index));
                JObject item = Placement(level, index);
                tile.text = active ? "·" : "×";
                tile.tooltip = "행 " + (index / 9 + 1) + " · 열 " + (index % 9 + 1) + (item == null ? "" : "\n" + item["definitionId"]);
                tile.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0 || busy) return;
                    board.Focus();
                    if (UseTutorialPicker(cell)) return;
                    if (UseFlowTool(cell)) return;
                    Edit(() =>
                    {
                        if (e.shiftKey && anchor >= 0)
                        {
                            int minRow = Math.Min(cell / 9, anchor / 9), maxRow = Math.Max(cell / 9, anchor / 9);
                            int minCol = Math.Min(cell % 9, anchor % 9), maxCol = Math.Max(cell % 9, anchor % 9);
                            Session.SelectCells(Enumerable.Range(0, 81).Where(i => i / 9 >= minRow && i / 9 <= maxRow && i % 9 >= minCol && i % 9 <= maxCol));
                        }
                        else { anchor = cell; Session.SelectCells(new[] { cell }); }
                        if (moveBody != null)
                        {
                            Session.Apply("장애물 이동", docs => LevelDocumentEditing.Move(docs[Session.SelectedLevelId].Data, definitions, moveBody, cell / 9, cell % 9));
                            moveBody = null; return;
                        }
                        if (brush != null && !e.shiftKey)
                            Session.Apply("요소 배치", docs => LevelDocumentEditing.Place(docs[Session.SelectedLevelId].Data, definitions, brush, cell / 9, cell % 9, e.clickCount >= 2, color: color, direction: direction));
                    });
                });
                board.Add(tile);
            }
            DrawFlowOverlay(level);
            DrawConnectionOverlay(level);
            DrawArtwork(level).Forget();
        }

        private void DrawProperties()
        {
            JObject level = Session.Get(Session.SelectedLevelId).Data;
            var pages = new System.Collections.Generic.List<string> { "기본", "공급", "흐름", "연결", "모양", "튜토리얼", "튜토리얼 공급", "자동 시험", "시험 기록" };
            DropdownField page = new DropdownField("편집 항목", pages, Math.Max(0, pages.IndexOf(inspectorPage))) { name = "inspector-page" };
            page.RegisterValueChangedCallback(e => { inspectorPage = e.newValue; CancelFlowTool(); wireDrawing = false; brush = null; moveBody = null; Refresh(); }); inspector.Add(page);
            page.SetEnabled(sharedTutorialDraft == null);
            page.style.display = DisplayStyle.None;
            if ((int)level["schemaVersion"] != 5)
            {
                inspector.Add(new Label("이전 형식의 레벨입니다. 보드·게임 시험은 미리 볼 수 있으며 편집에는 형식 전환이 필요합니다."));
                Button(inspector, "upgrade-level", "편집 형식으로 전환 (Undo 가능)", () => Edit(() =>
                {
                    JObject converted = LevelToolDocuments.UpgradePreview(level);
                    Session.Apply("레벨 편집 형식 전환", docs =>
                    {
                        foreach (string field in new[] { "schemaVersion", "elements", "elementSupply" })
                            docs[Session.SelectedLevelId].Data[field] = converted[field].DeepClone();
                    });
                }));
                return;
            }
            if (inspectorPage == "튜토리얼 공급") { DrawSupply(level, true); return; }
            if (inspectorPage == "자동 시험") { DrawToolBot(); return; }
            if (inspectorPage == "시험 기록") { DrawToolHistory(); DrawToolRecords(); return; }
            if (inspectorPage == "공급") { DrawSupply(level); return; }
            if (inspectorPage == "흐름") { DrawFlow(level); return; }
            if (inspectorPage == "연결") { DrawConnections(level); return; }
            if (inspectorPage == "모양") { DrawShapes(level); return; }
            if (inspectorPage == "튜토리얼") { DrawTutorial(); return; }
            VisualElement container = inspector;
            var settings = new VisualElement { name = "level-settings" }; container.Add(settings); inspector = settings;
            TextField name = new TextField("이름") { name = "level-name", value = (string)level["displayName"], isDelayed = true };
            name.RegisterValueChangedCallback(e => Change("이름 변경", data => data["displayName"] = e.newValue)); inspector.Add(name);
            IntegerField number = new IntegerField("레벨 번호") { name = "level-number", value = (int)level["levelNumber"], isDelayed = true };
            number.RegisterValueChangedCallback(e => Change("번호 변경", data => data["levelNumber"] = e.newValue)); inspector.Add(number);
            IntegerField moves = new IntegerField("이동 횟수") { name = "level-moves", value = (int)level["moveCount"], isDelayed = true };
            moves.RegisterValueChangedCallback(e => Change("이동 횟수", data => data["moveCount"] = e.newValue)); inspector.Add(moves);
            for (int i = 1; i <= 5; i++)
            {
                string value = "Type" + i;
                Toggle toggle = new Toggle("사용 색 " + i) { value = level["colors"].Values<string>().Contains(value) };
                toggle.RegisterValueChangedCallback(e => Change("사용 색", data =>
                {
                    JArray colors = (JArray)data["colors"];
                    if (e.newValue) { if (!colors.Values<string>().Contains(value)) colors.Add(value); }
                    else colors.FirstOrDefault(token => (string)token == value)?.Remove();
                })); inspector.Add(toggle);
            }
            var selection = new VisualElement { name = "selection-properties" }; container.Add(selection); inspector = selection;
            DropdownField brushColor = new DropdownField("배치 색", Enumerable.Range(1, 5).Select(i => "Type" + i).ToList(), int.Parse(color.Substring(4)) - 1);
            brushColor.RegisterValueChangedCallback(e => color = e.newValue); inspector.Add(brushColor);
            DropdownField orientation = new DropdownField("로켓 방향", new System.Collections.Generic.List<string> { "Horizontal", "Vertical" }, direction == "Horizontal" ? 0 : 1);
            orientation.RegisterValueChangedCallback(e => direction = e.newValue); inspector.Add(orientation);
            Slider zoom = new Slider("보드 확대", 32, 92) { value = cellSize };
            zoom.RegisterValueChangedCallback(e => { autoFitBoard = false; cellSize = e.newValue; board.Clear(); DrawBoard(); DrawTutorialPreview(); DrawTutorialPicking(); }); inspector.Add(zoom);
            Button(inspector, "fit-board", "보드를 화면에 맞추기", () => { autoFitBoard = true; FitBoardToViewport(); });
            inspector.Add(new Label("선택 칸 " + Session.SelectedCells.Length + "개 · Shift+클릭으로 영역 선택"));
            Button(inspector, "place-area", "선택 영역에 현재 도구 배치", () => Change("영역 배치", data =>
            {
                if (brush == null) throw new InvalidOperationException("먼저 왼쪽에서 배치할 요소를 선택하세요.");
                foreach (int cell in Session.SelectedCells) LevelDocumentEditing.Place(data, definitions, brush, cell / 9, cell % 9, false, color: color, direction: direction);
            }));
            Button(inspector, "erase", "선택 층 지우기", () => Change("선택 삭제", data =>
            {
                foreach (int cell in Session.SelectedCells) LevelDocumentEditing.Erase(data, definitions, layer, cell / 9, cell % 9);
            }));
            Button(inspector, "sources", "선택 칸에 생성구 추가", () => Change("생성구 추가", data => SupplyDocumentEditing.PlaceSources(data, Session.SelectedCells, false)));
            Button(inspector, "erase-sources", "선택 칸 생성구 삭제", () => Change("생성구 삭제", data => SupplyDocumentEditing.PlaceSources(data, Session.SelectedCells, true)));
            if (Session.SelectedCells.Length == 1)
            {
                JObject item = Placement(level, Session.SelectedCells[0]);
                if (item != null)
                {
                    string id = (string)item["instanceId"];
                    inspector.Add(new Label((string)item["definitionId"]));
                    IntegerField durability = new IntegerField("남은 내구도") { value = (int)item["durability"], isDelayed = true };
                    durability.name = "selected-durability";
                    durability.RegisterValueChangedCallback(e => Change("내구도 변경", data =>
                        data["elements"].First(value => (string)value["instanceId"] == id)["durability"] = e.newValue)); inspector.Add(durability);
                    Button(inspector, "apply-color-direction", "선택 요소에 배치 색·방향 적용", () => Change("요소 색·방향", data =>
                    {
                        JToken target = data["elements"].First(value => (string)value["instanceId"] == id);
                        target["color"] = color; target["rocketDirection"] = direction;
                    }));
                    if ((string)item["layer"] == "Obstacle")
                        Button(inspector, "move-body", "이 장애물 이동 → 목적지 클릭", () => { moveBody = id; brush = null; Show("장애물이 이동할 기준 칸을 클릭하세요."); });
                }
            }
            inspector = settings;
            DrawMissions(level);
            IntegerField seedInput = new IntegerField("시험 시드") { value = seed, isDelayed = true };
            seedInput.RegisterValueChangedCallback(e => seed = e.newValue); inspector.Add(seedInput);
            DropdownField tutorial = new DropdownField("튜토리얼 시험", Enum.GetNames(typeof(Tutorial.TutorialRunMode)).ToList(), (int)tutorialMode);
            tutorial.RegisterValueChangedCallback(e => tutorialMode = (Tutorial.TutorialRunMode)Enum.Parse(typeof(Tutorial.TutorialRunMode), e.newValue)); inspector.Add(tutorial);
            inspector.Add(new Label("튜토리얼·고정 공급 목록·연결/흐름은 원본 그대로 보존됩니다."));
            inspector = container;
        }

        private void DrawMissions(JObject level)
        {
            string[] kinds = { "Color", "Crate", "Web", "Scrap", "Dust", "Safe", "ColorLock", "Appliance", "Mold", "Recovery" };
            JArray missions = (JArray)level["missions"];
            inspector.Add(new Label("미션 (최대 4개)"));
            for (int i = 0; i < missions.Count; i++)
            {
                int index = i;
                string kind = (string)missions[i]["kind"], missionColor = (string)missions[i]["color"];
                int count = (int)missions[i]["count"];
                DropdownField target = new DropdownField("대상", kinds.ToList(), Math.Max(0, Array.IndexOf(kinds, kind)));
                target.RegisterValueChangedCallback(e => Change("미션 대상", data => MissionDocumentEditing.Set(data, index, e.newValue, missionColor, Math.Max(1, count)))); inspector.Add(target);
                DropdownField colors = new DropdownField("색", Enumerable.Range(1, 5).Select(n => "Type" + n).ToList(), Math.Max(0, int.Parse(missionColor.Substring(4)) - 1));
                colors.RegisterValueChangedCallback(e => Change("미션 색", data => MissionDocumentEditing.Set(data, index, kind, e.newValue, count))); inspector.Add(colors);
                IntegerField amount = new IntegerField("목표 수량") { value = count, isDelayed = true };
                amount.RegisterValueChangedCallback(e => Change("미션 수량", data => MissionDocumentEditing.Set(data, index, kind, missionColor, e.newValue))); inspector.Add(amount);
                Button(inspector, "remove-mission-" + i, "미션 삭제", () => Change("미션 삭제", data => MissionDocumentEditing.Remove(data, index)));
            }
            Button(inspector, "add-mission", "미션 추가", () => Change("미션 추가", data =>
                MissionDocumentEditing.Set(data, ((JArray)data["missions"]).Count, "Color", color, 10)));
        }
    }
}
#endif
