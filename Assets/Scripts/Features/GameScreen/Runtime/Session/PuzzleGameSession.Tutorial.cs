using Board;
using System;
using System.Linq;
using Levels;
using Simulation;
using Tutorial;
using UnityEngine;

namespace GameScreen
{
    public sealed partial class PuzzleGameSession
    {
        private TutorialBoardAdapter tutorial;
        private TutorialProgressSnapshot tutorialFinalState;
        private TutorialExecutionContext tutorialContext;
        private LevelTutorialDefinition completionTutorial;
        private bool tutorialCompletionRecorded;
        public bool CanShowTutorial => ready && !failed && !IsStartingFeedback && !IsRestarting && !IsChangingLevel && !IsPaused && !audioBackground;
        public Vector3 TutorialCellWorldPosition(BoardCoordinate at) => board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(at));
        public Vector3 TutorialCellWorldOffset => board.transform.TransformVector(new Vector3(1, 1, 0));
        public void ConfigureTutorial(TutorialExecutionContext context)
        {
            if (started) throw new InvalidOperationException("튜토리얼 실행 정책은 시작 전에 전달하세요.");
            tutorialContext = context ?? throw new ArgumentNullException(nameof(context));
        }
        private bool ShouldRunTutorial(LevelDefinition definition)
        {
            tutorialContext ??= TutorialExecutionContext.CreatePlayer();
            completionTutorial = new LevelTutorialDefinition { completionId = definition.Tutorial?.completionId };
            return tutorialContext.ShouldRun(definition);
        }
        public TutorialProgressSnapshot TutorialState => tutorial?.Progress.Snapshot ?? tutorialFinalState;
        private bool TutorialActive => tutorial != null && tutorial.Progress.State != TutorialProgressState.Completed;
        private bool TutorialAllowsBoardInput => !TutorialActive || tutorial.Progress.State == TutorialProgressState.AwaitAction && !HasProgressFeedback;
        private bool TutorialPresentationReady => ready && !failed && !IsRestarting && !IsChangingLevel && !IsPresenting && !HasProgressFeedback && !IsStartingFeedback;

        private static bool CheckTutorialReplay(LevelDefinition definition)
        {
            if (!definition.HasTutorial) return false;
            System.Collections.Generic.List<LevelValidationIssue> issues = LevelTutorialReplayValidator.Validate(definition);
            if (issues.Count == 0) return true;
            string diagnostic = string.Join(" · ", issues.Select(issue => issue.ToString()));
            if (issues.Any(issue => issue.Code != LevelValidationCode.InvalidTutorial)) throw new InvalidOperationException(diagnostic);
            Debug.LogWarning("튜토리얼 안내 생략 · " + diagnostic);
            return false;
        }

        public bool CanSelectBlock(BoardCoordinate coordinate)
            => CanAcceptInput && new BoardQueryView(State).Contains(coordinate) && State.CellAt(coordinate).IsActive &&
                (!TutorialActive || ActionQuery.Movable(State, State.CellAt(coordinate)) && tutorial.CanSelectBlock(coordinate));

        public bool CanSelectItem(BoardItem item)
            => CanUseItems && (!TutorialActive || tutorial.CanSelectItem(item));

        public bool CanPreviewSwap(BoardCoordinate first, BoardCoordinate second)
            => CanAcceptInput && (!TutorialActive || tutorial.Progress.CanApprove(TutorialInput.Swap(first, second)));

        public bool CanActivateBlock(BoardCoordinate coordinate)
            => CanAcceptInput && (!TutorialActive || tutorial.Progress.CanApprove(TutorialInput.Activate(coordinate)));

        public bool TryAdvanceTutorial()
        {
            if (!isActiveAndEnabled || !TutorialPresentationReady || IsPaused || audioBackground || !TutorialActive || executor.HasPendingCascade ||
                !tutorial.TryBegin(TutorialInput.Next())) return false;
            TickTutorial();
            Changed?.Invoke();
            return true;
        }

        private bool TryBeginTutorial(TutorialInput input) => !TutorialActive || tutorial.TryBegin(input);

        private void TickTutorial()
        {
            if (tutorial == null || failed) return;
            TutorialProgressState previousState = tutorial.Progress.State;
            int previousStep = tutorial.Progress.StepIndex;
            int previousGuidance = tutorial.Progress.GuidanceRevision;
            LevelRuntimeState previousBoard = executor.State;
            tutorial.Tick(TutorialPresentationReady, IsPaused || IsRestarting || IsChangingLevel || audioBackground || !isActiveAndEnabled);
            if (!ReferenceEquals(previousBoard, executor.State))
            {
                Draw();
                if (failed) return;
            }
            if (!tutorialCompletionRecorded && tutorial.Progress.State == TutorialProgressState.Completed && TutorialPresentationReady && !IsPaused)
            {
                tutorialContext?.Complete(State.LevelNumber, completionTutorial);
                tutorialCompletionRecorded = true;
            }
            if (tutorial.Progress.State == TutorialProgressState.Error || tutorial.Progress.State == TutorialProgressState.Cancelled)
            {
                if (tutorial.IsReleased)
                {
                    DisposeTutorial();
                    Message = executor.Outcome?.Message ?? "튜토리얼 안내를 종료했습니다. 계속 플레이하세요.";
                    Changed?.Invoke();
                }
                else if (TutorialPresentationReady && !IsPaused && !audioBackground && executor.Phase == BoardActionPhase.Stopped)
                    Fail("튜토리얼 처리 중단: " + tutorial.Progress.Message);
                return;
            }
            if (previousState != tutorial.Progress.State || previousStep != tutorial.Progress.StepIndex || previousGuidance != tutorial.Progress.GuidanceRevision)
            { Message = tutorial.Progress.Message; Changed?.Invoke(); }
        }

        private void DisposeTutorial()
        {
            if (tutorial == null) return;
            tutorialFinalState = tutorial.Progress.Snapshot;
            tutorial.Dispose(); tutorial = null;
        }
    }
}
