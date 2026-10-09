using System;
using System.IO;
using System.Linq;
using System.Threading;
using AutoPlay;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class MultiLevelVerification
    {
        private static void VerifyPersistence()
        {
            string root = "Logs/MultiLevelVerification/async-" + Guid.NewGuid().ToString("N");
            var storage = new MultiLevelTestStore(root);
            BotTrialSourceContext Context(LevelDefinition level) => new BotTrialSourceContext("document-" + owned.IndexOf(level),
                "source-" + owned.IndexOf(level), moves => "moves-" + moves);
            var run = new PersistedMultiLevelTest(owned, MultiLevelTestMode.Repeat, 1, root, Context);
            void Drain(PersistedMultiLevelTest value, bool savingOnly = false)
            {
                var timeout = DateTime.UtcNow.AddSeconds(60);
                while (savingOnly ? value.IsSaving : value.HasWork)
                {
                    if (DateTime.UtcNow > timeout) throw new TimeoutException("여러 레벨 저장/실행 시간 초과");
                    value.Advance();
                    if (value.IsSaving) Thread.Sleep(1);
                }
            }
            try
            {
                Check(run.IsSaving && run.Current == null && run.Index == 0, "목록 최초 저장 확정 전 실행 안 함");
                Drain(run, true);
                Check(run.Pause(), "여러 레벨 저장 후 일시정지"); Drain(run, true);
                for (int i = 0; i < 10; i++) run.Advance();
                Check(run.Paused && run.Current == null && !run.HasWork, "일시정지는 새 판을 만들지 않음");
                Check(run.Resume(), "여러 레벨 저장 후 재개");
                Drain(run);
                Check(run.PersistenceError == null && !run.IsSaving && run.Index == 3, "모든 저장 확정 후 여러 레벨 종료");
                var restored = storage.Load();
                Check(restored.entries.Select(e => e.sourceDocumentId).SequenceEqual(new[] { "document-0", "document-1", "document-2" }) &&
                    restored.entries.All(e => string.IsNullOrEmpty(e.assetGuid)), "독립 문서 ID는 에셋 GUID와 구분");
                Check(restored.entries[0].status == MultiLevelTestStatus.Completed &&
                    restored.entries[1].status == MultiLevelTestStatus.Error &&
                    restored.entries[2].status == MultiLevelTestStatus.Completed, "비동기 실행 오류 레벨 격리 및 최종 목록 저장");
                foreach (int index in new[] { 0, 2 })
                {
                    var reader = new BotAnalysisReader(Path.Combine(storage.LevelRoot(restored, index), restored.entries[index].resultId));
                    while (!reader.IsDone) reader.Advance();
                    Check(reader.Error == null && reader.Games.Count == 2, "목록 완료 시 개별 판 저장 보장 " + index);
                    Check(reader.SourceContext == "source-" + index && File.ReadAllText(Path.Combine(storage.LevelRoot(restored, index), "source.context")) == reader.SourceContext,
                        "목록과 개별 반복 시험에 같은 독립 원본 보관 " + index);
                }
                Check(JsonUtility.ToJson(owned[0]) == original, "비동기 여러 레벨 시험 원본 불변");
            }
            finally { run.StopAndDisposeAtShutdown(); }

            var stopped = new PersistedMultiLevelTest(owned, MultiLevelTestMode.Repeat, 1, root + "-stop");
            try
            {
                stopped.RequestStop(); Drain(stopped);
                Check(stopped.Current == null && new MultiLevelTestStore(root + "-stop").Load().entries.All(e =>
                    e.status == MultiLevelTestStatus.Stopped && string.IsNullOrEmpty(e.resultId)), "최초 저장 중 중지는 게임 생성 없이 확정");
            }
            finally { stopped.StopAndDisposeAtShutdown(); }

            var shutdown = new PersistedMultiLevelTest(owned, MultiLevelTestMode.Balance, 100, root + "-shutdown", Context);
            try
            {
                Drain(shutdown, true); shutdown.Advance(); Drain(shutdown, true);
                Check(!string.IsNullOrEmpty(shutdown.Record.entries[0].resultId), "추천 시험 중첩 저장 생성");
                shutdown.Advance();
                Check(shutdown.IsSaving && shutdown.Current == null, "추천 시험 내부 최초 저장 전 게임 생성 차단");
            }
            finally { shutdown.StopAndDisposeAtShutdown(); }
            Check(new MultiLevelTestStore(root + "-shutdown").Load().entries.All(e => e.status == MultiLevelTestStatus.Interrupted),
                "앱 종료는 추천 시험 및 대기 목록 중단 저장");
            string balanceRoot = new MultiLevelTestStore(root + "-shutdown").LevelRoot(shutdown.Record, 0);
            string balanceDirectory = Path.Combine(balanceRoot, shutdown.Record.entries[0].resultId);
            Check(File.ReadAllText(Path.Combine(balanceDirectory, "source.context")) == "source-0", "추천 시험 대표 원본 보관");
            string trialDirectory = Directory.GetDirectories(Path.Combine(balanceDirectory, "trials")).Single();
            Check(File.ReadAllText(Path.Combine(trialDirectory, "source.context")) == "moves-1", "추천 시험 이동 횟수별 원본 보관");
            var balanceReader = new BotMoveBalanceStore(balanceRoot).OpenReader(owned[0]);
            Check(balanceReader.IsDone && balanceReader.Error == null && balanceReader.Record.status == BotBatchStatus.Interrupted,
                "호출자가 복원한 원본으로 중단 추천 기록 조회");

            string invalid = root + "-file"; File.WriteAllText(invalid, "directory unavailable");
            var failed = new PersistedMultiLevelTest(owned, MultiLevelTestMode.Repeat, 1, invalid);
            try
            {
                Drain(failed);
                Check(failed.PersistenceError != null && failed.Index == 0 && failed.Current == null &&
                    failed.Record.entries[0].status == MultiLevelTestStatus.Error &&
                    failed.Record.entries.Skip(1).All(e => e.status == MultiLevelTestStatus.Interrupted), "실제 저장 오류는 다음 레벨과 거짓 완료를 차단");
            }
            finally { failed.StopAndDisposeAtShutdown(); }
        }
    }
}
