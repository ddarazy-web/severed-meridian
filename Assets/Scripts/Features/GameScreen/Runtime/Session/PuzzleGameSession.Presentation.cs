using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    public sealed partial class PuzzleGameSession
    {
        [SerializeField, Min(0.01f)] private float swapSeconds = 0.15f;
        private readonly PuzzleBoardSwapPlayback swapPlayback = new PuzzleBoardSwapPlayback();
        [SerializeField, Min(0.01f)] private float removalSeconds = 0.12f;
        private readonly PuzzleBoardRemovalPlayback removalPlayback = new PuzzleBoardRemovalPlayback();
        [SerializeField, Min(0.01f)] private float fallSeconds = .12f, supplySeconds = .16f, landingSeconds = .06f;
        private readonly PuzzleBoardSettlementPlayback settlementPlayback = new PuzzleBoardSettlementPlayback();
        private PuzzleBoardSnapshot presentationSnapshot;
        private BoardActionResult pendingSwap;
        private LevelRuntimeState presentationBefore;
        private PuzzlePowerPlayback powerPlayback;
        private CancellationTokenSource effectLoad;
        private bool preparingEffects;
        public bool IsPresenting => preparingEffects || powerPlayback?.IsPlaying == true || swapPlayback.IsPlaying || removalPlayback.IsPlaying || settlementPlayback.IsPlaying;

        public void EndSwipePreview()
        { board.ClearPreview(); swapPlayback.ReleaseSorting(); }

        public bool TrySwap(BoardCoordinate first, BoardCoordinate second)
        {
            if (!CanAcceptInput || !TryBeginTutorial(Tutorial.TutorialInput.Swap(first, second))) return false;
            ActionCandidate candidate = ActionQuery.Swap(State, first, second);
            CapturePresentation();
            SpriteRenderer a = board.OccupantAt(first), b = board.OccupantAt(second);
            Vector3 direction = PuzzleWorldBoard.CellPosition(second) - PuzzleWorldBoard.CellPosition(first);
            Vector3 worldDelta = board.transform.TransformVector(direction);
            BoardActionResult result = executor.Swap(first, second);
            tutorial?.ReportAction(result.IsApplied);
            ObserveMoves();
            pendingSwap = result;
            Message = result.Message;
            bool returns = !result.IsApplied && candidate.Reason == ActionReason.NoNewMatch;
            bool rejected = !result.IsApplied && !returns;
            if (rejected) { b = null; worldDelta = board.transform.TransformVector(direction.normalized); }
            // 원래 그림과 미리보기 위치를 보존한다. 매칭 후 결과는 재생 종료 때만 그린다.
            Vector3 aOrigin = a != null ? board.OccupantOrigin(a) : Vector3.zero;
            Vector3 bOrigin = b != null ? board.OccupantOrigin(b) : Vector3.zero;
            board.ReleasePreview();
            swapPlayback.Begin(a, aOrigin,
                a != null ? a.transform.parent.InverseTransformVector(worldDelta) : Vector3.zero,
                b, bOrigin,
                b != null ? b.transform.parent.InverseTransformVector(-worldDelta) : Vector3.zero,
                swapSeconds, returns, rejected);
            if (result.IsApplied) EmitAudio(PuzzleFeedbackCueKind.Swap);
            else if (returns) ScheduleAudio(PuzzleFeedbackCueKind.InvalidSwap, swapSeconds);
            Changed?.Invoke();
            return result.IsApplied;
        }

        private void AdvancePresentation(float deltaTime)
        {
            if (!ready || failed || IsPaused || IsRestarting) return;
            try
            {
                if (swapPlayback.IsPlaying)
                {
                    if (!swapPlayback.Tick(deltaTime)) return;
                    if (pendingSwap.IsApplied)
                    {
                        presentationSnapshot.Swap(pendingSwap.First, pendingSwap.Second);
                        SwapPresentationState(pendingSwap.First, pendingSwap.Second);
                        BoardActionResult result = pendingSwap; pendingSwap = null;
                        BeginEffects(result.Changes, result.Effects, result.PowerTrace);
                        return;
                    }
                    pendingSwap = null;
                    if (removalPlayback.IsPlaying) return;
                    ResetPresentation(); Draw();
                }
                else if (!preparingEffects && (powerPlayback?.Tick(deltaTime) == true || removalPlayback.Tick(deltaTime) || settlementPlayback.Tick(deltaTime))) { ResetPresentation(); Draw(); }
            }
            catch (Exception error) { Fail("보드 연출 중단: " + error.Message); }
        }

        private void CapturePresentation()
        {
            presentationSnapshot = board.Capture();
            presentationBefore = new LevelRuntimeState(State);
        }

        // 교환 연출 종료 시점의 점유자만 옮긴다. 덮개·바닥은 원래 칸에 남긴다.
        private void SwapPresentationState(BoardCoordinate first, BoardCoordinate second)
        {
            RuntimeCell a = presentationBefore.CellAt(first), b = presentationBefore.CellAt(second);
            Elements.ElementDefinition firstElement = a.ContentElement, secondElement = b.ContentElement;
            (a.Content, b.Content) = (b.Content, a.Content);
            (a.ContentElement, b.ContentElement) = (secondElement, firstElement);
            (a.Color, b.Color) = (b.Color, a.Color);
            (a.RocketDirection, b.RocketDirection) = (b.RocketDirection, a.RocketDirection);
            (a.ObstacleIndex, b.ObstacleIndex) = (b.ObstacleIndex, a.ObstacleIndex);
        }

        private void BeginEffects(IEnumerable<MatchedBlockChange> changes, IEnumerable<EffectRecord> effects, PowerPresentationTrace trace)
        {
            PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(presentationBefore, changes, effects, trace);
            if (timeline.Duration <= 0) { BeginRemoval(changes); return; }
            PrepareEffectsAsync(timeline).Forget(Debug.LogException);
        }

        private async UniTask PrepareEffectsAsync(PuzzleEffectTimeline timeline)
        {
            CancellationTokenSource load = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            effectLoad = load; preparingEffects = true;
            PuzzlePowerPlayback playback = new PuzzlePowerPlayback();
            try
            {
                Changed?.Invoke();
                await playback.PrepareAsync(presentationBefore, State, artwork, timeline, load.Token);
                load.Token.ThrowIfCancellationRequested();
                // 이전 세션의 늦은 로드 완료가 재시작한 보드의 소유권을 가져오지 않는다.
                if (effectLoad != load) return;
                presentationSnapshot?.Restore(); presentationSnapshot = null;
                powerPlayback = playback; powerPlayback.Begin(board);
                ScheduleProgress(timeline);
                foreach (PuzzleFeedbackCue cue in playback.AudioCues) ScheduleAudio(cue.Kind, cue.Time);
                preparingEffects = false; Changed?.Invoke();
            }
            catch (OperationCanceledException) { playback.Reset(); }
            catch (Exception error) { if (effectLoad == load) Fail("파워 연출 준비 실패: " + error.Message); }
            finally
            {
                if (effectLoad == load) { effectLoad = null; preparingEffects = false; }
                load.Dispose();
            }
        }

        private void BeginRemoval(IEnumerable<MatchedBlockChange> changes)
        {
            try
            {
                removalPlayback.Begin(presentationSnapshot, State, artwork, changes, removalSeconds);
                if (changes.Any(change => change.IsConsumed)) ScheduleAudio(PuzzleFeedbackCueKind.Match, removalSeconds);
                ProgressFeedback.Schedule(State, record => record.Source.HasValue && changes.Any(change => change.IsConsumed && change.Coordinate.Equals(record.Source.Value)) ? removalSeconds : (float?)null);
                if (!IsPresenting) { ResetPresentation(); Draw(); } else Changed?.Invoke();
            }
            catch (Exception error) { Fail("제거 연출 중단: " + error.Message); }
        }

        private void ResetPresentation()
        {
            effectLoad?.Cancel(); effectLoad = null; preparingEffects = false;
            powerPlayback?.Reset(); powerPlayback = null; presentationBefore = null;
            swapPlayback.Reset();
            removalPlayback.Reset();
            settlementPlayback.Reset();
            presentationSnapshot?.Restore(); presentationSnapshot = null; pendingSwap = null;
            if (board != null) board.ClearPreview();
        }
    }
}
