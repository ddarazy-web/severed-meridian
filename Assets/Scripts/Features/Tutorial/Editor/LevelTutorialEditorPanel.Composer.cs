using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Levels;
using System.Text.RegularExpressions;

namespace Tutorial.Editor
{
    public sealed partial class LevelTutorialEditorPanel
    {
        private VisualElement samplePreview;

        private void FocusIssue(LevelValidationIssue issue)
        {
            Match match = Regex.Match(issue.PropertyPath ?? "", @"steps\.Array\.data\[(\d+)\]");
            if (match.Success) selected = int.Parse(match.Groups[1].Value);
            Rebuild();
            if (issue.Coordinate.HasValue) board.ShowTutorialTargets(new[] { issue.Coordinate.Value }, null, null);
            PropertyField field = fields.Query<PropertyField>().ToList().FirstOrDefault(value => value.bindingPath == issue.PropertyPath);
            if (field == null) return;
            for (VisualElement ancestor = field.parent; ancestor != null; ancestor = ancestor.parent)
                if (ancestor is Foldout foldout) foldout.value = true;
            field.schedule.Execute(() =>
            {
                field.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(field);
                (field.Q<IntegerField>() as VisualElement ?? field.Q<TextField>() as VisualElement ?? field).Focus();
            });
        }

        private void ArrangeComposer()
        {
            if (stageHost == null || testHost == null) return;
            stageHost.Clear(); testHost.Clear();
            stageHost.Add(new Label("튜토리얼 단계"));
            for (int i = 0; i < owner.Tutorial.steps.Count; i++)
            {
                int index = i; TutorialStepDefinition step = owner.Tutorial.steps[i];
                Button button = new Button(() => { selected = index; Rebuild(); })
                { text = $"{i + 1}. {step.instructions}", name = "tutorial-select-step-" + i };
                button.style.whiteSpace = WhiteSpace.Normal;
                button.style.borderLeftWidth = i == selected ? 4 : 0;
                button.style.borderLeftColor = Color.cyan; stageHost.Add(button);
            }
            foreach (string name in new[] { "tutorial-sample-pick", "tutorial-sample-preview", "tutorial-add", "tutorial-duplicate" })
            {
                VisualElement control = fields.Q(name); if (control != null) stageHost.Add(control);
            }
            Button up = fields.Q<Button>("tutorial-up"); if (up != null) stageHost.Add(up.parent);
            VisualElement selector = fields.Q("tutorial-step"); if (selector != null) selector.style.display = DisplayStyle.None;
            testHost.Add(new Label("테스트 · 실제 게임 실행은 상단 ‘게임 플레이’를 사용하세요."));
            foreach (string name in new[] { "tutorial-replay", "tutorial-replay-status", "tutorial-pick-status", "tutorial-sample-boards" })
            {
                VisualElement control = fields.Q(name); if (control != null) testHost.Add(control);
            }
            // 기본 작업에 필요 없는 원시 설정은 펼침 영역으로 모아 둔다.
            Foldout advanced = new Foldout { text = "고급 설정 · 기존 단계 및 직접 강조 편집", value = false };
            foreach (string name in new[] { "kind", "highlights", "hasFirst", "first", "hasSecond", "second", "item", "actionDefinitionId", "results" })
            {
                VisualElement field = fields.Q("tutorial-field-" + name); if (field != null) advanced.Add(field);
            }
            fields.Add(advanced);
        }

