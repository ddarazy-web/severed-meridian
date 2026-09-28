using System.Collections.ObjectModel;
using System.Linq;

namespace Simulation
{
    public enum BoardOutcomeKind { Playing, Won, MovesExhausted, Blocked, Aborted }

    // 라스트팡이 보드와 미션을 바꾸더라도 성공 시점의 결과는 유지한다.
    public sealed class BoardOutcome
    {
        public BoardOutcomeKind Kind { get; }
        public string Message { get; }
        public int Turn { get; }
        public int MovesRemaining { get; }
        public ReadOnlyCollection<int> MissionProgress { get; }
        public ReadOnlyCollection<int> MissionTargets { get; }
        internal BoardOutcome(BoardOutcomeKind kind, string message, LevelRuntimeState state, int turn)
        {
            Kind = kind; Message = message; Turn = turn; MovesRemaining = state.MovesRemaining;
            MissionProgress = System.Array.AsReadOnly(state.Missions.Select(m => m.Progress).ToArray());
            MissionTargets = System.Array.AsReadOnly(state.Missions.Select(m => m.Target).ToArray());
        }
    }
}
