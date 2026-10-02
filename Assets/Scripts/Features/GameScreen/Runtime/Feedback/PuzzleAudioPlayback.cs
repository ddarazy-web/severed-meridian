using System;
using UnityEngine;

namespace GameScreen
{
    // 검증용 합성 음원과 재생 객체는 이 씬 객체가 소유한다.
    public sealed class PuzzleAudioPlayback : MonoBehaviour
    {
        private sealed class Voice
        {
            internal AudioSource Source;
            internal float Remaining;
            internal int Order;
            internal PuzzleFeedbackCueKind Kind;
        }
        [SerializeField] private bool soundEnabled = true;
        private AudioClip[] clips = Array.Empty<AudioClip>();
        private Voice[] voices = Array.Empty<Voice>();
        private bool paused;
        private int sequence;
        public int ClipCount => clips.Length;
        public int SourceCount => voices.Length;
        public int ActiveVoices { get { int count = 0; foreach (Voice voice in voices) if (voice.Remaining > 0) count++; return count; } }
        public bool SoundEnabled { get => soundEnabled; set { soundEnabled = value; if (!value) StopAndClear(); } }
        public PuzzleFeedbackCueKind LastPlayedKind { get; private set; }
        public event Action<PuzzleFeedbackCueKind> Played;

        public void Initialize()
        {
            if (clips.Length > 0) return;
            float[] frequencies = { 620, 240, 880, 350, 95, 540, 720, 430, 180, 1040, 1320, 660, 990, 220 };
            float[] lengths = { .1f, .14f, .12f, .2f, .24f, .18f, .12f, .22f, .08f, .12f, .25f, .3f, .42f, .38f };
            clips = new AudioClip[frequencies.Length];
            for (int kind = 0; kind < clips.Length; kind++)
            {
                int count = Mathf.RoundToInt(44100 * lengths[kind]);
                float[] samples = new float[count];
                for (int sample = 0; sample < count; sample++)
                {
                    float t = sample / 44100f, progress = (float)sample / count;
                    float sweep = kind == (int)PuzzleFeedbackCueKind.Lose ? 1 - progress * .45f : 1 + progress * .25f;
                    float phase = 2 * Mathf.PI * frequencies[kind] * t * sweep;
                    float envelope = Mathf.Min(1, t / .008f) * Mathf.Pow(1 - progress, 2);
                    samples[sample] = (Mathf.Sin(phase) + .2f * Mathf.Sin(phase * 2)) * .5f * envelope;
                }
                clips[kind] = AudioClip.Create("Puzzle-" + (PuzzleFeedbackCueKind)kind, count, 1, 44100, false);
                clips[kind].SetData(samples, 0);
            }
            voices = new Voice[8];
            for (int i = 0; i < voices.Length; i++)
            {
                GameObject child = new GameObject("AudioVoice" + i); child.transform.SetParent(transform, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = false; source.spatialBlend = 0;
                voices[i] = new Voice { Source = source };
            }
        }

        public bool Play(PuzzleFeedbackCueKind kind)
        {
            if (!soundEnabled || paused || clips.Length == 0) return false;
            Voice selected = null;
            foreach (Voice voice in voices) if (voice.Remaining <= 0) { selected = voice; break; }
            int priority = kind == PuzzleFeedbackCueKind.Win || kind == PuzzleFeedbackCueKind.Lose ? 3 :
                kind == PuzzleFeedbackCueKind.MissionComplete || kind == PuzzleFeedbackCueKind.MissionArrival ? 2 : kind == PuzzleFeedbackCueKind.Landing ? 0 : 1;
            // 높은 우선순위는 오래된 저우선 음성을 교체한다. 일반 요청은 풀을 늘리지 않는다.
            if (selected == null)
                foreach (Voice voice in voices)
                {
                    int previous = voice.Kind == PuzzleFeedbackCueKind.Win || voice.Kind == PuzzleFeedbackCueKind.Lose ? 3 :
                        voice.Kind == PuzzleFeedbackCueKind.MissionComplete || voice.Kind == PuzzleFeedbackCueKind.MissionArrival ? 2 : voice.Kind == PuzzleFeedbackCueKind.Landing ? 0 : 1;
                    if (previous < priority && (selected == null || voice.Order < selected.Order)) selected = voice;
                }
            if (selected == null) return false;
            selected.Source.Stop(); selected.Source.clip = clips[(int)kind];
            selected.Kind = kind; selected.Remaining = selected.Source.clip.length; selected.Order = sequence++;
            UpdateGain(); selected.Source.Play(); LastPlayedKind = kind; Played?.Invoke(kind);
            return true;
        }
        public void Tick(float deltaTime)
        {
            if (paused) return;
            foreach (Voice voice in voices)
            {
                if (voice.Remaining <= 0) continue;
                voice.Remaining = Mathf.Max(0, voice.Remaining - deltaTime);
                if (voice.Remaining == 0) { voice.Source.Stop(); voice.Source.clip = null; }
            }
            UpdateGain();
        }
        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value;
            foreach (Voice voice in voices)
                if (voice.Remaining > 0) { if (paused) voice.Source.Pause(); else voice.Source.UnPause(); }
            UpdateGain();
        }
        private void UpdateGain()
        {
            float gain = .35f / Mathf.Sqrt(Mathf.Max(1, ActiveVoices));
            foreach (Voice voice in voices) voice.Source.volume = gain;
        }
        public void StopAndClear()
        {
            foreach (Voice voice in voices) { voice.Source.Stop(); voice.Source.clip = null; voice.Remaining = 0; }
            sequence = 0; paused = false;
        }
        public void Release()
        {
            StopAndClear();
            foreach (AudioClip clip in clips) { if (Application.isPlaying) Destroy(clip); else DestroyImmediate(clip); }
            foreach (Voice voice in voices) { if (Application.isPlaying) Destroy(voice.Source.gameObject); else DestroyImmediate(voice.Source.gameObject); }
            clips = Array.Empty<AudioClip>(); voices = Array.Empty<Voice>();
        }
        private void OnDestroy() { Release(); Played = null; }
    }
}