        private void AddComposerControls()
        {
            fields.Add(new Button(BeginSamplePicking) { text = "셀 선택 → 새 단계 만들기", name = "tutorial-sample-pick" });
            samplePreview = new VisualElement { name = "tutorial-sample-preview" }; fields.Add(samplePreview);
            var samples = new VisualElement { name = "tutorial-sample-boards" };
            int sampleIndex = Math.Max(0, TutorialSampleBoards.All.ToList().FindIndex(value => owner.name == "시험 · " + value.Title));
            var sampleChoice = new PopupField<string>("시험 보드", TutorialSampleBoards.All.Select(value => value.Title).ToList(), sampleIndex) { name = "tutorial-sample-board-choice" };
            var expected = new Label(TutorialSampleBoards.All[sampleIndex].ExpectedResult) { name = "tutorial-sample-board-expected" };
            expected.style.whiteSpace = WhiteSpace.Normal;
            sampleChoice.RegisterValueChangedCallback(_ => expected.text = TutorialSampleBoards.All[sampleChoice.index].ExpectedResult);
            samples.Add(sampleChoice); samples.Add(expected);
            samples.Add(new Button(() => Levels.Editor.LevelEditorWindow.OpenTutorialSample(TutorialSampleBoards.All[sampleChoice.index]))
                { text = "시험 보드 사본을 새 창에서 열기", name = "tutorial-sample-board-open" });
            fields.Add(samples);
            if (owner.Tutorial.steps.Count == 0) return;
            SerializedProperty step = input.FindProperty("tutorial").FindPropertyRelative("steps").GetArrayElementAtIndex(selected);
            fields.Add(new Button(() => Edit(tutorial =>
            {
                SerializedProperty list = tutorial.FindPropertyRelative("steps");
                list.InsertArrayElementAtIndex(selected); selected++;
            })) { text = "현재 단계 복제", name = "tutorial-duplicate" });
            AddActionAreaControls();
            AddActionBindingControls();
            if (owner.Tutorial.steps[selected].kind == TutorialStepKind.Description) return;
            if (owner.Tutorial.steps[selected].kind == TutorialStepKind.Item)
            {
                var item = new PopupField<string>("체험 아이템", new List<string> { "망치", "교환", "섞기" }, (int)owner.Tutorial.steps[selected].item);
                item.RegisterValueChangedCallback(_ => Edit(value => value.FindPropertyRelative("steps").GetArrayElementAtIndex(selected).FindPropertyRelative("item").enumValueIndex = item.index)); fields.Add(item);
                fields.Add(new PropertyField(step.FindPropertyRelative("freeItemCount"), "이 단계의 무료 체험 횟수") { name = "tutorial-free-item-count" });
            }
            AddConditionSamples();
            fields.Add(new Label("완료 조건 · 게임 화면에는 표시되지 않습니다."));
            PopupField<string> combination = new PopupField<string>("조건 연결", new List<string> { "모두 충족", "하나 이상 충족" }, (int)owner.Tutorial.steps[selected].combination) { name = "tutorial-combination" };
            combination.RegisterValueChangedCallback(evt => Edit(value => value.FindPropertyRelative("steps").GetArrayElementAtIndex(selected).FindPropertyRelative("combination").enumValueIndex = combination.index));
            fields.Add(combination);
            VisualElement add = new VisualElement(); add.style.flexDirection = FlexDirection.Row;
            add.style.flexWrap = Wrap.Wrap;
            foreach (TutorialConditionKind kind in Enum.GetValues(typeof(TutorialConditionKind)))
            {
                if (kind == TutorialConditionKind.ItemUsed && owner.Tutorial.steps[selected].kind != TutorialStepKind.Item ||
                    kind == TutorialConditionKind.SuccessfulSwap && owner.Tutorial.steps[selected].kind == TutorialStepKind.Item) continue;
                TutorialConditionKind selectedKind = kind;
                add.Add(new Button(() => AddCondition(selectedKind)) { text = "+ " + ConditionLabel(kind), name = "tutorial-condition-add-" + kind });
            }
            fields.Add(add);
            SerializedProperty conditions = step.FindPropertyRelative("conditions");
            for (int i = 0; i < conditions.arraySize; i++)
            {
                int index = i;
                SerializedProperty condition = conditions.GetArrayElementAtIndex(i);
                VisualElement card = new VisualElement { name = "tutorial-condition-" + i };
                card.style.marginTop = 8; card.style.paddingLeft = 6;
                TutorialConditionKind kind = (TutorialConditionKind)condition.FindPropertyRelative("kind").enumValueIndex;
                bool match = condition.FindPropertyRelative("kind").enumValueIndex == (int)TutorialConditionKind.Match;
                if (kind >= TutorialConditionKind.DurabilityDecrease) AddExtendedCondition(card, condition, index);
                else
                {
                    card.Add(new Label(match ? "매칭이 발생하면" : "교환에 성공하면"));
                    card.Add(new PropertyField(condition.FindPropertyRelative("requiredCount"), "필요 횟수"));
                }
                if (match)
                {
                    card.Add(new PropertyField(condition.FindPropertyRelative("matchSize"), "매칭 블록 수"));
                    card.Add(new PropertyField(condition.FindPropertyRelative("sizeComparison"), "크기 비교"));
                    card.Add(new PropertyField(condition.FindPropertyRelative("origin"), "직접 / 연쇄 포함"));
                    card.Add(new PropertyField(condition.FindPropertyRelative("anyColor"), "모든 색상"));
                    card.Add(new PropertyField(condition.FindPropertyRelative("color"), "지정 색상"));
                }
                card.Add(new Button(() => Edit(value => value.FindPropertyRelative("steps").GetArrayElementAtIndex(selected).FindPropertyRelative("conditions").DeleteArrayElementAtIndex(index))) { text = "조건 삭제" });
                fields.Add(card);
            }
            Toggle automatic = new Toggle("대상과 조작 셀 자동 강조") { value = owner.Tutorial.steps[selected].automaticHighlights, name = "tutorial-auto-highlight" };
            automatic.RegisterValueChangedCallback(evt => Edit(value =>
            {
                SerializedProperty current = value.FindPropertyRelative("steps").GetArrayElementAtIndex(selected);
                current.FindPropertyRelative("automaticHighlights").boolValue = evt.newValue;
                if (evt.newValue) SetAutomaticHighlights(current);
            }));
            fields.Add(automatic);
            if (owner.Tutorial.steps.Take(selected).Any(value => value.kind != TutorialStepKind.Description) ||
                !string.IsNullOrEmpty(owner.Tutorial.steps[selected].firstBinding) || !string.IsNullOrEmpty(owner.Tutorial.steps[selected].secondBinding))
                fields.Add(new HelpBox("후속 단계의 종류 대상과 생성 개체의 실제 위치는 재생 검사 또는 게임에서 확인하세요. 편집 보드는 미래 위치를 추정하지 않습니다.", HelpBoxMessageType.Info));
        }

