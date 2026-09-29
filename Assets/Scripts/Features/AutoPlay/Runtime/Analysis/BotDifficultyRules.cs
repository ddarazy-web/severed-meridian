using System;
using System.Collections.Generic;

namespace AutoPlay
{
    public enum BotDifficultyGrade { Easy = 1, Normal = 2, Hard = 3, VeryHard = 4 }

    /// <summary>추천의 관측 근거와 보류 이유. 저장 기록이나 실행 상태를 소유하지 않는 읽기 전용 결과다.</summary>
    public sealed class BotDifficultyResult
    {
        public BotDifficultyGrade? Grade { get; }
        public BotStrategyKind Representative { get; }
        public double? SuccessPercent { get; }
        public IReadOnlyList<string> HoldReasons { get; }
        public IReadOnlyList<string> Tags { get; }
        public IReadOnlyList<string> Notes { get; }
        public string Title => Grade switch {
            BotDifficultyGrade.Easy => "쉬움",
            BotDifficultyGrade.Normal => "보통", BotDifficultyGrade.Hard => "어려움",
            BotDifficultyGrade.VeryHard => "매우 어려움", _ => "난이도 판단 보류" };

        internal BotDifficultyResult(BotDifficultyGrade? grade, BotStrategyKind representative, double? success,
            List<string> hold, List<string> tags, List<string> notes)
        {
            Grade = grade; Representative = representative; SuccessPercent = success;
            HoldReasons = hold.AsReadOnly(); Tags = tags.AsReadOnly(); Notes = notes.AsReadOnly();
        }
    }

    /// <summary>
    /// 기존 집계의 값만 읽는 평가 규칙. 게임 실행·파일·Editor API를 호출하지 않는다.
    /// 기준은 제작용 초기 휴리스틱이며 사람의 체감 난이도나 인과관계의 검증값이 아니다.
    /// 수치 또는 보류/태그 의미를 변경하면 Version도 변경하여 재계산의 의미를 드러낸다.
    /// </summary>
    public static class BotDifficultyRules
    {
        public const string Version = "bot-difficulty-v2";
        public const int MinimumNormalPerStrategy = 100;
        public const int MinimumMoveSamples = 20;
        public const int MinimumDiscordantPairs = 20;
        public const double PlanningAdvantagePercent = 20;
        public const double LowRemainingRatio = .1;

