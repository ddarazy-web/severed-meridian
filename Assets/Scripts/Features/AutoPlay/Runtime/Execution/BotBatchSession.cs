using System;
using System.Collections.Generic;
using System.Linq;
using Levels;
using Simulation;
using UnityEngine;

namespace AutoPlay
{
    /// <summary>
    /// 기본→계획 순서로 한 판씩 소유하는 반복 실행기. 일시정지는 Advance를 차단할 뿐
    /// 한 판의 검색·연쇄·난수를 건드리지 않는다. 저장은 Editor가 제공하는 체크포인트 콜백의 책임이다.
    /// </summary>
    public sealed class BotBatchSession : IDisposable
    {
        public const int DefaultCount = 100;
        public const string AssumptionVersion = PlanningBranch.Version;
        // 제작자가 실수로 아주 큰 배열을 예약하지 않도록 하는 입력 상한이다. 병렬 작업 수가 아니다.
        public const int MaximumCount = 10000;
        private LevelDefinition definition;
        private readonly Action<BotBatchRecord, BotBatchGame> checkpoint;
        private BotPlaySession current;
        private bool disposed;
        public BotBatchRecord Record { get; }
        public BotPlaySession Current => current;
        public bool CanContinue => !disposed && (Record.status == BotBatchStatus.Running || Record.status == BotBatchStatus.Paused);
        public bool NeedsAdvance => CanContinue && Record.status == BotBatchStatus.Running;
        public string SaveError { get; private set; }
        // 저장 실패 시 확정한 마지막 판을 잃지 않고 오류 화면에서 확인할 수 있도록 유지한다.
        public BotBatchGame UnsavedGame { get; private set; }

        /// <param name="source">현재 편집한 레벨. 저장되지 않은 값도 복사한다.</param>
        /// <param name="seeds">전략별로 공유할 중복 없는 시드 묶음.</param>
        /// <param name="checkpoint">상태 변경과 판 확정 시 동기 저장. 실패는 예외로 알려야 한다.</param>
        public BotBatchSession(LevelDefinition source, int[] seeds, Action<BotBatchRecord, BotBatchGame> checkpoint)
        {
            if (seeds == null || seeds.Length < 1 || seeds.Length > MaximumCount || seeds.Distinct().Count() != seeds.Length)
                throw new ArgumentException("시험 횟수는 1~10000회이며 묶음 안의 시드는 중복될 수 없습니다.", nameof(seeds));
            List<LevelValidationIssue> issues = LevelDefinitionValidator.Validate(source);
            if (issues.Count != 0) throw new ArgumentException(string.Join("\n", issues.Select(i => i.ToString())), nameof(source));
            this.checkpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
            definition = LevelPackCodec.Copy(source);
            definition.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                string json = JsonUtility.ToJson(definition);
                Record = new BotBatchRecord {
                    id = Guid.NewGuid().ToString("N"), definitionJson = json, fingerprint = LevelStateBuilder.Fingerprint(definition),
                    startedUtc = DateTime.UtcNow.ToString("O"), seeds = (int[])seeds.Clone(), status = BotBatchStatus.Running,
                    message = "반복 시험 준비", basicVersion = BasicBotStrategy.Version, planningVersion = PlanningSearch.Version,
                    observationVersion = BotObservationBuilder.Version, assumptionVersion = PlanningBranch.Version,
                    sessionVersion = BotPlaySession.Version, engineVersion = BoardActionExecutor.Version,
                    startingVersion = StartingBoardSearch.AlgorithmVersion };
                Save(null);
            }
            catch { UnityEngine.Object.DestroyImmediate(definition); definition = null; throw; }
        }

