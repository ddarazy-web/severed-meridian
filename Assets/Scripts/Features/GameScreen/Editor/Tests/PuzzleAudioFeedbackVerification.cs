using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzleAudioFeedbackVerification
    {
        private const string Output = "Logs/Stage10/";
        private static readonly List<string> results = new List<string>();
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); results.Add("PASS " + message); }
        private static object Call(object target, string name, params object[] args)
            => target.GetType().GetMethod(name).Invoke(target, args);
        private static int Count(object target, string name)
            => (int)target.GetType().GetProperty(name).GetValue(target);

        public static void Data()
        {
            Directory.CreateDirectory(Output); results.Clear(); int exit = 0; GameObject owner = null;
            try
            {
                Assembly assembly = typeof(PuzzleGameSession).Assembly;
                Type type = assembly.GetType("GameScreen.PuzzleFeedbackSchedule");
                Check(type != null, "실제 표시 시간 효과음 예약기 존재");
                Type kinds = assembly.GetType("GameScreen.PuzzleFeedbackCueKind");
                object swap = Enum.Parse(kinds, "Swap"), match = Enum.Parse(kinds, "Match");
                object schedule = Activator.CreateInstance(type);
                Call(schedule, "Schedule", swap, 0f); Call(schedule, "Schedule", swap, .03f); Call(schedule, "Schedule", swap, .07f);
                Check(((IEnumerable)Call(schedule, "Tick", .1f)).Cast<object>().Count() == 2, "저프레임 동일 종류 0/.03/.07에서 첫째·셋째만 허용");
                Check(!((IEnumerable)Call(schedule, "Tick", 1f)).Cast<object>().Any(), "소비한 효과음 재발행 없음");
                Call(schedule, "Schedule", match, 1f); Call(schedule, "Clear");
                Check(!((IEnumerable)Call(schedule, "Tick", 2f)).Cast<object>().Any(), "Clear 후 예약·중복 기록 정리");
                Call(schedule, "Schedule", swap, 0f);
                Check(((IEnumerable)Call(schedule, "Tick", 0f)).Cast<object>().Count() == 1, "Clear 후 같은 소리 다시 허용");
                Type playerType = assembly.GetType("GameScreen.PuzzleAudioPlayback");
                Check(playerType != null, "재사용 효과음 재생기 존재");
                owner = new GameObject("Stage10-Audio-Data");
                Component player = owner.AddComponent(playerType);
                Call(player, "Initialize");
                int clips = Count(player, "ClipCount");
                Check(clips == Enum.GetValues(kinds).Length && Count(player, "SourceCount") == 8, "종류별 합성 클립과 8음성 한 번 생성");
                Call(player, "Initialize");
                Check(Count(player, "ClipCount") == clips && Count(player, "SourceCount") == 8, "반복 준비에서 클립·재생 객체 증가 없음");
                for (int i = 0; i < 8; i++) Check((bool)Call(player, "Play", Enum.ToObject(kinds, i)), "빈 음성 재사용 " + i);
                Check(Count(player, "ActiveVoices") == 8 && !(bool)Call(player, "Play", match), "9번째 저우선 요청 생략·풀 확장 없음");
                object win = Enum.Parse(kinds, "Win");
                Check((bool)Call(player, "Play", win) && Count(player, "ActiveVoices") == 8, "포화 시 결과음은 저우선 음성 교체");
                AudioSource[] sources = owner.GetComponentsInChildren<AudioSource>();
                Check(sources.Length == 8 && sources.All(source => source.spatialBlend == 0 && !source.loop && source.volume <= .35f), "2D 비반복·밀집 음량 제한");
                AudioClip[] owned = sources.Where(source => source.clip != null).Select(source => source.clip).Distinct().ToArray();
                Check(owned.All(clip => clip.channels == 1 && clip.frequency == 44100 && clip.length > 0 && clip.length < 1), "44.1kHz 모노 짧은 PCM");
                Call(player, "SetPaused", true); Call(player, "Tick", 2f);
                Check(Count(player, "ActiveVoices") == 8 && !(bool)Call(player, "Play", swap), "pause 음성 시간 정지·새 요청 차단");
                Call(player, "SetPaused", false); Call(player, "Tick", 2f);
                Check(Count(player, "ActiveVoices") == 0, "재개 후 음성 종료·슬롯 반환");
                playerType.GetProperty("SoundEnabled").SetValue(player, false);
                Check(!(bool)Call(player, "Play", swap), "소리 비활성 시 요청 무시");
                playerType.GetProperty("SoundEnabled").SetValue(player, true);
                Call(player, "Play", swap); Call(player, "StopAndClear");
                Check(Count(player, "ActiveVoices") == 0 && Count(player, "SourceCount") == 8 && sources.All(source => source.clip == null), "Stop 후 참조 정리·풀 보존");
                for (int i = 0; i < 8; i++) Call(player, "Play", Enum.Parse(kinds, "Landing"));
                Check((bool)Call(player, "Play", Enum.Parse(kinds, "MissionComplete")) && Count(player, "ActiveVoices") == 8,
                    "포화 시 미션 완료음은 착지음보다 우선");
                Call(player, "Release");
                Check(Count(player, "ClipCount") == 0 && Count(player, "SourceCount") == 0, "소유 클립·음성 완전 반환");
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally
            {
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                File.WriteAllLines(Output + "data-results.txt", results); EditorApplication.Exit(exit);
            }
        }
    }
}
