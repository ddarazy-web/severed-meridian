using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class TargetPowerVerification
    {
        private const string Evidence = "Logs/TargetPowerVerification";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<LevelDefinition> Definitions = new List<LevelDefinition>();
        // 고정 입력 재실행 검사에서만 설정한다. 기존 단독 검사는 null을 유지한다.
        internal static Action<LevelDefinition> PrepareFixture;
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static object Invoke(Type type, string name, object owner, params object[] args)
            => type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).Invoke(owner, args);
        private static void Set(object owner, string property, object value) => owner.GetType().GetProperty(property).GetSetMethod(true).Invoke(owner, new[] { value });
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", null, value);
        private static void Check(bool valid, string message)
        { if (!valid) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static LevelDefinition Make()
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make", null);
            SerializedObject edit = new SerializedObject(level); edit.FindProperty("missions").arraySize = 0; edit.ApplyModifiedPropertiesWithoutUndo();
            Definitions.Add(level); return level;
        }
        private static void Place(LevelDefinition level, BoardCoordinate coordinate, InitialBlockKind kind, RocketDirection direction = RocketDirection.Horizontal, RabbitColor color = RabbitColor.Type1)
            => Invoke(typeof(PowerEffectVerification), "Place", null, level, coordinate, kind, direction, color);
        private static void Mission(LevelDefinition level, MissionKind kind, int count, RabbitColor color = RabbitColor.Type1)
        {
            SerializedObject serialized = new SerializedObject(level);
            SerializedProperty list = serialized.FindProperty("missions");
            int index = list.arraySize++; SerializedProperty item = list.GetArrayElementAtIndex(index);
            item.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            item.FindPropertyRelative("count").intValue = count;
            item.FindPropertyRelative("color").enumValueIndex = (int)color;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static LevelRuntimeState Build(LevelDefinition level, int seed = 12345)
        {
            PrepareFixture?.Invoke(level);
            if (level.Missions.Count == 0) Mission(level, MissionKind.Color, 100);
            LevelStateBuildResult result = LevelStateBuilder.Build(level, seed);
            if (!result.IsBuilt) throw new InvalidOperationException(string.Join("|", result.Issues));
            return result.State;
        }
        private static TurnEffectContext Context() => (TurnEffectContext)Activator.CreateInstance(typeof(TurnEffectContext), BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object[] { 1, Array.Empty<MatchedBlockChange>() }, null);
        private static DroneTargetManager Manager(LevelRuntimeState state, TurnEffectContext context) =>
            (DroneTargetManager)Activator.CreateInstance(typeof(DroneTargetManager), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { state, context }, null);
        private static List<EffectRecord> Effects(LevelRuntimeState state, BoardCoordinate origin, TurnEffectContext context)
        {
            List<EffectRecord> records = new List<EffectRecord>();
            object[] args = { state, Array.Empty<MatchedBlockChange>(), (BoardCoordinate?)origin, context, records, null };
            bool success = (bool)Invoke(typeof(PowerEffectResolution), "Apply", null, args);
            Check(success, "효과 단계 성공 " + origin + " " + args[5]); return records;
        }

        public static void Data()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { DataChecks(); File.WriteAllLines(Evidence + "/data-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/data-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static void DataChecks()
        {
            foreach (InitialBlockKind kind in new[] { InitialBlockKind.Drone, InitialBlockKind.Magnet })
                foreach (bool reverse in new[] { false, true })
                {
                    LevelDefinition level = Make(); Place(level, C(4, 4), kind); Mission(level, MissionKind.Color, 100);
                    LevelRuntimeState source = Build(level); string before = Snapshot(source), json = JsonUtility.ToJson(level);
                    RabbitColor expected = source.CellAt(C(4, 5)).Color.Value;
                    BoardActionExecutor executor = new BoardActionExecutor(source);
                    BoardActionResult result = executor.Swap(reverse ? C(4, 5) : C(4, 4), reverse ? C(4, 4) : C(4, 5));
                    Check(result.IsApplied && result.MovesAfter == 19 && executor.Turn == 1, kind + " 사용자 교환 양방향 " + reverse);
                    Check(Snapshot(source) == before && JsonUtility.ToJson(level) == json, kind + " 시작/원본 보존 " + reverse);
                    Check(executor.State.Missions[0].Progress == result.Effects.Count(e => e.Response == DamageResponse.Remove && e.OriginalColor == RabbitColor.Type1), kind + " 실제 제거 색 집계 " + reverse);
                    if (kind == InitialBlockKind.Magnet)
                        Check(executor.TurnEffects.Targeting.Single(r => r.Event == TargetingEvent.ColorSelected).Color == expected && result.RandomBefore == result.RandomAfter,
                            "자석 교환 전 상대 색/불필요 난수 없음 " + reverse);
                    else Check(executor.TurnEffects.Targeting.Count(r => r.Event == TargetingEvent.Landed) == 1 && result.Effects.Count(e => e.Response == DamageResponse.Remove) == 5,
                        "드론 중심 소모/주변 4칸/추가 1칸 " + reverse);
                    string applied = Snapshot(executor.State);
                    Check(!executor.Activate(C(4, 5)).IsApplied && Snapshot(executor.State) == applied, "대기 중 재입력 무변경 " + kind + reverse);
                }

            foreach (InitialBlockKind kind in new[] { InitialBlockKind.Drone, InitialBlockKind.Magnet })
            {
                LevelDefinition level = Make(); Place(level, C(0, 0), kind); Mission(level, MissionKind.Color, 2);
                BoardActionExecutor executor = new BoardActionExecutor(Build(level));
                BoardActionResult action = executor.Activate(C(0, 0));
                Check(action.IsApplied && executor.State.CellAt(C(0, 0)).Content == RuntimeContent.Empty, kind + " 모서리 제자리 발동");
                Check(executor.State.Missions[0].Progress <= 2, kind + " 목표 상한 제한");
                if (kind == InitialBlockKind.Drone) Check(action.Effects.Count(e => e.Response == DamageResponse.Remove) == 3, "모서리 + 유효 2칸/추가 1칸");
            }
            ReservationChecks(); MagnetChecks(); ChainChecks(); MatchAndFailureChecks();
        }

        private static void ReservationChecks()
        {
            LevelDefinition level = Make(); Mission(level, MissionKind.Color, 1); Mission(level, MissionKind.Crate, 1);
            Invoke(typeof(PowerEffectVerification), "Crate", null, level, C(7, 7), 2);
            LevelRuntimeState state = Build(level); TurnEffectContext context = Context(); DroneTargetManager manager = Manager(state, context);
            string before = Snapshot(state), history = Snapshot(context);
            ReadOnlyCollection<DroneTarget> first = manager.Query(), second = manager.Query();
            Check(first.Count == second.Count && manager.CacheBuildCount == 1 && manager.ReservationCount == 0 && Snapshot(state) == before && Snapshot(context) == history,
                "조회 보드/미션/난수/예약/기록 무변경 및 캐시 재사용");
            DroneTarget crate = first.Single(t => t.Content == RuntimeContent.Obstacle);
            Check(crate.Contributions.Single().ExpectedComplete == 0 && crate.Contributions.Single().Damage == 1, "내구도2 중간 기여/제거 예정0");
            int id = (int)Invoke(typeof(DroneTargetManager), "Request", manager, C(0, 0));
            TargetingRecord reservation = context.Targeting.Last(); BoardCoordinate target = reservation.Target.Value;
            Check(!manager.Query().Any(t => t.Coordinate.Equals(target)) && manager.ReservationCount == 1, "선택과 예약 원자적/다른 요청 중복 제외");
            RuntimeCell removed = state.CellAt(target); Set(removed, "Content", RuntimeContent.Empty); Set(removed, "Color", null); Set(removed, "ObstacleIndex", null);
            Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            DroneTarget landed = (DroneTarget)Invoke(typeof(DroneTargetManager), "Land", manager, id, C(0, 0));
            Check(landed != null && landed.IsMission && !landed.Coordinate.Equals(target) && manager.ReservationCount == 0 && manager.ExpectedComplete == 0 && manager.CacheBuildCount == 2,
                "목표 소실 후 미션부터 재검색/자기 예약 반환/변경 후 재평가");
            Check(context.Targeting.Any(r => r.Event == TargetingEvent.Retargeted), "재탐색 이유 기록");

            LevelDefinition one = Make(); Mission(one, MissionKind.Color, 1);
            LevelRuntimeState oneState = Build(one); TurnEffectContext oneContext = Context(); DroneTargetManager oneManager = Manager(oneState, oneContext);
            Invoke(typeof(DroneTargetManager), "Request", oneManager, C(0, 0));
            Check(oneManager.Query().All(t => !t.IsMission) && oneManager.ExpectedComplete == 1 && oneState.Missions[0].Progress == 0,
                "예약으로 남은 미션 충족 시 일반 대체/실제 진행 무변경");
            BoardCoordinate lost = oneContext.Targeting.Last().Target.Value;
            Set(oneState.CellAt(lost), "Content", RuntimeContent.Empty); Set(oneState.CellAt(lost), "Color", null);
            Invoke(typeof(DroneTargetManager), "Invalidate", oneManager);
            Check(oneManager.ReservationCount == 1 && oneManager.ExpectedComplete == 0 && oneManager.Query().All(t => t.IsMission),
                "다른 예약 목표 소실 시 착탄 전에도 예상 기여 재평가/신규 미션 후보 복구");
            Set(oneState.Missions[0], "Progress", 1); Invoke(typeof(DroneTargetManager), "Invalidate", oneManager);
            Check(oneManager.Query().All(t => !t.IsMission), "완료 미션 후보 제외");

            Set(context, "SettlementCount", 1);
            int crateIndex = state.Obstacles.Count - 1;
            Invoke(typeof(TurnEffectContext), "RegisterDamage", context, crateIndex); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            Check(manager.Query().All(t => t.Content != RuntimeContent.Obstacle) && context.HasDamaged(crateIndex), "턴 피해 상자 제외/예약 해제가 피해 제한 유지");
        }

        private static void MagnetChecks()
        {
            LevelDefinition level = Make(); Place(level, C(3, 3), InitialBlockKind.Magnet); Place(level, C(8, 8), InitialBlockKind.Drone);
            Invoke(typeof(PowerEffectVerification), "Crate", null, level, C(7, 7), 3);
            HashSet<RabbitColor> selected = new HashSet<RabbitColor>();
            for (int seed = 0; seed < 20; seed++)
            {
                LevelRuntimeState state = Build(level, seed);
                foreach (RuntimeCell cell in state.Cells.Where(c => c.Content == RuntimeContent.Normal)) Set(cell, "Color", RabbitColor.Type1);
                Set(state.CellAt(C(0, 0)), "Color", RabbitColor.Type2);
                TurnEffectContext context = Context(); List<EffectRecord> records = Effects(state, C(3, 3), context);
                RabbitColor color = context.Targeting.Single(r => r.Event == TargetingEvent.ColorSelected).Color.Value; selected.Add(color);
                int pick = new System.Random(seed).Next(2);
                Check(color == (RabbitColor)pick && records.Where(e => e.Response == DamageResponse.Remove).All(e => e.OriginalColor == color), "색 개수 편향 없이 존재 색 단위 선택 " + seed);
                Check(state.CellAt(C(8, 8)).Content == RuntimeContent.Drone && state.Obstacles[0].Durability == 3, "자석은 파워/상자 직접 타격 제외 " + seed);
            }
            Check(selected.Count == 2, "복수 존재 색 선택 확인");
            LevelRuntimeState single = Build(level);
            foreach (RuntimeCell cell in single.Cells.Where(c => c.Content == RuntimeContent.Normal)) Set(cell, "Color", RabbitColor.Type3);
            int draws = single.Random.DrawCount; Effects(single, C(3, 3), Context());
            Check(single.Random.DrawCount == draws, "단일 존재 색 무난수");
            LevelRuntimeState empty = Build(level);
            foreach (RuntimeCell cell in empty.Cells.Where(c => c.Content == RuntimeContent.Normal)) { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
            BoardActionExecutor executor = new BoardActionExecutor(empty); string before = Snapshot(executor.State);
            Check(!executor.Activate(C(3, 3)).IsApplied && Snapshot(executor.State) == before, "일반 블록 없는 자석 사용자 발동 무비용 거절");
            Effects(empty, C(3, 3), Context()); Check(empty.CellAt(C(3, 3)).Content == RuntimeContent.Empty, "일반 블록 없는 피격 자석 소모");
        }

        private static void ChainChecks()
        {
            LevelDefinition level = Make(); Mission(level, MissionKind.Color, 100);
            Place(level, C(4, 4), InitialBlockKind.Drone); Place(level, C(4, 5), InitialBlockKind.Drone);
            BoardActionExecutor executor = new BoardActionExecutor(Build(level)); BoardActionResult result = executor.Activate(C(4, 4));
            Check(result.IsApplied && result.Effects.Count(e => e.Response == DamageResponse.Activate) == 2, "실제 + 피격 드론 2대 연쇄");
            TargetingRecord[] reserved = executor.TurnEffects.Targeting.Where(r => r.Event == TargetingEvent.Reserved).ToArray();
            Check(reserved.Length == 2 && reserved.Select(r => r.Target).Distinct().Count() == 2 && reserved.Max(r => r.ReservedCount) == 2, "실제 연쇄 공유 예약 2개 동시 유지");
            Check(executor.TurnEffects.Targeting.Count(r => r.Event == TargetingEvent.Landed) == 2 && executor.TurnEffects.Targeting.Last().ReservedCount == 0,
                "실제 연쇄 2착탄 및 예약 종료");
            int progress = executor.State.Missions[0].Progress;
            Check(executor.Settle().IsApplied && executor.State.Missions[0].Progress == progress, "모든 착탄 뒤 정착/정착은 미션 미집계");

            LevelDefinition combo = Make(); Place(combo, C(4, 4), InitialBlockKind.Drone); Place(combo, C(4, 5), InitialBlockKind.Magnet);
            BoardActionExecutor combined = new BoardActionExecutor(Build(combo));
            Check(combined.Swap(C(4, 4), C(4, 5)).IsApplied && combined.State.MovesRemaining == 19 && combined.TurnEffects.Combination.Kind == PowerCombinationKind.MagnetDrone, "15단계 직접 자석/드론 조합 지원");

            LevelDefinition crateLevel = Make(); Place(crateLevel, C(4, 4), InitialBlockKind.Rocket); Mission(crateLevel, MissionKind.Crate, 2);
            Invoke(typeof(PowerEffectVerification), "Crate", null, crateLevel, C(4, 6), 1); Invoke(typeof(PowerEffectVerification), "Crate", null, crateLevel, C(4, 8), 2);
            BoardActionExecutor crates = new BoardActionExecutor(Build(crateLevel)); Check(crates.Activate(C(4, 4)).IsApplied, "상자 미션 로켓 발동");
            Check(crates.State.Missions[0].Progress == 1 && crates.State.Obstacles[1].Durability == 1, "상자 완전 제거만 실제 진행/부분 피해 미집계");

            LevelDefinition unsupported = Make(); Place(unsupported, C(4, 4), InitialBlockKind.Drone);
            LevelObstacleEditing.Apply(unsupported, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { C(8, 8) });
            LevelObstacleEditing.Apply(unsupported, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Safe, Durability = 1 }, new[] { C(8, 8) });
            LevelRuntimeState unsupportedState = Build(unsupported);
            typeof(RuntimeMission).GetField("<Definition>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(unsupportedState.Missions[0],
                JsonUtility.FromJson<LevelMissionDefinition>("{\"kind\":99,\"count\":1}"));
            BoardActionExecutor unavailable = new BoardActionExecutor(unsupportedState); string prior = Snapshot(unavailable.State);
            Check(!unavailable.Activate(C(4, 4)).IsApplied && Snapshot(unavailable.State) == prior, "미지원 미션 사전 거절/원자성");
        }

        private static void MatchAndFailureChecks()
        {
            foreach (InitialBlockKind kind in new[] { InitialBlockKind.Drone, InitialBlockKind.Magnet })
            {
                LevelDefinition level = (LevelDefinition)Invoke(typeof(BoardActionVerification), "RocketBoard", null); Definitions.Add(level);
                Place(level, C(2, 3), kind);
                BoardActionExecutor executor = new BoardActionExecutor(Build(level));
                RabbitColor color = executor.State.CellAt(C(3, 3)).Color.Value;
                BoardActionResult result = executor.Swap(C(2, 3), C(3, 3));
                Check(result.IsApplied, "파워/일반 교환 혼합 입력 " + kind);
                if (kind == InitialBlockKind.Magnet)
                    Check(executor.TurnEffects.Targeting.Single(r => r.Event == TargetingEvent.ColorSelected).Color == color, "교환 색 보존 혼합 입력");
            }
            LevelDefinition pattern = (LevelDefinition)Invoke(typeof(BoardActionVerification), "RocketBoard", null); Definitions.Add(pattern);
            BoardActionExecutor matched = new BoardActionExecutor(Build(pattern)); BoardActionResult match = matched.Swap(C(2, 3), C(3, 3));
            Check(match.IsApplied && match.Changes.Count == 4 && matched.State.Missions[0].Progress == 4, "매칭 파워 변환 칸 포함 색4개 집계");
            Check(matched.TurnEffects.IsProtected(C(3, 3)), "생성 파워 보호 유지");

            LevelDefinition noTarget = Make(); Place(noTarget, C(4, 4), InitialBlockKind.Drone);
            LevelRuntimeState empty = Build(noTarget);
            foreach (RuntimeCell cell in empty.Cells.Where(c => c.Content == RuntimeContent.Normal)) { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
            TurnEffectContext context = Context(); Effects(empty, C(4, 4), context);
            Check(context.Targeting.Any(r => r.Event == TargetingEvent.NoTarget) && !context.Targeting.Any(r => r.Event == TargetingEvent.Landed), "드론 후보 없음 추가 타격 없이 종료");

            LevelDefinition triple = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", null,
                new Dictionary<BoardCoordinate, int> { [C(0, 0)] = 0, [C(0, 1)] = 0, [C(0, 2)] = 0, [C(1, 0)] = 1 }, 20);
            Definitions.Add(triple);
            Invoke(typeof(PowerEffectVerification), "Crate", null, triple, C(1, 0), 2);
            BoardActionExecutor automatic = (BoardActionExecutor)Invoke(typeof(CascadeVerification), "Automatic", null, triple, 12345);
            typeof(RuntimeObstacle).GetField("<Definition>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(automatic.State.Obstacles[0], UnityEngine.JsonUtility.FromJson<ObstaclePlacementDefinition>("{\"kind\":99,\"durability\":3}"));
            string before = Snapshot(automatic.State), priorContext = Snapshot(automatic.TurnEffects);
            Check(!automatic.ResolveAutomaticMatch().IsApplied && Snapshot(automatic.State) == before && Snapshot(automatic.TurnEffects) == priorContext,
                "자동 매칭 소비 뒤 미지원 피해 실패: 미션/보드/난수/문맥 원자적 보존");
        }
    }
}


