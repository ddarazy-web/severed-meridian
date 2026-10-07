using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoPlay;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>전체 시험 중에는 Assets 밖에 준비하고, 종료 후 Editor/Tests로 옮겨 실행하는 저장 경계 검사.</summary>
    public static class Stage31StorageVerification
    {
        private const string Evidence = "Logs/Stage31Workflow";
        private static readonly List<string> Results = new List<string>();
        private static string sourceDirectory;
        private static BotMoveBalanceRecord sourceRecord;

        public static void Run()
        {
            try
            {
                string run = File.ReadAllText(Evidence + "/full-run-path.txt");
                string root = Path.Combine(run, "records");
                string id = File.ReadAllText(Path.Combine(root, "latest.txt"));
                sourceDirectory = Path.Combine(root, id);
                sourceRecord = JsonUtility.FromJson<BotMoveBalanceRecord>(File.ReadAllText(Path.Combine(sourceDirectory, "balance.json")));
                if (sourceRecord.trials.Count == 0) throw new InvalidOperationException("실제 완료된 횟수의 기록이 필요합니다.");
                Case valid = Fixture();
                string before = File.ReadAllText(valid.header);
                BotMoveBalanceRecord loaded = valid.store.Load();
                Check(loaded.trials.Count == 1 && loaded.trials[0].basicWon == sourceRecord.trials[0].basicWon, "실제 200판 사본의 정상 재조회");
                Check(File.ReadAllText(valid.header) == before, "조회는 원시 요약 파일을 수정하지 않음");
                Rejected("요약 파일 누락", item => File.Delete(item.header));
                Rejected("잘못된 JSON", item => File.WriteAllText(item.header, "{broken"));
                Rejected("정의 JSON과 지문 불일치", item => {
                    item.record.definitionJson = item.record.definitionJson.Replace("\"moveCount\":20", "\"moveCount\":21"); item.store.Save(item.record);
                });
                Rejected("요약의 성공 수 변조", item => { item.record.trials[0].basicWon = (item.record.trials[0].basicWon + 1) % 101; item.store.Save(item.record); });
                Rejected("요약의 등급 변조", item => { item.record.trials[0].grade = (item.record.trials[0].grade + 1) % 4; item.store.Save(item.record); });
                Rejected("완료한 판 파일 누락", item => File.Delete(Path.Combine(item.batch, "000000.json")));
                Rejected("완료한 판 파일 손상", item => File.WriteAllText(Path.Combine(item.batch, "000000.json"), "{broken"));
                Rejected("묶음 요약 누락", item => File.Delete(Path.Combine(item.batch, "batch.json")));
                Rejected("중복 시드", item => { item.record.seeds[1] = item.record.seeds[0]; item.store.Save(item.record); });
                Rejected("존재하지 않는 묶음 ID", item => { item.record.trials[0].batchId = Guid.NewGuid().ToString("N"); item.store.Save(item.record); });

                // ACL을 바꾸지 않고 전용 사본의 확정 파일만 독점 잠근다.
                // 제한 재시도가 소진된 뒤에도 기존 요약 바이트가 남아 있어야 한다.
                Case locked = Fixture();
                byte[] lockedBefore = File.ReadAllBytes(locked.header);
                bool writeRejected = false;
                using (FileStream hold = new FileStream(locked.header, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    locked.record.message = "저장 실패 검사";
                    try { locked.store.Save(locked.record); }
                    catch (IOException) { writeRejected = true; }
                }
                Check(writeRejected, "지속 파일 잠금의 저장 실패를 호출자에게 전달");
                Check(File.ReadAllBytes(locked.header).SequenceEqual(lockedBefore), "저장 실패 후 기존 확정 요약 보존");

                // 실제 세션 제어의 저장 실패 상태 전이를 검사한다. 판을 진행하지 않으므로
                // 이 검사를 실제 자동 플레이 수에 더하지 않는다.
                LevelDefinition definition = ScriptableObject.CreateInstance<LevelDefinition>();
                try
                {
                    JsonUtility.FromJsonOverwrite(sourceRecord.definitionJson, definition);
                    BotMoveBalanceStore failureStore = new BotMoveBalanceStore(Evidence + "/control-failure-" + Guid.NewGuid().ToString("N"));
                    using (BotMoveBalanceSession session = new BotMoveBalanceSession(definition, failureStore))
                    {
                        string header = Path.Combine(failureStore.Root, session.Record.id, "balance.json");
                        byte[] beforeControl = File.ReadAllBytes(header);
                        using (FileStream hold = new FileStream(header, FileMode.Open, FileAccess.Read, FileShare.None))
                            session.SetPaused(true);
                        Check(session.Record.status == BotBatchStatus.Error && !session.CanContinue &&
                            session.Record.message.Contains("저장 실패"), "제어 저장 실패는 오류 상태·실행 중지로 표시");
                        Check(File.ReadAllBytes(header).SequenceEqual(beforeControl), "제어 저장 실패 후 기존 기록 바이트 보존");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(definition); }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); }
            File.WriteAllLines(Evidence + "/storage-results.txt", Results);
            EditorApplication.Exit(Results.Any(r => r.StartsWith("FAIL ")) ? 1 : 0);
        }

        private sealed class Case
        {
            internal BotMoveBalanceStore store;
            internal BotMoveBalanceRecord record;
            internal string header, batch;
        }

        /// <summary>완료한 첫 200판만 전용 새 디렉터리로 복사한다. 실행 중 원본에는 쓰지 않는다.</summary>
        private static Case Fixture()
        {
            BotMoveBalanceRecord record = JsonUtility.FromJson<BotMoveBalanceRecord>(JsonUtility.ToJson(sourceRecord));
            record.trials = record.trials.Take(1).ToList(); record.status = BotBatchStatus.Stopped;
            BotMoveBalanceStore store = new BotMoveBalanceStore(Evidence + "/storage-case-" + Guid.NewGuid().ToString("N"));
            string directory = Path.Combine(store.Root, record.id);
            string batch = Path.Combine(directory, "trials", record.trials[0].batchId); Directory.CreateDirectory(batch);
            foreach (string path in Directory.GetFiles(Path.Combine(sourceDirectory, "trials", record.trials[0].batchId)))
                File.Copy(path, Path.Combine(batch, Path.GetFileName(path)));
            store.Save(record);
            return new Case { store = store, record = record, header = Path.Combine(directory, "balance.json"), batch = batch };
        }

        /// <param name="name">손상 시나리오.</param><param name="damage">새 사본에만 적용할 오류 주입.</param>
        private static void Rejected(string name, Action<Case> damage)
        {
            Case item = Fixture(); damage(item); bool rejected = false;
            try { item.store.Load(); }
            catch (Exception error) when (error is InvalidDataException || error is IOException || error is ArgumentException || error is UnityException)
            { rejected = true; }
            if (rejected) Results.Add("PASS " + name + " 거부"); else Results.Add("FAIL " + name + "를 정상 요약으로 수락");
        }

        private static void Check(bool pass, string message)
        {
            if (!pass) throw new InvalidOperationException(message);
            Results.Add("PASS " + message);
        }
    }
}
