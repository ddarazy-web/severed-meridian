using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class ScrapVerification
    {
        public static void Supplemental()
        {
            Results.Clear();
            try { MovementChecks(); InteractionChecks(); SupplyEdgeChecks(); File.WriteAllLines(Evidence + "/supplemental-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/supplemental-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static LevelDefinition Sparse(params BoardCoordinate[] cells) => (LevelDefinition)Invoke(typeof(SettlementVerification), "Make", null, cells);
        private static void Occupancy(LevelRuntimeState state, string label)
        {
            int[] live = state.Cells.Where(c => c.Content == RuntimeContent.Obstacle).Select(c => c.ObstacleIndex.Value).ToArray();
            Check(live.Distinct().Count() == live.Length && live.All(i => i >= 0 && i < state.Obstacles.Count && state.Obstacles[i].Durability > 0) &&
                state.Cells.Where(c => c.Content != RuntimeContent.Obstacle).All(c => !c.ObstacleIndex.HasValue) &&
                state.Obstacles.Select((o, i) => new { o, i }).Where(p => p.o.Durability > 0).All(p => live.Contains(p.i)), "본체 점유 무결성 " + label);
        }
        private static void MovementChecks()
        {
            foreach (GravityDirection direction in Enum.GetValues(typeof(GravityDirection)))
            {
                bool vertical = direction == GravityDirection.Up || direction == GravityDirection.Down;
                BoardCoordinate[] line = Enumerable.Range(0, 5).Select(i => vertical ? C(i, 0) : C(0, i)).ToArray();
                bool reverse = direction == GravityDirection.Up || direction == GravityDirection.Left;
                BoardCoordinate from = reverse ? line[4] : line[0], end = reverse ? line[0] : line[4];
                LevelDefinition level = Sparse(line); Place(level, from, 5); LevelFlowEditing.SetGravity(level, line, direction);
                LevelRuntimeState state = Build(level); Empty(state, line.Where(c => !c.Equals(from))); Set(state.CellAt(from), "DustDurability", 3);
                string before = Snapshot(state); MovementQuery.Find(state); MovementQuery.Find(state, true);
                SettlementResult result = SettlementResolution.Resolve(state);
                Check(result.IsApplied && result.State.CellAt(end).ObstacleIndex == 0 && result.State.Obstacles[0].Durability == 5 &&
                    result.State.CellAt(from).DustDurability == 3 && result.State.CellAt(end).DustDurability == 0 && Snapshot(state) == before, "4방향 고철/바닥/조회 무변경 " + direction);
                Occupancy(result.State, direction.ToString());
            }
            BoardCoordinate[] path = { C(3, 1), C(2, 1), C(2, 2), C(2, 3) };
            LevelDefinition routed = Sparse(path); Place(routed, path[0], 4); LevelFlowEditing.SetPath(routed, path);
            LevelRuntimeState route = Build(routed); Empty(route, path.Skip(1)); SettlementResult followed = SettlementResolution.Resolve(route);
            Check(followed.IsApplied && followed.State.CellAt(path[3]).ObstacleIndex == 0 && followed.Records.All(r => r.Kind == MovementKind.Path), "고철 꺾임 경로/끝칸"); Occupancy(followed.State, "경로");
            foreach (bool blocked in new[] { false, true })
            {
                LevelDefinition level = Sparse(C(0, 0), C(1, 0), C(1, 1), C(4, 4)); Place(level, C(0, 0), 2); LevelFlowEditing.SetPortal(level, C(0, 0), C(4, 4));
                LevelRuntimeState state = Build(level); Empty(state, new[] { C(1, 0), C(1, 1) }); if (!blocked) Empty(state, new[] { C(4, 4) });
                SettlementResult result = SettlementResolution.Resolve(state);
                Check(result.IsApplied && result.State.CellAt(blocked ? C(0, 0) : C(4, 4)).ObstacleIndex == 0 && result.Records.Count == (blocked ? 0 : 1), "고철 통로/막힌 출구 대기 " + blocked); Occupancy(result.State, "통로");
            }
            foreach (bool primary in new[] { false, true })
            {
                LevelDefinition level = Sparse(C(0, 0), C(1, 0), C(1, 1)); Place(level, C(0, 0), 3); Place(level, C(1, 0), 5);
                LevelFlowEditing.SetGravity(level, new[] { C(1, 0) }, GravityDirection.Right); LevelFlowEditing.SetPortal(level, C(0, 0), C(1, 1));
                LevelFlowEditing.SetMerge(level, C(1, 1), primary ? new[] { C(0, 0), C(1, 0) } : new[] { C(1, 0), C(0, 0) });
                LevelRuntimeState state = Build(level); Empty(state, new[] { C(1, 1) }); SettlementResult result = SettlementResolution.Resolve(state);
                Check(result.IsApplied && result.State.CellAt(C(1, 1)).ObstacleIndex == (primary ? 0 : 1), "고철 합류 우선순위 " + primary); Occupancy(result.State, "합류");
            }
            HashSet<BoardCoordinate> choices = new HashSet<BoardCoordinate>();
            for (int seed = 1; seed <= 12; seed++)
            {
                LevelDefinition level = Sparse(C(0, 1), C(1, 0), C(1, 2));
                Invoke(typeof(SettlementVerification), "Source", null, level, C(0, 1), SupplyExhaustion.Stop, new[] { new SupplyItem(SupplyKind.Scrap, durability: 4) });
                LevelRuntimeState state = Build(level, seed); Empty(state, new[] { C(0, 1), C(1, 0), C(1, 2) }); SettlementResult result = SettlementResolution.Resolve(state);
                Check(result.IsApplied && result.Records.Count(r => r.Kind == MovementKind.Diagonal) == 1 && result.State.Cells.Single(c => c.ObstacleIndex == 0).Coordinate.Row == 1, "신규 고철 대각선 경쟁 " + seed);
                choices.Add(result.Records.Single(r => r.Kind == MovementKind.Diagonal).Target); Occupancy(result.State, "대각선" + seed);
            }
            Check(choices.Count == 2, "고철 대각선 양쪽 선택");
            LevelDefinition wall = Sparse(C(0, 0), C(1, 0)); Place(wall, C(0, 0), 2); LevelFlowEditing.SetWalls(wall, new[] { new BoardEdge(C(0, 0), C(1, 0)) }, false);
            LevelRuntimeState stopped = Build(wall); Empty(stopped, new[] { C(1, 0) });
            Check(SettlementResolution.Resolve(stopped).Records.Count == 0, "고철 직선 벽 차단");
        }
        private static void InteractionChecks()
        {
            for (int durability = 1; durability <= 5; durability++)
            {
                LevelDefinition level = Make(); Place(level, C(3, 3), durability);
                foreach (BoardCoordinate c in new[] { C(3, 2), C(3, 4), C(2, 3) })
                    Invoke(typeof(PowerEffectVerification), "Place", null, level, c, InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type1);
                BoardActionExecutor executor = new BoardActionExecutor(Build(level));
                Check(executor.Swap(C(3, 3), C(2, 3)).IsApplied && executor.State.Obstacles[0].Durability == durability - 1 &&
                    (durability == 1 || executor.State.CellAt(C(2, 3)).ObstacleIndex == 0), "일반 유효 교환 후 인접 매칭 피해 " + durability);
                Occupancy(executor.State, "일반 교환" + durability);
            }
            LevelDefinition invalid = Make(); Place(invalid, C(4, 4), 2); Place(invalid, C(4, 5), 2);
            BoardActionExecutor rejected = new BoardActionExecutor(Build(invalid)); string original = Snapshot(rejected.State);
            Check(!rejected.Swap(C(4, 4), C(4, 5)).IsApplied && !rejected.Swap(C(4, 4), C(3, 4)).IsApplied && Snapshot(rejected.State) == original, "고철끼리/미매칭 일반 교환 무변경");
            Empty(rejected.State, new[] { C(3, 4) }); original = Snapshot(rejected.State);
            Check(!rejected.Swap(C(4, 4), C(3, 4)).IsApplied && Snapshot(rejected.State) == original, "고철 빈칸 교환 무변경");
            Check(LevelObstacleEditing.Apply(invalid, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 1 }, new[] { C(4, 4) }).Changed == 0, "고철 거미줄 배치 금지");
            for (int pair = 0; pair < 10; pair++)
            {
                LevelDefinition level = (LevelDefinition)Invoke(typeof(CombinationVerification), "Make", null, pair, RocketDirection.Horizontal, null);
                Place(level, C(4, 6), 3); Mission(level, 1); BoardActionExecutor executor = new BoardActionExecutor(Build(level));
                Check(executor.Swap(C(4, 4), C(4, 5)).IsApplied && executor.State.Obstacles[0].Durability >= 2 && executor.State.Missions[0].Progress == 0, "파워 조합 고철 턴1피해/미션 미완료 " + pair);
                if (pair == 9) Check(executor.State.Obstacles[0].Durability == 2, "자석자석 고철 직접1피해");
                if (pair >= 6 && pair <= 8) Check(executor.TurnEffects.Combination.Transformations.All(t => !t.Coordinate.Equals(C(4, 6))), "자석 색 변환에서 고철 제외 " + pair);
                Occupancy(executor.State, "조합" + pair);
            }
            LevelDefinition drones = Make(); Place(drones, C(4, 4), 3); Place(drones, C(4, 5), 1); Mission(drones, 2);
            LevelRuntimeState board = Build(drones); TurnEffectContext context = Context();
            DroneTargetManager manager = (DroneTargetManager)Invoke(typeof(TargetPowerVerification), "Manager", null, board, context);
            Check(manager.Query().Count == 2 && manager.Query().All(t => t.IsMission), "드론 내구도 무관 고철 미션 우선");
            int request = (int)Invoke(typeof(DroneTargetManager), "Request", manager, C(0, 0));
            BoardCoordinate selected = context.Targeting.Last().Target.Value; int index = board.CellAt(selected).ObstacleIndex.Value;
            Check(manager.Query().Count == 1 && manager.ReservationCount == 1, "고철 공유 예약 중복 제외");
            Hit(board, selected, context); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            DroneTarget landed = (DroneTarget)Invoke(typeof(DroneTargetManager), "Land", manager, request, C(0, 0));
            Check(landed != null && landed.IsMission && !landed.Coordinate.Equals(selected) && context.HasDamaged(index) && manager.ReservationCount == 0, "고철 소실/피해 후 미션 재탐색·피해 기록 유지");
            Hit(board, landed.Coordinate, context); Invoke(typeof(DroneTargetManager), "Invalidate", manager);
            Check(manager.Query().All(t => t.Content == RuntimeContent.Normal && !t.IsMission), "고철 유효 후보 소진 후 일반 대체");
            TurnEffectContext next = Context(); DroneTargetManager range = (DroneTargetManager)Invoke(typeof(TargetPowerVerification), "Manager", null, board, next);
            int area = (int)Invoke(typeof(DroneTargetManager), "RequestArea", range, C(0, 0), PowerArea.Blast3);
            Check(range.ExpectedDamage > 0 && range.ReservationCount == 1 && board.Missions[0].Progress == 1, "고철 범위 예약 예상 피해와 실제 미션 분리");
            Invoke(typeof(DroneTargetManager), "Land", range, area, C(0, 0)); Check(range.ReservationCount == 0, "범위 예약 착탄 해제");
        }
        private static void SupplyEdgeChecks()
        {
            LevelDefinition level = Make(); Maintain(level, new[] { C(0, 0), C(0, 2) }, 2, 3, 1); LevelRuntimeState state = Build(level); TurnEffectContext context = Context();
            for (int round = 0; round < 4; round++)
            {
                Empty(state, new[] { C(0, 0), C(0, 2) }); string before = Snapshot(state);
                SettlementResult result = SettlementResolution.Resolve(state, context); Check(result.IsApplied && Snapshot(state) == before, "반복 공급 사본 보존 " + round);
                state = result.State; context = result.TurnEffects; Occupancy(state, "반복 공급" + round);
                foreach (RuntimeCell cell in state.Cells.Where(c => c.Content == RuntimeContent.Obstacle).ToArray()) Hit(state, cell.Coordinate, context);
                Occupancy(state, "반복 제거" + round);
            }
            Check(state.Obstacles.Count == 3 && state.Supply.ScrapGenerated == 3 && state.Supply.ScrapRemaining == 0 && state.LiveScrapCount == 0, "추가 한도 소진/본체 키 비재사용");
            LevelDefinition fixedLevel = Make(); Fixed(fixedLevel, C(0, 0), new SupplyItem(SupplyKind.Scrap, durability: 2)); Fixed(fixedLevel, C(0, 2), new SupplyItem(SupplyKind.Scrap, durability: 5));
            LevelRuntimeState fixedState = Build(fixedLevel); Empty(fixedState, new[] { C(0, 2) }); SettlementResult supplied = SettlementResolution.Resolve(fixedState);
            Check(supplied.State.Supply.Sources[0].ItemIndex == 0 && supplied.State.Supply.Sources[1].ItemIndex == 1 && supplied.State.CellAt(C(0, 2)).ObstacleIndex == 0 && supplied.State.Obstacles[0].Durability == 5, "막힌 고정 생성구 대기/위치별 목록 유지");
            SerializedObject edit = new SerializedObject(fixedLevel); edit.FindProperty("supply.sources").GetArrayElementAtIndex(1).FindPropertyRelative("mode").intValue = (int)SupplyMode.MaintainScrap;
            edit.FindProperty("supply.scrapTarget").intValue = 2; edit.ApplyModifiedPropertiesWithoutUndo();
            Check(!LevelStateBuilder.Build(fixedLevel, 1).IsBuilt, "서로 다른 생성구 고정/유지 혼용 편집 검증 거절");
        }
    }
}
