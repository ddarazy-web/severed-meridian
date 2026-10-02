using System.Linq;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    public sealed partial class PuzzleGameSession
    {
        [SerializeField] private PuzzleAudioPlayback audioPrefab;
        [SerializeField] private bool soundEnabled = true;
        private readonly PuzzleFeedbackSchedule audioSchedule = new PuzzleFeedbackSchedule();
        private PuzzleAudioPlayback audioPlayback;
        private int[] audioMissionProgress = System.Array.Empty<int>(), audioMissionCompletions = System.Array.Empty<int>();
        private BoardOutcome audioOutcome;
        private bool audioBackground;
        public PuzzleAudioPlayback AudioPlayback => audioPlayback;
        public bool SoundEnabled
        {
            get => soundEnabled;
            set { soundEnabled = value; audioSchedule.Clear(); if (audioPlayback != null) audioPlayback.SoundEnabled = value; }
        }
        public void ConfigureAudio(PuzzleAudioPlayback prefab) => audioPrefab = prefab;
        private void InitializeAudio()
        {
            audioSchedule.Clear(); audioOutcome = null;
            if (audioPlayback == null && audioPrefab != null) audioPlayback = Instantiate(audioPrefab, transform);
            if (audioPlayback != null) { audioPlayback.Initialize(); audioPlayback.StopAndClear(); audioPlayback.SoundEnabled = soundEnabled; }
            audioMissionProgress = State.Missions.Select((mission, index) => DisplayedMissionProgress(index)).ToArray();
            audioMissionCompletions = new int[audioMissionProgress.Length];
        }
        private void EmitAudio(PuzzleFeedbackCueKind kind)
        {
            if (audioBackground) return;
            ScheduleAudio(kind, 0);
            foreach (PuzzleFeedbackCueKind cue in audioSchedule.Tick(0)) audioPlayback?.Play(cue);
        }
        private void ScheduleAudio(PuzzleFeedbackCueKind kind, float delay)
        { if (soundEnabled && !audioBackground) audioSchedule.Schedule(kind, delay); }
        private void TickAudio(float deltaTime)
        {
            if (audioBackground) return;
            audioPlayback?.Tick(deltaTime);
            foreach (PuzzleFeedbackCueKind cue in audioSchedule.Tick(deltaTime)) audioPlayback?.Play(cue);
        }
        private void ObserveMissionAudio()
        {
            bool arrived = false, complete = false;
            for (int index = 0; index < audioMissionProgress.Length; index++)
            {
                int progress = DisplayedMissionProgress(index), completed = ProgressFeedback.CompletionCount(index);
                if (progress > audioMissionProgress[index])
                { arrived = true; complete |= completed > audioMissionCompletions[index]; }
                audioMissionProgress[index] = progress; audioMissionCompletions[index] = completed;
            }
            // 같은 표시 프레임에 여러 미션이 도착하면 완료음을 우선한다.
            if (arrived) EmitAudio(complete ? PuzzleFeedbackCueKind.MissionComplete : PuzzleFeedbackCueKind.MissionArrival);
        }
        public void PlayResultFeedback()
        {
            if (!ResultReady || audioBackground || IsPaused || audioOutcome == Outcome) return;
            audioOutcome = Outcome;
            EmitAudio(Outcome.Kind == BoardOutcomeKind.Won ? PuzzleFeedbackCueKind.Win : PuzzleFeedbackCueKind.Lose);
        }
        private void ClearAudio()
        {
            audioSchedule.Clear(); audioPlayback?.StopAndClear(); audioOutcome = null;
            audioMissionProgress = System.Array.Empty<int>(); audioMissionCompletions = System.Array.Empty<int>();
        }
        private void OnApplicationPause(bool paused)
        {
            audioBackground = paused;
            BoardOutcome playedOutcome = audioOutcome;
            if (paused) { ClearAudio(); audioOutcome = playedOutcome; }
            else if (ready && !failed && !IsRestarting)
            {
                // 복귀 시 현재 표시를 관찰하되 아직 표시하지 않은 결과는 소비하지 않는다.
                InitializeAudio(); audioOutcome = playedOutcome; audioPlayback?.SetPaused(IsPaused);
                // 화면이 이미 결과를 표시했어도 백그라운드에서 미소비한 결과음은 복귀 후 한 번 전달한다.
                PlayResultFeedback();
            }
        }
    }
}
