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
    public static partial class BoardActionVerification
    {
        private const string Evidence = "Logs/BoardActionVerification";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<LevelDefinition> Definitions = new List<LevelDefinition>();
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static string Snapshot(object value) => (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { value });
        private static void Set(object target, string property, object value) => target.GetType().GetProperty(property).GetSetMethod(true).Invoke(target, new[] { value });
        private static int Next(SimulationRandom random, int bound) => (int)typeof(SimulationRandom).GetMethod("Next", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(random, new object[] { bound });
        private static LevelDefinition Make(Dictionary<BoardCoordinate, int> colors, int moves = 20)
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>(); Definitions.Add(level);
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":12}]}", level);
            using SerializedObject edit = new SerializedObject(level);
            edit.FindProperty("moveCount").intValue = moves;
            SerializedProperty cells = edit.FindProperty("board.cells"), blocks = edit.FindProperty("initialBlocks"); blocks.arraySize = colors.Count;
            for (int i = 0; i < cells.arraySize; i++) cells.GetArrayElementAtIndex(i).FindPropertyRelative("isActive").boolValue = colors.ContainsKey(C(i / 10, i % 10));
            int index = 0;
            foreach (KeyValuePair<BoardCoordinate, int> point in colors)
            {
                SerializedProperty block = blocks.GetArrayElementAtIndex(index++);
                LevelFlowEditing.SetCoordinate(block.FindPropertyRelative("coordinate"), point.Key);
                block.FindPropertyRelative("kind").intValue = 1; block.FindPropertyRelative("fixedColor").intValue = point.Value;
            }
            edit.ApplyModifiedPropertiesWithoutUndo(); return level;
        }
        private static LevelRuntimeState Build(LevelDefinition level, int seed = 12345)
        {
            LevelStateBuildResult result = LevelStateBuilder.Build(level, seed);
            if (!result.IsBuilt) throw new InvalidOperationException(string.Join(" | ", result.Issues));
            return result.State;
        }
        private static LevelDefinition Pattern(BoardCoordinate[] cells, BoardCoordinate hole, BoardCoordinate donor)
        {
            Dictionary<BoardCoordinate, int> colors = cells.ToDictionary(cell => cell, cell => cell.Equals(hole) ? 1 : 0);
            colors[donor] = 0; return Make(colors);
        }
        private static LevelDefinition RocketBoard() => Pattern(new[] { C(3, 2), C(3, 3), C(3, 4), C(3, 5) }, C(3, 3), C(2, 3));
        private static ReadOnlyCollection<MatchDecision> Resolve(LevelRuntimeState state, BoardCoordinate first, BoardCoordinate second)
            => (ReadOnlyCollection<MatchDecision>)typeof(MatchResolution).GetMethod("Select", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { MatchQuery.Find(state), first, second, state.Random });
        private static ReadOnlyCollection<MatchedBlockChange> Apply(LevelRuntimeState state, IEnumerable<MatchDecision> decisions)
            => (ReadOnlyCollection<MatchedBlockChange>)typeof(MatchResolution).GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { state, decisions, 1 });

        public static void Data()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { DataChecks(); File.WriteAllLines(Evidence + "/data-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/data-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static void DataChecks()
        {
            foreach (int length in new[] { 3, 4, 5 }) foreach (int axis in new[] { 0, 1 })
            {
                int gap = length / 2;
                BoardCoordinate[] cells = Enumerable.Range(0, length).Select(i => C(3 + i * axis, 2 + i * (1 - axis))).ToArray();
                BoardCoordinate hole = cells[gap], donor = C(hole.Row - (1 - axis), hole.Column - axis);
                VerifySwap(Pattern(cells, hole, donor), donor, hole, cells, length == 3 ? RuntimeContent.Empty : length == 4 ? RuntimeContent.Rocket : RuntimeContent.Magnet,
                    length == 4 ? (axis == 0 ? RocketDirection.Vertical : RocketDirection.Horizontal) : (RocketDirection?)null, "직선 " + length + "/" + axis);
            }
            foreach (bool tee in new[] { false, true }) for (int rotation = 0; rotation < 4; rotation++)
            {
                (int, int)[] shape = tee ? new[] { (0, 0), (0, 1), (0, 2), (1, 1), (2, 1) } : new[] { (0, 0), (0, 1), (0, 2), (1, 2), (2, 2) };
                (int, int) hole = tee ? (0, 1) : (0, 2), donor = tee ? (-1, 1) : (-1, 2);
                for (int i = 0; i < rotation; i++)
                { shape = shape.Select(p => (p.Item2, 2 - p.Item1)).ToArray(); hole = (hole.Item2, 2 - hole.Item1); donor = (donor.Item2, 2 - donor.Item1); }
                BoardCoordinate[] cells = shape.Select(p => C(p.Item1 + 3, p.Item2 + 3)).ToArray();
                BoardCoordinate target = C(hole.Item1 + 3, hole.Item2 + 3), from = C(donor.Item1 + 3, donor.Item2 + 3);
                VerifySwap(Pattern(cells, target, from), from, target, cells, RuntimeContent.Bomb, null, "폭탄 " + tee + "/" + rotation);
            }
            BoardCoordinate[] square = { C(3, 3), C(3, 4), C(4, 3), C(4, 4) };
            VerifySwap(Pattern(square, C(4, 4), C(4, 5)), C(4, 5), C(4, 4), square, RuntimeContent.Drone, null, "2x2 단독");
            Overlaps(); PriorityChecks(); Rejections(); Independence();
        }

        private static void PriorityChecks()
        {
            foreach (int scenario in new[] { 0, 1, 2 })
            {
                int length = scenario == 2 ? 5 : 4;
                Dictionary<BoardCoordinate, int> cells = Enumerable.Range(1, length).ToDictionary(i => C(3, i), i => 0);
                if (scenario == 0) { cells[C(4, 1)] = 0; cells[C(4, 2)] = 0; }
                else { cells[C(4, length)] = 0; cells[C(5, length)] = 0; }
                LevelRuntimeState state = Build(Make(cells));
                MatchDecision decision = Resolve(state, C(3, length), C(3, length)).Single();
                MatchKind expected = scenario == 0 ? MatchKind.Rocket : scenario == 1 ? MatchKind.Bomb : MatchKind.Magnet;
                MatchKind excluded = scenario == 0 ? MatchKind.Drone : scenario == 1 ? MatchKind.Rocket : MatchKind.Bomb;
                Apply(state, new[] { decision });
                BoardCoordinate residual = scenario == 0 ? C(4, 1) : scenario == 1 ? C(3, 1) : C(4, length);
                Check(decision.Selected.Kind == expected && decision.Excluded.Any(p => p.Kind == excluded) && state.CellAt(residual).Content == RuntimeContent.Normal, "우선순위/낮은 패턴 잔여 보존 " + expected + ">" + excluded);
            }
        }

        private static void VerifySwap(LevelDefinition level, BoardCoordinate first, BoardCoordinate second, BoardCoordinate[] matched, RuntimeContent power, RocketDirection? direction, string name)
        {
            string json = JsonUtility.ToJson(level); bool dirty = EditorUtility.IsDirty(level);
            LevelRuntimeState source = Build(level); string before = Snapshot(source);
            Check(new StartConditionReport(source).IsSatisfied, name + " 유효 시작 보드");
            BoardActionExecutor executor = new BoardActionExecutor(source);
            BoardActionResult result = executor.Swap(first, second);
            Check(result.IsApplied && result.First.Equals(first) && result.Second.Equals(second), name + " 실행/방향 보존");
            Check(result.Changes.Select(c => c.Coordinate).OrderBy(c => c.Row).ThenBy(c => c.Column).SequenceEqual(matched.OrderBy(c => c.Row).ThenBy(c => c.Column)), name + " 정확한 처리 좌표");
            Check(result.Changes.All(c => c.OriginalColor == RabbitColor.Type1) && result.Changes.Count(c => c.IsTransformation) == (power == RuntimeContent.Empty ? 0 : 1), name + " 제거/변환 원래 색과 수량");
            foreach (BoardCoordinate cell in matched)
            {
                RuntimeCell actual = executor.State.CellAt(cell);
                Check(actual.Content == (cell.Equals(second) ? power : RuntimeContent.Empty) && actual.Color == null && actual.ObstacleIndex == null && actual.RocketDirection == (cell.Equals(second) ? direction : null), name + " 결과 필드 " + cell);
            }
            Check(executor.State.CellAt(first).Color == RabbitColor.Type2 && executor.State.MovesRemaining == source.MovesRemaining - 1 && result.RandomBefore == result.RandomAfter, name + " 교환 잔여 칸/한 수 차감/불필요 난수 없음");
            Check(executor.Phase == BoardActionPhase.WaitingForFall && executor.Turn == 1 && result.Changes.Where(c => c.IsTransformation).All(c => c.CreatedOnTurn == 1), name + " 낙하 대기/신규 파워 턴 기록");
            Check(Snapshot(source) == before && JsonUtility.ToJson(level) == json && EditorUtility.IsDirty(level) == dirty, name + " 시작 스냅샷/원본/dirty 보존");
            string after = Snapshot(executor.State);
            Check(executor.Swap(first, second).Reason == BoardActionReason.WaitingForFall && Snapshot(executor.State) == after && executor.LastApplied == result, name + " 중복 입력/난수/결과 보존");
            Check(Snapshot(new BoardActionExecutor(Build(level)).Swap(first, second)) == Snapshot(result), name + " 같은 시드 실행 결과 재현");
        }

        private static void Overlaps()
        {
            // 6개 이상의 직선/복합 동률은 안정된 시작 보드의 한 교환으로 만들 수 없는 경우가 있으므로 처리 단계 입력으로 직접 검증한다.
            foreach (int axis in new[] { 0, 1 })
            {
                BoardCoordinate[] cells = Enumerable.Range(0, 6).Select(i => C(2 + axis * i, 2 + (1 - axis) * i)).ToArray();
                LevelRuntimeState state = Build(Make(cells.ToDictionary(c => c, c => 0)));
                MatchDecision selected = Resolve(state, cells[0], cells[3]).Single();
                Check(selected.Selected.Kind == MatchKind.Magnet && selected.Selected.Cells.Count == 6 && selected.Spawn.Value.Equals(cells[3]), "직선6+ 단일 자석/전체 구간 " + axis);
                ReadOnlyCollection<MatchedBlockChange> changes = Apply(state, new[] { selected });
                Check(changes.Count == 6 && changes.Count(c => c.IsTransformation) == 1 && cells.Count(c => state.CellAt(c).Content == RuntimeContent.Empty) == 5 && state.CellAt(cells[3]).Content == RuntimeContent.Magnet, "직선6+ 실제 다섯 칸 제거/한 칸 변환 " + axis);
            }
            Dictionary<BoardCoordinate, int> overlap = Enumerable.Range(1, 5).ToDictionary(i => C(3, i), i => 0);
            overlap[C(4, 1)] = overlap[C(4, 2)] = 0;
            LevelRuntimeState overlapState = Build(Make(overlap));
            MatchDecision magnet = Resolve(overlapState, C(3, 2), C(3, 3)).Single();
            Check(magnet.Selected.Kind == MatchKind.Magnet && magnet.Selected.Cells.All(c => c.Row == 3) && magnet.Excluded.Any(m => m.Kind == MatchKind.Drone), "자석+드론 낮은 패턴 전용 칸 제외");
            Apply(overlapState, new[] { magnet });
            Check(overlapState.CellAt(C(4, 1)).Content == RuntimeContent.Normal && overlapState.CellAt(C(4, 2)).Content == RuntimeContent.Normal && overlapState.CellAt(C(3, 3)).Content == RuntimeContent.Magnet, "자석+드론 적용 후 직선 밖 두 칸 실제 보존");
            Dictionary<BoardCoordinate, int> cross = Enumerable.Range(1, 5).ToDictionary(i => C(3, i), i => 0);
            foreach (int i in Enumerable.Range(1, 5)) cross[C(i, 3)] = 0;
            LevelDefinition tied = Make(cross);
            LevelRuntimeState a = Build(tied), b = Build(tied);
            MatchDecision decisionA = Resolve(a, C(3, 3), C(3, 3)).Single(), decisionB = Resolve(b, C(3, 3), C(3, 3)).Single();
            Check(decisionA.Selected.Key == decisionB.Selected.Key && a.Random.DrawCount == 1 && b.Random.DrawCount == 1 && decisionA.Reason.Contains("무작위"), "동률 선택만 난수1회/재현");
            cross[C(3, 6)] = 0;
            LevelRuntimeState longer = Build(Make(cross));
            Check(Resolve(longer, C(3, 3), C(3, 3)).Single().Selected.Cells.Count == 6 && longer.Random.DrawCount == 0, "같은 우선순위에서 긴 패턴/난수 미소비");
            LevelRuntimeState reverse = Build(RocketBoard());
            BoardActionResult reversed = new BoardActionExecutor(reverse).Swap(C(3, 3), C(2, 3));
            Check(reversed.IsApplied && reversed.Decisions.Single().Spawn.Value.Equals(C(3, 3)), "반대 입력 방향에서도 패턴을 완성한 블록의 도착 칸");
            Dictionary<BoardCoordinate, int> chain = new Dictionary<BoardCoordinate, int>();
            foreach (int i in Enumerable.Range(1, 5)) { chain[C(1, i)] = 0; chain[C(5, i)] = 0; chain[C(i, 3)] = 0; }
            MatchDecision chained = Resolve(Build(Make(chain)), C(1, 3), C(5, 3)).Single();
            Check(chained.Selected.Kind == MatchKind.Magnet && chained.Excluded.Count >= 2, "A-B-C 간접 겹침도 연결 그룹 하나");

            Dictionary<BoardCoordinate, int> both = new Dictionary<BoardCoordinate, int>();
            foreach (int r in new[] { 1, 2, 4 }) { both[C(r, 3)] = 0; both[C(r, 4)] = 1; }
            both[C(3, 3)] = 1; both[C(3, 4)] = 0;
            LevelRuntimeState source = Build(Make(both));
            BoardActionExecutor executor = new BoardActionExecutor(source);
            BoardActionResult result = executor.Swap(C(3, 3), C(3, 4));
            Check(result.IsApplied && result.Decisions.Count == 2 && result.Changes.Count == 8 && result.MovesAfter == 19, "양쪽 독립 매칭/한 수 차감");
            Check(result.Changes.Count(c => c.IsTransformation) == 2 && executor.State.CellAt(C(3, 3)).Content == RuntimeContent.Rocket && executor.State.CellAt(C(3, 4)).Content == RuntimeContent.Rocket, "양쪽 도착 칸 각각 파워 생성");
        }

        private static void Rejections()
        {
            LevelRuntimeState state = Build(RocketBoard());
            BoardActionExecutor executor = new BoardActionExecutor(state);
            foreach ((BoardCoordinate first, BoardCoordinate second) pair in new[] { (C(3, 2), C(3, 2)), (C(-1, 2), C(3, 2)), (C(3, 2), C(3, 5)), (C(3, 2), C(2, 3)), (C(3, 4), C(3, 5)), (C(3, 2), C(4, 2)) })
            {
                string before = Snapshot(executor.State);
                Check(!executor.Swap(pair.first, pair.second).IsApplied && Snapshot(executor.State) == before, "무효 교환 완전 무변경 " + pair);
            }
            foreach (string layer in new[] { "Cover", "DustDurability", "Recovery", "Obstacle", "Power", "Moves", "Empty", "Mold" })
            {
                LevelRuntimeState source = Build(RocketBoard()); RuntimeCell donor = source.CellAt(C(2, 3));
                if (layer == "Cover" || layer == "Mold") Set(donor, "Cover", layer == "Cover" ? CoverKind.Web : CoverKind.Mold);
                if (layer == "DustDurability") Set(donor, "DustDurability", 1);
                if (layer == "Recovery" || layer == "Obstacle" || layer == "Power" || layer == "Empty") { Set(donor, "Content", layer == "Recovery" ? RuntimeContent.Recovery : layer == "Obstacle" ? RuntimeContent.Obstacle : layer == "Power" ? RuntimeContent.Drone : RuntimeContent.Empty); Set(donor, "Color", null); }
                if (layer == "Moves") Set(source, "MovesRemaining", 0);
                BoardActionExecutor test = new BoardActionExecutor(source); string before = Snapshot(test.State);
                BoardActionResult result = test.Swap(C(2, 3), C(3, 3));
                // 14단계에서 드론 단독 교환을 지원한다. 나머지 거절/원본 보존 검사는 유지한다.
                if (layer == "Power") { Check(result.IsApplied && test.Turn == 1, "드론 단독 교환 지원"); continue; }
                if (layer == "DustDurability") { Check(result.IsApplied && test.State.CellAt(C(2, 3)).DustDurability == 1, "먼지 위 교환 지원/바닥 좌표 보존"); continue; }
                Check(!result.IsApplied && Snapshot(test.State) == before && test.Turn == 0, layer + " 거절 시 상태/난수 불변");
                if (layer == "Cover" || layer == "Mold") Check(result.Reason == BoardActionReason.InvalidSwap, "지원 거미줄의 교환 불가 분류");
                if (layer == "Recovery") Check(result.Reason == BoardActionReason.InvalidSwap, "매칭 없는 회수 교환 거절");
                if (layer == "Obstacle") Check(result.Reason == BoardActionReason.UnsupportedBoard, "잘못된 본체 실행 미지원 분류");
            }
            LevelDefinition wall = RocketBoard(); LevelFlowEditing.SetWalls(wall, new[] { new BoardEdge(C(2, 3), C(3, 3)) }, false);
            BoardActionExecutor blocked = new BoardActionExecutor(Build(wall));
            Check(blocked.Swap(C(2, 3), C(3, 3)).Reason == BoardActionReason.InvalidSwap, "벽 교환 거절");
            LevelRuntimeState lastMove = Build(RocketBoard()); Set(lastMove, "MovesRemaining", 1);
            BoardActionExecutor last = new BoardActionExecutor(lastMove);
            Check(last.Swap(C(2, 3), C(3, 3)).IsApplied && last.State.MovesRemaining == 0 && last.Phase == BoardActionPhase.WaitingForFall, "마지막 이동은 낙하 대기이며 패배 판정 없음");
        }

        private static void Independence()
        {
            LevelRuntimeState source = Build(RocketBoard());
            foreach (int bound in new[] { 2, 3, 5, 100, 7 }) Next(source.Random, bound);
            BoardActionExecutor executor = new BoardActionExecutor(source);
            Check(Snapshot(source) == Snapshot(executor.State) && !ReferenceEquals(source.Cells[0], executor.State.Cells[0]) && !ReferenceEquals(source.Missions[0], executor.State.Missions[0]), "현재 상태 전체 복사/변경 객체 독립");
            foreach (int bound in new[] { 3, 7, 11, 2 }) Check(Next(source.Random, bound) == Next(executor.State.Random, bound), "상이한 범위의 난수 복제 진행 " + bound);
            string before = Snapshot(source);
            Set(executor.State.Missions[0], "Progress", 2);
            Check(Snapshot(source) == before, "복사본 미션 변경 원본 독립");
            LevelDefinition powerLevel = Make(new Dictionary<BoardCoordinate, int> { [C(3, 2)] = 0, [C(3, 3)] = 1, [C(3, 4)] = 0, [C(3, 5)] = 0, [C(2, 3)] = 0, [C(9, 9)] = 2 });
            LevelRuntimeState other = Build(powerLevel);
            Set(other.CellAt(C(9, 9)), "Content", RuntimeContent.Bomb);
            Set(other.CellAt(C(9, 9)), "Color", null);
            BoardActionExecutor withPower = new BoardActionExecutor(other);
            string supply = Snapshot(withPower.State.Supply), flow = Snapshot(withPower.State.Flow);
            BoardActionResult result = withPower.Swap(C(2, 3), C(3, 3));
            Check(result.IsApplied && withPower.State.CellAt(C(9, 9)).Content == RuntimeContent.Bomb && withPower.State.Missions[0].Progress == 4 && Snapshot(withPower.State.Supply) == supply && Snapshot(withPower.State.Flow) == flow, "기존 파워/공급/흐름 보존·변환 포함 색4개 집계");
        }
    }
}
