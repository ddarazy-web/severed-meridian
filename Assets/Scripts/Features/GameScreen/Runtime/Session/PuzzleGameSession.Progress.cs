using System.Linq;
using Board;
using Levels;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    public sealed partial class PuzzleGameSession
    {
        public PuzzleProgressFeedback ProgressFeedback { get; } = new PuzzleProgressFeedback();
        private float startRemaining;
        private float movesRemaining;
        private int observedMoves;
        private int observedTurn;
        private float cascadeRemaining;
        private int observedCascadeRound;
        private bool lastPangFeedback;
        private float lastPangEnding;
        public float MovesPulse => movesRemaining / .15f;
        public float CascadePulse => cascadeRemaining / .3f;
        public bool IsStartingFeedback => startRemaining > 0;
        public bool HasProgressFeedback => ProgressFeedback.IsBusy || IsStartingFeedback || movesRemaining > 0 || cascadeRemaining > 0 || lastPangEnding > 0;
        public bool ResultReady => ready && !failed && !IsRestarting && Outcome != null && Phase == BoardActionPhase.Stopped && !IsPresenting && !HasProgressFeedback;
        public string FeedbackStatus => IsStartingFeedback ? "정리를 시작해요!" : lastPangFeedback ? "남은 파워를 정리해요!" : lastPangEnding > 0 ? "정리 완료!" : cascadeRemaining > 0 ? (observedCascadeRound + 1) + " 연쇄!" : null;
        private void BeginStartFeedback() { startRemaining = .65f; EmitAudio(PuzzleFeedbackCueKind.Start); Changed?.Invoke(); }
        public int DisplayedMissionProgress(int index) => index < ProgressFeedback.Display.Count
            ? ProgressFeedback.Display.Progress(index) : State.Missions[index].Progress;
        public Vector3 CollectionWorldPosition(BoardCoordinate source) => board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(source));
        private void InitializeProgress()
        { startRemaining = 0; movesRemaining = 0; cascadeRemaining = 0; observedCascadeRound = 0; lastPangFeedback = false; lastPangEnding = 0; observedMoves = State.MovesRemaining; observedTurn = executor.Turn; ProgressFeedback.Initialize(State); InitializeAudio(); }
        private void ClearProgress()
        { startRemaining = 0; movesRemaining = 0; cascadeRemaining = 0; observedCascadeRound = 0; lastPangFeedback = false; lastPangEnding = 0; ProgressFeedback.Clear(); ClearAudio(); }
        private void ObserveMoves()
        {
            // 이동을 소비하지 않는 아이템도 새 행동의 연쇄 라운드를 시작한다.
            if (executor.Turn != observedTurn) { observedCascadeRound = 0; cascadeRemaining = 0; }
            if (State.MovesRemaining < observedMoves)
            { movesRemaining = .15f; }
            observedMoves = State.MovesRemaining;
            observedTurn = executor.Turn;
        }
        private void ObserveCascadeStep(CascadeStepResult step)
        {
            if (!lastPangFeedback && Phase == BoardActionPhase.WaitingForLastPang) observedCascadeRound = 0;
            if (step.Reason == CascadeStepReason.Matched && step.Round > observedCascadeRound)
            { observedCascadeRound = step.Round; cascadeRemaining = .3f; }
            if (step.Reason == CascadeStepReason.LastPang || Phase == BoardActionPhase.WaitingForLastPang) lastPangFeedback = true;
            if (step.Reason == CascadeStepReason.LastPangComplete)
            { lastPangFeedback = false; lastPangEnding = .3f; }
        }
        private void TickProgress(float deltaTime)
        {
            if (!ready || failed || IsPaused || IsRestarting) return;
            TickAudio(deltaTime);
            bool wasBusy = HasProgressFeedback;
            startRemaining = Mathf.Max(0, startRemaining - deltaTime);
            movesRemaining = Mathf.Max(0, movesRemaining - deltaTime);
            cascadeRemaining = Mathf.Max(0, cascadeRemaining - deltaTime);
            lastPangEnding = Mathf.Max(0, lastPangEnding - deltaTime);
            ProgressFeedback.Tick(deltaTime);
            ObserveMissionAudio();
            if (wasBusy || ProgressFeedback.IsBusy) Changed?.Invoke();
        }

        private void ScheduleProgress(PuzzleEffectTimeline timeline)
        {
            ProgressFeedback.Schedule(State, record =>
            {
                if (!record.Source.HasValue) return null;
                BoardCoordinate source = record.Source.Value;
                if (record.BodyIndex.HasValue)
                {
                    var removals = timeline.Reactions.Where(reaction => reaction.Record.RemovedObstacleIndices.Contains(record.BodyIndex.Value)).ToArray();
                    return removals.Length > 0 ? removals.Max(reaction => reaction.Time) : (float?)null;
                }
                MissionKind kind = State.Missions[record.MissionIndex].Definition.Kind;
                if (kind == MissionKind.Web && timeline.Changes.Any(change => change.Coordinate.Equals(source) && change.CoverBefore > 0 && change.CoverAfter == 0))
                    return .12f;
                if ((kind == MissionKind.Color || kind == MissionKind.Dust) && timeline.Combination?.Transformations.Any(change => change.Coordinate.Equals(source)) == true)
                    return .35f;
                if ((kind == MissionKind.Color || kind == MissionKind.Dust) && timeline.Changes.Any(change => change.IsConsumed && change.Coordinate.Equals(source)))
                    return .12f;
                var reactions = timeline.Reactions.Where(reaction => reaction.Record.Target.Equals(source) &&
                    (kind == MissionKind.Color || kind == MissionKind.Dust ? reaction.Record.Response == DamageResponse.Remove : reaction.Record.Response == DamageResponse.CoverDamage)).ToArray();
                return reactions.Length > 0 ? reactions.Max(reaction => reaction.Time) : (float?)null;
            });
        }
    }
}
