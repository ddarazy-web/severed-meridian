using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoPlay;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class BotDifficultyVerification
    {
        /// <summary>게임을 실행하지 않는 입력 경계 검사. 실제 시험 횟수에 포함하지 않는다.</summary>
        public static void Edges()
        {
            Results.Clear(); Exception failure = null;
            try
            {
                foreach (string mode in new[] { "all-error", "missing-planning", "missing-pair", "unrun", "paused", "interrupted", "zero-moves" })
                {
                    var source = Synthetic(80, 80);
                    List<BotGameSummary> games = new List<BotGameSummary>();
                    for (int i = 0; i < 100; i++)
                        foreach (BotStrategyKind strategy in new[] { BotStrategyKind.Basic, BotStrategyKind.Planning })
                        {
                            if (mode == "missing-planning" && strategy == BotStrategyKind.Planning || mode == "unrun" && i == 99) continue;
                            BotBatchGame game = JsonUtility.FromJson<BotBatchGame>("{}");
                            game.seed = mode == "missing-pair" && strategy == BotStrategyKind.Planning ? i + 1001 : i + 1;
                            game.strategy = strategy; game.outcome = mode == "all-error" ? BotSessionStatus.Error : BotSessionStatus.Won;
                            game.remainingMoves = 1; game.usedMoves = 9; game.actions = Array.Empty<BotBatchAction>(); game.missions = Array.Empty<BotBatchMission>();
                            games.Add(new BotGameSummary(game));
                        }
                    if (mode == "paused") source.record.status = BotBatchStatus.Paused;
                    if (mode == "interrupted") source.record.status = BotBatchStatus.Interrupted;
                    BotStrategyStatistics basic = BotBatchStatistics.Calculate(source.record, games, Array.Empty<LevelMissionDefinition>(), BotStrategyKind.Basic);
                    BotStrategyStatistics planning = BotBatchStatistics.Calculate(source.record, games, Array.Empty<LevelMissionDefinition>(), BotStrategyKind.Planning);
                    BotPairedStatistics pairs = BotBatchStatistics.Pair(source.record.seeds, games.Where(g => g.Strategy == BotStrategyKind.Basic), games.Where(g => g.Strategy == BotStrategyKind.Planning));
                    BotDifficultyResult result = BotDifficultyRules.Evaluate(source.record, basic, planning, pairs, mode == "zero-moves" ? 0 : 10);
                    Check(result.Grade == null && result.Tags.Count == 0 && result.HoldReasons.Count > 0, "불완전 입력 등급·태그 보류 " + mode);
                    if (mode == "all-error") Check(result.SuccessPercent == null && result.HoldReasons.Any(r => r.Contains("실행 오류")), "전부 오류는 성공률 0%가 아님");
                    if (mode == "missing-pair") Check(result.HoldReasons.Any(r => r.Contains("시드")), "개수만 같아도 서로 다른 시드 쌍은 보류");
                }
                var actual = Synthetic(80, 80);
                BotDifficultyResult first = BotDifficultyRules.Evaluate(actual.record, actual.basic, actual.planning, actual.pairs, 10);
                System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 10000; i++)
                {
                    BotDifficultyResult again = BotDifficultyRules.Evaluate(actual.record, actual.basic, actual.planning, actual.pairs, 10);
                    if (again.Grade != first.Grade || !again.Tags.SequenceEqual(first.Tags)) throw new InvalidOperationException("재계산 불일치");
                }
                Check(true, "동일 입력 10000회 재계산 동일");
                Results.Add("TIME 순수 추천 10000회 " + watch.Elapsed.TotalMilliseconds.ToString("F2") + "ms");
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); UnityEngine.Debug.LogException(error); }
            File.WriteAllLines(Evidence + "/edges-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}
