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
    public static partial class CascadeVerification
    {
        private const string Evidence = "Logs/CascadeVerification";
        private static readonly List<string> Results = new List<string>();
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static void Set(object target, string property, object value) => target.GetType().GetProperty(property).GetSetMethod(true).Invoke(target, new[] { value });
        private static object Invoke(Type type, string method, object instance, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", null, value);
        private static LevelDefinition Make(IEnumerable<BoardCoordinate> cells, int moves = 20) =>
            (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", null, cells.ToDictionary(c => c, c => 0), moves);
        private static LevelRuntimeState Build(LevelDefinition level, int seed = 12345)
        {
            LevelStateBuildResult result = LevelStateBuilder.Build(level, seed);
            if (!result.IsBuilt) throw new InvalidOperationException(string.Join(" | ", result.Issues));
            return result.State;
        }
        private static BoardActionExecutor Automatic(LevelDefinition level, int seed = 12345)
        {
            BoardActionExecutor executor = new BoardActionExecutor(Build(level, seed));
            Set(executor, "Phase", BoardActionPhase.WaitingForAutomaticMatch); Set(executor, "Turn", 1);
            TurnEffectContext context = (TurnEffectContext)Activator.CreateInstance(typeof(TurnEffectContext), BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { 1, Array.Empty<MatchedBlockChange>() }, null);
            Set(executor, "TurnEffects", context); return executor;
        }
        private static void Arrive(TurnEffectContext context, BoardCoordinate coordinate, int settlement, int batch)
        {
            Set(context, "SettlementCount", settlement);
            Invoke(typeof(TurnEffectContext), "RecordArrival", context, coordinate, coordinate, batch, true);
        }
        private static string Context(BoardActionExecutor executor) => Snapshot(executor.State) + "|" + executor.Phase + "|" + executor.Turn + "|" + executor.CascadeRounds + "|" + executor.TurnEffects.SettlementCount +
            "|" + string.Join(";", executor.State.Cells.Select(c => executor.TurnEffects.IsProtected(c.Coordinate) + "," + executor.TurnEffects.LastArrival(c.Coordinate) + "," + executor.TurnEffects.HasFired(c.Coordinate))) +
            "|" + string.Join(";", Enumerable.Range(0, executor.State.Obstacles.Count).Select(executor.TurnEffects.HasDamaged));

        public static void Data()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { DataChecks(); File.WriteAllLines(Evidence + "/data-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/data-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static void DataChecks()
        {
            var fixtures = new[]
            {
                (MatchKind.Three, new[] { C(0,0), C(0,1), C(0,2) }),
                (MatchKind.Rocket, new[] { C(0,0), C(0,1), C(0,2), C(0,3) }),
                (MatchKind.Magnet, new[] { C(0,0), C(0,1), C(0,2), C(0,3), C(0,4) }),
                (MatchKind.Bomb, new[] { C(0,0), C(0,1), C(0,2), C(1,2), C(2,2) }),
                (MatchKind.Bomb, new[] { C(0,0), C(1,0), C(2,0), C(2,1), C(2,2) }),
                (MatchKind.Bomb, new[] { C(0,0), C(0,1), C(0,2), C(1,1), C(2,1) }),
                (MatchKind.Drone, new[] { C(0,0), C(0,1), C(1,0), C(1,1) })
            };
            foreach (var fixture in fixtures)
            {
                BoardActionExecutor executor = Automatic(Make(fixture.Item2));
                BoardCoordinate latest = fixture.Item2.Last(); Arrive(executor.TurnEffects, latest, 1, 9);
                CascadeStepResult result = executor.ResolveAutomaticMatch();
                Check(result.IsApplied && result.Decisions.Count == 1 && result.Decisions[0].Selected.Kind == fixture.Item1 && result.Changes.Count == fixture.Item2.Length, "자동 기본 패턴 " + fixture.Item1 + "/" + latest);
                Check(executor.Turn == 1 && executor.State.MovesRemaining == 20 && executor.Phase == BoardActionPhase.WaitingForFall && result.RandomBefore == result.RandomAfter, "자동 매칭 추가 수/턴/단일 후보 난수 무소비 " + fixture.Item1);
                if (fixture.Item1 != MatchKind.Three)
                    Check(result.Decisions[0].Spawn.Value.Equals(latest) && executor.TurnEffects.IsProtected(latest) && executor.TurnEffects.LastArrival(latest) == 0, "최후 도착 파워 위치/보호/일반 도착 정보 제거 " + fixture.Item1);
            }
            BoardCoordinate[] overlap = Enumerable.Range(0, 5).Select(c => C(0, c)).Concat(new[] { C(1, 0), C(1, 1) }).ToArray();
            BoardActionExecutor priority = Automatic(Make(overlap)); Arrive(priority.TurnEffects, C(0, 3), 1, 2); Arrive(priority.TurnEffects, C(1, 1), 1, 8);
            CascadeStepResult selected = priority.ResolveAutomaticMatch();
            Check(selected.Decisions.Single().Selected.Kind == MatchKind.Magnet && selected.Decisions[0].Spawn.Value.Equals(C(0, 3)) && priority.State.CellAt(C(1, 0)).Content == RuntimeContent.Normal && priority.State.CellAt(C(1, 1)).Content == RuntimeContent.Normal, "자석/드론 중첩 선택 밖 보존/제외 패턴 도착 무시");

            HashSet<BoardCoordinate> locations = new HashSet<BoardCoordinate>();
            for (int seed = 0; seed < 16; seed++)
            {
                BoardActionExecutor tied = Automatic(Make(fixtures[1].Item2), seed);
                CascadeStepResult result = tied.ResolveAutomaticMatch(); locations.Add(result.Decisions[0].Spawn.Value);
                Check(result.IsApplied && result.RandomAfter == result.RandomBefore + 1, "이력 없는 동시 후보 재현 난수 " + seed);
            }
            Check(locations.Count > 1, "동시 도착 후보 복수 위치 선택 가능");
            BoardActionExecutor rounds = Automatic(Make(fixtures[1].Item2));
            Arrive(rounds.TurnEffects, C(0, 1), 1, 100); Arrive(rounds.TurnEffects, C(0, 2), 2, 1);
            Check(rounds.ResolveAutomaticMatch().Decisions.Single().Spawn.Value.Equals(C(0, 2)), "정착 회차 우선/회차 내 묶음 비교");

            ContinuationChecks();
            EdgeChecks();
        }

        private static void ContinuationChecks()
        {
            // 12단계의 실제 교환·효과 사례에 고정 일반 블록 공급을 연결한다.
            LevelDefinition level = (LevelDefinition)Invoke(typeof(SettlementVerification), "FallingBoard", null);
            JsonUtility.FromJsonOverwrite("{\"moveCount\":3}", level);
            LevelSupplyEditing.SetItems(level, 0, new[] { new SupplyItem(SupplyKind.FixedNormal, 3) });
            BoardActionExecutor executor = new BoardActionExecutor(Build(level));
            Check(executor.Swap(C(3, 3), C(2, 3)).IsApplied, "실제 교환/효과 연쇄 시작");
            int steps = 0;
            while (executor.HasPendingCascade && steps++ < 40)
                Check(executor.AdvanceCascade().IsApplied, "실제 연쇄 단계 " + steps);
            Check(!executor.HasPendingCascade && executor.CascadeRounds >= 1 && executor.Turn == 1 && executor.State.MovesRemaining == 2, "실제 연쇄 종료/추가 수 차감 없음");
            BoardCoordinate power = executor.State.Cells.First(c => c.Content == RuntimeContent.Rocket).Coordinate;
            Check(executor.TurnEffects.IsProtected(power) && executor.Phase == BoardActionPhase.Ready && executor.State.Cells.Any(c => c.IsActive && c.Content == RuntimeContent.Empty), "빈칸 보드 다음 수 허용/이전 파워 보호 유지");
            string before = Context(executor); Check(!executor.Swap(C(0, 0), C(9, 9)).IsApplied && Context(executor) == before, "잘못된 다음 입력 문맥 보존");
            Check(executor.Activate(power).IsApplied && executor.Turn == 2 && executor.State.MovesRemaining == 1 && !executor.TurnEffects.IsProtected(power), "다음 유효 행동 보호 해제/수와 턴 1회 변경");

            foreach (RuntimeContent content in new[] { RuntimeContent.Empty, RuntimeContent.Drone, RuntimeContent.Rocket })
            {
                BoardActionExecutor terminal = Automatic(Make(new[] { C(0, 0) }));
                Set(terminal.State.CellAt(C(0, 0)), "Content", content); Set(terminal.State.CellAt(C(0, 0)), "Color", null);
                CascadeStepReason expected = content == RuntimeContent.Empty ? CascadeStepReason.Blocked : CascadeStepReason.Stable;
                Check(terminal.ResolveAutomaticMatch().Reason == expected, "안정 종료 사유 " + expected);
            }
            BoardActionExecutor noMoves = Automatic(Make(new[] { C(0, 0), C(0, 1), C(0, 2) })); Set(noMoves.State, "MovesRemaining", 0);
            Check(noMoves.AdvanceCascade().Reason == CascadeStepReason.Matched && noMoves.AdvanceCascade().Reason == CascadeStepReason.Settled && noMoves.AdvanceCascade().Reason == CascadeStepReason.MovesExhausted, "마지막 수 0 자동 매칭/정착 후 미판정 정지");
            string ended = Context(noMoves); Check(!noMoves.Activate(C(0, 0)).IsApplied && !noMoves.AdvanceCascade().IsApplied && Context(noMoves) == ended, "정지 후 행동/연쇄 중복 호출 보존");
        }
    }
}
