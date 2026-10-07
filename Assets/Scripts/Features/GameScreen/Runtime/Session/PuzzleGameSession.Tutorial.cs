using Board;
using System;
using System.Linq;
using Levels;
using Simulation;
using Tutorial;

namespace GameScreen
{
    public sealed partial class PuzzleGameSession
    {
        private TutorialBoardAdapter tutorial;
        private TutorialProgressSnapshot tutorialFinalState;
        public TutorialProgressSnapshot TutorialState => tutorial?.Progress.Snapshot ?? tutorialFinalState;
        private bool TutorialActive => tutorial != null && tutorial.Progress.State != TutorialProgressState.Completed;
        private bool TutorialAllowsBoardInput => !TutorialActive || tutorial.Progress.State == TutorialProgressState.AwaitAction && !HasProgressFeedback;
        private bool TutorialPresentationReady => ready && !failed && !IsRestarting && !IsChangingLevel && !IsPresenting && !HasProgressFeedback && !IsStartingFeedback;

        private static void CheckTutorialReplay(LevelDefinition definition)
        {
            if (!definition.HasTutorial) return;
            System.Collections.Generic.List<LevelValidationIssue> issues = LevelTutorialReplayValidator.Validate(definition);
            if (issues.Count > 0) throw new InvalidOperationException(string.Join(" · ", issues.Select(issue => issue.ToString())));
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
            tutorial.Tick(TutorialPresentationReady, IsPaused || IsRestarting || IsChangingLevel || audioBackground || !isActiveAndEnabled);
            if (tutorial.Progress.State == TutorialProgressState.Error)
            { Fail("튜토리얼 처리 중단: " + tutorial.Progress.Message); return; }
            if (previousState != tutorial.Progress.State || previousStep != tutorial.Progress.StepIndex)
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
