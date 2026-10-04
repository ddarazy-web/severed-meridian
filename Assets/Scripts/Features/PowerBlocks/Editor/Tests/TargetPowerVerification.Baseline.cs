using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using GameScreen;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class TargetPowerVerification
    {
        private const string BaselineOutput = "Logs/ElementFramework/Stage02/";
        private static readonly List<string> BaselineRows = new List<string>();

        [Serializable]
        private sealed class DroneObservation
        {
            public string name, action, input, state, context, candidates, effects, attacks, reactions;
            public int seed, randomDraws, candidateCount, reservations, expectedComplete, expectedDamage, expectedCharge, cacheBuilds, displayedProgress;
            public int[] actualMissionProgress;
            public float duration;
        }

        public static void Baseline()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("EF-02 검사는 별도 배치 에디터에서 실행하세요.");
            Directory.CreateDirectory(BaselineOutput); Results.Clear(); BaselineRows.Clear(); int exit = 0;
            try
            {
                ReservationChecks();
                ObserveColorPolicy();
                ObserveBodyReservations();
                ObserveGeneratorReservations();
                ObserveRetargetTimeline();
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                foreach (LevelDefinition level in Definitions.Where(level => level != null)) UnityEngine.Object.DestroyImmediate(level);
                Definitions.Clear();
                File.WriteAllLines(BaselineOutput + "baseline-results.txt", Results);
                File.WriteAllLines(BaselineOutput + "drone-observations.jsonl", BaselineRows);
            }
            EditorApplication.Exit(exit);
        }

        // 내부 기준 기록이며 봇 공개 관찰이나 사용자 저장 데이터에는 연결하지 않는다.
        private static void ObserveDrone(string name, string action, LevelDefinition input, int seed,
            LevelRuntimeState state, TurnEffectContext context, DroneTargetManager manager,
            IEnumerable<DroneTarget> candidates, IEnumerable<EffectRecord> effects = null, PuzzleEffectTimeline timeline = null, int displayedProgress = -1)
        {
            DroneTarget[] captured = candidates.ToArray();
            BaselineRows.Add(JsonUtility.ToJson(new DroneObservation
            {
                name = name, action = action, input = JsonUtility.ToJson(input), seed = seed,
                state = Snapshot(state), context = Snapshot(context), candidates = Snapshot(captured),
                effects = Snapshot(effects), attacks = Snapshot(timeline?.Attacks), reactions = Snapshot(timeline?.Reactions),
                randomDraws = state.Random.DrawCount, candidateCount = captured.Length,
                reservations = manager?.ReservationCount ?? -1, expectedComplete = manager?.ExpectedComplete ?? -1,
                expectedDamage = manager?.ExpectedDamage ?? -1, expectedCharge = manager?.ExpectedCharge ?? -1,
                cacheBuilds = manager?.CacheBuildCount ?? -1, actualMissionProgress = state.Missions.Select(m => m.Progress).ToArray(),
                duration = timeline?.Duration ?? 0, displayedProgress = displayedProgress
            }));
        }

        private static void ObserveColorPolicy()
        {
            LevelDefinition level = Make(); Mission(level, MissionKind.Color, 1);
            LevelRuntimeState state = Build(level, 7); TurnEffectContext context = Context();
            foreach (RuntimeCell cell in state.Cells.Where(cell => cell.Content == RuntimeContent.Normal)) Set(cell, "Color", RabbitColor.Type2);
            Set(state.CellAt(C(0, 0)), "Color", RabbitColor.Type1); Set(state.CellAt(C(8, 8)), "Color", RabbitColor.Type1);
            Set(state.CellAt(C(8, 8)), "Cover", (CoverKind?)CoverKind.Web); Set(state.CellAt(C(8, 8)), "CoverDurability", 1);
            DroneTargetManager manager = Manager(state, context);
            string before = Snapshot(state), history = Snapshot(context); int draws = state.Random.DrawCount;
            DroneTarget[] direct = manager.Query().ToArray(); manager.Query();
            Check(direct.Length == 1 && direct[0].Coordinate.Equals(C(0, 0)) && !direct[0].Contributions[0].IsFallback,
                "노출 색 목표가 같은 색 덮개 대체보다 우선");
            Check(Snapshot(state) == before && Snapshot(context) == history && state.Random.DrawCount == draws && manager.ReservationCount == 0 && manager.CacheBuildCount == 1,
                "후보 반복 조회 상태/문맥/난수/예약 무변경");
            ObserveDrone("color-direct", "setup: all Type2; (0,0)/(8,8) Type1; web (8,8)=1; Query twice", level, 7, state, context, manager, direct);
            int request = (int)Invoke(typeof(DroneTargetManager), "Request", manager, C(4, 4));
            Check(state.Random.DrawCount == draws && manager.ExpectedComplete == 1 && manager.Query().All(t => !t.IsMission), "단일 후보 무난수/예정 완료 후 일반 대체");
            ObserveDrone("color-reserved", "Request (4,4)", level, 7, state, context, manager, manager.Query());
            Set(state.CellAt(C(0, 0)), "Content", RuntimeContent.Empty); Set(state.CellAt(C(0, 0)), "Color", null);
            Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            DroneTarget[] fallback = manager.Query().ToArray();
            Check(fallback.Length == 1 && fallback[0].Coordinate.Equals(C(8, 8)) && fallback[0].Contributions[0].IsFallback && manager.ExpectedComplete == 0,
                "다른 예약 목표 소실 후 덮개 대체 복구/예약은 착탄 전 유지");
            ObserveDrone("color-lost", "remove (0,0); Invalidate; Query", level, 7, state, context, manager, fallback);
            DroneTarget landed = (DroneTarget)Invoke(typeof(DroneTargetManager), "Land", manager, request, C(4, 4));
            Check(landed.Coordinate.Equals(C(8, 8)) && manager.ReservationCount == 0 && context.Targeting.Any(r => r.Event == TargetingEvent.Retargeted), "미션 재선정/자기 예약 해제");
            ObserveDrone("color-retarget", "Land -> (8,8) web", level, 7, state, context, manager, new[] { landed });
            List<EffectRecord> effects = Effects(state, C(8, 8), context); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            Check(manager.Query().Single().Contributions.All(c => !c.IsFallback) && state.Missions[0].Progress == 0, "덮개 제거 뒤 직접 색 후보/실제 미션은 아직0");
            ObserveDrone("color-uncovered", "Hit web (8,8); Invalidate", level, 7, state, context, manager, manager.Query(), effects);
            Set(state.Missions[0], "Progress", 1); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            Check(manager.Query().All(t => !t.IsMission), "완료 미션 후보 제외");
            ObserveDrone("color-completed", "set mission progress=1; Invalidate", level, 7, state, context, manager, manager.Query());
        }

        private static void ObserveBodyReservations()
        {
            LevelDefinition level = Make();
            Invoke(typeof(FixedObstacleVerification), "Obstacle", null, level, ObstacleKind.Appliance, 2, C(4, 4), RabbitColor.Type1);
            Mission(level, MissionKind.Appliance, 1);
            LevelRuntimeState state = Build(level, 19); TurnEffectContext context = Context(); DroneTargetManager manager = Manager(state, context);
            ObserveDrone("body-before", "2x2 (4,4) durability=2; Query", level, 19, state, context, manager, manager.Query());
            Invoke(typeof(DroneTargetManager), "Request", manager, C(0, 0));
            Check(manager.ExpectedDamage == 1 && manager.ExpectedComplete == 0 && manager.Query().Count == 3, "2x2 첫 칸 예약/나머지 세 칸");
            ObserveDrone("body-one", "Request (0,0)", level, 19, state, context, manager, manager.Query());
            Invoke(typeof(DroneTargetManager), "Request", manager, C(0, 1));
            Check(manager.ExpectedDamage == 2 && manager.ExpectedComplete == 1 && manager.Query().All(t => !t.IsMission) && state.Missions[0].Progress == 0,
                "2x2 두 칸 예약 완료 예측/과다 예약 제외/실제 진행0");
            ObserveDrone("body-two", "Request (0,1)", level, 19, state, context, manager, manager.Query());
            TurnEffectContext areaContext = Context(); DroneTargetManager areaManager = Manager(state, areaContext);
            DroneTarget[] blast = areaManager.QueryArea(PowerArea.Blast3).ToArray();
            DroneTarget adjacent = blast.First(t => t.Coordinate.Equals(C(3, 3)));
            Check(adjacent.Content == RuntimeContent.Normal && adjacent.Contributions.Single().Damage == 1 && adjacent.Impacts.Single().Coordinate.Equals(C(4, 4)),
                "범위 조준 중심은 목표 밖 일반 칸도 가능");
            Check(blast.First(t => t.Coordinate.Equals(C(4, 4))).Contributions.Single().Damage == 2, "범위 피해는 본체 남은 내구도로 제한");
            ObserveDrone("body-area", "fresh manager/context; Blast3 includes center (3,3)", level, 19, state, areaContext, areaManager, blast);
        }

        private static void ObserveGeneratorReservations()
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", null, ObstacleKind.Appliance, 3);
            try
            {
                LevelRuntimeState state = Build(level); TurnEffectContext context = Context(); DroneTargetManager manager = Manager(state, context);
                Dictionary<int, DroneTarget> reservations = (Dictionary<int, DroneTarget>)typeof(DroneTargetManager).GetField("reservations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                DroneTarget generator = manager.Query().First(t => t.ObstacleIndex == 0); reservations.Add(1, generator);
                Check(manager.ExpectedCharge == 1 && manager.ExpectedComplete == 0 && manager.Query().All(t => t.ObstacleIndex != 0), "미완충 발전기 전체 단일 예약");
                ObserveDrone("generator-partial", "generator (4,4) charge0/3; reserve generator candidate #1", level, 12345, state, context, manager, manager.Query());
                reservations.Clear(); Set(state.Obstacles[0], "Charge", 2); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
                reservations.Add(2, manager.Query().First(t => t.ObstacleIndex == 0));
                Check(manager.ExpectedComplete == 1 && manager.Query().All(t => t.ObstacleIndex != 0 && t.ObstacleIndex != 1), "완충 예약은 연결 본체 제거 예측/중복 후보 제외");
                ObserveDrone("generator-full", "charge=2; Invalidate; reserve generator #2", level, 12345, state, context, manager, manager.Query());
                reservations.Clear();
                Check(manager.ExpectedCharge == 0 && manager.Query().Any(t => t.ObstacleIndex == 0), "예약 취소 기여/후보 복구");
                ObserveDrone("generator-cancel", "clear owned test reservations", level, 12345, state, context, manager, manager.Query());
            }
            finally { UnityEngine.Object.DestroyImmediate(level); }
        }

        private static void ObserveRetargetTimeline()
        {
            LevelDefinition level = Make(); Mission(level, MissionKind.Color, 2, RabbitColor.Type5);
            Place(level, C(4, 0), InitialBlockKind.Rocket); Place(level, C(4, 2), InitialBlockKind.Drone);
            bool found = false;
            for (int seed = 0; seed < 16 && !found; seed++)
            {
                LevelRuntimeState source = Build(level, seed);
                foreach (RuntimeCell cell in source.Cells.Where(c => c.Content == RuntimeContent.Normal))
                    Set(cell, "Color", cell.Coordinate.Equals(C(4, 8)) || cell.Coordinate.Equals(C(8, 8)) ? RabbitColor.Type5 : RabbitColor.Type2);
                LevelRuntimeState work = new BoardActionExecutor(source).State; TurnEffectContext context = Context();
                // 색을 제한한 fixture에는 시작 매칭이 있으므로 기존 재선정 검사와 같은 효과 실행 경로를 사용한다.
                List<EffectRecord> effects = Effects(work, C(4, 0), context);
                if (!context.Targeting.Any(r => r.Event == TargetingEvent.Retargeted)) continue;
                string before = Snapshot(work), trace = Snapshot(context.PowerTrace), history = Snapshot(context);
                int draws = work.Random.DrawCount;
                PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(source, Array.Empty<MatchedBlockChange>(), effects, context.PowerTrace);
                Check(Snapshot(work) == before && Snapshot(context.PowerTrace) == trace && Snapshot(context) == history && work.Random.DrawCount == draws,
                    "타임라인 생성은 상태/목표 기록/예약 문맥/난수 무변경");
                PuzzleEffectTimeline.Attack flight = timeline.Attacks.Single(a => a.Record.IsFlight);
                Check(flight.Record.Retargeted && flight.Record.Center.Equals(C(8, 8)) && work.Missions[0].Progress == 2, "최종 재선정 착탄점/실제 미션2");
                Check(timeline.Attacks.Take(flight.Record.WaitForAttacks).All(a => flight.Start >= a.End) &&
                    timeline.Reactions.Where(r => r.Record.HitGroup == flight.Record.HitGroup).All(r => r.Time >= flight.ImpactAt(r.Record.Target)), "선행 공격 대기/착탄 후 반응");
                ObserveDrone("timeline-retarget", "setup Type5=(4,8)/(8,8), others Type2; Apply effects (4,0); create timeline", level, seed,
                    work, context, null, Array.Empty<DroneTarget>(), effects, timeline);
                PuzzleProgressFeedback feedback = new PuzzleProgressFeedback(); feedback.Initialize(source);
                feedback.Schedule(work, record => timeline.Reactions.Where(r => r.Record.Target.Equals(record.Source.Value) && r.Record.Response == DamageResponse.Remove).Max(r => r.Time));
                Check(feedback.Display.Progress(0) == 0 && work.Missions[0].Progress == 2, "규칙 완료와 표시 진행 분리");
                ObserveDrone("timeline-display-pending", "feedback scheduled before collection arrival", level, seed,
                    work, context, null, Array.Empty<DroneTarget>(), effects, timeline, feedback.Display.Progress(0));
                feedback.Tick(timeline.Duration + 1); feedback.Tick(1);
                Check(feedback.Display.Progress(0) == 2, "착탄/수집 이후 표시 진행2");
                ObserveDrone("timeline-display-complete", "feedback tick duration+1 then 1; displayed progress=2", level, seed,
                    work, context, null, Array.Empty<DroneTarget>(), effects, timeline, feedback.Display.Progress(0));
                found = true;
            }
            Check(found, "실제 연쇄 재선정 타임라인 사례 확보");
        }
    }
}
