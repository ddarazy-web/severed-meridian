using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Levels;
using UnityEditor;
using UnityEngine;
using Board;
using Simulation;

namespace Tutorial.Editor
{
    /// <summary>실제 출시 데이터를 변경하지 않는 조립형 데이터·실행 검사.</summary>
    public static partial class TutorialComposerVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Results.Add("PASS " + message);
        }

        public static void Run()
        {
            if (!RunChecks()) { EditorApplication.Exit(1); return; }
            SessionState.SetBool("Tutorial.Composer01.FullRun", true);
            TutorialComposerWorkflowVerification.Run();
        }

        public static void RunCore() => EditorApplication.Exit(RunChecks() ? 0 : 1);

        private static bool RunChecks()
        {
            Results.Clear();
            foreach (Action test in new Action[] { VerifyStorage, VerifyValidation, VerifyRepeatedActions, VerifyMatchFilters, VerifyActualMatch, VerifyAutomaticHighlight, VerifyAllAny, VerifyMoveBudget, VerifyPackBoundaries, VerifyActualGroups })
                try { test(); }
                catch (Exception error) { Results.Add("FAIL " + test.Method.Name + " " + error); Debug.LogException(error); }
            Directory.CreateDirectory("Logs/Tutorial/Composer01");
            File.WriteAllLines("Logs/Tutorial/Composer01/results.txt", Results);
            return !Results.Any(value => value.StartsWith("FAIL"));
        }

        private static void VerifyStorage()
        {
            byte[] legacy = File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-v3.bytes");
            LevelDefinition level = LevelPackCodec.ReadLevel(legacy, 1);
            LevelDefinition restored = null;
            try
            {
                Check(level.HasTutorial, "기존 팩3 튜토리얼 읽기");
                List<LevelDefinition> originals = new List<LevelDefinition>();
                try
                {
                    foreach (PackedLevelTutorial entry in LevelPackCodec.DecodeTutorial(legacy).Tutorials)
                        originals.Add(LevelPackCodec.ReadLevel(legacy, entry.LevelNumber));
                    Check(legacy.SequenceEqual(LevelPackCodec.EncodeWithTutorial(originals, originals[0].CreateElementCatalog())), "모든 기존 레벨 재인코딩 시 팩3 바이트 보존");
                }
                finally { foreach (LevelDefinition original in originals) UnityEngine.Object.DestroyImmediate(original); }
                // 새 작성 조건이 JSON 입력부터 팩 실행 데이터까지 소실되지 않아야 한다.
                JsonUtility.FromJsonOverwrite("{\"kind\":1,\"hasFirst\":true,\"first\":{\"row\":2,\"column\":2},\"hasSecond\":true,\"second\":{\"row\":2,\"column\":3},\"conditions\":[{\"kind\":0,\"requiredCount\":2}]}", level.Tutorial.steps[0]);
                byte[] packed = LevelPackCodec.Snapshot(level);
                restored = LevelPackCodec.ReadLevel(packed, 1);
                Check(JsonUtility.ToJson(restored.Tutorial.steps[0]).Contains("\"requiredCount\":2"), "새 교환 조건 요구 횟수 2가 팩 왕복 후 보존");
                Check(restored.Tutorial.steps[0].conditions[0].requiredCount == 2, "실제 실행용 조건 요구량 복원");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(level);
                if (restored != null) UnityEngine.Object.DestroyImmediate(restored);
            }
        }

        private static TutorialStepDefinition SwapStep()
        {
            return new TutorialStepDefinition
            {
                kind = TutorialStepKind.Swap, instructions = "교환",
                hasFirst = true, first = new BoardCoordinate(2, 2), hasSecond = true, second = new BoardCoordinate(2, 3),
                conditions = new List<TutorialConditionDefinition> { new TutorialConditionDefinition { requiredCount = 2 } }
            };
        }

        private static void VerifyValidation()
        {
            TutorialStepDefinition step = SwapStep();
            step.conditions[0].requiredCount = 0;
            using (TutorialProgress progress = new TutorialProgress(new LevelTutorialDefinition { steps = new List<TutorialStepDefinition> { step } }))
                Check(progress.State == TutorialProgressState.Error, "0회 조건은 실행 전 설정 오류");
        }

        private static void VerifyAutomaticHighlight()
        {
            TutorialStepDefinition step = SwapStep(); step.automaticHighlights = true;
            step.highlights.Add(new BoardCoordinate(8, 8));
            using TutorialProgress progress = new TutorialProgress(new LevelTutorialDefinition { steps = new List<TutorialStepDefinition> { step } });
            Check(progress.Snapshot.Highlights.SequenceEqual(new[] { step.first, step.second }), "자동 강조는 오래된 수동 좌표 대신 현재 조작 셀을 사용");
        }

        private static void VerifyAllAny()
        {
            foreach (TutorialConditionCombination combination in Enum.GetValues(typeof(TutorialConditionCombination)))
            {
                TutorialStepDefinition step = SwapStep(); step.combination = combination;
                step.conditions.Add(new TutorialConditionDefinition { kind = TutorialConditionKind.Match });
                TutorialStepDefinition next = SwapStep();
                using TutorialProgress progress = new TutorialProgress(new LevelTutorialDefinition { steps = new List<TutorialStepDefinition> { step, next } });
                TutorialInput input = TutorialInput.Swap(step.first, step.second);
                progress.TryApprove(input, out TutorialActionTicket ticket);
                progress.ReportCompletion(ticket, true);
                progress.ReportConditionEvents(ticket, new[] { new TutorialConditionEvent(TutorialConditionKind.Match, "one", 3, RabbitColor.Type1, true) });
                progress.ReportResultsComplete(ticket); progress.ReportPresentationComplete(ticket);
                if (combination == TutorialConditionCombination.All)
                {
                    Check(progress.StepIndex == 0 && progress.State == TutorialProgressState.AwaitAction, "모두 조건은 교환 2회가 남아 있으면 진행하지 않음");
                    progress.TryApprove(input, out TutorialActionTicket second);
                    progress.ReportCompletion(second, true); progress.ReportResultsComplete(second); progress.ReportPresentationComplete(second);
                }
                Check(progress.StepIndex == 1 && progress.Snapshot.ConditionCounts.SequenceEqual(new[] { 0 }), combination + " 충족 후 다음 단계 집계 초기화");
                progress.ReportConditionEvents(ticket, new[] { new TutorialConditionEvent(TutorialConditionKind.Match, "late", 3, RabbitColor.Type1, true) });
                Check(progress.Snapshot.ConditionCounts[0] == 0, combination + " 이전 단계 지연 사건 무시");
            }
        }

        private static void VerifyRepeatedActions()
        {
            TutorialStepDefinition step = SwapStep();
            using TutorialProgress progress = new TutorialProgress(new LevelTutorialDefinition { steps = new List<TutorialStepDefinition> { step } });
            TutorialInput input = TutorialInput.Swap(step.first, step.second);
            Check(progress.TryApprove(input, out TutorialActionTicket first), "첫 교환 승인");
            progress.ReportCompletion(first, true); progress.ReportResultsComplete(first);
            Check(progress.State == TutorialProgressState.AwaitPresentation, "조건 미충족이어도 먼저 연출 종료 대기");
            progress.ReportPresentationComplete(first);
            Check(progress.State == TutorialProgressState.AwaitAction && progress.StepIndex == 0, "첫 성공 이후 조건 미충족은 같은 단계의 다음 조작 허용");
            Check(progress.TryApprove(input, out TutorialActionTicket failed), "두 번째 시도 승인");
            progress.ReportCompletion(failed, false);
            Check(progress.Snapshot.ConditionCounts[0] == 1, "실패 교환은 앞선 성공 누적값 보존");
            Check(progress.TryApprove(input, out TutorialActionTicket second), "실패 이후 재시도 승인");
            progress.ReportCompletion(first, true);
            Check(progress.Snapshot.ConditionCounts[0] == 1, "지난 시도의 지연 완료 중복 제외");
            progress.ReportCompletion(second, true); progress.ReportResultsComplete(second);
            Check(progress.State == TutorialProgressState.AwaitPresentation, "두 번째 성공 이후에도 연출 대기");
            progress.ReportPresentationComplete(second);
            Check(progress.State == TutorialProgressState.Completed, "성공 교환 2회와 연출 완료 후 종료");
        }

        private static void VerifyMatchFilters()
        {
            TutorialStepDefinition step = SwapStep();
            step.conditions = new List<TutorialConditionDefinition>
            {
                new TutorialConditionDefinition { kind = TutorialConditionKind.Match, requiredCount = 2, matchSize = 3 },
                new TutorialConditionDefinition { kind = TutorialConditionKind.Match, requiredCount = 2, matchSize = 4, sizeComparison = TutorialMatchSizeComparison.AtLeast, origin = TutorialMatchOrigin.IncludingCascade, anyColor = false, color = RabbitColor.Type2 }
            };
            using TutorialProgress progress = new TutorialProgress(new LevelTutorialDefinition { steps = new List<TutorialStepDefinition> { step } });
            progress.TryApprove(TutorialInput.Swap(step.first, step.second), out TutorialActionTicket ticket);
            progress.ReportCompletion(ticket, true);
            progress.ReportConditionEvents(ticket, new[]
            {
                new TutorialConditionEvent(TutorialConditionKind.Match, "first", 3, RabbitColor.Type1, true),
                new TutorialConditionEvent(TutorialConditionKind.Match, "first", 3, RabbitColor.Type1, true),
                new TutorialConditionEvent(TutorialConditionKind.Match, "cascade", 3, RabbitColor.Type1, false),
                new TutorialConditionEvent(TutorialConditionKind.Match, "four", 4, RabbitColor.Type1, true),
                new TutorialConditionEvent(TutorialConditionKind.Match, "five", 5, RabbitColor.Type2, false)
            });
            Check(progress.Snapshot.ConditionCounts.SequenceEqual(new[] { 1, 1 }), "정확히·이상·색상·직접/연쇄·중복 사건 필터");
        }

        private static void VerifyActualMatch()
        {
            LevelDefinition level = LevelPackCodec.ReadLevel(File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-v3.bytes"), 1);
            try
            {
                TutorialStepDefinition step = level.Tutorial.steps.First(value => value.kind == TutorialStepKind.Swap);
                level.Tutorial.steps = new List<TutorialStepDefinition> { step };
                step.conditions = new List<TutorialConditionDefinition> { new TutorialConditionDefinition { kind = TutorialConditionKind.Match } };
                StartingBoardSearch search = StartingBoardBuilder.Build(level, level.Tutorial.seed);
                Check(search.Status == StartingBoardStatus.Success, "실제 기본 매칭 보드 준비");
                using TutorialBoardAdapter adapter = TutorialBoardAdapter.Prepare(level, search.State);
                Check(adapter.TryBegin(TutorialInput.Swap(step.first, step.second)), "실제 교환 승인");
                BoardActionResult result = adapter.Executor.Swap(step.first, step.second);
                Check(result.IsApplied && result.Decisions.Count == 1 && result.Decisions[0].Selected.Cells.Count == 3, "실제 퍼즐에서 3매칭 한 묶음 발생");
                adapter.ReportAction(result.IsApplied);
                Check(adapter.Progress.Snapshot.ConditionCounts.Last() == 1, "실제 매칭 결정이 튜토리얼 조건으로 전달");
                int rounds = 0;
                while (adapter.Executor.HasPendingCascade && rounds++ < 200) adapter.ObserveCascade(adapter.Executor.AdvanceCascade());
                adapter.Tick(false, false);
                Check(adapter.Progress.State == TutorialProgressState.AwaitPresentation, "실제 논리 완료 후 화면 연출 대기");
                adapter.Tick(true, false);
                Check(adapter.Progress.State == TutorialProgressState.Completed, "실제 매칭과 연출 완료 후 종료");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }
    }
}
