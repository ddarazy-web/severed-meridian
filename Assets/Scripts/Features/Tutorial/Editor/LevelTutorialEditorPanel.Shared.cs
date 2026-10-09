using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tutorial.Editor
{
    public sealed partial class LevelTutorialEditorPanel
    {
        private bool AddSharedControls(SerializedProperty tutorial)
        {
            fields.Add(new Button(() => Application.OpenURL(System.IO.Path.GetFullPath("Docs/MoonRabbitJunkyard/Manual/tutorial.html")))
                { text = "튜토리얼 만들기 · 사용 설명", name = "tutorial-manual", tooltip = "샘플 선택 → 셀 연결 → 수치 입력 → 테스트를 예제로 설명합니다." });
            fields.Add(new PropertyField(tutorial.FindPropertyRelative("completionId"), "학습 완료 ID")
                { tooltip = "전체 튜토리얼의 완료 기록 이름입니다. 같은 학습만 같은 ID를 사용하세요. 예: basic.swap" });
            fields.Add(new PropertyField(tutorial.FindPropertyRelative("previousLevelNumbers"), "이어받을 이전 레벨 번호")
                { tooltip = "이전에 같은 학습을 진행했던 레벨 번호만 등록합니다. 새 학습은 비워 두세요. 기존 기록은 삭제하지 않습니다." });
            if (isolatedJsonDraft)
            {
                fields.Add(new HelpBox(LevelEditorWindow.JsonFlowDraftRestriction, HelpBoxMessageType.Info));
                return false;
            }
            if (jsonWorkspace != null) AddJsonFlowChoice();
            else
            {
            ObjectField choice = new ObjectField("공통 진행 구성") { name = "tutorial-flow-choice", objectType = typeof(TutorialFlowDefinition), allowSceneObjects = false, value = owner.Tutorial.flow,
                tooltip = "동작 순서·조건·안내를 공유합니다. 레벨별 값만 수정하면 공유 원본은 바뀌지 않습니다." };
            choice.RegisterValueChangedCallback(evt =>
            {
                try
                {
                    Mutate("공통 진행 구성 연결", () => { if (evt.newValue == null) TutorialFlowAuthoring.Detach(owner); else TutorialFlowAuthoring.Connect(owner, (TutorialFlowDefinition)evt.newValue); });
                    Rebuild();
                }
                catch (Exception error) { choice.SetValueWithoutNotify(owner.Tutorial.flow); fields.Add(new HelpBox(error.Message, HelpBoxMessageType.Error)); }
            }); fields.Add(choice);
            }
            if (owner.Tutorial.flow == null)
            {
                fields.Add(new HelpBox("이 레벨만의 독립 구성입니다. 샘플로 단계를 만든 뒤 공통 구성으로 저장하면 다른 레벨에서도 재사용할 수 있습니다.", HelpBoxMessageType.Info));
                fields.Add(new Button(CreateSharedFlow) { text = "현재 흐름을 공통 구성으로 저장", name = "tutorial-flow-create" });
                AddUserSamples(); return false;
            }
            TutorialFlowDefinition flow = owner.Tutorial.flow;
            fields.Add(new HelpBox("공통 진행 구성은 공유 중입니다. 아래 값만 이 레벨에 저장합니다. 동작 순서나 조건 종류가 달라야 한다면 독립 복사하세요.", HelpBoxMessageType.Info));
            fields.Add(new Button(() => { if (openFlow != null) openFlow(flow); else LevelEditorWindow.OpenTutorialFlow(owner, flow); })
                { text = "공통 원본 편집 · 사용 레벨 확인", name = "tutorial-flow-edit", tooltip = "보드 에디터의 사본에서 수정합니다. 적용 버튼을 누르기 전에는 원본이 바뀌지 않습니다." });
            fields.Add(new Button(() => { Mutate("공통 구성 독립 복사", () => TutorialFlowAuthoring.Detach(owner)); Rebuild(); })
                { text = "독립 복사 · 공유 연결 해제", name = "tutorial-flow-detach", tooltip = "현재 레벨 값을 적용한 독립 단계를 만듭니다. 완료 ID는 유지되며 Undo 가능합니다." });
            fields.Add(new Label("이 레벨의 설정") { name = "tutorial-level-settings" });
            fields.Add(new Button(() => { Mutate("공통 구성 설정 동기화", () => TutorialFlowAuthoring.Synchronize(owner)); Rebuild(); })
                { text = "설정 목록 동기화", tooltip = "추가·삭제된 설정 항목을 반영합니다. 이름과 종류가 같은 기존 값은 유지합니다. Undo 가능." });
            SerializedProperty bindings = tutorial.FindPropertyRelative("bindings");
            foreach (TutorialFlowParameter parameter in flow.parameters)
            {
                int index = owner.Tutorial.bindings.FindIndex(value => value.key == parameter.key && value.field == parameter.field);
                VisualElement card = new VisualElement { name = "tutorial-binding-" + parameter.key }; card.style.marginTop = 8; fields.Add(card);
                card.Add(new Label(string.IsNullOrWhiteSpace(parameter.label) ? TutorialFlowAuthoring.Label(parameter.field) : parameter.label));
                card.Add(new HelpBox(string.IsNullOrWhiteSpace(parameter.help) ? "이 레벨에 사용할 값입니다. 다른 레벨에는 영향을 주지 않습니다." : parameter.help, HelpBoxMessageType.Info));
                if (index < 0) { card.Add(new HelpBox("설정 목록 동기화를 누르고 값을 입력하세요.", HelpBoxMessageType.Error)); continue; }
                TutorialFlowField kind = parameter.field;
                string property = kind == TutorialFlowField.First || kind == TutorialFlowField.Second ? "coordinate" :
                    kind == TutorialFlowField.ActionArea || kind == TutorialFlowField.Highlights ? "cells" : kind == TutorialFlowField.Target ? "target" :
                    kind == TutorialFlowField.PowerDefinitionId || kind == TutorialFlowField.ActionDefinitionId ? "definitionId" : kind == TutorialFlowField.Color ? "color" : "number";
                card.Add(new PropertyField(bindings.GetArrayElementAtIndex(index).FindPropertyRelative(property), TutorialFlowAuthoring.Label(kind)) { tooltip = parameter.help });
                if (property == "coordinate" || property == "cells" || property == "target")
                    card.Add(new Button(() => BeginBindingPick(index)) { text = "보드에서 선택", tooltip = "여러 칸은 선택 완료로 확정하고 Esc로 취소합니다." });
            }
            pickStatus = new Label("보드에서 선택 버튼으로 대상을 연결하세요.") { name = "tutorial-pick-status" }; fields.Add(pickStatus);
            foreach (LevelValidationIssue issue in LevelTutorialValidator.Validate(owner))
            {
                fields.Add(new HelpBox(issue.ToString(), HelpBoxMessageType.Error));
                TutorialFlowParameter parameter = flow.parameters.FirstOrDefault(value => issue.Message.Contains(value.key) || MatchesBindingIssue(flow, value, issue.PropertyPath));
                if (parameter != null) fields.Add(new Button(() =>
                {
                    VisualElement target = fields.Q("tutorial-binding-" + parameter.key);
                    target?.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(target);
                    target?.Query<VisualElement>().ToList().FirstOrDefault(element => element.focusable && element.enabledInHierarchy)?.Focus();
                    if (issue.Coordinate.HasValue) board.ShowTutorialTargets(new[] { issue.Coordinate.Value }, null, null);
                }) { text = "이 설정으로 이동" });
            }
            stageHost?.Clear(); testHost?.Clear();
            if (stageHost != null)
                for (int i = 0; i < flow.steps.Count; i++)
                { int index = i; stageHost.Add(new Button(() => { selected = index; ShowTargets(); }) { text = $"{i + 1}. {flow.steps[i].instructions}" }); }
            fields.Bind(input); ShowTargets(); return true;
        }

        private static bool MatchesBindingIssue(TutorialFlowDefinition flow, TutorialFlowParameter parameter, string path)
        {
            int index = flow.steps.FindIndex(step => step.authoringId == parameter.stepId);
            if (index < 0 || path == null) return false;
            string prefix = $"tutorial.steps.Array.data[{index}]";
            if (!string.IsNullOrEmpty(parameter.conditionId))
            {
                int condition = flow.steps[index].conditions.FindIndex(value => value.authoringId == parameter.conditionId);
                prefix += $".conditions.Array.data[{condition}]";
            }
            string field = parameter.field switch
            {
                TutorialFlowField.First => "first", TutorialFlowField.Second => "second", TutorialFlowField.ActionArea => "actionArea",
                TutorialFlowField.Highlights => "highlights", TutorialFlowField.FreeItemCount => "freeItemCount", TutorialFlowField.RequiredCount => "requiredCount",
                TutorialFlowField.MatchSize => "matchSize", TutorialFlowField.Target => "target", TutorialFlowField.PowerDefinitionId => "powerDefinitionId",
                TutorialFlowField.Color => "color", TutorialFlowField.MissionIndex => "missionIndex", TutorialFlowField.ActionDefinitionId => "actionDefinitionId", _ => ""
            };
            prefix += "." + field;
            return path == prefix || path.StartsWith(prefix + ".", StringComparison.Ordinal);
        }

        private void BeginBindingPick(int index)
        {
            CancelPicking(); board.CancelStroke(); TutorialFlowBinding binding = owner.Tutorial.bindings[index];
            bool target = binding.field == TutorialFlowField.Target;
            bool area = binding.field == TutorialFlowField.ActionArea || binding.field == TutorialFlowField.Highlights || target && binding.target.kind == TutorialTargetKind.Area;
            if (target && binding.target.kind != TutorialTargetKind.Entity && binding.target.kind != TutorialTargetKind.Area)
            { pickStatus.text = "대상 종류를 특정 개체 또는 영역으로 선택한 뒤 보드에서 지정하세요."; return; }
            HashSet<BoardCoordinate> cells = new HashSet<BoardCoordinate>(area ? target ? binding.target.cells : binding.cells : new List<BoardCoordinate>());
            void Apply(BoardCoordinate coordinate)
            {
                Edit(value =>
                {
                    SerializedProperty entry = value.FindPropertyRelative("bindings").GetArrayElementAtIndex(index);
                    if (target) entry = entry.FindPropertyRelative("target");
                    if (!area) SetCoordinate(entry.FindPropertyRelative("coordinate"), coordinate);
                    else
                    {
                        SerializedProperty list = entry.FindPropertyRelative("cells"); list.arraySize = cells.Count; int i = 0;
                        foreach (BoardCoordinate cell in cells.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column)) SetCoordinate(list.GetArrayElementAtIndex(i++), cell);
                    }
                });
            }
            picker = coordinate =>
            {
                if (!area) { Apply(coordinate); return; }
                if (!cells.Add(coordinate)) cells.Remove(coordinate);
                board.ShowTutorialTargets(cells.ToList(), null, null); pickStatus.text = $"{cells.Count}칸 선택 · 선택 완료로 확정 · Esc 취소";
            };
            if (area) fields.Add(new Button(() => Apply(default)) { text = "선택 완료", name = "tutorial-binding-confirm" });
            board.TutorialTargetPicked = picker; board.Focus(); pickStatus.text = "보드에서 대상을 선택하세요. Esc로 취소할 수 있습니다.";
        }

        private void CreateSharedFlow()
        {
            if (isolatedJsonDraft) { fields.Add(new HelpBox(LevelEditorWindow.JsonFlowDraftRestriction, HelpBoxMessageType.Warning)); return; }
            if (jsonWorkspace != null) { CreateJsonFlow(); return; }
            if (owner.Tutorial.steps.Count == 0) { fields.Add(new HelpBox("단계를 먼저 추가하세요.", HelpBoxMessageType.Warning)); return; }
            string path = EditorUtility.SaveFilePanelInProject("공통 진행 구성 저장", "TutorialFlow", "asset", "새 이름으로 저장하세요.", "Assets/Data");
            if (string.IsNullOrEmpty(path)) return;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) { fields.Add(new HelpBox("기존 에셋을 덮어쓰지 않습니다. 새 이름을 사용하세요.", HelpBoxMessageType.Error)); return; }
            TutorialFlowDefinition flow = ScriptableObject.CreateInstance<TutorialFlowDefinition>();
            flow.steps = TutorialFlowAuthoring.CopySteps(owner.Tutorial.steps); TutorialFlowAuthoring.EnsureIds(flow.steps);
            AssetDatabase.CreateAsset(flow, path); AssetDatabase.SaveAssetIfDirty(flow);
            TutorialFlowAuthoring.Connect(owner, flow); Rebuild(); LevelEditorWindow.OpenTutorialFlow(owner, flow);
        }

        private void AddUserSamples()
        {
            if (isolatedJsonDraft) return;
            if (jsonWorkspace != null) { AddJsonSamples(); return; }
            Foldout section = new Foldout { text = "내 샘플 · 저장한 구성을 복사해서 사용", value = false }; fields.Add(section);
            section.Add(new HelpBox("샘플은 적용 시 복사합니다. 이후 샘플 수정이 이미 적용한 단계에 전파되지 않습니다.", HelpBoxMessageType.Info));
            foreach (bool whole in new[] { false, true })
                section.Add(new Button(() =>
                {
                    if (owner.Tutorial.steps.Count == 0) return;
                    TutorialUserSampleStore.EnsureFolder();
                    string path = EditorUtility.SaveFilePanelInProject("내 샘플 저장", "TutorialSample", "asset", "샘플 이름을 지정하세요.", TutorialUserSampleStore.Folder);
                    if (path == "") return;
                    try { TutorialUserSampleStore.Save(path, whole ? owner.Tutorial.steps : new List<TutorialStepDefinition> { owner.Tutorial.steps[selected] }, whole ? "전체 흐름" : "한 단계"); }
                    catch (Exception error) { section.Add(new HelpBox(error.Message, HelpBoxMessageType.Error)); }
                }) { text = whole ? "전체 흐름을 내 샘플로 저장" : "현재 단계를 내 샘플로 저장" });
            ObjectField sample = new ObjectField("적용할 샘플") { objectType = typeof(TutorialUserSampleDefinition), allowSceneObjects = false, name = "tutorial-user-sample" };
            VisualElement preview = new VisualElement();
            Button apply = new Button(() =>
            { if (sample.value is TutorialUserSampleDefinition selectedSample) { Mutate("내 샘플 적용", () => TutorialUserSampleStore.Apply(owner, selectedSample)); Rebuild(); } }) { text = "미리 본 샘플을 끝에 추가", name = "tutorial-user-sample-apply" };
            apply.SetEnabled(false);
            sample.RegisterValueChangedCallback(evt =>
            {
                preview.Clear(); apply.SetEnabled(evt.newValue != null);
                if (evt.newValue is TutorialUserSampleDefinition selectedSample)
                    foreach (TutorialStepDefinition step in selectedSample.steps) preview.Add(new HelpBox($"{step.instructions}\n동작: {step.kind} · 조건 {step.conditions.Count}개", HelpBoxMessageType.Info));
            });
            section.Add(sample); section.Add(preview); section.Add(apply);
            section.Add(new Button(() => sample.value = null) { text = "샘플 적용 취소" });
        }
    }
}
