using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using GameScreen;
using GameScreen.Editor;
using Levels;
using Levels.Editor;
using Simulation;
using Tutorial;
using Tutorial.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

[InitializeOnLoad]
public static partial class TutorialGuidanceVerification
{
    private const string PlayKey = "Tutorial.Stage04.Play";
    private const string TempOverlay = "Assets/Prefabs/UI/Puzzle/__TutorialStage04Play.prefab";
    static TutorialGuidanceVerification()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PlayKey, false)) return;
            SessionState.SetBool(PlayKey, false);
            VerifyPlay().Forget(error => { Debug.LogException(error); EditorApplication.Exit(1); });
        };
    }
    private static void StartPlayVerification()
    {
        if (!Application.isBatchMode) throw new Exception("별도 배치 검사에서만 임시 씬을 사용한다.");
        if (File.Exists(TempOverlay)) throw new Exception("소유권 없는 임시 프리팹 존재");
        PuzzleTutorialAssets.GenerateOverlay(TempOverlay);
        PuzzleUIRenderVerification.RememberSize(); PuzzleUIRenderVerification.SetSize(1280, 720);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(PlayKey, true); EditorApplication.EnterPlaymode();
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        File.AppendAllText("Logs/Tutorial/Stage04/guidance-results.txt", "PASS " + message + "\n");
    }
    private static async UniTask Until(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await UniTask.WaitUntil(condition, cancellationToken: timeout.Token);
    }
    private static void Click(UnityEngine.UI.Button button)
        => ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);

    private static async UniTask VerifyPlay()
    {
        int exit = 0; GameObject owner = null, screen = null; PuzzleWorldBoard board = null; LevelDefinition level = null;
        try
        {
            level = LevelTutorialDataVerification.Fixture(); level.Tutorial.steps.Clear(); level.Tutorial.supply.sources.Clear(); level.Tutorial.seed = 6789;
            BoardCoordinate target = new BoardCoordinate(2, 2);
            level.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "같은 색을 맞춰 고물을 정리해요.", highlights = new List<BoardCoordinate> { target } });
            level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Item, item = BoardItem.Hammer, instructions = "무료 망치를 눌러 표시된 칸을 정리하세요.", hasFirst = true, first = target });
            level.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "이제 자유롭게 정리해 보세요." });
            LevelRuntimeState initial = StartingBoardBuilder.Build(level, 6789).State;
            foreach (RuntimeSource source in initial.Supply.Sources)
                level.Tutorial.supply.sources.Add(new ElementSupplySourceDefinition { coordinate = source.Coordinate, mode = SupplyMode.Fixed, exhaustion = SupplyExhaustion.Stop,
                    items = Enumerable.Range(0, 64).Select(index => new ElementSupplyItemDefinition { definitionId = "supply.normal.fixed", color = initial.Colors[(index * 2 + source.Coordinate.Column) % initial.Colors.Count] }).ToList() });
            board = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PuzzleWorldBoard>(PuzzleGameAssets.Folder + "/PuzzleWorldBoard.prefab"));
            owner = new GameObject("TutorialGuidanceSession");
            Camera camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(owner.transform); camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.cullingMask &= ~(1 << 5);
            PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera);
            HashSet<int> completed = new HashSet<int>(); int writes = 0;
            session.ConfigureTutorial(TutorialExecutionContext.CreateEditor(TutorialRunMode.Automatic, completed.Contains, number => { completed.Add(number); writes++; }));
            PuzzleBoardInput input = owner.AddComponent<PuzzleBoardInput>(); input.Configure(session, board, camera);
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem));
            screen = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PuzzleUIAssets.Folder + "/PuzzleScreen.prefab"));
            TutorialOverlayView existingOverlay = screen.GetComponentInChildren<TutorialOverlayView>(true);
            GameObject overlayRoot = existingOverlay != null ? existingOverlay.gameObject : UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TempOverlay), screen.transform.Find("SafeArea"));
            screen.GetComponent<PuzzleScreenView>().Configure(session, input);
            TutorialOverlayView view = overlayRoot.GetComponent<TutorialOverlayView>();
            await session.InitializeAsync(level, 12345, CancellationToken.None); level = null;
            await Until(() => !session.IsStartingFeedback); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Check(view.Content.gameObject.activeInHierarchy && view.Next.interactable && writes == 0, "실제 시작 표시 후 안내·다음 표시, 완료 미기록");
            foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(720, 1280) })
            {
                PuzzleUIRenderVerification.SetSize(size.x, size.y);
                await Until(() => Screen.width == size.x && Screen.height == size.y);
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate); Canvas.ForceUpdateCanvases();
                RectTransform cell = view.Content.Find("Cell20") as RectTransform;
                Vector2 actual = RectTransformUtility.WorldToScreenPoint(null, cell.position);
                Vector2 expected = camera.WorldToScreenPoint(session.TutorialCellWorldPosition(target));
                Check(Vector2.Distance(actual, expected) < 2, "실제 방향 변경 시 강조 좌표 일치 " + size);
                Vector3[] corners = new Vector3[4]; view.Bubble.GetWorldCorners(corners);
                Rect balloon = new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y);
                cell.GetWorldCorners(corners);
                Rect cellBounds = new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y);
                Check(!balloon.Overlaps(cellBounds), "말풍선은 지정 대상 칸 전체를 가리지 않음 " + size);
                ScreenCapture.CaptureScreenshot("Logs/Tutorial/Stage04/guidance-" + size.x + "x" + size.y + ".png");
                await UniTask.Delay(150, ignoreTimeScale: true);
            }
            PuzzleScreenLayout layout = screen.GetComponent<PuzzleScreenLayout>();
            layout.enabled = false;
            layout.ApplyLayout(new Rect(32, 48, Screen.width - 64, Screen.height - 96), new Vector2(Screen.width, Screen.height));
            Canvas.ForceUpdateCanvases(); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            RectTransform insetCell = view.Content.Find("Cell20") as RectTransform;
            Check(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null, insetCell.position), camera.WorldToScreenPoint(session.TutorialCellWorldPosition(target))) < 2,
                "인셋 Safe Area에서도 실제 강조 좌표 일치");
            Vector3 originalCamera = camera.transform.position; camera.transform.position += new Vector3(.15f, .2f, 0);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Check(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null, insetCell.position), camera.WorldToScreenPoint(session.TutorialCellWorldPosition(target))) < 2,
                "카메라 이동 후 안내 좌표 갱신");
            camera.transform.position = originalCamera; layout.enabled = true; layout.ApplyLayout(Screen.safeArea, new Vector2(Screen.width, Screen.height));
            PuzzlePopupBinding popup = screen.GetComponent<PuzzlePopupBinding>(); PopupUI.PopupHandle pause = popup.OpenPause();
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Check(session.IsPaused && !view.Content.gameObject.activeInHierarchy && session.TutorialState.StepIndex == 0, "실제 팝업은 안내와 입력을 정지");
            PopupUI.PopupHandle description = popup.OpenDescription("중첩 설명"); popup.Service.Close(pause);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Check(!view.Content.gameObject.activeInHierarchy && input.IsUIBlocked && session.TutorialState.StepIndex == 0, "중간 팝업 제거 후 최상위 팝업 입력 유지");
            popup.Service.Close(description); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Click(view.Next); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Check(session.TutorialState.StepIndex == 1 && view.FreeBadge.gameObject.activeInHierarchy, "실제 다음 버튼과 무료 체험 표시 연결");
            UnityEngine.UI.Button hammer = screen.GetComponentInChildren<PuzzleItemBarView>().ButtonRect(BoardItem.Hammer).GetComponent<UnityEngine.UI.Button>(); Click(hammer);
            Check(input.SelectedItem == BoardItem.Hammer, "기존 아이템 버튼으로 무료 망치 선택");
            Vector2 point = camera.WorldToScreenPoint(session.TutorialCellWorldPosition(target));
            typeof(PuzzleBoardInput).GetMethod("BeginPointer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, new object[] { -1, point });
            typeof(PuzzleBoardInput).GetMethod("EndPointer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, new object[] { -1, point });
            await Until(() => session.TutorialState.StepIndex == 2); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Check(writes == 0 && view.Next.gameObject.activeInHierarchy, "실제 망치·낙하·수집 완료 후 마지막 설명, 완료 미기록");
            Click(view.Next); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Check(writes == 1 && session.TutorialState.State == TutorialProgressState.Completed && !view.Content.gameObject.activeInHierarchy, "마지막 다음 후 완료 1회 기록·안내 해제");
            await session.RestartAsync(CancellationToken.None); await Until(() => !session.IsStartingFeedback);
            Check(session.TutorialState == null && session.State.Random.Seed == 12345 && writes == 1, "완료 자동 재시작은 일반 시드·공급으로 생략");
            string assetState = await VerifyRepresentative(false, board, camera, screen);
            string packState = await VerifyRepresentative(true, board, camera, screen);
            Check(assetState == packState, "Asset/팩3 실제 안내·스와이프·최종 보드/미션/이동/난수 동등");
            await VerifyAdditionalGuidance(board, camera, screen);
        }
        catch (Exception error) { File.AppendAllText("Logs/Tutorial/Stage04/guidance-results.txt", "FAIL " + error + "\n"); Debug.LogException(error); exit = 1; }
        finally
        {
            if (level != null) UnityEngine.Object.Destroy(level);
            if (screen != null) UnityEngine.Object.Destroy(screen);
            if (owner != null) UnityEngine.Object.Destroy(owner);
            if (board != null) UnityEngine.Object.Destroy(board.gameObject);
            AssetDatabase.DeleteAsset(TempOverlay); PuzzleUIRenderVerification.RestoreSize(); EditorApplication.Exit(exit);
        }
    }

    private static async UniTask<string> VerifyRepresentative(bool packed, PuzzleWorldBoard board, Camera camera, GameObject screen)
    {
        LevelDefinition definition = packed ? LevelPackCodec.ReadLevel(File.ReadAllBytes(Levels.Editor.LevelPackBuild.FilePath(1)), 1)
            : UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset"));
        GameObject owner = new GameObject("RepresentativeTutorial");
        try
        {
            PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera); session.ConfigureTutorial(TutorialExecutionContext.CreateTest(1));
            PuzzleBoardInput input = owner.AddComponent<PuzzleBoardInput>(); input.Configure(session, board, camera);
            screen.GetComponent<PuzzleScreenView>().Configure(session, input);
            TutorialOverlayView view = screen.GetComponentInChildren<TutorialOverlayView>(true);
            await session.InitializeAsync(definition, 9999, CancellationToken.None); definition = null;
            await Until(() => !session.IsStartingFeedback); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Click(view.Next); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Check(view.Finger.gameObject.activeInHierarchy && session.TutorialState.State == TutorialProgressState.AwaitAction, "대표 안내의 실제 교환 손가락 표시 " + packed);
            if (!packed)
            {
                ScreenCapture.CaptureScreenshot("Logs/Tutorial/Stage04/guidance-swipe.png");
                await UniTask.Delay(150, ignoreTimeScale: true);
            }
            BoardCoordinate first = session.TutorialState.First.Value, second = session.TutorialState.Second.Value;
            Vector2 start = camera.WorldToScreenPoint(session.TutorialCellWorldPosition(first)), end = camera.WorldToScreenPoint(session.TutorialCellWorldPosition(second));
            typeof(PuzzleBoardInput).GetMethod("BeginPointer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, new object[] { -1, start });
            typeof(PuzzleBoardInput).GetMethod("UpdatePointer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, new object[] { -1, end });
            typeof(PuzzleBoardInput).GetMethod("EndPointer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, new object[] { -1, end });
            await Until(() => session.TutorialState.StepIndex == 2); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Check(session.State.MovesRemaining == 19 && !session.IsPresenting && !session.HasProgressFeedback, "대표 3매칭 실제 입력·이동 차감·표시 완료 " + packed);
            Click(view.Next); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Check(session.TutorialState.State == TutorialProgressState.Completed && session.CanAcceptInput && !view.Content.gameObject.activeInHierarchy,
                "대표 안내 종료 후 동일 보드 자유 플레이 " + packed);
            return string.Join("|", session.State.Cells.Select(cell => $"{cell.Coordinate}:{cell.Content}:{cell.Color}:{cell.CoverDurability}:{cell.DustDurability}")) +
                "/" + string.Join(",", session.State.Missions.Select(mission => mission.Progress)) + "/" + session.State.MovesRemaining + "/" + session.State.Random.DrawCount;
        }
        finally { if (definition != null) UnityEngine.Object.Destroy(definition); UnityEngine.Object.Destroy(owner); await UniTask.Yield(); }
    }

    private static void Pointer(PuzzleBoardInput input, PuzzleGameSession session, BoardCoordinate first, BoardCoordinate? second = null)
    {
        Vector2 start = session.BoardCamera.WorldToScreenPoint(session.TutorialCellWorldPosition(first));
        Vector2 end = session.BoardCamera.WorldToScreenPoint(session.TutorialCellWorldPosition(second ?? first));
        foreach (string method in new[] { "BeginPointer", "UpdatePointer", "EndPointer" })
            typeof(PuzzleBoardInput).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, new object[] { -1, method == "BeginPointer" ? start : end });
    }

    private static void FixedSupply(LevelDefinition level)
    {
        level.Tutorial.seed = 12345;
        LevelRuntimeState initial = StartingBoardBuilder.Build(level, 12345).State;
        if (initial.Supply.Sources.Count == 0)
        {
            LevelDefinition sourceLayout = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
            JsonUtility.FromJsonOverwrite("{\"supply\":" + JsonUtility.ToJson(sourceLayout.Supply) + "}", level);
            initial = StartingBoardBuilder.Build(level, 12345).State;
        }
        foreach (RuntimeSource source in initial.Supply.Sources)
            level.Tutorial.supply.sources.Add(new ElementSupplySourceDefinition { coordinate = source.Coordinate, mode = SupplyMode.Fixed, exhaustion = SupplyExhaustion.Stop,
                items = Enumerable.Range(0, 128).Select(index => new ElementSupplyItemDefinition { definitionId = "supply.normal.fixed", color = initial.Colors[(index * 2 + source.Coordinate.Column) % initial.Colors.Count] }).ToList() });
    }

    private static async UniTask VerifyAdditionalGuidance(PuzzleWorldBoard board, Camera camera, GameObject screen)
    {
        LevelDefinition rocket = (LevelDefinition)typeof(TutorialGameIntegrationVerification).GetMethod("RocketTutorial", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { false });
        await VerifyActions(rocket, "생성 로켓", board, camera, screen);
        BoardCoordinate origin = new BoardCoordinate(4, 4);
        foreach (InitialBlockKind power in new[] { InitialBlockKind.Rocket, InitialBlockKind.Bomb, InitialBlockKind.Drone, InitialBlockKind.Magnet })
        {
            LevelDefinition powerLevel = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { powerLevel, origin, power, RocketDirection.Horizontal, RabbitColor.Type1 });
            powerLevel.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.PowerSwap, instructions = "표시된 파워를 옆 칸과 교환하세요.", hasFirst = true, first = origin, hasSecond = true, second = new BoardCoordinate(4, 5),
                actionDefinitionId = "power." + power.ToString().ToLowerInvariant() });
            powerLevel.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "파워 안내 완료" });
            FixedSupply(powerLevel);
            await VerifyActions(powerLevel, power.ToString(), board, camera, screen);
        }
        LevelDefinition combination = (LevelDefinition)typeof(CombinationVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { 0, RocketDirection.Horizontal, null });
        combination.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.PowerSwap, instructions = "두 로켓을 교환해 보세요.", actionDefinitionId = "power.rocket",
            hasFirst = true, first = origin, hasSecond = true, second = new BoardCoordinate(4, 5) });
        combination.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "조합 안내 완료" }); FixedSupply(combination);
        await VerifyActions(combination, "로켓 조합", board, camera, screen);
        LevelDefinition items = LevelTutorialDataVerification.Fixture();
        items.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Item, instructions = "무료 교환으로 두 칸을 바꿔 보세요.", item = BoardItem.Swap,
            hasFirst = true, first = new BoardCoordinate(2, 2), hasSecond = true, second = new BoardCoordinate(2, 3) });
        items.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Item, instructions = "무료 섞기를 눌러 보세요.", item = BoardItem.Shuffle });
        items.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "무료 아이템 안내 완료" }); FixedSupply(items);
        await VerifyActions(items, "교환·섞기", board, camera, screen);
        LevelDefinition lastAction = LevelTutorialDataVerification.Fixture();
        lastAction.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Item, instructions = "마지막 무료 망치", item = BoardItem.Hammer,
            hasFirst = true, first = new BoardCoordinate(2, 2) }); FixedSupply(lastAction);
        await VerifyActions(lastAction, "마지막 행동 표시 후 기록", board, camera, screen);
        await VerifyPolicyLifetime(board, camera, screen);
        foreach (bool canceled in new[] { false, true })
        {
            GameObject owner = new GameObject("TutorialFailedFixture");
            LevelDefinition definition = LevelTutorialDataVerification.Fixture();
            try
            {
                definition.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "시작 실패 검사", highlights = new List<BoardCoordinate> { new BoardCoordinate(999, 999) } });
                PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera);
                int writes = 0; session.ConfigureTutorial(TutorialExecutionContext.CreateEditor(TutorialRunMode.Always, number => false, number => writes++));
                PuzzleBoardInput input = owner.AddComponent<PuzzleBoardInput>(); input.Configure(session, board, camera); screen.GetComponent<PuzzleScreenView>().Configure(session, input);
                await session.InitializeAsync(definition, 9999, canceled ? new CancellationToken(true) : CancellationToken.None);
                definition = null; await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                Check(!session.IsReady && session.State == null && session.TutorialState == null && (canceled || session.HasFailed) && writes == 0 && !screen.GetComponentInChildren<TutorialOverlayView>(true).Content.gameObject.activeInHierarchy,
                    "시작 실패/취소는 안내·기록 미게시 " + canceled);
            }
            finally { if (definition != null) UnityEngine.Object.Destroy(definition); UnityEngine.Object.Destroy(owner); await UniTask.Yield(); }
        }
    }

    private static async UniTask VerifyActions(LevelDefinition definition, string label, PuzzleWorldBoard board, Camera camera, GameObject screen)
    {
        GameObject owner = new GameObject("TutorialGuidanceFixture");
        try
        {
            List<LevelValidationIssue> issues = LevelTutorialReplayValidator.Validate(definition);
            Check(issues.Count == 0, "임시 안내 실제 논리 재생 " + label + " · " + string.Join(" · ", issues));
            int writes = 0; HashSet<int> records = new HashSet<int>();
            PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera);
            session.ConfigureTutorial(TutorialExecutionContext.CreateEditor(TutorialRunMode.Always, records.Contains, number => { records.Add(number); writes++; }));
            PuzzleBoardInput input = owner.AddComponent<PuzzleBoardInput>(); input.Configure(session, board, camera);
            screen.GetComponent<PuzzleScreenView>().Configure(session, input);
            TutorialOverlayView view = screen.GetComponentInChildren<TutorialOverlayView>(true);
            await session.InitializeAsync(definition, 9999, CancellationToken.None); definition = null;
            await Until(() => !session.IsStartingFeedback); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            while (session.TutorialState.State != TutorialProgressState.Completed)
            {
                TutorialProgressSnapshot step = session.TutorialState;
                Check(view.Content.gameObject.activeInHierarchy && writes == 0, "실제 안내·완료 미기록 " + label + "/" + step.StepIndex);
                if (step.State == TutorialProgressState.AwaitDescription) Click(view.Next);
                else
                {
                    int moves = session.State.MovesRemaining;
                    Pointer(input, session, new BoardCoordinate(0, 0));
                    Check(session.TutorialState.StepIndex == step.StepIndex && session.State.MovesRemaining == moves, "비지정 실제 포인터 거절 " + label + "/" + step.StepIndex);
                    if (step.Item.HasValue)
                    {
                        Check(view.FreeBadge.gameObject.activeInHierarchy, "무료 아이템 표시 " + step.Item);
                        Click(screen.GetComponentInChildren<PuzzleItemBarView>().ButtonRect(step.Item.Value).GetComponent<UnityEngine.UI.Button>());
                        if (step.First.HasValue) Pointer(input, session, step.First.Value);
                        if (step.Second.HasValue) Pointer(input, session, step.Second.Value);
                    }
                    else Pointer(input, session, step.First.Value, step.Second);
                    Check(!view.Next.gameObject.activeInHierarchy && writes == 0, "실행·표시 대기 중 다음/완료 기록 없음 " + label);
                }
                await Until(() => session.TutorialState.StepIndex > step.StepIndex || session.TutorialState.State == TutorialProgressState.Completed);
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            }
            Check(writes == 1 && !view.Content.gameObject.activeInHierarchy && !session.HasProgressFeedback && !session.IsPresenting,
                "실제 UI 완료·표시 종료·기록 1회 " + label);
        }
        finally { if (definition != null) UnityEngine.Object.Destroy(definition); UnityEngine.Object.Destroy(owner); await UniTask.Yield(); }
    }

    private static async UniTask VerifyPolicyLifetime(PuzzleWorldBoard board, Camera camera, GameObject screen)
    {
        HashSet<int> interruptedRecords = new HashSet<int>(); int interruptedWrites = 0;
        TutorialExecutionContext automatic = TutorialExecutionContext.CreateEditor(TutorialRunMode.Automatic, interruptedRecords.Contains,
            number => { interruptedRecords.Add(number); interruptedWrites++; });
        string firstBoard = null;
        for (int entry = 0; entry < 2; entry++)
        {
            GameObject owner = new GameObject("TutorialInterruptedEntry");
            LevelDefinition definition = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset"));
            try
            {
                PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera); session.ConfigureTutorial(automatic);
                PuzzleBoardInput input = owner.AddComponent<PuzzleBoardInput>(); input.Configure(session, board, camera); screen.GetComponent<PuzzleScreenView>().Configure(session, input);
                await session.InitializeAsync(definition, 9999, CancellationToken.None); definition = null;
                await Until(() => !session.IsStartingFeedback); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                string cells = string.Join("|", session.State.Cells.Select(cell => $"{cell.Coordinate}:{cell.Content}:{cell.Color}"));
                if (entry == 0) firstBoard = cells;
                Check(session.TutorialState.StepIndex == 0 && cells == firstBoard && session.State.Random.Seed == 12345 && interruptedWrites == 0,
                    "자동 미완료 재진입 첫 단계·고정 보드 재현 " + entry);
                Click(screen.GetComponentInChildren<TutorialOverlayView>(true).Next);
                Check(session.TutorialState.StepIndex == 1 && interruptedWrites == 0, "중간 종료는 완료 기록 없음 " + entry);
            }
            finally { if (definition != null) UnityEngine.Object.Destroy(definition); UnityEngine.Object.Destroy(owner); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate); }
        }
        foreach (TutorialRunMode mode in Enum.GetValues(typeof(TutorialRunMode)))
        {
            GameObject owner = new GameObject("TutorialPolicyFixture");
            LevelDefinition definition = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset"));
            try
            {
                PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera);
                int writes = 0; session.ConfigureTutorial(TutorialExecutionContext.CreateEditor(mode, number => true, number => writes++));
                PuzzleBoardInput input = owner.AddComponent<PuzzleBoardInput>(); input.Configure(session, board, camera);
                screen.GetComponent<PuzzleScreenView>().Configure(session, input);
                await session.InitializeAsync(definition, 9999, CancellationToken.None); definition = null;
                await Until(() => !session.IsStartingFeedback); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                Check((session.TutorialState != null) == (mode == TutorialRunMode.Always) && session.State.Random.Seed == (mode == TutorialRunMode.Always ? 12345 : 9999),
                    "완료 시험 기록에 대한 실제 준비 정책 " + mode);
                if (mode == TutorialRunMode.Always)
                {
                    TutorialOverlayView view = screen.GetComponentInChildren<TutorialOverlayView>(true);
                    screen.SetActive(false); Click(view.Next);
                    Check(session.TutorialState.StepIndex == 0 && writes == 0, "화면 해제 후 이전 다음 구독 제거");
                    screen.SetActive(true); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate); Click(view.Next);
                    Check(session.TutorialState.StepIndex == 1 && writes == 0, "화면 재활성화 시 구독 한 번·중간 진행 미기록");
                    await session.RestartAsync(CancellationToken.None); await Until(() => !session.IsStartingFeedback);
                    Check(session.TutorialState.StepIndex == 0 && session.State.Random.Seed == 12345 && writes == 0, "항상 실행 재시작은 처음부터 고정 재현");
                    UnityEngine.Object.Destroy(owner); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                    Click(view.Next); Check(writes == 0 && !view.Content.gameObject.activeInHierarchy, "세션 파기 후 늦은 UI 신호는 기록하지 않음");
                }
            }
            finally { if (definition != null) UnityEngine.Object.Destroy(definition); if (owner != null) UnityEngine.Object.Destroy(owner); await UniTask.Yield(); }
        }
    }
}
