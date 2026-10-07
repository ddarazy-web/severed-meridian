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
    public static partial class RoundEndVerification
    {
        private const string Evidence = "Logs/RoundEndVerification";
        private static readonly List<string> Results = new List<string>();
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static void Set(object owner, string property, object value) => owner.GetType().GetProperty(property).GetSetMethod(true).Invoke(owner, new[] { value });
        private static LevelRuntimeState Build(LevelDefinition level)
        {
            LevelStateBuildResult result = LevelStateBuilder.Build(level, 12345);
            if (!result.IsBuilt) throw new InvalidOperationException(string.Join(" | ", result.Issues)); return result.State;
        }
        private static void Finish(BoardActionExecutor executor, bool untilOutcome = false)
        {
            int limit = 1000;
            while (executor.HasPendingCascade && (!untilOutcome || executor.Outcome == null) && limit-- > 0) executor.AdvanceCascade();
            if (limit <= 0) throw new InvalidOperationException("검증 처리 한도");
        }
        public static void Data()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            try { DataChecks(); File.WriteAllLines(Evidence + "/data-results.txt", Results); EditorApplication.Exit(0); }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/data-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void DataChecks()
        {
            foreach (int moves in new[] { 1, 20 })
            {
                LevelDefinition level = (LevelDefinition)Invoke(typeof(RecoveryVerification), "PlayFixture");
                JsonUtility.FromJsonOverwrite("{\"moveCount\":" + moves + "}", level);
                LevelRuntimeState raw = Build(level); BoardActionExecutor executor = new BoardActionExecutor(raw);
                Check(executor.Activate(C(8, 0)).IsApplied, "회수 완료 행동 " + moves);
                Finish(executor, true);
                Check(executor.Outcome?.Kind == BoardOutcomeKind.Won && executor.State.Recoveries.Count == 2 && executor.Outcome.MovesRemaining == moves - 1, "모든 회수·정착 후 성공 우선 " + moves);
                Check(executor.IsLastPang && executor.Phase == BoardActionPhase.WaitingForLastPang, "성공 확정과 라스트팡 단계 분리 " + moves);
                BoardOutcome outcome = executor.Outcome; string original = Snapshot(outcome), board = Snapshot(executor.State);
                Check(!executor.Activate(C(8, 0)).IsApplied && Snapshot(executor.State) == board, "성공 후 사용자 입력·난수 차단 " + moves);
                Finish(executor);
                Check(executor.Phase == BoardActionPhase.Stopped && ReferenceEquals(outcome, executor.Outcome) && Snapshot(outcome) == original && executor.LastPangWaves > 0, "라스트팡 실제 발동·종료·성공 기록 보존 " + moves);
                Check(!executor.State.Cells.Any(c => !c.Cover.HasValue && c.Content >= RuntimeContent.Rocket && c.Content <= RuntimeContent.Magnet), "남은 노출 파워 모두 처리 " + moves);
                BoardActionExecutor skip = new BoardActionExecutor(raw); skip.Activate(C(8, 0)); Finish(skip, true);
                string beforeSkip = Snapshot(skip.State);
                Check(skip.SkipLastPang().IsApplied && skip.Outcome.Kind == BoardOutcomeKind.Won && Snapshot(skip.State) == beforeSkip && !skip.HasPendingCascade, "건너뛰기 성공·보드·난수 보존 " + moves);
                BoardOutcome skipped = skip.Outcome;
                Check(!skip.SkipLastPang().IsApplied && ReferenceEquals(skip.Outcome, skipped), "중복 건너뛰기 거절 " + moves);
                UnityEngine.Object.DestroyImmediate(level);
            }
            LevelDefinition lose = (LevelDefinition)Invoke(typeof(PowerEffectVerification), "Make");
            Invoke(typeof(PowerEffectVerification), "Place", lose, C(8, 0), InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1);
            JsonUtility.FromJsonOverwrite("{\"moveCount\":1,\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", lose);
            BoardActionExecutor lost = new BoardActionExecutor(Build(lose)); lost.Activate(C(8, 0)); Finish(lost);
            Check(lost.Outcome?.Kind == BoardOutcomeKind.MovesExhausted && !lost.HasPendingCascade, "마지막 수 목표 미달성 패배");
            LevelRuntimeState emptyMissions = Build(lose);
            typeof(LevelRuntimeState).GetField("<Missions>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(emptyMissions, Array.AsReadOnly(Array.Empty<RuntimeMission>()));
            BoardActionExecutor invalid = new BoardActionExecutor(emptyMissions); Finish(invalid);
            Check(invalid.Outcome?.Kind == BoardOutcomeKind.Aborted, "미션 없는 데이터는 성공 대신 오류");
            UnityEngine.Object.DestroyImmediate(lose);
            Dictionary<BoardCoordinate, int> colors = new Dictionary<BoardCoordinate, int> { [C(1, 0)] = 0, [C(1, 1)] = 0, [C(1, 2)] = 1, [C(0, 1)] = 0 };
            LevelDefinition shuffle = (LevelDefinition)Invoke(typeof(BoardActionVerification), "Make", colors, 20);
            LevelRuntimeState source = Build(shuffle); string before = Snapshot(source);
            ShuffleResult shuffled = ShuffleResolution.Resolve(source);
            Check(shuffled.Reason == ShuffleReason.Applied && MatchQuery.Find(shuffled.State).Count == 0 && ActionQuery.Find(shuffled.State).Count > 0, "재배치 무매칭·행동 확보");
            Check(Snapshot(source) == before && shuffled.State.MovesRemaining == source.MovesRemaining &&
                shuffled.State.Cells.Select(c => c.Color).OrderBy(c => c).SequenceEqual(source.Cells.Select(c => c.Color).OrderBy(c => c)), "재배치 수량·원본·이동 수 보존");
            Check(Snapshot(shuffled.State) == Snapshot(ShuffleResolution.Resolve(source).State), "재배치 난수 재현");
            foreach (RuntimeCell cell in source.Cells.Where(c => c.IsActive)) Set(cell, "Color", RabbitColor.Type1);
            ShuffleResult impossible = ShuffleResolution.Resolve(source);
            Check(impossible.Reason == ShuffleReason.Impossible && impossible.State == null, "동일 블록 불가능 증명·후보 미커밋");
            UnityEngine.Object.DestroyImmediate(shuffle);
        }
    }
}

