using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoPlay
{
    /// <summary>이동 횟수별 실제 시험 요약. grade는 0 쉬움, 1 보통, 2 어려움, 3 매우 어려움, -1 보류다.</summary>
    [Serializable]
    public sealed class BotMoveTrial
    {
        public int moves, basicWon, planningWon, normalPerStrategy, grade = -1;
        public string batchId, reason;
    }

    /// <summary>한 레벨 사본의 1~100회 시험 기록. 원본에 추천값을 자동 적용하지 않는다.</summary>
    [Serializable]
    public sealed class BotMoveBalanceRecord
    {
        public int formatVersion = 1;
        public string id, sourceName, definitionJson, fingerprint, engineVersion, rulesVersion;
        public string startedUtc, message;
        public int[] seeds;
        public BotBatchStatus status;
        public List<BotMoveTrial> trials = new List<BotMoveTrial>();
    }

    /// <summary>실측한 이동 횟수만 등급별로 묶는다. 시험하지 않은 중간 값은 범위로 연결하지 않는다.</summary>
    public static class BotMoveRecommendations
    {
        public const string Version = "move-balance-v1";
        public const int MaximumMoves = 100;
        public const int Samples = 100;
        // 추론 전략이 바뀐 과거 결과도 현재 실력으로 오해하지 않도록 실행 조건 전체를 함께 식별한다.
        public static string ExecutionVersion => string.Join("|", Simulation.BoardActionExecutor.Version,
            BotPlaySession.Version, BasicBotStrategy.Version, PlanningSearch.Version,
            BotObservationBuilder.Version, BotBatchSession.AssumptionVersion, Simulation.StartingBoardSearch.AlgorithmVersion);
        public static readonly string[] Titles = { "쉬움", "보통", "어려움", "매우 어려움" };

        /// <param name="grade">공통 난이도 네 등급 또는 보류.</param>
        /// <returns>네 등급의 인덱스. 기존 평가가 보류이면 -1.</returns>
        public static int Grade(BotDifficultyGrade? grade) => grade switch {
            BotDifficultyGrade.Easy => 0,
            BotDifficultyGrade.Normal => 1, BotDifficultyGrade.Hard => 2,
            BotDifficultyGrade.VeryHard => 3, _ => -1 };

        /// <param name="trials">정상적으로 평가를 마친 실측 결과.</param><param name="grade">표시할 네 등급 인덱스.</param>
        /// <returns>연속 정수만 묶은 추천 횟수. 일치하는 값이 없으면 빈 문자열.</returns>
        public static string Ranges(IEnumerable<BotMoveTrial> trials, int grade)
        {
            int[] moves = trials.Where(t => t.grade == grade && t.normalPerStrategy == Samples)
                .Select(t => t.moves).Distinct().OrderBy(n => n).ToArray();
            List<string> ranges = new List<string>();
            for (int i = 0; i < moves.Length; i++)
            {
                int start = moves[i], end = start;
                while (i + 1 < moves.Length && moves[i + 1] == end + 1) end = moves[++i];
                ranges.Add(start == end ? start + "회" : start + "~" + end + "회");
            }
            return string.Join(", ", ranges);
        }
    }
}