        private IEnumerable<BoardCoordinate> PreviewTargetHighlights(TutorialStepDefinition step)
        {
            bool initialAction = owner.Tutorial.steps.Take(selected).All(value => value.kind == TutorialStepKind.Description);
            Elements.ElementCatalog catalog = owner.CreateElementCatalog();
            IReadOnlyList<ElementPlacementDefinition> placements = owner.SchemaVersion == LevelDefinition.LegacySchemaVersion ? LegacyElementLevelAdapter.Preview(owner) : owner.Elements;
            var result = new HashSet<BoardCoordinate>();
            foreach (TutorialConditionDefinition condition in step.conditions)
            {
                TutorialTargetDefinition target = condition.target;
                if (target == null || condition.kind < TutorialConditionKind.DurabilityDecrease || condition.kind > TutorialConditionKind.Combined) continue;
                if (target.kind == TutorialTargetKind.Area) { result.UnionWith(target.cells); continue; }
                if (target.kind == TutorialTargetKind.Entity) result.Add(target.coordinate);
                if (!initialAction || target.kind != TutorialTargetKind.Entity && target.kind != TutorialTargetKind.Definition) continue;
                foreach (ElementPlacementDefinition placement in placements)
                {
                    if (placement == null || string.IsNullOrWhiteSpace(placement.definitionId)) continue;
                    Elements.ElementDefinition definition = catalog.Definitions.FirstOrDefault(value => value.Id.Value == placement.definitionId);
                    if (definition == null) continue;
                    TutorialTargetLayer layer = placement.layer == PlacementLayer.Cover ? TutorialTargetLayer.Cover : placement.layer == PlacementLayer.Dust ? TutorialTargetLayer.Floor : TutorialTargetLayer.Content;
                    if (target.layer != layer) continue;
                    var footprint = LevelPlacementRules.Footprint(placement.coordinate, definition.Placement?.Size ?? definition.ChargePlacement?.Size ?? 1).ToArray();
                    if (target.kind == TutorialTargetKind.Entity ? footprint.Contains(target.coordinate) : target.definitionId == placement.definitionId)
                        result.UnionWith(footprint);
                }
            }
            return result;
        }

