using Levels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AutoPlay
{
    public sealed class MultiLevelHistoryEntry
    {
        public string Id, Error;
        public MultiLevelTestRecord Record;
    }
    public enum MultiLevelTestMode { Repeat, Balance }
    public enum MultiLevelTestStatus { Waiting, Running, Paused, Completed, Error, Stopped, Interrupted }

    [Serializable]
    public sealed class MultiLevelTestEntry
    {
        public string name, assetGuid, sourceDocumentId, definitionJson, resultId, message;
        public MultiLevelTestStatus status;
    }

    [Serializable]
    public sealed class MultiLevelTestRecord
    {
        public int version = 1, samples = 100;
        public string id, startedUtc;
        public MultiLevelTestMode mode;
        public List<MultiLevelTestEntry> entries = new List<MultiLevelTestEntry>();
    }

    /// <summary>일괄 실행 목록과 개별 원시 기록을 같은 실행 폴더에 보관한다. 원본 레벨은 저장하지 않는다.</summary>
    public sealed class MultiLevelTestStore
    {
        public const string DefaultRoot = "Library/Match/MultiLevelTests";
        public readonly string Root;
        internal Action<Action> Persist { get; }
        public MultiLevelTestStore(string root = DefaultRoot, Action<Action> persist = null) { Root = root; Persist = persist; }
        public void SaveSource(MultiLevelTestRecord record, int index, string content)
        {
            string folder = LevelRoot(record, index);
            Action write = () => {
                Directory.CreateDirectory(folder);
                BotBatchStore.WriteAtomic(Path.Combine(folder, "source.context"), content);
            };
            if (Persist == null) write(); else Persist(write);
        }
        /// <param name="record">고유 실행 ID와 레벨별 진행 상태.</param>
        public void Save(MultiLevelTestRecord record)
        {
            string id = record.id, json = JsonUtility.ToJson(record, true);
            Action write = () => {
                string folder = Path.Combine(Root, id); Directory.CreateDirectory(folder);
                BotBatchStore.WriteAtomic(Path.Combine(folder, "queue.json"), json);
                BotBatchStore.WriteAtomic(Path.Combine(Root, "latest.txt"), id);
            };
            if (Persist == null) write(); else Persist(write);
        }
        /// <returns>최근 실행 목록. 이전 프로세스에서 진행 중이던 작업은 자동 재개하지 않는다.</returns>
        public MultiLevelTestRecord Load()
        {
            string pointer = Path.Combine(Root, "latest.txt");
            TestRecordPaths.Check(Root, pointer);
            if (!File.Exists(pointer)) return null;
            string id = File.ReadAllText(pointer).Trim();
            return Load(id);
        }

        /// <param name="id">목록에서 선택한 실행 ID. 최근 실행 포인터는 바꾸지 않는다.</param>
        /// <returns>저장된 실행 사본. 종료된 프로세스의 작업은 중단 상태로 읽는다.</returns>
        public MultiLevelTestRecord Load(string id)
        {
            if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("일괄 시험 ID 오류");
            string path = Path.Combine(Root, id, "queue.json");
            TestRecordPaths.Check(Root, path);
            MultiLevelTestRecord record = JsonUtility.FromJson<MultiLevelTestRecord>(File.ReadAllText(path));
            if (record == null || record.version != 1 || record.id != id || record.entries == null ||
                !Enum.IsDefined(typeof(MultiLevelTestMode), record.mode) || record.samples < 1 || record.samples > 10000 ||
                record.entries.Any(e => e == null || !Enum.IsDefined(typeof(MultiLevelTestStatus), e.status) ||
                    (!string.IsNullOrEmpty(e.resultId) && !Guid.TryParseExact(e.resultId, "N", out _))))
                throw new InvalidDataException("일괄 시험 기록이 손상되었습니다.");
            foreach (MultiLevelTestEntry entry in record.entries)
                if (entry.status == MultiLevelTestStatus.Running || entry.status == MultiLevelTestStatus.Paused || entry.status == MultiLevelTestStatus.Waiting)
                { entry.status = MultiLevelTestStatus.Interrupted; entry.message = "창 종료로 중단 · 새 시험으로 다시 시작하세요."; }
            return record;
        }

        /// <returns>원시 판을 열지 않고 실행 요약만 하나씩 읽는 목록. 손상 항목도 별도로 표시한다.</returns>
        public IEnumerable<MultiLevelHistoryEntry> Scan()
        {
            TestRecordPaths.Check(Root, Root);
            if (!Directory.Exists(Root)) yield break;
            foreach (string directory in Directory.EnumerateDirectories(Root))
            {
                string id = Path.GetFileName(directory);
                if (!Guid.TryParseExact(id, "N", out _)) continue;
                MultiLevelHistoryEntry entry = new MultiLevelHistoryEntry { Id = id };
                try { entry.Record = Load(id); }
                catch (Exception error) { entry.Error = error.Message; }
                yield return entry;
            }
        }
        /// <param name="record">조회할 실행.</param><param name="index">레벨 목록 순번.</param>
        /// <returns>사용자가 수정한 이름 대신 ID와 순번으로 정하는 원시 기록 폴더.</returns>
        public string LevelRoot(MultiLevelTestRecord record, int index) => Path.Combine(Root, record.id, index.ToString("D4"));
    }
}
