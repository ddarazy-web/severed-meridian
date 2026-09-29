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
    public static partial class SettlementVerification
    {
        private const string Evidence = "Logs/SettlementVerification";
        private static readonly List<string> Results = new List<string>();
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static string Snapshot(object value) => (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { value });
        private static void Set(object target, string property, object value) => target.GetType().GetProperty(property).GetSetMethod(true).Invoke(target, new[] { value });
        private static LevelDefinition Make(IEnumerable<BoardCoordinate> active)
        {
            Dictionary<BoardCoordinate, int> colors = active.Distinct().ToDictionary(c => c, c => (c.Row * 2 + c.Column) % 5);
            return (LevelDefinition)typeof(BoardActionVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { colors, 20 });
        }
        private static LevelRuntimeState Build(LevelDefinition level, int seed = 12345)
        {
            LevelStateBuildResult result = LevelStateBuilder.Build(level, seed);
            if (!result.IsBuilt) throw new InvalidOperationException(string.Join(" | ", result.Issues));
            return result.State;
        }
        private static void Empty(LevelRuntimeState state, IEnumerable<BoardCoordinate> cells)
        {
            foreach (BoardCoordinate coordinate in cells)
            { RuntimeCell cell = state.CellAt(coordinate); Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); Set(cell, "RocketDirection", null); }
        }
        private static void Source(LevelDefinition level, BoardCoordinate coordinate, SupplyExhaustion exhaustion, params SupplyItem[] items)
        {
            string error = LevelSupplyEditing.PlaceSources(level, new[] { coordinate });
            if (error != null) throw new InvalidOperationException(error);
            int index = level.Supply.Sources.ToList().FindIndex(s => s.Coordinate.Equals(coordinate));
            LevelSupplyEditing.SetSourceProperty(level, new[] { index }, "mode", (int)SupplyMode.Fixed);
            LevelSupplyEditing.SetSourceProperty(level, new[] { index }, "exhaustion", (int)exhaustion);
            error = LevelSupplyEditing.SetItems(level, index, items);
            if (error != null) throw new InvalidOperationException(error);
        }
        private static LevelDefinition FallingBoard()
        {
            BoardCoordinate[] cells = new[] { C(2, 3), C(3, 2), C(3, 4), C(3, 5) }.Concat(Enumerable.Range(3, 7).Select(r => C(r, 3))).ToArray();
            LevelDefinition level = Make(cells);
            foreach (BoardCoordinate c in new[] { C(2, 3), C(3, 2), C(3, 4), C(3, 5) })
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Kind = (int)InitialBlockKind.FixedNormal, Color = RabbitColor.Type1 }, new[] { c });
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { C(3, 3) });
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Kind = (int)InitialBlockKind.Rocket, Direction = RocketDirection.Vertical }, new[] { C(3, 3) });
            Source(level, C(2, 3), SupplyExhaustion.Stop, new SupplyItem(SupplyKind.Rocket));
            JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", level); return level;
        }
        public static void Data()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { DataChecks(); File.WriteAllLines(Evidence + "/data-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/data-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void DataChecks()
        {
            foreach (GravityDirection direction in Enum.GetValues(typeof(GravityDirection)))
            {
                bool horizontal = direction == GravityDirection.Left || direction == GravityDirection.Right;
                BoardCoordinate[] line = Enumerable.Range(0, 5).Select(i => horizontal ? C(3, i) : C(i, 3)).ToArray();
                LevelDefinition level = Make(line); LevelFlowEditing.SetGravity(level, line, direction);
                LevelRuntimeState state = Build(level); bool reverse = direction == GravityDirection.Up || direction == GravityDirection.Left;
                BoardCoordinate from = reverse ? line[4] : line[0], end = reverse ? line[0] : line[4]; Empty(state, line.Where(c => !c.Equals(from)));
                RabbitColor? color = state.CellAt(from).Color; string before = Snapshot(state);
                Check(MovementQuery.Find(state).Count(c => c.IsAllowed) == 1 && Snapshot(state) == before, "방향 후보 읽기 전용 " + direction);
                SettlementResult result = SettlementResolution.Resolve(state);
                Check(result.IsApplied && result.State.CellAt(end).Color == color && result.Records.Count == 4 && result.Records.Select(r => r.Batch).SequenceEqual(new[] { 1, 2, 3, 4 }), "4방향 긴 빈 구간 정착 " + direction);
                Check(Snapshot(state) == before && result.RandomBefore == result.RandomAfter && result.State.MovesRemaining == 20, "방향 정착 원본/수/난수 보존 " + direction);
            }
            DiagonalChecks(); FlowChecks(); SupplyChecks(); IntegrationChecks(); TerminationAndSupplyEdges();
        }
        private static void DiagonalChecks()
        {
            foreach (bool twoSources in new[] { false, true })
            {
                BoardCoordinate[] points = twoSources ? new[] { C(0, 0), C(0, 2), C(1, 1) } : new[] { C(0, 1), C(1, 0), C(1, 2) };
                LevelDefinition level = Make(points); HashSet<BoardCoordinate> chosen = new HashSet<BoardCoordinate>();
                for (int seed = 0; seed < 32; seed++)
                {
                    LevelRuntimeState state = Build(level, seed); Empty(state, points.Where(c => c.Row == 1));
                    SettlementResult result = SettlementResolution.Resolve(state);
                    Check(result.IsApplied && result.Records.Count == 1 && result.Records[0].Kind == MovementKind.Diagonal && result.RandomAfter == result.RandomBefore + 1, "대각선 단순 경쟁 한 번 선택 " + twoSources + "/" + seed);
                    chosen.Add(twoSources ? result.Records[0].Source : result.Records[0].Target);
                }
                Check(chosen.Count == 2, "대각선 양쪽 모두 선택 가능 " + twoSources);
            }
            LevelDefinition straight = Make(new[] { C(0, 1), C(1, 1), C(1, 0), C(1, 2) });
            LevelRuntimeState priority = Build(straight); Empty(priority, new[] { C(1, 1), C(1, 0), C(1, 2) });
            Check(!MovementQuery.Find(priority, true).Any(c => c.IsAllowed) && SettlementResolution.Resolve(priority).Records[0].Kind == MovementKind.Gravity, "자신의 직선 목적지 우선");
            LevelDefinition region = Make(new[] { C(0, 0), C(1, 1) }); LevelFlowEditing.SetGravity(region, new[] { C(1, 1) }, GravityDirection.Right);
            LevelRuntimeState differing = Build(region); Empty(differing, new[] { C(1, 1) });
            Check(!MovementQuery.Find(differing, true).Any(c => c.IsAllowed), "서로 다른 중력 대각선 금지");
            LevelFlowEditing.SetPath(region, new[] { C(1, 1) }); LevelFlowEditing.SetGravity(region, new[] { C(1, 1) }, GravityDirection.Down);
            LevelRuntimeState pathEnd = Build(region); Empty(pathEnd, new[] { C(1, 1) });
            Check(!MovementQuery.Find(pathEnd, true).Any(c => c.IsAllowed), "직접 경로 대각선 유입 금지");
            foreach (bool both in new[] { false, true })
            {
                BoardCoordinate[] square = { C(0, 0), C(0, 1), C(1, 0), C(1, 1) }; LevelDefinition level = Make(square);
                typeof(PowerEffectVerification).GetMethod("Crate", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { level, C(0, 1), 2 });
                typeof(PowerEffectVerification).GetMethod("Crate", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { level, C(1, 0), 2 });
                List<BoardEdge> walls = new List<BoardEdge> { new BoardEdge(C(0, 0), C(0, 1)) }; if (both) walls.Add(new BoardEdge(C(0, 0), C(1, 0)));
                LevelFlowEditing.SetWalls(level, walls, false); LevelRuntimeState state = Build(level); Empty(state, new[] { C(1, 1) });
                SettlementResult result = SettlementResolution.Resolve(state);
                Check(result.IsApplied && result.Records.Count == (both ? 0 : 1) && result.State.Obstacles.All(o => o.Durability == 2), "대각선 벽 두 경로/상자 점유 " + both);
            }
            LevelRuntimeState hole = Build(Make(new[] { C(0, 0), C(2, 0) })); Empty(hole, new[] { C(2, 0) });
            SettlementResult blocked = SettlementResolution.Resolve(hole);
            Check(blocked.IsApplied && blocked.Records.Count == 0 && blocked.EmptyCells.Single().Equals(C(2, 0)), "비활성 칸 건너뛰기 금지/막힌 빈칸 유지");
        }
        private static void FlowChecks()
        {
            BoardCoordinate[] path = { C(3, 1), C(2, 1), C(2, 2), C(2, 3) }; LevelDefinition level = Make(path); LevelFlowEditing.SetPath(level, path);
            LevelRuntimeState state = Build(level); Empty(state, path.Skip(1)); SettlementResult result = SettlementResolution.Resolve(state);
            Check(result.IsApplied && result.Records.Select(r => r.Target).SequenceEqual(path.Skip(1)) && result.Records.All(r => r.Kind == MovementKind.Path), "위쪽/꺾임 직접 경로 및 끝칸");
            foreach (bool occupied in new[] { false, true })
            {
                LevelDefinition portal = Make(new[] { C(0, 0), C(1, 0), C(1, 1), C(4, 4) }); LevelFlowEditing.SetPortal(portal, C(0, 0), C(4, 4));
                LevelRuntimeState board = Build(portal); Empty(board, new[] { C(1, 0), C(1, 1) }); if (!occupied) Empty(board, new[] { C(4, 4) });
                SettlementResult transport = SettlementResolution.Resolve(board);
                Check(transport.IsApplied && transport.Records.Count == (occupied ? 0 : 1) && (occupied || transport.Records[0].Kind == MovementKind.Portal), "통로 이동/출구 대기/일반 대각선 탈출 금지 " + occupied);
            }
            foreach (bool firstEmpty in new[] { false, true })
            {
                LevelDefinition merge = Make(new[] { C(0, 0), C(1, 0), C(1, 1) });
                LevelFlowEditing.SetGravity(merge, new[] { C(1, 0) }, GravityDirection.Right);
                LevelFlowEditing.SetPortal(merge, C(0, 0), C(1, 1));
                LevelFlowEditing.SetMerge(merge, C(1, 1), new[] { C(0, 0), C(1, 0) });
                LevelRuntimeState board = Build(merge); Empty(board, new[] { C(1, 1) }); if (firstEmpty) Empty(board, new[] { C(0, 0) });
                SettlementResult merged = SettlementResolution.Resolve(board);
                Check(merged.IsApplied && merged.Records[0].Source.Equals(firstEmpty ? C(1, 0) : C(0, 0)), "통로/구역 합류 우선 및 차선 " + firstEmpty);
            }
            LevelDefinition turn = Make(new[] { C(0, 0), C(1, 0), C(1, 1) }); LevelFlowEditing.SetGravity(turn, new[] { C(1, 0) }, GravityDirection.Right);
            LevelRuntimeState turned = Build(turn); Empty(turned, new[] { C(1, 0), C(1, 1) });
            Check(SettlementResolution.Resolve(turned).Records.Select(r => r.Target).SequenceEqual(new[] { C(1, 0), C(1, 1) }), "직선 다른 중력 진입 후 새 방향 적용");
        }
        private static void SupplyChecks()
        {
            BoardCoordinate[] randomColumn = Enumerable.Range(0, 10).Select(i => C(i, 0)).ToArray();
            LevelDefinition powers = Make(randomColumn);
            Source(powers, C(0, 0), SupplyExhaustion.Stop, new SupplyItem(SupplyKind.RandomPower, 10));
            HashSet<RuntimeContent> kinds = new HashSet<RuntimeContent>();
            HashSet<RocketDirection> directions = new HashSet<RocketDirection>();
            for (int seed = 0; seed < 8; seed++)
            {
                LevelRuntimeState input = Build(powers, seed); Empty(input, randomColumn);
                string before = Snapshot(input);
                SettlementResult supplied = SettlementResolution.Resolve(input);
                RuntimeCell[] cells = randomColumn.Select(supplied.State.CellAt).ToArray();
                Check(supplied.IsApplied && cells.All(c => c.Content == RuntimeContent.Rocket || c.Content == RuntimeContent.Bomb || c.Content == RuntimeContent.Drone), "랜덤 파워 3종만 공급 " + seed);
                Check(cells.All(c => c.Color == null && (c.Content == RuntimeContent.Rocket ? c.RocketDirection.HasValue : !c.RocketDirection.HasValue)), "랜덤 파워 색 없음/로켓만 방향 " + seed);
                Check(supplied.State.Supply.Sources[0].ItemIndex == 1 && supplied.State.Supply.Sources[0].ItemConsumed == 0 &&
                    supplied.State.Supply.Sources[0].Items[0].Kind == SupplyKind.RandomPower, "랜덤 파워 수량 소진/목록 유지 " + seed);
                Check(Snapshot(input) == before && Snapshot(SettlementResolution.Resolve(input).State) == Snapshot(supplied.State), "랜덤 파워 원본 보존/같은 시드 재현 " + seed);
                SettlementResult blockedPower = SettlementResolution.Resolve(supplied.State);
                Check(blockedPower.RandomBefore == blockedPower.RandomAfter && blockedPower.Records.Count == 0, "랜덤 파워 소진 후 추가 생성 없음 " + seed);
                foreach (RuntimeCell cell in cells)
                { kinds.Add(cell.Content); if (cell.RocketDirection.HasValue) directions.Add(cell.RocketDirection.Value); }
            }
            Check(kinds.Count == 3 && directions.Count == 2, "랜덤 파워 모든 종류/양방향 등장");
            UnityEngine.Object.DestroyImmediate(powers);
            BoardCoordinate[] column = Enumerable.Range(0, 10).Select(i => C(i, 3)).ToArray(); LevelDefinition level = Make(column);
            Source(level, C(0, 3), SupplyExhaustion.Stop, new SupplyItem(SupplyKind.FixedNormal, 2, RabbitColor.Type3),
                new SupplyItem(SupplyKind.Rocket, direction: RocketDirection.Vertical), new SupplyItem(SupplyKind.Bomb), new SupplyItem(SupplyKind.Drone), new SupplyItem(SupplyKind.Magnet));
            LevelRuntimeState state = Build(level); Empty(state, column); string original = Snapshot(state); SettlementResult result = SettlementResolution.Resolve(state);
            SettlementRecord[] generated = result.Records.Where(r => r.Kind == MovementKind.Supply).ToArray();
            Check(result.IsApplied && generated.Select(r => r.Content).SequenceEqual(new[] { RuntimeContent.Normal, RuntimeContent.Normal, RuntimeContent.Rocket, RuntimeContent.Bomb, RuntimeContent.Drone, RuntimeContent.Magnet }), "고정 목록 수량/파워 4종 순서");
            Check(result.State.CellAt(C(7, 3)).RocketDirection == RocketDirection.Vertical && result.State.CellAt(C(9, 3)).Color == RabbitColor.Type3, "공급 블록 색/방향 낙하 유지");
            Check(result.State.Supply.Sources[0].ItemIndex == 5 && result.State.Supply.Sources[0].ItemConsumed == 0 && result.RandomBefore == result.RandomAfter && result.EmptyCells.Count == 4, "고정 공급 커서/소진 중단/난수 무소비");
            Check(Snapshot(state) == original && !generated.Any(r => r.Protected), "공급 원본 보존/파워 보호 없음");
            LevelDefinition fallback = Make(column); Source(fallback, C(0, 3), SupplyExhaustion.Random, new SupplyItem(SupplyKind.FixedNormal));
            LevelRuntimeState empty = Build(fallback); Empty(empty, column); SettlementResult filled = SettlementResolution.Resolve(empty);
            Check(filled.IsApplied && filled.EmptyCells.Count == 0 && filled.RandomAfter - filled.RandomBefore == 9 && filled.State.Cells.Where(c => c.IsActive).All(c => filled.State.Colors.Contains(c.Color.Value)), "고정 소진 후 무작위/선택 색 범위");
            LevelRuntimeState occupied = Build(fallback); string blockedBefore = Snapshot(occupied); SettlementResult blocked = SettlementResolution.Resolve(occupied);
            Check(blocked.IsApplied && blocked.Records.Count == 0 && Snapshot(blocked.State) == blockedBefore, "막힌 생성구 커서/난수 무소비");
            LevelDefinition inflow = Make(new[] { C(0, 0), C(1, 0) }); Source(inflow, C(1, 0), SupplyExhaustion.Stop, new SupplyItem(SupplyKind.Bomb));
            LevelRuntimeState entering = Build(inflow); Empty(entering, new[] { C(1, 0) }); SettlementResult incoming = SettlementResolution.Resolve(entering);
            Check(incoming.IsApplied && incoming.Records.Count == 1 && incoming.State.Supply.Sources[0].ItemIndex == 0 && incoming.State.CellAt(C(1, 0)).Content == RuntimeContent.Normal, "생성구 기존 유입 우선/목록 대기");
        }
        private static void IntegrationChecks()
        {
            LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("ProtectionBoard", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            BoardActionExecutor executor = new BoardActionExecutor(Build(level));
            Check(executor.Settle().Reason == SettlementReason.WrongPhase, "효과 전 정착 차단");
            Check(executor.Swap(C(3, 3), C(2, 3)).IsApplied, "통합 교환/매칭/파워 효과");
            string action = Snapshot(executor.LastApplied), mission = Snapshot(executor.State.Missions), supply = Snapshot(executor.State.Supply); int moves = executor.State.MovesRemaining;
            SettlementResult result = executor.Settle();
            Check(result.IsApplied && executor.Phase == BoardActionPhase.WaitingForAutomaticMatch && executor.State.MovesRemaining == moves && executor.Turn == 1, "효과 후 정착/자동 매칭 대기/수 보존");
            Check(executor.TurnEffects.IsProtected(C(3, 3)) && executor.State.CellAt(C(3, 3)).Content == RuntimeContent.Rocket, "고정 상자 위 신규 파워 보호 유지");
            Check(executor.TurnEffects.HasDamaged(0) && executor.State.Obstacles[0].Durability == 2 && Snapshot(executor.LastApplied) == action && Snapshot(executor.State.Missions) == mission && Snapshot(executor.State.Supply) == supply, "정착 상자 피해/직전 행동/미션/공급 보존");
            string settled = Snapshot(executor.State); Check(!executor.Settle().IsApplied && !executor.Activate(C(0, 0)).IsApplied && Snapshot(executor.State) == settled, "정착 후 반복/행동 차단");
            LevelDefinition unsupported = Make(new[] { C(0, 0), C(1, 0) }); Source(unsupported, C(0, 0), SupplyExhaustion.Stop, new SupplyItem(SupplyKind.FixedNormal), new SupplyItem(SupplyKind.Scrap));
            LevelRuntimeState invalid = Build(unsupported); Empty(invalid, new[] { C(1, 0) }); string before = Snapshot(invalid);
            Check(SettlementResolution.Resolve(invalid).IsApplied && Snapshot(invalid) == before, "고철 고정 목록 지원/입력 보존");
            LevelDefinition falling = FallingBoard();
            BoardActionExecutor moving = new BoardActionExecutor(Build(falling));
            Check(moving.Swap(C(3, 3), C(2, 3)).IsApplied && moving.State.MovesRemaining == 0, "보호 낙하 통합 마지막 수 효과");
            SettlementResult moved = moving.Settle();
            Check(moved.IsApplied && moving.TurnEffects.IsProtected(C(9, 3)) && !moving.TurnEffects.IsProtected(C(3, 3)) && moved.Records.Any(r => r.Protected), "보호 파워 낙하/이전 보호 좌표 해제");
            Check(moving.State.CellAt(C(8, 3)).Content == RuntimeContent.Rocket && !moving.TurnEffects.IsProtected(C(8, 3)) &&
                DamageReaction.Evaluate(moving.State, C(8, 3), DamageCause.Power, C(8, 2), moving.TurnEffects).Response == DamageResponse.Activate, "공급 파워 보호 없음/소모 좌표 발동 기록 분리");
            Check(moving.State.MovesRemaining == 0 && moving.Turn == 1 && moving.Phase == BoardActionPhase.WaitingForAutomaticMatch, "마지막 수에도 정착 완료/승패 미판정");
        }

        private static void TerminationAndSupplyEdges()
        {
            BoardCoordinate[] line = Enumerable.Range(0, 10).Select(r => C(r, 0)).ToArray();
            foreach (int count in new[] { 3, 4, 5 })
            {
                LevelDefinition random = Make(line);
                JsonUtility.FromJsonOverwrite("{\"colors\":[" + string.Join(",", Enumerable.Range(0, count)) + "]}", random);
                LevelObstacleEditing.Apply(random, new PlacementBrush { Layer = PlacementLayer.Block, Kind = (int)InitialBlockKind.FixedNormal, Color = RabbitColor.Type1 }, line);
                LevelSupplyEditing.PlaceSources(random, new[] { C(0, 0) });
                LevelRuntimeState state = Build(random); Empty(state, line); SettlementResult result = SettlementResolution.Resolve(state);
                Check(result.IsApplied && result.RandomAfter - result.RandomBefore == 10 && result.State.Cells.Where(c => c.IsActive).All(c => (int)c.Color.Value < count), "Random 생성구 선택 색 " + count + "종/생성 시만 난수");
            }
            BoardCoordinate[] pair = Enumerable.Range(0, 3).SelectMany(r => new[] { C(r, 0), C(r, 2) }).ToArray();
            LevelDefinition independent = Make(pair);
            Source(independent, C(0, 0), SupplyExhaustion.Stop, new SupplyItem(SupplyKind.FixedNormal, 2, RabbitColor.Type2));
            Source(independent, C(0, 2), SupplyExhaustion.Stop, new SupplyItem(SupplyKind.Bomb));
            LevelRuntimeState empty = Build(independent); Empty(empty, pair); SettlementResult both = SettlementResolution.Resolve(empty);
            Check(both.IsApplied && both.Records.Count(r => r.Kind == MovementKind.Supply) == 3 && both.State.CellAt(C(2, 0)).Color == RabbitColor.Type2 && both.State.CellAt(C(2, 2)).Content == RuntimeContent.Bomb && both.State.Supply.Sources.All(s => s.ItemIndex == 1), "생성구별 고정 수량/종류/커서 독립");
            Check(both.Records.Where(r => r.Kind == MovementKind.Supply).Take(2).Select(r => r.Source).SequenceEqual(new[] { C(0, 0), C(0, 2) }) &&
                both.Records.Where(r => r.Kind == MovementKind.Supply).Take(2).Select(r => r.Batch).Distinct().Count() == 1, "동시 공급 묶음/좌표 순서");

            LevelRuntimeState cycle = Build(Make(new[] { C(0, 0), C(1, 0) })); Set(cycle.CellAt(C(1, 0)), "Gravity", GravityDirection.Up);
            string original = Snapshot(cycle);
            Check(SettlementResolution.Resolve(cycle).Reason == SettlementReason.InvalidFlow && Snapshot(cycle) == original, "현재 중력 순환 사전 거절/원본 보존");

            LevelDefinition repeat = Make(new[] { C(0, 0), C(1, 1), C(2, 1) }); LevelFlowEditing.SetPortal(repeat, C(2, 1), C(0, 0));
            LevelRuntimeState repeating = Build(repeat); Empty(repeating, new[] { C(1, 1), C(2, 1) }); string beforeRepeat = Snapshot(repeating);
            Check(SettlementResolution.Resolve(repeating).Reason == SettlementReason.Repeating && Snapshot(repeating) == beforeRepeat, "일차 흐름 비순환이어도 대각선 포함 반복 감지/보존");

            BoardCoordinate[] loop = { C(0, 1), C(1, 0), C(1, 2), C(2, 0), C(2, 1), C(2, 2) };
            LevelDefinition wandering = Make(loop); LevelFlowEditing.SetGravity(wandering, new[] { C(2, 0), C(2, 1) }, GravityDirection.Right);
            LevelFlowEditing.SetPortal(wandering, C(2, 2), C(0, 1)); LevelFlowEditing.SetMerge(wandering, C(2, 2), new[] { C(1, 2), C(2, 1) });
            LevelRuntimeState randomLoop = Build(wandering); Empty(randomLoop, loop.Where(c => !c.Equals(C(0, 1)))); string beforeLoop = Snapshot(randomLoop);
            Check(SettlementResolution.Resolve(randomLoop).Reason == SettlementReason.LimitReached && Snapshot(randomLoop) == beforeLoop, "난수 진행 반복도 내부 한도 실패/난수 포함 보존");

            BoardCoordinate[] snake = Enumerable.Range(0, 10).SelectMany(r => Enumerable.Range(0, 10).Select(c => C(r, r % 2 == 0 ? c : 9 - c))).ToArray();
            LevelDefinition longPath = Make(snake); LevelFlowEditing.SetPath(longPath, snake);
            Source(longPath, snake[0], SupplyExhaustion.Stop, new SupplyItem(SupplyKind.FixedNormal, 100));
            LevelRuntimeState longEmpty = Build(longPath); Empty(longEmpty, snake); SettlementResult full = SettlementResolution.Resolve(longEmpty);
            Check(full.IsApplied && full.EmptyCells.Count == 0 && full.Records.Count(r => r.Kind == MovementKind.Supply) == 100 && full.Records.Count == 5050 && full.State.Supply.Sources[0].ItemIndex == 1, "10x10 최장 경로 100개 공급/4950칸 이동 정상 종료");
            Check(MatchQuery.Find(full.State).Count > 0 && full.State.Cells.All(c => c.Content == RuntimeContent.Normal), "정착 중/후 매칭 제거하지 않음");
        }
    }
}