        private void AddActionBindingControls()
        {
            TutorialStepDefinition step = owner.Tutorial.steps[selected];
            bool supported = step.kind != TutorialStepKind.Description && !(step.kind == TutorialStepKind.Item && step.item == Simulation.BoardItem.Shuffle);
            List<string> names = owner.Tutorial.steps.Take(selected).SelectMany(value => value.conditions)
                .Where(value => value.kind == TutorialConditionKind.Generated).Select(value => value.bindGeneratedAs)
                .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList();
            foreach (string key in new[] { "first", "second" })
            {
                string current = key == "first" ? step.firstBinding : step.secondBinding;
                bool allowed = supported && !(key == "second" && step.kind == TutorialStepKind.Item && step.item == Simulation.BoardItem.Hammer);
                if (!allowed && string.IsNullOrEmpty(current)) continue;
                var bindings = allowed ? new List<string>(names) : new List<string>();
                bool missing = !string.IsNullOrEmpty(current) && !bindings.Contains(current);
                if (missing) bindings.Add(current);
                bindings.Insert(0, "");
                var labels = bindings.Select(value => value.Length == 0 ? "지정 칸 / 선택 영역 사용" : "생성: " + value).ToList();
                var choice = new PopupField<string>(key == "first" ? "첫 조작 대상" : "둘째 조작 대상", labels, Math.Max(0, bindings.IndexOf(current)))
                    { name = "tutorial-action-binding-" + key };
                choice.RegisterValueChangedCallback(_ => Edit(value => value.FindPropertyRelative("steps").GetArrayElementAtIndex(selected)
                    .FindPropertyRelative(key + "Binding").stringValue = bindings[choice.index]));
                fields.Add(choice);
                if (missing) fields.Add(new HelpBox("이 생성 연결은 현재 단계에서 사용할 수 없습니다. 이전 단계의 생성 조건을 확인하거나 지정 칸 방식으로 변경하세요.", HelpBoxMessageType.Error));
            }
        }

        private void AddActionAreaControls()
        {
            TutorialStepDefinition step = owner.Tutorial.steps[selected];
            bool supported = step.kind != TutorialStepKind.Description && !(step.kind == TutorialStepKind.Item && step.item == Simulation.BoardItem.Shuffle);
            if (!supported && step.actionArea.Count == 0) return;
            var group = new VisualElement { name = "tutorial-action-area" }; fields.Add(group);
            group.Add(new Label(step.actionArea.Count == 0 ? "동작 · 지정한 칸에서 조작" : $"동작 · 선택 영역 {step.actionArea.Count}칸 안에서 조작"));
            group.Add(new HelpBox("조작을 허용할 영역입니다. 완료 조건의 대상과 별도로 지정하며, 안내는 조건에 도움이 되는 행동을 표시합니다.", HelpBoxMessageType.Info));
            var pick = new Button(BeginActionAreaPicking) { text = "보드에서 조작 영역 선택", name = "tutorial-action-area-pick" };
            pick.SetEnabled(supported); group.Add(pick);
            if (step.actionArea.Count > 0)
                group.Add(new Button(() => Edit(value => value.FindPropertyRelative("steps").GetArrayElementAtIndex(selected).FindPropertyRelative("actionArea").ClearArray()))
                    { text = "영역 해제 · 지정 칸 사용", name = "tutorial-action-area-clear" });
        }

