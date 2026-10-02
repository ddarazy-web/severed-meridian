using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static partial class PuzzleProgressFeedbackVerification
    {
        private const string PlayKey = "Stage09.Scene";
        static PuzzleProgressFeedbackVerification()
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
            Directory.CreateDirectory(Output); SessionState.SetBool(PlayKey, true);
            EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath); EditorApplication.EnterPlaymode();
        }
        private static void Invoke(object target, string name, params object[] args)
            => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static async UniTask SceneAsync()
        {
            results.Clear(); int exit = 0; float previousScale = Time.timeScale; LevelDefinition level = null;
            try
            {
                PropertyInfo feedbackProperty = typeof(PuzzleGameSession).GetProperty("ProgressFeedback");
                Check(feedbackProperty != null, "실제 게임 세션에 수집 표시기 연결");
                PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                float deadline = Time.realtimeSinceStartup + 30;
                while (!session.CanAcceptInput && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                Check(session.CanAcceptInput, "실제 MemoryPack 게임 화면 준비");
                Time.timeScale = 0;
                level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":2}]}", level);
                BoardCoordinate origin = new BoardCoordinate(4, 4);
                typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                    new object[] { level, origin, InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1 });
                LevelRuntimeState initial = LevelStateBuilder.Build(level, 12345).State;
                Check(initial != null, "실제 미션 로켓 레벨 유효");
                BoardActionExecutor executor = new BoardActionExecutor(initial), direct = new BoardActionExecutor(initial);
                Invoke(session, "ResetPresentation");
                typeof(PuzzleGameSession).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, executor);
                Invoke(session, "InitializeProgress");
                PuzzleArtwork art = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                await art.PrepareAsync(initial, CancellationToken.None);
                PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>(); board.Draw(initial, art);
                PuzzleScreenView screen = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenView>(); Invoke(screen, "Refresh");
                PuzzleUIRenderVerification.SetSize(1280, 720); await UniTask.Yield();
                PuzzleHudView hud = screen.GetComponentInChildren<PuzzleHudView>();
                PuzzleResultView resultPanel = screen.GetComponentInChildren<PuzzleResultView>(true);
                UnityEngine.UI.Text count = hud.GetComponentsInChildren<UnityEngine.UI.Text>().Single(text => text.name == "Count");
                Check(count.text == "0/2", "시작 HUD 실제 초기 미션 표시");
                Check(session.TryActivate(origin), "게임 화면 로켓 발동"); direct.Activate(origin);
                Check(session.MovesPulse > 0, "실제 게임 행동의 이동 소비 HUD 강조");
                deadline = Time.realtimeSinceStartup + 15;
                while ((bool)typeof(PuzzleGameSession).GetField("preparingEffects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session)
                    && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                object feedback = feedbackProperty.GetValue(session);
                Check(executor.State.Missions[0].Progress > 0 && count.text == "0/2", "실제 진행 확정 후 HUD 조기 반영 없음");
                bool sawFlight = false, sawPulse = false, checkedPause = false, shotComplete = false, shotLastPang = false;
                for (int frame = 0; frame < 1000; frame++)
                {
                    Invoke(session, "TickProgress", .03f);
                    if (session.IsPresenting) Invoke(session, "AdvancePresentation", .03f); else Invoke(session, "Advance");
                    await UniTask.Yield();
                    if (((System.Collections.IEnumerable)feedback.GetType().GetProperty("Flights").GetValue(feedback)).Cast<object>().Any())
                    {
                        sawFlight = true;
                        Check(screen.GetComponentsInChildren<UnityEngine.UI.Image>().Any(image => image.name.StartsWith("MissionCollection") && image.sprite != null), "실제 HUD 위 수집 이미지 표시");
                        if (!checkedPause)
                        {
                            object flight = ((System.Collections.IEnumerable)feedback.GetType().GetProperty("Flights").GetValue(feedback)).Cast<object>().First();
                            float before = (float)flight.GetType().GetProperty("Progress").GetValue(flight);
                            Check(session.SetPaused(true), "수집 중 pause 가능");
                            Invoke(session, "TickProgress", .5f); Invoke(session, "AdvancePresentation", .5f);
                            Check((float)flight.GetType().GetProperty("Progress").GetValue(flight) == before, "pause 중 수집 시간·보드 표시 정지");
                            session.SetPaused(false); checkedPause = true;
                            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate); ScreenCapture.CaptureScreenshot(Output + "collection-landscape.png"); await UniTask.Yield();
                            PuzzleUIRenderVerification.SetSize(450, 800); await UniTask.Yield(); await UniTask.Yield();
                            Canvas.ForceUpdateCanvases();
                            Check(screen.GetComponentsInChildren<UnityEngine.UI.Image>().Where(image => image.name.StartsWith("MissionCollection")).All(image =>
                                image.gameObject.activeSelf && image.rectTransform.rect.width > 0 && !image.raycastTarget), "회전 후 수집 이미지 유지·UI 터치 비차단");
                            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate); ScreenCapture.CaptureScreenshot(Output + "collection-portrait.png"); await UniTask.Yield();
                        }
                    }
                    if ((float)feedback.GetType().GetMethod("Pulse").Invoke(feedback, new object[] { 0 }) > 0) sawPulse = true;
                    if (!shotComplete && session.ProgressFeedback.Pulse(0) > 0 && session.DisplayedMissionProgress(0) >= session.State.Missions[0].Target)
                    {
                        shotComplete = true; await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                        ScreenCapture.CaptureScreenshot(Output + "mission-complete.png"); await UniTask.Yield();
                    }
                    if (!shotLastPang && session.FeedbackStatus?.Contains("남은 파워") == true)
                    {
                        shotLastPang = true; await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                        ScreenCapture.CaptureScreenshot(Output + "last-pang.png"); await UniTask.Yield();
                    }
                    if (session.Outcome != null && session.Phase == BoardActionPhase.Stopped && session.HasProgressFeedback)
                        Check(!resultPanel.gameObject.activeSelf, "보드 종료 후 남은 수집·종료 반응 중 결과 패널 숨김");
                    if (!session.IsPresenting && !executor.HasPendingCascade && !session.HasProgressFeedback) break;
                    if (frame == 999) throw new InvalidOperationException("수집·보드 종료 timeout");
                }
                while (direct.HasPendingCascade) direct.AdvanceCascade();
                Check(sawFlight && sawPulse, "실제 씬 수집 출발·도착 HUD 반응 관찰");
                PuzzleMissionDisplay display = (PuzzleMissionDisplay)feedback.GetType().GetProperty("Display").GetValue(feedback);
                Check(display.Progress(0) == session.State.Missions[0].Progress && count.text.Contains(display.Progress(0) + "/2"), "최종 HUD와 실제 미션 일치");
                Check(session.State.MovesRemaining == direct.State.MovesRemaining && session.State.Random.DrawCount == direct.State.Random.DrawCount
                    && session.State.Cells.Select(cell => (cell.Content, cell.Color)).SequenceEqual(direct.State.Cells.Select(cell => (cell.Content, cell.Color))), "표시 연결 후 실행기 보드·이동·난수 동등성");
                MethodInfo snapshot = typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static);
                Check((string)snapshot.Invoke(null, new object[] { session.State }) == (string)snapshot.Invoke(null, new object[] { direct.State }) &&
                    session.Phase == direct.Phase && session.Outcome.Kind == direct.Outcome.Kind, "실제 씬 전체 공개 상태·미션·공급·승패 동등성");
                Check(hud.CollectionPoolCount <= 8, "실제 수집 이미지 풀 최대 8개");
                Check(session.ResultReady && resultPanel.gameObject.activeSelf && shotLastPang, "실제 라스트팡과 표시 정리 후 승패 패널 표시");
                UnityEngine.UI.Text resultTitle = (UnityEngine.UI.Text)typeof(PuzzleResultView).GetField("title", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(resultPanel);
                string originalTitle = resultTitle.text; resultTitle.text = "표시 한 번 검사";
                for (int refresh = 0; refresh < 3; refresh++) Invoke(screen, "Refresh");
                Check(resultTitle.text == "표시 한 번 검사", "같은 결과에서 HUD Refresh는 패널을 다시 Show하지 않음");
                resultTitle.text = originalTitle;
                Check(session.ProgressFeedback.CompletionCount(0) == 1, "실제 게임 미션 완료 강조 한 번");
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate); ScreenCapture.CaptureScreenshot(Output + "result.png"); await UniTask.Yield();
                PropertyInfo starting = typeof(PuzzleGameSession).GetProperty("IsStartingFeedback");
                Check(starting != null, "게임 시작 표시와 입력 경계 제공");
                typeof(PuzzleGameSession).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, new BoardActionExecutor(initial));
                Invoke(session, "InitializeProgress");
                board.Draw(initial, art);
                Invoke(session, "BeginStartFeedback");
                Check((bool)starting.GetValue(session) && !session.CanAcceptInput, "시작 표시 중 보드 입력 차단");
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate); ScreenCapture.CaptureScreenshot(Output + "start.png"); await UniTask.Yield();
                Invoke(session, "TickProgress", .3f);
                Check((bool)starting.GetValue(session), "시작 표시 중간 유지");
                Invoke(session, "TickProgress", .4f);
                Check(!(bool)starting.GetValue(session) && session.CanAcceptInput, "시작 표시 종료 후 입력 허용");
                int pool = hud.CollectionPoolCount;
                for (int retry = 0; retry < 5; retry++)
                {
                    await session.RestartAsync(CancellationToken.None);
                    Check(session.IsReady && session.IsStartingFeedback && !session.ProgressFeedback.IsBusy && !session.CanAcceptInput,
                        "다시하기 " + retry + " 수집 초기화·시작 입력 잠금");
                    Invoke(session, "TickProgress", .7f); await UniTask.Yield();
                    Check(session.CanAcceptInput && hud.CollectionPoolCount == pool &&
                        !screen.GetComponentsInChildren<UnityEngine.UI.Image>().Any(image => image.name.StartsWith("MissionCollection")),
                        "다시하기 " + retry + " 풀 누적·잔상·입력 잠금 잔류 없음");
                }
                await WarmSwapTimingCheck(session, screen);
                await SourceAndLifetimeChecks();
                await RecoveryAndItemChecks();
            }
            catch (Exception error) { exit = 1; results.Add("FAIL " + error); Debug.LogException(error); }
            finally
            {
                Time.timeScale = previousScale; PuzzleUIRenderVerification.RestoreSize(); if (level != null) UnityEngine.Object.Destroy(level);
                File.WriteAllLines(Output + "scene-results.txt", results); EditorApplication.Exit(exit);
            }
        }

        private static async UniTask WarmSwapTimingCheck(PuzzleGameSession session, PuzzleScreenView screen)
        {
            session.enabled = false;
            ActionCandidate candidate = ActionQuery.Find(session.State).First(action => action.Kind == QueryActionKind.SwapMatch);
            BoardActionExecutor probe = new BoardActionExecutor(session.State);
            BoardActionResult match = probe.Swap(candidate.First, candidate.Second.Value);
            RabbitColor color = match.Changes.First(change => change.IsConsumed).OriginalColor;
            LevelMissionDefinition definition = (LevelMissionDefinition)Activator.CreateInstance(typeof(LevelMissionDefinition), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { MissionKind.Color, color, 3 }, null);
            RuntimeMission mission = (RuntimeMission)Activator.CreateInstance(typeof(RuntimeMission), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { definition }, null);
            typeof(LevelRuntimeState).GetField("<Missions>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session.State, Array.AsReadOnly(new[] { mission }));
            Invoke(session, "InitializeProgress"); Invoke(screen, "Refresh");
            BoardActionExecutor direct = new BoardActionExecutor(session.State); BoardActionResult action = direct.Swap(candidate.First, candidate.Second.Value);
            Check(action.IsApplied, "캐시 준비된 실제 교환 매칭 fixture");
            PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(session.State, action.Changes, action.Effects, action.PowerTrace);
            Type playbackType = typeof(PuzzleGameSession).Assembly.GetType("GameScreen.PuzzlePowerPlayback"); object warmup = Activator.CreateInstance(playbackType, true);
            PuzzleArtwork artwork = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            await (UniTask)playbackType.GetMethod("PrepareAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(warmup,
                new object[] { session.State, direct.State, artwork, timeline, CancellationToken.None });
            playbackType.GetMethod("Reset", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(warmup, null);
            float capture = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = .15f; Time.timeScale = 1;
                await UniTask.Yield(PlayerLoopTiming.Update);
                session.enabled = true; Check(session.TrySwap(candidate.First, candidate.Second.Value), "캐시 교환 시작");
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                object playback = typeof(PuzzleGameSession).GetField("powerPlayback", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                Check(playback != null && (float)playbackType.GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playback) == 0,
                    "교환 완료 프레임에 매칭 표시 시간은 아직 0");
                Check(session.State.Missions[0].Progress > 0 && session.ProgressFeedback.Flights.Count == 0,
                    "같은 프레임에 새로 예약한 수집이 보드 타격보다 먼저 출발하지 않음");
            }
            finally { Time.captureDeltaTime = capture; Time.timeScale = 0; session.enabled = true; }
        }
    }
}
