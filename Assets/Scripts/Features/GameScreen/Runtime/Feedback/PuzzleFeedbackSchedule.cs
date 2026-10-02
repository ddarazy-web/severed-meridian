using System;
using System.Collections.Generic;

namespace GameScreen
{
    // 실제 표시 시간에 예약한다. 저프레임에서도 예약 시각으로 중복을 판정한다.
    public sealed class PuzzleFeedbackSchedule
    {
        private readonly List<(PuzzleFeedbackCueKind kind, float at, int order)> pending = new List<(PuzzleFeedbackCueKind, float, int)>();
        private readonly List<PuzzleFeedbackCueKind> emitted = new List<PuzzleFeedbackCueKind>();
        private readonly float[] last = new float[Enum.GetValues(typeof(PuzzleFeedbackCueKind)).Length];
        private float clock;
        private int sequence;
        public int PendingCount => pending.Count;
        public PuzzleFeedbackSchedule() { Clear(); }
        public void Schedule(PuzzleFeedbackCueKind kind, float at)
            => pending.Add((kind, clock + Math.Max(0, at), sequence++));
        public IReadOnlyList<PuzzleFeedbackCueKind> Tick(float deltaTime)
        {
            clock += Math.Max(0, deltaTime); emitted.Clear();
            pending.Sort((a, b) => a.at != b.at ? a.at.CompareTo(b.at) : a.order.CompareTo(b.order));
            int consumed = 0;
            foreach (var cue in pending)
            {
                if (cue.at > clock) break;
                consumed++;
                int index = (int)cue.kind;
                if (cue.at - last[index] < .06f) continue;
                last[index] = cue.at; emitted.Add(cue.kind);
            }
            pending.RemoveRange(0, consumed);
            return emitted;
        }
        public void Clear()
        {
            pending.Clear(); emitted.Clear(); clock = 0; sequence = 0;
            for (int i = 0; i < last.Length; i++) last[i] = float.NegativeInfinity;
        }
    }
}
