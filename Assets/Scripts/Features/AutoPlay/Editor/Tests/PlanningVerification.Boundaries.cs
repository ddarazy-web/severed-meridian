using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class PlanningVerification
    {
        /// <summary>숨은 공급 쌍, 실제 큰 판의 가정 실패, 작업량 경계를 함께 검사한다.</summary>
        public static void Boundaries()
        {
            LevelDefinition level = null;
            Exception failure = null;
            Results.Clear(); Directory.CreateDirectory(Evidence);
            try
            {
                level = (LevelDefinition)typeof(ItemBoosterVerification).GetMethod("PlayFixture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                JsonUtility.FromJsonOverwrite("{\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":0},\"mode\":1,\"exhaustion\":0,\"items\":[{\"kind\":1,\"count\":2,\"color\":0},{\"kind\":2,\"count\":5,\"direction\":0}]}]}}", level);
                LevelStateBuildResult first = LevelStateBuilder.Build(level, 123);
                JsonUtility.FromJsonOverwrite("{\"supply\":{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":0},\"mode\":1,\"exhaustion\":0,\"items\":[{\"kind\":3,\"count\":3},{\"kind\":1,\"count\":7,\"color\":4}]}]}}", level);
                LevelStateBuildResult second = LevelStateBuilder.Build(level, 987);
                Check(first.IsBuilt && second.IsBuilt, "숨은 공급이 다른 두 실제 정의 구성 성공");
                BoardActionExecutor a = new BoardActionExecutor(first.State), b = new BoardActionExecutor(second.State);
                // 이전에 가정 재배치 한도에 도달했던 10×10 판의 공개 덮개 조건도 유지한다.
                Set(a.State.Cells[0], "Cover", CoverKind.Mold); Set(a.State.Cells[0], "CoverDurability", 1);
                Set(b.State.Cells[0], "Cover", CoverKind.Mold); Set(b.State.Cells[0], "CoverDurability", 1);
                Set(b.State.Supply.Sources[0], "ItemIndex", 1); Set(b.State.Supply.Sources[0], "ItemConsumed", 1);
                typeof(SimulationRandom).GetMethod("Next", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(b.State.Random, new object[] { 100 });
                string actualA = Snapshot(a.State), actualB = Snapshot(b.State);
                Check(actualA != actualB && Snapshot(a.State.Supply) != Snapshot(b.State.Supply), "실제 시드·공급·커서·정의 지문 차이 존재");
                BotObservation publicA = BotObservationBuilder.Capture(a), publicB = BotObservationBuilder.Capture(b);
                Check(Snapshot(publicA) == Snapshot(publicB), "숨은 공급 쌍의 공개 값·모든 후보 동일");
                Type branch = typeof(PlanningSearch).Assembly.GetType("AutoPlay.PlanningBranch");
                BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
                System.Collections.Generic.HashSet<int> sampleSeeds = new System.Collections.Generic.HashSet<int>();
                for (int sample = 0; sample < PlanningSearch.SampleLimit; sample++)
                {
                    sampleSeeds.Add((int)branch.GetMethod("Seed", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { publicA, sample }));
                    object modelA = branch.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { publicA, sample });
                    object modelB = branch.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { publicB, sample });
                    BoardActionExecutor assumedA = (BoardActionExecutor)branch.GetField("executor", instance).GetValue(modelA);
                    BoardActionExecutor assumedB = (BoardActionExecutor)branch.GetField("executor", instance).GetValue(modelB);
                    Check(Snapshot(assumedA.State) == Snapshot(assumedB.State), "표본 " + sample + " 숨은 공급이 달라도 가정 상태·가정 난수 동일");
                    Check(!ReferenceEquals(a.State.Random, assumedA.State.Random) && !ReferenceEquals(a.State.Supply, assumedA.State.Supply) &&
                        !ReferenceEquals(a.State.Cells[0], assumedA.State.Cells[0]), "표본 " + sample + " 실제 판과 가정의 가변 참조 분리");
                }
                Check(sampleSeeds.Count == PlanningSearch.SampleLimit, "표본 번호별 별도 가정 난수 시작값 사용");
                PlanningSearch searchA = new PlanningSearch(publicA); double maximumStep = 0;
                Stopwatch elapsed = Stopwatch.StartNew();
                while (!searchA.IsDone)
                {
                    long before = Stopwatch.GetTimestamp(); searchA.Advance();
                    maximumStep = Math.Max(maximumStep, (Stopwatch.GetTimestamp() - before) * 1000d / Stopwatch.Frequency);
                }
                elapsed.Stop();
                PlanningSearch searchB = RunSearch(publicB, 17);
                Check(Snapshot(searchA.Result) == Snapshot(searchB.Result) && searchA.WorkDone == searchB.WorkDone &&
                    searchA.SecondActions == searchB.SecondActions && searchA.SelectedValue == searchB.SelectedValue,
                    "숨은 공급·실제 시드·갱신량 변경에도 전체 탐색 점수·이유·첫 행동 동일");
                Check(actualA == Snapshot(a.State) && actualB == Snapshot(b.State), "양쪽 탐색 완료 후 실제 상태·난수·공급 커서 보존");
                File.WriteAllText(Evidence + "/boundary-choice.txt", searchA.Result.Reason + "\nCompleted=" + searchA.CompletedSamples + ", Rejected=" + searchA.RejectedSamples);
                Check(searchA.UsedFallback && searchA.CompletedSamples == 0 && searchA.RejectedSamples == PlanningSearch.SampleLimit &&
                    searchA.Result.Reason.Contains("가정 처리 불가") && searchA.Result.Reason.Contains("기본 전략으로 전환"),
                    "큰 판의 공통 재배치 한계는 실제 패배가 아닌 명시적 기본 전환");
                Check(Snapshot(searchA.Result.Action) == Snapshot(BasicBotStrategy.Choose(publicA).Action), "가정 실패 시 기존 기본 전략의 최선 행동 유지");
                File.WriteAllText(Evidence + "/boundary-measurement.txt",
                    $"Board=10x10, Work={searchA.WorkDone}, Seconds={elapsed.Elapsed.TotalSeconds:F3}, MaxStepMs={maximumStep:F3}, RejectedSamples={searchA.RejectedSamples}\n");

                // 검사 전용 카운터를 한도 직전으로 옮겨 실제 소진 분기를 실행한다.
                // 일부 후보만 계산한 값이 공통 표본 점수에 섞여서는 안 된다.
                PlanningSearch budget = new PlanningSearch(publicA); budget.Advance();
                long[] partial = (long[])typeof(PlanningSearch).GetField("round", instance).GetValue(budget); partial[0] = long.MaxValue;
                Set(budget, "WorkDone", PlanningSearch.WorkLimit); budget.Advance();
                Check(budget.IsDone && budget.UsedFallback && budget.SelectedValue == 0 && budget.Result.Reason.Contains("작업량 한도"),
                    "예산 소진 시 미완료 표본의 큰 점수를 버리고 명시적 기본 전환");
                PlanningSearch completedBudget = new PlanningSearch(publicA);
                long[] totals = (long[])typeof(PlanningSearch).GetField("totals", instance).GetValue(completedBudget);
                Check(totals.Length > 1, "부분 비교 경계에 복수 후보 존재");
                totals[1] = 123; Set(completedBudget, "CompletedSamples", 1); Set(completedBudget, "WorkDone", PlanningSearch.WorkLimit);
                completedBudget.Advance();
                Check(!completedBudget.UsedFallback && completedBudget.SelectedValue == 123 && completedBudget.CompletedSamples == 1,
                    "예산 소진 후에도 이미 완료한 공통 표본만 비교");

                object invalid = branch.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { publicA, 0 });
                bool propagated = false;
                try { branch.GetMethod("Apply", instance).Invoke(invalid, new object[] { null }); }
                catch (TargetInvocationException error) { propagated = error.InnerException != null; }
                Check(propagated, "계약 위반 예외를 가정 표본 실패로 숨기지 않음");
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            finally { if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            File.WriteAllLines(Evidence + "/boundary-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}