        private void BeginActionAreaPicking()
        {
            CancelPicking(); board.CancelStroke();
            var cells = new HashSet<BoardCoordinate>(owner.Tutorial.steps[selected].actionArea);
            picker = coordinate =>
            {
                if (!cells.Add(coordinate)) cells.Remove(coordinate);
                board.ShowTutorialTargets(cells.ToList(), null, null);
                pickStatus.text = $"조작 영역 {cells.Count}칸 선택 · 다시 누르면 제외 · 선택 완료로 저장 · Esc로 취소";
            };
            fields.Q("tutorial-action-area").Add(new Button(() => Edit(value =>
            {
                SerializedProperty area = value.FindPropertyRelative("steps").GetArrayElementAtIndex(selected).FindPropertyRelative("actionArea");
                BoardCoordinate[] ordered = cells.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToArray();
                area.arraySize = ordered.Length;
                for (int i = 0; i < ordered.Length; i++)
                {
                    area.GetArrayElementAtIndex(i).FindPropertyRelative("row").intValue = ordered[i].Row;
                    area.GetArrayElementAtIndex(i).FindPropertyRelative("column").intValue = ordered[i].Column;
                }
            })) { text = "조작 영역 선택 완료", name = "tutorial-action-area-confirm" });
            board.TutorialTargetPicked = picker; board.ShowTutorialTargets(cells.ToList(), null, null); board.Focus();
            pickStatus.text = "조작을 허용할 칸을 선택하세요. 선택 완료로 저장하며 Esc로 취소합니다.";
        }

        private void BeginSamplePicking()
        {
            CancelPicking(); samplePreview.Clear(); board.CancelStroke(); BoardCoordinate? first = null;
            picker = coordinate =>
            {
                if (!first.HasValue)
                {
                    if (ShowObstacleSamplePreview(coordinate)) return;
                    first = coordinate; pickStatus.text = "교환할 두 번째 칸을 선택하세요. Esc로 취소";
                    board.ShowTutorialTargets(Array.Empty<BoardCoordinate>(), coordinate, null); return;
                }
                if (!new BoardEdge(first.Value, coordinate).IsAdjacent)
                { pickStatus.text = "첫 칸과 인접한 칸을 선택하세요."; return; }
                BoardCoordinate start = first.Value;
                board.TutorialTargetPicked = null; picker = null;
                samplePreview.Clear();
                PopupField<string> choice = new PopupField<string>("샘플", new List<string> { "지정한 두 칸 교환", "직접 3매칭" }, 0) { name = "tutorial-sample-choice" };
                samplePreview.Add(choice);
                Label preview = new Label("두 칸 교환 허용 · 성공한 교환 1회 · 선택한 셀 자동 강조");
                preview.style.whiteSpace = WhiteSpace.Normal; samplePreview.Add(preview);
                choice.RegisterValueChangedCallback(_ => preview.text = choice.index == 0 ? "두 칸 교환 허용 · 성공한 교환 1회 · 선택한 셀 자동 강조" : "두 칸 교환 허용 · 직접 정확히 3개 매칭 1회 · 선택한 셀 자동 강조");
                samplePreview.Add(new Button(() =>
                {
                    Undo.RecordObject(owner, "튜토리얼 샘플 적용");
                    owner.Tutorial.steps.Add(TutorialSampleCatalog.CreateSwap(start, coordinate, choice.index == 1));
                    selected = owner.Tutorial.steps.Count - 1; EditorUtility.SetDirty(owner); Rebuild();
                }) { text = "새 단계로 적용", name = "tutorial-sample-apply" });
                samplePreview.Add(new Button(() => { samplePreview.Clear(); ShowTargets(); }) { text = "취소" });
                board.ShowTutorialTargets(new[] { start, coordinate }, start, coordinate);
                pickStatus.text = "샘플 내용을 확인한 뒤 적용하세요. 아직 저장되지 않았습니다.";
            };
            board.TutorialTargetPicked = picker; board.Focus(); pickStatus.text = "교환할 첫 번째 칸을 선택하세요. Esc로 취소";
        }

        private void SetAutomaticHighlights(SerializedProperty current)
        {
            if (!current.FindPropertyRelative("automaticHighlights").boolValue) return;
            SerializedProperty list = current.FindPropertyRelative("highlights"); list.ClearArray();
            foreach (string key in new[] { "first", "second" })
            {
                if (!current.FindPropertyRelative(key == "first" ? "hasFirst" : "hasSecond").boolValue) continue;
                SerializedProperty source = current.FindPropertyRelative(key); int index = list.arraySize++;
                SetCoordinate(list.GetArrayElementAtIndex(index), new BoardCoordinate(source.FindPropertyRelative("row").intValue, source.FindPropertyRelative("column").intValue));
            }
        }
    }
}
