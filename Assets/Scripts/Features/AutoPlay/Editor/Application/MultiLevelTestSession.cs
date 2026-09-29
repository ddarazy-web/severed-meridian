using System;
using System.Collections.Generic;
using System.Linq;
using AutoPlay;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>한 번에 레벨 하나만 기존 실행기로 처리한다. 레벨별 오류는 보존하고 다음 레벨로 넘어간다.</summary>
    internal sealed class MultiLevelTestSession : IDisposable
    {
        internal MultiLevelTestRecord Record { get; }
        internal bool CanContinue { get; private set; } = true;
        internal bool Paused { get; private set; }
        internal string Error { get; private set; }
        internal int Index { get; private set; }
        private readonly MultiLevelTestStore store;
        private BotBatchSession repeat;
        private BotMoveBalanceSession balance;

        /// <param name="levels">선택된 저장 에셋. 시작 순간의 내용을 복사해 이후 편집과 분리한다.</param>
        /// <param name="mode">현재 횟수 반복 또는 1~100회 추천 시험.</param>
        /// <param name="samples">반복 시험의 전략별 판 수. 추천 시험은 기존 100판을 유지한다.</param>
        /// <param name="store">목록과 원시 기록 보관소.</param>
        internal MultiLevelTestSession(IEnumerable<LevelDefinition> levels, MultiLevelTestMode mode, int samples, MultiLevelTestStore store)
        {
            if (samples < 1 || samples > BotBatchSession.MaximumCount) throw new ArgumentOutOfRangeException(nameof(samples));
            this.store = store;
            Record = new MultiLevelTestRecord { id = Guid.NewGuid().ToString("N"), startedUtc = DateTime.UtcNow.ToString("O"), mode = mode, samples = samples };
            foreach (LevelDefinition level in levels.Where(l => l != null).Distinct())
                Record.entries.Add(new MultiLevelTestEntry { name = level.name, assetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level)),
                    definitionJson = JsonUtility.ToJson(level), status = MultiLevelTestStatus.Waiting, message = "대기" });
            if (Record.entries.Count == 0) throw new ArgumentException("시험할 레벨을 선택하세요.");
            store.Save(Record);
        }

        /// <summary>작은 실행 단위 하나를 진행한다. 보관소 자체 오류는 전체 실행을 중지한다.</summary>
        internal void Advance()
        {
            if (!CanContinue || Paused) return;
            MultiLevelTestEntry entry = Record.entries[Index];
            try
            {
                if (repeat == null && balance == null)
                {
                    LevelDefinition copy = ScriptableObject.CreateInstance<LevelDefinition>(); copy.hideFlags = HideFlags.HideAndDontSave;
                    try
                    {
                        JsonUtility.FromJsonOverwrite(entry.definitionJson, copy); copy.name = entry.name;
                        string root = store.LevelRoot(Record, Index);
                        if (Record.mode == MultiLevelTestMode.Balance)
                        { balance = new BotMoveBalanceSession(copy, new BotMoveBalanceStore(root)); entry.resultId = balance.Record.id; }
                        else
                        {
                            BotBatchStore batchStore = new BotBatchStore(root);
                            repeat = new BotBatchSession(copy, BotBatchSession.NewSeeds(Record.samples), (record, game) => {
                                record.sourceName = entry.name; batchStore.Save(record, game);
                            }); entry.resultId = repeat.Record.id;
                        }
                        entry.status = MultiLevelTestStatus.Running; Save();
                    }
                    finally { UnityEngine.Object.DestroyImmediate(copy); }
                    return;
                }
                balance?.Advance(); repeat?.Advance();
                BotBatchStatus status = balance?.Record.status ?? repeat.Record.status;
                entry.message = balance != null ? balance.Record.message : $"{repeat.Record.finished}/{repeat.Record.Total}판 · {repeat.Record.message}";
                if (status == BotBatchStatus.Running || status == BotBatchStatus.Paused) return;
                entry.status = status == BotBatchStatus.Completed ? MultiLevelTestStatus.Completed : MultiLevelTestStatus.Error;
            }
            catch (Exception error) { entry.status = MultiLevelTestStatus.Error; entry.message = error.Message; }
            balance?.Dispose(); balance = null; repeat?.Dispose(); repeat = null;
            Index++; if (Index == Record.entries.Count) CanContinue = false;
            Save();
        }

        /// <param name="paused">같은 실행 객체를 멈추거나 재개할지 여부.</param>
        internal void SetPaused(bool paused)
        {
            if (!CanContinue) return;
            Paused = paused; balance?.SetPaused(paused);
            if (paused) repeat?.Pause(); else repeat?.Resume();
            Record.entries[Index].status = paused ? MultiLevelTestStatus.Paused : MultiLevelTestStatus.Running;
            Save();
        }
        /// <param name="interrupted">창 종료·재컴파일이면 true.</param>
        internal void Stop(bool interrupted = false)
        {
            if (!CanContinue) return;
            balance?.Stop(interrupted); repeat?.Stop(interrupted);
            foreach (MultiLevelTestEntry entry in Record.entries.Skip(Index))
            { entry.status = interrupted ? MultiLevelTestStatus.Interrupted : MultiLevelTestStatus.Stopped; entry.message = "완료된 판은 보존 · 남은 실행 중단"; }
            CanContinue = false; Save();
            balance?.Dispose(); balance = null; repeat?.Dispose(); repeat = null;
        }
        /// <summary>요약 저장 실패 후 자동 진행하지 않아 화면과 디스크의 상태가 더 벌어지지 않게 한다.</summary>
        private void Save()
        {
            try { store.Save(Record); }
            catch (Exception error) { Error = "일괄 기록 저장 실패: " + error.Message; CanContinue = false; }
        }
        public void Dispose()
        { Stop(true); balance?.Dispose(); repeat?.Dispose(); balance = null; repeat = null; }
    }
}
