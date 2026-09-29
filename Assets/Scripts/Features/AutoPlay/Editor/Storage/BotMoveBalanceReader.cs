using System;
using System.IO;
using System.Linq;
using AutoPlay;
using Simulation;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>
    /// 밸런스 요약을 원시 판과 대조한다. 한 번에 한 판만 읽어 큰 기록에서도 UI가 제어를 돌려받는다.
    /// 완료 구간만 검사하며 중단된 마지막 구간을 새 완료 표본으로 올리지 않는다.
    /// </summary>
    internal sealed class BotMoveBalanceReader
    {
        private readonly string root;
        private BotAnalysisReader batch;
        internal BotMoveBalanceRecord Record { get; }
        internal int CheckedTrials { get; private set; }
        internal bool IsDone { get; private set; }
        internal string Error { get; private set; }

        /// <param name="store">원본 파일을 변경하지 않는 조회 대상 보관소.</param>
        internal BotMoveBalanceReader(BotMoveBalanceStore store)
        {
            root = store.Root; Record = store.LoadSummary();
            IsDone = Record == null || Record.trials.Count == 0;
        }

        /// <summary>다음 파일을 확인한다. 오류가 나면 이전 성공 결과를 정상 추천으로 내보내지 않는다.</summary>
        internal void Advance()
        {
            if (IsDone) return;
            try
            {
                BotMoveTrial trial = Record.trials[CheckedTrials];
                if (batch == null)
                {
                    batch = new BotAnalysisReader(Path.Combine(root, Record.id, "trials", trial.batchId));
                    BotBatchRecord header = batch.Record;
                    string version = string.Join("|", header.engineVersion, header.sessionVersion, header.basicVersion,
                        header.planningVersion, header.observationVersion, header.assumptionVersion, header.startingVersion);
                    if (header.id != trial.batchId || header.status != BotBatchStatus.Completed || header.finished != 200 ||
                        batch.FileCount != 200 || !header.seeds.SequenceEqual(Record.seeds) || version != Record.engineVersion)
                        throw new InvalidDataException("완료 구간의 식별자·판 수·시드·버전이 요약과 다릅니다.");
                    // 각 구간은 대표 정의에서 이동 수만 달라야 한다. 읽기 사본은 이 단계 안에서 정리한다.
                    LevelDefinition expected = ScriptableObject.CreateInstance<LevelDefinition>();
                    try
                    {
                        JsonUtility.FromJsonOverwrite(Record.definitionJson, expected);
                        JsonUtility.FromJsonOverwrite("{\"moveCount\":" + trial.moves + "}", expected);
                        if (LevelStateBuilder.Fingerprint(expected) != header.fingerprint || batch.InitialMoves != trial.moves)
                            throw new InvalidDataException("시험 구간의 레벨 정의가 대표 조건과 다릅니다.");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(expected); }
                }
                batch.Advance();
                if (!batch.IsDone) return;
                if (batch.Error != null) throw new InvalidDataException(batch.Error);
                if (batch.Games.Any(game => game.UsedMoves < 0 || game.RemainingMoves < 0 ||
                    (long)game.UsedMoves + game.RemainingMoves != trial.moves))
                    throw new InvalidDataException("원시 판의 사용·남은 이동 수가 시험 조건과 다릅니다.");
                BotStrategyStatistics basic = BotBatchStatistics.Calculate(batch.Record, batch.Games, batch.Missions, BotStrategyKind.Basic);
                BotStrategyStatistics planning = BotBatchStatistics.Calculate(batch.Record, batch.Games, batch.Missions, BotStrategyKind.Planning);
                BotPairedStatistics pairs = BotBatchStatistics.Pair(Record.seeds,
                    batch.Games.Where(game => game.Strategy == BotStrategyKind.Basic), batch.Games.Where(game => game.Strategy == BotStrategyKind.Planning));
                if (basic.Normal != 100 || planning.Normal != 100 || pairs.Included != 100 || pairs.Excluded != 0 ||
                    basic.Won != trial.basicWon || planning.Won != trial.planningWon)
                    throw new InvalidDataException("원시 판의 정상 종료·성공 수가 밸런스 요약과 다릅니다.");
                // 다른 평가 버전은 UI에서 추천을 막는다. 현재 버전일 때만 저장 등급까지 같은 기준으로 대조한다.
                if (Record.rulesVersion == BotMoveRecommendations.Version)
                {
                    BotDifficultyResult evaluated = BotDifficultyRules.Evaluate(batch.Record, basic, planning, pairs, trial.moves);
                    if (BotMoveRecommendations.Grade(evaluated.Grade) != trial.grade)
                        throw new InvalidDataException("원시 성공률과 저장된 난이도가 다릅니다.");
                }
                CheckedTrials++; batch = null;
                IsDone = CheckedTrials == Record.trials.Count;
            }
            catch (Exception error) { Error = error.Message; IsDone = true; batch = null; }
        }
    }
}
