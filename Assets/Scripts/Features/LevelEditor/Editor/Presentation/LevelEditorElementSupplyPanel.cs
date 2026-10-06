using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Elements;
using Elements.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        private void BuildElementSourceList()
        {
            Foldout list = new Foldout { text = $"생성구 · {level.ElementSupply?.sources?.Count ?? 0}개", value = true, name = "used-sources" };
            tools.Add(list);
            list.Add(SupplyButton("상단 생성구 추가", "add-top-sources", () => LevelSupplyEditing.AddTopSources(level)));
            if (level.ElementSupply?.sources == null) return;
            foreach (ElementSupplySourceDefinition source in level.ElementSupply.sources)
            {
                BoardCoordinate cell = source.coordinate;
                long total = source.items?.Sum(item => (long)item.count) ?? 0;
                string mode = Enum.IsDefined(typeof(SupplyMode), source.mode) ? SupplyModeNames[(int)source.mode] : "방식 오류";
                LevelDefinition owner = level; string snapshot = JsonUtility.ToJson(level);
                list.Add(new Button(() =>
                {
                    if (!SupplyViewCurrent(owner, snapshot)) return;
                    board.CancelStroke(); board.Brush = LevelBrush.SourceSelect;
                    if (cell.Row < 0 || cell.Row >= BoardDefinition.DefaultRows || cell.Column < 0 || cell.Column >= BoardDefinition.DefaultColumns)
                    { operation.text = "보드 밖 생성구입니다. 원본 Inspector에서 수정하세요."; return; }
                    SelectSource(cell, false); boardScroll.ScrollTo(board.CellAt(cell));
                }) { text = $"{cell.Row + 1},{cell.Column + 1} · {mode}" + (source.mode == SupplyMode.Fixed ? $" · {total}개" : ""),
                    name = $"source-list-{cell.Row}-{cell.Column}", tooltip = cell + " " + mode });
            }
        }

        private bool BuildElementSupplyProperties()
        {
            if (board.Brush != LevelBrush.SourceSelect && board.Brush != LevelBrush.Source && board.Brush != LevelBrush.SourceErase) return false;
            if (!LevelSupplyEditing.CanEdit(level))
            { selectedProperties.Add(new Label("현재 형식으로 전환한 뒤 생성구를 편집하세요.")); return true; }
            if (board.Brush != LevelBrush.SourceSelect)
            {
                selectedSources.Clear();
                if (selected.HasValue && LevelSupplyRules.FindSource(level, selected.Value) >= 0) selectedSources.Add(selected.Value);
            }
            selectedSources.RemoveWhere(cell => LevelSupplyRules.FindSource(level, cell) < 0);
            board.SourceSelection = selectedSources;
            int[] indices = selectedSources.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).Select(cell => LevelSupplyRules.FindSource(level, cell)).ToArray();
            selectedProperties.Add(new Label($"생성구 {indices.Length}개 선택"));
            selectedProperties.Add(new Label("생성구 선택 도구 · Ctrl/Cmd+클릭으로 다중 선택"));
            if (indices.Length == 0) return true;
            LevelDefinition owner = level; string snapshot = JsonUtility.ToJson(level);
            AddCommon("공급 방식", "source-mode", SupplyModeNames, indices.Select(index => (int)level.ElementSupply.sources[index].mode), "mode");
            if (indices.All(index => level.ElementSupply.sources[index].mode == SupplyMode.Random ||
                level.ElementSupply.sources[index].mode == SupplyMode.Fixed && level.ElementSupply.sources[index].exhaustion == SupplyExhaustion.Random))
            {
                List<ElementDefinition> definitions = level.CreateElementCatalog().Definitions.Where(value => value.Supply?.Behavior == ElementSupplyBehavior.RandomNormal).OrderBy(value => value.Id.Value).ToList();
                List<string> names = definitions.Select(value => value.DisplayName + " [" + value.Id.Value + "]").ToList();
                string[] chosen = indices.Select(index => level.ElementSupply.sources[index].randomDefinitionId).Distinct().ToArray();
                int current = chosen.Length == 1 ? definitions.FindIndex(value => value.Id.Value == chosen[0]) : -1;
                if (current < 0) names.Insert(0, "— 혼합/정의 오류 —");
                if (names.Count > 0)
                {
                    PopupField<string> random = new PopupField<string>("무작위 공급 정의", names, Math.Max(0, current)) { name = "source-random-definition" };
                    random.RegisterValueChangedCallback(evt =>
                    {
                        int index = names.IndexOf(evt.newValue) - (current < 0 ? 1 : 0);
                        if (index >= 0 && SupplyViewCurrent(owner, snapshot)) SupplyAction(() => ElementSupplyEditing.SetRandomDefinition(level, indices, definitions[index].Id));
                    }); selectedProperties.Add(random);
                }
            }
            bool allFixed = indices.All(index => level.ElementSupply.sources[index].mode == SupplyMode.Fixed);
            if (allFixed)
            {
                AddCommon("소진 후", "source-exhaustion", ExhaustionNames, indices.Select(index => (int)level.ElementSupply.sources[index].exhaustion), "exhaustion");
                if (indices.Length == 1) BuildElementSupplyItems(indices[0]);
                else selectedProperties.Add(new HelpBox("순서 목록은 한 생성구에서 편집/복사하고 선택 대상에 붙여넣습니다.", HelpBoxMessageType.Info));
            }
            else selectedProperties.Add(new Label("유지 목표와 추가 한도는 레벨 설정에서 공유합니다. 순서 목록은 고정 공급 방식에서 편집합니다."));
            BoardCoordinate[] targets = selectedSources.ToArray();
            selectedProperties.Add(SupplyButton("선택 생성구 삭제", "delete-sources", () => LevelSupplyEditing.PlaceSources(level, targets, true)));
            return true;

            void AddCommon(string label, string name, List<string> choices, IEnumerable<int> values, string field)
            {
                int[] distinct = values.Distinct().ToArray();
                bool mixed = distinct.Length != 1 || distinct[0] < 0 || distinct[0] >= choices.Count;
                List<string> display = new List<string>(choices);
                if (mixed) display.Insert(0, "— 혼합/오류 —");
                PopupField<string> input = new PopupField<string>(label, display, mixed ? 0 : distinct[0]) { name = name };
                input.RegisterValueChangedCallback(evt =>
                {
                    int value = display.IndexOf(evt.newValue) - (mixed ? 1 : 0);
                    if (value >= 0 && SupplyViewCurrent(owner, snapshot)) SupplyAction(() => LevelSupplyEditing.SetSourceProperty(level, indices, field, value));
                });
                selectedProperties.Add(input);
            }
        }

        private void BuildElementSupplyItems(int sourceIndex)
        {
            ElementSupplySourceDefinition source = level.ElementSupply.sources[sourceIndex];
            if (source.items == null) { selectedProperties.Add(new Label("목록 누락: 원본 Inspector를 확인하세요.")); return; }
            LevelDefinition owner = level; string snapshot = JsonUtility.ToJson(level);
            List<ElementSupplyItemDefinition> original = source.items.ToList();
            // 저장 목록은 0번부터 공급하지만 화면은 물건을 쌓은 것처럼 아래쪽 1번부터 보여준다.
            // 화면 인덱스와 저장 인덱스가 반대이므로 표시용 목록을 별도로 뒤집어 사용한다.
            List<ElementSupplyItemDefinition> display = original.AsEnumerable().Reverse().ToList();
            selectedProperties.Add(new Label($"총 {original.Sum(item => (long)item.count)}개 · 아래쪽 1번부터 공급"));
            VisualElement actions = new VisualElement(); actions.style.flexDirection = FlexDirection.Row;
            actions.Add(SupplyButton("비우기", "clear-supply", () => ElementSupplyEditing.SetItems(level, sourceIndex, Array.Empty<ElementSupplyItemDefinition>())));
            selectedProperties.Add(actions);
            ListView list = new ListView(display, 28, () => new Label(), (element, index) =>
            {
                element.userData = index;
                ElementSupplyItemDefinition item = display[index];
                ((Label)element).text = $"{display.Count - index}. {ElementSupplyName(item.definitionId)} ×{item.count}";
                element.tooltip = ((Label)element).text;
            }) { name = "supply-items", reorderable = true, reorderMode = ListViewReorderMode.Animated, selectionType = SelectionType.Single };
            // 네이티브 메뉴는 상위 속성 영역을 대상으로 열리므로 우클릭 시점에 행을 선택한다.
            list.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 1) return;
                Label row = list.Query<Label>().ToList().FirstOrDefault(label => label.userData is int &&
                    evt.position.y >= label.worldBound.yMin && evt.position.y < label.worldBound.yMax);
                if (row?.userData is int index) list.SetSelection(index);
            }, TrickleDown.TrickleDown);
            bool pointerDown = false, cancelled = false;
            Action cancelDrag = () =>
            {
                // 포커스 이탈 시 Unity가 먼저 PointerUp을 보내도 예약된 재정렬을 취소한다.
                cancelled = true;
                if (!pointerDown) return;
                pointerDown = false;
                // 취소 이벤트가 끝난 후 원본 목록으로 복원한다. 미완료 드래그는 저장하지 않는다.
                editorRoot.schedule.Execute(() => { if (this != null && list.panel != null) Refresh(); });
            };
            cancelSupplyDrag = cancelDrag;
            // 속성 탭으로 재배치할 때도 목록이 분리·연결되므로 취소 연결을 복구한다.
            list.RegisterCallback<AttachToPanelEvent>(evt => { if (evt.target == list) cancelSupplyDrag = cancelDrag; });
            list.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0) { pointerDown = true; cancelled = false; } }, TrickleDown.TrickleDown);
            list.RegisterCallback<PointerUpEvent>(_ => pointerDown = false);
            list.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape) return;
                cancelDrag(); evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            list.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                if (evt.target != list) return;
                cancelled = true;
                if (cancelSupplyDrag == cancelDrag) cancelSupplyDrag = null;
            });
            list.style.height = Math.Max(32, Math.Min(7, display.Count) * 28 + 4);
            selectedProperties.Add(list);
            selectedProperties.Add(new Label("↓ 생성구 · 먼저 공급"));
            list.itemIndexChanged += (_, _) =>
            {
                // 드래그 종료 이벤트 중 패널을 분리하면 Unity 내부 정리에서 예외가 발생한다.
                list.schedule.Execute(() =>
                {
                    if (!cancelled && SupplyViewCurrent(owner, snapshot)) SupplyAction(() => ElementSupplyEditing.SetItems(level, sourceIndex, display.AsEnumerable().Reverse().ToList()));
                });
            };
            VisualElement details = new VisualElement { name = "supply-item-details" };
            list.selectionChanged += _ =>
            {
                selectedSupplyItem = list.selectedIndex < 0 ? -1 : display.Count - 1 - list.selectedIndex;
                details.Clear();
                if (selectedSupplyItem >= 0 && selectedSupplyItem < original.Count) BuildElementSupplyItemDetails(details, sourceIndex, selectedSupplyItem);
            };
            VisualElement itemActions = new VisualElement(); itemActions.style.flexDirection = FlexDirection.Row;
            itemActions.Add(SupplyButton("+ 항목", "add-supply-item", () =>
            {
                List<ElementSupplyItemDefinition> updated = original.ToList(); updated.Add(NewElementSupplyItem(SupplyKind.RandomNormal));
                selectedSupplyItem = updated.Count - 1; return ElementSupplyEditing.SetItems(level, sourceIndex, updated);
            }));
            itemActions.Add(SupplyButton("복제", "duplicate-supply-item", () => ChangeItem(0)));
            itemActions.Add(SupplyButton("삭제", "delete-supply-item", () => ChangeItem(1)));
            selectedProperties.Add(itemActions);
            selectedProperties.Add(SupplyButton("+ 파워블록", "add-power-supply-item", () =>
            {
                List<ElementSupplyItemDefinition> updated = original.ToList(); updated.Add(NewElementSupplyItem(SupplyKind.RandomPower));
                selectedSupplyItem = updated.Count - 1; return ElementSupplyEditing.SetItems(level, sourceIndex, updated);
            }));
            VisualElement order = new VisualElement(); order.style.flexDirection = FlexDirection.Row;
            order.Add(SupplyButton("↑ 나중에", "supply-item-later", () => ChangeItem(2)));
            order.Add(SupplyButton("↓ 먼저", "supply-item-earlier", () => ChangeItem(3)));
            selectedProperties.Add(order);
            selectedProperties.Add(details);
            if (selectedSupplyItem >= 0 && selectedSupplyItem < original.Count) list.SetSelection(original.Count - 1 - selectedSupplyItem);

            string ChangeItem(int action)
            {
                int index = selectedSupplyItem;
                if (index < 0 || index >= original.Count) return "목록에서 항목을 선택하세요.";
                List<ElementSupplyItemDefinition> updated = original.ToList();
                if (action == 0) { updated.Insert(index + 1, updated[index]); selectedSupplyItem++; }
                else if (action == 1) { updated.RemoveAt(index); selectedSupplyItem = Math.Min(index, updated.Count - 1); }
                else
                {
                    int target = index + (action == 2 ? 1 : -1);
                    if (target < 0 || target >= updated.Count) return "더 이동할 수 없습니다.";
                    (updated[index], updated[target]) = (updated[target], updated[index]); selectedSupplyItem = target;
                }
                return ElementSupplyEditing.SetItems(level, sourceIndex, updated);
            }
        }

        private string ElementSupplyName(string id)
        {
            try { return level.CreateElementCatalog().Get(new ElementId(id)).DisplayName + " [" + id + "]"; }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is KeyNotFoundException) { return "정의 오류 [" + id + "]"; }
        }
        private ElementSupplyItemDefinition NewElementSupplyItem(SupplyKind kind) => new ElementSupplyItemDefinition
        { definitionId = LegacyElementMap.Get(kind).Value, color = level.Colors?.FirstOrDefault() ?? RabbitColor.Type1 };

        private void BuildElementSupplyItemDetails(VisualElement parent, int sourceIndex, int itemIndex)
        {
            LevelDefinition owner = level; string snapshot = JsonUtility.ToJson(level);
            ElementSupplyItemDefinition item = level.ElementSupply.sources[sourceIndex].items[itemIndex];
            List<ElementDefinition> definitions = level.CreateElementCatalog().Definitions.Where(value => value.Supply != null).OrderBy(value => value.DisplayName).ThenBy(value => value.Id.Value).ToList();
            List<string> names = definitions.Select(value => value.DisplayName + " [" + value.Id.Value + "]").ToList();
            int selectedIndex = definitions.FindIndex(value => value.Id.Value == item.definitionId);
            if (selectedIndex < 0) { parent.Add(new HelpBox("공급 정의를 찾을 수 없습니다: " + item.definitionId, HelpBoxMessageType.Error)); return; }
            ElementSupplyProfile profile = definitions[selectedIndex].Supply;
            PopupField<string> definition = new PopupField<string>("공급 정의", names, selectedIndex) { name = "supply-item-kind" };
            definition.RegisterValueChangedCallback(evt => Apply(copy =>
            {
                ElementDefinition chosen = definitions[names.IndexOf(evt.newValue)]; copy.definitionId = chosen.Id.Value;
                copy.color = level.Colors?.FirstOrDefault() ?? RabbitColor.Type1;
                copy.durability = chosen.Supply.Behavior == ElementSupplyBehavior.Obstacle ? Math.Min(Math.Max(1, copy.durability), ElementSupplyEditing.MaximumDurability(level, copy.definitionId)) : 1;
            })); parent.Add(definition);
            IntegerField count = new IntegerField("수량") { value = item.count, isDelayed = true, name = "supply-item-count" };
            count.RegisterValueChangedCallback(evt => Apply(copy => copy.count = evt.newValue)); parent.Add(count);
            if (profile.Behavior == ElementSupplyBehavior.Obstacle)
            {
                int maximum;
                try { maximum = ElementSupplyEditing.MaximumDurability(level, item.definitionId); }
                catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is KeyNotFoundException)
                { parent.Add(new HelpBox(error.Message, HelpBoxMessageType.Error)); return; }
                IntegerField durability = new IntegerField("내구도 1~" + maximum) { value = item.durability, isDelayed = true, name = "supply-item-durability" };
                durability.RegisterValueChangedCallback(evt => Apply(copy => copy.durability = evt.newValue)); parent.Add(durability);
            }
            if (profile.Content == Simulation.RuntimeContent.Rocket)
            {
                PopupField<string> direction = new PopupField<string>("방향", new List<string> { "가로", "세로" }, (int)item.direction == 1 ? 1 : 0) { name = "supply-item-direction" };
                direction.RegisterValueChangedCallback(evt => Apply(copy => copy.direction = (RocketDirection)direction.index)); parent.Add(direction);
            }
            if (profile.Behavior == ElementSupplyBehavior.FixedNormal)
            {
                List<RabbitColor> colors = (level.Colors ?? Array.Empty<RabbitColor>()).Where(color => Enum.IsDefined(typeof(RabbitColor), color)).Distinct().ToList();
                if (colors.Count > 0)
                {
                    PopupField<string> color = new PopupField<string>("색", colors.Select(value => "달토끼 " + ((int)value + 1)).ToList(), Math.Max(0, colors.IndexOf(item.color))) { name = "supply-item-color" };
                    color.RegisterValueChangedCallback(evt => Apply(copy => copy.color = colors[color.index])); parent.Add(color);
                }
            }
            void Apply(Action<ElementSupplyItemDefinition> change)
            {
                if (!SupplyViewCurrent(owner, snapshot)) return;
                ElementSupplyItemDefinition copy = JsonUtility.FromJson<ElementSupplyItemDefinition>(JsonUtility.ToJson(item));
                try { change(copy); }
                catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is KeyNotFoundException) { operation.text = error.Message; return; }
                List<ElementSupplyItemDefinition> updated = level.ElementSupply.sources[sourceIndex].items.ToList(); updated[itemIndex] = copy;
                SupplyAction(() => ElementSupplyEditing.SetItems(level, sourceIndex, updated));
            }
        }
    }
}
