using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Board;
using Levels;
using Levels.Editor;
using Simulation;
using Tutorial;
using UnityEngine;

public static partial class TutorialGameIntegrationVerification
{
    private static object PrivateCall(Type type, string name, params object[] args)
        => type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

    private static void Drain(BoardActionExecutor executor, TutorialBoardAdapter adapter = null)
    {
        for (int budget = 0; executor.HasPendingCascade && budget < executor.CascadeLimit * 2; budget++)
        {
            CascadeStepResult step = executor.AdvanceCascade();
            adapter?.ObserveCascade(step);
            if (adapter?.Progress.State == TutorialProgressState.Error) break;
        }
        Check(!executor.HasPendingCascade || adapter?.Progress.State == TutorialProgressState.Error, "논리 연쇄의 유한 종료");
    }

    private static void VerifyEndingBoundaries()
    {
        LevelDefinition win = (LevelDefinition)PrivateCall(typeof(PowerEffectVerification), "Make");
        LevelDefinition isolated = (LevelDefinition)PrivateCall(typeof(BoardActionVerification), "Make",
            new Dictionary<BoardCoordinate, int> { [new BoardCoordinate(0, 0)] = 0, [new BoardCoordinate(0, 2)] = 1 }, 20);
        LevelDefinition mold = (LevelDefinition)PrivateCall(typeof(MoldVerification), "Make");
        try
        {
            LevelRuntimeState winning = LevelStateBuilder.Build(win, 12345).State;
            foreach (RuntimeMission mission in winning.Missions) typeof(RuntimeMission).GetProperty("Progress").SetValue(mission, mission.Target);
            BoardActionExecutor executor = new BoardActionExecutor(winning);
            executor.SetEndingDeferred(true); Drain(executor);
            Check(executor.Outcome == null && executor.LastPangConversions == 0 && executor.Phase == BoardActionPhase.Ready, "조기 승리·라스트팡 보류");
            int turn = executor.Turn;
            executor.SetEndingDeferred(false);
            Check(executor.ResumeDeferredEnding()?.Reason == CascadeStepReason.Won && executor.IsLastPang && executor.LastPangConversions > 0, "완료 후 승리와 라스트팡 재개");
            Check(executor.ResumeDeferredEnding() == null && executor.Turn == turn, "승리 재평가 1회·턴 보존");

            executor = new BoardActionExecutor(LevelStateBuilder.Build(isolated, 12345).State);
            typeof(BoardActionExecutor).GetProperty("TurnEffects").SetValue(executor,
                Activator.CreateInstance(typeof(TurnEffectContext), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 0, Array.Empty<MatchedBlockChange>() }, null));
            typeof(BoardActionExecutor).GetProperty("Phase").SetValue(executor, BoardActionPhase.WaitingForAutomaticMatch);
            executor.SetEndingDeferred(true); int before = executor.State.Random.DrawCount; Drain(executor);
            Check(executor.Outcome == null && executor.LastShuffle == null && executor.State.Random.DrawCount == before, "진행 불가 보드의 자동 섞기·난수 소비 보류");
            executor.SetEndingDeferred(false); executor.ResumeDeferredEnding();
            Check(executor.Outcome?.Kind == BoardOutcomeKind.Blocked && executor.LastShuffle != null && executor.ResumeDeferredEnding() == null, "완료 후 진행 불가·자동 섞기 판단 1회");

            PrivateCall(typeof(MoldVerification), "Missions", mold);
            PrivateCall(typeof(MoldVerification), "Mold", mold, new[] { new BoardCoordinate(4, 4) });
            PrivateCall(typeof(PowerEffectVerification), "Place", mold, new BoardCoordinate(0, 0), InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1);
            executor = new BoardActionExecutor(LevelStateBuilder.Build(mold, 12345).State);
            executor.SetEndingDeferred(true);
            Check(executor.Activate(new BoardCoordinate(0, 0)).IsApplied, "곰팡이 턴 경계 실제 이동 소비");
            Drain(executor);
            MoldSpreadRecord spread = executor.TurnEffects.MoldSpread;
            int count = executor.State.Cells.Count(cell => cell.Cover == CoverKind.Mold), target = executor.State.Missions.Last().Target;
            Check(spread?.Reason == MoldSpreadReason.Spread && count == 2, "종료 보류 중 곰팡이 1회 확산");
            executor.SetEndingDeferred(false); executor.ResumeDeferredEnding();
            Check(executor.State.Cells.Count(cell => cell.Cover == CoverKind.Mold) == count && executor.State.Missions.Last().Target == target && ReferenceEquals(spread, executor.TurnEffects.MoldSpread), "종료 재개 시 곰팡이·미션 목표 중복 적용 없음");
        }
        finally { UnityEngine.Object.DestroyImmediate(win); UnityEngine.Object.DestroyImmediate(isolated); UnityEngine.Object.DestroyImmediate(mold); }
    }

    private static LevelDefinition RocketTutorial(bool reverse)
    {
        LevelDefinition level = (LevelDefinition)PrivateCall(typeof(BoardActionVerification), "RocketBoard");
        BoardCoordinate donor = new BoardCoordinate(2, 3), spawn = new BoardCoordinate(3, 3), neighbor = new BoardCoordinate(3, 4);
        level.Tutorial.seed = 12345;
        level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Swap, instructions = "로켓 만들기", hasFirst = true, first = donor,
            hasSecond = true, second = spawn, results = new List<TutorialResultDefinition>
            { new TutorialResultDefinition { kind = TutorialResultKind.Generated, definitionId = "power.rocket", hasCoordinate = true, coordinate = spawn } } });
        level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.PowerSwap, instructions = "새 로켓 교환", actionDefinitionId = "power.rocket",
            hasFirst = true, first = reverse ? neighbor : spawn, hasSecond = true, second = reverse ? spawn : neighbor,
            results = new List<TutorialResultDefinition> { new TutorialResultDefinition { kind = TutorialResultKind.Activated, definitionId = "power.rocket" } } });
        level.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "마지막 설명" });
        foreach (BoardCoordinate coordinate in new[] { new BoardCoordinate(3, 2), donor, neighbor, new BoardCoordinate(3, 5) })
            level.Tutorial.supply.sources.Add(new ElementSupplySourceDefinition { coordinate = coordinate, mode = SupplyMode.Fixed, exhaustion = SupplyExhaustion.Stop,
                items = Enumerable.Range(0, 64).Select(index => new ElementSupplyItemDefinition { definitionId = "supply.normal.fixed", color = (RabbitColor)((index + coordinate.Column) % 5) }).ToList() });
        return level;
    }

    private static string ReplayState(LevelDefinition level)
    {
        using TutorialBoardAdapter adapter = TutorialBoardAdapter.Prepare(level, StartingBoardBuilder.Build(level, level.Tutorial.seed).State);
        for (int budget = 0; adapter.Progress.State != TutorialProgressState.Completed && budget < level.Tutorial.steps.Count + 1; budget++)
        {
            TutorialProgressSnapshot snapshot = adapter.Progress.Snapshot;
            TutorialInput input = snapshot.State == TutorialProgressState.AwaitDescription ? TutorialInput.Next() : TutorialInput.Swap(snapshot.First.Value, snapshot.Second.Value);
            Check(adapter.TryBegin(input), "연속 로켓 재생 승인 단계 " + snapshot.StepIndex);
            if (input.Kind != TutorialInputKind.Next)
            {
                BoardActionResult action = adapter.Executor.Swap(input.First.Value, input.Second.Value);
                Check(action.IsApplied, "연속 로켓 실제 행동 단계 " + snapshot.StepIndex);
                adapter.ReportAction(action.IsApplied); Drain(adapter.Executor, adapter);
            }
            adapter.Tick(true, false);
            Check(adapter.Progress.StepIndex > snapshot.StepIndex, "실제 생성·발동 결과로 단계 진행 " + snapshot.StepIndex + " · " + adapter.Progress.Message);
        }
        Check(adapter.Progress.State == TutorialProgressState.Completed, "연속 로켓 재생 완료");
        LevelRuntimeState state = adapter.Executor.State;
        // 팩 변환은 제작 스키마 4를 요소 정의 스키마 5로 올린다. 실행 상태 전체를 별도로 비교한다.
        return (string)PrivateCall(typeof(LevelInitialStateVerification), "Snapshot", new
        {
            state.LevelNumber, state.Rows, state.Columns, state.InitialMoves, state.MovesRemaining, state.Cells,
            state.Obstacles, state.Missions, state.Recoveries, state.MissionProgressRecords, state.Flow, state.Supply,
            state.Random, adapter.Executor.Turn, adapter.Executor.Phase, adapter.Executor.Outcome
        });
    }

    private static void VerifyItemReplay()
    {
        LevelDefinition level = Tutorial.Editor.LevelTutorialDataVerification.Fixture();
        try
        {
            level.Tutorial.seed = 12345;
            level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Item, instructions = "무료 교환", item = BoardItem.Swap,
                hasFirst = true, first = new BoardCoordinate(2, 2), hasSecond = true, second = new BoardCoordinate(2, 3) });
            level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Item, instructions = "무료 섞기", item = BoardItem.Shuffle });
            level.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "아이템 완료 설명" });
            LevelRuntimeState initial = StartingBoardBuilder.Build(level, level.Tutorial.seed).State;
            foreach (RuntimeSource source in initial.Supply.Sources)
                level.Tutorial.supply.sources.Add(new ElementSupplySourceDefinition { coordinate = source.Coordinate, mode = SupplyMode.Fixed, exhaustion = SupplyExhaustion.Stop,
                    items = Enumerable.Range(0, 64).Select(index => new ElementSupplyItemDefinition { definitionId = "supply.normal.fixed", color = initial.Colors[(index * 2 + source.Coordinate.Column) % initial.Colors.Count] }).ToList() });
            List<LevelValidationIssue> issues = LevelTutorialReplayValidator.Validate(level);
            Check(issues.Count == 0, "실제 무료 교환→섞기 논리 재생 · " + string.Join(" · ", issues));
            using TutorialBoardAdapter adapter = TutorialBoardAdapter.Prepare(level, initial);
            MethodInfo use = typeof(BoardActionExecutor).GetMethod("UseApprovedFreeItem", BindingFlags.Instance | BindingFlags.NonPublic);
            int moves = initial.MovesRemaining;
            foreach (BoardItem item in new[] { BoardItem.Swap, BoardItem.Shuffle })
            {
                TutorialProgressSnapshot snapshot = adapter.Progress.Snapshot;
                Check(!adapter.TryBegin(TutorialInput.UseItem(BoardItem.Hammer, snapshot.First)), "교환/섞기 단계의 임의 망치 거절 " + item);
                Check(adapter.TryBegin(TutorialInput.UseItem(item, snapshot.First, snapshot.Second)), "지정 무료 아이템 승인 " + item);
                ItemUseResult action = (ItemUseResult)use.Invoke(adapter.Executor, new object[] { item, snapshot.First, snapshot.Second });
                adapter.ReportAction(action.IsApplied);
                Check(action.IsApplied && !adapter.Progress.Snapshot.FreeItemAvailable, "실제 아이템 성공은 권한 1회 소진 " + item);
                Drain(adapter.Executor, adapter); adapter.Tick(true, false);
                Check(adapter.Progress.StepIndex == snapshot.StepIndex + 1 && adapter.Executor.State.MovesRemaining == moves, "실제 아이템 후 단계 진행·이동 보존 " + item);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(level); }
    }

    private static void VerifyRocketReplay()
    {
        foreach (bool reverse in new[] { false, true })
        {
            LevelDefinition level = RocketTutorial(reverse), packed = null;
            try
            {
                List<LevelValidationIssue> issues = LevelTutorialReplayValidator.Validate(level);
                Check(issues.Count == 0, "로켓 생성→양방향 교환 발동 논리 재생 " + reverse + " · " + string.Join(" · ", issues));
                packed = LevelPackCodec.ReadLevel(LevelPackCodec.Snapshot(level), level.LevelNumber);
                string assetState = ReplayState(level), packState = ReplayState(packed);
                System.IO.File.WriteAllText("Logs/Tutorial/Stage03/rocket-asset-state.txt", assetState);
                System.IO.File.WriteAllText("Logs/Tutorial/Stage03/rocket-pack-state.txt", packState);
                Check(assetState == packState, "Asset·팩3 연속 진행 최종 보드/미션/이동/난수 동등 " + reverse);
                packed.Tutorial.steps[0].results[0].count = 2;
                issues = LevelTutorialReplayValidator.Validate(packed);
                Check(issues.Any(issue => issue.Message.Contains("결과 부족") && issue.Message.Contains("1/2") && issue.PropertyPath.Contains("[0]")), "실제 행동 종료 후 결과 불충족은 단계 오류 " + reverse);
                foreach (ElementSupplySourceDefinition source in level.Tutorial.supply.sources) source.items = source.items.Take(1).ToList();
                issues = LevelTutorialReplayValidator.Validate(level);
                Check(issues.Any(issue => issue.Message.Contains("공급") && issue.PropertyPath.Contains("steps")), "실제 공급 소진은 단계 오류 " + reverse);
                level.Tutorial.supply.sources.Clear();
                issues = LevelTutorialReplayValidator.Validate(level);
                Check(issues.Any(issue => issue.Message.Contains("공급") && issue.PropertyPath.Contains("steps")), "누락 생성구는 무작위 대체 없이 단계 오류 " + reverse);
            }
            finally { if (packed != null) UnityEngine.Object.DestroyImmediate(packed); UnityEngine.Object.DestroyImmediate(level); }
        }
    }
}
