using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Levels.Editor
{
    internal enum MultiLevelTestMode { Repeat, Balance }
    internal enum MultiLevelTestStatus { Waiting, Running, Paused, Completed, Error, Stopped, Interrupted }

    [Serializable]
    internal sealed class MultiLevelTestEntry
    {
        public string name, assetGuid, definitionJson, resultId, message;
        public MultiLevelTestStatus status;
    }

    [Serializable]
    internal sealed class MultiLevelTestRecord
    {
        public int version = 1, samples = 100;
        public string id, startedUtc;
        public MultiLevelTestMode mode;
        public List<MultiLevelTestEntry> entries = new List<MultiLevelTestEntry>();
    }

    /// <summary>일괄 실행 목록과 개별 원시 기록을 같은 실행 폴더에 보관한다. 원본 레벨은 저장하지 않는다.</summary>
    internal sealed class MultiLevelTestStore
    {
        internal const string DefaultRoot = "Library/Match/MultiLevelTests";
        internal readonly string Root;
        internal MultiLevelTestStore(string root = DefaultRoot) { Root = root; }
        /// <param name="record">고유 실행 ID와 레벨별 진행 상태.</param>
        internal void Save(MultiLevelTestRecord record)
        {
            string folder = Path.Combine(Root, record.id); Directory.CreateDirectory(folder);
            BotBatchStore.WriteAtomic(Path.Combine(folder, "queue.json"), JsonUtility.ToJson(record, true));
            BotBatchStore.WriteAtomic(Path.Combine(Root, "latest.txt"), record.id);
        }
        /// <returns>최근 실행 목록. 이전 프로세스에서 진행 중이던 작업은 자동 재개하지 않는다.</returns>
        internal MultiLevelTestRecord Load()
        {
            string pointer = Path.Combine(Root, "latest.txt");
            if (!File.Exists(pointer)) return null;
            string id = File.ReadAllText(pointer).Trim();
            if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("일괄 시험 ID 오류");
            MultiLevelTestRecord record = JsonUtility.FromJson<MultiLevelTestRecord>(File.ReadAllText(Path.Combine(Root, id, "queue.json")));
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
        /// <param name="record">조회할 실행.</param><param name="index">레벨 목록 순번.</param>
        /// <returns>사용자가 수정한 이름 대신 ID와 순번으로 정하는 원시 기록 폴더.</returns>
        internal string LevelRoot(MultiLevelTestRecord record, int index) => Path.Combine(Root, record.id, index.ToString("D4"));
    }
}
