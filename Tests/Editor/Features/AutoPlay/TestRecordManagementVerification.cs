using AutoPlay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>사용자 기록과 완전히 분리된 임시 루트에서 기록 관리 경계를 검사한다. 게임은 실행하지 않는다.</summary>
    public static class TestRecordManagementVerification
    {
        private static readonly List<string> results = new List<string>();
        private static string root;
        public static void Run()
        {
            root = Path.GetFullPath("Logs/Stage32Verification/owned-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string[] roots = { root + "/repeat", root + "/balance", root + "/multi" };
                string previousId = File.ReadAllText(BotBatchStore.DefaultRoot + "/latest.txt").Trim();
                string readOnlyRoot = root + "/read-only", readOnlyFolder = readOnlyRoot + "/" + previousId;
                Directory.CreateDirectory(readOnlyFolder);
                foreach (string file in Directory.GetFiles(BotBatchStore.DefaultRoot + "/" + previousId)) File.Copy(file, readOnlyFolder + "/" + Path.GetFileName(file));
                File.WriteAllText(readOnlyRoot + "/latest.txt", previousId);
                string preservedJson = File.ReadAllText(readOnlyFolder + "/batch.json");
                DateTime preservedTime = File.GetLastWriteTimeUtc(readOnlyFolder + "/batch.json");
                new BotBatchStore(readOnlyRoot).LoadLatest(false);
                Check(File.ReadAllText(readOnlyFolder + "/batch.json") == preservedJson && File.GetLastWriteTimeUtc(readOnlyFolder + "/batch.json") == preservedTime, "화면용 반복 시험 복원은 원본 내용·수정 시각 보존");
                using (TestRecordCleanup empty = new TestRecordCleanup(roots))
                { Scan(empty); empty.Begin(); Finish(empty); Check(empty.Errors.Count == 0 && empty.Deleted == 0, "세 저장소가 아직 없으면 오류 없이 빈 상태 유지"); }
                string repeat = Guid.NewGuid().ToString("N"), balance = Guid.NewGuid().ToString("N"), multi = Guid.NewGuid().ToString("N");
                Header(roots[0] + "/" + repeat, "batch.json", repeat);
                File.WriteAllText(roots[0] + "/" + repeat + "/000000.json", "{}");
                Header(roots[1] + "/" + balance, "balance.json", balance);
                Header(roots[1] + "/" + balance + "/trials/" + repeat, "batch.json", repeat);
                MultiLevelTestStore store = new MultiLevelTestStore(roots[2]);
                store.Save(new MultiLevelTestRecord { id = multi, startedUtc = "2026-09-29T01:00:00Z", entries = new List<MultiLevelTestEntry> {
                    new MultiLevelTestEntry { name = "완료", status = MultiLevelTestStatus.Completed },
                    new MultiLevelTestEntry { name = "중단", status = MultiLevelTestStatus.Running } } });
                Header(roots[2] + "/" + multi + "/0000/" + repeat, "batch.json", repeat);
                // JSON 시험은 원본 사본을 함께 보관한다. 세 종류의 소유 위치와 중첩 기록을 검사한다.
                foreach (string folder in new[] {
                    roots[0] + "/" + repeat,
                    roots[1] + "/" + balance,
                    roots[1] + "/" + balance + "/trials/" + repeat,
                    roots[2] + "/" + multi + "/0000",
                    roots[2] + "/" + multi + "/0000/" + repeat })
                {
                    File.WriteAllText(folder + "/source.context", "isolated JSON source fixture");
                    File.WriteAllText(folder + "/source.context.tmp", "interrupted atomic write fixture");
                }
                string pointer = File.ReadAllText(roots[2] + "/latest.txt");
                Check(store.Load(multi).entries[1].status == MultiLevelTestStatus.Interrupted, "이전 실행 상태는 중단으로 읽기");
                Check(File.ReadAllText(roots[2] + "/latest.txt") == pointer, "ID 조회는 최근 포인터 보존");
                string bad = Guid.NewGuid().ToString("N"); Directory.CreateDirectory(roots[2] + "/" + bad);
                File.WriteAllText(roots[2] + "/" + bad + "/queue.json", "bad");
                var history = store.Scan().ToList();
                Check(history.Count == 2 && history.Count(e => e.Error != null) == 1, "손상 이력과 정상 이력 분리");
                bool rejected = false;
                try { store.Load("../outside"); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "잘못된 실행 ID 거절");
                rejected = false;
                try { TestRecordPaths.Check(roots[0], root + "/outside"); } catch (IOException) { rejected = true; }
                Check(rejected, "허용 저장소 밖 경로 거절");
                string external = root + "/export.json"; File.WriteAllText(external, "preserve");
                using (TestRecordCleanup cleanup = new TestRecordCleanup(roots))
                {
                    Scan(cleanup); Check(cleanup.Counts.SequenceEqual(new[] { 1, 1, 2 }), "최상위 실행 수만 집계");
                    Check(File.Exists(roots[0] + "/" + repeat + "/batch.json"), "확인 전에는 파일 보존");
                }
                bool prepared = false, locked = false;
                using (TestRecordManagement ui = new TestRecordManagement(() => false, () => prepared = true, () => {}, value => locked = value, roots, _ => false))
                {
                    ui.Request(); for (int i = 0; ui.IsBusy && i < 1000; i++) ui.Tick();
                    Check(!prepared && !locked && File.Exists(roots[0] + "/" + repeat + "/batch.json"), "확인 취소는 파일·캐시 보존 및 잠금 해제");
                }
                using (TestRecordManagement ui = new TestRecordManagement(() => true, () => prepared = true, () => {}, _ => {}, roots, _ => true))
                { ui.Request(); Check(!ui.IsBusy && !prepared, "실행·일시정지·내보내기 상태 접근자 잠금"); }
                using (TestRecordCleanup cleanup = new TestRecordCleanup(roots))
                {
                    Scan(cleanup); cleanup.Begin(); Finish(cleanup);
                    Check(cleanup.Deleted == 4 && cleanup.Failed == 0 && cleanup.Errors.Count == 0, "세 종류·중첩·손상 기록 전체 삭제");
                    Check(!File.Exists(roots[2] + "/latest.txt"), "빈 목록 최근 포인터 제거");
                    Check(File.ReadAllText(external) == "preserve", "외부 내보낸 파일 보존");
                }
                Header(roots[0] + "/" + repeat, "batch.json", repeat);
                string held = roots[0] + "/" + repeat + "/000000.json"; File.WriteAllText(held, "{}");
                using (FileStream file = new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.None))
                using (TestRecordCleanup cleanup = new TestRecordCleanup(roots))
                {
                    Scan(cleanup); cleanup.Begin(); Finish(cleanup);
                    Check(cleanup.Failed == 1 && cleanup.Errors.Count > 0, "잠긴 파일의 부분 실패 보고");
                    Check(File.Exists(roots[0] + "/" + repeat + "/batch.json"), "하위 실패 시 실행 요약 보존");
                    Check(File.ReadAllText(roots[0] + "/latest.txt") == repeat, "남은 실행으로 최근 포인터 복구");
                }
                File.WriteAllText(roots[0] + "/" + repeat + "/unknown.txt", "preserve");
                using (TestRecordCleanup cleanup = new TestRecordCleanup(roots))
                {
                    Scan(cleanup); cleanup.Begin(); Finish(cleanup);
                    Check(cleanup.Failed == 1 && File.Exists(roots[0] + "/" + repeat + "/unknown.txt"), "알 수 없는 파일 보존");
                }
                for (int i = 0; i < 300; i++) Header(roots[1] + "/" + Guid.NewGuid().ToString("N"), "balance.json", null);
                using (TestRecordCleanup cleanup = new TestRecordCleanup(roots))
                {
                    cleanup.Advance(); Check(cleanup.Scanning, "많은 실행 목록을 갱신 단위로 나눔");
                    Scan(cleanup); cleanup.Begin(); cleanup.Advance(); Check(!cleanup.IsDone, "많은 기록 삭제를 갱신 단위로 나눔");
                    Finish(cleanup); Check(cleanup.Deleted == 300, "많은 요약 기록 삭제 완료");
                }
                string[] linkRoots = File.ReadAllLines("Logs/Stage32Verification/link-roots.txt");
                using (TestRecordCleanup cleanup = new TestRecordCleanup(linkRoots.Take(3).ToArray()))
                {
                    Scan(cleanup); cleanup.Begin(); Finish(cleanup);
                    Check(cleanup.Failed == 1 && File.ReadAllText(linkRoots[3]) == "preserve", "실제 정션 경유 삭제 거절·외부 파일 보존");
                }
                rejected = false;
                try { TestRecordPaths.Check(linkRoots[4], linkRoots[4] + "/sentinel.txt"); } catch (IOException) { rejected = true; }
                Check(rejected, "저장 루트 자체가 정션일 때도 거절");
            }
            catch (Exception error) { results.Add("FAIL " + error); }
            File.WriteAllLines("Logs/Stage32Verification/storage-results.txt", results);
            File.WriteAllText("Logs/Stage32Verification/owned-root.txt", root);
            EditorApplication.Exit(results.Any(r => r.StartsWith("FAIL")) ? 1 : 0);
        }
        private static void Header(string folder, string name, string id)
        {
            Directory.CreateDirectory(folder); id ??= Path.GetFileName(folder);
            File.WriteAllText(folder + "/" + name, "{\"id\":\"" + id + "\",\"formatVersion\":1,\"startedUtc\":\"2026-09-29T00:00:00Z\"}");
        }
        private static void Scan(TestRecordCleanup cleanup) { for (int i = 0; cleanup.Scanning && i < 100000; i++) cleanup.Advance(); }
        private static void Finish(TestRecordCleanup cleanup) { for (int i = 0; !cleanup.IsDone && i < 100000; i++) cleanup.Advance(); Check(cleanup.IsDone, "삭제 작업 종료"); }
        private static void Check(bool valid, string name) { results.Add((valid ? "PASS " : "FAIL ") + name); }
    }
}
