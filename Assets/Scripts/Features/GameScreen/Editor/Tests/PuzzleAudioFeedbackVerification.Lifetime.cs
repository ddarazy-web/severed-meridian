using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameScreen.Editor
{
    public static partial class PuzzleAudioFeedbackVerification
    {
        private static async UniTask LifetimeChecks(PuzzleGameSession session)
        {
            float previous = Time.timeScale; Time.timeScale = 0; session.enabled = false;
            PuzzleAudioPlayback player = session.AudioPlayback;
            List<PuzzleFeedbackCueKind> heard = new List<PuzzleFeedbackCueKind>();
            void Played(PuzzleFeedbackCueKind kind) { heard.Add(kind); }
            player.Played += Played;
            try
            {
                player.StopAndClear(); player.Play(PuzzleFeedbackCueKind.Swap); heard.Clear();
                Check(session.SetPaused(true), "실제 게임 pause 진입");
                Invoke(session, "TickProgress", 1f);
                Check(player.ActiveVoices == 1 && !player.Play(PuzzleFeedbackCueKind.Match) && heard.Count == 0,
                    "세션 pause에서 음성 시간과 새 소리 요청 정지");
                Check(session.SetPaused(false), "실제 게임 pause 재개"); await Frame(session, .5f);
                Check(player.ActiveVoices == 0, "재개 후 음성 종료");
                MethodInfo background = typeof(PuzzleGameSession).GetMethod("OnApplicationPause", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(background != null, "앱 백그라운드 소리 정리 경계 존재");
                player.Play(PuzzleFeedbackCueKind.Bomb); heard.Clear();
                background.Invoke(session, new object[] { true });
                Invoke(session, "EmitAudio", PuzzleFeedbackCueKind.Rocket); await Frame(session, .2f);
                PuzzleFeedbackSchedule schedule = (PuzzleFeedbackSchedule)typeof(PuzzleGameSession).GetField("audioSchedule", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                Check(player.ActiveVoices == 0 && schedule.PendingCount == 0 && heard.Count == 0, "백그라운드 기존 음성·예약 정리와 새 요청 차단");
                background.Invoke(session, new object[] { false }); Invoke(session, "TickProgress", .1f);
                Check(heard.Count == 0, "복귀 시 이전 소리 재발행 없음");
                for (int repeat = 0; repeat < 5; repeat++)
                {
                    player.Play(PuzzleFeedbackCueKind.DroneDive); heard.Clear(); session.enabled = true;
                    await session.RestartAsync(CancellationToken.None); session.enabled = false;
                    Check(heard.SequenceEqual(new[] { PuzzleFeedbackCueKind.Start }) && player.SourceCount == 8 && player.ClipCount == 14 && schedule.PendingCount == 0,
                        "다시하기 기존 음성·예약 제거와 풀 재사용 " + repeat);
                    Invoke(session, "TickProgress", .7f);
                }
                player.Play(PuzzleFeedbackCueKind.Bomb);
                object priorState = session.State;
                string priorLogical = session.LogicalSessionId;
                heard.Clear(); session.enabled = true;
                using (CancellationTokenSource cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel(); await session.RestartAsync(cancellation.Token);
                }
                session.enabled = false;
                Check(session.IsReady && !session.IsRestarting && ReferenceEquals(session.State, priorState) && session.LogicalSessionId == priorLogical &&
                    player.ActiveVoices == 1 && schedule.PendingCount == 0 && heard.Count == 0 &&
                    player.GetComponentsInChildren<AudioSource>().Count(source => source.clip != null) == 1,
                    "준비 취소 기존 플레이·문맥·유효 음성 보존 소리 재실행0");
                session.enabled = true; await session.RestartAsync(CancellationToken.None); session.enabled = false;
                Invoke(session, "TickProgress", .7f); player.Play(PuzzleFeedbackCueKind.Bomb);
                Invoke(session, "Fail", "Stage10 의도한 표시 오류");
                Check(session.HasFailed && player.ActiveVoices == 0 && schedule.PendingCount == 0, "오류 후 이전 음성·예약 정리");
                session.enabled = true; await session.RestartAsync(CancellationToken.None); session.enabled = false; Invoke(session, "TickProgress", .7f);
                AudioClip[] owned = (AudioClip[])typeof(PuzzleAudioPlayback).GetField("clips", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
                player.Play(PuzzleFeedbackCueKind.Bomb);
                await EditorSceneManager.LoadSceneAsyncInPlayMode(PuzzleGameAssets.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                await UniTask.Yield(); await UniTask.Yield();
                Check(player == null && owned.All(clip => clip == null), "씬 종료 재생 객체·소유 합성 클립 파괴");
                session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                float deadline = Time.realtimeSinceStartup + 30;
                while (!session.IsReady && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                Invoke(session, "TickProgress", .7f);
                Check(session.CanAcceptInput && session.AudioPlayback.SourceCount == 8 && session.AudioPlayback.ClipCount == 14 &&
                    UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.enabled) == 1,
                    "씬 재진입 새 소유권·입력·리스너 정상");
            }
            finally
            {
                if (player != null) player.Played -= Played;
                Time.timeScale = previous; if (session != null) session.enabled = true;
            }
        }
    }
}
