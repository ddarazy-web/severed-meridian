using System;
using System.IO;
using System.Threading;
using AutoPlay;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class BotBatchVerification
    {
        private static void Persisted(LevelDefinition level)
        {
            string source = JsonUtility.ToJson(level);
            string folder = Path.Combine(Evidence, "async-" + Guid.NewGuid().ToString("N"));
            var store = new AutoPlay.BotBatchStore(folder, "independent source context");
            var batch = new PersistedBotBatch(level, new[] { 771 }, store);
            void DrainSaving(PersistedBotBatch value)
            {
                int count = 0;
                while (value.IsSaving && count++ < 10000) { value.Advance(); Thread.Sleep(1); }
                Check(!value.IsSaving, "비동기 체크포인트 완료");
            }
            void Finish(PersistedBotBatch value)
            {
                int count = 0;
                while (value.HasWork && count++ < 50000) { value.Advance(); if (value.IsSaving) Thread.Sleep(1); }
                Check(!value.HasWork, "반복 실행과 저장 작업 모두 종료");
            }
            try
            {
                Check(batch.IsSaving && batch.Current == null && batch.Games.Count == 0, "최초 저장 전 판 실행과 성공 집계 없음");
                DrainSaving(batch);
                Check(batch.Pause(), "저장 완료 후 반복 시험 일시정지"); DrainSaving(batch);
                for (int i = 0; i < 10; i++) batch.Advance();
                Check(batch.Current == null && batch.Record.status == BotBatchStatus.Paused, "일시정지 중 다음 판 생성 안 함");
                Check(batch.Resume(), "반복 시험 같은 상태로 재개"); Finish(batch);
                Check(batch.Record.status == BotBatchStatus.Completed && batch.Games.Count == 2 && batch.PersistenceError == null, "두 전략 저장 확정 후 완료");
                Check(store.LoadLatest(false).finished == 2, "비동기 판 기록 디스크 재조회");
                Check(store.ReadSourceContext(batch.Record.id) == "independent source context", "독립 원본 sidecar가 판 조회와 충돌하지 않음");
                var reader = new BotAnalysisReader(Path.Combine(folder, batch.Record.id), level);
                while (!reader.IsDone) reader.Advance();
                Check(reader.Error == null && reader.Games.Count == 2, "실행용 로더가 복원한 정의로 전체 판 검증");
                string exported = folder + "-export";
                using (var export = new BotAnalysisExport(reader, exported))
                {
                    while (!export.IsDone) export.Advance();
                    Check(export.Error == null, "실행용 이력 새 폴더 보관");
                }
                Check(File.ReadAllText(Path.Combine(exported, "source.context")) == reader.SourceContext, "이력 보관에 독립 제작 원본 포함");
                Check(JsonUtility.ToJson(level) == source, "비동기 반복 실행 원본 불변");
            }
            finally { batch.RequestStop(); Finish(batch); batch.Dispose(); }
            var stopped = new PersistedBotBatch(level, new[] { 771 }, new AutoPlay.BotBatchStore(folder + "-stop"));
            stopped.RequestStop(); Finish(stopped);
            Check(stopped.Record.status == BotBatchStatus.Stopped && stopped.Record.finished == 0, "저장 중 중지 요청은 저장 경계에서 처리"); stopped.Dispose();
            var shutdownStore = new AutoPlay.BotBatchStore(folder + "-shutdown");
            var shutdown = new PersistedBotBatch(level, new[] { 771 }, shutdownStore);
            shutdown.StopAndDisposeAtShutdown();
            Check(shutdownStore.LoadLatest(false).status == BotBatchStatus.Interrupted, "앱 종료는 마지막 저장을 확정하고 중단 상태 보존");
            string invalid = folder + "-file"; File.WriteAllText(invalid, "file instead of directory");
            var failed = new PersistedBotBatch(level, new[] { 771 }, new AutoPlay.BotBatchStore(invalid));
            Finish(failed);
            Check(failed.PersistenceError != null && failed.Record.status == BotBatchStatus.Error && failed.Games.Count == 0,
                "실제 비동기 저장 실패를 완료/성공으로 숨기지 않음"); failed.Dispose();
        }
    }
}
