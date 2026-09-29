using System;
using System.Collections.Generic;
using System.Linq;
using Levels;

namespace AutoPlay
{
    /// <summary>이력 표에 필요한 값만 보관한다. 무거운 행동열과 게임 상태는 사례를 선택할 때만 읽는다.</summary>
    public sealed class BotGameSummary
    {
        public int Ordinal { get; }
        public int Seed { get; }
        public BotStrategyKind Strategy { get; }
        public BotSessionStatus Outcome { get; }
        public int UsedMoves { get; }
        public int RemainingMoves { get; }
        public string Message { get; }
        public IReadOnlyList<BotBatchMission> Missions { get; }
        public bool IsNormal => Outcome == BotSessionStatus.Won || Outcome == BotSessionStatus.MovesExhausted || Outcome == BotSessionStatus.Blocked;

        /// <param name="game">검증한 판별 저장 값. 행동 배열은 보관하지 않는다.</param>
        public BotGameSummary(BotBatchGame game)
        {
            Ordinal = game.ordinal; Seed = game.seed; Strategy = game.strategy; Outcome = game.outcome;
            UsedMoves = game.usedMoves; RemainingMoves = game.remainingMoves; Message = game.message;
            Missions = Array.AsReadOnly(game.missions.Select(m => new BotBatchMission { kind = m.kind, color = m.color, remaining = m.remaining }).ToArray());
        }
    }

    /// <summary>표본이 없는 경우 null을 반환하여 0회/0%로 오해하지 않게 한다.</summary>
    public sealed class BotMoveStatistics
    {
        public int Count { get; }
        public double? Mean { get; }
        public double? Median { get; }
        public int? Minimum { get; }
        public int? Maximum { get; }

        /// <param name="values">같은 조건에서 수집한 이동 수 표본.</param>
        public BotMoveStatistics(IEnumerable<int> values)
        {
            int[] sorted = values.OrderBy(value => value).ToArray(); Count = sorted.Length;
            if (Count == 0) return;
            Mean = sorted.Average(); Minimum = sorted[0]; Maximum = sorted[Count - 1];
            Median = Count % 2 == 1 ? sorted[Count / 2] : ((double)sorted[Count / 2 - 1] + sorted[Count / 2]) / 2;
        }
    }

    public sealed class BotMissionStatistics
    {
        public int Index { get; internal set; }
        public MissionKind Kind { get; internal set; }
        public RabbitColor Color { get; internal set; }
        public int Samples { get; internal set; }
        public int Missing { get; internal set; }
        public double? MeanRemaining { get; internal set; }
        public int? MaximumRemaining { get; internal set; }
        public int Unfinished { get; internal set; }
    }

    public sealed class BotStrategyStatistics
    {
        public BotStrategyKind Strategy { get; internal set; }
        public int Planned { get; internal set; }
        public int Won { get; internal set; }
        public int Exhausted { get; internal set; }
        public int Blocked { get; internal set; }
        public int Errors { get; internal set; }
        public int Stopped { get; internal set; }
        public int Normal => Won + Exhausted + Blocked;
        public int Unrun => Planned - Normal - Errors - Stopped;
        public double? SuccessPercent => Normal == 0 ? (double?)null : 100d * Won / Normal;
        public BotMoveStatistics Used { get; internal set; }
        public BotMoveStatistics Remaining { get; internal set; }
        public IReadOnlyList<BotMissionStatistics> Missions { get; internal set; }
    }

    /// <summary>양쪽 모두 정상 종료한 같은 시드의 쌍만 비교한다.</summary>
    public sealed class BotPairedStatistics
    {
        public int BothWon { get; internal set; }
        public int LeftOnlyWon { get; internal set; }
        public int RightOnlyWon { get; internal set; }
        public int NeitherWon { get; internal set; }
        public int Included => BothWon + LeftOnlyWon + RightOnlyWon + NeitherWon;
        public int Excluded { get; internal set; }
    }

