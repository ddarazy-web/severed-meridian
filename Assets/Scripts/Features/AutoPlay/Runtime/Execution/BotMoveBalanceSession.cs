using Levels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoPlay;
using Simulation;
using UnityEngine;

namespace AutoPlay
{
    /// <summary>실제 반복 실행기를 1~100회 설정으로 순차 실행한다. 봇·게임 규칙에는 분석 정보를 전달하지 않는다.</summary>
    public sealed class BotMoveBalanceSession : IDisposable
    {
        private readonly LevelDefinition source;
        private readonly BotMoveBalanceStore store;
        private readonly List<BotGameSummary> games = new List<BotGameSummary>();
        private BotBatchSession batch;
        private bool disposed;
        public BotMoveBalanceRecord Record { get; }
        public BotBatchSession Current => batch;
        public bool CanContinue => !disposed && (Record.status == BotBatchStatus.Running || Record.status == BotBatchStatus.Paused);
        public bool NeedsAdvance => CanContinue && Record.status == BotBatchStatus.Running;

        /// <param name="level">편집 원본. JSON 사본만 사용한다.</param><param name="store">프로젝트 로컬 보관소.</param>
        public BotMoveBalanceSession(LevelDefinition level, BotMoveBalanceStore store)
        {
            var issues = LevelDefinitionValidator.Validate(level);
            if (issues.Count != 0) throw new ArgumentException(string.Join("\n", issues));
            this.store = store;
            source = LevelPackCodec.Copy(level); source.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                string json = JsonUtility.ToJson(source);
                Record = new BotMoveBalanceRecord { id = Guid.NewGuid().ToString("N"), sourceName = level.name,
                    definitionJson = json, fingerprint = LevelStateBuilder.Fingerprint(level),
                    engineVersion = BotMoveRecommendations.ExecutionVersion, rulesVersion = BotMoveRecommendations.Version,
                    startedUtc = DateTime.UtcNow.ToString("O"), seeds = BotBatchSession.NewSeeds(BotMoveRecommendations.Samples),
                    status = BotBatchStatus.Running, message = "1~100회 시험 준비 · 총 20,000판" };
                store.Save(Record);
            }
            catch { UnityEngine.Object.DestroyImmediate(source); throw; }
        }

        /// <summary>게임 실행 한 단위만 진행한다. 난이도는 200판이 모두 끝난 횟수에만 부여한다.</summary>
        public void Advance()
        {
            if (!NeedsAdvance) return;
            try
            {
                if (batch == null)
                {
                    int moves = Record.trials.Count + 1;
                    // 원본을 고치지 않고 사본의 이동 횟수 필드만 덮어쓴다. 각 시험은 같은 시드로 시작한다.
                    JsonUtility.FromJsonOverwrite("{\"moveCount\":" + moves + "}", source);
                    games.Clear();
                    BotBatchStore trialStore = new BotBatchStore(Path.Combine(store.Root, Record.id, "trials"), store.Source?.ForMoves(moves), store.Persist);
                    batch = new BotBatchSession(source, Record.seeds, (record, game) => {
                        record.sourceName = Record.sourceName; trialStore.Save(record, game);
                        if (game != null) games.Add(new BotGameSummary(game));
                    });
                    // 비동기 소유자가 최초 판 목록의 저장을 확인한 뒤 게임 생성을 허용한다.
                    if (store.Persist != null) return;
                }
                batch.Advance();
                Record.message = $"이동 {Record.trials.Count + 1}회 시험 · {batch.Record.finished}/200판";
                if (batch.Record.status == BotBatchStatus.Error) throw new InvalidOperationException(batch.Record.message);
                if (batch.Record.status != BotBatchStatus.Completed) return;
                BotStrategyStatistics basic = BotBatchStatistics.Calculate(batch.Record, games, source.Missions, BotStrategyKind.Basic);
                BotStrategyStatistics planning = BotBatchStatistics.Calculate(batch.Record, games, source.Missions, BotStrategyKind.Planning);
                BotPairedStatistics pairs = BotBatchStatistics.Pair(Record.seeds, games.Where(g => g.Strategy == BotStrategyKind.Basic), games.Where(g => g.Strategy == BotStrategyKind.Planning));
                BotDifficultyResult result = BotDifficultyRules.Evaluate(batch.Record, basic, planning, pairs, source.MoveCount);
                Record.trials.Add(new BotMoveTrial { moves = source.MoveCount, basicWon = basic.Won, planningWon = planning.Won,
                    normalPerStrategy = Math.Min(basic.Normal, planning.Normal), batchId = batch.Record.id,
                    grade = BotMoveRecommendations.Grade(result.Grade), reason = string.Join(" / ", result.HoldReasons) });
                batch.Dispose(); batch = null; games.Clear();
                if (Record.trials.Count == BotMoveRecommendations.MaximumMoves)
                { Record.status = BotBatchStatus.Completed; Record.message = "1~100회 시험 완료 · 총 20,000판"; }
                store.Save(Record);
            }
            catch (Exception error)
            {
                Record.status = BotBatchStatus.Error; Record.message = "시험 오류 · " + error.Message;
                batch?.Dispose(); batch = null;
                try { store.Save(Record); } catch (Exception saveError) { Record.message += " / 요약 저장 실패: " + saveError.Message; }
            }
        }

        /// <param name="paused">true는 현재 상태 유지, false는 같은 객체에서 재개.</param>
        public void SetPaused(bool paused)
        {
            if (!CanContinue) return;
            if (paused) batch?.Pause(); else batch?.Resume();
            Record.status = paused ? BotBatchStatus.Paused : BotBatchStatus.Running;
            Record.message = paused ? "일시정지 · 재개 또는 중지 가능" : "같은 상태에서 재개";
            SaveControl();
        }

        /// <param name="interrupted">창 종료·재컴파일에 따른 중단인지 여부.</param>
        public void Stop(bool interrupted = false)
        {
            if (!CanContinue) return;
            batch?.Stop(interrupted); batch?.Dispose(); batch = null;
            Record.status = interrupted ? BotBatchStatus.Interrupted : BotBatchStatus.Stopped;
            Record.message = "일부 시험 중단 · 완료한 이동 횟수만 표시";
            SaveControl();
        }

        /// <summary>제어 상태의 저장 실패도 정상 완료로 취급하지 않는다.</summary>
        private void SaveControl()
        {
            try { store.Save(Record); }
            catch (Exception error) { Record.status = BotBatchStatus.Error; Record.message = "요약 저장 실패: " + error.Message; batch?.Dispose(); batch = null; }
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop(true); batch?.Dispose(); batch = null; disposed = true;
            UnityEngine.Object.DestroyImmediate(source);
        }
    }
}
