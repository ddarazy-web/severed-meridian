using System;
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
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static partial class TutorialRocketLevelVerification
{
    private const string PlayKey = "Tutorial.Stage05.Editor.";
    private const string RecordKey = "Puzzle.EditorLaunch.tutorialCompleted.";
    static TutorialRocketLevelVerification()
    {
        Application.logMessageReceived += (message, stack, kind) =>
        {
            if (!SessionState.GetBool(PlayKey + "active", false) || kind != LogType.Exception) return;
            PuzzleGameSession current = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
            File.AppendAllText(Output + "/runtime-exceptions.txt", "case=" + SessionState.GetInt(PlayKey + "case", -1) +
                " playing=" + Application.isPlaying + " changing=" + EditorApplication.isPlayingOrWillChangePlaymode +
                " ready=" + current?.IsReady + " restart=" + current?.IsRestarting + " enabled=" + current?.isActiveAndEnabled +
                "\n" + message + "\n" + stack + "\n");
        };
        EditorApplication.playModeStateChanged += change =>
        {
            if (!SessionState.GetBool(PlayKey + "active", false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
                VerifyEditorPlay().Forget(error => FinishEditorVerification(error));
            if (change == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += NextEditorCase;
        };
    }

    public static void Reentry()
    {
        Directory.CreateDirectory(Output); Results.Clear();
        Results.AddRange(File.ReadAllLines(Output + "/editor-final-green-results.txt"));
        VerifyData(AssetDatabase.LoadAssetAtPath<LevelDefinition>(LevelPath));
        File.WriteAllLines(Output + "/run-results.txt", Results);
        StartEditorVerification(5);
    }

    public static void RocketPresentation()
    {
        Results.Clear();
        try
        {
            typeof(PuzzlePowerAnimationVerification).GetMethod("VerifyRocketBodyArrival", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Results.AddRange((System.Collections.Generic.List<string>)typeof(PuzzlePowerAnimationVerification).GetField("results", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null));
            File.WriteAllLines(Output + "/rocket-presentation-results.txt", Results); EditorApplication.Exit(0);
        }
        catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Output + "/rocket-presentation-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void StartEditorVerification(int previousCase = -1)
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 에디터 검사만 허용");
        SessionState.SetBool(PlayKey + "record1", SessionState.GetBool(RecordKey + "1", false));
        File.WriteAllText(Output + "/runtime-exceptions.txt", "");
        SessionState.SetBool(PlayKey + "record2", SessionState.GetBool(RecordKey + "2", false));
        foreach (int number in new[] { 1, 2 })
        {
            string key = "MoonRabbit.Tutorial.Completed." + number;
            SessionState.SetBool(PlayKey + "playerHas" + number, PlayerPrefs.HasKey(key));
            SessionState.SetInt(PlayKey + "playerValue" + number, PlayerPrefs.GetInt(key, 0));
        }
        SessionState.SetBool(RecordKey + "1", true);
        SessionState.SetBool(RecordKey + "2", false);
        PuzzleUIRenderVerification.RememberSize();
        SessionState.SetInt(PlayKey + "case", previousCase);
        SessionState.SetBool(PlayKey + "active", true);
        NextEditorCase();
    }

    private static void NextEditorCase()
    {
        if (!SessionState.GetBool(PlayKey + "active", false)) return;
        try
        {
            LevelEditorWindow previous = EditorUtility.InstanceIDToObject(SessionState.GetInt(PlayKey + "owner", 0)) as LevelEditorWindow;
            if (previous != null) previous.Close();
            int index = SessionState.GetInt(PlayKey + "case", -1) + 1;
            SessionState.SetInt(PlayKey + "case", index);
            if (index == 8) { FinishEditorVerification(null); return; }
            if (index < 2) SessionState.SetBool(RecordKey + "2", false);
            if (index == 5) { SessionState.SetBool(RecordKey + "1", false); SessionState.SetBool(RecordKey + "2", false); }
            LevelEditorWindow owner = ScriptableObject.CreateInstance<LevelEditorWindow>();
            SessionState.SetInt(PlayKey + "owner", owner.GetInstanceID());
            owner.ShowUtility(); owner.CreateGUI(); owner.SetLevel(AssetDatabase.LoadAssetAtPath<LevelDefinition>(index == 5 ? "Assets/Data/Levels/Level_01.asset" : LevelPath)); owner.CreateGUI();
            owner.rootVisualElement.Q<PopupField<string>>("game-level-source").value = index % 2 == 0 ? "에셋" : "MemoryPack";
            owner.rootVisualElement.Q<PopupField<string>>("game-tutorial-mode").value = index == 2 ? "항상 실행" : index == 3 ? "실행 안 함" : "자동";
            owner.rootVisualElement.Q<IntegerField>("game-level-seed").value = 9999;
            UnityEngine.UIElements.Button button = owner.rootVisualElement.Q<UnityEngine.UIElements.Button>("game-play-level");
            using NavigationSubmitEvent click = NavigationSubmitEvent.GetPooled(); click.target = button; button.SendEvent(click);
            Check(PuzzleEditorLauncher.IsBusy, "실제 에디터 2레벨 선택/게임 플레이 버튼 " + index);
            File.WriteAllLines(Output + "/run-results.txt", Results);
        }
        catch (Exception error) { FinishEditorVerification(error); }
    }

    private static async UniTask UntilPlay(Func<bool> predicate)
    {
        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await UniTask.WaitUntil(predicate, cancellationToken: timeout.Token);
    }
    private static void ClickNext(TutorialOverlayView view)
        => ExecuteEvents.Execute(view.Next.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    private static void Swipe(PuzzleBoardInput input, PuzzleGameSession session, BoardCoordinate first, BoardCoordinate second)
    {
        Vector2 start = session.BoardCamera.WorldToScreenPoint(session.TutorialCellWorldPosition(first));
        Vector2 end = session.BoardCamera.WorldToScreenPoint(session.TutorialCellWorldPosition(second));
        foreach (string method in new[] { "BeginPointer", "UpdatePointer", "EndPointer" })
            typeof(PuzzleBoardInput).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input,
                new object[] { -1, method == "BeginPointer" ? start : end });
    }
    private static string StateSummary(PuzzleGameSession session)
        => string.Join("|", session.State.Cells.Select(cell => $"{cell.Coordinate}:{cell.Content}:{cell.Color}:{cell.CoverDurability}:{cell.DustDurability}")) +
            "/" + string.Join(",", session.State.Missions.Select(mission => mission.Progress)) + "/" + session.State.MovesRemaining + "/" + session.State.Random.DrawCount;

    private static async UniTask VerifyEditorPlay()
    {
        Results.Clear(); Results.AddRange(File.ReadAllLines(Output + "/run-results.txt"));
        int index = SessionState.GetInt(PlayKey + "case", 0);
        PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
        await UntilPlay(() => session != null && session.State != null && session.CanShowTutorial);
        PuzzleBoardInput input = UnityEngine.Object.FindFirstObjectByType<PuzzleBoardInput>();
        TutorialOverlayView view = UnityEngine.Object.FindFirstObjectByType<TutorialOverlayView>(FindObjectsInactive.Include);
        int writes = 0;
        TutorialExecutionContext context = (TutorialExecutionContext)typeof(PuzzleGameSession).GetField("tutorialContext", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
        FieldInfo writeField = typeof(TutorialExecutionContext).GetField("write", BindingFlags.Instance | BindingFlags.NonPublic);
        Action<int> originalWrite = (Action<int>)writeField.GetValue(context);
        // 기존 에디터 기록 경로를 유지한 채 호출 횟수만 관찰한다.
        writeField.SetValue(context, new Action<int>(number => { writes++; originalWrite(number); }));
        if (index >= 6)
        {
            Check(session.State.LevelNumber == 2 && session.TutorialState.StepIndex == 0 && view.Next.interactable,
                "중단 후 새 에디터 게임 세션 첫 단계 " + index);
            if (index == 6)
            {
                SessionState.SetString(PlayKey + "interruptedBoard", StateSummary(session));
                ClickNext(view); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                Check(session.TutorialState.StepIndex == 1, "실제 2레벨 안내 중단 시점");
            }
            else Check(StateSummary(session) == SessionState.GetString(PlayKey + "interruptedBoard", ""), "중단 후 새 Asset→MemoryPack 진입은 동일한 고정 보드/시드 재현");
            Check(writes == 0 && !SessionState.GetBool(RecordKey + "2", false), "중단/새 진입은 완료 기록 없음 " + index);
            File.WriteAllLines(Output + "/run-results.txt", Results);
            view.GetComponentInParent<PuzzleScreenView>().enabled = false; EditorApplication.ExitPlaymode();
            return;
        }
        if (index == 5)
        {
            await VerifyActualTransition(session, input, view);
            Check(writes == 1, "1→2 전환은 레벨1 완료 기록만 1회");
            File.WriteAllLines(Output + "/run-results.txt", Results);
            view.GetComponentInParent<PuzzleScreenView>().enabled = false;
            EditorApplication.ExitPlaymode();
            return;
        }
        Check(session.State.LevelNumber == 2, "실제 게임 씬 레벨 2 진입 " + index);
        if (index >= 3)
        {
            Check(session.TutorialState == null && !view.Content.gameObject.activeInHierarchy && session.CanAcceptInput,
                "실행 안 함/완료 자동 생략 " + index);
        }
        else
        {
            Check(session.TutorialState.StepIndex == 0 && session.State.Random.Seed == 12345, "실제 안내 첫 단계/고정 시드 " + index);
            if (index == 0)
            {
                ClickNext(view); await UniTask.Yield();
                await session.RestartAsync(CancellationToken.None); await UntilPlay(() => session.CanShowTutorial);
                Check(session.TutorialState.StepIndex == 0 && !SessionState.GetBool(RecordKey + "2", false), "중단 재시작은 첫 단계 재현·완료 미기록");
            }
            for (int step = 0; step < 5; step++)
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                Check(session.TutorialState.StepIndex == step && view.Content.gameObject.activeInHierarchy, "실제 안내 단계 표시 " + index + "/" + step);
                if (step == 1 || step == 3)
                {
                    BoardCoordinate first = session.TutorialState.First.Value, second = session.TutorialState.Second.Value;
                    foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(720, 1280) })
                    {
                        PuzzleUIRenderVerification.SetSize(size.x, size.y);
                        await UntilPlay(() => Screen.width == size.x && Screen.height == size.y);
                        await UniTask.DelayFrame(3, PlayerLoopTiming.LastPostLateUpdate); Canvas.ForceUpdateCanvases();
                        RectTransform cell = view.Content.Find("Cell" + (first.Row * 9 + first.Column)) as RectTransform;
                        Results.Add("INFO 강조 actual=" + (cell != null ? RectTransformUtility.WorldToScreenPoint(null, cell.position).ToString() : "없음") +
                            " expected=" + session.BoardCamera.WorldToScreenPoint(session.TutorialCellWorldPosition(first)));
                        Check(cell != null && Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null, cell.position),
                            session.BoardCamera.WorldToScreenPoint(session.TutorialCellWorldPosition(first))) < 2,
                            "실제 강조 좌표 " + index + "/" + step + "/" + size);
                        Check(view.Finger.gameObject.activeInHierarchy, "교환 손가락 표시 " + size);
                        Vector3[] corners = new Vector3[4]; view.Bubble.GetWorldCorners(corners);
                        Rect bubble = new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y);
                        foreach (BoardCoordinate target in new[] { first, second })
                        {
                            RectTransform targetCell = view.Content.Find("Cell" + (target.Row * 9 + target.Column)) as RectTransform;
                            targetCell.GetWorldCorners(corners);
                            Rect targetBounds = new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y);
                            Check(!bubble.Overlaps(targetBounds), "말풍선이 교환 칸을 가리지 않음 " + target + "/" + size);
                            view.Finger.GetWorldCorners(corners);
                            Check(!new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y).Overlaps(targetBounds),
                                "손가락이 지정 교환 칸을 가리지 않음 " + target + "/" + size);
                            Check(!targetCell.GetComponent<UnityEngine.UI.Graphic>().raycastTarget && !view.Finger.GetComponent<UnityEngine.UI.Graphic>().raycastTarget,
                                "강조/손가락 장식은 입력을 가로채지 않음");
                        }
                        if (index == 0) { ScreenCapture.CaptureScreenshot(Output + "/rocket-step" + step + "-" + size.x + "x" + size.y + ".png"); await UniTask.Delay(150, ignoreTimeScale: true); }
                    }
                    Check(!session.CanActivateBlock(first) && !session.CanPreviewSwap(new BoardCoordinate(0, 0), new BoardCoordinate(0, 1)), "실제 세션 비지정 입력 거절 " + step);
                    int moves = session.State.MovesRemaining;
                    Swipe(input, session, first, second);
                    await UntilPlay(() => session.TutorialState.StepIndex > step);
                    Check(session.State.MovesRemaining == moves - 1 && !session.IsPresenting && !session.HasProgressFeedback,
                        "실제 포인터 교환·이동·낙하/수집 표시 종료 " + step);
                    if (step == 1) Check(session.State.CellAt(Spawn).Content == RuntimeContent.Rocket && session.State.CellAt(Spawn).RocketDirection == RocketDirection.Vertical, "화면 생성 로켓 유지/방향");
                }
                else
                {
                    LevelRuntimeState beforeState = session.State;
                    string beforeSummary = StateSummary(session);
                    ClickNext(view);
                    if (step == 4) Check(ReferenceEquals(beforeState, session.State) && StateSummary(session) == beforeSummary,
                        "종료 설명은 보드·미션·이동·난수 보존");
                }
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            }
            Check(session.TutorialState.State == TutorialProgressState.Completed && session.CanAcceptInput && !view.Content.gameObject.activeInHierarchy &&
                session.State.MovesRemaining == 18 && SessionState.GetBool(RecordKey + "2", false), "완료/같은 보드 자유 플레이·시험 기록 " + index);
            Check(writes == (index < 2 ? 1 : 0), "실제 완료 기록 호출 횟수 " + index + " = " + writes);
            ClickNext(view); await UniTask.Yield();
            Check(writes == (index < 2 ? 1 : 0), "완료 뒤 중복 다음은 재기록하지 않음");
            string summary = StateSummary(session);
            if (index == 0) SessionState.SetString(PlayKey + "assetState", summary);
            if (index == 1) Check(summary == SessionState.GetString(PlayKey + "assetState", ""), "실제 에디터 Asset/MemoryPack 최종 보드·미션·이동·난수 동등");
            ActionCandidate ordinary = ActionQuery.Find(session.State).First(action => action.Second.HasValue);
            Swipe(input, session, ordinary.First, ordinary.Second.Value);
            await UntilPlay(() => session.State.MovesRemaining == 17 && session.CanAcceptInput && !session.IsPresenting && !session.HasProgressFeedback);
            Check(session.TutorialState.State == TutorialProgressState.Completed, "안내 종료 후 실제 일반 교환 " + index);
            if (index < 2)
            {
                await session.RestartAsync(CancellationToken.None); await UntilPlay(() => session.CanShowTutorial);
                Check(session.TutorialState == null && session.State.Random.Seed == 9999, "완료 자동 재시작은 안내 생략·일반 시드");
            }
        }
        Check(SessionState.GetBool(RecordKey + "1", false), "레벨 1 시험 기록 분리 " + index);
        File.WriteAllLines(Output + "/run-results.txt", Results);
        // 이 배치 검사의 화면 구독을 먼저 해제한 뒤 Addressables를 종료하는 편집 모드로 복귀한다.
        // 종료 중 파괴되는 네이티브 Sprite를 HUD의 마지막 프레임이 다시 조회하지 않게 한다.
        view.GetComponentInParent<PuzzleScreenView>().enabled = false;
        EditorApplication.ExitPlaymode();
    }

    private static async UniTask VerifyActualTransition(PuzzleGameSession session, PuzzleBoardInput input, TutorialOverlayView view)
    {
        Check(session.State.LevelNumber == 1 && session.TutorialState.StepIndex == 0 && session.LevelAdvanceEnabled, "실제 에디터 MemoryPack 1레벨 안내 진입");
        ClickNext(view); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        Swipe(input, session, session.TutorialState.First.Value, session.TutorialState.Second.Value);
        await UntilPlay(() => session.TutorialState.StepIndex == 2);
        TutorialBoardAdapter previous = (TutorialBoardAdapter)typeof(PuzzleGameSession).GetField("tutorial", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
        TutorialActionTicket ticket = (TutorialActionTicket)typeof(TutorialBoardAdapter).GetField("ticket", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(previous);
        // 전환 경계만 격리하기 위해 이 배치 인스턴스의 미션을 승리 조건으로 만든다. 출시 데이터는 수정하지 않는다.
        foreach (RuntimeMission mission in session.State.Missions) typeof(RuntimeMission).GetProperty("Progress").SetValue(mission, mission.Target);
        ClickNext(view); await UniTask.Yield(); previous.Executor.SkipLastPang();
        await UntilPlay(() => session.ResultReady);
        Check(SessionState.GetBool(RecordKey + "1", false) && !SessionState.GetBool(RecordKey + "2", false), "실제 레벨별 완료 기록 분리 전환 전");
        string originalSession = session.LogicalSessionId;
        Check(await session.AdvanceLevelAsync(CancellationToken.None), "출시 팩으로 실제 1→2 전환");
        Check(session.IsStartingFeedback && !session.TryAdvanceTutorial(), "전환 시작 표시 중 늦은 다음 입력 차단");
        await UntilPlay(() => session.CanShowTutorial); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        string before = StateSummary(session);
        previous.Progress.ReportPresentationComplete(ticket); previous.ReportAction(true); previous.Tick(true, false);
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        Check(previous.Executor == null && session.State.LevelNumber == 2 && session.LogicalSessionId != originalSession &&
            session.TutorialState.StepIndex == 0 && session.TutorialState.State == TutorialProgressState.AwaitDescription && StateSummary(session) == before &&
            !SessionState.GetBool(RecordKey + "2", false), "1레벨 늦은 결과/표시 신호는 2레벨 보드·단계·기록에 영향 없음");
        Check(view.Content.gameObject.activeInHierarchy && view.Next.interactable, "전환된 2레벨의 실제 설명/다음 UI");
    }

    private static void FinishEditorVerification(Exception error)
    {
        foreach (int number in new[] { 1, 2 })
        {
            string key = "MoonRabbit.Tutorial.Completed." + number;
            if (PlayerPrefs.HasKey(key) != SessionState.GetBool(PlayKey + "playerHas" + number, false) ||
                PlayerPrefs.GetInt(key, 0) != SessionState.GetInt(PlayKey + "playerValue" + number, 0))
                error ??= new InvalidOperationException("시험이 실제 플레이어 완료 기록을 변경했습니다: " + number);
            else Results.Add("PASS 실제 플레이어 완료 키 존재/값 보존 " + number);
        }
        if (error == null && new FileInfo(Output + "/runtime-exceptions.txt").Length > 0)
            error = new InvalidOperationException("실제 게임 검사 중 예외 기록이 남았습니다. runtime-exceptions.txt 확인");
        SessionState.SetBool(PlayKey + "active", false);
        SessionState.SetBool(RecordKey + "1", SessionState.GetBool(PlayKey + "record1", false));
        SessionState.SetBool(RecordKey + "2", SessionState.GetBool(PlayKey + "record2", false));
        LevelEditorWindow owner = EditorUtility.InstanceIDToObject(SessionState.GetInt(PlayKey + "owner", 0)) as LevelEditorWindow;
        if (owner != null) owner.Close();
        PuzzleUIRenderVerification.RestoreSize();
        if (error != null) { Results.Add("FAIL " + error); Debug.LogException(error); }
        File.WriteAllLines(Output + "/run-results.txt", Results);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
