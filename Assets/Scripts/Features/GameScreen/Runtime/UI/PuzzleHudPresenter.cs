using System;

namespace GameScreen
{
    /// <summary>연출 표시값을 수집하고 기존 HUD에 전달한다. 구독과 현재 버퍼 수명은 이 객체가 소유한다.</summary>
    public sealed class PuzzleHudPresenter : IDisposable
    {
        private PuzzleGameSession session;
        private PuzzleHudView view;
        public PuzzleHudState State { get; } = new PuzzleHudState();
        public PuzzleHudPresenter(PuzzleGameSession session, PuzzleHudView view)
        {
            this.session = session != null ? session : throw new ArgumentNullException(nameof(session));
            this.view = view != null ? view : throw new ArgumentNullException(nameof(view));
            session.Changed += Refresh;
            Refresh();
        }
        public void Refresh()
        {
            if (session == null || view == null) return;
            Capture(session, State);
            view.Refresh(State);
        }
        public void Frame()
        {
            if (session == null || view == null) return;
            Capture(session, State);
            view.Frame(State);
        }

        // 구형 호출부도 같은 수집 경계를 사용한다. 버퍼를 재사용해 프레임마다 배열/목록을 만들지 않는다.
        internal static PuzzleHudState Capture(PuzzleGameSession source, PuzzleHudState target)
        {
            bool ready = source.IsReady && !source.IsRestarting;
            target.BeginUpdate(source.State?.MovesRemaining, source.MovesPulse, ready, source.BoardCamera);
            if (!ready) return target;
            for (int i = 0; i < source.State.Missions.Count; i++)
            {
                Simulation.RuntimeMission mission = source.State.Missions[i];
                target.AddMission(new PuzzleHudMissionState(PuzzleMissionView.Name(mission.Definition.Kind),
                    source.DisplayedMissionProgress(i), mission.Target, source.MissionSprite(i), source.ProgressFeedback.Pulse(i)));
            }
            if (source.BoardCamera != null)
                for (int i = 0; i < source.ProgressFeedback.Flights.Count; i++)
                {
                    PuzzleProgressFeedback.Flight flight = source.ProgressFeedback.Flights[i];
                    target.AddFlight(new PuzzleHudFlightState(flight.Slot, flight.MissionIndex,
                        source.CollectionWorldPosition(flight.Source.Value), flight.Progress));
                }
            return target;
        }
        public void Dispose()
        {
            if (session != null) session.Changed -= Refresh;
            session = null;
            State.BeginUpdate(null, 0, false, null);
            if (view != null) { view.Refresh(State); view.Frame(State); }
            view = null;
        }
    }
}
