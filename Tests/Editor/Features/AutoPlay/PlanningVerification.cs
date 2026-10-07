using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>공개 가정 탐색의 경계·결정성 검사. 검증 전용 Unity에서 실행한다.</summary>
    public static partial class PlanningVerification
    {
        private const string Evidence = "Logs/BotPlanningVerification";
        private static readonly List<string> Results = new List<string>();

        public static void Run()
        {
            LevelDefinition level = null;
            Exception failure = null;
            Directory.CreateDirectory(Evidence);
            try
            {
                BoardCoordinate[] active = Enumerable.Range(0, 16).Select(i => new BoardCoordinate(i / 4, i % 4)).ToArray();
                level = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { active });
                typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { level, new BoardCoordinate(3, 3), InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1 });
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
                BoardActionExecutor real = new BoardActionExecutor(LevelStateBuilder.Build(level, 12345).State);
                string json = JsonUtility.ToJson(level); bool dirty = EditorUtility.IsDirty(level);
                RuntimeCell hidden = real.State.CellAt(new BoardCoordinate(0, 0));
                Set(hidden, "Cover", CoverKind.Mold); Set(hidden, "CoverDurability", 1);
                BotObservation view = BotObservationBuilder.Capture(real);
                string before = Snapshot(real.State) + Snapshot(real.TurnEffects);
                Stopwatch watch = Stopwatch.StartNew();
                PlanningSearch first = RunSearch(view, 1);
                watch.Stop();
                File.WriteAllText(Evidence + "/search-measurement.txt", $"Milliseconds={watch.ElapsedMilliseconds}\nWork={first.WorkDone}\nSecondActions={first.SecondActions}\nSamples={first.CompletedSamples}\n{first.Result?.Reason}\n");
                Check(first.IsDone && first.Result != null && first.WorkDone <= PlanningSearch.WorkLimit, "유한 작업량 안에서 계획 또는 명시적 기본 전환 완료");
                Check(first.SecondActions > 0, "둘째 행동을 가정용 공통 실행기로 실제 실행");
                Check(first.CompletedSamples > 0 && !first.UsedFallback, "모든 첫 후보가 완료한 공통 표본으로 비교");
                Check(view.Actions.Contains(first.Result.Action), "최종 선택은 원래 공개 관찰의 첫 행동");
                Check(before == Snapshot(real.State) + Snapshot(real.TurnEffects), "가정 구성·분기·전체 탐색이 실제 상태·난수·예약 무변경");
                Check(JsonUtility.ToJson(level) == json && EditorUtility.IsDirty(level) == dirty, "탐색 후 원본 JSON·dirty 보존");
                string expected = Snapshot(first.Result);
                Set(hidden, "Content", RuntimeContent.Magnet); Set(hidden, "Color", RabbitColor.Type5);
                typeof(SimulationRandom).GetMethod("Next", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(real.State.Random, new object[] { 100 });
                BotObservation changed = BotObservationBuilder.Capture(real);
                Check(Snapshot(view) == Snapshot(changed), "숨은 블록·실제 난수 변경 뒤 공개 관찰 동일");
                PlanningSearch second = RunSearch(changed, 17);
                Check(Snapshot(second.Result) == expected && second.WorkDone == first.WorkDone && second.SecondActions == first.SecondActions,
                    "숨은 정보·갱신 단위 변경에도 계획 점수·이유·선택·작업 수 동일");
                PlanningSearch cancelled = new PlanningSearch(view); cancelled.Advance(3); cancelled.Dispose(); cancelled.Advance(10);
                Check(cancelled.IsCancelled && cancelled.Result == null && cancelled.WorkDone == 3, "탐색 중 취소는 결과·작업을 폐기하고 재실행하지 않음");
                // 후보의 열거 순서를 뒤집어도 실제 관찰 내용과 가정 시드는 같아야 한다.
                BotObservation reversed = (BotObservation)Activator.CreateInstance(typeof(BotObservation), BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { view.Rows, view.Columns, view.MovesRemaining, view.Cells, view.Bodies, view.Missions,
                        view.Actions.Reverse(), view.Walls, view.Arrivals, view.Portals }, null);
                PlanningSearch reordered = RunSearch(reversed, 7);
                Check(Snapshot(reordered.Result) == expected, "후보 열거 순서 변경에도 같은 계획 선택");
                // 실제 분기 후속 처리가 대기 중일 때 한도 경계를 주입해 두 제한 경로를 검사한다.
                BindingFlags hiddenMember = BindingFlags.Instance | BindingFlags.NonPublic;
                PlanningSearch firstLimit = new PlanningSearch(view); firstLimit.Advance();
                typeof(PlanningSearch).GetField("branchSteps", hiddenMember).SetValue(firstLimit, PlanningSearch.BranchStepLimit);
                firstLimit.Advance();
                Check((string)typeof(PlanningSearch).GetField("limitation", hiddenMember).GetValue(firstLimit) == "첫 수 가정 연쇄 한도" && firstLimit.CompletedSamples == 0,
                    "첫 분기 후속 처리 한도는 미완료 공통 표본을 제외");
                PlanningSearch secondLimit = new PlanningSearch(view);
                for (int i = 0; typeof(PlanningSearch).GetField("second", hiddenMember).GetValue(secondLimit) == null && !secondLimit.IsDone && i < 1000; i++) secondLimit.Advance();
                Check(typeof(PlanningSearch).GetField("second", hiddenMember).GetValue(secondLimit) != null, "둘째 분기 처리 경계 도달");
                typeof(PlanningSearch).GetField("branchSteps", hiddenMember).SetValue(secondLimit, PlanningSearch.BranchStepLimit);
                secondLimit.Advance();
                Check((string)typeof(PlanningSearch).GetField("limitation", hiddenMember).GetValue(secondLimit) == "둘째 수 가정 연쇄 한도",
                    "둘째 분기 한도도 오류 표본의 일부 점수를 채택하지 않음");
                CheckSession(level);
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); UnityEngine.Debug.LogException(error); }
            finally { if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            File.WriteAllLines(Evidence + "/planning-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        /// <param name="observation">공개 입력.</param><param name="units">갱신당 처리량.</param><returns>완료된 탐색.</returns>
        private static PlanningSearch RunSearch(BotObservation observation, int units)
        {
            PlanningSearch search = new PlanningSearch(observation);
            for (int i = 0; !search.IsDone && i <= PlanningSearch.WorkLimit; i++) search.Advance(units);
            return search;
        }

        /// <summary>계획 계산 취소는 무비용이고 실제 제출 이후에는 공통 실행기의 비용/결과를 따른다.</summary>
        /// <param name="level">검사 소유 레벨.</param>
        private static void CheckSession(LevelDefinition level)
        {
            using BotPlaySession session = new BotPlaySession(level, 771, BotStrategyKind.Planning);
            for (int i = 0; session.NeedsAdvance && i < 2000; i++) session.Advance();
            Check(session.Status == BotSessionStatus.Ready && session.Strategy == BotStrategyKind.Planning, "계획 전략 세션의 공통 시작 조건 통과");
            BotObservation original = session.Observe(); string before = Snapshot(session.State);
            session.Begin(false); session.Advance();
            Check(session.IsPlanning && session.Records.Count == 0 && session.LastChoice == null, "한 수 실행의 첫 단계는 가정 계산만 수행");
            Check(!session.TrySubmit(original, original.Actions[0]) && Snapshot(session.State) == before, "계획 중 외부 입력 거절·실제 판 무변경");
            session.RequestStop(); session.Advance();
            Check(session.Status == BotSessionStatus.Stopped && !session.IsPlanning && Snapshot(session.State) == before, "탐색 중 중지는 비용 없이 작업 폐기");
            session.Begin(false);
            for (int i = 0; session.NeedsAdvance && i < 10000; i++) session.Advance();
            Check(session.Records.Count == 1 && session.LastChoice.Reason.Contains("가정"), "계획 후 한 행동만 제출·가정 이유를 기록까지 보존");
            BoardActionExecutor manual = new BoardActionExecutor(StartingBoardBuilder.Build(level, 771).State);
            BotAction action = session.LastChoice.Action;
            BoardActionResult applied = action.Kind == BotActionKind.Activate ? manual.Activate(action.First) : manual.Swap(action.First, action.Second.Value);
            for (int i = 0; manual.HasPendingCascade && i < 2000; i++) manual.AdvanceCascade();
            Check(applied.IsApplied && Snapshot(manual.State) == Snapshot(session.State) && Snapshot(manual.Outcome) == Snapshot(session.Outcome),
                "실제 첫 행동의 상태·난수·미션·결과는 같은 수동 명령과 일치");
            Check(!session.TrySubmit(original, original.Actions[0]), "이전 계획 관찰로 다시 제출할 수 없음");
        }
        /// <param name="value">검사 사본.</param><param name="property">가정 차이를 주입할 속성.</param><param name="data">새 값.</param>
        private static void Set(object value, string property, object data) => value.GetType().GetProperty(property).GetSetMethod(true).Invoke(value, new[] { data });
        /// <param name="value">비교할 객체.</param><returns>기존 검증기의 결정적 표현.</returns>
        private static string Snapshot(object value) => (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { value });
        /// <param name="pass">조건.</param><param name="name">근거 이름.</param>
        private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
    }
}
