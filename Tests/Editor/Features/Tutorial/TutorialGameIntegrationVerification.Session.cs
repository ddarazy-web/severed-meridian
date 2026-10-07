using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using GameScreen;
using Levels;
using Levels.Editor;
using Simulation;
using Tutorial;
using Tutorial.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

[InitializeOnLoad]
public static partial class TutorialGameIntegrationVerification
{
    private const string PlayKey = "Tutorial.Stage03.GameIntegration";
    static TutorialGameIntegrationVerification()
    {
        EditorApplication.playModeStateChanged += change =>
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PlayKey, false)) return;
            SessionState.SetBool(PlayKey, false);
            VerifySessionAsync().Forget(error => { Debug.LogException(error); EditorApplication.Exit(1); });
        };
    }

    private static void StartSessionVerification()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 검사에서만 임시 씬을 구성한다.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(PlayKey, true);
        EditorApplication.EnterPlaymode();
    }

    private static async UniTask VerifySessionAsync()
    {
        Results.Clear(); Results.AddRange(File.ReadAllLines("Logs/Tutorial/Stage03/integration-results.txt"));
        int exit = 0;
        GameObject owner = null, cameraOwner = null;
        PuzzleWorldBoard board = null;
        LevelDefinition level = null;
        try
        {
            level = LevelTutorialDataVerification.Fixture();
            level.Tutorial.seed = 6789;
            level.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "첫 설명" });
            BoardCoordinate target = new BoardCoordinate(2, 2);
            level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Item, item = BoardItem.Hammer, instructions = "무료 망치",
                hasFirst = true, first = target, results = new System.Collections.Generic.List<TutorialResultDefinition>
                { new TutorialResultDefinition { kind = TutorialResultKind.Removed, definitionId = "supply.normal.fixed" } } });
            level.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "마지막 설명" });
            LevelRuntimeState initial = StartingBoardBuilder.Build(level, level.Tutorial.seed).State;
            Check(initial != null, "실제 세션 튜토리얼 fixture 시작 구성");
            foreach (RuntimeSource source in initial.Supply.Sources)
                level.Tutorial.supply.sources.Add(new ElementSupplySourceDefinition { coordinate = source.Coordinate, mode = SupplyMode.Fixed, exhaustion = SupplyExhaustion.Stop,
                    items = Enumerable.Range(0, 64).Select(index => new ElementSupplyItemDefinition { definitionId = "supply.normal.fixed", color = initial.Colors[(index * 2 + source.Coordinate.Column) % initial.Colors.Count] }).ToList() });
            board = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PuzzleWorldBoard>("Assets/Prefabs/Game/Puzzle/PuzzleWorldBoard.prefab"));
            cameraOwner = new GameObject("TutorialVerificationCamera"); Camera camera = cameraOwner.AddComponent<Camera>(); camera.orthographic = true;
            owner = new GameObject("TutorialVerificationSession"); PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera); session.ConfigureTutorial(TutorialExecutionContext.CreateTest(1));
            await session.InitializeAsync(level, 12345, CancellationToken.None); level = null;
            PropertyInfo snapshotProperty = typeof(PuzzleGameSession).GetProperty("TutorialState");
            MethodInfo next = typeof(PuzzleGameSession).GetMethod("TryAdvanceTutorial");
            Check(session.IsReady && !session.HasFailed && session.State.Random.Seed == 6789, "실제 게임 세션 고정 시드와 사본 준비");
            string initialCells = (string)PrivateCall(typeof(LevelInitialStateVerification), "Snapshot", session.State.Cells);
            int initialDraws = session.State.Random.DrawCount;
            Check(((TutorialProgressSnapshot)snapshotProperty.GetValue(session)).State == TutorialProgressState.AwaitDescription, "실제 게임에서 첫 설명 대기");
            int draws = session.State.Random.DrawCount, moves = session.State.MovesRemaining;
            Check(!session.TrySwap(target, new BoardCoordinate(2, 3)) && !session.TryActivate(target) && !session.TryUseItem(BoardItem.Hammer, target), "첫 설명 중 직접 게임 명령 차단");
            Check(session.State.Random.DrawCount == draws && session.State.MovesRemaining == moves, "거절 명령은 실제 이동·난수를 보존");
            float deadline = Time.realtimeSinceStartup + 30;
            while (session.IsStartingFeedback && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
            Check(session.SetPopupPaused(true) && !(bool)next.Invoke(session, null), "팝업 중 설명 다음 차단");
            session.SetPopupPaused(false);
            Check((bool)next.Invoke(session, null), "실제 화면 시작 표시 종료 후 설명 다음");
            Check(!session.TryUseItem(BoardItem.Shuffle) && !session.TryUseItem(BoardItem.Hammer, new BoardCoordinate(2, 3)), "지정하지 않은 아이템·좌표 차단");
            Check(session.TryUseItem(BoardItem.Hammer, target), "실제 게임에서 지정 무료 망치 실행");
            Check(((TutorialProgressSnapshot)snapshotProperty.GetValue(session)).StepIndex == 1, "실제 연출 완료 전 단계 유지");
            deadline = Time.realtimeSinceStartup + 30;
            while (((TutorialProgressSnapshot)snapshotProperty.GetValue(session)).StepIndex == 1 && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
            TutorialProgressSnapshot final = (TutorialProgressSnapshot)snapshotProperty.GetValue(session);
            Check(!session.HasFailed && final.StepIndex == 2 && final.State == TutorialProgressState.AwaitDescription && !session.IsPresenting && !session.HasProgressFeedback, "실제 논리·낙하·효과·수집 표시 뒤 마지막 설명");
            Check(session.State.MovesRemaining == moves, "무료 망치는 실제 이동을 소비하지 않음");
            LevelRuntimeState stable = session.State; int stableDraws = stable.Random.DrawCount;
            Check((bool)next.Invoke(session, null) && ((TutorialProgressSnapshot)snapshotProperty.GetValue(session)).State == TutorialProgressState.Completed, "실제 세션 튜토리얼 완료");
            Check(ReferenceEquals(stable, session.State) && session.State.Random.DrawCount == stableDraws && session.CanUseItems, "완료 시 같은 보드에서 일반 플레이 복귀");
            await session.RestartAsync(CancellationToken.None);
            Check(!session.IsRestarting && !session.HasFailed && session.TutorialState.StepIndex == 0 && session.TutorialState.State == TutorialProgressState.AwaitDescription, "실제 재시작은 튜토리얼 첫 설명 복원");
            Check(session.State.Random.Seed == 6789 && session.State.Random.DrawCount == initialDraws &&
                (string)PrivateCall(typeof(LevelInitialStateVerification), "Snapshot", session.State.Cells) == initialCells, "재시작의 고정 시드·보드·난수 재현");
            LevelRuntimeState restartState = session.State;
            using (CancellationTokenSource canceled = new CancellationTokenSource())
            {
                canceled.Cancel(); await session.RestartAsync(canceled.Token);
            }
            Check(ReferenceEquals(restartState, session.State) && session.TutorialState.StepIndex == 0 && !session.IsRestarting, "취소된 재시작은 기존 실행·진행 보존");
            TutorialBoardAdapter retained = (TutorialBoardAdapter)typeof(PuzzleGameSession).GetField("tutorial", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
            session.SetPaused(true);
            using (CancellationTokenSource waiting = new CancellationTokenSource())
            {
                UniTask restarting = session.RestartAsync(waiting.Token);
                await UniTask.Yield(); await UniTask.Yield(); await UniTask.Yield();
                Check(session.IsRestarting && ReferenceEquals(restartState, session.State), "일시정지 중 준비한 재시작 후보는 기존 보드를 교체하지 않음");
                waiting.Cancel(); await restarting;
            }
            session.SetPaused(false);
            Check(!session.IsRestarting && ReferenceEquals(retained, typeof(PuzzleGameSession).GetField("tutorial", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session)), "후보 준비 중 취소는 기존 튜토리얼 엔진 보존");
            FieldInfo bytesField = typeof(PuzzleGameSession).GetField("initialBytes", BindingFlags.NonPublic | BindingFlags.Instance);
            byte[] restartBytes = (byte[])bytesField.GetValue(session);
            try { bytesField.SetValue(session, new byte[] { 0 }); await session.RestartAsync(CancellationToken.None); }
            finally { bytesField.SetValue(session, restartBytes); }
            Check(!session.HasFailed && !session.IsRestarting && ReferenceEquals(restartState, session.State) && session.TutorialState.StepIndex == 0, "실패한 재시작 준비도 기존 보드·진행 보존");
            deadline = Time.realtimeSinceStartup + 30;
            while (session.IsStartingFeedback && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
            Check(session.TryAdvanceTutorial() && session.TryUseItem(BoardItem.Hammer, target), "연출 중 재시작 검사 행동 실행");
            TutorialBoardAdapter previous = (TutorialBoardAdapter)typeof(PuzzleGameSession).GetField("tutorial", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
            TutorialActionTicket previousTicket = (TutorialActionTicket)typeof(TutorialBoardAdapter).GetField("ticket", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(previous);
            await session.RestartAsync(CancellationToken.None);
            Check(previous.Executor == null && previous.Progress.State == TutorialProgressState.Cancelled, "실제 재시작은 이전 엔진·실행 연결 해제");
            previous.Progress.ReportPresentationComplete(previousTicket);
            previous.Tick(true, false);
            Check(session.TutorialState.StepIndex == 0 && session.TutorialState.State == TutorialProgressState.AwaitDescription, "이전 행동의 늦은 완료 신호는 새 세션 진행에 영향 없음");
            TutorialBoardAdapter dying = (TutorialBoardAdapter)typeof(PuzzleGameSession).GetField("tutorial", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
            UnityEngine.Object.Destroy(owner); owner = null;
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Check(dying.Executor == null && dying.Progress.State == TutorialProgressState.Cancelled, "실제 오브젝트 파기 시 튜토리얼 수명 정리");
            await VerifyZeroMovesSession(board, camera);
            await VerifyDroneSession(board, camera);
            await VerifyStartFailures(board, camera);
        }
        catch (Exception error) { exit = 1; Results.Add("FAIL 실제 게임 세션 " + error); Debug.LogException(error); }
        finally
        {
            if (level != null) UnityEngine.Object.Destroy(level);
            if (owner != null) UnityEngine.Object.Destroy(owner);
            if (board != null) UnityEngine.Object.Destroy(board.gameObject);
            if (cameraOwner != null) UnityEngine.Object.Destroy(cameraOwner);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            File.WriteAllLines("Logs/Tutorial/Stage03/integration-results.txt", Results);
            EditorApplication.Exit(exit);
        }
    }

    private static async UniTask VerifyZeroMovesSession(PuzzleWorldBoard board, Camera camera)
    {
        GameObject owner = new GameObject("TutorialZeroMovesSession");
        LevelDefinition level = RocketTutorial(false);
        try
        {
            JsonUtility.FromJsonOverwrite("{\"moveCount\":2}", level);
            BoardCoordinate target = new BoardCoordinate(2, 3);
            level.Tutorial.steps.Insert(2, new TutorialStepDefinition { kind = TutorialStepKind.Item, item = BoardItem.Hammer, instructions = "마지막 무료 체험",
                hasFirst = true, first = target });
            PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera); session.ConfigureTutorial(TutorialExecutionContext.CreateTest(1));
            await session.InitializeAsync(level, 99999, CancellationToken.None); level = null;
            Check(session.IsReady && !session.HasFailed, "이동 0 경계 실제 게임 준비 · " + session.Message);
            await WaitTutorial(session, 0);
            PuzzleBoardInput input = owner.AddComponent<PuzzleBoardInput>(); input.Configure(session, board, camera);
            BoardCoordinate donor = new BoardCoordinate(2, 3), spawn = new BoardCoordinate(3, 3), wrong = new BoardCoordinate(3, 2);
            Vector2 Screen(BoardCoordinate coordinate) => camera.WorldToScreenPoint(board.transform.TransformPoint(PuzzleWorldBoard.CellPosition(coordinate)));
            void Pointer(string method, BoardCoordinate coordinate) => typeof(PuzzleBoardInput).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(input, new object[] { 42, Screen(coordinate) });
            Pointer("BeginPointer", wrong); Pointer("UpdatePointer", spawn); Pointer("EndPointer", spawn);
            Check(session.State.MovesRemaining == 2 && session.TutorialState.StepIndex == 0 && !session.CanSelectBlock(wrong), "튜토리얼 비지정 블록 포인터 선택·드래그 거절");
            Pointer("BeginPointer", donor); Pointer("UpdatePointer", spawn); Pointer("EndPointer", spawn);
            Check(session.State.MovesRemaining == 1 && session.IsPresenting, "지정 블록 실제 포인터 스와이프로 로켓 생성");
            await WaitTutorial(session, 1);
            Check(session.TrySwap(new BoardCoordinate(3, 3), new BoardCoordinate(3, 4)), "마지막 이동의 실제 로켓 발동");
            await WaitTutorial(session, 2);
            Check(session.State.MovesRemaining == 0 && session.Outcome == null && session.TutorialState.FreeItemAvailable, "이동 0에서 종료 보류·지정 무료 체험 권한 유지");
            Check(!session.TrySwap(new BoardCoordinate(3, 3), new BoardCoordinate(3, 4)) && !session.TryUseItem(BoardItem.Shuffle), "이동 0 추가 교환·일반 아이템 우회 거절");
            Check(session.TryUseItem(BoardItem.Hammer, target) && !session.TutorialState.FreeItemAvailable && !session.TryUseItem(BoardItem.Hammer, target), "이동 0 지정 무료 체험 성공 1회 소진");
            await WaitTutorial(session, 3);
            Check(session.TutorialState.State == TutorialProgressState.AwaitDescription && session.State.MovesRemaining == 0 && session.Outcome == null, "이동 0 마지막 설명 대기 유지");
            Check(session.TryAdvanceTutorial() && session.TutorialState.State == TutorialProgressState.Completed && session.Outcome?.Kind == BoardOutcomeKind.MovesExhausted, "이동 0 설명 완료 후 종료 판단 재개");
        }
        finally
        {
            if (level != null) UnityEngine.Object.Destroy(level);
            UnityEngine.Object.Destroy(owner);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        }
    }

    private static async UniTask VerifyDroneSession(PuzzleWorldBoard board, Camera camera)
    {
        GameObject owner = new GameObject("TutorialDroneSession");
        LevelDefinition level = (LevelDefinition)PrivateCall(typeof(PowerEffectVerification), "Make");
        try
        {
            // 출시 레벨 증가와 무관하게 다음 구간이 없는 상태를 검사한다.
            if (File.Exists(LevelPackBuild.FilePath(987650011))) throw new InvalidOperationException("검사 전용 구간이 사용 중입니다.");
            JsonUtility.FromJsonOverwrite("{\"levelNumber\":987650011}", level);
            BoardCoordinate drone = new BoardCoordinate(4, 4), neighbor = new BoardCoordinate(4, 5);
            PrivateCall(typeof(PowerEffectVerification), "Place", level, drone, InitialBlockKind.Drone, RocketDirection.Horizontal, RabbitColor.Type1);
            PrivateCall(typeof(PowerEffectVerification), "Crate", level, new BoardCoordinate(6, 6), 1);
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":1,\"count\":1}]}", level);
            level.Tutorial.seed = 12345;
            level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.PowerSwap, instructions = "드론 날리기", actionDefinitionId = "power.drone",
                hasFirst = true, first = drone, hasSecond = true, second = neighbor, results = new System.Collections.Generic.List<TutorialResultDefinition>
                { new TutorialResultDefinition { kind = TutorialResultKind.Activated, definitionId = "power.drone" },
                    new TutorialResultDefinition { kind = TutorialResultKind.Removed, definitionId = "obstacle.crate.wood" } } });
            level.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "드론 착탄 뒤 설명" });
            for (int column = 0; column < 9; column++)
            {
                int sourceColumn = column;
                level.Tutorial.supply.sources.Add(new ElementSupplySourceDefinition { coordinate = new BoardCoordinate(0, column), mode = SupplyMode.Fixed, exhaustion = SupplyExhaustion.Stop,
                    items = Enumerable.Range(0, 64).Select(index => new ElementSupplyItemDefinition { definitionId = "supply.normal.fixed", color = (RabbitColor)((index * 2 + sourceColumn) % 5) }).ToList() });
            }
            PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera); session.ConfigureTutorial(TutorialExecutionContext.CreateTest(1));
            await session.InitializeAsync(level, 555, CancellationToken.None); level = null;
            Check(session.IsReady && !session.HasFailed, "뒤늦은 드론 결과 실제 게임 준비 · " + session.Message);
            await WaitTutorial(session, 0);
            Check(session.TrySwap(drone, neighbor) && session.IsPresenting && session.TutorialState.StepIndex == 0, "드론 논리 결과 후 실제 비행 연출 대기");
            Check(session.Outcome == null && session.State.Missions[0].Remaining == 0, "드론 목표 달성도 설명 완료까지 승리 보류");
            session.SetPopupPaused(true);
            await UniTask.Yield(); await UniTask.Yield();
            Check(session.TutorialState.StepIndex == 0 && session.IsPaused, "드론 비행 중 팝업은 단계 진행 보류");
            session.SetPopupPaused(false);
            await WaitTutorial(session, 1);
            Check(session.TutorialState.State == TutorialProgressState.AwaitDescription && !session.IsPresenting && !session.HasProgressFeedback, "원래 행동의 드론 착탄·수집·낙하가 끝나면 설명 진행");
            Check(session.TryAdvanceTutorial() && session.Outcome?.Kind == BoardOutcomeKind.Won, "드론 튜토리얼 완료 후 실제 승리 재평가");
            TutorialBoardAdapter previous = (TutorialBoardAdapter)typeof(PuzzleGameSession).GetField("tutorial", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
            previous.Executor.SkipLastPang();
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                await UniTask.WaitUntil(() => session.ResultReady, cancellationToken: timeout.Token);
            LevelRuntimeState won = session.State;
            using (CancellationTokenSource canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                Check(!await session.AdvanceLevelAsync(canceled.Token) && ReferenceEquals(won, session.State), "취소된 다음 레벨 준비는 튜토리얼 승리 보드 보존");
            }
            Check(!await session.AdvanceLevelAsync(CancellationToken.None) && ReferenceEquals(won, session.State) && previous.Executor != null, "다음 레벨 준비 실패는 승리 보드·튜토리얼 연결 보존 · " + session.Message);
            await VerifyNextLevelSession(session, previous);
        }
        finally
        {
            if (level != null) UnityEngine.Object.Destroy(level);
            UnityEngine.Object.Destroy(owner);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
        }
    }

    private static async UniTask VerifyStartFailures(PuzzleWorldBoard board, Camera camera)
    {
        GameObject owner = new GameObject("TutorialFailedStart");
        LevelDefinition invalid = RocketTutorial(false);
        try
        {
            invalid.Tutorial.supply.sources.Clear();
            PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera); session.ConfigureTutorial(TutorialExecutionContext.CreateTest(1));
            await session.InitializeAsync(invalid, 12345, CancellationToken.None);
            Check(session.HasFailed && !session.CanAcceptInput && session.State == null && session.TutorialState == null && session.Message.Contains("공급"), "실제 재생 오류는 게임 시작 전에 진단·사본/엔진 미게시");
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Check(invalid == null, "시작 실패의 소유 레벨 사본 파기");
        }
        finally { UnityEngine.Object.Destroy(owner); if (invalid != null) UnityEngine.Object.Destroy(invalid); }
        owner = new GameObject("TutorialCanceledStart");
        LevelDefinition canceledLevel = RocketTutorial(false);
        try
        {
            PuzzleGameSession session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera); session.ConfigureTutorial(TutorialExecutionContext.CreateTest(1));
            using CancellationTokenSource canceled = new CancellationTokenSource(); canceled.Cancel();
            await session.InitializeAsync(canceledLevel, 12345, canceled.Token);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            Check(!session.IsReady && !session.CanAcceptInput && session.State == null && session.TutorialState == null && canceledLevel == null, "취소된 시작은 소유 사본 파기·실행/진행 미게시");
        }
        finally { UnityEngine.Object.Destroy(owner); if (canceledLevel != null) UnityEngine.Object.Destroy(canceledLevel); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate); }
    }

    private static async UniTask VerifyNextLevelSession(PuzzleGameSession session, TutorialBoardAdapter previous)
    {
        string path = "Assets/__TutorialStage03Next-" + Guid.NewGuid().ToString("N") + ".bytes";
        IResourceLocator[] original = Addressables.ResourceLocators.ToArray();
        IResourceLocator locator = null;
        int nextNumber = checked(session.State.LevelNumber + 1);
        LevelDefinition next = LevelTutorialDataVerification.Fixture(nextNumber);
        bool installed = false;
        try
        {
            if (File.Exists(path) || File.Exists(path + ".meta")) throw new InvalidOperationException("검사 전용 경로가 이미 존재합니다.");
            File.WriteAllBytes(path, LevelPackCodec.Snapshot(next));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ResourceLocationMap map = new ResourceLocationMap("TutorialStage03Next");
            AssetDatabaseProvider provider = Addressables.ResourceManager.ResourceProviders.OfType<AssetDatabaseProvider>().First();
            map.Add(LevelPackCodec.Address(nextNumber), new ResourceLocationBase("TutorialStage03Next", path, provider.ProviderId, typeof(TextAsset)));
            Type fixtureLocator = typeof(GameScreen.Editor.PuzzleLevelTransitionVerification).GetNestedType("FixtureLocator", BindingFlags.NonPublic);
            locator = (IResourceLocator)Activator.CreateInstance(fixtureLocator, new object[] { original, map });
            installed = true;
            foreach (IResourceLocator old in original) Addressables.RemoveResourceLocator(old);
            Addressables.AddResourceLocator(locator);
            Check(await session.AdvanceLevelAsync(CancellationToken.None), "튜토리얼 승리 후 실제 다음 레벨 준비·교체 · " + session.Message);
            Check(previous.Executor == null && session.State.LevelNumber == nextNumber && session.TutorialState == null && session.State.Random.Seed == 555, "다음 레벨은 이전 튜토리얼 해제·일반 시드 경로 유지");
        }
        finally
        {
            if (installed)
            {
                if (locator != null) Addressables.RemoveResourceLocator(locator);
                foreach (IResourceLocator old in original) Addressables.AddResourceLocator(old);
            }
            AssetDatabase.DeleteAsset(path);
            UnityEngine.Object.Destroy(next);
        }
    }

    private static async UniTask WaitTutorial(PuzzleGameSession session, int step)
    {
        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await UniTask.WaitUntil(() => session.HasFailed || session.TutorialState.StepIndex == step && !session.IsPresenting && !session.HasProgressFeedback,
            cancellationToken: timeout.Token);
        Check(!session.HasFailed && session.TutorialState.StepIndex == step, "실제 게임 단계·관련 표시 완료 " + step + " · " + session.Message);
    }
}
