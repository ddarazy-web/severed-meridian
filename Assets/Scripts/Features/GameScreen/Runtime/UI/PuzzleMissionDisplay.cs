using System;
using System.Collections.ObjectModel;
using System.Linq;
using Simulation;

namespace GameScreen
{
    // 규칙 진행과 별도로 도착한 수집량만 표시한다. 보드 규칙을 실행하지 않는다.
    public sealed class PuzzleMissionDisplay
    {
        private int[] progress = Array.Empty<int>();
        private int[] targets = Array.Empty<int>();
        private int consumedRecords;
        public int Count => progress.Length;

        public void Initialize(LevelRuntimeState state)
        {
            progress = state.Missions.Select(mission => mission.Progress).ToArray();
            targets = state.Missions.Select(mission => mission.Target).ToArray();
            consumedRecords = state.MissionProgressRecords.Count;
        }

        public ReadOnlyCollection<MissionProgressRecord> Collect(LevelRuntimeState state)
        {
            MissionProgressRecord[] added = state.MissionProgressRecords.Skip(consumedRecords).ToArray();
            consumedRecords = state.MissionProgressRecords.Count;
            return Array.AsReadOnly(added);
        }

        public int Progress(int index) => progress[index];
        public void Arrive(int index, int amount)
        { progress[index] = Math.Min(targets[index], progress[index] + amount); }
    }
}
