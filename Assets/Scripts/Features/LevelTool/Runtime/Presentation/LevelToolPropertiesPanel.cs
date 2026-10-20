#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Editing;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace LevelTool.Presentation
{
    public sealed class LevelToolPropertiesOptions
    {
        public string Layer = "Block", Brush, Color = "Type1", Direction = "Horizontal";
        public float CellSize = 52;
        public int Seed = 12345;
        public Tutorial.TutorialRunMode TutorialMode;
    }

    public sealed class LevelToolPropertiesActions
    {
        public Action<string> SetColor, SetDirection, BeginMove;
        public Action<float> Zoom;
        public Action Fit;
        public Action<int> SetSeed;
        public Action<Tutorial.TutorialRunMode> SetTutorialMode;
    }

    public sealed class LevelToolPropertiesPanel : IDisposable
    {
        private readonly Func<AuthoringEditSession> currentSession;
        private readonly Action<string, Action<JObject>> Change;
        private readonly Func<LevelToolPropertiesOptions> readOptions;
        private readonly LevelToolPropertiesActions actions;
        private VisualElement inspector;
        private int revision;
        private bool disposed;
        public VisualElement Root { get; } = new VisualElement();
        private AuthoringEditSession Session => currentSession();
        private string layer => readOptions().Layer;
        private string brush => readOptions().Brush;
        private string color { get => readOptions().Color; set => actions.SetColor(value); }
        private string direction { get => readOptions().Direction; set => actions.SetDirection(value); }
        private float cellSize => readOptions().CellSize;
        private int seed { get => readOptions().Seed; set => actions.SetSeed(value); }
        private Tutorial.TutorialRunMode tutorialMode { get => readOptions().TutorialMode; set => actions.SetTutorialMode(value); }

        public LevelToolPropertiesPanel(Func<AuthoringEditSession> currentSession, Action<string, Action<JObject>> change,
            Func<LevelToolPropertiesOptions> readOptions, LevelToolPropertiesActions actions)
        {
            this.currentSession = currentSession; Change = change;
            this.readOptions = readOptions; this.actions = actions;
            Root.style.flexShrink = 0;
        }

        public void Clear() { revision++; Root.Clear(); }
        public void Dispose() { if (disposed) return; disposed = true; Clear(); }

        private Func<bool> CaptureValidity()
        {
            int generation = revision;
            AuthoringEditSession session = Session;
            string levelId = session.SelectedLevelId;
            // 표시 당시의 세션과 세대가 일치하는 콜백만 허용한다.
            return () => !disposed && generation == revision && ReferenceEquals(session, Session) && session.SelectedLevelId == levelId;
        }

        private void Bind<T>(BaseField<T> field, EventCallback<ChangeEvent<T>> changed)
        {
            Func<bool> active = CaptureValidity();
            field.RegisterValueChangedCallback(e => { if (active()) changed(e); });
        }

        private Button Button(VisualElement parent, string name, string text, Action action)
        {
            Func<bool> active = CaptureValidity();
            return LevelToolPanelFields.Button(parent, name, text, () => { if (active()) action(); });
        }

        public void Refresh(JObject level, IReadOnlyDictionary<string, JObject> definitions, string[] selectedIds, string selectionReason)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LevelToolPropertiesPanel));
            Clear();
            VisualElement container = Root;
            var settings = new VisualElement { name = "level-settings" }; container.Add(settings); inspector = settings;
            TextField name = new TextField("이름") { name = "level-name", value = (string)level["displayName"], isDelayed = true };
            Bind(name, e => Change("이름 변경", data => LevelDocumentEditing.SetName(data, e.newValue))); inspector.Add(name);
            IntegerField number = new IntegerField("레벨 번호") { name = "level-number", value = (int)level["levelNumber"], isDelayed = true };
            Bind(number, e => Change("번호 변경", data => LevelDocumentEditing.SetNumber(data, e.newValue))); inspector.Add(number);
            IntegerField moves = new IntegerField("이동 횟수") { name = "level-moves", value = (int)level["moveCount"], isDelayed = true };
            Bind(moves, e => Change("이동 횟수", data => LevelDocumentEditing.SetMoveCount(data, e.newValue))); inspector.Add(moves);
            for (int i = 1; i <= 5; i++)
            {
                string value = "Type" + i;
                Toggle toggle = new Toggle("사용 색 " + i) { value = level["colors"].Values<string>().Contains(value) };
                Bind(toggle, e => Change("사용 색", data => LevelDocumentEditing.SetColorEnabled(data, value, e.newValue))); inspector.Add(toggle);
            }
            var selection = new VisualElement { name = "selection-properties" }; container.Add(selection); inspector = selection;
            DropdownField brushColor = new DropdownField("배치 색", Enumerable.Range(1, 5).Select(i => "Type" + i).ToList(), int.Parse(color.Substring(4)) - 1);
            Bind(brushColor, e => color = e.newValue); inspector.Add(brushColor);
            DropdownField orientation = new DropdownField("로켓 방향", new System.Collections.Generic.List<string> { "Horizontal", "Vertical" }, direction == "Horizontal" ? 0 : 1);
            Bind(orientation, e => direction = e.newValue); inspector.Add(orientation);
            Slider zoom = new Slider("보드 확대", 32, 144) { name = "board-zoom", value = cellSize };
            Bind(zoom, e => actions.Zoom(e.newValue)); inspector.Add(zoom);
            Button(inspector, "fit-board", "보드를 화면에 맞추기", actions.Fit);
            inspector.Add(new Label("선택 칸 " + Session.SelectedCells.Length + "개 · Shift+클릭으로 영역 선택"));
            Button(inspector, "place-area", "선택 영역에 현재 도구 배치", () => Change("영역 배치", data =>
            {
                if (brush == null) throw new InvalidOperationException("먼저 왼쪽에서 배치할 요소를 선택하세요.");
                LevelDocumentEditing.PlaceSelection(data, definitions, brush, Session.SelectedCells, false, color, direction);
            }));
            Button(inspector, "erase", "선택 층 지우기", () => Change("선택 삭제", data => LevelDocumentEditing.EraseSelection(data, definitions, layer, Session.SelectedCells)));
            Button(inspector, "sources", "선택 칸에 생성구 추가", () => Change("생성구 추가", data => SupplyDocumentEditing.PlaceSources(data, Session.SelectedCells, false)));
            Button(inspector, "erase-sources", "선택 칸 생성구 삭제", () => Change("생성구 삭제", data => SupplyDocumentEditing.PlaceSources(data, Session.SelectedCells, true)));
            if (selectionReason != null) inspector.Add(new Label(selectionReason));
            if (selectedIds.Length == 1)
            {
                JObject item = ((JArray)level["elements"]).OfType<JObject>().FirstOrDefault(value => (string)value["instanceId"] == selectedIds[0]);
                if (item != null)
                {
                    string id = (string)item["instanceId"];
                    inspector.Add(new Label((string)item["definitionId"]));
                    IntegerField durability = new IntegerField("남은 내구도") { value = (int)item["durability"], isDelayed = true };
                    durability.name = "selected-durability";
                    Bind(durability, e => Change("내구도 변경", data => LevelDocumentEditing.SetElementDurability(data, id, e.newValue))); inspector.Add(durability);
                    Button(inspector, "apply-color-direction", "선택 요소에 배치 색·방향 적용", () => Change("요소 색·방향", data =>
                        LevelDocumentEditing.SetElementAppearance(data, id, color, direction)));
                    if ((string)item["layer"] == "Obstacle")
                        Button(inspector, "move-body", "이 장애물 이동 → 목적지 클릭", () => actions.BeginMove(id));
                }
            }
            inspector = settings;
            DrawMissions(level);
            IntegerField seedInput = new IntegerField("시험 시드") { value = seed, isDelayed = true };
            Bind(seedInput, e => seed = e.newValue); inspector.Add(seedInput);
            DropdownField tutorial = new DropdownField("튜토리얼 시험", Enum.GetNames(typeof(Tutorial.TutorialRunMode)).ToList(), (int)tutorialMode);
            Bind(tutorial, e => tutorialMode = (Tutorial.TutorialRunMode)Enum.Parse(typeof(Tutorial.TutorialRunMode), e.newValue)); inspector.Add(tutorial);
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
                Bind(target, e => Change("미션 대상", data => MissionDocumentEditing.Set(data, index, e.newValue, missionColor, Math.Max(1, count)))); inspector.Add(target);
                DropdownField colors = new DropdownField("색", Enumerable.Range(1, 5).Select(n => "Type" + n).ToList(), Math.Max(0, int.Parse(missionColor.Substring(4)) - 1));
                Bind(colors, e => Change("미션 색", data => MissionDocumentEditing.Set(data, index, kind, e.newValue, count))); inspector.Add(colors);
                IntegerField amount = new IntegerField("목표 수량") { value = count, isDelayed = true };
                Bind(amount, e => Change("미션 수량", data => MissionDocumentEditing.Set(data, index, kind, missionColor, e.newValue))); inspector.Add(amount);
                Button(inspector, "remove-mission-" + i, "미션 삭제", () => Change("미션 삭제", data => MissionDocumentEditing.Remove(data, index)));
            }
            Button(inspector, "add-mission", "미션 추가", () => Change("미션 추가", data =>
                MissionDocumentEditing.Set(data, ((JArray)data["missions"]).Count, "Color", color, 10)));
        }
    }
}
#endif
