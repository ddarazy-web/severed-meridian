using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static partial class PuzzleAudioFeedbackVerification
    {
        private const string PlayKey = "Stage10.Scene";
        static PuzzleAudioFeedbackVerification()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PlayKey, false))
                { SessionState.SetBool(PlayKey, false); SceneAsync().Forget(Debug.LogException); }
            };
        }
        public static void RunScene()
        {
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            System.IO.Directory.CreateDirectory(Output); SessionState.SetBool(PlayKey, true);
            EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath); EditorApplication.EnterPlaymode();
        }
        private static async UniTask SceneAsync()
        {
            results.Clear(); int exit = 0;
            try
            {
                PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                float deadline = Time.realtimeSinceStartup + 30;
                while (!session.CanAcceptInput && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                Check(session.CanAcceptInput, "실제 게임 씬 기존 MemoryPack 시작");
                PropertyInfo audio = typeof(PuzzleGameSession).GetProperty("AudioPlayback");
                Check(audio != null && audio.GetValue(session) != null, "실제 세션 재생기·게임 프리팹 연결");
                Check(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.enabled) == 1,
                    "실제 게임 씬 활성 AudioListener 한 개");
                await ReviewChecks();
                session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                await ActionChecks(session);
                await BoundaryChecks(session);
                await LifetimeChecks(session);
                await SourceChecks();
                await RelatedRecoveryChecks();
                await LossCheck();
            }
            catch (Exception error) { results.Add("FAIL " + error); exit = 1; }
            finally { System.IO.File.WriteAllLines(Output + "scene-results.txt", results); EditorApplication.Exit(exit); }
        }

        private static void Invoke(object target, string name, params object[] args)
            => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static string Snapshot(object state)
            => (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { state });
        private static async UniTask Frame(PuzzleGameSession session, float delta)
        {
            FieldInfo preparing = typeof(PuzzleGameSession).GetField("preparingEffects", BindingFlags.NonPublic | BindingFlags.Instance);
            float deadline = Time.realtimeSinceStartup + 20;
            while ((bool)preparing.GetValue(session) && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
            if ((bool)preparing.GetValue(session) || session.HasFailed) throw new InvalidOperationException("효과 준비 실패: " + session.Message);
            Invoke(session, "TickProgress", delta);
            if (session.IsPresenting) Invoke(session, "AdvancePresentation", delta); else Invoke(session, "Advance");
            await UniTask.Yield();
        }
        private static async UniTask ActionChecks(PuzzleGameSession session)
        {
            float previousTime = Time.timeScale; Time.timeScale = 0; session.enabled = false;
            LevelDefinition level = null;
            PuzzleAudioPlayback player = session.AudioPlayback;
            List<(PuzzleFeedbackCueKind kind, float at)> heard = new List<(PuzzleFeedbackCueKind, float)>();
            float elapsed = 0;
            void Played(PuzzleFeedbackCueKind kind) { heard.Add((kind, elapsed)); }
            player.Played += Played;
            try
            {
                PuzzleScreenView screen = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenView>();
                PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
                BoardCoordinate origin = new BoardCoordinate(4, 4), second = new BoardCoordinate(4, 5);
                for (int fixture = 0; fixture < 14; fixture++)
                {
                    bool combination = fixture >= 4;
                    level = combination ? (LevelDefinition)typeof(CombinationVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null, new object[] { fixture - 4, RocketDirection.Horizontal, null }) :
                        (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                    if (!combination)
                        typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                            new object[] { level, origin, new[] { InitialBlockKind.Rocket, InitialBlockKind.Bomb, InitialBlockKind.Drone, InitialBlockKind.Magnet }[fixture], RocketDirection.Horizontal, RabbitColor.Type1 });
                    if (fixture == 0) JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
                    LevelRuntimeState initial = LevelStateBuilder.Build(level, 12345).State;
                    BoardActionExecutor direct = new BoardActionExecutor(initial), executor = new BoardActionExecutor(initial);
                    Invoke(session, "ResetPresentation");
                    typeof(PuzzleGameSession).GetField("executor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, executor);
                    Invoke(session, "InitializeProgress");
                    PuzzleArtwork art = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                    await art.PrepareAsync(initial, CancellationToken.None); board.Draw(initial, art);
                    Invoke(screen, "Refresh"); heard.Clear(); elapsed = 0; session.enabled = true;
                    Check(combination ? session.TrySwap(origin, second) : session.TryActivate(origin), "실제 씬 4파워·10조합 발동 " + fixture);
                    if (combination) direct.Swap(origin, second); else direct.Activate(origin);
                    session.enabled = false;
                    Check(heard.Count(cue => cue.kind == PuzzleFeedbackCueKind.Swap) == (combination ? 1 : 0), "유효 교환 시작 소리 한 번 " + fixture);
                    if (combination) { elapsed += .15f; await Frame(session, .15f); }
                    // 효과 준비 완료 후 실제 재생기가 제공하는 드론 시각을 독립 확인한다.
                    await Frame(session, 0);
                    object playback = typeof(PuzzleGameSession).GetField("powerPlayback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                    PuzzleFeedbackCue[] cues = playback == null ? Array.Empty<PuzzleFeedbackCue>() :
                        ((IEnumerable<PuzzleFeedbackCue>)playback.GetType().GetProperty("AudioCues", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playback)).ToArray();
                    float powerStart = elapsed;
                    bool drone = cues.Any(cue => cue.Kind == PuzzleFeedbackCueKind.DroneDive);
                    for (int frame = 0; frame < 20000 && (session.IsPresenting || executor.HasPendingCascade || session.HasProgressFeedback); frame++)
                    {
                        float delta = fixture % 2 == 0 ? .017f : .04f; elapsed += delta; await Frame(session, delta);
                        Invoke(screen, "Refresh");
                        if (!session.ResultReady) Check(!heard.Any(cue => cue.kind == PuzzleFeedbackCueKind.Win || cue.kind == PuzzleFeedbackCueKind.Lose), "최종 결과 준비 이전 결과음 없음 " + fixture + "/" + frame);
                    }
                    while (direct.HasPendingCascade) direct.AdvanceCascade();
                    Check(!session.HasFailed && !session.IsPresenting && !executor.HasPendingCascade && Snapshot(session.State) == Snapshot(direct.State) &&
                        session.Phase == direct.Phase && Snapshot(session.Outcome) == Snapshot(direct.Outcome), "소리 연결 후 보드·미션·공급·난수·승패 동등 " + fixture);
                    Check(heard.Any(cue => cue.kind != PuzzleFeedbackCueKind.Swap), "파워·착지·수집 실제 소리 요청 " + fixture);
                    if (fixture == 0) Check(heard.Any(cue => cue.kind == PuzzleFeedbackCueKind.MissionArrival) && !heard.Any(cue => cue.kind == PuzzleFeedbackCueKind.MissionComplete), "목표 미달 도착은 일반 미션 도착음");
                    if (drone)
                    {
                        float dive = cues.Where(cue => cue.Kind == PuzzleFeedbackCueKind.DroneDive).Min(cue => cue.Time);
                        float hit = cues.Where(cue => cue.Kind == PuzzleFeedbackCueKind.DroneHit).Select(cue => cue.Time).DefaultIfEmpty(float.PositiveInfinity).Min();
                        Check(heard.Where(cue => cue.kind == PuzzleFeedbackCueKind.DroneDive).All(cue => cue.at + .001f >= powerStart + dive), "최신 드론 돌파 이전 소리 없음 " + fixture);
                        Check(heard.Where(cue => cue.kind == PuzzleFeedbackCueKind.DroneHit).All(cue => cue.at + .001f >= powerStart + hit), "실제 드론 타격 이전 소리 없음 " + fixture);
                        Check(heard.Any(cue => cue.kind == PuzzleFeedbackCueKind.DroneDive), "최신 드론 돌파 시 실제 재생 요청 " + fixture);
                        Check(float.IsPositiveInfinity(hit) || heard.Any(cue => cue.kind == PuzzleFeedbackCueKind.DroneHit), "유효 드론 타격 시 실제 재생 요청 " + fixture);
                    }
                    int count = heard.Count;
                    for (int repeat = 0; repeat < 10; repeat++) Invoke(screen, "Refresh");
                    Check(heard.Count == count, "UI Refresh 10회 소리 중복 없음 " + fixture);
                    Check(player.SourceCount == 8 && player.ClipCount == 14, "반복 행동 재생 객체·클립 수 고정 " + fixture);
                    UnityEngine.Object.Destroy(level); level = null;
                }
            }
            finally
            {
                player.Played -= Played; if (level != null) UnityEngine.Object.Destroy(level);
                Time.timeScale = previousTime; session.enabled = true;
            }
        }

        private static async UniTask LossCheck()
        {
            PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
            PuzzleScreenView screen = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenView>();
            float previous = Time.timeScale; Time.timeScale = 0; session.enabled = false;
            List<PuzzleFeedbackCueKind> heard = new List<PuzzleFeedbackCueKind>();
            void Played(PuzzleFeedbackCueKind kind) { heard.Add(kind); }
            session.AudioPlayback.Played += Played;
            LevelDefinition level = null;
            try
            {
                level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                BoardCoordinate origin = new BoardCoordinate(4, 4);
                typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                    new object[] { level, origin, InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1 });
                JsonUtility.FromJsonOverwrite("{\"moveCount\":1,\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345); Check(built.IsBuilt, "이동 소진 실패음 실제 fixture");
                Invoke(session, "ResetPresentation"); BoardActionExecutor executor = new BoardActionExecutor(built.State), direct = new BoardActionExecutor(built.State);
                typeof(PuzzleGameSession).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, executor);
                Invoke(session, "InitializeProgress");
                PuzzleArtwork art = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                await art.PrepareAsync(built.State, CancellationToken.None); UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>().Draw(session.State, art); Invoke(screen, "Refresh");
                session.enabled = true; Check(session.TryActivate(origin), "이동 소진 실제 파워 발동"); session.enabled = false; direct.Activate(origin);
                for (int frame = 0; frame < 20000 && (session.IsPresenting || executor.HasPendingCascade || session.HasProgressFeedback); frame++) { await Frame(session, .02f); Invoke(screen, "Refresh"); }
                while (direct.HasPendingCascade) direct.AdvanceCascade(); Invoke(screen, "Refresh");
                Check(session.ResultReady && session.Outcome.Kind == BoardOutcomeKind.MovesExhausted && Snapshot(session.State) == Snapshot(direct.State) && Snapshot(session.Outcome) == Snapshot(direct.Outcome), "이동 소진 실패 최종 상태·승패 동등");
                Check(heard.Count(kind => kind == PuzzleFeedbackCueKind.Lose) == 1 && !heard.Contains(PuzzleFeedbackCueKind.Win), "실패음 한 번·승리음 없음");
                int count = heard.Count; for (int repeat = 0; repeat < 10; repeat++) Invoke(screen, "Refresh");
                Check(heard.Count == count, "실패 패널 Refresh 소리 중복 없음");
            }
            finally
            {
                session.AudioPlayback.Played -= Played; if (level != null) UnityEngine.Object.Destroy(level);
                Time.timeScale = previous; session.enabled = true;
            }
        }

        private static async UniTask BoundaryChecks(PuzzleGameSession session)
        {
            float previous = Time.timeScale; Time.timeScale = 0; session.enabled = false;
            List<PuzzleFeedbackCueKind> heard = new List<PuzzleFeedbackCueKind>();
            void Played(PuzzleFeedbackCueKind kind) { heard.Add(kind); }
            session.AudioPlayback.Played += Played;
            LevelDefinition level = null;
            try
            {
                level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                BoardCoordinate origin = new BoardCoordinate(4, 4);
                typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                    new object[] { level, origin, InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1 });
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", level);
                LevelRuntimeState initial = LevelStateBuilder.Build(level, 12345).State;
                PuzzleScreenView screen = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenView>();
                for (int muted = 0; muted < 2; muted++)
                {
                    Invoke(session, "ResetPresentation");
                    BoardActionExecutor direct = new BoardActionExecutor(initial), executor = new BoardActionExecutor(initial);
                    typeof(PuzzleGameSession).GetField("executor", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(session, executor);
                    Invoke(session, "InitializeProgress"); session.SoundEnabled = muted == 0;
                    PuzzleArtwork art = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                    await art.PrepareAsync(initial, CancellationToken.None); UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>().Draw(initial, art);
                    Invoke(screen, "Refresh"); heard.Clear();
                    Check(!session.TryActivate(origin) && heard.Count == 0, "차단 입력은 효과음 0 " + muted);
                    session.enabled = true; Check(session.TryActivate(origin), "최종 미션 파워 실제 발동 " + muted); session.enabled = false;
                    direct.Activate(origin);
                    bool beforeArrival = false;
                    for (int frame = 0; frame < 20000 && (session.IsPresenting || executor.HasPendingCascade || session.HasProgressFeedback); frame++)
                    {
                        await Frame(session, .02f); Invoke(screen, "Refresh");
                        if (session.State.Missions[0].Progress > session.DisplayedMissionProgress(0))
                        {
                            beforeArrival = true;
                            Check(!heard.Contains(PuzzleFeedbackCueKind.MissionComplete) && !heard.Contains(PuzzleFeedbackCueKind.MissionArrival), "도착 전 미션음 대기 " + muted + "/" + frame);
                        }
                    }
                    while (direct.HasPendingCascade) direct.AdvanceCascade(); Invoke(screen, "Refresh");
                    Check(beforeArrival && session.ResultReady && Snapshot(session.State) == Snapshot(direct.State) && Snapshot(session.Outcome) == Snapshot(direct.Outcome),
                        "on/off 실제 미션 도착·라스트팡·직접 결과 동등 " + muted);
                    Check(muted == 0 ? heard.Count(kind => kind == PuzzleFeedbackCueKind.MissionComplete) == 1 &&
                        !heard.Contains(PuzzleFeedbackCueKind.MissionArrival) && heard.Count(kind => kind == PuzzleFeedbackCueKind.Win) == 1 : heard.Count == 0,
                        "완료 도착은 완료음만 한 번·승리 한 번·mute 요청 0 " + muted);
                    int count = heard.Count; for (int repeat = 0; repeat < 10; repeat++) Invoke(screen, "Refresh");
                    Check(heard.Count == count, "최종 패널 Refresh 결과음 중복 없음 " + muted);
                }
                session.SoundEnabled = true;
                // 시작은 실제 다시하기 경계에서만 한 번 발생한다.
                session.enabled = true; heard.Clear(); await session.RestartAsync(CancellationToken.None); session.enabled = false;
                Check(heard.Count(kind => kind == PuzzleFeedbackCueKind.Start) == 1, "실제 다시하기 시작음 한 번");
                for (int repeat = 0; repeat < 10; repeat++) Invoke(screen, "Refresh");
                Check(heard.Count(kind => kind == PuzzleFeedbackCueKind.Start) == 1, "시작 HUD Refresh 중복 없음");
                Invoke(session, "TickProgress", .7f);
                BoardCoordinate first = default, second = default; bool found = false;
                foreach (RuntimeCell cell in session.State.Cells)
                {
                    BoardCoordinate right = new BoardCoordinate(cell.Coordinate.Row, cell.Coordinate.Column + 1);
                    if (right.Column < 9 && ActionQuery.Swap(session.State, cell.Coordinate, right).Reason == ActionReason.NoNewMatch)
                    { first = cell.Coordinate; second = right; found = true; break; }
                }
                Check(found, "무효 교환 실제 fixture"); heard.Clear(); session.enabled = true;
                Check(!session.TrySwap(first, second), "무효 교환은 실제 복귀 연출"); session.enabled = false;
                await Frame(session, .14f); Check(heard.Count == 0, "무효 복귀 시작 전 소리 없음");
                await Frame(session, .02f); Check(heard.Count(kind => kind == PuzzleFeedbackCueKind.InvalidSwap) == 1 && !heard.Contains(PuzzleFeedbackCueKind.Swap), "실제 무효 복귀 시점 소리 한 번");
            }
            finally
            {
                session.AudioPlayback.Played -= Played; if (level != null) UnityEngine.Object.Destroy(level);
                session.SoundEnabled = true; Time.timeScale = previous; session.enabled = true;
            }
        }
    }
}