        /// <summary>새 시험은 새 시드를 만들되 과거 모든 시험과의 영구 중복 금지는 하지 않는다.</summary>
        /// <param name="count">전략별 판 수.</param><param name="previous">직전 묶음. 동일 묶음 재생성을 피한다.</param>
        /// <returns>중복 없는 시드 값 배열.</returns>
        public static int[] NewSeeds(int count, int[] previous = null)
        {
            if (count < 1 || count > MaximumCount) throw new ArgumentOutOfRangeException(nameof(count), "시험 횟수는 1~10000회입니다.");
            HashSet<int> used = new HashSet<int>();
            int[] result = new int[count];
            for (int i = 0; i < count; i++)
            {
                int seed;
                do { seed = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0); }
                while (!used.Add(seed) || i == 0 && previous?.Length > 0 && seed == previous[0]);
                result[i] = seed;
            }
            return result;
        }

        /// <summary>한 번의 호출에서는 판 생성 또는 한 판의 처리 한 단위만 수행한다.</summary>
        public void Advance()
        {
            if (!NeedsAdvance) return;
            if (current == null)
            {
                int index = Record.Recorded;
                try
                {
                    current = new BotPlaySession(definition, Record.seeds[index / 2],
                        index % 2 == 0 ? BotStrategyKind.Basic : BotStrategyKind.Planning);
                    current.Begin(true);
                }
                catch (Exception error)
                {
                    Record.status = BotBatchStatus.Error; Record.message = "판 준비 오류: " + error.Message;
                    Record.endedUtc = DateTime.UtcNow.ToString("O"); Save(null);
                }
                return;
            }
            current.Advance();
            Record.message = current.Message;
            // 실제 세션은 라스트팡을 모두 끝낸 다음 종료 상태를 노출한다.
            // 판 확정과 다음 판 생성을 같은 호출에서 처리하지 않아 판 사이도 일시정지 가능하다.
            if (!current.NeedsAdvance) Finish(current.Status, current.Message);
        }

        /// <returns>실행 중인 묶음을 멈춘 경우 true.</returns>
        public bool Pause()
        {
            if (!NeedsAdvance) return false;
            Record.status = BotBatchStatus.Paused; Record.message = "일시정지 · 현재 처리 상태 유지";
            return Save(null);
        }

        /// <returns>같은 진행 객체를 재개한 경우 true.</returns>
        public bool Resume()
        {
            if (!CanContinue || Record.status != BotBatchStatus.Paused) return false;
            Record.status = BotBatchStatus.Running; Record.message = "같은 상태에서 재개";
            return Save(null);
        }

        /// <param name="interrupted">창 종료·재로드 등 수명 종료면 true.</param>
        /// <param name="reason">중단 이유. 완료된 판에는 영향을 주지 않는다.</param>
        public void Stop(bool interrupted = false, string reason = "사용자 중지")
        {
            if (!CanContinue) return;
            Record.status = interrupted ? BotBatchStatus.Interrupted : BotBatchStatus.Stopped;
            Record.message = reason; Record.endedUtc = DateTime.UtcNow.ToString("O");
            if (current != null) Finish(BotSessionStatus.Stopped, reason);
            else Save(null);
        }

        /// <summary>확정 기록을 한 번만 집계·저장한 다음 실행 객체를 폐기한다.</summary>
        /// <param name="outcome">정상 종료, 오류 또는 중단.</param><param name="message">판 결과 설명.</param>
        private void Finish(BotSessionStatus outcome, string message)
        {
            if (outcome != BotSessionStatus.Won && outcome != BotSessionStatus.MovesExhausted &&
                outcome != BotSessionStatus.Blocked && outcome != BotSessionStatus.Stopped && outcome != BotSessionStatus.Error)
            { outcome = BotSessionStatus.Error; message = "종료 상태 불일치: " + message; }
            BotBatchGame game = new BotBatchGame(Record, current, outcome, message);
            switch (outcome)
            {
                case BotSessionStatus.Won: Record.won++; break;
                case BotSessionStatus.MovesExhausted: Record.exhausted++; break;
                case BotSessionStatus.Blocked: Record.blocked++; break;
                case BotSessionStatus.Stopped: Record.stopped++; break;
                default: Record.errors++; Record.status = BotBatchStatus.Error; break;
            }
            if (outcome == BotSessionStatus.Won || outcome == BotSessionStatus.MovesExhausted || outcome == BotSessionStatus.Blocked)
            {
                Record.finished++;
                if (current.Strategy == BotStrategyKind.Basic) Record.basicFinished++; else Record.planningFinished++;
            }
            if (Record.finished == Record.Total) Record.status = BotBatchStatus.Completed;
            Record.message = message;
            if (!CanContinue) Record.endedUtc = DateTime.UtcNow.ToString("O");
            Save(game);
            current.Dispose(); current = null;
        }

        /// <summary>저장 실패를 정상 종료로 집계하지 않고 멈춘다. 같은 판의 재시도로 중복 기록하지 않는다.</summary>
        /// <param name="game">이번에 확정한 판. 상태만 저장할 때는 null.</param><returns>저장 성공 여부.</returns>
        private bool Save(BotBatchGame game)
        {
            try { checkpoint(Record, game); return true; }
            catch (Exception error)
            {
                SaveError = error.GetType().Name + ": " + error.Message; UnsavedGame = game;
                Record.status = BotBatchStatus.Error; Record.message = "기록 저장 실패 · " + SaveError;
                Record.endedUtc = DateTime.UtcNow.ToString("O");
                return false;
            }
        }

        /// <summary>호출자가 닫기 사유를 주지 않은 해제도 중단으로 남긴다. 정상 완료 상태는 유지한다.</summary>
        public void Dispose()
        {
            if (disposed) return;
            Stop(true, "실행 소유자 종료");
            disposed = true; current?.Dispose(); current = null;
            if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
            definition = null;
        }
    }
}
