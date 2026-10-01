using Board;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    public sealed partial class PuzzleGameSession
    {
        [SerializeField, Min(0.01f)] private float swapSeconds = 0.15f;
        private readonly PuzzleBoardSwapPlayback swapPlayback = new PuzzleBoardSwapPlayback();
        public bool IsPresenting => swapPlayback.IsPlaying;

        public bool TrySwap(BoardCoordinate first, BoardCoordinate second)
        {
            if (!CanAcceptInput) return false;
            ActionCandidate candidate = ActionQuery.Swap(State, first, second);
            SpriteRenderer a = board.OccupantAt(first), b = board.OccupantAt(second);
            Vector3 direction = PuzzleWorldBoard.CellPosition(second) - PuzzleWorldBoard.CellPosition(first);
            Vector3 worldDelta = board.transform.TransformVector(direction);
            BoardActionResult result = executor.Swap(first, second);
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
            if (swapPlayback.Tick(deltaTime)) Draw();
        }

        private void ResetPresentation()
        {
            swapPlayback.Reset();
            if (board != null) board.ClearPreview();
        }
    }
}
