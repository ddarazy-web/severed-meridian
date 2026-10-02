using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    // 표시 시간만 진행한다. 수집 대기는 보드 연쇄의 재생 상태와 분리한다.
    public sealed class PuzzleProgressFeedback
    {
        public sealed class Flight
        {
            public int MissionIndex { get; internal set; }
            public int Amount { get; internal set; }
            public BoardCoordinate? Source { get; internal set; }
            public int Slot { get; internal set; }
            public float Progress => Mathf.Clamp01(Elapsed / .32f);
            internal float Starts, Elapsed;
        }

        private readonly List<Flight> pending = new List<Flight>();
        private readonly List<Flight> active = new List<Flight>();
        private float[] pulses = Array.Empty<float>();
        private int[] targets = Array.Empty<int>(), completions = Array.Empty<int>();
        private float clock;
        public PuzzleMissionDisplay Display { get; private set; } = new PuzzleMissionDisplay();
        public ReadOnlyCollection<Flight> Flights { get; }
        public bool IsBusy => pending.Count > 0 || active.Count > 0 || pulses.Any(pulse => pulse > 0);
        public PuzzleProgressFeedback() { Flights = active.AsReadOnly(); }

        public void Initialize(LevelRuntimeState state)
        {
            Clear(); Display.Initialize(state);
            targets = state.Missions.Select(mission => mission.Target).ToArray();
            pulses = new float[targets.Length]; completions = new int[targets.Length];
        }

        // 같은 실행 결과의 한 미션을 묶는다. 마지막 실제 원점에서 출발하며 전체 증가량을 보존한다.
        public void Schedule(LevelRuntimeState state, Func<MissionProgressRecord, float?> timeFor)
        {
            foreach (IGrouping<int, MissionProgressRecord> group in Display.Collect(state).GroupBy(record => record.MissionIndex))
            {
                var records = group.Select(record => (record, time: timeFor(record))).ToArray();
                bool known = records.All(item => item.time.HasValue && item.record.Source.HasValue);
                var last = records.OrderBy(item => item.time ?? 0).Last();
                pending.Add(new Flight { MissionIndex = group.Key, Amount = group.Sum(record => record.Amount),
                    Source = known ? last.record.Source : null, Slot = -1,
                    Starts = clock + records.Max(item => Mathf.Max(0, item.time ?? 0)) });
            }
        }

        public void Tick(float deltaTime)
        {
            float delta = Mathf.Max(0, deltaTime); clock += delta;
            for (int i = 0; i < pulses.Length; i++) pulses[i] = Mathf.Max(0, pulses[i] - delta);
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Flight flight = active[i]; flight.Elapsed += delta;
                if (flight.Elapsed < .32f) continue;
                Arrive(flight); active.RemoveAt(i);
            }
            for (int i = 0; i < pending.Count;)
            {
                Flight flight = pending[i];
                if (flight.Starts > clock) { i++; continue; }
                if (!flight.Source.HasValue) { Arrive(flight); pending.RemoveAt(i); continue; }
                if (active.Count >= 8) { i++; continue; }
                int slot = 0;
                while (active.Any(other => other.Slot == slot)) slot++;
                flight.Slot = slot; active.Add(flight); pending.RemoveAt(i);
            }
        }

        private void Arrive(Flight flight)
        {
            int index = flight.MissionIndex, before = Display.Progress(index);
            Display.Arrive(index, flight.Amount); pulses[index] = .15f;
            if (before < targets[index] && Display.Progress(index) >= targets[index]) completions[index]++;
        }

        public float Pulse(int index) => index < pulses.Length ? pulses[index] / .15f : 0;
        public int CompletionCount(int index) => completions[index];
        public void Clear()
        {
            pending.Clear(); active.Clear(); clock = 0;
            pulses = Array.Empty<float>(); targets = Array.Empty<int>(); completions = Array.Empty<int>();
            Display = new PuzzleMissionDisplay();
        }
    }
}
