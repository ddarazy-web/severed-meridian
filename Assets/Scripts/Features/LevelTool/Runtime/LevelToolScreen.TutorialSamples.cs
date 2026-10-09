#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Runtime;
using Levels;
using Simulation;
using Tutorial;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private string tutorialSampleName = "새 샘플", tutorialSampleDescription = "", selectedTutorialSample;
        private string tutorialSampleBoard = "swap";
        private int tutorialConditionSample;

        private void DrawTutorialSamples(LevelDefinition level)
        {
            bool independent = level.Tutorial.flow == null;
            var samples = new Foldout { text = "처음 만들기 · 재사용 샘플", value = false }; inspector.Add(samples);
            samples.Add(new Label("보드에서 두 칸을 선택하면 매칭 안내를 바로 시작할 수 있습니다. 만든 단계를 내 샘플로 등록하면 다른 레벨에서 사본으로 재사용합니다.") { style = { whiteSpace = WhiteSpace.Normal } });
            Button(samples, "tutorial-quick-match", "선택한 두 칸으로 3매칭 단계", () => AddQuickTutorial(true)).SetEnabled(independent);
            Button(samples, "tutorial-quick-swap", "선택한 두 칸으로 교환 단계", () => AddQuickTutorial(false)).SetEnabled(independent);
            TutorialText(samples, "tutorial-sample-name", "저장할 샘플 이름", tutorialSampleName, value => tutorialSampleName = value);
            TutorialText(samples, "tutorial-sample-description", "샘플 설명", tutorialSampleDescription, value => tutorialSampleDescription = value);
            int stepCount = (level.Tutorial.flow?.steps ?? level.Tutorial.steps).Count;
            if (stepCount > 0)
                Choice(samples, "tutorial-sample-step", "등록할 단계", Enumerable.Range(0, stepCount).Select(index => index.ToString()).ToArray(),
                    (level.Tutorial.flow?.steps ?? level.Tutorial.steps).Select((step, index) => (index + 1) + ". " + step.instructions).ToArray(),
                    Math.Max(0, Math.Min(tutorialStep, stepCount - 1)).ToString(), value => { tutorialStep = int.Parse(value); Refresh(); });
            Button(samples, "tutorial-save-step-sample", "현재 단계만 내 샘플로 등록", () => Edit(() =>
                selectedTutorialSample = TutorialDraftEditing.CreateSample(Session, tutorialSampleName, tutorialSampleDescription, Math.Max(0, Math.Min(tutorialStep, stepCount - 1))))).SetEnabled(stepCount > 0);
            Button(samples, "tutorial-save-all-sample", "전체 진행을 내 샘플로 등록", () => Edit(() =>
                selectedTutorialSample = TutorialDraftEditing.CreateSample(Session, tutorialSampleName, tutorialSampleDescription, -1))).SetEnabled(stepCount > 0);
            var library = Session.Documents.Where(doc => doc.Kind == "tutorialSample").ToArray();
            if (!library.Any(doc => doc.Id == selectedTutorialSample)) selectedTutorialSample = library.FirstOrDefault()?.Id;
            Choice(samples, "tutorial-user-sample", "내 샘플", library.Select(doc => doc.Id).ToArray(), library.Select(doc => (string)doc.Data["displayName"]).ToArray(),
                selectedTutorialSample, value => { selectedTutorialSample = value; Refresh(); });
            var selected = library.FirstOrDefault(doc => doc.Id == selectedTutorialSample);
            if (selected != null)
            {
                string preview = (string)selected.Data["description"] + "\n" + string.Join("\n", selected.Data["steps"].Select((step, index) => (index + 1) + ". " + step["instructions"]));
                samples.Add(new Label(preview) { name = "tutorial-sample-preview", style = { whiteSpace = WhiteSpace.Normal } });
                Button(samples, "tutorial-apply-sample", "끝에 사본으로 추가", () => Edit(() =>
                {
                    TutorialDraftEditing.ApplySample(Session, selectedTutorialSample);
                    tutorialStep = Session.Get(Session.SelectedLevelId).Data["tutorial"]["steps"].Count() - 1;
                })).SetEnabled(independent);
                if (!independent) samples.Add(new Label("공유 구성에는 독립 복사 후 샘플을 적용하세요.") { style = { whiteSpace = WhiteSpace.Normal } });
            }
            Choice(samples, "tutorial-sample-board", "완성된 시험 보드", TutorialSampleBoards.All.Select(value => value.Id).ToArray(), TutorialSampleBoards.All.Select(value => value.Title).ToArray(),
                tutorialSampleBoard, value => { tutorialSampleBoard = value; Refresh(); });
            var board = TutorialSampleBoards.All.First(value => value.Id == tutorialSampleBoard);
            samples.Add(new Label(board.ExpectedResult) { name = "tutorial-sample-board-preview", style = { whiteSpace = WhiteSpace.Normal } });
            Button(samples, "tutorial-add-sample-board", "시험 보드 사본을 새 레벨로 추가", () => Edit(() => TutorialDraftEditing.AddSampleBoard(Session, tutorialSampleBoard)))
                .tooltip = "현재 레벨을 바꾸지 않고 새 레벨을 추가합니다. 상단 게임 시험에서 실행하고, 필요 없으면 Undo로 제거하세요. 저장하면 작업 폴더에 포함됩니다.";
        }
        private void AddQuickTutorial(bool match) => TutorialEdit("선택 칸으로 새 안내", level =>
        {
            var cells = SelectedCoordinates();
            if (cells.Count != 2 || Math.Abs(cells[0].Row - cells[1].Row) + Math.Abs(cells[0].Column - cells[1].Column) != 1)
                throw new ArgumentException("서로 붙어 있는 두 칸을 선택하세요.");
            var step = TutorialSampleCatalog.CreateSwap(cells[0], cells[1], match);
            TutorialAuthoringRules.EnsureIds(new List<TutorialStepDefinition> { step });
            level.Tutorial.steps.Add(step); tutorialStep = level.Tutorial.steps.Count - 1;
        });
        private void DrawTutorialConditionSamples(LevelDefinition level)
        {
            var library = TutorialSampleCatalog.ConditionSamples(level.CreateElementCatalog()).Concat(
                Enum.GetValues(typeof(BoardItem)).Cast<BoardItem>().Select(TutorialSampleCatalog.ItemSample)).ToArray();
            tutorialConditionSample = Math.Max(0, Math.Min(tutorialConditionSample, library.Length - 1));
            var menu = new Foldout { text = "조건 조합 샘플", value = false }; inspector.Add(menu);
            Choice(menu, "tutorial-condition-sample", "조건 샘플", Enumerable.Range(0, library.Length).Select(index => index.ToString()).ToArray(), library.Select(value => value.Title).ToArray(),
                tutorialConditionSample.ToString(), value => { tutorialConditionSample = int.Parse(value); Refresh(); });
            menu.Add(new Label(library[tutorialConditionSample].Preview) { name = "tutorial-condition-sample-preview", style = { whiteSpace = WhiteSpace.Normal } });
            Button(menu, "tutorial-add-condition-sample", "현재 단계에 조건 사본 추가", () => TutorialEdit("조건 샘플 적용", data =>
            {
                var copy = library[tutorialConditionSample].CreateConditions();
                var cells = SelectedCoordinates();
                foreach (var condition in copy)
                {
                    condition.authoringId = Guid.NewGuid().ToString("N");
                    if (condition.target == null || cells.Count == 0) continue;
                    condition.target.kind = cells.Count == 1 && condition.kind != TutorialConditionKind.Generated ? TutorialTargetKind.Entity : TutorialTargetKind.Area;
                    condition.target.layer = layer == "Cover" ? TutorialTargetLayer.Cover : layer == "Dust" ? TutorialTargetLayer.Floor : TutorialTargetLayer.Content;
                    condition.target.coordinate = cells[0]; condition.target.cells = cells.ToList();
                }
                data.Tutorial.steps[tutorialStep].conditions.AddRange(copy);
            }));
        }
    }
}
#endif