        /// <param name="record">읽기 검증을 거친 한 시험 묶음. 원본에 값을 쓰지 않는다.</param>
        /// <param name="basic">기본 전략의 기존 통계.</param>
        /// <param name="planning">계획 전략의 기존 통계.</param>
        /// <param name="pairs">기본을 왼쪽, 계획을 오른쪽으로 한 같은 시드 정상 종료 쌍.</param>
        /// <param name="initialMoves">저장된 레벨 정의의 최초 이동 수. 라스트팡 표시 수가 아니다.</param>
        /// <param name="recordIssue">읽기·정합성 검사에서 발견한 문제. 없으면 null.</param>
        /// <param name="versionIssue">현재 평가가 지원하지 않는 실행 조건의 설명. 없으면 null.</param>
        /// <returns>4단계 추천 또는 모든 보류 이유와 관측 근거.</returns>
        public static BotDifficultyResult Evaluate(BotBatchRecord record, BotStrategyStatistics basic,
            BotStrategyStatistics planning, BotPairedStatistics pairs, int initialMoves,
            string recordIssue = null, string versionIssue = null)
        {
            if (record == null || basic == null || planning == null || pairs == null)
                throw new ArgumentNullException("평가에는 검증한 기록과 두 전략의 집계가 필요합니다.");
            List<string> hold = new List<string>(), tags = new List<string>(), notes = new List<string>();
            // 보류 이유는 입력 신뢰성 → 실행 완결성 → 표본 → 관측 결과 순서로 모두 유지한다.
            // 오류가 난 레벨을 낮은 성공률만 보고 어려운 레벨로 바꾸지 않는다.
            if (!string.IsNullOrEmpty(recordIssue)) hold.Add("기록 확인 필요: " + recordIssue);
            if (!string.IsNullOrEmpty(versionIssue)) hold.Add("지원하지 않는 시험 조건: " + versionIssue);
            if (initialMoves <= 0) hold.Add("최초 이동 수가 올바르지 않습니다.");
            if (basic.Errors + planning.Errors > 0) hold.Add("실행 오류가 있습니다. 오류를 수정한 뒤 다시 시험하세요.");
            if (record.status != BotBatchStatus.Completed || basic.Stopped + planning.Stopped > 0 ||
                basic.Unrun + planning.Unrun > 0)
                hold.Add("일부 실행 또는 중단된 시험입니다. 예정한 두 전략의 시험을 완료하세요.");
            if (basic.Normal == 0 || planning.Normal == 0) hold.Add("한쪽 또는 양쪽 전략의 정상 종료 기록이 없습니다.");
            if (basic.Normal < MinimumNormalPerStrategy || planning.Normal < MinimumNormalPerStrategy)
                hold.Add("기본·계획 전략 각각 정상 종료 100판 이상이 필요합니다.");
            if (pairs.Excluded != 0 || pairs.Included != basic.Normal || pairs.Included != planning.Normal)
                hold.Add("두 전략의 같은 시드 정상 종료 쌍이 완전하지 않습니다.");
            if (basic.Normal > 0 && planning.Normal > 0 && basic.Won == 0 && planning.Won == 0)
                hold.Add("이번 시험에서 성공 사례가 없습니다. 해결 불가능하다는 뜻은 아닙니다.");

            // 계획 전략이 항상 우수하다고 가정하지 않는다. 동률은 기본을 선택하여 결과를 고정한다.
            // 두 전략을 합쳐 독립적인 200판처럼 계산하지 않고 선택한 전략의 분모를 유지한다.
            BotStrategyStatistics representative = (planning.SuccessPercent ?? -1) > (basic.SuccessPercent ?? -1) ? planning : basic;
            double? rate = representative.SuccessPercent;
            BotDifficultyGrade? grade = null;
            if (hold.Count == 0)
            {
                // 표시용 반올림은 이 계산 뒤에 한다. 59.99%를 60% 구간으로 올리지 않는다.
                grade = rate >= 60 ? BotDifficultyGrade.Easy :
                    rate >= 40 ? BotDifficultyGrade.Normal : rate >= 20 ? BotDifficultyGrade.Hard : BotDifficultyGrade.VeryHard;
                double difference = 100d * (pairs.RightOnlyWon - pairs.LeftOnlyWon) / pairs.Included;
                if (pairs.Included >= MinimumNormalPerStrategy && pairs.RightOnlyWon + pairs.LeftOnlyWon >= MinimumDiscordantPairs &&
                    difference >= PlanningAdvantagePercent)
                    tags.Add("계획 요구");
                // 일부 성공 판의 이동 수만 남았다면 편향된 중앙값으로 태그를 만들지 않는다.
                if (representative.Won >= MinimumMoveSamples && representative.Remaining.Count == representative.Won &&
                    representative.Remaining.Median.HasValue && representative.Remaining.Median.Value <= initialMoves * LowRemainingRatio)
                    tags.Add("이동 여유 부족");
            }
            if (representative.Won > 0 && representative.Remaining.Count < representative.Won)
                notes.Add("성공 판의 남은 이동 수 기록이 일부 없어 이동 여유 태그를 판단하지 않았습니다.");
            notes.Add("현재 평가 기준으로 다시 계산한 봇의 예상 난이도입니다. 사람의 체감 난이도는 미검증입니다.");
            notes.Add("새 시드 묶음에서는 등급이 달라질 수 있습니다. 무작위 영향의 인과 근거가 부족하여 해당 태그는 표시하지 않습니다.");
            return new BotDifficultyResult(grade, representative.Strategy, rate, hold, tags, notes);
        }
    }
}
