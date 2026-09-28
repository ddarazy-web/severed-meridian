using System;
using System.Collections.Generic;
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
    public static partial class PlanningVerification
    {
        /// <summary>공개 통로·벽·도착을 유지하고 숨은 중력 가정의 충돌을 명시적으로 처리한다.</summary>
        public static void Flow()
        {
            List<LevelDefinition> owned = new List<LevelDefinition>(); Exception failure = null; Results.Clear();
            try
            {
                Type branchType = typeof(PlanningSearch).Assembly.GetType("AutoPlay.PlanningBranch");
                BindingFlags member = BindingFlags.Instance | BindingFlags.NonPublic;
                for (int scenario = 0; scenario < 3; scenario++)
                {
                    BoardCoordinate[] active = scenario == 0 ? new[] { new BoardCoordinate(0, 0), new BoardCoordinate(1, 0), new BoardCoordinate(1, 1), new BoardCoordinate(4, 4), new BoardCoordinate(4, 5) } :
                        scenario == 1 ? new[] { new BoardCoordinate(0, 0), new BoardCoordinate(1, 0), new BoardCoordinate(2, 0), new BoardCoordinate(2, 1) } :
                        new[] { new BoardCoordinate(0, 0), new BoardCoordinate(0, 1), new BoardCoordinate(1, 0), new BoardCoordinate(1, 1) };
                    LevelDefinition level = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { active });
                    owned.Add(level);
                    JsonUtility.FromJsonOverwrite("{\"moveCount\":1,\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
                    BoardCoordinate rocket = scenario == 0 ? new BoardCoordinate(4, 4) : scenario == 1 ? new BoardCoordinate(2, 0) : new BoardCoordinate(1, 1);
                    if (scenario == 0)
                    {
                        Check(LevelFlowEditing.SetPortal(level, new BoardCoordinate(0, 0), rocket) == null, "통로 사례 설정");
                        Check(LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(new BoardCoordinate(1, 0), new BoardCoordinate(1, 1)) }, false) == null, "공개 벽 사례 설정");
                    }
                    else if (scenario == 1)
                    {
                        Check(LevelFlowEditing.SetArrival(level, rocket, false) == null, "회수 도착 설정");
                        typeof(RecoveryVerification).GetMethod("Place", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { level, new BoardCoordinate(1, 0) });
                        JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Recovery + ",\"count\":1}]}", level);
                    }
                    else
                    {
                        Check(LevelFlowEditing.SetPortal(level, new BoardCoordinate(1, 0), new BoardCoordinate(0, 0)) == null, "가정 충돌용 공개 통로 설정");
                        Check(LevelFlowEditing.SetGravity(level, new[] { new BoardCoordinate(0, 0) }, GravityDirection.Right) == null, "실제 흐름은 오른쪽으로 순환 회피");
                    }
                    typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                        new object[] { level, rocket, InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1 });
                    LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                    Check(built.IsBuilt, "흐름 사례 " + scenario + " 실제 정의 정합성 통과");
                    BoardActionExecutor actual = new BoardActionExecutor(built.State);
                    BotObservation visible = BotObservationBuilder.Capture(actual); string before = Snapshot(actual.State);
                    object branch = branchType.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { visible, 0 });
                    BoardActionExecutor assumed = (BoardActionExecutor)branchType.GetField("executor", member).GetValue(branch);
                    Check(Snapshot(BotObservationBuilder.Capture(assumed)) == Snapshot(visible), "흐름 사례 " + scenario + " 공개 통로·벽·도착·회수·후보 유지");
                    if (scenario == 2)
                    {
                        PlanningSearch search = RunSearch(visible, 1);
                        Check(search.UsedFallback && search.RejectedSamples == PlanningSearch.SampleLimit && search.Result.Reason.Contains("순환"),
                            "공개 통로와 아래 중력 가정이 충돌하면 이유를 남기고 기본 전환");
                        Check(actual.Outcome == null && Snapshot(actual.State) == before, "가정 순환 오류가 실제 판의 오류·패배를 만들지 않음");
                        continue;
                    }
                    BotAction action = visible.Actions.Single(a => a.Kind == BotActionKind.Activate && a.First.Equals(rocket));
                    branchType.GetMethod("Apply", member).Invoke(branch, new object[] { action }); DrainBranch(branch, branchType);
                    Check(Snapshot(actual.State) == before, "흐름 사례 " + scenario + " 가정 낙하·공급·회수가 실제 상태 무변경");
                    Check(assumed.CascadeHistory.Any(step => step.Settlement != null), "흐름 사례 " + scenario + " 공통 후속 실행 기록 존재");
                    SettlementRecord[] moves = assumed.CascadeHistory.Where(step => step.Settlement != null).SelectMany(step => step.Settlement.Records).ToArray();
                    if (scenario == 0)
                    {
                        Check(moves.Any(move => move.Kind == MovementKind.Portal), "가정 공통 실행기가 공개 입출구로 실제 이동");
                        Check(moves.Any(move => move.Kind == MovementKind.Supply), "가정 생성구에서 일반 공급 실행");
                    }
                    else Check(assumed.State.Recoveries.Count == 1 && assumed.Outcome?.Kind == BoardOutcomeKind.Won,
                        "가정의 기존 회수 부품이 도착에서 목표 완료");
                    Check(actual.Activate(rocket).IsApplied, "흐름 사례 " + scenario + " 동일 실제 행동 수락");
                    for (int i = 0; actual.HasPendingCascade && i < 2000; i++) actual.AdvanceCascade();
                    Check(!actual.HasPendingCascade && actual.State.MovesRemaining == assumed.State.MovesRemaining,
                        "흐름 사례 " + scenario + " 실제·가정 행동 비용 일치");
                    if (scenario == 1) Check(actual.State.Recoveries.Count == assumed.State.Recoveries.Count && actual.Outcome?.Kind == assumed.Outcome?.Kind,
                        "공개 회수·도착 조건의 실제·가정 성공 결과 일치");
                }
                SessionEdges();
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            finally { foreach (LevelDefinition level in owned) if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            File.WriteAllLines(Evidence + "/flow-edge-results.txt", Results); EditorApplication.Exit(failure == null ? 0 : 1);
        }

        /// <summary>계획 제출 뒤에는 기존 안전 경계를 지키며 탐색의 코드 오류는 실제 오류로 드러낸다.</summary>
        private static void SessionEdges()
        {
            BoardCoordinate[] active = Enumerable.Range(0, 16).Select(i => new BoardCoordinate(i / 4, i % 4)).ToArray();
            LevelDefinition level = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { active });
            try
            {
                JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[],\"moveCount\":3,\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
                using BotPlaySession session = new BotPlaySession(level, 771, BotStrategyKind.Planning);
                session.Begin(true);
                for (int i = 0; session.LastChoice == null && session.NeedsAdvance && i < 10000; i++) session.Advance();
                Check(session.LastChoice != null && session.Records.Count == 0 && session.Status == BotSessionStatus.Running, "계획 첫 행동 실제 제출 직후 경계 도달");
                session.RequestStop();
                Check(session.Status == BotSessionStatus.Stopping, "제출 후 중지는 후속 처리 경계까지 대기");
                for (int i = 0; session.NeedsAdvance && i < 2000; i++) session.Advance();
                Check(session.Status == BotSessionStatus.Stopped && session.Records.Count == 1 && session.State.MovesRemaining == 2,
                    "제출 후 중지는 정확히 한 행동만 완료·기록");
                string stopped = Snapshot(session.State); session.Advance();
                Check(stopped == Snapshot(session.State) && session.Records.Count == 1, "중지 후 가정 둘째 수를 자동 제출하지 않음");
                session.Begin(false); session.Advance();
                PlanningSearch plan = (PlanningSearch)typeof(BotPlaySession).GetField("planning", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                Check(plan != null, "새 관찰에서 다음 계획을 새로 생성");
                // 검사 소유 탐색의 분기 실행기를 손상시켜 코드 예외 경로를 강제로 실행한다.
                object first = typeof(PlanningSearch).GetField("first", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(plan);
                BoardActionExecutor executor = (BoardActionExecutor)first.GetType().GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(first);
                Set(executor, "Phase", (BoardActionPhase)999);
                for (int i = 0; session.NeedsAdvance && i < 10000; i++) session.Advance();
                Check(session.Status == BotSessionStatus.Error && !session.IsPlanning && session.Message.Contains("안정 상태"),
                    "가정의 잘못된 실행 경계는 기본 전환으로 숨기지 않고 세션 오류");
                Check(Snapshot(session.State) == stopped && session.Records.Count == 1, "가정 코드 오류에서 실제 판·이동·기록 추가 없음");
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }
    }
}
