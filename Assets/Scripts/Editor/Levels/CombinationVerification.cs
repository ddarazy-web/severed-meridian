using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class CombinationVerification
    {
        private const string Evidence = "Logs/CombinationVerification";
        private static readonly List<string> Results = new List<string>();
        private static readonly (InitialBlockKind a, InitialBlockKind b, PowerCombinationKind kind)[] Pairs =
        {
            (InitialBlockKind.Rocket, InitialBlockKind.Rocket, PowerCombinationKind.RocketRocket),
            (InitialBlockKind.Rocket, InitialBlockKind.Bomb, PowerCombinationKind.RocketBomb),
            (InitialBlockKind.Rocket, InitialBlockKind.Drone, PowerCombinationKind.RocketDrone),
            (InitialBlockKind.Bomb, InitialBlockKind.Bomb, PowerCombinationKind.BombBomb),
            (InitialBlockKind.Bomb, InitialBlockKind.Drone, PowerCombinationKind.BombDrone),
            (InitialBlockKind.Drone, InitialBlockKind.Drone, PowerCombinationKind.DroneDrone),
            (InitialBlockKind.Magnet, InitialBlockKind.Rocket, PowerCombinationKind.MagnetRocket),
            (InitialBlockKind.Magnet, InitialBlockKind.Bomb, PowerCombinationKind.MagnetBomb),
            (InitialBlockKind.Magnet, InitialBlockKind.Drone, PowerCombinationKind.MagnetDrone),
            (InitialBlockKind.Magnet, InitialBlockKind.Magnet, PowerCombinationKind.MagnetMagnet)
        };
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static object Invoke(Type type, string name, object owner, params object[] args)
            => type.GetMethod(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", null, value);
        private static void Set(object owner, string name, object value) => owner.GetType().GetProperty(name).GetSetMethod(true).Invoke(owner, new[] { value });
        private static void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static LevelDefinition Make(int pair = 0, RocketDirection direction = RocketDirection.Horizontal, BoardCoordinate? origin = null)
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make", null);
            BoardCoordinate first = origin ?? C(4, 4), second = C(first.Row, first.Column + 1);
            Place(level, first, Pairs[pair].a, direction); Place(level, second, Pairs[pair].b, direction);
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level); return level;
        }
        private static void Place(LevelDefinition level, BoardCoordinate coordinate, InitialBlockKind kind, RocketDirection direction = RocketDirection.Horizontal)
            => Invoke(typeof(PowerEffectVerification), "Place", null, level, coordinate, kind, direction, RabbitColor.Type1);
        private static LevelRuntimeState Build(LevelDefinition level, int seed = 12345)
        {
            LevelStateBuildResult result = LevelStateBuilder.Build(level, seed);
            if (!result.IsBuilt) throw new InvalidOperationException(string.Join(" | ", result.Issues)); return result.State;
        }
        private static TurnEffectContext Context() => (TurnEffectContext)Invoke(typeof(TargetPowerVerification), "Context", null);
        private static DroneTargetManager Manager(LevelRuntimeState state, TurnEffectContext context)
            => (DroneTargetManager)Invoke(typeof(TargetPowerVerification), "Manager", null, state, context);

        public static void Data()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { DataChecks(); File.WriteAllLines(Evidence + "/data-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/data-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static void DataChecks()
        {
            for (int pair = 0; pair < Pairs.Length; pair++)
                foreach (RocketDirection direction in new[] { RocketDirection.Horizontal, RocketDirection.Vertical })
                    foreach (bool reverse in new[] { false, true })
                    {
                        LevelDefinition level = Make(pair, direction); LevelRuntimeState source = Build(level);
                        string baseline = Snapshot(source), json = JsonUtility.ToJson(level); bool dirty = EditorUtility.IsDirty(level);
                        BoardCoordinate first = reverse ? C(4, 5) : C(4, 4), second = reverse ? C(4, 4) : C(4, 5);
                        BoardActionExecutor executor = new BoardActionExecutor(source); BoardActionResult action = executor.Swap(first, second);
                        string label = Pairs[pair].kind + "/" + direction + "/" + reverse;
                        Check(action.IsApplied && action.MovesAfter == 19 && executor.Turn == 1 && action.Changes.Count == 0, "조합/이동1/턴1/추가매칭 없음 " + label);
                        PowerCombination combination = executor.TurnEffects.Combination;
                        Check(combination.Kind == Pairs[pair].kind && combination.Center.Equals(second) && combination.First.Equals(first), "종류와 두 번째 칸 중심 " + label);
                        Check(!action.Effects.Any(e => e.Response == DamageResponse.Activate && (e.Target.Equals(first) || e.Target.Equals(second))), "두 재료 단독 발동 없음 " + label);
                        Check(Snapshot(source) == baseline && JsonUtility.ToJson(level) == json && EditorUtility.IsDirty(level) == dirty, "원본/시작/dirty 보존 " + label);
                        Check(executor.State.Missions[0].Progress == source.Cells.Count(c => c.Content == RuntimeContent.Normal && c.Color == RabbitColor.Type1) -
                            executor.State.Cells.Count(c => c.Content == RuntimeContent.Normal && c.Color == RabbitColor.Type1), "변환 포함 실제 색 소비 집계 " + label);
                        if (combination.IsTransformation)
                        {
                            PowerTransformation[] changes = combination.Transformations.ToArray();
                            Check(changes.Length == source.Cells.Count(c => c.Content == RuntimeContent.Normal && c.Color == combination.Color) && changes.All(t => t.OriginalColor == combination.Color), "선택 색 전체 선변환 " + label);
                            Check(action.Effects.Count(e => e.Response == DamageResponse.Activate) == changes.Length &&
                                changes.All(t => action.Effects.Count(e => e.Target.Equals(t.Coordinate) && e.Response == DamageResponse.Activate) == 1), "변환 파워 피격 중복 없이 각각1회 " + label);
                            Check(changes.All(t => !executor.TurnEffects.IsProtected(t.Coordinate)), "변환 파워 보호 예외 " + label);
                            if (pair == 6) Check(executor.State.Random.DrawCount == 1 + changes.Length && changes.All(t => t.Direction.HasValue), "색1회/로켓별 방향 난수 " + label);
                            if (pair == 8) Check(executor.TurnEffects.Targeting.Count(r => r.Event == TargetingEvent.Landed || r.Event == TargetingEvent.NoTarget) == changes.Length, "변환 드론 모두 요청 종료 " + label);
                        }
                        else
                        {
                            HashSet<BoardCoordinate> expected = new HashSet<BoardCoordinate>();
                            foreach (RuntimeCell cell in source.Cells.Where(c => c.Content == RuntimeContent.Normal))
                            {
                                int r = Math.Abs(cell.Coordinate.Row - second.Row), c = Math.Abs(cell.Coordinate.Column - second.Column);
                                bool hit = pair == 0 ? r == 0 || c == 0 : pair == 1 ? r <= 1 || c <= 1 : pair == 3 ? r <= 2 && c <= 2 : pair == 9 ||
                                    (pair == 5 ? r <= 1 && c <= 1 : r + c <= 1);
                                if (hit) expected.Add(cell.Coordinate);
                            }
                            foreach (TargetingRecord landing in executor.TurnEffects.Targeting.Where(r => r.Event == TargetingEvent.Landed))
                                foreach (RuntimeCell cell in source.Cells.Where(c => c.Content == RuntimeContent.Normal))
                                {
                                    int r = Math.Abs(cell.Coordinate.Row - landing.Target.Value.Row), c = Math.Abs(cell.Coordinate.Column - landing.Target.Value.Column);
                                    if (pair == 2 ? direction == RocketDirection.Horizontal ? r == 0 : c == 0 : pair == 4 ? r <= 1 && c <= 1 : r == 0 && c == 0) expected.Add(cell.Coordinate);
                                }
                            Check(expected.SetEquals(action.Effects.Where(e => e.Response == DamageResponse.Remove).Select(e => e.Target)), "정답 직접 범위/도착 범위 일치 " + label);
                            if (pair == 2 || pair == 4 || pair == 5)
                                Check(executor.TurnEffects.Targeting.Count(r => r.Event == TargetingEvent.Landed) == (pair == 5 ? 3 : 1), "조합 드론 수/착탄 " + label);
                        }
                        Check(executor.TurnEffects.Targeting.All(r => r.ReservedCount >= 0) && (!executor.TurnEffects.Targeting.Any() || executor.TurnEffects.Targeting.Last().ReservedCount == 0), "효과 종료 예약0 " + label);
                        BoardActionExecutor repeat = new BoardActionExecutor(Build(level));
                        Check(Snapshot(repeat.Swap(first, second)) == Snapshot(action) && Snapshot(repeat.TurnEffects) == Snapshot(executor.TurnEffects), "동일 시드 조합/목표/방향 재현 " + label);
                    }
            ReservationChecks(); Exceptions();
        }

        private static void ReservationChecks()
        {
            LevelDefinition level = Make();
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":1,\"count\":3}]}", level);
            foreach (BoardCoordinate coordinate in new[] { C(5, 5), C(5, 6), C(9, 9) })
                Invoke(typeof(PowerEffectVerification), "Crate", null, level, coordinate, 1);
            LevelRuntimeState state = Build(level); TurnEffectContext context = Context(); DroneTargetManager manager = Manager(state, context);
            string before = Snapshot(state), history = Snapshot(context);
            DroneTarget[] points = manager.Query().ToArray(), rows = manager.QueryArea(PowerArea.Horizontal).ToArray(), blasts = manager.QueryArea(PowerArea.Blast3).ToArray();
            Check(points.Length == 3 && rows.Any(t => t.Impacts.Count == 2) && blasts.Any(t => t.Impacts.Count == 2), "한 칸/한 줄/3x3 범위별 미션 기여");
            manager.QueryArea(PowerArea.Horizontal);
            Check(manager.CacheBuildCount == 3 && manager.ReservationCount == 0 && Snapshot(state) == before && Snapshot(context) == history, "범위별 캐시/재사용/조회 무변경");
            int id = (int)Invoke(typeof(DroneTargetManager), "RequestArea", manager, C(0, 0), PowerArea.Horizontal);
            BoardCoordinate anchor = context.Targeting.Last().Target.Value;
            DroneTarget reserved = rows.Single(t => t.Coordinate.Equals(anchor));
            Check(manager.QueryArea(PowerArea.Blast3).All(t => !reserved.Impacts.Any(i => i.Coordinate.Equals(t.Coordinate)) &&
                t.Impacts.All(i => reserved.Impacts.All(r => !r.Coordinate.Equals(i.Coordinate)))), "다른 범위 요청의 예약 좌표/예상 기여 중복 제외");
            DroneImpact disappearing = reserved.Impacts.First(); RuntimeCell lost = state.CellAt(disappearing.Coordinate);
            Set(lost, "Content", RuntimeContent.Empty); Set(lost, "ObstacleIndex", null); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            Check(manager.ExpectedComplete == reserved.Impacts.Count - 1, "범위 내 대상 소실 예상 기여 즉시 감소");
            DroneTarget landing = (DroneTarget)Invoke(typeof(DroneTargetManager), "Land", manager, id, C(0, 0));
            Check(landing != null && landing.Area == PowerArea.Horizontal && manager.ReservationCount == 0 && manager.ExpectedComplete == 0, "부분 무효화 착탄 재평가/방향 유지/예약 반환");
        }

        private static void Exceptions()
        {
            foreach (int pair in new[] { 6, 7, 8, 9 })
            {
                LevelRuntimeState state = Build(Make(pair));
                foreach (RuntimeCell cell in state.Cells.Where(c => c.Content == RuntimeContent.Normal)) { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
                BoardActionExecutor executor = new BoardActionExecutor(state); string before = Snapshot(executor.State);
                BoardActionResult result = executor.Swap(C(4, 4), C(4, 5));
                Check(pair == 9 ? result.IsApplied && result.MovesAfter == 19 : !result.IsApplied && Snapshot(executor.State) == before && executor.Turn == 0, "일반 없는 자석 조합 예외 " + pair);
            }
            foreach (int pair in new[] { 0, 1, 3, 9 })
            {
                LevelDefinition level = Make(pair, origin: C(0, 0));
                Invoke(typeof(PowerEffectVerification), "Crate", null, level, C(0, 8), 3);
                Place(level, C(0, 5), InitialBlockKind.Rocket);
                LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(0, 4), C(0, 5)) }, false);
                BoardActionExecutor executor = new BoardActionExecutor(Build(level)); BoardActionResult result = executor.Swap(C(0, 0), C(0, 1));
                Check(result.IsApplied, "가장자리/벽/상자 조합 " + pair);
                if (pair != 3) Check(executor.State.Obstacles[0].Durability == 2 && result.Effects.Count(e => e.Response == DamageResponse.Activate) == 1, "기존 로켓 단독 연쇄/상자 턴1피해 " + pair);
            }
            LevelDefinition wall = Make(); LevelFlowEditing.SetWalls(wall, new[] { new BoardEdge(C(4, 4), C(4, 5)) }, false);
            BoardActionExecutor blocked = new BoardActionExecutor(Build(wall)); string saved = Snapshot(blocked.State);
            Check(!blocked.Swap(C(4, 4), C(4, 5)).IsApplied && Snapshot(blocked.State) == saved, "벽 교환 조합 무변경 거절");
        }
    }
}
