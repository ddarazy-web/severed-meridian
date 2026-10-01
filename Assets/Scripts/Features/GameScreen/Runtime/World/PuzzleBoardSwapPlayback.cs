using UnityEngine;

namespace GameScreen
{
    // 규칙 상태를 건드리지 않고 점유자 두 개의 표시만 이동한다.
    public sealed class PuzzleBoardSwapPlayback
    {
        private SpriteRenderer first, second;
        private Vector3 firstOrigin, secondOrigin, firstStart, secondStart, firstDelta, secondDelta;
        private int firstOrder, secondOrder;
        private float elapsed, duration;
        private bool returnTrip, rejected;
        public bool IsPlaying { get; private set; }

        public void Begin(SpriteRenderer a, Vector3 aOrigin, Vector3 aDelta,
            SpriteRenderer b, Vector3 bOrigin, Vector3 bDelta, float seconds, bool returns, bool reject)
        {
            Reset();
            first = a; second = b; firstOrigin = aOrigin; secondOrigin = bOrigin;
            firstDelta = aDelta; secondDelta = bDelta;
            if (first != null) { firstStart = first.transform.localPosition; firstOrder = first.sortingOrder; first.sortingOrder = PuzzleWorldBoard.SwipeSortingOrder; }
            if (second != null) { secondStart = second.transform.localPosition; secondOrder = second.sortingOrder; second.sortingOrder = PuzzleWorldBoard.SwipeSortingOrder - 1; }
            elapsed = 0; duration = Mathf.Max(0.01f, seconds); returnTrip = returns; rejected = reject;
            IsPlaying = true;
        }

        public bool Tick(float deltaTime)
        {
            if (!IsPlaying) return false;
            elapsed += Mathf.Max(0, deltaTime);
            float total = returnTrip && !rejected ? duration * 2 : duration;
            if (elapsed >= total) { Reset(); return true; }
            float t = Mathf.Clamp01(elapsed / duration);
            if (rejected)
            {
                if (first != null) first.transform.localPosition = Vector3.Lerp(firstStart, firstOrigin, t) + firstDelta * (Mathf.Sin(t * Mathf.PI) * 0.08f);
                return false;
            }
            bool returning = elapsed > duration;
            float eased = Mathf.SmoothStep(0, 1, returning ? (elapsed - duration) / duration : t);
            if (first != null) first.transform.localPosition = returning
                ? Vector3.Lerp(firstOrigin + firstDelta, firstOrigin, eased) : Vector3.Lerp(firstStart, firstOrigin + firstDelta, eased);
            if (second != null) second.transform.localPosition = returning
                ? Vector3.Lerp(secondOrigin + secondDelta, secondOrigin, eased) : Vector3.Lerp(secondStart, secondOrigin + secondDelta, eased);
            return false;
        }

        public void Reset()
        {
            if (first != null) { first.transform.localPosition = firstOrigin; first.sortingOrder = firstOrder; }
            if (second != null) { second.transform.localPosition = secondOrigin; second.sortingOrder = secondOrder; }
            first = second = null; IsPlaying = false; elapsed = 0;
        }

        // 손을 놓은 뒤에도 이동은 이어가고 표시 순서만 복원한다.
        public void ReleaseSorting()
        {
            if (first != null) first.sortingOrder = firstOrder;
            if (second != null) second.sortingOrder = secondOrder;
        }
    }
}
