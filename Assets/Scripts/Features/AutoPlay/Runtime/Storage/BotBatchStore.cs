using System;
using System.IO;
using System.Linq;
using AutoPlay;
using Levels;
using Simulation;
using UnityEngine;

namespace AutoPlay
{
    /// <summary>
    /// 프로젝트 로컬 시험 기록. 판 파일을 먼저 확정하고 요약을 나중에 교체한다.
    /// 두 저장 사이에 Editor가 종료되어도 판 파일을 다시 세어 완료 기록을 복구한다.
    /// Assets 밖에 저장하므로 매 판 가져오기나 도메인 재로드를 일으키지 않는다.
    /// </summary>
    public sealed class BotBatchStore
    {
        private readonly string root;
        private readonly string sourceContext;
        private readonly Action<Action> persist;
        public BotBatchStore(string root, string sourceContext = null, Action<Action> persist = null)
        { this.root = root; this.sourceContext = sourceContext; this.persist = persist; }

        public string ReadSourceContext(string id)
        {
            if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("시험 식별자가 올바르지 않습니다.");
            return File.ReadAllText(Path.Combine(root, id, "source.context"));
        }

        /// <param name="record">현재 묶음 요약.</param><param name="game">새로 확정한 판. 제어 상태만 바뀌면 null.</param>
        public void Save(BotBatchRecord record, BotBatchGame game)
        {
            Action write = PrepareSave(record, game);
            if (persist == null) write(); else persist(write);
        }

        // Unity 직렬화는 호출 스레드에서 끝내고, 반환 작업은 불변 문자열만 파일에 쓴다.
        public Action PrepareSave(BotBatchRecord record, BotBatchGame game)
        {
            if (!Guid.TryParseExact(record.id, "N", out _)) throw new InvalidDataException("시험 식별자가 올바르지 않습니다.");
            string id = record.id, summary = JsonUtility.ToJson(record, true);
            string detail = game == null ? null : JsonUtility.ToJson(game);
            int ordinal = game?.ordinal ?? -1;
            return () => SavePrepared(id, summary, ordinal, detail);
        }

        private void SavePrepared(string id, string summary, int ordinal, string detail)
        {
            string directory = Path.Combine(root, id);
            Directory.CreateDirectory(directory);
            // 독립 제작 원본은 첫 체크포인트보다 먼저 확정한다. 판 JSON 순회에는 섞이지 않는다.
            string contextPath = Path.Combine(directory, "source.context");
            if (sourceContext != null && !File.Exists(contextPath)) WriteAtomic(contextPath, sourceContext);
            if (detail != null)
            {
                string path = Path.Combine(directory, ordinal.ToString("D6") + ".json");
                // 같은 완료 기록의 두 번 발행은 덮어쓰기로 감추지 않는다.
                if (File.Exists(path)) throw new InvalidDataException("이미 기록한 판입니다: " + ordinal);
                WriteAtomic(path, detail);
            }
            WriteAtomic(Path.Combine(directory, "batch.json"), summary);
            // 같은 묶음의 매 판마다 동일한 포인터를 교체할 필요는 없다.
            // Windows에서 불필요한 즉시 재교체가 파일 접근 충돌을 일으킬 수 있으므로
            // 새 묶음일 때만 포인터를 바꾸고 판별 체크포인트는 요약 파일에 남긴다.
            string latest = Path.Combine(root, "latest.txt");
            if (!File.Exists(latest) || File.ReadAllText(latest) != id) WriteAtomic(latest, id);
        }

