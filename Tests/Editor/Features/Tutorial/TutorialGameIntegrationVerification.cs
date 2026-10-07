using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using Tutorial;
using Tutorial.Editor;
using UnityEditor;
using UnityEngine;

public static partial class TutorialGameIntegrationVerification
{
    private static readonly List<string> Results = new List<string>();
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }

    public static void Run()
    {
        Results.Clear();
        foreach (Action test in new Action[] { VerifyPreview, VerifyDeferredEnding, VerifyEndingBoundaries, VerifyRocketReplay, VerifyItemReplay, VerifySupplyLifetime, VerifyExecutionRecords, VerifyBodyAndPowerRecords, VerifyRecoveryRecord, VerifyLogicalCompletion, VerifyApprovedItemBoundary, VerifyPreparedAdapter, VerifySessionContract, VerifyReplay })
            try { test(); } catch (Exception error) { Results.Add("FAIL " + test.Method.Name + " " + error); Debug.LogException(error); }
        Directory.CreateDirectory("Logs/Tutorial/Stage03");
        File.WriteAllLines("Logs/Tutorial/Stage03/integration-results.txt", Results);
        if (Results.Any(line => line.StartsWith("FAIL"))) EditorApplication.Exit(1);
        else StartSessionVerification();
    }

    private static void VerifyPreview()
    {
        MethodInfo query = typeof(TutorialProgress).GetMethod("CanApprove");
        Check(query != null, "승인을 소비하지 않는 입력 조회 경계");
        BoardCoordinate first = new BoardCoordinate(2, 2), second = new BoardCoordinate(2, 3);
        using TutorialProgress progress = new TutorialProgress(new LevelTutorialDefinition
        { steps = new List<TutorialStepDefinition> { new TutorialStepDefinition { kind = TutorialStepKind.Swap,
            instructions = "교환", hasFirst = true, first = first, hasSecond = true, second = second } } });
        TutorialInput input = TutorialInput.Swap(first, second);
        for (int i = 0; i < 10; i++) Check((bool)query.Invoke(progress, new object[] { input }), "선택 미리보기 반복 " + i);
        Check(progress.State == TutorialProgressState.AwaitAction && progress.StepIndex == 0, "조회는 진행을 변경하지 않음");
        Check(!(bool)query.Invoke(progress, new object[] { TutorialInput.Activate(first) }), "지정하지 않은 발동 조회 거절");
        progress.SetPaused(true);
        Check(!(bool)query.Invoke(progress, new object[] { input }), "일시정지 조회 차단");
        progress.SetPaused(false);
        Check(progress.TryApprove(input, out TutorialActionTicket ticket), "조회 후 실제 승인 가능");
        Check(!(bool)query.Invoke(progress, new object[] { input }), "대기 중 추가 입력 조회 차단");
        progress.ReportCompletion(ticket, false);
        Check((bool)query.Invoke(progress, new object[] { input }), "실패 후 조회와 재시도 허용");
    }

    private static void VerifyDeferredEnding()
    {
        MethodInfo gate = typeof(BoardActionExecutor).GetMethod("SetEndingDeferred");
        MethodInfo resume = typeof(BoardActionExecutor).GetMethod("ResumeDeferredEnding");
        Check(gate != null && resume != null, "실제 퍼즐 종료 보류·재평가 경계");
        LevelDefinition level = LevelTutorialDataVerification.Fixture();
        try
        {
            LevelRuntimeState state = StartingBoardBuilder.Build(level, 12345).State;
            typeof(LevelRuntimeState).GetProperty("MovesRemaining").SetValue(state, 0);
            BoardActionExecutor executor = new BoardActionExecutor(state);
            gate.Invoke(executor, new object[] { true });
            int remaining = executor.State.Missions.Sum(m => m.Remaining), draws = executor.State.Random.DrawCount;
            for (int i = 0; executor.HasPendingCascade && i < executor.CascadeLimit; i++) executor.AdvanceCascade();
            Check(!executor.HasPendingCascade && executor.Phase == BoardActionPhase.Ready && executor.Outcome == null, "이동 0 종료를 실제 안정 경계에서 보류");
            LevelRuntimeState stable = executor.State;
            Check(resume.Invoke(executor, null) == null && executor.Outcome == null, "보류 중 재평가는 종료하지 않음");
            Check(!executor.Swap(new BoardCoordinate(2, 2), new BoardCoordinate(2, 3)).IsApplied, "이동 0 추가 교환 차단");
            Check(!executor.CanUseItems && !executor.UseItem(BoardItem.Shuffle).IsApplied, "이동 0 일반 아이템 우회 차단");
            gate.Invoke(executor, new object[] { false });
            Check(resume.Invoke(executor, null) != null && executor.Outcome?.Kind == BoardOutcomeKind.MovesExhausted, "보류 해제 후 종료 결정 실행");
            Check(ReferenceEquals(stable, executor.State) && executor.Turn == 0 && executor.State.Random.DrawCount == draws && executor.State.Missions.Sum(m => m.Remaining) == remaining, "재평가는 턴·상태·미션·난수를 보존");
            Check(resume.Invoke(executor, null) == null, "종료 재평가는 한 번만 실행");
            BoardActionExecutor ordinary = new BoardActionExecutor(state);
            for (int i = 0; ordinary.HasPendingCascade && i < ordinary.CascadeLimit; i++) ordinary.AdvanceCascade();
            Check(ordinary.Outcome?.Kind == BoardOutcomeKind.MovesExhausted, "일반 퍼즐 종료 경로 보존");
        }
        finally { UnityEngine.Object.DestroyImmediate(level); }
    }

    private static void VerifySupplyLifetime()
    {
        Type adapterType = typeof(TutorialProgress).Assembly.GetType("Tutorial.TutorialBoardAdapter");
        Check(adapterType != null, "보드 연결부의 공급 사본 소유 경계");
        LevelDefinition level = LevelTutorialDataVerification.Fixture();
        IDisposable adapter = null;
        try
        {
            LevelRuntimeState initial = StartingBoardBuilder.Build(level, 12345).State;
            RuntimeSource source = initial.Supply.Sources.First();
            level.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "설명" });
            level.Tutorial.supply.sources.Add(new ElementSupplySourceDefinition { coordinate = source.Coordinate,
                mode = SupplyMode.Fixed, exhaustion = SupplyExhaustion.Stop,
                items = new List<ElementSupplyItemDefinition> { new ElementSupplyItemDefinition { definitionId = "supply.normal.fixed", count = 9, color = initial.Colors.First() } } });
            BoardActionExecutor boosted = new BoardActionExecutor(initial, new[] { StartBooster.Rocket });
            Check(boosted.BoosterPlacements.Count == 1, "실제 시작 부스터 적용 검사 보드");
            RuntimeSupply boostedSupply = boosted.State.Supply; int boostedDraws = boosted.State.Random.DrawCount;
            bool boosterRejected = false;
            try { using TutorialBoardAdapter unexpected = new TutorialBoardAdapter(level, boosted); }
            catch (ArgumentException) { boosterRejected = true; }
            Check(boosterRejected && ReferenceEquals(boostedSupply, boosted.State.Supply) && boosted.State.Random.DrawCount == boostedDraws, "부스터 적용 실행기의 튜토리얼 연결 거절·공급/난수 보존");
            BoardActionExecutor executor = new BoardActionExecutor(initial);
            LevelRuntimeState board = executor.State;
            int draws = board.Random.DrawCount;
            adapter = (IDisposable)Activator.CreateInstance(adapterType, new object[] { level, executor });
            TutorialProgress progress = (TutorialProgress)adapterType.GetProperty("Progress").GetValue(adapter);
            MethodInfo complete = adapterType.GetMethod("ReleaseCompleted");
            Check(board.Supply.Sources.Count == 1 && board.Supply.Sources[0].Mode == SupplyMode.Fixed && board.Supply.Sources[0].Items[0].Count == 9, "고정 공급을 실제 상태에 적용");
            Check(!(bool)complete.Invoke(adapter, null), "진행 중 일반 공급 복귀 거절");
            level.Tutorial.supply.sources[0].items[0].count = 1;
            level.Tutorial.steps[0].instructions = "수정됨";
            Check(board.Supply.Sources[0].Items[0].Count == 9 && progress.Snapshot.Instructions == "설명", "공급과 엔진 사본은 제작 변경에서 격리");
            UnityEngine.Object.DestroyImmediate(level); level = null;
            Check(progress.TryApprove(TutorialInput.Next(), out _), "레벨 파기 후 설명 진행");
            Check((bool)complete.Invoke(adapter, null), "완료 후 일반 공급 복귀");
            Check(ReferenceEquals(board, executor.State) && board.Random.DrawCount == draws && executor.Turn == 0 && board.MovesRemaining == initial.MovesRemaining, "공급 복귀는 보드·난수·이동·턴을 보존");
            Check(board.Supply.Sources.Count == initial.Supply.Sources.Count && board.Supply.Sources[0].Mode == source.Mode, "기존 일반 공급 목록 복구");
            Check(!(bool)complete.Invoke(adapter, null), "공급 복귀는 한 번만 실행");
        }
        finally { adapter?.Dispose(); if (level != null) UnityEngine.Object.DestroyImmediate(level); }
    }

    private static void VerifyExecutionRecords()
    {
        PropertyInfo records = typeof(TurnEffectContext).GetProperty("ElementRecords", BindingFlags.Instance | BindingFlags.NonPublic);
        PropertyInfo identity = typeof(RuntimeCell).GetProperty("ContentOccurrence", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(records != null && identity != null, "실제 정의·개체 발생 기록 경계");
        LevelDefinition level = LevelTutorialDataVerification.Fixture();
        try
        {
            BoardActionExecutor executor = new BoardActionExecutor(StartingBoardBuilder.Build(level, 12345).State);
            ActionCandidate action = ActionQuery.Find(executor.State).First(candidate => candidate.Second.HasValue && candidate.Kind == QueryActionKind.SwapMatch);
            Check(executor.Swap(action.First, action.Second.Value).IsApplied, "실제 매칭 행동 실행");
            object[] events = ((System.Collections.IEnumerable)records.GetValue(executor.TurnEffects)).Cast<object>().ToArray();
            Check(events.Length >= 3, "실제 매칭 소비 개체 기록");
            Type eventType = events[0].GetType();
            string[] ids = events.Select(record => (string)eventType.GetProperty("DefinitionId").GetValue(record)).ToArray();
            long[] occurrences = events.Select(record => (long)eventType.GetProperty("Occurrence").GetValue(record)).ToArray();
            Check(ids.All(id => !string.IsNullOrWhiteSpace(id)), "매칭 기록 실제 정의 ID");
            Check(occurrences.All(id => id != 0) && occurrences.Distinct().Count() >= 3, "소비 개체를 좌표와 독립적으로 구별");
            RuntimeCell surviving = executor.State.Cells.First(cell => cell.Content == RuntimeContent.Normal);
            long original = (long)identity.GetValue(surviving);
            ShuffleResult shuffled = ShuffleResolution.Resolve(executor.State);
            Check(shuffled.Reason == ShuffleReason.Applied && shuffled.State.Cells.Any(cell => (long)identity.GetValue(cell) == original), "섞기는 개체 식별을 유지");
            SettlementResult fall = executor.Settle();
            Check(fall.IsApplied, "실제 낙하·재공급 실행");
            Check(fall.State.Cells.Any(cell => (long)identity.GetValue(cell) == original), "직선 낙하는 기존 개체 식별 유지");
            object[] after = ((System.Collections.IEnumerable)records.GetValue(executor.TurnEffects)).Cast<object>().ToArray();
            Check(after.Length > events.Length, "신규 공급은 새로운 생성 기록 추가");
            long[] generated = after.Where(record => eventType.GetProperty("Kind").GetValue(record).ToString() == "Generated")
                .Select(record => (long)eventType.GetProperty("Occurrence").GetValue(record)).ToArray();
            long[] removed = events.Where(record => eventType.GetProperty("Kind").GetValue(record).ToString() == "Removed")
                .Select(record => (long)eventType.GetProperty("Occurrence").GetValue(record)).ToArray();
            Check(generated.Length > 0 && generated.Distinct().Count() == generated.Length && generated.All(id => !removed.Contains(id)), "재보충 개체는 제거된 개체와 다른 발생 식별");
        }
        finally { UnityEngine.Object.DestroyImmediate(level); }
    }

    private static void VerifyBodyAndPowerRecords()
    {
        PropertyInfo records = typeof(TurnEffectContext).GetProperty("ElementRecords", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (bool reverse in new[] { false, true })
        {
            Dictionary<BoardCoordinate, int> colors = new Dictionary<BoardCoordinate, int>();
            for (int row = 0; row < 9; row++) for (int column = 0; column < 9; column++) colors[new BoardCoordinate(row, column)] = (row * 2 + column) % 5;
            LevelDefinition level = (LevelDefinition)typeof(BoardActionVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { colors, 20 });
            try
            {
                BoardCoordinate rocket = new BoardCoordinate(4, 0), neighbor = new BoardCoordinate(4, 1);
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { rocket });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Kind = (int)InitialBlockKind.Rocket, Direction = RocketDirection.Horizontal }, new[] { rocket });
                foreach (int column in new[] { 3, 6 })
                {
                    BoardCoordinate anchor = new BoardCoordinate(4, column);
                    LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { anchor, new BoardCoordinate(4, column + 1), new BoardCoordinate(5, column), new BoardCoordinate(5, column + 1) });
                    Check(LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Appliance, Durability = 1 }, new[] { anchor }).Changed == 1, "2×2 실제 장애물 배치 " + column);
                }
                LevelStateBuildResult build = LevelStateBuilder.Build(level, 12345);
                Check(build.IsBuilt, "2×2·로켓 기록 검사 보드 유효");
                BoardActionExecutor executor = new BoardActionExecutor(build.State);
                BoardCoordinate first = reverse ? neighbor : rocket, second = reverse ? rocket : neighbor;
                BoardActionResult action = executor.Swap(first, second);
                Check(action.IsApplied, "양방향 실제 로켓 교환 발동 " + reverse);
                object[] actual = ((System.Collections.IEnumerable)records.GetValue(executor.TurnEffects)).Cast<object>().ToArray();
                TutorialResultRecord[] converted = actual.Select(record => new TutorialResultRecord(
                    (TutorialResultKind)Enum.Parse(typeof(TutorialResultKind), record.GetType().GetProperty("Kind").GetValue(record).ToString()),
                    (string)record.GetType().GetProperty("DefinitionId").GetValue(record),
                    record.GetType().GetProperty("Occurrence").GetValue(record).ToString(),
                    (BoardCoordinate)record.GetType().GetProperty("Coordinate").GetValue(record))).ToArray();
                IGrouping<string, TutorialResultRecord>[] bodies = converted.Where(record => record.Kind == TutorialResultKind.Removed && record.DefinitionId == "obstacle.metal-rod-box").GroupBy(record => record.OccurrenceId).ToArray();
                Check(bodies.Length == 2 && bodies.All(body => body.Count() == 4), "각 2×2 footprint는 동일 본체 식별·본체끼리는 별도 식별 " + reverse);
                using TutorialProgress progress = new TutorialProgress(new LevelTutorialDefinition { steps = new List<TutorialStepDefinition>
                { new TutorialStepDefinition { kind = TutorialStepKind.PowerSwap, instructions = "로켓", actionDefinitionId = "power.rocket", hasFirst = true,
                    first = rocket, hasSecond = true, second = neighbor, results = new List<TutorialResultDefinition>
                    { new TutorialResultDefinition { kind = TutorialResultKind.Removed, definitionId = "obstacle.metal-rod-box", count = 3 } } } } });
                Check(progress.TryApprove(TutorialInput.Swap(first, second), out TutorialActionTicket ticket), "양방향 파워 행동 승인 " + reverse);
                progress.ReportCompletion(ticket, true, converted.First(record => record.Kind == TutorialResultKind.Activated && record.DefinitionId == "power.rocket").DefinitionId);
                progress.ReportResults(ticket, converted);
                progress.ReportResultsComplete(ticket);
                Check(progress.State == TutorialProgressState.Error && progress.Message.Contains("2/3"), "2×2 점유칸 8개를 본체 2개로 집계 " + reverse);
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }
    }

    private static void VerifyRecoveryRecord()
    {
        PropertyInfo removal = typeof(RecoveryRecord).GetProperty("Removal", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(removal != null, "도착점 회수에도 실제 제거 식별 기록 제공");
        LevelDefinition level = LevelTutorialDataVerification.Fixture();
        try
        {
            using (SerializedObject data = new SerializedObject(level))
            {
                SerializedProperty arrivals = data.FindProperty("flow").FindPropertyRelative("arrivals");
                arrivals.arraySize = 1;
                arrivals.GetArrayElementAtIndex(0).FindPropertyRelative("row").intValue = 8;
                arrivals.GetArrayElementAtIndex(0).FindPropertyRelative("column").intValue = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            LevelRuntimeState state = StartingBoardBuilder.Build(level, 12345).State;
            RuntimeCell cell = state.CellAt(new BoardCoordinate(8, 0));
            typeof(RuntimeCell).GetProperty("Content").SetValue(cell, RuntimeContent.Recovery);
            long occurrence = (long)typeof(RuntimeCell).GetProperty("ContentOccurrence", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cell);
            typeof(RecoveryRules).GetMethod("Collect", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { state, 1, 0 });
            object record = removal.GetValue(state.Recoveries.Single());
            Check((long)record.GetType().GetProperty("Occurrence").GetValue(record) == occurrence, "회수 후 빈칸에서도 원래 개체 식별 유지");
            Check((string)record.GetType().GetProperty("DefinitionId").GetValue(record) == "supply.recovery", "회수 제거는 실제 부품 정의로 기록");
        }
        finally { UnityEngine.Object.DestroyImmediate(level); }
    }

    private static void VerifyLogicalCompletion()
    {
        MethodInfo finish = typeof(TutorialProgress).GetMethod("ReportResultsComplete");
        Check(finish != null, "실제 논리 결과 종료 신호");
        BoardCoordinate first = new BoardCoordinate(2, 2), second = new BoardCoordinate(2, 3);
        using TutorialProgress progress = new TutorialProgress(new LevelTutorialDefinition { steps = new List<TutorialStepDefinition>
        { new TutorialStepDefinition { kind = TutorialStepKind.Swap, instructions = "교환", hasFirst = true, first = first,
            hasSecond = true, second = second, results = new List<TutorialResultDefinition>
            { new TutorialResultDefinition { kind = TutorialResultKind.Generated, definitionId = "power.rocket" } } } } });
        Check(progress.TryApprove(TutorialInput.Swap(first, second), out TutorialActionTicket ticket), "논리 종료 검사 행동 승인");
        progress.ReportCompletion(ticket, true);
        progress.ReportPresentationComplete(ticket);
        Check(progress.State == TutorialProgressState.AwaitPresentation, "결과 미완료 중에는 기다림");
        finish.Invoke(progress, new object[] { ticket });
        Check(progress.State == TutorialProgressState.Error && progress.Message.Contains("results") && progress.Message.Contains("0/1"), "논리 종료 후 부족한 조건을 단계 경로로 진단");
    }

    private static void VerifyApprovedItemBoundary()
    {
        MethodInfo use = typeof(BoardActionExecutor).GetMethod("UseApprovedFreeItem", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(use != null, "승인받은 무료 체험의 좁은 실행 경계");
        LevelDefinition level = LevelTutorialDataVerification.Fixture();
        try
        {
            LevelRuntimeState state = StartingBoardBuilder.Build(level, 12345).State;
            typeof(LevelRuntimeState).GetProperty("MovesRemaining").SetValue(state, 0);
            BoardActionExecutor executor = new BoardActionExecutor(state);
            executor.SetEndingDeferred(true);
            while (executor.HasPendingCascade) executor.AdvanceCascade();
            BoardCoordinate first = state.Cells.First(cell => cell.Content == RuntimeContent.Normal && !cell.Cover.HasValue).Coordinate;
            Check(!executor.UseItem(BoardItem.Hammer, first).IsApplied, "일반 아이템은 이동 0에서 거절");
            ItemUseResult result = (ItemUseResult)use.Invoke(executor, new object[] { BoardItem.Hammer, (BoardCoordinate?)first, null });
            Check(result.IsApplied && executor.State.MovesRemaining == 0 && executor.Turn == 1, "승인 무료 망치만 이동 0에서 실제 실행");
        }
        finally { UnityEngine.Object.DestroyImmediate(level); }
    }

    private static void VerifySessionContract()
    {
        Check(typeof(GameScreen.PuzzleGameSession).GetProperty("TutorialState")?.PropertyType == typeof(TutorialProgressSnapshot), "실제 세션의 읽기 전용 튜토리얼 상태");
        Check(typeof(GameScreen.PuzzleGameSession).GetMethod("TryAdvanceTutorial") != null, "설명 다음 요청 API");
        Check(typeof(GameScreen.PuzzleGameSession).GetMethod("CanSelectBlock") != null && typeof(GameScreen.PuzzleGameSession).GetMethod("CanSelectItem") != null, "선택과 직접 입력의 공통 조회 API");
        Check(typeof(GameScreen.PuzzleGameSession).GetMethod("CanActivateBlock") != null, "파워 교환 선택과 제자리 발동 조회를 구별");
    }

    private static void VerifyPreparedAdapter()
    {
        MethodInfo prepare = typeof(TutorialBoardAdapter).GetMethod("Prepare");
        Check(prepare != null, "실행기 초기 정착 전에 고정 공급 준비");
        LevelDefinition level = LevelTutorialDataVerification.Fixture();
        TutorialBoardAdapter adapter = null;
        try
        {
            LevelRuntimeState initial = StartingBoardBuilder.Build(level, 12345).State;
            foreach (RuntimeCell cell in initial.Cells.Where(cell => cell.Content == RuntimeContent.Normal))
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Kind = (int)InitialBlockKind.FixedNormal, Color = cell.Color.Value }, new[] { cell.Coordinate });
            initial = StartingBoardBuilder.Build(level, 12345).State;
            ActionCandidate action = ActionQuery.Find(initial).First(candidate => candidate.Kind == QueryActionKind.SwapMatch && candidate.Second.HasValue);
            level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Swap, instructions = "실제 교환", hasFirst = true,
                first = action.First, hasSecond = true, second = action.Second.Value, results = new List<TutorialResultDefinition>
                { new TutorialResultDefinition { kind = TutorialResultKind.Removed, definitionId = "supply.normal.fixed", count = 3 } } });
            level.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "완료 설명" });
            foreach (RuntimeSource source in initial.Supply.Sources)
                level.Tutorial.supply.sources.Add(new ElementSupplySourceDefinition { coordinate = source.Coordinate, mode = SupplyMode.Fixed, exhaustion = SupplyExhaustion.Stop,
                    items = Enumerable.Range(0, 64).Select(index => new ElementSupplyItemDefinition { definitionId = "supply.normal.fixed", color = initial.Colors[(index * 2 + source.Coordinate.Column) % initial.Colors.Count] }).ToList() });
            RuntimeSupply originalSupply = initial.Supply;
            adapter = (TutorialBoardAdapter)prepare.Invoke(null, new object[] { level, initial });
            BoardActionExecutor executor = (BoardActionExecutor)typeof(TutorialBoardAdapter).GetProperty("Executor").GetValue(adapter);
            Check(ReferenceEquals(originalSupply, initial.Supply), "준비는 호출자가 가진 보드 공급을 보존");
            MethodInfo begin = typeof(TutorialBoardAdapter).GetMethod("TryBegin"), report = typeof(TutorialBoardAdapter).GetMethod("ReportAction"), observe = typeof(TutorialBoardAdapter).GetMethod("ObserveCascade"), tick = typeof(TutorialBoardAdapter).GetMethod("Tick");
            Check((bool)begin.Invoke(adapter, new object[] { TutorialInput.Swap(action.First, action.Second.Value) }), "실제 게임 행동 ticket 연결 승인");
            BoardActionResult result = executor.Swap(action.First, action.Second.Value);
            report.Invoke(adapter, new object[] { result.IsApplied });
            for (int i = 0; executor.HasPendingCascade && i < executor.CascadeLimit; i++) observe.Invoke(adapter, new object[] { executor.AdvanceCascade() });
            tick.Invoke(adapter, new object[] { false, false });
            Check(adapter.Progress.State == TutorialProgressState.AwaitPresentation && adapter.Progress.StepIndex == 0, "실제 결과 완료 후 표시 대기 · " + adapter.Progress.Message);
            tick.Invoke(adapter, new object[] { true, true });
            Check(adapter.Progress.StepIndex == 0, "팝업 정지 중 실제 결과 진행 보류");
            tick.Invoke(adapter, new object[] { true, false });
            Check(adapter.Progress.State == TutorialProgressState.AwaitDescription && adapter.Progress.StepIndex == 1, "실제 결과·논리·표시가 끝나면 다음 설명");
        }
        finally { adapter?.Dispose(); UnityEngine.Object.DestroyImmediate(level); }
    }

    private static void VerifyReplay()
    {
        Type validator = typeof(TutorialProgress).Assembly.GetType("Tutorial.LevelTutorialReplayValidator");
        Check(validator != null, "실제 실행기로 재생 검사하는 공개 경계");
        MethodInfo validate = validator.GetMethod("Validate");
        LevelDefinition level = LevelTutorialDataVerification.Fixture(), packed = null;
        try
        {
            level.Tutorial.seed = 6789;
            level.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "설명" });
            level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Item, item = BoardItem.Hammer, instructions = "망치", hasFirst = true,
                first = new BoardCoordinate(2, 2), results = new List<TutorialResultDefinition> { new TutorialResultDefinition { kind = TutorialResultKind.Removed, definitionId = "supply.normal.fixed" } } });
            level.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "완료" });
            LevelRuntimeState initial = StartingBoardBuilder.Build(level, 6789).State;
            foreach (RuntimeSource source in initial.Supply.Sources)
                level.Tutorial.supply.sources.Add(new ElementSupplySourceDefinition { coordinate = source.Coordinate, mode = SupplyMode.Fixed, exhaustion = SupplyExhaustion.Stop,
                    items = Enumerable.Range(0, 64).Select(index => new ElementSupplyItemDefinition { definitionId = "supply.normal.fixed", color = initial.Colors[(index * 2 + source.Coordinate.Column) % initial.Colors.Count] }).ToList() });
            string before = JsonUtility.ToJson(level);
            List<LevelValidationIssue> issues = (List<LevelValidationIssue>)validate.Invoke(null, new object[] { level });
            Check(issues.Count == 0, "실제 설명·망치·결과·공급 재생 통과 · " + string.Join(" · ", issues));
            Check(JsonUtility.ToJson(level) == before, "재생 검사는 원본을 보존");
            packed = LevelPackCodec.ReadLevel(LevelPackCodec.Snapshot(level), level.LevelNumber);
            Check(((List<LevelValidationIssue>)validate.Invoke(null, new object[] { packed })).Count == 0, "팩3 실제 재생 결과 동등");
            level.Tutorial.steps.Insert(2, new TutorialStepDefinition { kind = TutorialStepKind.PowerSwap, instructions = "미래 파워", actionDefinitionId = "power.magnet",
                hasFirst = true, first = new BoardCoordinate(8, 8), hasSecond = true, second = new BoardCoordinate(8, 7) });
            Check(LevelDefinitionValidator.Validate(level).Count == 0, "미래 잘못된 대상은 정적 검사만으로 결정하지 않음");
            issues = (List<LevelValidationIssue>)validate.Invoke(null, new object[] { level });
            Check(issues.Count > 0 && issues.Any(issue => issue.PropertyPath.Contains("[2]") && issue.Message.Contains("레벨")), "불가능한 후속 행동을 실제 재생에서 레벨·단계로 진단");
        }
        finally { if (packed != null) UnityEngine.Object.DestroyImmediate(packed); UnityEngine.Object.DestroyImmediate(level); }
    }
}
