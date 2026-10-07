using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using GameScreen;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    /// <summary>구간2의 독립 증거만 기록하며 기존 드론 검사의 출력은 수정하지 않는다.</summary>
    public static partial class DroneFrameworkVerification
    {
        private const string Output = "Logs/ElementFramework/Phase02";
        private static readonly List<string> Results = new List<string>();
        [Serializable]
        private sealed class FixedInput { public string name, input; public int seed; }
        private static object Invoke(Type type, string method, params object[] args)
            => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

        public static void Baseline() => Capture("baseline");
        public static void Current() => Capture("policy-current");

        private static void Capture(string name)
        {
            Run(name, () =>
            {
                Type type = typeof(TargetPowerVerification);
                List<string> checks = (List<string>)type.GetField("Results", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                List<string> rows = (List<string>)type.GetField("BaselineRows", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                checks.Clear(); rows.Clear();
                foreach (string method in new[] { "DataChecks", "SupplementalChecks", "ReservationChecks" })
                    Invoke(type, method);
                string[] observations = { "ObserveColorPolicy", "ObserveBodyReservations", "ObserveGeneratorReservations", "ObserveRetargetTimeline" };
                string[] names = { "color-direct", "body-before", "generator-partial", "timeline-retarget" };
                FixedInput[] saved = name == "baseline" ? null : File.ReadAllLines(Output + "/baseline-observations.jsonl")
                    .Select(line => JsonUtility.FromJson<FixedInput>(line)).ToArray();
                FieldInfo prepare = type.GetField("PrepareFixture", BindingFlags.Static | BindingFlags.NonPublic);
                try
                {
                    for (int index = 0; index < observations.Length; index++)
                    {
                        string input = saved?.First(row => row.name == names[index]).input;
                        prepare.SetValue(null, input == null ? null : (Action<LevelDefinition>)(level => JsonUtility.FromJsonOverwrite(input, level)));
                        Invoke(type, observations[index]);
                    }
                }
                finally { prepare.SetValue(null, null); }
                Results.AddRange(checks);
                File.WriteAllLines(Output + "/" + name + "-observations.jsonl", rows);
            });
        }

        public static void RiseHover()
        {
            Run("rise-hover", () =>
            {
                foreach (int pair in new[] { 2, 4, 5, 8 })
                {
                    LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", pair, RocketDirection.Horizontal, null);
                    try
                    {
                        LevelRuntimeState source = LevelStateBuilder.Build(level, 12345).State;
                        BoardActionExecutor executor = new BoardActionExecutor(source);
                        BoardActionResult action = executor.Swap(new BoardCoordinate(4, 4), new BoardCoordinate(4, 5));
                        if (!action.IsApplied) throw new InvalidOperationException("조합 fixture 발동 실패 " + pair);
                        PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(source, action.Changes, action.Effects, action.PowerTrace);
                        PuzzleEffectTimeline.Attack first = timeline.Attacks.First(attack => attack.Record.IsFlight);
                        float available = timeline.Combination.IsTransformation ? .35f : 0;
                        Results.Add((first.Start <= available + 1.2f ? "PASS " : "FAIL ") +
                            "조합도 짧은 상승/호버 후 첫 돌진 " + pair + " 실제=" + first.Start);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
            });
        }

        public static void Policies()
        {
            Run("policies", () =>
            {
                Type policyType = typeof(DroneTargetManager).Assembly.GetType("Simulation.DroneTargetPolicyRegistry");
                if (policyType == null) throw new InvalidOperationException("활성 특성 키로 조회하는 드론 정책 등록표가 없음");
                foreach (int fixture in new[] { 0, 1, 2 })
                {
                    LevelDefinition level = fixture == 0 ? (LevelDefinition)Invoke(typeof(TargetPowerVerification), "Make") :
                        fixture == 1 ? (LevelDefinition)Invoke(typeof(PowerEffectVerification), "ProtectionBoard") :
                        (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", ObstacleKind.Appliance, 3);
                    try
                    {
                        LevelRuntimeState state = (LevelRuntimeState)Invoke(typeof(TargetPowerVerification), "Build", level, 12345);
                        TurnEffectContext context = (TurnEffectContext)Invoke(typeof(TargetPowerVerification), "Context");
                        object policies = Activator.CreateInstance(policyType, BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { state, context }, null);
                        MethodInfo query = policyType.GetMethod("Query", BindingFlags.NonPublic | BindingFlags.Instance);
                        string before = (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", state);
                        int draws = state.Random.DrawCount;
                        foreach (RuntimeCell cell in state.Cells)
                        {
                            object actual = query.Invoke(policies, new object[] { cell.Coordinate, null });
                            string expected = (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", MissionProgressRules.Query(state, cell.Coordinate, context));
                            string observed = (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", actual);
                            Results.Add((expected == observed ? "PASS " : "FAIL ") + "정책 기여 원문/순서 동일 " + fixture + ":" + cell.Coordinate);
                        }
                        Results.Add(((string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", state) == before && state.Random.DrawCount == draws ? "PASS " : "FAIL ") + "정책 조회 상태/난수 보존 " + fixture);
                        if (fixture == 0)
                        {
                            System.Collections.IDictionary counts = (System.Collections.IDictionary)policyType.GetField("invocations", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(policies);
                            Results.Add((counts.Count == 1 && counts.Keys.Cast<object>().Single().ToString() == "Color" ? "PASS " : "FAIL ") + "색 미션만 있으면 비활성 정책 호출0");
                            int calls = (int)counts.Values.Cast<object>().Single();
                            RuntimeMission mission = state.Missions[0];
                            mission.GetType().GetProperty("Progress").GetSetMethod(true).Invoke(mission, new object[] { mission.Target });
                            foreach (RuntimeCell cell in state.Cells) query.Invoke(policies, new object[] { cell.Coordinate, null });
                            Results.Add(((int)counts.Values.Cast<object>().Single() == calls ? "PASS " : "FAIL ") + "완료한 미션 정책은 추가 호출0");
                        }
                        if (fixture == 2)
                        {
                            ElementDefinition original = LegacyElementDefinitions.Get(ObstacleKind.Appliance);
                            ElementDefinition alternate = new ElementDefinition(new ElementId("test.drone.alternate-body"), "다른 ID의 동일 드론 정책",
                                original.Placement, original.ChargePlacement, original.DamageSourcePolicy, original.ColorMatchPolicy,
                                original.DamageAggregationPolicy, original.RemovalMissionProfile, original.ReactionBehavior);
                            BoardCoordinate coordinate = new BoardCoordinate(4, 7);
                            object expected = query.Invoke(policies, new object[] { coordinate, null });
                            object actual = query.Invoke(policies, new object[] { coordinate, alternate });
                            Results.Add(((string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", actual) ==
                                (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", expected) ? "PASS " : "FAIL ") + "다른 ID가 기존 내구도 드론 정책 재사용");
                            ElementDefinition changed = new ElementDefinition(new ElementId("test.drone.changed-mission"), "선택 정의의 미션",
                                original.Placement, original.ChargePlacement, original.DamageSourcePolicy, original.ColorMatchPolicy,
                                original.DamageAggregationPolicy, new ElementRemovalMissionProfile(MissionKind.Scrap), original.ReactionBehavior);
                            System.Collections.ICollection redirected = (System.Collections.ICollection)query.Invoke(policies, new object[] { coordinate, changed });
                            Results.Add((redirected.Count == 0 ? "PASS " : "FAIL ") + "선택 정의의 미션 키로만 조회 · 기존 종류 미션으로 대체 없음");
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
            });
        }

        public static void RetargetFlight()
        {
            Run("retarget-flight", () =>
            {
                FixedInput input = File.ReadAllLines(Output + "/baseline-observations.jsonl")
                    .Select(line => JsonUtility.FromJson<FixedInput>(line)).First(row => row.name == "timeline-retarget");
                LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
                try
                {
                    JsonUtility.FromJsonOverwrite(input.input, level);
                    LevelRuntimeState source = LevelStateBuilder.Build(level, input.seed).State;
                    foreach (RuntimeCell cell in source.Cells.Where(cell => cell.Content == RuntimeContent.Normal))
                        cell.GetType().GetProperty("Color").GetSetMethod(true).Invoke(cell, new object[] {
                            cell.Coordinate.Equals(new BoardCoordinate(4, 8)) || cell.Coordinate.Equals(new BoardCoordinate(8, 8)) ? RabbitColor.Type5 : RabbitColor.Type2 });
                    LevelRuntimeState work = new BoardActionExecutor(source).State;
                    TurnEffectContext context = (TurnEffectContext)Invoke(typeof(TargetPowerVerification), "Context");
                    List<EffectRecord> effects = (List<EffectRecord>)Invoke(typeof(TargetPowerVerification), "Effects", work, new BoardCoordinate(4, 0), context);
                    TargetingRecord lost = context.Targeting.Single(record => record.Event == TargetingEvent.Retargeted);
                    Results.Add("PASS 실제 부모 로켓의 예약 목표 소실 fixture");
                    PropertyInfo property = typeof(PowerPresentationTrace).GetProperty("Flights");
                    if (property == null) throw new InvalidOperationException("최초 목표와 돌진 중 소실을 잇는 비행 기록이 없음");
                    object flight = ((System.Collections.IEnumerable)property.GetValue(context.PowerTrace)).Cast<object>().Single();
                    Type type = flight.GetType();
                    BoardCoordinate initial = (BoardCoordinate)type.GetProperty("InitialTarget").GetValue(flight);
                    BoardCoordinate landing = (BoardCoordinate)type.GetProperty("LandingTarget").GetValue(flight);
                    int interrupted = (int)type.GetProperty("InterruptedByEffect").GetValue(flight);
                    Results.Add((initial.Equals(lost.Target.Value) && !initial.Equals(landing) ? "PASS " : "FAIL ") + "소실된 최초 목표와 최종 목표 별도 보존");
                    Results.Add((interrupted >= 0 && interrupted < effects.Count && effects[interrupted].Target.Equals(initial) ? "PASS " : "FAIL ") + "실제 소실 효과와 비행 정지 시점 연결");
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            });
        }

        private static void Run(string name, Action action)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 검사 Editor에서만 실행한다.");
            Directory.CreateDirectory(Output); Results.Clear();
            try { action(); }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); }
            finally
            {
                List<LevelDefinition> levels = (List<LevelDefinition>)typeof(TargetPowerVerification)
                    .GetField("Definitions", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                foreach (LevelDefinition level in levels.Where(level => level != null)) UnityEngine.Object.DestroyImmediate(level);
                levels.Clear();
                File.WriteAllLines(Output + "/" + name + "-results.txt", Results);
            }
            EditorApplication.Exit(Results.Any(line => line.StartsWith("FAIL ")) ? 1 : 0);
        }
    }
}
