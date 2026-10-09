#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using LevelAuthoring.Runtime;
using Levels;
using Simulation;
using Tutorial;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private int tutorialStep;
        private TutorialConditionKind newTutorialCondition = TutorialConditionKind.SuccessfulSwap;

        private void TutorialEdit(string label, Action<LevelDefinition> edit) => Edit(() =>
        {
            if (sharedTutorialDraft == null) TutorialDraftEditing.EditLevel(Session, label, edit);
            else TutorialDraftEditing.EditFlowSteps(sharedTutorialDraft.Session, sharedTutorialDraft.FlowId, label, edit);
        });
        private void StepEdit(Action<TutorialStepDefinition> edit) => TutorialEdit("튜토리얼 단계 편집", level => edit(level.Tutorial.steps[tutorialStep]));
        private void ConditionEdit(int index, Action<TutorialConditionDefinition> edit) => StepEdit(step => edit(step.conditions[index]));

        private void DrawTutorial()
        {
            TutorialToggle(inspector, "tutorial-preview", "보드 강조 미리보기", tutorialPreview, value => { tutorialPreview = value; Refresh(); });
            inspector.Add(new Label("밝은 칸은 강조 대상입니다. 후속 단계의 이동·생성 후 위치는 재생 검사와 게임 시험에서 확인하세요.") { style = { whiteSpace = WhiteSpace.Normal } });
            using var graph = new AuthoringObjectGraph((sharedTutorialDraft?.Session ?? Session).Documents);
            var level = (LevelDefinition)graph.Resolve(Session.SelectedLevelId);
            if (sharedTutorialDraft != null)
            {
                var flow = (TutorialFlowDefinition)graph.Resolve(sharedTutorialDraft.FlowId);
                DrawSharedDraft(flow);
                level.Tutorial.flow = null; level.Tutorial.steps = flow.steps;
                DrawTutorialSteps(level); return;
            }
            LevelTutorialDefinition tutorial = level.Tutorial;
            inspector.Add(new Label("1. 단계를 추가하세요. 2. 보드에서 조작할 칸을 선택하세요. 3. 필요한 행동과 완료 조건을 붙이세요.") { style = { whiteSpace = WhiteSpace.Normal } });
            Number(inspector, "tutorial-seed", "튜토리얼 시드", tutorial.seed, value => TutorialEdit("튜토리얼 시드", data => data.Tutorial.seed = value));
            TutorialText(inspector, "tutorial-completion", "학습 완료 ID", tutorial.completionId, value => TutorialEdit("학습 ID", data => data.Tutorial.completionId = value),
                "같은 ID를 쓰면 같은 학습으로 간주합니다. 비워 두면 기존 레벨별 규칙을 사용합니다.");
            TutorialText(inspector, "tutorial-previous", "선행 레벨 번호 (쉼표 구분)", string.Join(",", tutorial.previousLevelNumbers), value => TutorialEdit("선행 레벨", data =>
                data.Tutorial.previousLevelNumbers = value.Split(',').Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => int.Parse(part.Trim())).ToList()));
            DrawTutorialSamples(level);
            if (DrawTutorialSharing(level)) return;
            DrawTutorialSteps(level);
        }
        private void DrawTutorialSteps(LevelDefinition level)
        {
            var tutorial = level.Tutorial;
            Button(inspector, "tutorial-add-step", "새 단계 추가", () => TutorialEdit("튜토리얼 단계 추가", data =>
            {
                data.Tutorial.steps.Add(new TutorialStepDefinition { authoringId = Guid.NewGuid().ToString("N"), automaticHighlights = true });
                tutorialStep = data.Tutorial.steps.Count - 1;
            }));
            if (tutorial.steps.Count == 0) return;
            tutorialStep = Math.Max(0, Math.Min(tutorialStep, tutorial.steps.Count - 1));
            Choice(inspector, "tutorial-step", "편집할 단계", Enumerable.Range(0, tutorial.steps.Count).Select(i => i.ToString()).ToArray(),
                tutorial.steps.Select((step, i) => (i + 1) + ". " + step.instructions).ToArray(), tutorialStep.ToString(), value => { tutorialStep = int.Parse(value); Refresh(); });
            Button(inspector, "tutorial-copy-step", "현재 단계 복사", () => TutorialEdit("단계 복사", data =>
            {
                var steps = data.Tutorial.steps;
                var copy = TutorialAuthoringRules.CopySample(steps, new List<TutorialStepDefinition> { steps[tutorialStep] });
                steps.InsertRange(++tutorialStep, copy);
            }));
            Button(inspector, "tutorial-remove-step", "현재 단계 삭제", () => TutorialEdit("단계 삭제", data => data.Tutorial.steps.RemoveAt(tutorialStep)));
            Button(inspector, "tutorial-step-up", "단계 위로", () => MoveTutorialStep(-1)).SetEnabled(tutorialStep > 0);
            Button(inspector, "tutorial-step-down", "단계 아래로", () => MoveTutorialStep(1)).SetEnabled(tutorialStep + 1 < tutorial.steps.Count);
            DrawTutorialStep(level, tutorial.steps[tutorialStep]);
        }
        private void MoveTutorialStep(int offset) => TutorialEdit("단계 순서", level =>
        {
            var steps = level.Tutorial.steps; var step = steps[tutorialStep];
            steps.RemoveAt(tutorialStep); tutorialStep += offset; steps.Insert(tutorialStep, step);
        });
        private void DrawTutorialStep(LevelDefinition level, TutorialStepDefinition step)
        {
            TutorialText(inspector, "tutorial-instructions", "플레이어 안내문", step.instructions, value => StepEdit(data => data.instructions = value));
            TutorialEnum(inspector, "tutorial-kind", "허용 행동", step.kind, value => StepEdit(data => data.kind = value),
                value => value == TutorialStepKind.Description ? "설명" : value == TutorialStepKind.Swap ? "교환" : value == TutorialStepKind.PowerSwap ? "파워 교환" : "아이템");
            TutorialToggle(inspector, "tutorial-auto-highlight", "조작 칸 자동 강조", step.automaticHighlights, value => StepEdit(data => data.automaticHighlights = value));
            Button(inspector, "tutorial-set-highlights", "선택 칸을 강조 영역으로", () => StepEdit(data => { data.highlights = SelectedCoordinates(); data.automaticHighlights = false; }));
            Button(inspector, "tutorial-pick-highlights", "보드에서 강조 영역 선택", () => BeginTutorialPick("highlights"));
            if (step.kind != TutorialStepKind.Description)
            {
                inspector.Add(new Label("보드 클릭으로 선택, Shift+클릭으로 사각 영역을 선택한 뒤 아래 버튼으로 적용하세요.") { style = { whiteSpace = WhiteSpace.Normal } });
                Button(inspector, "tutorial-set-action", "선택 칸을 조작 위치로", () => StepEdit(data =>
                {
                    var cells = SelectedCoordinates();
                    if (cells.Count < 1 || cells.Count > 2) throw new ArgumentException("조작할 한 칸 또는 두 칸을 선택하세요.");
                    data.hasFirst = true; data.first = cells[0]; data.firstBinding = "";
                    data.hasSecond = cells.Count == 2; if (data.hasSecond) data.second = cells[1]; data.secondBinding = "";
                }));
                inspector.Add(new Label("첫 칸: " + (step.hasFirst ? step.first.ToString() : "없음") + " / 둘째: " + (step.hasSecond ? step.second.ToString() : "없음")));
                Button(inspector, "tutorial-pick-action", "보드에서 조작 위치 선택", () => BeginTutorialPick("action"));
                Button(inspector, "tutorial-pick-area", "보드에서 자유 조작 영역 선택", () => BeginTutorialPick("area"));
                Button(inspector, "tutorial-set-area", "선택 칸을 자유 조작 영역으로", () => StepEdit(data => data.actionArea = SelectedCoordinates()));
                Button(inspector, "tutorial-clear-area", "자유 조작 영역 해제", () => StepEdit(data => data.actionArea.Clear()));
                TutorialText(inspector, "tutorial-first-binding", "첫 칸: 이전 생성 연결 이름", step.firstBinding, value => StepEdit(data => data.firstBinding = value));
                TutorialText(inspector, "tutorial-second-binding", "둘째 칸: 이전 생성 연결 이름", step.secondBinding, value => StepEdit(data => data.secondBinding = value));
                if (step.kind == TutorialStepKind.Item)
                {
                    TutorialEnum(inspector, "tutorial-item", "아이템", step.item, value => StepEdit(data => data.item = value), ItemLabel);
                    Number(inspector, "tutorial-free-count", "무료 체험 횟수", step.freeItemCount, value => StepEdit(data => data.freeItemCount = value));
                }
                if (step.kind == TutorialStepKind.PowerSwap) TutorialPower(inspector, level, "tutorial-action-power", "조작할 파워", step.actionDefinitionId, value => StepEdit(data => data.actionDefinitionId = value));
                TutorialEnum(inspector, "tutorial-combination", "완료 조건 조합", step.combination, value => StepEdit(data => data.combination = value), value => value == TutorialConditionCombination.All ? "모두 만족" : "하나 이상 만족");
                DrawTutorialConditions(level, step);
                DrawTutorialConditionSamples(level);
            }
        }
        private List<BoardCoordinate> SelectedCoordinates() => Session.SelectedCells.Select(cell => new BoardCoordinate(cell / 9, cell % 9)).ToList();
        private static string ItemLabel(BoardItem value) => value == BoardItem.Hammer ? "망치" : value == BoardItem.Swap ? "교환" : "섞기";
        private static void TutorialText(VisualElement host, string name, string label, string current, Action<string> changed, string hint = "")
        {
            var field = new TextField(label) { name = name, value = current ?? "", isDelayed = true, tooltip = hint };
            field.RegisterValueChangedCallback(e => changed(e.newValue)); host.Add(field);
        }
        private static void TutorialToggle(VisualElement host, string name, string label, bool current, Action<bool> changed)
        {
            var field = new Toggle(label) { name = name, value = current };
            field.RegisterValueChangedCallback(e => changed(e.newValue)); host.Add(field);
        }
        private static void TutorialEnum<T>(VisualElement host, string name, string label, T current, Action<T> changed, Func<T, string> display = null) where T : struct, Enum
        {
            var values = Enum.GetValues(typeof(T)).Cast<T>().ToArray();
            Choice(host, name, label, values.Select(value => value.ToString()).ToArray(), values.Select(value => display?.Invoke(value) ?? value.ToString()).ToArray(),
                current.ToString(), value => changed((T)Enum.Parse(typeof(T), value)));
        }
    }
}
#endif
