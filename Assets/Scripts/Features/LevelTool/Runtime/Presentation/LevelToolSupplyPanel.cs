#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Editing;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace LevelTool.Presentation
{
    public sealed class LevelToolSupplyState
    {
        public string DefinitionId = "supply.normal.fixed", Color = "Type1", Direction = "Horizontal";
        public int Count = 1, Durability = 1;
    }

    public sealed class LevelToolSupplyPanel : IDisposable
    {
        private readonly Func<AuthoringEditSession> currentSession;
        private readonly Action<string, Action<JObject>> change;
        private readonly LevelToolSupplyState state;
        private int revision;
        private bool disposed;
        public VisualElement Root { get; } = new VisualElement();

        public LevelToolSupplyPanel(Func<AuthoringEditSession> currentSession, Action<string, Action<JObject>> change, LevelToolSupplyState state)
        {
            this.currentSession = currentSession;
            this.change = change;
            this.state = state;
            Root.style.flexShrink = 0;
        }

        public void Clear()
        {
            revision++;
            Root.Clear();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Clear();
        }

        public void Refresh(JObject level, IReadOnlyDictionary<string, JObject> definitions, bool tutorial = false)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LevelToolSupplyPanel));
            Clear();
            AuthoringEditSession session = currentSession();
            string levelId = session.SelectedLevelId;
            int generation = revision;
            // 이전 화면의 지연 입력이 레벨 전환 후 다른 문서를 수정하지 않도록 한다.
            bool Active() => !disposed && generation == revision && ReferenceEquals(session, currentSession()) && session.SelectedLevelId == levelId;
            Button Button(VisualElement parent, string name, string text, Action action) =>
                LevelToolPanelFields.Button(parent, name, text, () => { if (Active()) action(); });
            void Choice(VisualElement parent, string name, string label, string[] values, string[] labels, string current, Action<string> changed) =>
                LevelToolPanelFields.Choice(parent, name, label, values, labels, current, value => { if (Active()) changed(value); });
            void Number(VisualElement parent, string name, string label, int current, Action<int> changed) =>
                LevelToolPanelFields.Number(parent, name, label, current, value => { if (Active()) changed(value); });
            void SupplyChange(string label, Action<JObject> edit) => change(label, data =>
            {
                if (tutorial) SupplyDocumentEditing.EditTutorial(data, definitions, edit);
                else edit(data);
            });
            if (tutorial)
            {
                level = SupplyDocumentEditing.TutorialPreview(level);
                Root.Add(new Label("튜토리얼 진행 중 생성할 순서입니다. 고정 목록을 다 쓰면 생성이 멈춥니다. 일반 공급 설정은 바뀌지 않습니다."));
            }
            JArray sources = (JArray)level["elementSupply"]["sources"];
            int[] selected = sources.Select((source, index) => new { source, index })
                .Where(value => session.SelectedCells.Contains((int)value.source["coordinate"]["row"] * 9 + (int)value.source["coordinate"]["column"]))
                .Select(value => value.index).ToArray();
            Root.Add(new Label("보드에서 생성구 칸을 선택하세요. Shift+클릭으로 여러 생성구를 선택할 수 있습니다."));
            Button(Root, "sources", "선택 칸에 생성구 추가", () => SupplyChange("생성구 추가", data =>
            {
                if (tutorial) SupplyDocumentEditing.PlaceTutorialSources(data, session.SelectedCells);
                else SupplyDocumentEditing.PlaceSources(data, session.SelectedCells, false);
            }));
            Button(Root, "erase-sources", "선택 칸 생성구 삭제", () => SupplyChange("생성구 삭제", data => SupplyDocumentEditing.PlaceSources(data, session.SelectedCells, true)));
            string[] supplies = definitions.Where(pair => (bool?)pair.Value["supply"]?["enabled"] == true && (!tutorial || !new[] { "RandomNormal", "RandomPower" }.Contains((string)pair.Value["supply"]?["behavior"]))).Select(pair => pair.Key).ToArray();
            string[] Names(IEnumerable<string> ids) => ids.Select(id => (string)definitions[id]["displayName"]).ToArray();
            if (selected.Length > 0)
            {
                JObject first = (JObject)sources[selected[0]];
                Root.Add(new Label("선택 생성구 " + selected.Length + "개 · 방식 변경과 추가는 모두에 적용"));
                if (!tutorial) Choice(Root, "source-mode", "공급 방식", new[] { "Random", "Fixed", "MaintainScrap", "MaintainRecovery" },
                    new[] { "무작위", "고정 순서", "고철 수량 유지", "회수 부품 수량 유지" }, (string)first["mode"],
                    value => SupplyChange("공급 방식", data => SupplyDocumentEditing.SetSourceProperty(data, definitions, selected, "mode", value)));
                if (!tutorial && selected.All(index => (string)sources[index]["mode"] == "Fixed"))
                    Choice(Root, "source-exhaustion", "목록을 다 쓴 뒤", new[] { "Stop", "Random" }, new[] { "생성 중단", "무작위로 계속" },
                        (string)first["exhaustion"], value => SupplyChange("소진 정책", data => SupplyDocumentEditing.SetSourceProperty(data, definitions, selected, "exhaustion", value)));
                string[] random = supplies.Where(id => (string)definitions[id]["supply"]["behavior"] == "RandomNormal").ToArray();
                if (!tutorial) Choice(Root, "random-supply", "무작위 공급 종류", random, Names(random), (string)first["randomDefinitionId"],
                    value => SupplyChange("무작위 공급", data => SupplyDocumentEditing.SetRandomDefinition(data, definitions, selected, value)));
                Root.Add(new Label("고정 목록에 추가할 항목"));
                if (!supplies.Contains(state.DefinitionId)) state.DefinitionId = supplies.FirstOrDefault();
                Choice(Root, "supply-add-definition", "종류", supplies, Names(supplies), state.DefinitionId, value => state.DefinitionId = value);
                Number(Root, "supply-add-count", "개수", state.Count, value => state.Count = value);
                Choice(Root, "supply-add-color", "색", Enumerable.Range(1, 5).Select(i => "Type" + i).ToArray(),
                    new[] { "분홍", "노랑", "파랑", "초록", "다섯째 색" }, state.Color, value => state.Color = value);
                Choice(Root, "supply-add-direction", "로켓 방향", new[] { "Horizontal", "Vertical" }, new[] { "가로", "세로" }, state.Direction, value => state.Direction = value);
                Number(Root, "supply-add-durability", "장애물 내구도", state.Durability, value => state.Durability = value);
                Button(Root, "append-supply", "선택한 고정 생성구에 추가", () => SupplyChange("고정 공급 추가", data =>
                    SupplyDocumentEditing.AppendItem(data, definitions, selected, state.DefinitionId, state.Count, state.Color, state.Direction, state.Durability)));
                Button(Root, "clear-supply", "선택한 생성구의 목록 비우기", () => SupplyChange("고정 공급 비우기", data => SupplyDocumentEditing.SetItems(data, definitions, selected, new JArray(), false)));
                foreach (int sourceIndex in selected)
                {
                    JObject source = (JObject)sources[sourceIndex]; JArray items = (JArray)source["items"];
                    Foldout list = new Foldout { text = "행 " + ((int)source["coordinate"]["row"] + 1) + " · 열 " + ((int)source["coordinate"]["column"] + 1) + " 고정 목록", value = true };
                    Root.Add(list);
                    for (int i = 0; i < items.Count; i++)
                    {
                        int itemIndex = i; JObject item = (JObject)items[i];
                        void Modify(string field, JToken value) => SupplyChange("공급 항목 수정", data =>
                            SupplyDocumentEditing.SetItemProperty(data, definitions, sourceIndex, itemIndex, field, value));
                        string prefix = "source-" + sourceIndex + "-item-" + i;
                        Choice(list, prefix + "-kind", (i + 1) + "번째 종류", supplies, Names(supplies), (string)item["definitionId"], value => Modify("definitionId", value));
                        Number(list, prefix + "-count", "개수", (int)item["count"], value => Modify("count", value));
                        Choice(list, prefix + "-color", "색", Enumerable.Range(1, 5).Select(n => "Type" + n).ToArray(),
                            new[] { "분홍", "노랑", "파랑", "초록", "다섯째 색" }, (string)item["color"], value => Modify("color", value));
                        Choice(list, prefix + "-direction", "로켓 방향", new[] { "Horizontal", "Vertical" }, new[] { "가로", "세로" }, (string)item["direction"], value => Modify("direction", value));
                        Number(list, prefix + "-durability", "장애물 내구도", (int)item["durability"], value => Modify("durability", value));
                        void Reorder(bool remove) => SupplyChange(remove ? "공급 항목 삭제" : "공급 순서 변경", data =>
                            SupplyDocumentEditing.ReorderItem(data, definitions, sourceIndex, itemIndex, remove));
                        Button(list, prefix + "-up", "위로", () => Reorder(false)).SetEnabled(i > 0);
                        Button(list, prefix + "-remove", "이 항목 삭제", () => Reorder(true));
                    }
                }
            }
            if (tutorial) return;
            Root.Add(new Label("수량 유지 설정 · 유지 생성구가 있을 때 사용합니다."));
            foreach (var field in new[] { ("scrapTarget", "보드 고철 목표 수"), ("scrapLimit", "고철 생성 한도"), ("scrapDurability", "고철 내구도"), ("recoveryTarget", "보드 회수 부품 목표 수") })
                Number(Root, field.Item1, field.Item2, (int)level["elementSupply"][field.Item1],
                    value => SupplyChange("유지 수량", data => SupplyDocumentEditing.SetMaintenanceProperty(data, definitions, field.Item1, value)));
            foreach (string field in new[] { "scrapDefinitionId", "recoveryDefinitionId" })
                Choice(Root, field, field == "scrapDefinitionId" ? "유지 고철 종류" : "유지 회수 부품 종류", supplies, Names(supplies),
                    (string)level["elementSupply"][field], value => SupplyChange("유지 공급 종류", data => SupplyDocumentEditing.SetMaintenanceDefinition(data, definitions, field, value)));
        }

    }
}
#endif