    /// <summary>파일·UI·게임 실행과 독립된 통계 산식. 분석용 비공개 값은 봇의 관찰 입력으로 전달하지 않는다.</summary>
    public static class BotBatchStatistics
    {
        /// <param name="record">한 묶음의 조건.</param><param name="games">검증한 전체 판 요약.</param>
        /// <param name="missions">저장된 정의의 미션. 순서와 종류·색을 함께 비교한다.</param>
        /// <param name="strategy">집계할 전략.</param><returns>해당 전략의 표본 수와 통계.</returns>
        public static BotStrategyStatistics Calculate(BotBatchRecord record, IReadOnlyList<BotGameSummary> games,
            IReadOnlyList<LevelMissionDefinition> missions, BotStrategyKind strategy)
        {
            BotGameSummary[] selected = games.Where(g => g.Strategy == strategy).ToArray();
            BotGameSummary[] won = selected.Where(g => g.Outcome == BotSessionStatus.Won).ToArray();
            BotGameSummary[] lost = selected.Where(g => g.Outcome == BotSessionStatus.MovesExhausted || g.Outcome == BotSessionStatus.Blocked).ToArray();
            List<BotMissionStatistics> remaining = new List<BotMissionStatistics>();
            for (int i = 0; i < missions.Count; i++)
            {
                LevelMissionDefinition definition = missions[i];
                // 같은 종류가 두 칸에 있어도 합치지 않는다. 곰팡이의 증가분도 실제 잔여량 그대로 집계한다.
                int[] values = lost.Where(g => g.Missions.Count > i && g.Missions[i].kind == definition.Kind &&
                    g.Missions[i].color == definition.Color && g.Missions[i].remaining >= 0).Select(g => g.Missions[i].remaining).ToArray();
                remaining.Add(new BotMissionStatistics {
                    Index = i, Kind = definition.Kind, Color = definition.Color, Samples = values.Length, Missing = lost.Length - values.Length,
                    MeanRemaining = values.Length == 0 ? (double?)null : values.Average(),
                    MaximumRemaining = values.Length == 0 ? (int?)null : values.Max(), Unfinished = values.Count(v => v > 0) });
            }
            return new BotStrategyStatistics {
                Strategy = strategy, Planned = record.seeds.Length, Won = won.Length,
                Exhausted = selected.Count(g => g.Outcome == BotSessionStatus.MovesExhausted), Blocked = selected.Count(g => g.Outcome == BotSessionStatus.Blocked),
                Errors = selected.Count(g => g.Outcome == BotSessionStatus.Error), Stopped = selected.Count(g => g.Outcome == BotSessionStatus.Stopped),
                // 구기록에서 한쪽 값만 빠져도 이미 알려진 다른 이동 수까지 버리지 않는다.
                Used = new BotMoveStatistics(won.Where(g => g.UsedMoves >= 0).Select(g => g.UsedMoves)),
                Remaining = new BotMoveStatistics(won.Where(g => g.RemainingMoves >= 0).Select(g => g.RemainingMoves)), Missions = remaining.AsReadOnly() };
        }

        /// <param name="seeds">비교할 시드 집합. 제외 수의 기준으로도 사용한다.</param>
        /// <param name="left">왼쪽 단일 전략 기록.</param><param name="right">오른쪽 단일 전략 기록.</param>
        /// <returns>양쪽 모두 정상 종료 기록이 있는 쌍의 성공 비교.</returns>
        public static BotPairedStatistics Pair(IEnumerable<int> seeds, IEnumerable<BotGameSummary> left, IEnumerable<BotGameSummary> right)
        {
            Dictionary<int, BotGameSummary> l = left.ToDictionary(g => g.Seed), r = right.ToDictionary(g => g.Seed);
            BotPairedStatistics result = new BotPairedStatistics();
            foreach (int seed in seeds.Distinct())
            {
                if (!l.TryGetValue(seed, out BotGameSummary a) || !r.TryGetValue(seed, out BotGameSummary b) || !a.IsNormal || !b.IsNormal)
                { result.Excluded++; continue; }
                bool aw = a.Outcome == BotSessionStatus.Won, bw = b.Outcome == BotSessionStatus.Won;
                if (aw && bw) result.BothWon++; else if (aw) result.LeftOnlyWon++; else if (bw) result.RightOnlyWon++; else result.NeitherWon++;
            }
            return result;
        }
    }
}
