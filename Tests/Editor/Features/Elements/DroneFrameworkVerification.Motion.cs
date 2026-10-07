using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using GameScreen;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEngine;

namespace Elements.Editor
{
    public static partial class DroneFrameworkVerification
    {
        public static void Motion()
        {
            Run("motion", () =>
            {
                foreach (int pair in new[] { 2, 4, 5, 8 })
                {
                    LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", pair, RocketDirection.Horizontal, null);
                    try
                    {
                        LevelRuntimeState source = LevelStateBuilder.Build(level, 12345).State;
                        BoardActionExecutor executor = new BoardActionExecutor(source);
                        BoardActionResult action = executor.Swap(new BoardCoordinate(4, 4), new BoardCoordinate(4, 5));
                        if (!action.IsApplied) throw new InvalidOperationException("비행 fixture 발동 실패 " + pair);
                        PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(source, action.Changes, action.Effects, action.PowerTrace);
                        PropertyInfo property = typeof(PuzzleEffectTimeline).GetProperty("Flights");
                        if (property == null) throw new InvalidOperationException("상승/호버/돌진 구간을 연결하는 표시 비행이 없음");
                        object[] flights = ((IEnumerable)property.GetValue(timeline)).Cast<object>().ToArray();
                        Results.Add((flights.Length == action.PowerTrace.Flights.Count && flights.Length > 0 ? "PASS " : "FAIL ") + "모든 조합의 예약 비행 표시 연결 " + pair);
                        Type playbackType = typeof(PuzzleGameSession).Assembly.GetType("GameScreen.PuzzlePowerPlayback");
                        object playback = Activator.CreateInstance(playbackType, true);
                        // 실제 Begin과 같이 등록 시각 조회를 제공한다. 이미지 로드는 이 비행 구간 검사에 필요하지 않다.
                        using PuzzleArtwork artwork = new PuzzleArtwork();
                        playbackType.GetField("art", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(playback, artwork);
                        playbackType.GetField("timeline", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(playback, timeline);
                        playbackType.GetField("final", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(playback, executor.State);
                        playbackType.GetMethod("BuildClips", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(playback, new object[] { source });
                        object[] clips = ((IEnumerable)playbackType.GetField("clips", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(playback)).Cast<object>().ToArray();
                        DroneFlightMotion.Phase[] expected = timeline.Flights.SelectMany(item => item.Phases).ToArray();
                        object[] rotorClips = clips.Where(clip => ((string[])clip.GetType().GetField("Paths", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(clip))
                            .Any(path => path.Contains("collection-drone-rotor"))).ToArray();
                        Results.Add((rotorClips.Length == expected.Length && rotorClips.Select((clip, index) => ReferenceEquals(
                            clip.GetType().GetField("DronePhase", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(clip), expected[index])).All(value => value)
                            ? "PASS " : "FAIL ") + "화면 클립이 실제 비행 구간 전체를 재사용 " + pair);
                        foreach (object flight in flights)
                        {
                            Type type = flight.GetType();
                            object[] phases = ((IEnumerable)type.GetProperty("Phases").GetValue(flight)).Cast<object>().ToArray();
                            MethodInfo position = type.GetMethod("PositionAt");
                            object first = phases.First(); Type phaseType = first.GetType();
                            float begin = (float)phaseType.GetProperty("Start").GetValue(first);
                            float riseEnd = (float)phaseType.GetProperty("End").GetValue(first);
                            DroneFlightRecord record = (DroneFlightRecord)type.GetProperty("Record").GetValue(flight);
                            Vector3 origin = PuzzleWorldBoard.CellPosition(record.Origin);
                            Vector3 from = (Vector3)position.Invoke(flight, new object[] { begin });
                            Vector3 lifted = (Vector3)position.Invoke(flight, new object[] { riseEnd });
                            Results.Add((phaseType.GetProperty("Kind").GetValue(first).ToString() == "Rise" && Vector3.Distance(origin, from) < .001f &&
                                Mathf.Abs(lifted.y - origin.y - .65f) < .001f && Mathf.Abs(riseEnd - begin - .5f) < .001f ? "PASS " : "FAIL ") + "다수/조합도 발생 위치에서 천천히 상승 " + pair + ":" + record.Request);
                            Results.Add((phases.All(phase => phase.GetType().GetProperty("Kind").GetValue(phase).ToString() != "Orbit") &&
                                phases.Any(phase => phase.GetType().GetProperty("Kind").GetValue(phase).ToString() == "Hover") ? "PASS " : "FAIL ") + "원 선회 없이 호버와 돌진 " + record.Request);
                            for (int i = 1; i < phases.Length; i++)
                            {
                                float previousEnd = (float)phaseType.GetProperty("End").GetValue(phases[i - 1]);
                                float nextStart = (float)phaseType.GetProperty("Start").GetValue(phases[i]);
                                Vector3 previousPosition = (Vector3)phaseType.GetMethod("PositionAt").Invoke(phases[i - 1], new object[] { previousEnd });
                                Vector3 nextPosition = (Vector3)phaseType.GetMethod("PositionAt").Invoke(phases[i], new object[] { nextStart });
                                Results.Add((Mathf.Abs(previousEnd - nextStart) < .001f && Vector3.Distance(previousPosition, nextPosition) < .001f ? "PASS " : "FAIL ") + "구간 경계 시간/위치 연속 " + record.Request + ":" + i);
                            }
                        }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
            });
        }
        public static void LossMotion()
        {
            Run("loss-motion", () =>
            {
                bool found = false;
                foreach (int pair in new[] { 4, 2, 8, 5 })
                {
                    for (int seed = 1; seed <= 50 && !found; seed++)
                    {
                        LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", pair, RocketDirection.Horizontal, null);
                        try
                        {
                            LevelRuntimeState source = LevelStateBuilder.Build(level, seed).State;
                            var fixtureRandom = new System.Random(seed);
                            foreach (RuntimeCell cell in source.Cells.Where(cell => cell.Content == RuntimeContent.Normal))
                            {
                                int choice = fixtureRandom.Next(12);
                                if (choice > 1) continue;
                                cell.GetType().GetProperty("Content").GetSetMethod(true).Invoke(cell, new object[] { choice == 0 ? RuntimeContent.Bomb : RuntimeContent.Rocket });
                                cell.GetType().GetProperty("RocketDirection").GetSetMethod(true).Invoke(cell, new object[] { RocketDirection.Horizontal });
                            }
                            BoardActionExecutor executor = new BoardActionExecutor(source);
                            BoardActionResult action = executor.Swap(new BoardCoordinate(4, 4), new BoardCoordinate(4, 5));
                            if (!action.IsApplied) continue;
                            PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(source, action.Changes, action.Effects, action.PowerTrace);
                            bool PhysicalLoss(DroneFlightMotion item, DroneFlightMotion.Phase phase) => phase.Interrupted && item.Record.Retargets.Any(change =>
                                Mathf.Abs(timeline.Reactions[change.EffectIndex].Time - phase.End) < .001f &&
                                Vector3.Distance(PuzzleWorldBoard.CellPosition(change.LostTarget), phase.To) < .001f &&
                                timeline.Reactions[change.EffectIndex].Record.Target.Equals(change.LostTarget) &&
                                (timeline.Reactions[change.EffectIndex].Record.Response == DamageResponse.Remove || timeline.Reactions[change.EffectIndex].Record.Response == DamageResponse.Activate));
                            DroneFlightMotion motion = timeline.Flights.FirstOrDefault(item => item.Record.LandingTarget.HasValue && item.Phases.Any(phase => PhysicalLoss(item, phase)));
                            if (motion == null) continue;
                            found = true;
                            DroneFlightMotion.Phase interrupted = motion.Phases.First(phase => PhysicalLoss(motion, phase));
                            float lossTime = motion.Record.Retargets.Select(change => timeline.Reactions[change.EffectIndex].Time)
                                .First(time => Mathf.Abs(time - interrupted.End) < .001f);
                            int index = motion.Phases.IndexOf(interrupted);
                            DroneFlightMotion.Phase hover = motion.Phases[index + 1];
                            Results.Add((interrupted.Kind == DroneFlightPhaseKind.Dash && interrupted.Start < lossTime && Mathf.Abs(interrupted.End - lossTime) < .001f ? "PASS " : "FAIL ") + "실제 연쇄 효과가 돌진 중 현재 목표 제거 " + pair + ":" + seed);
                            Results.Add((Vector3.Distance(interrupted.PositionAt(lossTime), hover.From) < .001f && hover.Kind == DroneFlightPhaseKind.Hover &&
                                Vector3.Distance(hover.PositionAt(hover.Start), hover.PositionAt(hover.End)) < .001f && hover.End > hover.Start ? "PASS " : "FAIL ") + "소실 순간 현재 비행 위치에서 정지·호버");
                            Results.Add((motion.Phases.Last().Kind == DroneFlightPhaseKind.Dash && Vector3.Distance(interrupted.To, PuzzleWorldBoard.CellPosition(motion.Record.LandingTarget.Value)) > .001f &&
                                Vector3.Distance(motion.Phases.Last().To, PuzzleWorldBoard.CellPosition(motion.Record.LandingTarget.Value)) < .001f ? "PASS " : "FAIL ") + "다른 실제 예약 목표로 다시 돌진");
                            File.WriteAllText(Output + "/loss-motion-fixture.json", JsonUtility.ToJson(level, true));
                            File.WriteAllText(Output + "/loss-motion-source.json", (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", source));
                            File.WriteAllText(Output + "/loss-motion-trace.json", (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", action.PowerTrace));
                        }
                        finally { UnityEngine.Object.DestroyImmediate(level); }
                    }
                    if (found) break;
                }
                Results.Add((found ? "PASS " : "FAIL ") + "실제 돌진 중 소실 fixture 확보");
            });
        }
        public static void NoTargetMotion()
        {
            Run("no-target-motion", () =>
            {
                FixedInput input = File.ReadAllLines(Output + "/baseline-observations.jsonl")
                    .Select(line => JsonUtility.FromJson<FixedInput>(line)).First(row => row.name == "timeline-retarget");
                LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
                try
                {
                    JsonUtility.FromJsonOverwrite(input.input, level);
                    LevelRuntimeState source = LevelStateBuilder.Build(level, input.seed).State;
                    foreach (RuntimeCell cell in source.Cells.Where(cell => cell.Content == RuntimeContent.Normal))
                    {
                        bool retained = cell.Coordinate.Equals(new BoardCoordinate(4, 8));
                        cell.GetType().GetProperty("Content").GetSetMethod(true).Invoke(cell, new object[] { retained ? RuntimeContent.Normal : RuntimeContent.Empty });
                        cell.GetType().GetProperty("Color").GetSetMethod(true).Invoke(cell, new object[] { retained ? (RabbitColor?)RabbitColor.Type5 : null });
                    }
                    LevelRuntimeState work = new BoardActionExecutor(source).State;
                    TurnEffectContext context = (TurnEffectContext)Invoke(typeof(TargetPowerVerification), "Context");
                    List<EffectRecord> effects = (List<EffectRecord>)Invoke(typeof(TargetPowerVerification), "Effects", work, new BoardCoordinate(4, 0), context);
                    DroneFlightRecord record = context.PowerTrace.Flights.Single();
                    Results.Add((!record.LandingTarget.HasValue && record.LandingHitGroup == 0 ? "PASS " : "FAIL ") + "최초 예약 목표 소실 후 후보 없음·착탄 없음");
                    PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(source, Array.Empty<MatchedBlockChange>(), effects, context.PowerTrace);
                    DroneFlightMotion motion = timeline.Flights.Single();
                    Results.Add((motion.Phases.Last().Kind == DroneFlightPhaseKind.Hover && !float.IsInfinity(motion.End) && motion.End <= timeline.Duration &&
                        !timeline.Attacks.Any(attack => attack.Record.IsFlight) ? "PASS " : "FAIL ") + "후보 없는 비행은 유한 호버 후 종료·중복 착탄 없음");
                    Results.Add((context.Targeting.Any(item => item.Event == TargetingEvent.NoTarget) ? "PASS " : "FAIL ") + "재탐색 후보 없음 기록 유지");
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            });
        }
        public static void RepeatSelection()
        {
            Run("repeat-selection", () =>
            {
                LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", 8, RocketDirection.Horizontal, null);
                try
                {
                    LevelRuntimeState source = LevelStateBuilder.Build(level, 25).State;
                    var fixtureRandom = new System.Random(25);
                    foreach (RuntimeCell cell in source.Cells.Where(cell => cell.Content == RuntimeContent.Normal))
                    {
                        int choice = fixtureRandom.Next(12);
                        if (choice > 1) continue;
                        cell.GetType().GetProperty("Content").GetSetMethod(true).Invoke(cell, new object[] { choice == 0 ? RuntimeContent.Bomb : RuntimeContent.Rocket });
                        cell.GetType().GetProperty("RocketDirection").GetSetMethod(true).Invoke(cell, new object[] { RocketDirection.Horizontal });
                    }
                    BoardActionExecutor executor = new BoardActionExecutor(source);
                    BoardActionResult action = executor.Swap(new BoardCoordinate(4, 4), new BoardCoordinate(4, 5));
                    if (!action.IsApplied) throw new InvalidOperationException("반복 재선택 fixture 발동 실패");
                    PropertyInfo history = typeof(DroneFlightRecord).GetProperty("Retargets");
                    if (history == null) throw new InvalidOperationException("여러 차례 실제 목표 재선택 이력 없음");
                    object[] histories = action.PowerTrace.Flights.Select(item => history.GetValue(item)).ToArray();
                    Results.Add((action.PowerTrace.Flights.Select(item => item.Request).Distinct().Count() == action.PowerTrace.Flights.Count ? "PASS " : "FAIL ") + "요청당 비행 결과 하나·중복 착탄 없음");
                    int[] landingHits = action.PowerTrace.Flights.Where(item => item.LandingHitGroup != 0).Select(item => item.LandingHitGroup).ToArray();
                    Results.Add((landingHits.Distinct().Count() == landingHits.Length && landingHits.All(hit => action.PowerTrace.Attacks.Count(attack => attack.IsFlight && attack.HitGroup == hit) == 1) ? "PASS " : "FAIL ") + "착탄 hit와 실제 공격 일대일");
                    Results.Add((executor.State.Missions.All(mission => mission.Progress >= 0 && mission.Progress <= mission.Target) ? "PASS " : "FAIL ") + "반복 재선택은 미션 진행 중복 없음");
                    foreach (DroneFlightRecord flight in action.PowerTrace.Flights)
                    {
                        Results.Add((flight.Retargets.All(change => change.EffectIndex >= flight.ReservedAfterEffects && change.EffectIndex < action.Effects.Count) ? "PASS " : "FAIL ") + "재선택 원인은 예약 이후 실제 효과 " + flight.Request);
                        Results.Add((flight.Retargets.Select((change, index) => index == flight.Retargets.Count - 1 || change.NextTarget.HasValue && change.NextTarget.Value.Equals(flight.Retargets[index + 1].LostTarget)).All(value => value)
                            ? "PASS " : "FAIL ") + "이전 선택에서 다음 소실로 이어지는 이력 " + flight.Request);
                        if (flight.Retargets.Count > 0)
                            Results.Add((flight.Retargets.Last().NextTarget.Equals(flight.LandingTarget) ? "PASS " : "FAIL ") + "마지막 재선택 목표와 실제 착탄/후보 없음 일치 " + flight.Request);
                    }
                    Results.Add((histories.Any(item => ((IEnumerable)item).Cast<object>().Count() >= 2) ? "PASS " : "FAIL ") + "한 요청에서 효과 도중 여러 차례 실제 재선택");
                    PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(source, action.Changes, action.Effects, action.PowerTrace);
                    foreach (DroneFlightMotion flight in timeline.Flights)
                    {
                        for (int index = 1; index < flight.Phases.Count; index++)
                        {
                            DroneFlightMotion.Phase previous = flight.Phases[index - 1], next = flight.Phases[index];
                            Results.Add((next.End > next.Start && Mathf.Abs(previous.End - next.Start) < .001f &&
                                Vector3.Distance(previous.PositionAt(previous.End), next.PositionAt(next.Start)) < .001f ? "PASS " : "FAIL ") + "반복 재탐색 구간 시간·위치 연속 " + flight.Record.Request + ":" + index);
                        }
                    }
                    File.WriteAllText(Output + "/repeat-selection-state.txt", (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", executor.State));
                    File.WriteAllText(Output + "/repeat-selection-trace.txt", (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", action.PowerTrace));
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            });
        }
        public static void Comparison()
        {
            Run("comparison", () =>
            {
                string before = "{Attacks=[{Damage=2}]|State={Seed=1|Mission=3}}";
                string after = "{Attacks=[{Damage=2}]|Flights=[{Retargets=[{NextTarget=null}]}]|State={Seed=1|Mission=3}}";
                Results.Add((RecordedLogicComparison.Equal(new[] { before }, new[] { after }) ? "PASS " : "FAIL ") + "새 표시 이력만 분리해 기존 모든 필드 비교");
                foreach (string changed in new[] { after.Replace("Damage=2", "Damage=1"), after.Replace("Seed=1", "Seed=2"), after.Replace("Mission=3", "Mission=4") })
                    Results.Add((!RecordedLogicComparison.Equal(new[] { before }, new[] { changed }) ? "PASS " : "FAIL ") + "피해·난수·미션 논리 차이는 거부");
                Results.Add((!RecordedLogicComparison.Equal(new[] { before }, new[] { after, after }) && !RecordedLogicComparison.Equal(new[] { before }, Array.Empty<string>()) ? "PASS " : "FAIL ") + "행 개수 차이 거부");
                Results.Add((RecordedLogicComparison.Equal(new[] { after }, new[] { after }) && !RecordedLogicComparison.Equal(new[] { after }, new[] { after.Replace("NextTarget=null", "NextTarget=1") }) ? "PASS " : "FAIL ") + "기준에 이미 있는 표시 기록 차이도 거부");
            });
        }
    }
}
