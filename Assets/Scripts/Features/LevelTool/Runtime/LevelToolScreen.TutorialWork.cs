#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using Levels;
using Tutorial;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private AuthoringEditSession.LevelWorkCheckpoint tutorialWork;
        private bool tutorialInternalNavigation;
        private VisualElement tutorialWorkBar;

        private void BeginTutorialWork()
        {
            if (tutorialWork != null || Session?.SelectedLevelId == null || busy || sharedTutorialDraft != null) return;
            tutorialWork = Session.BeginLevelWork();
            forceProperties = true; Refresh();
        }

        private void CompleteTutorialWork()
        {
            if (tutorialWork == null || sharedTutorialDraft != null) { Show("공유 원본 사본을 먼저 적용하거나 취소하세요."); return; }
            if (tutorialPick != null || purposeDraft != null) { Show("작성 중인 단계의 선택·설정을 적용하거나 취소한 뒤 완료하세요."); return; }
            if (!CommitCommandInput() || committingCommandInput) { Show("입력 확정을 마친 뒤 완료를 눌러 주세요."); return; }
            try
            {
                using var graph = new AuthoringObjectGraph(Session);
                LevelDefinition level = (LevelDefinition)graph.Resolve(Session.SelectedLevelId);
                var steps = TutorialFlowResolver.Resolve(level).steps;
                if (steps.Count == 0) throw new InvalidOperationException("튜토리얼 단계를 하나 이상 작성하세요.");
                var errors = LevelTutorialValidator.Validate(level);
                if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors.Select(error => error.Message)));
                Session.CompleteLevelWork(tutorialWork); tutorialWork = null;
                forceProperties = true; Refresh();
                Show("튜토리얼 작성 완료 · 메모리에 반영했습니다. 파일 저장은 별도입니다. " + (CreationChecklist().PlayChecked ? "플레이 조작 확인됨" : "플레이 확인 필요"));
            }
            catch (Exception error) { Show("완료 전 확인 필요: " + error.Message); }
        }

        private bool GuardTutorialWorkExit(Action next)
        {
            if (tutorialWork == null) return false;
            if (busy || modal != null) return true;
            VisualElement panel = OpenModal("튜토리얼을 작성 중입니다");
            panel.Add(new Label("계속 작성하거나 이번 튜토리얼 작업을 모두 취소하세요. 전체 취소는 이 레벨의 보드·공급도 작업 시작 상태로 복원합니다. 작업 전 변경과 다른 레벨·공유 데이터는 유지합니다.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            Button(panel, "tutorial-continue-work", "계속 작성", CloseModal);
            Button(panel, "tutorial-discard-work", "튜토리얼 작업 전체 취소", () =>
            {
                try
                {
                    Session.CancelLevelWork(tutorialWork); tutorialWork = null; sharedTutorialDraft = null;
                    DiscardUncommittedInputs(); CancelPendingInput(); CloseModal(); forceProperties = true; Refresh();
                    Show("튜토리얼 작업을 취소하고 보드·공급을 시작 상태로 복원했습니다. 다른 변경은 유지했습니다.");
                    next?.Invoke();
                }
                catch (Exception error) { Show("작업을 취소하지 못했습니다: " + error.Message); }
            });
            return true;
        }

        private void RefreshTutorialWorkBar()
        {
            if (tutorialWorkBar == null)
            {
                tutorialWorkBar = new VisualElement { name = "tutorial-work-bar" }; tutorialWorkBar.AddToClassList("panel-tabs");
                editor.Insert(editor.IndexOf(body), tutorialWorkBar);
                Button(tutorialWorkBar, "tutorial-return-work", "튜토리얼 작성으로", () => SelectWorkspacePage("튜토리얼"));
                Button(tutorialWorkBar, "tutorial-work-board", "보드 배치", () =>
                {
                    tutorialInternalNavigation = true;
                    try { SelectWorkspacePage("보드 편집"); }
                    finally { tutorialInternalNavigation = false; }
                });
                Button(tutorialWorkBar, "tutorial-work-complete", "작성 완료", CompleteTutorialWork);
                Button(tutorialWorkBar, "tutorial-work-cancel", "전체 취소", () => GuardTutorialWorkExit(() => SelectWorkspacePage("튜토리얼")));
            }
            tutorialWorkBar.style.display = tutorialWork == null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void DrawTutorialOverview()
        {
            inspector.Add(new Label("튜토리얼 · 플레이어에게 한 가지 행동씩 안내합니다.") { style = { whiteSpace = WhiteSpace.Normal } });
            Button(inspector, "tutorial-begin-work", "튜토리얼 만들기 / 수정하기", BeginTutorialWork);
            Button(inspector, "tutorial-sample-board-menu", "시험용 샘플 보드 만들기", () =>
            {
                VisualElement panel = OpenModal("새 레벨로 시험용 보드 만들기");
                var samples = TutorialSampleBoards.All.ToArray();
                DropdownField choice = new DropdownField("샘플", samples.Select(sample => sample.Title).ToList(), 0);
                panel.Add(choice);
                panel.Add(new Label("현재 레벨은 유지하고 샘플 사본을 새 레벨로 만듭니다. 파일 저장은 별도입니다.") { style = { whiteSpace = WhiteSpace.Normal } });
                Button(panel, "tutorial-create-sample-board", "새 레벨 만들기", () =>
                {
                    CloseModal(); Edit(() => TutorialDraftEditing.AddSampleBoard(Session, samples[choice.index].Id));
                });
                Button(panel, "cancel", "취소", CloseModal);
            });
            inspector.Add(new Label("작성 중에는 보드·공급을 함께 조정할 수 있습니다. 완료 또는 전체 취소 후 일반 작업으로 돌아갑니다.") { style = { whiteSpace = WhiteSpace.Normal } });
            try
            {
                using var graph = new AuthoringObjectGraph(Session);
                LevelDefinition level = (LevelDefinition)graph.Resolve(Session.SelectedLevelId);
                var tutorial = TutorialFlowResolver.Resolve(level);
                for (int i = 0; i < tutorial.steps.Count; i++)
                {
                    TutorialStepDefinition step = tutorial.steps[i];
                    inspector.Add(new Label((i + 1) + ". " + step.instructions + "\n" + string.Join(" · ", step.conditions.Select(condition => TutorialAuthoringRules.ConditionLabel(condition.kind))))
                        { style = { whiteSpace = WhiteSpace.Normal } });
                }
                if (tutorial.steps.Count == 0) inspector.Add(new Label("아직 튜토리얼이 없습니다. 만들기를 눌러 목적부터 선택하세요."));
            }
            catch (Exception error) { inspector.Add(new Label("연결 확인 필요: " + error.Message) { style = { whiteSpace = WhiteSpace.Normal } }); }
        }
    }
}
#endif
