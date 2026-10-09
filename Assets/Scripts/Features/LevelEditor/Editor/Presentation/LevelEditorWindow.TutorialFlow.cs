using System;
using System.Collections.Generic;
using System.Linq;
using Tutorial;
using Tutorial.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        [SerializeField] private LevelEditorWindow jsonFlowOwner;
        [SerializeField] private TutorialFlowDefinition editingTutorialFlow;
        [SerializeField] private TutorialFlowDefinition tutorialFlowDraft;
        [SerializeField] private string tutorialFlowOriginal;

        public static LevelEditorWindow OpenTutorialFlow(LevelDefinition source, TutorialFlowDefinition flow, LevelEditorWindow jsonOwner = null)
        {
            LevelEditorWindow window = CreateInstance<LevelEditorWindow>();
            window.jsonFlowOwner = jsonOwner; window.jsonFlowDraftMode = jsonOwner != null;
            if (jsonOwner != null)
            {
                window.jsonFlowDocumentId = jsonOwner.jsonWorkspace.DocumentId(flow);
                window.jsonFlowSourceLevelId = jsonOwner.jsonWorkspace.DocumentId(source);
                window.jsonFlowParentFolder = jsonOwner.jsonFolder;
            }
            window.temporaryTutorialSample = Instantiate(source);
            window.temporaryTutorialSample.hideFlags = HideFlags.HideAndDontSave;
            // 공통 기본값을 편집한다. 특정 레벨의 연결 값은 원본에 섞지 않는다.
            window.temporaryTutorialSample.Tutorial.flow = null;
            window.temporaryTutorialSample.Tutorial.bindings.Clear();
            window.temporaryTutorialSample.Tutorial.steps = TutorialFlowAuthoring.CopySteps(flow.steps);
            window.editingTutorialFlow = flow;
            window.tutorialFlowDraft = Instantiate(flow); window.tutorialFlowDraft.hideFlags = HideFlags.HideAndDontSave;
            window.tutorialFlowOriginal = JsonUtility.ToJson(flow);
            window.tutorialSampleExpected = "공통 진행 구성 편집 사본 · 아래 적용 버튼을 누를 때만 공유 원본을 수정합니다.";
            window.tutorialComposerMode = true; window.gameTutorialMode = TutorialRunMode.Always;
            window.position = new Rect(80, 80, 1280, 900); window.level = window.temporaryTutorialSample;
            window.ShowUtility(); window.CreateGUI(); return window;
        }

        private void AddTutorialFlowControls(VisualElement host)
        {
            if (editingTutorialFlow == null || tutorialFlowDraft == null) return;
            workspaceLevel.SetEnabled(false);
            Foldout controls = new Foldout { text = "공통 원본 편집 · 레벨별 설정 항목 지정", value = true, name = "tutorial-flow-draft" };
            host.Add(controls);
            foreach (LevelDefinition used in jsonFlowOwner != null ? jsonFlowOwner.JsonFlowUsers(editingTutorialFlow) : TutorialFlowUsageQuery.Find(editingTutorialFlow))
                controls.Add(new Button(() => { if (jsonFlowOwner != null) jsonFlowOwner.OpenJsonUsageLevel(used); else OpenLevel(used); }) { text = $"사용 중: 레벨 {used.LevelNumber} · {used.name}" });
            controls.Add(new HelpBox("아래에서 단계를 편집하고, 레벨마다 달라질 항목만 체크하세요. 공통 원본 적용 전까지는 사본입니다. 창을 닫으면 적용하지 않은 수정은 버립니다.", HelpBoxMessageType.Info));
            VisualElement declarations = new VisualElement(); controls.Add(declarations);
            void RefreshDeclarations()
            {
                declarations.Clear(); TutorialFlowAuthoring.EnsureIds(level.Tutorial.steps);
                for (int i = 0; i < level.Tutorial.steps.Count; i++)
                {
                    TutorialStepDefinition step = level.Tutorial.steps[i];
                    Foldout row = new Foldout { text = $"{i + 1}단계 · {step.instructions}", value = false }; declarations.Add(row);
                    List<(TutorialFlowField field, TutorialConditionDefinition condition)> candidates = new List<(TutorialFlowField, TutorialConditionDefinition)>();
                    if (step.hasFirst && string.IsNullOrEmpty(step.firstBinding)) candidates.Add((TutorialFlowField.First, null));
                    if (step.hasSecond && string.IsNullOrEmpty(step.secondBinding)) candidates.Add((TutorialFlowField.Second, null));
                    if (step.actionArea.Count > 0) candidates.Add((TutorialFlowField.ActionArea, null));
                    if (!step.automaticHighlights) candidates.Add((TutorialFlowField.Highlights, null));
                    if (step.kind == TutorialStepKind.Item) candidates.Add((TutorialFlowField.FreeItemCount, null));
                    if (!string.IsNullOrEmpty(step.actionDefinitionId)) candidates.Add((TutorialFlowField.ActionDefinitionId, null));
                    foreach (TutorialConditionDefinition condition in step.conditions)
                    {
                        candidates.Add((TutorialFlowField.RequiredCount, condition));
                        if (condition.target != null) candidates.Add((TutorialFlowField.Target, condition));
                        if (condition.kind == TutorialConditionKind.Match)
                        { candidates.Add((TutorialFlowField.MatchSize, condition)); candidates.Add((TutorialFlowField.Color, condition)); }
                        if (!string.IsNullOrEmpty(condition.powerDefinitionId)) candidates.Add((TutorialFlowField.PowerDefinitionId, condition));
                        if (condition.kind == TutorialConditionKind.MissionProgress) candidates.Add((TutorialFlowField.MissionIndex, condition));
                    }
                    foreach ((TutorialFlowField field, TutorialConditionDefinition condition) in candidates)
                    {
                        string conditionId = condition?.authoringId ?? "";
                        string title = (condition == null ? "" : $"조건 {step.conditions.IndexOf(condition) + 1} · ") + TutorialFlowAuthoring.Label(field);
                        TutorialFlowParameter existing = tutorialFlowDraft.parameters.FirstOrDefault(value => value.stepId == step.authoringId && value.conditionId == conditionId && value.field == field);
                        Toggle toggle = new Toggle(title + "을 레벨별로 설정") { value = existing != null, tooltip = "체크하면 각 레벨에서 값을 입력합니다. 해제하면 공통 원본의 값을 모든 레벨이 사용합니다." };
                        toggle.RegisterValueChangedCallback(evt =>
                        {
                            Undo.RecordObject(tutorialFlowDraft, "레벨별 설정 선언");
                            if (evt.newValue) tutorialFlowDraft.parameters.Add(new TutorialFlowParameter { key = Guid.NewGuid().ToString("N"), label = title, help = "예시 값을 참고하고 이 레벨의 대상과 수치로 변경하세요.", stepId = step.authoringId, conditionId = conditionId, field = field });
                            else tutorialFlowDraft.parameters.RemoveAll(value => value.stepId == step.authoringId && value.conditionId == conditionId && value.field == field);
                            RefreshDeclarations();
                        }); row.Add(toggle);
                        if (existing != null)
                        {
                            TextField label = new TextField("레벨에 보여줄 이름") { value = existing.label };
                            label.RegisterValueChangedCallback(evt => { Undo.RecordObject(tutorialFlowDraft, "설정 이름"); existing.label = evt.newValue; }); row.Add(label);
                            TextField help = new TextField("입력 도움말·예시") { value = existing.help, multiline = true };
                            help.RegisterValueChangedCallback(evt => { Undo.RecordObject(tutorialFlowDraft, "설정 도움말"); existing.help = evt.newValue; }); row.Add(help);
                        }
                    }
                }
                foreach (TutorialFlowParameter stale in tutorialFlowDraft.parameters.Where(value => !level.Tutorial.steps.Any(step => step.authoringId == value.stepId && (string.IsNullOrEmpty(value.conditionId) || step.conditions.Any(condition => condition.authoringId == value.conditionId)))).ToArray())
                    declarations.Add(new Button(() => { Undo.RecordObject(tutorialFlowDraft, "삭제된 연결 제거"); tutorialFlowDraft.parameters.Remove(stale); RefreshDeclarations(); })
                        { text = "삭제된 단계/조건 연결 정리: " + stale.label });
            }
            controls.Add(new Button(RefreshDeclarations) { text = "단계 수정 후 설정 항목 새로고침" }); RefreshDeclarations();
            VisualElement report = new VisualElement(); controls.Add(report);
            controls.Add(new Button(() =>
            {
                report.Clear();
                if (!TryPrepareJsonDraftTest(out string connectionError))
                { report.Add(new HelpBox(connectionError, HelpBoxMessageType.Error)); return; }
                if (level != temporaryTutorialSample || level == null) { report.Add(new HelpBox("원래 편집 사본이 아닙니다. 원본 편집 창을 다시 여세요.", HelpBoxMessageType.Error)); return; }
                if (JsonUtility.ToJson(editingTutorialFlow) != tutorialFlowOriginal)
                { report.Add(new HelpBox("다른 창에서 원본이 변경되었습니다. 이 창을 닫고 원본 편집을 다시 열어 변경 내용을 확인하세요.", HelpBoxMessageType.Error)); return; }
                TutorialFlowAuthoring.EnsureIds(level.Tutorial.steps);
                if (tutorialFlowDraft.parameters.Any(value => !level.Tutorial.steps.Any(step => step.authoringId == value.stepId && (string.IsNullOrEmpty(value.conditionId) || step.conditions.Any(condition => condition.authoringId == value.conditionId)))))
                { report.Add(new HelpBox("삭제된 단계/조건의 연결을 먼저 정리하세요.", HelpBoxMessageType.Error)); return; }
                void ApplyFlow()
                {
                    Undo.RecordObject(editingTutorialFlow, "공통 튜토리얼 적용");
                    editingTutorialFlow.steps = TutorialFlowAuthoring.CopySteps(level.Tutorial.steps);
                    editingTutorialFlow.parameters = JsonUtility.FromJson<ParameterCopy>(JsonUtility.ToJson(new ParameterCopy { values = tutorialFlowDraft.parameters })).values;
                    EditorUtility.SetDirty(editingTutorialFlow);
                }
                try
                {
                    if (jsonFlowOwner != null) jsonFlowOwner.CommitJsonFlow(editingTutorialFlow, ApplyFlow);
                    else { ApplyFlow(); if (EditorUtility.IsPersistent(editingTutorialFlow)) AssetDatabase.SaveAssetIfDirty(editingTutorialFlow); }
                }
                catch (Exception error) { report.Add(new HelpBox(error.Message, HelpBoxMessageType.Error)); return; }                tutorialFlowOriginal = JsonUtility.ToJson(editingTutorialFlow);
                IReadOnlyList<(LevelDefinition level, LevelValidationIssue issue)> issues = jsonFlowOwner == null ? TutorialFlowUsageQuery.Validate(editingTutorialFlow) :
                    jsonFlowOwner.JsonFlowUsers(editingTutorialFlow).SelectMany(used => LevelTutorialReplayValidator.Validate(used).Select(issue => (used, issue))).ToArray();
                report.Add(new HelpBox(issues.Count == 0 ? "공통 원본 적용 및 사용 레벨 검사 통과" : "원본 적용됨 · 아래 레벨의 설정을 확인하세요. 새 선언은 설정 목록 동기화가 필요합니다.", issues.Count == 0 ? HelpBoxMessageType.Info : HelpBoxMessageType.Warning));
                foreach ((LevelDefinition affected, LevelValidationIssue issue) in issues)
                    report.Add(new Button(() => { if (jsonFlowOwner != null) jsonFlowOwner.OpenJsonUsageLevel(affected); else OpenLevel(affected); }) { text = $"레벨 {affected.LevelNumber}: {issue}", tooltip = "해당 레벨을 열어 튜토리얼 오류와 연결 설정을 확인합니다." });
            }) { text = "공통 원본에 적용하고 사용 레벨 검사", name = "tutorial-flow-apply" });
        }
        [Serializable] private sealed class ParameterCopy { public List<TutorialFlowParameter> values; }
    }
}
