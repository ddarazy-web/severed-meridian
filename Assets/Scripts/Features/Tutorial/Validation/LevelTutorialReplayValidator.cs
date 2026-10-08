using System;
using System.Collections.Generic;
using System.Linq;
using Levels;
using Simulation;
using UnityEngine;

namespace Tutorial
{
    /// <summary>사본에서 실제 퍼즐 실행기를 재생한다. 화면 연출의 시간·입력 전달 검사는 포함하지 않는다.</summary>
    public static class LevelTutorialReplayValidator
    {
        /// <param name="source">검사할 원본. 배치와 저장 값은 변경하지 않는다.</param>
        /// <returns>레벨·단계·대상 경로를 포함한 정적 또는 논리 재생 오류.</returns>
        public static List<LevelValidationIssue> Validate(LevelDefinition source)
        {
            List<LevelValidationIssue> issues = LevelDefinitionValidator.Validate(source);
            if (issues.Count > 0 || !source.HasTutorial) return issues;
            LevelDefinition owned = null;
            TutorialBoardAdapter adapter = null;
            int stepIndex = 0;
            void Error(string message, TutorialProgressSnapshot snapshot = null)
                => issues.Add(new LevelValidationIssue(LevelValidationCode.InvalidTutorial,
                    $"레벨 {source.LevelNumber} · {message}", $"tutorial.steps.Array.data[{stepIndex}]", snapshot?.First));
            try
            {
                owned = LevelPackCodec.Copy(source);
                StartingBoardSearch search = StartingBoardBuilder.Build(owned, owned.Tutorial.seed);
                if (search.Status != StartingBoardStatus.Success) { Error("시작 보드: " + search.Message); return issues; }
                adapter = TutorialBoardAdapter.Prepare(owned, search.State);
                BoardActionExecutor executor = adapter.Executor;
                long actionBudget = (long)owned.Tutorial.steps.Count + owned.MoveCount + owned.Tutorial.steps.Where(step => step.kind == TutorialStepKind.Item).Sum(step => (long)step.freeItemCount) + 1;
                for (long budget = 0; budget < actionBudget && adapter.Progress.State != TutorialProgressState.Completed; budget++)
                {
                    stepIndex = adapter.Progress.StepIndex;
                    TutorialProgressSnapshot snapshot = adapter.Progress.Snapshot;
                    int rounds = 0;
                    while (executor.HasPendingCascade && rounds++ <= executor.CascadeLimit * 2)
                        adapter.ObserveCascade(executor.AdvanceCascade());
                    if (executor.HasPendingCascade) { Error("논리 연쇄 종료 한도 초과", snapshot); break; }
                    adapter.Tick(true, false);
                    snapshot = adapter.Progress.Snapshot;
                    if (adapter.Progress.State == TutorialProgressState.Completed) break;
                    if (adapter.Progress.State == TutorialProgressState.Error || adapter.Progress.State == TutorialProgressState.Cancelled) { Error(adapter.Progress.Message, snapshot); break; }
                    TutorialInput input = snapshot.State == TutorialProgressState.AwaitDescription ? TutorialInput.Next() :
                        snapshot.Item.HasValue ? TutorialInput.UseItem(snapshot.Item.Value, snapshot.First, snapshot.Second) :
                        TutorialInput.Swap(snapshot.First.Value, snapshot.Second.Value);
                    if (!adapter.TryBegin(input)) { Error("지정 행동을 승인할 수 없습니다.", snapshot); break; }
                    if (input.Kind != TutorialInputKind.Next)
                    {
                        bool succeeded;
                        string message;
                        if (input.Kind == TutorialInputKind.Item)
                        {
                            ItemUseResult action = executor.UseApprovedFreeItem(input.Item.Value, input.First, input.Second);
                            succeeded = action.IsApplied; message = action.Message;
                        }
                        else
                        {
                            BoardActionResult action = executor.Swap(input.First.Value, input.Second.Value);
                            succeeded = action.IsApplied; message = action.Message;
                        }
                        adapter.ReportAction(succeeded);
                        if (!succeeded) { Error("후속 대상/행동 실패: " + message, snapshot); break; }
                        rounds = 0;
                        while (executor.HasPendingCascade && rounds++ <= executor.CascadeLimit * 2)
                            adapter.ObserveCascade(executor.AdvanceCascade());
                        if (executor.HasPendingCascade) { Error("논리 연쇄 종료 한도 초과", snapshot); break; }
                    }
                    // 논리 재생에서는 표시 시간을 생략한다. 실제 화면 검사는 별도 Play Mode 검사가 담당한다.
                    adapter.Tick(true, false);
                    if (adapter.Progress.State == TutorialProgressState.Error || adapter.Progress.State == TutorialProgressState.Cancelled) { Error(adapter.Progress.Message, snapshot); break; }
                    if (adapter.Progress.StepIndex == stepIndex && adapter.Progress.State != TutorialProgressState.AwaitAction)
                    { Error("행동 결과 또는 완료 조건이 충족되지 않았습니다.", snapshot); break; }
                }
                if (issues.Count == 0 && adapter.Progress.State != TutorialProgressState.Completed) Error("단계 재생 한도 초과");
            }
            catch (Exception error) { Error(error.Message); }
            finally
            {
                adapter?.Dispose();
                if (owned != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(owned);
                    else UnityEngine.Object.DestroyImmediate(owned);
                }
            }
            return issues;
        }
    }
}
