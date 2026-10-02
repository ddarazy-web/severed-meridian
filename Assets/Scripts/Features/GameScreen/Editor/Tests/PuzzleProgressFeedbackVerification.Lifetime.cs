using System;
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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameScreen.Editor
{
    public static partial class PuzzleProgressFeedbackVerification
    {
        private static async UniTask SourceAndLifetimeChecks()
        {
            LevelDefinition source = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
            string sourceJson = JsonUtility.ToJson(source);
            foreach (PuzzleEditorLevelSource mode in new[] { PuzzleEditorLevelSource.Asset, PuzzleEditorLevelSource.MemoryPack })
            {
                PuzzleGameSession old = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                PuzzleArtwork previousArt = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(old);
                PuzzleHudView previousHud = UnityEngine.Object.FindFirstObjectByType<PuzzleHudView>();
                Image[] previousCollections = previousHud.GetComponentInParent<Canvas>().GetComponentsInChildren<Image>(true)
                    .Where(image => image.name.StartsWith("MissionCollection")).ToArray();
                old.enabled = false;
                PuzzleEditorLaunchRequest request = PuzzleEditorLaunchRequest.Capture(source, mode, 12345);
                LevelDefinition baseline = request.CreateDefinition();
                StartingBoardSearch search = new StartingBoardSearch(baseline, 12345);
                while (!search.IsDone) search.Advance(128);
                Check(search.Status == StartingBoardStatus.Success, mode + " 입력 사본 시작 보드 유효");
                UnityEngine.Object.Destroy(baseline);
                // 실제 씬 로드에서 Start보다 먼저 에디터 실행 요청의 소유 사본을 전달한다.
                void Load(Scene scene, LoadSceneMode loadMode)
                {
                    if (scene.path != PuzzleGameAssets.ScenePath) return;
                    scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PuzzleGameSession>(true)).Single()
                        .InitializeAsync(request.CreateDefinition(), 12345, CancellationToken.None).Forget(Debug.LogException);
                }
                SceneManager.sceneLoaded += Load;
                try { await EditorSceneManager.LoadSceneAsyncInPlayMode(PuzzleGameAssets.ScenePath, new LoadSceneParameters(LoadSceneMode.Single)); }
                finally { SceneManager.sceneLoaded -= Load; }
                await UniTask.Yield();
                PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                float deadline = Time.realtimeSinceStartup + 30;
                while (!session.IsReady && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                Check(session.IsReady && session.IsStartingFeedback, mode + " 실제 씬 소유 데이터 준비·시작 표시");
                Check(old == null && previousHud == null && previousArt.AtlasCount == 0, mode + " 씬 교체 후 이전 세션·HUD·아틀라스 반환");
                Check(previousCollections.All(image => image == null), mode + " 이전 HUD 소유 수집 이미지 모두 파괴");
                Check(StateSnapshot(session.State) == StateSnapshot(new BoardActionExecutor(search.State).State), mode + " 실제 입력 소스 초기 전체 상태 동등");
                Check(session.SetPaused(true), mode + " 시작 표시 pause");
                Invoke(session, "TickProgress", 1f);
                Check(session.IsStartingFeedback && !session.CanAcceptInput, mode + " pause 중 시작 표시·입력 잠금 유지");
                session.SetPaused(false); Invoke(session, "TickProgress", .7f);
                PuzzleScreenView screen = UnityEngine.Object.FindFirstObjectByType<PuzzleScreenView>();
                Canvas.ForceUpdateCanvases();
                PuzzleBoardInput pointerInput = session.GetComponent<PuzzleBoardInput>();
                Button pauseButton = (Button)typeof(PuzzleScreenView).GetField("pauseButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(screen);
                Vector2 uiPoint = RectTransformUtility.WorldToScreenPoint(null, pauseButton.transform.position);
                var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = uiPoint }, hits);
                Check(hits.Count > 0 && session.CanAcceptInput, mode + " UI 터치 검사 시 실제 UI raycast와 보드 입력 준비");
                string beforeTouch = StateSnapshot(session.State);
                Vector2 boardPoint = session.BoardCamera.WorldToScreenPoint(session.CollectionWorldPosition(new BoardCoordinate(4, 4)));
                Invoke(pointerInput, "BeginPointer", 31, uiPoint); Invoke(pointerInput, "UpdatePointer", 31, boardPoint); Invoke(pointerInput, "EndPointer", 31, boardPoint);
                Check(StateSnapshot(session.State) == beforeTouch && !pointerInput.Selected.HasValue &&
                    typeof(PuzzleBoardInput).GetField("pointer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pointerInput) == null,
                    mode + " UI에서 시작한 터치가 보드로 이동해도 선택·교환 없음");
                PuzzleHudView hud = screen.GetComponentInChildren<PuzzleHudView>();
                PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
                ActionCandidate candidate = ActionQuery.Find(session.State).First(action => action.Kind == QueryActionKind.SwapMatch);
                BoardActionExecutor originalSourceDirect = new BoardActionExecutor(session.State);
                originalSourceDirect.Swap(candidate.First, candidate.Second.Value);
                while (originalSourceDirect.HasPendingCascade) originalSourceDirect.AdvanceCascade();
                Check(session.TrySwap(candidate.First, candidate.Second.Value), mode + " 원본 맵 미션 유지 실제 교환");
                session.enabled = false;
                for (int frame = 0; frame < 20000; frame++)
                {
                    await ProgressFrame(session, .02f);
                    BoardActionExecutor original = (BoardActionExecutor)typeof(PuzzleGameSession).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                    if (!session.IsPresenting && !original.HasPendingCascade && !session.HasProgressFeedback) break;
                    if (frame == 19999) throw new InvalidOperationException("원본 맵 행동 종료 timeout " + mode);
                }
                Check(StateSnapshot(session.State) == StateSnapshot(originalSourceDirect.State) && session.Phase == originalSourceDirect.Phase &&
                    StateSnapshot(session.Outcome) == StateSnapshot(originalSourceDirect.Outcome), mode + " 원본 맵 미션을 유지한 실제 행동 전체 동등");
                Check(session.State.Missions.Select((item, index) => session.DisplayedMissionProgress(index) == item.Progress).All(value => value),
                    mode + " 원본 맵 최종 실제 미션과 HUD 일치");
                session.enabled = true; await session.RestartAsync(CancellationToken.None); Invoke(session, "TickProgress", .7f);
                BoardActionExecutor probe = new BoardActionExecutor(session.State);
                RabbitColor color = probe.Swap(candidate.First, candidate.Second.Value).Changes.First(change => change.IsConsumed).OriginalColor;
                LevelMissionDefinition definition = (LevelMissionDefinition)Activator.CreateInstance(typeof(LevelMissionDefinition), BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { MissionKind.Color, color, 3 }, null);
                RuntimeMission mission = (RuntimeMission)Activator.CreateInstance(typeof(RuntimeMission), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { definition }, null);
                typeof(LevelRuntimeState).GetField("<Missions>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session.State, Array.AsReadOnly(new[] { mission }));
                LevelRuntimeState input = session.State;
                PuzzleArtwork art = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                int pool = -1;
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    session.enabled = false;
                    Invoke(session, "ResetPresentation");
                    typeof(PuzzleGameSession).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, new BoardActionExecutor(input));
                    Invoke(session, "InitializeProgress"); board.Draw(session.State, art); Invoke(screen, "Refresh");
                    session.enabled = true;
                    BoardActionExecutor direct = new BoardActionExecutor(input); direct.Swap(candidate.First, candidate.Second.Value);
                    Check(session.TrySwap(candidate.First, candidate.Second.Value), mode + " 실제 수집 교환 " + repeat);
                    session.enabled = false;
                    bool positioned = false;
                    for (int frame = 0; frame < 20000; frame++)
                    {
                        await ProgressFrame(session, .02f);
                        if (!positioned && session.ProgressFeedback.Flights.Count > 0)
                        {
                            positioned = true;
                            if (repeat == 0) await CheckCollectionPosition(session, screen, hud);
                        }
                        BoardActionExecutor executor = (BoardActionExecutor)typeof(PuzzleGameSession).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                        if (!session.IsPresenting && !executor.HasPendingCascade && !session.HasProgressFeedback) break;
                        if (frame == 19999) throw new InvalidOperationException("입력 소스 수집 종료 timeout " + mode);
                    }
                    while (direct.HasPendingCascade) direct.AdvanceCascade();
                    Check(positioned && session.DisplayedMissionProgress(0) == session.State.Missions[0].Progress &&
                        StateSnapshot(session.State) == StateSnapshot(direct.State) && session.Phase == direct.Phase && StateSnapshot(session.Outcome) == StateSnapshot(direct.Outcome),
                        mode + " 수집 후 미션·보드·공급·난수·이동·승패 전체 동등 " + repeat);
                    hud.Frame(session);
                    if (pool < 0) pool = hud.CollectionPoolCount;
                    Check(pool > 0 && pool <= 8 && hud.CollectionPoolCount == pool && !screen.GetComponentsInChildren<Image>().Any(image => image.name.StartsWith("MissionCollection")),
                        mode + " 반복 실제 수집 풀 재사용·잔상 없음 " + repeat);
                    Check(hud.GetComponentsInChildren<PuzzleMissionView>().All(view => view.Icon.localScale == Vector3.one && view.Icon.GetComponent<Image>().color == Color.white),
                        mode + " 수집 강조 종료 후 아이콘 크기·색 복원 " + repeat);
                }
                session.enabled = true;
                Invoke(session, "ResetPresentation");
                typeof(PuzzleGameSession).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, new BoardActionExecutor(input));
                Invoke(session, "InitializeProgress"); board.Draw(session.State, art); Invoke(screen, "Refresh");
                Check(session.TrySwap(candidate.First, candidate.Second.Value), mode + " 수집 중 취소 fixture 교환");
                session.enabled = false;
                for (int frame = 0; frame < 1000 && session.ProgressFeedback.Flights.Count == 0; frame++) await ProgressFrame(session, .02f);
                Check(session.ProgressFeedback.Flights.Count > 0, mode + " 취소 직전 실제 수집 진행 중");
                using CancellationTokenSource cancelled = new CancellationTokenSource(); cancelled.Cancel();
                await session.RestartAsync(cancelled.Token); await UniTask.Yield();
                Check(!session.IsReady && !session.IsRestarting && !session.HasProgressFeedback && !session.CanAcceptInput, mode + " 재시작 준비 취소·표시·잠금 정리");
                Check(!screen.GetComponentsInChildren<Image>().Any(image => image.name.StartsWith("MissionCollection")), mode + " 수집 중 취소 직후 화면 잔상 없음");
                await session.RestartAsync(CancellationToken.None); Invoke(session, "TickProgress", .7f);
                session.enabled = true;
                Check(session.CanAcceptInput && !session.HasFailed, mode + " 취소 후 새 재시작 정상 입력");
                candidate = ActionQuery.Find(session.State).First(action => action.Kind == QueryActionKind.SwapMatch);
                Check(session.TrySwap(candidate.First, candidate.Second.Value), mode + " 실패 검사 실제 행동");
                session.enabled = false;
                await ProgressFrame(session, .15f);
                PuzzleArtwork failureArt = (PuzzleArtwork)typeof(PuzzleGameSession).GetField("artwork", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                Invoke(session, "Fail", "Stage09 검사 의도된 실패"); await UniTask.Yield();
                deadline = Time.realtimeSinceStartup + 20;
                while ((int)typeof(PuzzleArtwork).GetField("pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(failureArt) > 0
                    && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                Check(session.HasFailed && !session.HasProgressFeedback && !session.IsPresenting && !session.CanAcceptInput && failureArt.AtlasCount == 0,
                    mode + " 실패 후 표시·예약·입력·진행 중 로드 완료 후 아틀라스 정리");
                session.enabled = true; await session.RestartAsync(CancellationToken.None); Invoke(session, "TickProgress", .7f);
                Check(session.CanAcceptInput && !session.HasFailed && hud.CollectionPoolCount == pool, mode + " 실패 후 재시작 풀·입력 복원");
            }
            Check(JsonUtility.ToJson(source) == sourceJson, "Asset·MemoryPack 실제 씬 검사 후 원본 레벨 불변");
        }

        private static async UniTask ProgressFrame(PuzzleGameSession session, float seconds)
        {
            FieldInfo preparing = typeof(PuzzleGameSession).GetField("preparingEffects", BindingFlags.Instance | BindingFlags.NonPublic);
            float deadline = Time.realtimeSinceStartup + 20;
            while ((bool)preparing.GetValue(session) && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
            if ((bool)preparing.GetValue(session) || session.HasFailed) throw new InvalidOperationException("표시 검사 효과 준비 실패: " + session.Message);
            Invoke(session, "TickProgress", seconds);
            if (session.IsPresenting) Invoke(session, "AdvancePresentation", seconds); else Invoke(session, "Advance");
            await UniTask.Yield();
        }

        private static string StateSnapshot(object state)
            => (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { state });

        private static async UniTask CheckCollectionPosition(PuzzleGameSession session, PuzzleScreenView screen, PuzzleHudView hud)
        {
            PuzzleProgressFeedback.Flight flight = session.ProgressFeedback.Flights[0];
            Invoke(session, "TickProgress", .16f);
            PuzzleScreenLayout layout = screen.GetComponent<PuzzleScreenLayout>();
            Vector2 size = new Vector2(Screen.width, Screen.height);
            Rect safe = new Rect(17, 24, size.x - 29, size.y - 65);
            layout.ApplyLayout(safe, size); Canvas.ForceUpdateCanvases(); hud.Frame(session);
            Image icon = screen.GetComponentsInChildren<Image>().Single(image => image.name == "MissionCollection" + flight.Slot);
            PuzzleMissionView target = hud.GetComponentsInChildren<PuzzleMissionView>().First();
            Vector2 start = session.BoardCamera.WorldToScreenPoint(session.CollectionWorldPosition(flight.Source.Value));
            Vector2 end = RectTransformUtility.WorldToScreenPoint(null, target.Icon.position);
            Vector2 middle = RectTransformUtility.WorldToScreenPoint(null, icon.rectTransform.position);
            Check(flight.Progress >= .5f && Vector2.Distance(middle, start) > 3 && Vector2.Distance(middle, end) > 3 && !icon.raycastTarget,
                "실제 수집 중간 위치가 원점·목적지와 구분되고 UI 입력 비차단");
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            ScreenCapture.CaptureScreenshot(Output + "collection-middle.png"); await UniTask.Delay(150, ignoreTimeScale: true);
            Invoke(session, "TickProgress", .32f * .99f - flight.Progress * .32f);
            layout.ApplyLayout(safe, size); Canvas.ForceUpdateCanvases(); hud.Frame(session);
            end = RectTransformUtility.WorldToScreenPoint(null, target.Icon.position);
            Vector2 near = RectTransformUtility.WorldToScreenPoint(null, icon.rectTransform.position);
            Check(Vector2.Distance(near, end) < 3 && session.DisplayedMissionProgress(0) == 0,
                "안전 영역 변경 후 수집이 실제 HUD 아이콘 3px 이내 도착·수치 대기");
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            ScreenCapture.CaptureScreenshot(Output + "collection-arrival.png"); await UniTask.Delay(150, ignoreTimeScale: true);
            layout.ApplyLayout(Screen.safeArea, size); Canvas.ForceUpdateCanvases();
        }
    }
}
