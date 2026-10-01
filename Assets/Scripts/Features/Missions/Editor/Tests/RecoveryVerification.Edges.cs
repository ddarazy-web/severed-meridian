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
    public static partial class RecoveryVerification
    {
        private static LevelDefinition Sparse(params BoardCoordinate[] cells) => (LevelDefinition)Invoke(typeof(SettlementVerification), "Make", cells);
        public static void Edges()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { MovementChecks(); SwapChecks(); SupplyEdges(); File.WriteAllLines(Evidence + "/edge-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/edge-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void MovementChecks()
        {
            foreach (GravityDirection direction in Enum.GetValues(typeof(GravityDirection)))
            {
                bool vertical = direction == GravityDirection.Up || direction == GravityDirection.Down;
                BoardCoordinate[] line = Enumerable.Range(0, 5).Select(i => vertical ? C(i, 0) : C(0, i)).ToArray();
                bool reverse = direction == GravityDirection.Up || direction == GravityDirection.Left;
                BoardCoordinate from = reverse ? line[4] : line[0], end = reverse ? line[0] : line[4];
                LevelDefinition level = Sparse(line); Place(level, from); LevelFlowEditing.SetGravity(level, line, direction); LevelFlowEditing.SetArrival(level, end, false);
                LevelRuntimeState state = Build(level); Empty(state, line.Where(c => !c.Equals(from))); Set(state.CellAt(from), "DustDurability", 3);
                string before = Snapshot(state); MovementQuery.Find(state); MovementQuery.Find(state, true);
                SettlementResult result = SettlementResolution.Resolve(state);
                Check(result.IsApplied && result.State.Recoveries.Single().Coordinate.Equals(end) && result.Records.Count == 4 &&
                    result.State.CellAt(from).DustDurability == 3 && Snapshot(state) == before, "4방향 도착 회수·바닥/조회 보존 " + direction);
                UnityEngine.Object.DestroyImmediate(level);
            }
            BoardCoordinate[] path = { C(3, 1), C(2, 1), C(2, 2), C(2, 3) };
            LevelDefinition routed = Sparse(path); Place(routed, path[0]); LevelFlowEditing.SetPath(routed, path);
            LevelRuntimeState route = Build(routed); Empty(route, path.Skip(1)); SettlementResult followed = SettlementResolution.Resolve(route);
            Check(followed.IsApplied && followed.State.CellAt(path[3]).Content == RuntimeContent.Recovery && followed.Records.All(r => r.Kind == MovementKind.Path), "꺾임 경로·끝칸 정지");
            UnityEngine.Object.DestroyImmediate(routed);
            foreach (bool blocked in new[] { false, true })
            {
                LevelDefinition level = Sparse(C(0, 0), C(1, 0), C(1, 1), C(4, 4)); Place(level, C(0, 0)); LevelFlowEditing.SetPortal(level, C(0, 0), C(4, 4));
                LevelRuntimeState state = Build(level); Empty(state, new[] { C(1, 0), C(1, 1) }); if (!blocked) Empty(state, new[] { C(4, 4) });
                SettlementResult result = SettlementResolution.Resolve(state);
                Check(result.IsApplied && result.State.CellAt(blocked ? C(0, 0) : C(4, 4)).Content == RuntimeContent.Recovery && result.Records.Count == (blocked ? 0 : 1), "통로·점유 출구 대기 " + blocked);
                UnityEngine.Object.DestroyImmediate(level);
            }
            foreach (bool primary in new[] { false, true })
            {
                LevelDefinition level = Sparse(C(0, 0), C(1, 0), C(1, 1)); Place(level, C(0, 0)); Place(level, C(1, 0));
                LevelFlowEditing.SetGravity(level, new[] { C(1, 0) }, GravityDirection.Right); LevelFlowEditing.SetPortal(level, C(0, 0), C(1, 1));
                LevelFlowEditing.SetMerge(level, C(1, 1), primary ? new[] { C(0, 0), C(1, 0) } : new[] { C(1, 0), C(0, 0) });
                LevelRuntimeState state = Build(level); Empty(state, new[] { C(1, 1) }); SettlementResult result = SettlementResolution.Resolve(state);
                Check(result.IsApplied && result.Records[0].Source.Equals(primary ? C(0, 0) : C(1, 0)) && RecoveryRules.OnBoard(result.State) == 2, "합류 순서 및 대기 부품 보존 " + primary);
                UnityEngine.Object.DestroyImmediate(level);
            }
            HashSet<BoardCoordinate> choices = new HashSet<BoardCoordinate>();
            for (int seed = 1; seed <= 12; seed++)
            {
                LevelDefinition level = Sparse(C(0, 1), C(1, 0), C(1, 2));
                Invoke(typeof(SettlementVerification), "Source", level, C(0, 1), SupplyExhaustion.Stop, new[] { new SupplyItem(SupplyKind.Recovery) });
                LevelFlowEditing.SetArrival(level, C(1, 0), false); LevelFlowEditing.SetArrival(level, C(1, 2), false);
                LevelRuntimeState state = Build(level, seed); Empty(state, new[] { C(0, 1), C(1, 0), C(1, 2) }); SettlementResult result = SettlementResolution.Resolve(state);
                Check(result.IsApplied && result.Records.Count(r => r.Kind == MovementKind.Diagonal) == 1 && result.State.Recoveries.Count == 1, "신규 회수 부품 대각선 경쟁·여러 도착 중 한 번 회수 " + seed);
                choices.Add(result.State.Recoveries[0].Coordinate); UnityEngine.Object.DestroyImmediate(level);
            }
            Check(choices.Count == 2, "대각선 양쪽 도착 선택");
            LevelDefinition wall = Sparse(C(0, 0), C(1, 0)); Place(wall, C(0, 0)); LevelFlowEditing.SetWalls(wall, new[] { new BoardEdge(C(0, 0), C(1, 0)) }, false);
            LevelRuntimeState stopped = Build(wall); Empty(stopped, new[] { C(1, 0) });
            Check(SettlementResolution.Resolve(stopped).Records.Count == 0, "직선 벽 차단"); UnityEngine.Object.DestroyImmediate(wall);
            LevelDefinition multiple = Sparse(C(0, 0), C(1, 0), C(0, 3), C(1, 3));
            foreach (int column in new[] { 0, 3 }) { Place(multiple, C(0, column)); LevelFlowEditing.SetArrival(multiple, C(1, column), false); }
            LevelRuntimeState two = Build(multiple); Empty(two, new[] { C(1, 0), C(1, 3) }); SettlementResult together = SettlementResolution.Resolve(two);
            Check(together.IsApplied && together.State.Recoveries.Count == 2 && together.State.Recoveries.All(r => r.Batch == 1), "동일 묶음 복수 도착·개별 회수");
            UnityEngine.Object.DestroyImmediate(multiple);
            foreach (RuntimeContent content in new[] { RuntimeContent.Normal, RuntimeContent.Rocket, RuntimeContent.Bomb, RuntimeContent.Drone, RuntimeContent.Magnet, RuntimeContent.Obstacle })
            {
                LevelDefinition level = Sparse(C(0, 0), C(1, 0)); LevelFlowEditing.SetArrival(level, C(1, 0), false);
                if (content == RuntimeContent.Obstacle) Invoke(typeof(ScrapVerification), "Place", level, C(0, 0), 3);
                else if (content != RuntimeContent.Normal) Invoke(typeof(PowerEffectVerification), "Place", level, C(0, 0), (InitialBlockKind)(int)content, RocketDirection.Horizontal, RabbitColor.Type1);
                LevelRuntimeState state = Build(level); Empty(state, new[] { C(1, 0) }); SettlementResult result = SettlementResolution.Resolve(state);
                Check(result.IsApplied && result.State.CellAt(C(1, 0)).Content == content && result.State.Recoveries.Count == 0, "도착 바닥의 일반·파워·고철 유지 " + content);
                UnityEngine.Object.DestroyImmediate(level);
            }
        }
        private static void SwapChecks()
        {
            foreach (InitialBlockKind power in new[] { InitialBlockKind.Rocket, InitialBlockKind.Bomb, InitialBlockKind.Drone, InitialBlockKind.Magnet })
                foreach (bool reverse in new[] { false, true })
                {
                    LevelDefinition level = Make(1); Place(level, C(4, 4));
                    Invoke(typeof(PowerEffectVerification), "Place", level, C(4, 5), power, RocketDirection.Horizontal, RabbitColor.Type1);
                    LevelFlowEditing.SetArrival(level, C(4, 5), false);
                    BoardActionExecutor executor = new BoardActionExecutor(Build(level)); string before = Snapshot(executor.State);
                    BoardActionResult action = executor.Swap(reverse ? C(4, 5) : C(4, 4), reverse ? C(4, 4) : C(4, 5));
                    if (power == InitialBlockKind.Magnet) Check(!action.IsApplied && Snapshot(executor.State) == before, "자석·부품 양방향 무비용 취소 " + reverse);
                    else Check(action.IsApplied && executor.State.Recoveries.Count == 1 && executor.State.Missions[0].Progress == 1 && executor.State.MovesRemaining == 19 &&
                        action.Effects.Any(e => e.Response == DamageResponse.Activate && e.Target.Equals(C(4, 4))), "파워 교환·도착 즉시 회수 " + power + reverse);
                    UnityEngine.Object.DestroyImmediate(level);
                }
            LevelDefinition normal = Make(1); Place(normal, C(3, 3)); LevelFlowEditing.SetArrival(normal, C(2, 3), false);
            foreach (BoardCoordinate cell in new[] { C(3, 2), C(3, 4), C(2, 3) })
                Invoke(typeof(PowerEffectVerification), "Place", normal, cell, InitialBlockKind.FixedNormal, RocketDirection.Horizontal, RabbitColor.Type1);
            BoardActionExecutor matched = new BoardActionExecutor(Build(normal));
            Check(matched.Swap(C(3, 3), C(2, 3)).IsApplied && matched.State.Recoveries.Count == 1 && matched.LastApplied.Changes.Count == 3, "일반 매칭 교환과 도착 회수");
            UnityEngine.Object.DestroyImmediate(normal);
            LevelDefinition invalid = Make(1); Place(invalid, C(4, 4)); BoardActionExecutor rejected = new BoardActionExecutor(Build(invalid)); string original = Snapshot(rejected.State);
            Check(!rejected.Swap(C(4, 4), C(4, 5)).IsApplied && Snapshot(rejected.State) == original, "매칭 없는 부품 교환·난수·미션 보존");
            UnityEngine.Object.DestroyImmediate(invalid);
            LevelDefinition last = PlayFixture(); JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", last);
            BoardActionExecutor final = new BoardActionExecutor(Build(last)); final.Activate(C(8, 0)); Finish(final);
            Check(final.State.MovesRemaining == 0 && final.State.Recoveries.Count == 2 && final.State.Missions[0].Progress == 2 && !final.HasPendingCascade, "마지막 수 전체 회수 연쇄 완료·승리 조기 확정 없음");
            UnityEngine.Object.DestroyImmediate(last);
        }
        private static void SupplyEdges()
        {
            LevelDefinition random = Make(1);
            Invoke(typeof(SettlementVerification), "Source", random, C(0, 0), SupplyExhaustion.Random, new[] { new SupplyItem(SupplyKind.Recovery) });
            LevelRuntimeState state = Build(random); Empty(state, new[] { C(0, 0) }); state = SettlementResolution.Resolve(state).State;
            Check(state.CellAt(C(0, 0)).Content == RuntimeContent.Recovery && state.Supply.Sources[0].ItemIndex == 1, "고정 부품 공급 커서");
            Empty(state, new[] { C(0, 0) }); SettlementResult fallback = SettlementResolution.Resolve(state);
            Check(fallback.IsApplied && fallback.State.CellAt(C(0, 0)).Content == RuntimeContent.Normal && fallback.RandomAfter == fallback.RandomBefore + 1, "고정 소진 후 일반 공급");
            UnityEngine.Object.DestroyImmediate(random);
            LevelDefinition failure = Sparse(C(0, 0), C(1, 1), C(2, 1));
            LevelFlowEditing.SetPortal(failure, C(2, 1), C(0, 0));
            Invoke(typeof(SettlementVerification), "Source", failure, C(0, 0), SupplyExhaustion.Stop, new[] { new SupplyItem(SupplyKind.Recovery) });
            LevelRuntimeState loop = Build(failure); Empty(loop, loop.Cells.Where(c => c.IsActive).Select(c => c.Coordinate).ToArray()); string before = Snapshot(loop);
            SettlementResult repeated = SettlementResolution.Resolve(loop);
            Check(repeated.Reason == SettlementReason.Repeating && repeated.State == null && Snapshot(loop) == before && loop.Recoveries.Count == 0, "순환 실패시 공급·회수·난수 전체 원자성");
            UnityEngine.Object.DestroyImmediate(failure);
            LevelDefinition invalid = Make(1); Place(invalid, C(8, 0)); Invoke(typeof(PowerEffectVerification), "Crate", invalid, C(8, 8), 1);
            LevelRuntimeState unsupported = Build(invalid);
            typeof(RuntimeObstacle).GetField("<Definition>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(unsupported.Obstacles[0], JsonUtility.FromJson<ObstaclePlacementDefinition>("{\"kind\":99,\"durability\":1}"));
            BoardActionExecutor initial = new BoardActionExecutor(unsupported);
            Check(initial.State.Recoveries.Count == 0 && initial.State.Missions[0].Progress == 0 && initial.State.CellAt(C(8, 0)).Content == RuntimeContent.Recovery &&
                initial.Outcome.Kind == BoardOutcomeKind.Aborted && initial.Settle().Reason == SettlementReason.WrongPhase, "초기 정규화 미지원시 회수 사본 미커밋·실행 중단");
            UnityEngine.Object.DestroyImmediate(invalid);
            MixedSupplyChecks();
            IndirectChecks();
        }
        private static void MixedSupplyChecks()
        {
            LevelDefinition level = Make(4);
            BoardCoordinate[] sources = { C(0, 0), C(0, 2) };
            LevelSupplyEditing.PlaceSources(level, sources);
            LevelSupplyEditing.SetSourceProperty(level, new[] { 0 }, "mode", (int)SupplyMode.MaintainRecovery);
            LevelSupplyEditing.SetSourceProperty(level, new[] { 1 }, "mode", (int)SupplyMode.MaintainScrap);
            using (SerializedObject data = new SerializedObject(level))
            {
                data.FindProperty("supply.recoveryTarget").intValue = 3;
                data.FindProperty("supply.scrapTarget").intValue = 2;
                data.FindProperty("supply.scrapLimit").intValue = 5;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            LevelRuntimeState state = Build(level); Empty(state, sources);
            SettlementResult result = SettlementResolution.Resolve(state);
            Check(result.IsApplied && RecoveryRules.OnBoard(result.State) == 1 && result.State.LiveScrapCount == 1 && result.State.Supply.ScrapGenerated == 1 && result.State.Missions[0].Progress == 0,
                "회수·고철 유지 공급 독립 및 막힌 보드 부족량 대기");
            Check(RecoveryRules.Needed(result.State) == 2 && RecoveryRules.Needed(state) == 3 && state.Supply.ScrapGenerated == 0, "유지 공급 사본/부족분 독립");
            LevelRuntimeState reset = Build(level);
            Check(reset.Supply.ScrapGenerated == 0 && reset.Recoveries.Count == 0 && reset.Missions[0].Progress == 0, "혼합 유지 재시작 초기화");
            Set(result.State.Missions[0], "Progress", 3);
            Check(RecoveryRules.Needed(result.State) == 0, "유지3·남은1·보드1 추가 공급 없음");
            LevelRuntimeState corrupt = Build(level);
            typeof(RuntimeSource).GetField("<Mode>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(corrupt.Supply.Sources[0], (SupplyMode)99);
            string before = Snapshot(corrupt);
            Check(SettlementResolution.Resolve(corrupt).Reason == SettlementReason.Unsupported && Snapshot(corrupt) == before, "실제 미지원 공급 방식 거절·원자성");
            UnityEngine.Object.DestroyImmediate(level);
        }
        private static void IndirectChecks()
        {
            LevelDefinition level = (LevelDefinition)Invoke(typeof(GeneratorVerification), "Make", ObstacleKind.Crate, 3);
            Place(level, C(3, 4)); LevelFlowEditing.SetArrival(level, C(8, 4), false);
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Recovery + ",\"count\":1}]}", level);
            LevelRuntimeState state = Build(level);
            TurnEffectContext context = (TurnEffectContext)Invoke(typeof(TargetPowerVerification), "Context");
            MissionContribution charge = MissionProgressRules.Query(state, C(4, 4), context).Single();
            Check(charge.Charge == 1 && charge.ExpectedComplete == 0 && charge.Damage == 0, "낙하 방해 발전기 충전 간접 기여·회수 완료 예측0");
            DroneTargetManager manager = (DroneTargetManager)Activator.CreateInstance(typeof(DroneTargetManager), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { state, context }, null);
            Check(manager.Query().Any(t => t.Coordinate.Equals(C(4, 4)) && t.IsMission && t.Contributions.All(c => c.ExpectedComplete == 0)), "발전기 회수 간접 후보 투영");
            UnityEngine.Object.DestroyImmediate(level);
            LevelDefinition wall = Make(1); Place(wall, C(4, 4));
            LevelFlowEditing.SetWalls(wall, new[] { new BoardEdge(C(4, 4), C(5, 4)) }, false);
            Check(MissionProgressRules.Query(Build(wall), C(5, 4), context).Count == 0, "벽 뒤 점유자 간접 기여 제외");
            UnityEngine.Object.DestroyImmediate(wall);
        }
    }
}
