using System;
using System.IO;
using System.Linq;
using AutoPlay;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class BotAnalysisVerification
    {
        private static void StorageChecks(BotAnalysisReader source)
        {
            string own = Path.GetFullPath(Evidence + "/cases-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(own);
            string exported = Path.Combine(own, "exported");
            using (BotAnalysisExport export = new BotAnalysisExport(source, exported))
            {
                while (!export.IsDone) export.Advance();
                Check(export.Error == null && export.Copied == 200, "200판 보관 내보내기");
            }
            BotAnalysisReader copy = Load(exported);
            Check(copy.Games.Count == 200 && copy.Record.definitionJson == source.Record.definitionJson && copy.Record.seeds.SequenceEqual(source.Record.seeds), "보관본 다시 열기 · 정의·시드·판 수 유지");
            Check(JsonUtility.ToJson(copy.ReadGame(0)) == JsonUtility.ToJson(source.ReadGame(0)), "보관 행동·미션 값 보존");
            bool rejected = false;
            try { using BotAnalysisExport invalid = new BotAnalysisExport(source, exported); } catch (IOException) { rejected = true; }
            Check(rejected, "동명 보관 폴더 덮어쓰기 거절");
            rejected = false;
            try { using BotAnalysisExport invalid = new BotAnalysisExport(source, Path.Combine(source.DirectoryPath, "child")); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "원본 기록 내부 내보내기 거절");
            string cancel = Path.Combine(own, "cancelled");
            using (BotAnalysisExport partial = new BotAnalysisExport(source, cancel)) partial.Advance();
            Check(!Directory.Exists(cancel) && Directory.GetDirectories(own, "*.pending-*").Length == 0, "취소 시 소유 임시 폴더 정리");
            string header = Path.Combine(exported, "batch.json"), before = File.ReadAllText(header);
            Load(exported);
            Check(File.ReadAllText(header) == before, "이력 조회는 요약 파일을 다시 쓰지 않음");
            BotBatchRecord stale = JsonUtility.FromJson<BotBatchRecord>(before);
            stale.finished = stale.won = stale.exhausted = stale.blocked = stale.errors = stale.stopped = stale.basicFinished = stale.planningFinished = 0;
            stale.status = BotBatchStatus.Running;
            string staleJson = JsonUtility.ToJson(stale); File.WriteAllText(header, staleJson);
            BotAnalysisReader recovered = Load(exported);
            Check(recovered.Record.finished == 200 && recovered.Warning != null && File.ReadAllText(header) == staleJson, "판 파일 선저장 복구는 읽기 전용·안내 표시");
            File.WriteAllText(header, before);
            string first = Path.Combine(exported, "000000.json"), firstJson = File.ReadAllText(first);
            File.WriteAllText(first, firstJson + " ");
            rejected = false;
            try { copy.ReadGame(0); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "조회 후 판 파일 변경 감지"); File.WriteAllText(first, firstJson);
            BotBatchGame wrong = JsonUtility.FromJson<BotBatchGame>(firstJson); wrong.seed++;
            File.WriteAllText(first, JsonUtility.ToJson(wrong)); rejected = false;
            try { Load(exported); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "다른 시드 판 기록 거절"); File.WriteAllText(first, firstJson);
            string last = Path.Combine(exported, "000199.json"), lastJson = File.ReadAllText(last); File.Delete(last);
            rejected = false;
            try { Load(exported); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "완료 요약보다 적은 판 파일 거절"); File.WriteAllText(last, lastJson);
            BotBatchRecord invalidHeader = JsonUtility.FromJson<BotBatchRecord>(before); invalidHeader.id = "../../outside";
            File.WriteAllText(header, JsonUtility.ToJson(invalidHeader)); rejected = false;
            try { Load(exported); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "경로처럼 생긴 묶음 ID 거절"); File.WriteAllText(header, before);
            File.WriteAllText(Evidence + "/test-archive-path.txt", exported);
        }

        private static void ReplayEdges(LevelDefinition level, BotAnalysisReader reader)
        {
            BotBatchGame changed = reader.ReadGame(0); changed.actions[0].randomBefore++;
            using (BotRecordReplay replay = new BotRecordReplay(level, reader.Record, changed))
            {
                replay.Begin(true);
                for (int i = 0; replay.NeedsAdvance && i < 50000; i++) replay.Advance();
                Check(replay.Status == BotReplayStatus.Error && replay.ActionIndex == 0, "최초 행동의 난수 불일치에서 중지");
            }
            BotBatchRecord version = JsonUtility.FromJson<BotBatchRecord>(JsonUtility.ToJson(reader.Record)); version.engineVersion = "different";
            Check(BotRecordReplay.CompatibilityError(version) != null, "엔진 버전이 다른 기록 재생 차단");
            version.engineVersion = reader.Record.engineVersion; version.planningVersion = "different-strategy";
            Check(BotRecordReplay.CompatibilityError(version) == null, "기록 행동 재생은 과거 전략을 실행하지 않음");
            foreach (BotSessionStatus outcome in new[] { BotSessionStatus.Stopped, BotSessionStatus.Error })
            {
                BotBatchGame partial = reader.ReadGame(0); partial.outcome = outcome; partial.actions = partial.actions.Take(1).ToArray();
                using BotRecordReplay replay = new BotRecordReplay(level, reader.Record, partial); replay.Begin(false);
                for (int i = 0; replay.NeedsAdvance && i < 50000; i++) replay.Advance();
                Check(replay.Status == BotReplayStatus.Partial && replay.ActionIndex == 1, "완료 행동까지만 부분 재생 " + outcome);
            }
        }
    }
}
