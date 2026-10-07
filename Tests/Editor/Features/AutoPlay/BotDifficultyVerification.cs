using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoPlay;
using Levels;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>등급의 수기 기대값과 실패 경계를 검사한다. 합성 집계는 실제 플레이 결과와 구분한다.</summary>
    public static partial class BotDifficultyVerification
    {
        private const string Evidence = "Logs/BotDifficultyVerification";
        private static readonly List<string> Results = new List<string>();

        private static void Check(bool value, string message)
        {
            Results.Add((value ? "PASS " : "FAIL ") + message);
            if (!value) throw new InvalidOperationException(message);
        }

        /// <param name="basicWins">기본 전략 성공 수.</param><param name="planningWins">계획 전략 성공 수.</param>
        /// <param name="count">전략별 표본 수.</param><param name="remaining">성공 판의 남은 이동 수.</param>
        /// <returns>실제 게임을 실행하지 않고 만든 경계 검사 입력.</returns>
        private static (BotBatchRecord record, BotStrategyStatistics basic, BotStrategyStatistics planning, BotPairedStatistics pairs)
            Synthetic(int basicWins, int planningWins, int count = 100, int remaining = 5)
        {
            BotBatchRecord record = new BotBatchRecord { seeds = Enumerable.Range(1, count).ToArray(), status = BotBatchStatus.Completed,
                finished = count * 2, basicFinished = count, planningFinished = count, won = basicWins + planningWins,
                exhausted = count * 2 - basicWins - planningWins };
            List<BotGameSummary> games = new List<BotGameSummary>();
            for (int i = 0; i < count; i++)
                foreach (BotStrategyKind strategy in new[] { BotStrategyKind.Basic, BotStrategyKind.Planning })
                {
                    BotBatchGame game = JsonUtility.FromJson<BotBatchGame>("{}");
                    game.seed = i + 1; game.strategy = strategy;
                    game.outcome = i < (strategy == BotStrategyKind.Basic ? basicWins : planningWins) ? BotSessionStatus.Won : BotSessionStatus.MovesExhausted;
                    game.usedMoves = 10 - remaining; game.remainingMoves = remaining;
                    game.actions = Array.Empty<BotBatchAction>(); game.missions = Array.Empty<BotBatchMission>();
                    games.Add(new BotGameSummary(game));
                }
            return (record,
                BotBatchStatistics.Calculate(record, games, Array.Empty<LevelMissionDefinition>(), BotStrategyKind.Basic),
                BotBatchStatistics.Calculate(record, games, Array.Empty<LevelMissionDefinition>(), BotStrategyKind.Planning),
                BotBatchStatistics.Pair(record.seeds, games.Where(g => g.Strategy == BotStrategyKind.Basic), games.Where(g => g.Strategy == BotStrategyKind.Planning)));
        }

        public static void Core()
        {
            Directory.CreateDirectory(Evidence); Results.Clear(); Exception failure = null;
            try
            {
                Check(Enum.GetValues(typeof(BotDifficultyGrade)).Length == 4, "공통 난이도는 네 단계만 제공");
                Check(BotMoveRecommendations.Grade(BotDifficultyGrade.Easy) == 0 && BotMoveRecommendations.Grade(BotDifficultyGrade.Normal) == 1 && BotMoveRecommendations.Grade(BotDifficultyGrade.Hard) == 2 && BotMoveRecommendations.Grade(BotDifficultyGrade.VeryHard) == 3, "이동 횟수 추천과 공통 네 등급 일치");
                foreach (var sample in new[] {
                    (wins: 1, grade: BotDifficultyGrade.VeryHard), (wins: 1999, grade: BotDifficultyGrade.VeryHard),
                    (wins: 2000, grade: BotDifficultyGrade.Hard), (wins: 2001, grade: BotDifficultyGrade.Hard),
                    (wins: 3999, grade: BotDifficultyGrade.Hard), (wins: 4000, grade: BotDifficultyGrade.Normal),
                    (wins: 4001, grade: BotDifficultyGrade.Normal), (wins: 5999, grade: BotDifficultyGrade.Normal),
                    (wins: 6000, grade: BotDifficultyGrade.Easy), (wins: 6001, grade: BotDifficultyGrade.Easy),
                    (wins: 7999, grade: BotDifficultyGrade.Easy), (wins: 8000, grade: BotDifficultyGrade.Easy),
                    (wins: 8001, grade: BotDifficultyGrade.Easy), (wins: 10000, grade: BotDifficultyGrade.Easy) })
                {
                    var input = Synthetic(sample.wins, sample.wins, 10000);
                    BotDifficultyResult result = BotDifficultyRules.Evaluate(input.record, input.basic, input.planning, input.pairs, 10);
                    Check(result.Grade == sample.grade && result.Representative == BotStrategyKind.Basic &&
                        result.SuccessPercent == sample.wins / 100d, "반올림 전 등급 경계 " + sample.wins + "/10000");
                }
                foreach (int count in new[] { 0, 1, 99 })
                {
                    var input = Synthetic(count, count, count);
                    Check(BotDifficultyRules.Evaluate(input.record, input.basic, input.planning, input.pairs, 10).Grade == null, "정상 종료 표본 부족 " + count);
                }
                var failed = Synthetic(0, 0);
                BotDifficultyResult noSuccess = BotDifficultyRules.Evaluate(failed.record, failed.basic, failed.planning, failed.pairs, 10);
                Check(noSuccess.Grade == null && noSuccess.HoldReasons.Any(r => r.Contains("성공 사례")), "전부 실패는 매우 어려움/해결 불가능으로 단정하지 않음");
                var reverse = Synthetic(80, 60);
                Check(BotDifficultyRules.Evaluate(reverse.record, reverse.basic, reverse.planning, reverse.pairs, 10).Representative == BotStrategyKind.Basic, "기본 전략이 더 우수한 역전");
                var planned = Synthetic(60, 80);
                BotDifficultyResult advantage = BotDifficultyRules.Evaluate(planned.record, planned.basic, planned.planning, planned.pairs, 10);
                Check(advantage.Representative == BotStrategyKind.Planning && advantage.Tags.Contains("계획 요구"), "같은 시드 20%p 우세와 20개 불일치 쌍");
                var below = Synthetic(61, 80);
                Check(!BotDifficultyRules.Evaluate(below.record, below.basic, below.planning, below.pairs, 10).Tags.Contains("계획 요구"), "계획 태그 기준 바로 아래");
                foreach (int remaining in new[] { 0, 1, 2 })
                {
                    var input = Synthetic(20, 20, 100, remaining);
                    Check(BotDifficultyRules.Evaluate(input.record, input.basic, input.planning, input.pairs, 10).Tags.Contains("이동 여유 부족") == (remaining <= 1),
                        "이동 여유 중앙값 경계 " + remaining);
                }
                var fewWins = Synthetic(19, 19, 100, 0);
                Check(!BotDifficultyRules.Evaluate(fewWins.record, fewWins.basic, fewWins.planning, fewWins.pairs, 10).Tags.Contains("이동 여유 부족"), "성공 19판은 이동 여유 태그 표본 부족");
                var missingMove = Synthetic(50, 50, 100, -1);
                BotDifficultyResult missing = BotDifficultyRules.Evaluate(missingMove.record, missingMove.basic, missingMove.planning, missingMove.pairs, 10);
                Check(missing.Grade.HasValue && missing.Tags.Count == 0 && missing.Notes.Any(n => n.Contains("기록이 일부")), "이동 수 누락은 태그 보류·성공률 근거 보존");
                planned.record.status = BotBatchStatus.Stopped;
                BotDifficultyResult partial = BotDifficultyRules.Evaluate(planned.record, planned.basic, planned.planning, planned.pairs, 10);
                Check(partial.Grade == null && partial.Tags.Count == 0, "중단 묶음은 충분한 일부 표본도 보류");
                BotDifficultyResult issues = BotDifficultyRules.Evaluate(reverse.record, reverse.basic, reverse.planning, reverse.pairs, 10, "손상된 파일", "다른 규칙");
                Check(issues.Grade == null && issues.HoldReasons.Count == 2 && issues.Tags.Count == 0, "입력/버전 문제를 모두 표시하고 등급·태그 보류");
                string before = JsonUtility.ToJson(reverse.record);
                BotDifficultyResult again = BotDifficultyRules.Evaluate(reverse.record, reverse.basic, reverse.planning, reverse.pairs, 10);
                Check(again.Grade == BotDifficultyGrade.Easy && JsonUtility.ToJson(reverse.record) == before, "같은 기록·기준의 재계산과 입력 불변");
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); UnityEngine.Debug.LogException(error); }
            File.WriteAllLines(Evidence + "/core-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}
