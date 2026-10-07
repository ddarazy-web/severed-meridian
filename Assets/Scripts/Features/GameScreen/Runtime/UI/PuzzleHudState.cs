using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace GameScreen
{
    public readonly struct PuzzleHudMissionState
    {
        public string Name { get; }
        public int Progress { get; }
        public int Target { get; }
        public Sprite Sprite { get; }
        public float Pulse { get; }
        public PuzzleHudMissionState(string name, int progress, int target, Sprite sprite, float pulse)
        { Name = name; Progress = progress; Target = target; Sprite = sprite; Pulse = pulse; }
    }

    public readonly struct PuzzleHudFlightState
    {
        public int Slot { get; }
        public int MissionIndex { get; }
        public Vector3 Origin { get; }
        public float Progress { get; }
        public PuzzleHudFlightState(int slot, int missionIndex, Vector3 origin, float progress)
        { Slot = slot; MissionIndex = missionIndex; Origin = origin; Progress = progress; }
    }

    /// <summary>화면이 읽는 현재 표시 버퍼. Presenter만 갱신하며 실행 객체나 수집 예약을 노출하지 않는다.</summary>
    public sealed class PuzzleHudState
    {
        private readonly List<PuzzleHudMissionState> missions = new List<PuzzleHudMissionState>(4);
        private readonly List<PuzzleHudFlightState> flights = new List<PuzzleHudFlightState>(8);
        public ReadOnlyCollection<PuzzleHudMissionState> Missions { get; }
        public ReadOnlyCollection<PuzzleHudFlightState> Flights { get; }
        public int? MovesRemaining { get; private set; }
        public string MovesText { get; private set; } = "—";
        public float MovesPulse { get; private set; }
        public bool IsReady { get; private set; }
        public Camera BoardCamera { get; private set; }
        public PuzzleHudState() { Missions = missions.AsReadOnly(); Flights = flights.AsReadOnly(); }

        internal void BeginUpdate(int? moves, float pulse, bool ready, Camera camera)
        {
            if (MovesRemaining != moves) { MovesRemaining = moves; MovesText = moves?.ToString() ?? "—"; }
            MovesPulse = pulse; IsReady = ready; BoardCamera = camera;
            missions.Clear(); flights.Clear();
        }
        internal void AddMission(PuzzleHudMissionState value) => missions.Add(value);
        internal void AddFlight(PuzzleHudFlightState value) => flights.Add(value);
    }
}