        /// <summary>직전 묶음의 값만 복원한다. 진행 중 게임 객체나 과거 전략 코드는 복원하지 않는다.</summary>
        /// <param name="persistRecovery">복구한 요약을 다시 쓸지 여부. 화면 조회는 false로 원본 파일을 보존한다.</param>
        /// <returns>기록이 없으면 null. 손상된 기록은 예외로 구분하여 UI에 알린다.</returns>
        public BotBatchRecord LoadLatest(bool persistRecovery = true)
        {
            string pointer = Path.Combine(root, "latest.txt");
            if (!File.Exists(pointer)) return null;
            string id = File.ReadAllText(pointer).Trim();
            if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("최근 시험 식별자가 손상되었습니다.");
            string directory = Path.Combine(root, id);
            BotBatchRecord record = JsonUtility.FromJson<BotBatchRecord>(File.ReadAllText(Path.Combine(directory, "batch.json")));
            if (record == null || record.formatVersion != 1 || record.id != id || string.IsNullOrEmpty(record.definitionJson) ||
                record.seeds == null || record.seeds.Length < 1 || record.seeds.Length > BotBatchSession.MaximumCount ||
                record.seeds.Distinct().Count() != record.seeds.Length || !Enum.IsDefined(typeof(BotBatchStatus), record.status))
                throw new InvalidDataException("시험 기록 형식이나 시드 묶음이 올바르지 않습니다.");

            int savedRecorded = record.Recorded;
            record.finished = record.won = record.exhausted = record.blocked = record.errors = record.stopped = 0;
            record.basicFinished = record.planningFinished = 0;
            string[] paths = Directory.GetFiles(directory, "*.json").Where(p => Path.GetFileName(p) != "batch.json")
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (paths.Length > record.Total) throw new InvalidDataException("요청한 횟수보다 판 기록이 많습니다.");
            for (int i = 0; i < paths.Length; i++)
            {
                BotBatchGame game = JsonUtility.FromJson<BotBatchGame>(File.ReadAllText(paths[i]));
                if (game == null || Path.GetFileName(paths[i]) != i.ToString("D6") + ".json" || game.batchId != id || game.ordinal != i ||
                    game.seed != record.seeds[i / 2] || game.strategy != (i % 2 == 0 ? BotStrategyKind.Basic : BotStrategyKind.Planning) ||
                    game.actions == null || game.missions == null || record.errors != 0 || record.stopped != 0)
                    throw new InvalidDataException("판 기록에 누락·중복·순서 오류가 있습니다: " + i);
                switch (game.outcome)
                {
                    case BotSessionStatus.Won: record.won++; break;
                    case BotSessionStatus.MovesExhausted: record.exhausted++; break;
                    case BotSessionStatus.Blocked: record.blocked++; break;
                    case BotSessionStatus.Error: record.errors++; break;
                    case BotSessionStatus.Stopped: record.stopped++; break;
                    default: throw new InvalidDataException("확정되지 않은 판 결과입니다: " + i);
                }
                if (game.outcome == BotSessionStatus.Won || game.outcome == BotSessionStatus.MovesExhausted || game.outcome == BotSessionStatus.Blocked)
                {
                    record.finished++;
                    if (game.strategy == BotStrategyKind.Basic) record.basicFinished++; else record.planningFinished++;
                }
            }
            // 판 파일 없이 요약 수만 높으면 손실이다. 조용히 수를 줄여 정상 결과처럼 보이지 않게 한다.
            if (savedRecorded > record.Recorded) throw new InvalidDataException("완료 요약에 있는 판 파일이 사라졌습니다.");
            if (record.finished == record.Total) record.status = BotBatchStatus.Completed;
            else if (record.errors > 0) record.status = BotBatchStatus.Error;
            else if (record.status == BotBatchStatus.Running || record.status == BotBatchStatus.Paused || record.Recorded > savedRecorded)
            {
                record.status = BotBatchStatus.Interrupted;
                record.message = "시험 도구 실행이 끝나 일부만 기록되었습니다. 완료된 판은 보존했습니다.";
                record.endedUtc = DateTime.UtcNow.ToString("O");
            }
            else if (record.status == BotBatchStatus.Completed) throw new InvalidDataException("전체 완료 표시와 판 수가 맞지 않습니다.");
            // 되살아난 객체는 없다. 중단된 묶음에서 새로운 판을 실행하는 경로도 제공하지 않는다.
            if (persistRecovery) WriteAtomic(Path.Combine(directory, "batch.json"), JsonUtility.ToJson(record, true));
            return record;
        }

        /// <param name="record">저장된 시험 조건.</param><returns>현재 코드가 기록 당시의 규칙 버전과 모두 같은지.</returns>
        public static bool SameVersions(BotBatchRecord record) => record.basicVersion == BasicBotStrategy.Version &&
            record.planningVersion == PlanningSearch.Version && record.observationVersion == BotObservationBuilder.Version &&
            record.sessionVersion == BotPlaySession.Version && record.engineVersion == BoardActionExecutor.Version &&
            record.assumptionVersion == BotBatchSession.AssumptionVersion;

        /// <summary>같은 폴더의 임시 파일을 교체한다. 실패 시 이전 확정 파일을 삭제하지 않는다.</summary>
        /// <param name="path">확정할 파일.</param><param name="contents">UTF-8로 기록할 값.</param>
        public static void WriteAtomic(string path, string contents)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, contents);
            // Windows에서는 파일 검사기 등이 막 생성된 파일을 잠깐 잡는 동안 교체가
            // IOException으로 실패할 수 있다. 기존 파일을 삭제하는 우회는 하지 않는다.
            // 총 대기 70ms 안에서 같은 원자 교체만 재시도하며, 지속 실패는 그대로 보고한다.
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                    return;
                }
                catch (IOException) when (attempt < 3 && File.Exists(temporary))
                { System.Threading.Thread.Sleep(10 << attempt); }
            }
        }
    }
}
