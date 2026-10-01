using System;
using System.Collections.Generic;
using Board;
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
        public bool IsPresenting => swapPlayback.IsPlaying || removalPlayback.IsPlaying || settlementPlayback.IsPlaying;

        public bool TrySwap(BoardCoordinate first, BoardCoordinate second)
        {
            if (!CanAcceptInput) return false;
            ActionCandidate candidate = ActionQuery.Swap(State, first, second);
            presentationSnapshot = board.Capture();
            SpriteRenderer a = board.OccupantAt(first), b = board.OccupantAt(second);
            Vector3 direction = PuzzleWorldBoard.CellPosition(second) - PuzzleWorldBoard.CellPosition(first);
            Vector3 worldDelta = board.transform.TransformVector(direction);
            BoardActionResult result = executor.Swap(first, second);
            pendingSwap = result;
            Message = result.Message;
            bool returns = !result.IsApplied && candidate.Reason == ActionReason.NoNewMatch;
            bool rejected = !result.IsApplied && !returns;
            if (rejected) { b = null; worldDelta = board.transform.TransformVector(direction.normalized); }
            // 원래 그림과 미리보기 위치를 보존한다. 매칭 후 결과는 재생 종료 때만 그린다.
            swapPlayback.Begin(a, a != null ? board.OccupantOrigin(a) : Vector3.zero,
                a != null ? a.transform.parent.InverseTransformVector(worldDelta) : Vector3.zero,
                b, b != null ? board.OccupantOrigin(b) : Vector3.zero,
                b != null ? b.transform.parent.InverseTransformVector(-worldDelta) : Vector3.zero,
                swapSeconds, returns, rejected);
            board.ReleasePreview();
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
                        removalPlayback.Begin(presentationSnapshot, State, artwork, pendingSwap.Changes, removalSeconds);
                    }
                    pendingSwap = null;
                    if (removalPlayback.IsPlaying) return;
                    ResetPresentation(); Draw();
                }
                else if (removalPlayback.Tick(deltaTime) || settlementPlayback.Tick(deltaTime)) { ResetPresentation(); Draw(); }
            }
            catch (Exception error) { Fail("보드 연출 중단: " + error.Message); }
        }

        private void BeginRemoval(IEnumerable<MatchedBlockChange> changes)
        {
            try
            {
                removalPlayback.Begin(presentationSnapshot, State, artwork, changes, removalSeconds);
                if (!IsPresenting) { ResetPresentation(); Draw(); } else Changed?.Invoke();
            }
            catch (Exception error) { Fail("제거 연출 중단: " + error.Message); }
        }

        private void ResetPresentation()
        {
            swapPlayback.Reset();
            removalPlayback.Reset();
            settlementPlayback.Reset();
            presentationSnapshot?.Restore(); presentationSnapshot = null; pendingSwap = null;
            if (board != null) board.ClearPreview();
        }
    }
}
