using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AutoPlay;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>수기 기대값과 실제 저장 기록으로 검사한다. 사용자 에셋은 생성하거나 수정하지 않는다.</summary>
    public static partial class BotAnalysisVerification
    {
        private const string Evidence = "Logs/BotAnalysisVerification";
        private static readonly List<string> Results = new List<string>();
        private static void Check(bool value, string message)
        {
            Results.Add((value ? "PASS " : "FAIL ") + message);
            if (!value) throw new InvalidOperationException(message);
        }

        public static void Core() => Run(false);
        public static void Replay() => Run(true);

        private static void Run(bool independent)
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            Exception failure = null;
            try
            {
                if (!independent) FormulaChecks();
                string source = independent ? File.ReadAllText(Evidence + "/source-path.txt") :
                    Directory.GetFiles("Library/Match/AutoPlayLoadVerification", "batch.json", SearchOption.AllDirectories)
                        .Where(path => JsonUtility.FromJson<BotBatchRecord>(File.ReadAllText(path)).finished == 200)
                        .OrderBy(path => path, StringComparer.Ordinal).Select(Path.GetDirectoryName).First();
                if (!independent) File.WriteAllText(Evidence + "/source-path.txt", source);
                Stopwatch timer = Stopwatch.StartNew(); long memory = GC.GetTotalMemory(true);
                BotAnalysisReader reader = Load(source); timer.Stop();
                Check(reader.Games.Count == 200 && reader.Record.finished == 200, "실제 27단계 200판 기록 확인 · 새 실행 아님");
                foreach (BotStrategyKind strategy in Enum.GetValues(typeof(BotStrategyKind)))
                {
                    BotStrategyStatistics stats = BotBatchStatistics.Calculate(reader.Record, reader.Games, reader.Missions, strategy);
                    int wins = 0, losses = 0, blocks = 0;
                    for (int i = (int)strategy; i < 200; i += 2)
                    {
                        BotBatchGame raw = JsonUtility.FromJson<BotBatchGame>(File.ReadAllText(Path.Combine(source, i.ToString("D6") + ".json")));
                        if (raw.outcome == BotSessionStatus.Won) wins++;
                        else if (raw.outcome == BotSessionStatus.MovesExhausted) losses++;
                        else if (raw.outcome == BotSessionStatus.Blocked) blocks++;
                    }
                    Check(stats.Normal == 100 && stats.Won == wins && stats.Exhausted == losses && stats.Blocked == blocks && stats.Unrun == 0,
                        "200판 원시 JSON 종료 종류 독립 집계 " + strategy);
                }
                if (!independent) StorageChecks(reader);
                List<string> proof = new List<string>();
                LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
                try
                {
                    JsonUtility.FromJsonOverwrite(reader.Record.definitionJson, level);
                    string original = JsonUtility.ToJson(level); bool dirty = EditorUtility.IsDirty(level);
                    double maxStep = 0; Stopwatch replayTime = Stopwatch.StartNew(); int pauses = 0;
                    for (int ordinal = 0; ordinal < reader.Games.Count; ordinal++)
                    {
                        using BotRecordReplay replay = new BotRecordReplay(level, reader.Record, reader.ReadGame(ordinal));
                        replay.Begin(true); int steps = 0;
                        while (replay.NeedsAdvance && steps++ < 50000)
                        {
                            long before = Stopwatch.GetTimestamp(); replay.Advance();
                            maxStep = Math.Max(maxStep, (Stopwatch.GetTimestamp() - before) * 1000d / Stopwatch.Frequency);
                            if (independent && replay.NeedsAdvance && steps % 7 == 0)
                            {
                                replay.Pause(); int action = replay.ActionIndex; int random = replay.State?.Random.DrawCount ?? -1;
                                replay.Advance(); replay.Advance();
                                if (replay.ActionIndex != action || (replay.State?.Random.DrawCount ?? -1) != random) throw new InvalidOperationException("일시정지 중 재생 상태 변경");
                                replay.Begin(true); pauses++;
                            }
                        }
                        Check(replay.Status == BotReplayStatus.Completed, "저장 행동 재생 " + ordinal + " · " + replay.Message);
                        string snapshot = (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic)
                            .Invoke(null, new object[] { replay.State });
                        using SHA256 hash = SHA256.Create();
                        proof.Add(Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(snapshot))));
                    }
                    replayTime.Stop();
                    Check(JsonUtility.ToJson(level) == original && EditorUtility.IsDirty(level) == dirty, "200판 재생 후 정의 JSON·dirty 보존");
                    File.WriteAllLines(Evidence + (independent ? "/replay-measurement.txt" : "/core-measurement.txt"), new[] {
                        "Source=27단계 기존 4x4/3수 200판 기록 재사용", $"LoadMs={timer.Elapsed.TotalMilliseconds:F3}",
                        $"ReplayMs={replayTime.Elapsed.TotalMilliseconds:F3}", $"MaxAdvanceMs={maxStep:F3}",
                        $"ManagedBefore={memory}", $"ManagedAfter={GC.GetTotalMemory(true)}", $"Pauses={pauses}" });
                    if (independent)
                    {
                        Check(File.ReadAllText(Evidence + "/core-process.txt") != Process.GetCurrentProcess().Id.ToString(), "서로 다른 Unity 프로세스");
                        Check(proof.SequenceEqual(File.ReadAllLines(Evidence + "/core-proof.txt")) && pauses > 0, "독립 프로세스·일시정지 분할 후 200판 전체 상태 동일");
                    }
                    else
                    {
                        File.WriteAllLines(Evidence + "/core-proof.txt", proof);
                        File.WriteAllText(Evidence + "/core-process.txt", Process.GetCurrentProcess().Id.ToString());
                        ReplayEdges(level, reader);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(level); }
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            File.WriteAllLines(Evidence + (independent ? "/replay-results.txt" : "/core-results.txt"), Results);
            if (failure != null) UnityEngine.Debug.LogException(failure);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static BotAnalysisReader Load(string folder)
        {
            BotAnalysisReader reader = new BotAnalysisReader(folder);
            while (!reader.IsDone) reader.Advance();
            if (reader.Error != null) throw new InvalidDataException(reader.Error);
            return reader;
        }

        private static BotBatchGame Game(BotSessionStatus outcome, int seed, int used = 0, int remaining = 0, int mission = 0)
        {
            BotBatchGame game = JsonUtility.FromJson<BotBatchGame>("{}");
            game.outcome = outcome; game.seed = seed; game.strategy = BotStrategyKind.Basic;
            game.usedMoves = used; game.remainingMoves = remaining; game.actions = Array.Empty<BotBatchAction>();
            game.missions = new[] { new BotBatchMission { kind = MissionKind.Mold, color = RabbitColor.Type1, remaining = mission } };
            return game;
        }

        private static void FormulaChecks()
        {
            BotBatchRecord batch = new BotBatchRecord { seeds = Enumerable.Range(0, 10).ToArray() };
            LevelDefinition definition = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":8,\"color\":0,\"count\":2},{\"kind\":8,\"color\":0,\"count\":3}]}", definition);
                List<BotGameSummary> games = new List<BotGameSummary> {
                    new BotGameSummary(Game(BotSessionStatus.Won, 0, 2, 8)), new BotGameSummary(Game(BotSessionStatus.Won, 1, 4, 6)),
                    new BotGameSummary(Game(BotSessionStatus.Won, 2, 9, 1)), new BotGameSummary(Game(BotSessionStatus.MovesExhausted, 3, 10, 0, 8)),
                    new BotGameSummary(Game(BotSessionStatus.MovesExhausted, 4, 10, 0, 4)), new BotGameSummary(Game(BotSessionStatus.Blocked, 5, 1, 9, 0)),
                    new BotGameSummary(Game(BotSessionStatus.Error, 6)), new BotGameSummary(Game(BotSessionStatus.Stopped, 7)) };
                BotStrategyStatistics stats = BotBatchStatistics.Calculate(batch, games, definition.Missions, BotStrategyKind.Basic);
                Check(stats.Planned == 10 && stats.Normal == 6 && stats.SuccessPercent == 50 && stats.Errors == 1 && stats.Stopped == 1 && stats.Unrun == 2,
                    "수기 표본: 오류·중단 제외 3/6=50%, 미실행 2");
                Check(stats.Used.Mean == 5 && stats.Used.Median == 4 && stats.Used.Minimum == 2 && stats.Used.Maximum == 9 && stats.Used.Count == 3,
                    "성공 이동 [2,4,9] 평균5·중앙4·범위2~9");
                Check(stats.Remaining.Mean == 5 && stats.Remaining.Median == 6, "남은 이동 수 수기 기대값");
                Check(stats.Missions[0].Samples == 3 && stats.Missions[0].MeanRemaining == 4 && stats.Missions[0].MaximumRemaining == 8 && stats.Missions[0].Unfinished == 2,
                    "동적 미션 잔여량 [8,4,0] 원값 집계");
                Check(stats.Missions[1].Missing == 3 && stats.Missions[1].MeanRemaining == null && stats.Missions[1].MaximumRemaining == null,
                    "같은 종류 두 번째 미션 분리·누락은 0 아님");
                BotStrategyStatistics none = BotBatchStatistics.Calculate(batch, games, definition.Missions, BotStrategyKind.Planning);
                Check(none.Normal == 0 && none.SuccessPercent == null && none.Used.Mean == null && none.Unrun == 10, "미실행 전략은 0% 아님");
                BotStrategyStatistics errors = BotBatchStatistics.Calculate(batch, games.Where(g => !g.IsNormal).ToArray(), definition.Missions, BotStrategyKind.Basic);
                Check(errors.SuccessPercent == null && errors.Normal == 0 && errors.Used.Count == 0, "오류/중단만 있으면 정상 통계 없음");
                Check(new BotMoveStatistics(new[] { 2, 4, 8, 10 }).Median == 6, "짝수 표본 중앙값");
                BotStrategyStatistics oneMissing = BotBatchStatistics.Calculate(batch,
                    new[] { new BotGameSummary(Game(BotSessionStatus.Won, 0, 9, -1)), new BotGameSummary(Game(BotSessionStatus.Won, 1, -1, 3)) },
                    definition.Missions, BotStrategyKind.Basic);
                Check(oneMissing.Used.Count == 1 && oneMissing.Used.Mean == 9 && oneMissing.Remaining.Count == 1 && oneMissing.Remaining.Mean == 3,
                    "사용/남은 이동 수 한쪽 누락은 각 통계에서 독립적으로 제외");
                BotGameSummary[] right = new[] { new BotGameSummary(Game(BotSessionStatus.Won, 0)), new BotGameSummary(Game(BotSessionStatus.MovesExhausted, 1)),
                    new BotGameSummary(Game(BotSessionStatus.Won, 3)), new BotGameSummary(Game(BotSessionStatus.MovesExhausted, 4)), new BotGameSummary(Game(BotSessionStatus.Error, 5)) };
                BotPairedStatistics paired = BotBatchStatistics.Pair(batch.seeds, games, right);
                Check(paired.Included == 4 && paired.BothWon == 1 && paired.LeftOnlyWon == 1 && paired.RightOnlyWon == 1 && paired.NeitherWon == 1 && paired.Excluded == 6,
                    "정상 시드 쌍 네 종류 각1·나머지6 제외");
            }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
        }
    }
}
