using System.Collections.Generic;
using System.Collections.ObjectModel;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    public enum DroneFlightPhaseKind { Rise, Hover, Dash }

    /// <summary>실제 예약·효과 시간에서 만든 표시 구간이다. 목표 선택이나 실행 상태를 수정하지 않는다.</summary>
    public sealed class DroneFlightMotion
    {
        public sealed class Phase
        {
            public DroneFlightPhaseKind Kind { get; }
            public float Start { get; }
            public float End { get; }
            public Vector3 From { get; }
            public Vector3 To { get; }
            public bool Interrupted { get; }
            private readonly float pathDuration;
            private readonly Vector3 control1, control2;

            internal Phase(DroneFlightPhaseKind kind, float start, float end, Vector3 from, Vector3 to, int lane = 0,
                float duration = 0, bool interrupted = false)
            {
                Kind = kind; Start = start; End = end; From = from; To = to; Interrupted = interrupted;
                pathDuration = duration > 0 ? duration : end - start;
                Vector3 heading = (to - from).normalized;
                Vector3 perpendicular = new Vector3(-heading.y, heading.x, 0);
                float bend = Mathf.Clamp(Vector3.Distance(from, to) * .18f, .35f, .85f) * (lane % 2 == 0 ? 1 : -1);
                control1 = from + heading * .25f + perpendicular * bend;
                control2 = to - heading * .35f + perpendicular * bend * .6f;
            }

            public Vector3 PositionAt(float time)
            {
                if (Kind == DroneFlightPhaseKind.Hover) return From;
                float t = Mathf.Clamp01((time - Start) / pathDuration);
                if (Kind == DroneFlightPhaseKind.Rise) return Vector3.Lerp(From, To, Mathf.SmoothStep(0, 1, t));
                float u = t * t, remaining = 1 - u;
                return remaining * remaining * remaining * From + 3 * remaining * remaining * u * control1 +
                    3 * remaining * u * u * control2 + u * u * u * To;
            }
        }

        public DroneFlightRecord Record { get; }
        public ReadOnlyCollection<Phase> Phases { get; }
        public float Ready { get; }
        public float End => Phases[Phases.Count - 1].End;
        internal static float HoverDelay(int request) => (request * 73 % 251) * .001f;

        internal DroneFlightMotion(DroneFlightRecord record, float begin, float finalDeparture, float finalDuration, int lane, int siblings, float[] retargetTimes)
        {
            Record = record;
            Vector3 origin = PuzzleWorldBoard.CellPosition(record.Origin);
            Vector3 departure = origin + new Vector3(lane - (siblings - 1) * .5f, .65f, 0);
            Ready = begin + .85f + HoverDelay(record.Request);
            List<Phase> phases = new List<Phase>
            {
                new Phase(DroneFlightPhaseKind.Rise, begin, begin + .5f, origin, departure)
            };
            float cursor = begin + .5f;
            for (int index = 0; index < record.Retargets.Count; index++)
            {
                float lostAt = retargetTimes[index];
                if (lostAt <= cursor) continue;
                Vector3 previousTarget = PuzzleWorldBoard.CellPosition(record.Retargets[index].LostTarget);
                float duration = Mathf.Clamp(Vector3.Distance(departure, previousTarget) * .045f, .18f, .38f);
                float earliest = index == 0 ? Ready : Mathf.Max(Ready, cursor + .2f);
                float launch = Mathf.Max(earliest, lostAt - duration * .6f);
                if (launch < lostAt)
                {
                    if (launch > cursor) phases.Add(new Phase(DroneFlightPhaseKind.Hover, cursor, launch, departure, departure));
                    Phase interrupted = new Phase(DroneFlightPhaseKind.Dash, launch, lostAt, departure, previousTarget, lane, duration, true);
                    phases.Add(interrupted);
                    departure = interrupted.PositionAt(lostAt); cursor = lostAt;
                }
                else
                {
                    // 초기 상승이나 재탐색 호버 중 사라진 목표에는 허위 돌진을 만들지 않는다.
                    phases.Add(new Phase(DroneFlightPhaseKind.Hover, cursor, lostAt, departure, departure));
                    cursor = lostAt;
                }
            }
            if (finalDeparture > cursor)
                phases.Add(new Phase(DroneFlightPhaseKind.Hover, cursor, finalDeparture, departure, departure));
            if (record.LandingTarget.HasValue)
                phases.Add(new Phase(DroneFlightPhaseKind.Dash, finalDeparture, finalDeparture + finalDuration, departure,
                    PuzzleWorldBoard.CellPosition(record.LandingTarget.Value), lane));
            Phases = phases.AsReadOnly();
        }

        public Vector3 PositionAt(float time)
        {
            foreach (Phase phase in Phases)
                if (time < phase.End) return phase.PositionAt(time);
            return Phases[Phases.Count - 1].PositionAt(End);
        }
    }
}
