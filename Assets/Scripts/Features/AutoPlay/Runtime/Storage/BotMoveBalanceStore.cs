using Levels;
using System;
using System.IO;
using System.Linq;
using AutoPlay;
using UnityEngine;

namespace AutoPlay
{
    /// <summary>이동 횟수 시험 요약과 실제 판별 기록을 기존 반복 시험 이력과 분리하여 보존한다.</summary>
    public sealed class BotMoveBalanceStore
    {
        public const string DefaultRoot = "Library/Match/MoveBalance";
        public readonly string Root;
        internal Action<Action> Persist { get; }
        internal BotTrialSourceContext Source { get; }
        public BotMoveBalanceStore(string root = DefaultRoot, Action<Action> persist = null, BotTrialSourceContext source = null)
        { Root = root; Persist = persist; Source = source; }

        /// <param name="record">시작 조건 및 완료된 횟수별 결과.</param>
        public void Save(BotMoveBalanceRecord record)
        {
            string id = record.id, json = JsonUtility.ToJson(record, true);
            Action write = () => {
            string directory = Path.Combine(Root, id);
            Directory.CreateDirectory(directory);
            string context = Path.Combine(directory, "source.context");
            if (Source != null && !File.Exists(context)) BotBatchStore.WriteAtomic(context, Source.Content);
            // 실제 Windows 검사에서 완료 직후 일시정지 저장이 파일의 일시 잠금과 충돌했다.
            // 기존 판 기록과 동일한 제한 재시도를 재사용하고, 확정 파일을 먼저 삭제하지 않는다.
            BotBatchStore.WriteAtomic(Path.Combine(directory, "balance.json"), json);
            string pointer = Path.Combine(Root, "latest.txt");
            if (!File.Exists(pointer) || File.ReadAllText(pointer) != id) BotBatchStore.WriteAtomic(pointer, id);
            };
            if (Persist == null) write(); else Persist(write);
        }

        /// <summary>검사·동기 호출용 조회. 화면은 OpenReader로 나누어 읽어 메인 스레드를 오래 점유하지 않는다.</summary>
        /// <returns>원시 판까지 대조한 직전 결과. 실행 객체는 복원하지 않는다.</returns>
        public BotMoveBalanceRecord Load()
        {
            BotMoveBalanceReader reader = OpenReader();
            while (!reader.IsDone) reader.Advance();
            if (reader.Error != null) throw new InvalidDataException(reader.Error);
            return reader.Record;
        }

        /// <returns>UI 갱신마다 한 판씩 검증할 읽기 객체. 원본 기록을 고치지 않는다.</returns>
        public BotMoveBalanceReader OpenReader(LevelDefinition restoredDefinition = null) => new BotMoveBalanceReader(this, restoredDefinition);

        /// <returns>형식과 대표 정의만 확인한 요약. 추천 표시는 원시 판 검증 완료 후에 허용한다.</returns>
        public BotMoveBalanceRecord LoadSummary(LevelDefinition restoredDefinition = null)
        {
            string pointer = Path.Combine(Root, "latest.txt");
            if (!File.Exists(pointer)) return null;
            string id = File.ReadAllText(pointer);
            if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("밸런스 시험 식별자가 올바르지 않습니다.");
            BotMoveBalanceRecord record = JsonUtility.FromJson<BotMoveBalanceRecord>(File.ReadAllText(Path.Combine(Root, id, "balance.json")));
            if (record == null || record.id != id || record.formatVersion != 1 || record.trials == null ||
                string.IsNullOrEmpty(record.definitionJson) || string.IsNullOrEmpty(record.engineVersion) || string.IsNullOrEmpty(record.rulesVersion) ||
                record.seeds?.Length != BotMoveRecommendations.Samples || record.seeds.Distinct().Count() != record.seeds.Length ||
                !Enum.IsDefined(typeof(BotBatchStatus), record.status) || record.trials.Count > 100 ||
                record.trials.Where((t, i) => t == null || t.moves != i + 1 || t.grade < -1 || t.grade > 3 ||
                    !Guid.TryParseExact(t.batchId, "N", out _) || t.normalPerStrategy != 100 || t.basicWon < 0 || t.basicWon > 100 || t.planningWon < 0 || t.planningWon > 100).Any() ||
                record.trials.Select(t => t.batchId).Distinct().Count() != record.trials.Count ||
                record.status == BotBatchStatus.Completed && record.trials.Count != 100)
                throw new InvalidDataException("밸런스 시험 요약이 손상되었습니다.");
            LevelDefinition definition = restoredDefinition ?? ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                if (restoredDefinition == null) JsonUtility.FromJsonOverwrite(record.definitionJson, definition);
                if (Simulation.LevelStateBuilder.Fingerprint(definition) != record.fingerprint)
                    throw new InvalidDataException("밸런스 대표 정의와 지문이 다릅니다.");
            }
            finally { if (restoredDefinition == null) UnityEngine.Object.DestroyImmediate(definition); }
            if (record.status == BotBatchStatus.Running || record.status == BotBatchStatus.Paused)
            { record.status = BotBatchStatus.Interrupted; record.message = "에디터 종료로 중단 · 완료한 이동 횟수만 표시"; }
            return record;
        }
    }
}
