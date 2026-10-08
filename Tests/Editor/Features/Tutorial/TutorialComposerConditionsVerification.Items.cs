using System;
using System.Linq;
using System.IO;
using Levels;
using Levels.Editor;
using Board;
using System.Reflection;
using Simulation;
using UnityEngine;

namespace Tutorial.Editor
{
    public static partial class TutorialComposerConditionsVerification
    {
        private static void VerifyRepeatedItems()
        {
            Check(typeof(TutorialStepDefinition).GetField("freeItemCount") != null, "단계별 무료 아이템 체험 횟수 제공");
            var definition = new LevelTutorialDefinition();
            definition.steps.Add(JsonUtility.FromJson<TutorialStepDefinition>("{\"kind\":3,\"item\":2,\"instructions\":\"섞기\",\"freeItemCount\":2,\"conditions\":[{\"kind\":8,\"item\":2,\"requiredCount\":2}]}"));
            using TutorialProgress progress = new TutorialProgress(definition);
            TutorialInput input = TutorialInput.UseItem(BoardItem.Shuffle);
            Check(progress.TryApprove(input, out TutorialActionTicket rejected), "첫 무료 아이템 승인");
            progress.ReportCompletion(rejected, false);
            Check(progress.Snapshot.FreeItemAvailable && progress.Snapshot.ConditionCounts.Single() == 0, "실패는 체험 횟수와 성공 조건을 소진하지 않음");
            for (int i = 0; i < 2; i++)
            {
                Check(progress.TryApprove(input, out TutorialActionTicket ticket), "반복 무료 체험 승인 " + i);
                progress.ReportCompletion(ticket, true); progress.ReportCompletion(ticket, true);
                Check(progress.Snapshot.ConditionCounts.Single() == i + 1, "실제 성공 보고 한 번만 집계 " + i);
                progress.ReportResultsComplete(ticket);
                Check(progress.State == TutorialProgressState.AwaitPresentation, "아이템 연출 완료 전 단계 전환 금지 " + i);
                progress.ReportPresentationComplete(ticket);
                Check(progress.State == (i == 0 ? TutorialProgressState.AwaitAction : TutorialProgressState.Completed), "체험 횟수만큼 누적 후 완료 " + i);
            }
            Check(!progress.TryApprove(input, out _), "완료 후 무료 체험 재사용 금지");
            definition.steps[0].freeItemCount = 1;
            using (TutorialProgress invalid = new TutorialProgress(definition)) Check(invalid.State == TutorialProgressState.Error, "필요 사용 횟수보다 부족한 무료 횟수는 준비 오류");
            definition.steps[0].combination = TutorialConditionCombination.Any;
            definition.steps[0].conditions.Add(new TutorialConditionDefinition { kind = TutorialConditionKind.ItemUsed, item = BoardItem.Shuffle });
            using (TutorialProgress possible = new TutorialProgress(definition)) Check(possible.State == TutorialProgressState.AwaitAction, "Any 조건은 가능한 최소 사용 횟수로 검사");
            definition.steps[0].conditions.RemoveAt(1); definition.steps[0].combination = TutorialConditionCombination.All; definition.steps[0].freeItemCount = 2;
            LevelDefinition level = LevelPackCodec.ReadLevel(File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-v3.bytes"), 1);
            try
            {
                foreach (bool composed in new[] { true, false })
                {
                    level.Tutorial.steps.Clear();
                    TutorialStepDefinition step = JsonUtility.FromJson<TutorialStepDefinition>(JsonUtility.ToJson(definition.steps[0]));
                    if (!composed) step.conditions.Clear(); level.Tutorial.steps.Add(step);
                    byte[] bytes = LevelPackCodec.Snapshot(level);
                    LevelDefinition restored = LevelPackCodec.ReadLevel(bytes, level.LevelNumber);
                    try { Check(bytes[4] == 5 && JsonUtility.ToJson(restored.Tutorial.steps[0]) == JsonUtility.ToJson(step), "아이템 종류·무료 횟수 팩 왕복 " + composed); }
                    finally { UnityEngine.Object.DestroyImmediate(restored); }
                    LevelDefinition plain = LevelPackCodec.Copy(level);
                    try
                    {
                        plain.Tutorial.steps.Clear(); JsonUtility.FromJsonOverwrite("{\"levelNumber\":2}", plain);
                        byte[] mixed = LevelPackCodec.EncodeWithTutorial(new[] { level, plain }, level.CreateElementCatalog());
                        LevelDefinition restoredPlain = LevelPackCodec.ReadLevel(mixed, 2);
                        try { Check(!restoredPlain.HasTutorial, "아이템 튜토리얼과 일반 레벨의 혼합 팩 " + composed); }
                        finally { UnityEngine.Object.DestroyImmediate(restoredPlain); }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(plain); }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
            level = LevelPackCodec.ReadLevel(File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-v3.bytes"), 1);
            try
            {
                TutorialStepDefinition step = level.Tutorial.steps.First(value => value.kind == TutorialStepKind.Swap);
                level.Tutorial.steps = new System.Collections.Generic.List<TutorialStepDefinition> { step };
                step.kind = TutorialStepKind.Item; step.item = BoardItem.Swap; step.results.Clear();
                step.conditions = new System.Collections.Generic.List<TutorialConditionDefinition> { new TutorialConditionDefinition { kind = TutorialConditionKind.Match } };
                using TutorialBoardAdapter adapter = TutorialBoardAdapter.Prepare(level, StartingBoardBuilder.Build(level, level.Tutorial.seed).State);
                Check(adapter.TryBegin(TutorialInput.UseItem(BoardItem.Swap, step.first, step.second)), "직접 매칭용 교환 아이템 승인");
                ItemUseResult result = (ItemUseResult)typeof(BoardActionExecutor).GetMethod("UseApprovedFreeItem", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(adapter.Executor, new object[] { BoardItem.Swap, (BoardCoordinate?)step.first, (BoardCoordinate?)step.second });
                adapter.ReportAction(result.IsApplied);
                Check(result.IsApplied && adapter.Progress.Snapshot.ConditionCounts[0] == 1, "교환 아이템의 실제 직접3매칭을 조건으로 전달");
                step.actionArea = new System.Collections.Generic.List<BoardCoordinate> { step.first, step.second }; step.hasFirst = step.hasSecond = false;
                using TutorialBoardAdapter area = TutorialBoardAdapter.Prepare(level, StartingBoardBuilder.Build(level, level.Tutorial.seed).State);
                Check(area.CanSelectItem(BoardItem.Swap) && area.CanSelectItemTarget(BoardItem.Swap, step.first) && area.CanSelectItemTarget(BoardItem.Swap, step.second), "영역 교환 아이템과 두 대상 선택 허용");
                area.Tick(true, false);
                Check(area.Progress.Snapshot.First.HasValue && area.TryBegin(TutorialInput.UseItem(BoardItem.Swap, step.first, step.second)), "영역 교환 아이템 안내와 승인 연결");
                result = (ItemUseResult)typeof(BoardActionExecutor).GetMethod("UseApprovedFreeItem", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(area.Executor, new object[] { BoardItem.Swap, (BoardCoordinate?)step.first, (BoardCoordinate?)step.second }); area.ReportAction(result.IsApplied);
                int budget = 100;
                while (area.Executor.HasPendingCascade && budget-- > 0) area.ObserveCascade(area.Executor.AdvanceCascade());
                area.Tick(true, false);
                Check(area.Progress.State == TutorialProgressState.Completed, "영역 교환 아이템의 실제 실행 완료");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
            object Invoke(string method, params object[] args) => typeof(FixedObstacleVerification).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            level = (LevelDefinition)Invoke("Make");
            try
            {
                BoardCoordinate target = new BoardCoordinate(4, 4);
                Invoke("Obstacle", level, ObstacleKind.Appliance, 3, target, RabbitColor.Type1);
                Invoke("SetMission", level, ObstacleKind.Appliance);
                level.Tutorial.steps.Add(new TutorialStepDefinition { kind = TutorialStepKind.Item, item = BoardItem.Hammer, freeItemCount = 2,
                    instructions = "망치 사용", hasFirst = true, first = target,
                    conditions = new System.Collections.Generic.List<TutorialConditionDefinition> { new TutorialConditionDefinition { kind = TutorialConditionKind.ItemUsed, item = BoardItem.Hammer, requiredCount = 2 } } });
                LevelRuntimeState state = (LevelRuntimeState)Invoke("Build", level, 12345);
                using TutorialBoardAdapter adapter = new TutorialBoardAdapter(level, new BoardActionExecutor(state));
                MethodInfo use = typeof(BoardActionExecutor).GetMethod("UseApprovedFreeItem", BindingFlags.Instance | BindingFlags.NonPublic);
                int moves = adapter.Executor.State.MovesRemaining;
                Check(adapter.TryBegin(TutorialInput.UseItem(BoardItem.Hammer, target)), "실제 망치 실패 시도 승인");
                ItemUseResult failure = (ItemUseResult)use.Invoke(adapter.Executor, new object[] { BoardItem.Hammer, null, null }); adapter.ReportAction(failure.IsApplied);
                Check(!failure.IsApplied && adapter.Progress.Snapshot.ConditionCounts[0] == 0 && adapter.Progress.Snapshot.FreeItemAvailable, "실행기 실패도 무료 횟수를 소비하지 않음");
                for (int i = 0; i < 2; i++)
                {
                    Check(adapter.TryBegin(TutorialInput.UseItem(BoardItem.Hammer, target)), "실제 망치 반복 승인 " + i);
                    ItemUseResult action = (ItemUseResult)use.Invoke(adapter.Executor, new object[] { BoardItem.Hammer, (BoardCoordinate?)target, null }); adapter.ReportAction(action.IsApplied);
                    int budget = 100;
                    while (adapter.Executor.HasPendingCascade && budget-- > 0) adapter.ObserveCascade(adapter.Executor.AdvanceCascade());
                    adapter.Tick(true, false);
                    Check(action.IsApplied && !adapter.Executor.HasPendingCascade && adapter.Executor.State.MovesRemaining == moves, "실제 망치 후 연쇄 정착과 이동 수 보존 " + i);
                }
                Check(adapter.Progress.State == TutorialProgressState.Completed, "실제 망치2회 완료가 튜토리얼 완료로 연결");
                BoardCoordinate other = new BoardCoordinate(4, 5);
                level.Tutorial.steps[0].actionArea = new System.Collections.Generic.List<BoardCoordinate> { target, other };
                level.Tutorial.steps[0].hasFirst = false;
                using TutorialBoardAdapter area = new TutorialBoardAdapter(level, new BoardActionExecutor(state));
                Check(area.CanSelectItem(BoardItem.Hammer), "영역 망치는 고정 대상 없이 선택 가능");
                area.Tick(true, false);
                Check(area.CanSelectItemTarget(BoardItem.Hammer, other) && !area.CanSelectItemTarget(BoardItem.Hammer, new BoardCoordinate(0, 0)), "망치는 안내 대상 외에도 허용 영역 안의 유효 대상을 선택 가능");
                Check(area.TryBegin(TutorialInput.UseItem(BoardItem.Hammer, other)), "안내와 다른 영역 망치 대상 승인");
                ItemUseResult areaResult = (ItemUseResult)use.Invoke(area.Executor, new object[] { BoardItem.Hammer, (BoardCoordinate?)other, null }); area.ReportAction(areaResult.IsApplied);
                int areaBudget = 100;
                while (area.Executor.HasPendingCascade && areaBudget-- > 0) area.ObserveCascade(area.Executor.AdvanceCascade());
                area.Tick(true, false);
                Check(areaResult.IsApplied && area.Progress.State == TutorialProgressState.AwaitAction && area.Progress.Snapshot.FreeItemAvailable, "영역 망치 성공 후 남은 무료 횟수와 다음 안내 유지");
                area.Dispose();
                Check(!area.CanSelectItemTarget(BoardItem.Hammer, other) && !area.CanSelectItem(BoardItem.Hammer), "영역 아이템 연결 해제 후 선택 조회는 거절");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }
    }
}
