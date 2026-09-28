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
    public static partial class PowerEffectVerification
    {
        private const string Evidence = "Logs/PowerEffectVerification";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<LevelDefinition> Definitions = new List<LevelDefinition>();
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static string Snapshot(object value) => (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { value });
        private static LevelDefinition Make()
        {
            Dictionary<BoardCoordinate, int> colors = new Dictionary<BoardCoordinate, int>();
            for (int row = 0; row < 10; row++) for (int column = 0; column < 10; column++) colors[C(row, column)] = (row * 2 + column) % 5;
            LevelDefinition level = (LevelDefinition)typeof(BoardActionVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { colors, 20 });
            Definitions.Add(level); return level;
        }
        private static void Place(LevelDefinition level, BoardCoordinate coordinate, InitialBlockKind kind, RocketDirection direction = RocketDirection.Horizontal, RabbitColor color = RabbitColor.Type1)
        {
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { coordinate });
            PlacementEditResult result = LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Kind = (int)kind, Direction = direction, Color = color }, new[] { coordinate });
            if (result.Changed != 1) throw new InvalidOperationException(result.ToString());
        }
        private static void Crate(LevelDefinition level, BoardCoordinate coordinate, int durability)
        {
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { coordinate });
            PlacementEditResult result = LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = durability }, new[] { coordinate });
            if (result.Changed != 1) throw new InvalidOperationException(result.ToString());
        }
        private static LevelRuntimeState Build(LevelDefinition level)
        {
            LevelStateBuildResult result = LevelStateBuilder.Build(level, 12345);
            if (!result.IsBuilt) throw new InvalidOperationException(string.Join(" | ", result.Issues));
            return result.State;
        }
        private static LevelDefinition ProtectionBoard()
        {
            LevelDefinition level = Make();
            foreach (BoardCoordinate cell in new[] { C(3, 2), C(3, 4), C(3, 5), C(2, 3) }) Place(level, cell, InitialBlockKind.FixedNormal);
            Place(level, C(3, 3), InitialBlockKind.Rocket, RocketDirection.Vertical);
            Crate(level, C(4, 3), 3); Crate(level, C(5, 3), 1);
            Place(level, C(6, 3), InitialBlockKind.Bomb);
            Place(level, C(6, 4), InitialBlockKind.Rocket);
            return level;
        }
        public static void Data()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { DataChecks(); File.WriteAllLines(Evidence + "/data-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/data-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void DataChecks()
        {
            foreach (RocketDirection direction in new[] { RocketDirection.Horizontal, RocketDirection.Vertical })
                foreach (BoardCoordinate origin in new[] { C(0, 0), C(4, 4), C(9, 9) })
                {
                    LevelDefinition level = Make(); Place(level, origin, InitialBlockKind.Rocket, direction);
                    BoardActionExecutor executor = new BoardActionExecutor(Build(level));
                    string before = Snapshot(executor.State); int draws = executor.State.Random.DrawCount;
                    BoardCoordinate[] expected = Enumerable.Range(0, 10).Select(i => direction == RocketDirection.Horizontal ? C(origin.Row, i) : C(i, origin.Column)).ToArray();
                    Check(PowerEffectResolution.Range(executor.State, origin).SequenceEqual(expected) && Snapshot(executor.State) == before, "로켓 범위/읽기 전용 " + direction + origin);
                    BoardActionResult result = executor.Activate(origin);
                    Check(result.IsApplied && result.Effects.Count(e => e.Response == DamageResponse.Remove) == 9 && expected.All(c => executor.State.CellAt(c).Content == RuntimeContent.Empty), "로켓 끝까지 제거 " + direction + origin);
                    Check(executor.State.Cells.Where(c => !expected.Contains(c.Coordinate)).All(c => c.Content == RuntimeContent.Normal), "로켓 범위 밖 보존 " + direction + origin);
                    Check(result.MovesAfter == 19 && result.RandomAfter == draws && executor.Phase == BoardActionPhase.WaitingForFall, "로켓 한 수/난수/낙하 대기 " + direction + origin);
                }
            foreach (BoardCoordinate origin in new[] { C(0, 0), C(0, 5), C(5, 5), C(9, 9) })
            {
                LevelDefinition level = Make(); Place(level, origin, InitialBlockKind.Bomb);
                BoardActionExecutor executor = new BoardActionExecutor(Build(level));
                BoardCoordinate[] expected = (from row in Enumerable.Range(Math.Max(0, origin.Row - 1), origin.Row == 0 || origin.Row == 9 ? 2 : 3)
                    from column in Enumerable.Range(Math.Max(0, origin.Column - 1), origin.Column == 0 || origin.Column == 9 ? 2 : 3) select C(row, column)).ToArray();
                BoardActionResult result = executor.Activate(origin);
                Check(result.IsApplied && expected.All(c => executor.State.CellAt(c).Content == RuntimeContent.Empty) &&
                    executor.State.Cells.Count(c => c.Content == RuntimeContent.Empty) == expected.Length, "폭탄 중앙/경계 정답 " + origin);
            }
            for (int durability = 1; durability <= 6; durability++)
            {
                LevelDefinition level = Make(); Place(level, C(4, 0), InitialBlockKind.Rocket); Crate(level, C(4, 4), durability); Crate(level, C(4, 7), 2);
                LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(4, 3), C(4, 4)) }, false);
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { C(4, 2) });
                using (SerializedObject edit = new SerializedObject(level)) { edit.FindProperty("board.cells").GetArrayElementAtIndex(42).FindPropertyRelative("isActive").boolValue = false; edit.ApplyModifiedPropertiesWithoutUndo(); }
                BoardActionExecutor executor = new BoardActionExecutor(Build(level));
                BoardActionResult result = executor.Activate(C(4, 0));
                Check(result.IsApplied && executor.State.Obstacles[0].Durability == durability - 1 && executor.State.Obstacles[1].Durability == 1, "상자 내구도/각 상자 독립 " + durability);
                Check(executor.State.CellAt(C(4, 9)).Content == RuntimeContent.Empty && !executor.State.CellAt(C(4, 2)).IsActive, "벽/구멍/상자 관통 " + durability);
                Check((executor.State.CellAt(C(4, 4)).Content == RuntimeContent.Empty) == (durability == 1) && executor.State.Obstacles[0].Definition.Durability == durability, "파괴/정의/인덱스 보존 " + durability);
            }
            ProtectionAndRollback(); AdjacentAndQueries(); Rejections(); GeneratedProtection(); SupplementalChecks();
        }
        private static void ProtectionAndRollback()
        {
            foreach (bool reverse in new[] { false, true })
            {
                LevelDefinition level = ProtectionBoard(); LevelRuntimeState source = Build(level); string sourceBefore = Snapshot(source);
                Check(new StartConditionReport(source).IsSatisfied, "새 파워 보호 통합 보드 시작 조건 " + reverse);
                BoardActionExecutor executor = new BoardActionExecutor(source);
                BoardActionResult result = executor.Swap(reverse ? C(2, 3) : C(3, 3), reverse ? C(3, 3) : C(2, 3));
                Check(result.IsApplied && result.Changes.Count == 4 && executor.State.CellAt(C(3, 3)).Content == RuntimeContent.Rocket, "파워 교환+4매칭/생성 보존 " + reverse);
                Check(result.Effects.Where(e => e.Response == DamageResponse.Activate).Select(e => e.Target).SequenceEqual(new[] { C(2, 3), C(6, 3), C(6, 4) }), "교환 도착 발동/즉시 깊이 우선 연쇄 " + reverse);
                Check(result.Effects.Any(e => e.Response == DamageResponse.Protected && e.Target.Equals(C(3, 3))) && executor.TurnEffects.IsProtected(C(3, 3)), "새 파워 실제 피격 보호 " + reverse);
                Check(executor.State.Obstacles[0].Durability == 2 && executor.State.Obstacles[1].Durability == 0 && result.Effects.Count(e => e.Response == DamageResponse.Damage) == 2, "매칭+연쇄 중첩 상자 턴당 1 피해 " + reverse);
                Check(result.Effects.Any(e => e.Response == DamageResponse.AlreadyDamaged) && result.MovesAfter == 19 && Snapshot(source) == sourceBefore, "중복 피해 억제/원본 독립 " + reverse);
                string before = Snapshot(executor.State), applied = Snapshot(executor.LastApplied);
                Check(executor.Activate(C(3, 3)).Reason == BoardActionReason.WaitingForFall && Snapshot(executor.State) == before && Snapshot(executor.LastApplied) == applied, "낙하 대기 재입력/직전 결과 보존 " + reverse);
            }
            foreach (InitialBlockKind kind in new[] { InitialBlockKind.Drone, InitialBlockKind.Magnet })
            {
                LevelDefinition level = ProtectionBoard(); Place(level, C(6, 9), kind);
                BoardActionExecutor executor = new BoardActionExecutor(Build(level));
                BoardActionResult result = executor.Swap(C(3, 3), C(2, 3));
                Check(result.IsApplied && result.Effects.Any(e => e.Target.Equals(C(6, 9)) && e.Response == DamageResponse.Activate), "14단계 드론/자석 피격 단독 연쇄 지원 " + kind);
                Check(Snapshot(new BoardActionExecutor(Build(level)).Swap(C(3, 3), C(2, 3))) == Snapshot(result), "지원된 연쇄 재실행 결정적 " + kind);
            }
        }
        private static void AdjacentAndQueries()
        {
            foreach (bool wall in new[] { false, true })
            {
                LevelDefinition level = Make();
                foreach (BoardCoordinate c in new[] { C(3, 2), C(3, 4), C(2, 3) }) Place(level, c, InitialBlockKind.FixedNormal);
                Place(level, C(3, 3), InitialBlockKind.FixedNormal, color: RabbitColor.Type2); Crate(level, C(4, 3), 2);
                if (wall) LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(3, 3), C(4, 3)) }, false);
                LevelRuntimeState source = Build(level); string before = Snapshot(source);
                DamageReaction reaction = DamageReaction.Evaluate(source, C(4, 3), DamageCause.AdjacentMatch, C(3, 3));
                Check(reaction.Response == (wall ? DamageResponse.Wall : DamageResponse.Damage) && Snapshot(source) == before, "피해 예측 무변경/벽 " + wall);
                BoardActionExecutor executor = new BoardActionExecutor(source); BoardActionResult result = executor.Swap(C(2, 3), C(3, 3));
                Check(result.IsApplied && executor.State.Obstacles[0].Durability == (wall ? 2 : 1), "인접 매칭 실제 피해/벽 " + wall);
                string applied = Snapshot(executor.State);
                DamageReaction.Evaluate(executor.State, C(4, 3), DamageCause.Power, C(0, 3), executor.TurnEffects);
                Check(Snapshot(executor.State) == applied, "턴 문맥 포함 조회 무변경 " + wall);
            }
            LevelDefinition adjacent = Make(); Place(adjacent, C(3, 0), InitialBlockKind.Rocket); Crate(adjacent, C(4, 3), 3);
            BoardActionExecutor power = new BoardActionExecutor(Build(adjacent));
            Check(power.Activate(C(3, 0)).IsApplied && power.State.Obstacles[0].Durability == 3, "파워 제거 일반 블록은 인접 상자 피해 없음");
        }
        private static void Rejections()
        {
            foreach (InitialBlockKind kind in new[] { InitialBlockKind.Rocket, InitialBlockKind.Bomb, InitialBlockKind.Drone, InitialBlockKind.Magnet })
            {
                LevelDefinition level = Make(); Place(level, C(3, 3), InitialBlockKind.Rocket); Place(level, C(3, 4), kind);
                BoardActionExecutor executor = new BoardActionExecutor(Build(level));
                Check(executor.Swap(C(3, 3), C(3, 4)).IsApplied && executor.State.MovesRemaining == 19 && executor.TurnEffects.Combination != null, "15단계 직접 조합 지원/한 수 " + kind);
            }
            LevelDefinition emptyMoves = Make(); Place(emptyMoves, C(3, 3), InitialBlockKind.Bomb); JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", emptyMoves);
            LevelRuntimeState state = Build(emptyMoves);
            typeof(LevelRuntimeState).GetProperty("MovesRemaining").GetSetMethod(true).Invoke(state, new object[] { 0 });
            BoardActionExecutor zero = new BoardActionExecutor(state);
            Check(!zero.Activate(C(3, 3)).IsApplied, "0수 초기 종료 판정 전 발동 거절");
            while (zero.HasPendingCascade) zero.AdvanceCascade();
            string stopped = Snapshot(zero.State);
            Check(zero.Outcome.Kind == BoardOutcomeKind.MovesExhausted && zero.Activate(C(3, 3)).Reason == BoardActionReason.Stopped && Snapshot(zero.State) == stopped, "0수 종료 확정 후 발동·난수 보존");
            BoardActionExecutor invalid = new BoardActionExecutor(Build(Make())); string snapshot = Snapshot(invalid.State);
            Check(!invalid.Activate(C(-1, 0)).IsApplied && !invalid.Activate(C(0, 0)).IsApplied && Snapshot(invalid.State) == snapshot, "보드 밖/일반 블록 제자리 발동 무변경");
        }

        // 자동 매칭 직후를 나타내는 처리 단계 사례다. 실제 교환 통합 검증은 ProtectionAndRollback에서 수행한다.
        private static void GeneratedProtection()
        {
            foreach (MatchKind kind in new[] { MatchKind.Drone, MatchKind.Rocket, MatchKind.Bomb, MatchKind.Magnet })
            {
                BoardCoordinate[] cells = kind == MatchKind.Drone ? new[] { C(3, 3), C(3, 4), C(4, 3), C(4, 4) } :
                    kind == MatchKind.Bomb ? new[] { C(3, 3), C(3, 4), C(3, 5), C(4, 5), C(5, 5) } :
                    Enumerable.Range(3, kind == MatchKind.Rocket ? 4 : 5).Select(i => C(3, i)).ToArray();
                Dictionary<BoardCoordinate, int> colors = cells.ToDictionary(c => c, c => 0); colors[C(3, 0)] = 1;
                LevelDefinition level = (LevelDefinition)typeof(BoardActionVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { colors, 20 });
                Definitions.Add(level); Place(level, C(3, 0), InitialBlockKind.Rocket);
                LevelRuntimeState state = Build(level);
                var decisions = (System.Collections.ObjectModel.ReadOnlyCollection<MatchDecision>)typeof(BoardActionVerification).GetMethod("Resolve", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { state, C(3, 3), C(3, 3) });
                var changes = (System.Collections.ObjectModel.ReadOnlyCollection<MatchedBlockChange>)typeof(BoardActionVerification).GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { state, decisions });
                Check(decisions.Single().Selected.Kind == kind, "보호 처리 사례 매칭 생성 " + kind);
                TurnEffectContext context = (TurnEffectContext)Activator.CreateInstance(typeof(TurnEffectContext), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 1, changes }, null);
                RuntimeContent expected = state.CellAt(C(3, 3)).Content; string before = Snapshot(state);
                DamageReaction predicted = DamageReaction.Evaluate(state, C(3, 3), DamageCause.Power, C(3, 0), context);
                Check(predicted.Response == DamageResponse.Protected && predicted.Amount == 0 && Snapshot(state) == before, "모든 신규 파워 보호 조회/무변경 " + kind);
                var effects = new List<EffectRecord>(); object[] args = { state, changes, C(3, 0), context, effects, null };
                bool applied = (bool)typeof(PowerEffectResolution).GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
                Check(applied && state.CellAt(C(3, 3)).Content == expected && effects.Count(e => e.Response == DamageResponse.Activate) == 1 && effects.Any(e => e.Response == DamageResponse.Protected), "모든 신규 파워 피격 제거/발동 방지 " + kind);
            }
        }

        public static void Supplemental()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { SupplementalChecks(); File.WriteAllLines(Evidence + "/supplemental-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/supplemental-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void SupplementalChecks()
        {
            foreach (bool reverse in new[] { false, true })
            {
                LevelDefinition level = Make(); Place(level, C(3, 3), InitialBlockKind.Bomb);
                LevelRuntimeState source = Build(level); RabbitColor? movedColor = source.CellAt(C(3, 4)).Color;
                BoardActionExecutor executor = new BoardActionExecutor(source);
                BoardActionResult result = executor.Swap(reverse ? C(3, 4) : C(3, 3), reverse ? C(3, 3) : C(3, 4));
                Check(result.IsApplied && result.Effects.Single(e => e.Response == DamageResponse.Activate).Target.Equals(C(3, 4)) && result.Changes.Count == 0, "달폭탄 매칭 없는 교환/도착 중심 " + reverse);
                Check(result.Effects.Count(e => e.Response == DamageResponse.Remove) == 8 && executor.State.CellAt(C(3, 5)).Content == RuntimeContent.Empty &&
                    executor.State.CellAt(C(3, 2)).Content == RuntimeContent.Normal && result.MovesAfter == 19 && source.CellAt(C(3, 4)).Color == movedColor, "달폭탄 교환 범위/원본/한 수 " + reverse);
            }
            LevelDefinition hole = Make(); Place(hole, C(3, 3), InitialBlockKind.Bomb);
            LevelObstacleEditing.Apply(hole, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { C(2, 3) });
            using (SerializedObject edit = new SerializedObject(hole)) { edit.FindProperty("board.cells").GetArrayElementAtIndex(23).FindPropertyRelative("isActive").boolValue = false; edit.ApplyModifiedPropertiesWithoutUndo(); }
            LevelFlowEditing.SetWalls(hole, new[] { new BoardEdge(C(3, 3), C(3, 4)) }, false);
            BoardActionExecutor bomb = new BoardActionExecutor(Build(hole));
            BoardActionResult burst = bomb.Activate(C(3, 3));
            Check(burst.IsApplied && burst.Effects.Count(e => e.Response == DamageResponse.Remove) == 7 && !burst.Effects.Any(e => e.Target.Equals(C(2, 3))) && bomb.State.CellAt(C(3, 4)).Content == RuntimeContent.Empty, "폭탄 비활성 칸 제외/벽 무시");
            Check(!bomb.State.CellAt(C(2, 3)).IsActive && bomb.State.Flow.Walls.Count == 1, "폭탄 지형/벽 보존");
        }
    }
}
