using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Board;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>반복 관리자가 한 판 규칙을 바꾸지 않고 제어 경계와 기록을 유지하는지 검사한다.</summary>
    public static partial class BotBatchVerification
    {
        private const string Evidence = "Logs/BotBatchVerification";
        private static readonly List<string> Results = new List<string>();

        public static void Core()
        {
            LevelDefinition level = null;
            Exception failure = null;
            Results.Clear(); Directory.CreateDirectory(Evidence);
            try
            {
                Check(typeof(BotBatchSession).Assembly.GetType("AutoPlay.BotAnalysisReader")?.IsPublic == true, "실행용 결과 분석 로더 제공");
                Type runtimeStore = typeof(BotBatchSession).Assembly.GetType("AutoPlay.BotBatchStore");
                Check(runtimeStore != null && runtimeStore.IsPublic, "실행용 반복 시험 기록 저장소 제공");
                Check(typeof(BotBatchSession).Assembly.GetType("AutoPlay.PersistedBotBatch") != null, "비동기 저장을 기다리는 반복 시험 소유자 제공");
                BoardCoordinate[] active = Enumerable.Range(0, 16).Select(i => new BoardCoordinate(i / 4, i % 4)).ToArray();
                level = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { active });
                JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[],\"moveCount\":3,\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
                string original = JsonUtility.ToJson(level); bool dirty = EditorUtility.IsDirty(level);
                int[] seeds = BotBatchSession.NewSeeds(100);
                Check(seeds.Length == 100 && seeds.Distinct().Count() == 100, "기본 묶음 100개 시드 중복 없음");
                Check(!BotBatchSession.NewSeeds(100, seeds).SequenceEqual(seeds), "새 시험은 직전 시드 묶음과 다름");
                foreach (int invalid in new[] { 0, -1, BotBatchSession.MaximumCount + 1 })
                {
                    bool rejected = false;
                    try { BotBatchSession.NewSeeds(invalid); } catch (ArgumentOutOfRangeException) { rejected = true; }
                    Check(rejected, "잘못된 횟수 거절 " + invalid);
                }
                bool duplicateRejected = false;
                try { using BotBatchSession invalid = new BotBatchSession(level, new[] { 1, 1 }, (_, _) => { }); }
                catch (ArgumentException) { duplicateRejected = true; }
                Check(duplicateRejected, "중복 시드 실행 거절");

                List<string> uninterrupted = new List<string>(), paused = new List<string>();
                for (int mode = 0; mode < 2; mode++)
                {
                    List<string> target = mode == 0 ? uninterrupted : paused;
                    using BotBatchSession batch = new BotBatchSession(level, new[] { 771, 772 }, (_, game) => {
                        if (game != null) { game.batchId = "comparison"; target.Add(JsonUtility.ToJson(game)); }
                    });
                    int units = 0, pauses = 0;
                    while (batch.NeedsAdvance && units++ < 100000)
                    {
                        batch.Advance();
                        if (mode == 1 && batch.NeedsAdvance && units % 31 == 1)
                        {
                            BotPlaySession current = batch.Current;
                            int draw = current?.State?.Random.DrawCount ?? -1;
                            int turns = current?.Records.Count ?? -1;
                            if (!batch.Pause()) throw new InvalidOperationException("일시정지 실패");
                            for (int i = 0; i < 7; i++) batch.Advance();
                            if (batch.Current != current || (current?.State?.Random.DrawCount ?? -1) != draw ||
                                (current?.Records.Count ?? -1) != turns) throw new InvalidOperationException("일시정지 중 상태 변경");
                            if (!batch.Resume()) throw new InvalidOperationException("재개 실패");
                            pauses++;
                        }
                    }
                    Check(batch.Record.status == BotBatchStatus.Completed && batch.Record.finished == 4 && batch.Record.Unrun == 0,
                        "두 시드×두 전략 정상 완료 " + mode + ": " + batch.Record.message);
                    Check(batch.Record.basicFinished == 2 && batch.Record.planningFinished == 2 && batch.Current == null,
                        "전략별 집계와 최종 실행 객체 해제 " + mode);
                    Check(target.Count == 4 && (mode == 0 || pauses > 20), "판별 1회 기록 및 반복 일시정지 " + mode);
                    Check(!batch.Resume() && !batch.Pause(), "완료된 묶음 자동 재개 금지 " + mode);
                }
                Check(uninterrupted.SequenceEqual(paused), "일시정지 유무와 호출 분할이 전체 행동·난수·결과를 바꾸지 않음");
                BotBatchGame[] games = uninterrupted.Select(JsonUtility.FromJson<BotBatchGame>).ToArray();
                Check(games.Select(g => g.ordinal).SequenceEqual(new[] { 0, 1, 2, 3 }) &&
                    games.Select(g => g.strategy).SequenceEqual(new[] { BotStrategyKind.Basic, BotStrategyKind.Planning, BotStrategyKind.Basic, BotStrategyKind.Planning }) &&
                    games.Select(g => g.seed).SequenceEqual(new[] { 771, 771, 772, 772 }), "시드별 기본→계획 순차 실행과 누락·중복 없음");

                List<BotBatchGame> stopped = new List<BotBatchGame>();
                using (BotBatchSession batch = new BotBatchSession(level, new[] { 771 }, (_, game) => { if (game != null) stopped.Add(game); }))
                {
                    batch.Advance(); batch.Pause(); batch.Stop();
                    for (int i = 0; i < 10; i++) batch.Advance();
                    batch.Stop();
                    Check(batch.Record.status == BotBatchStatus.Stopped && batch.Record.stopped == 1 && batch.Record.finished == 0 &&
                        batch.Record.Unrun == 1 && batch.Current == null && stopped.Count == 1,
                        "준비 중 일시정지 후 중지: 미완료 판 1개·미실행 1개·중복 기록 없음");
                }
                using (BotBatchSession batch = new BotBatchSession(level, new[] { 771 }, (_, _) => throw new IOException("검사 저장 실패")))
                {
                    batch.Advance();
                    Check(batch.Record.status == BotBatchStatus.Error && !batch.NeedsAdvance && batch.Current == null && batch.SaveError != null,
                        "최초 저장 실패는 판을 시작하지 않고 오류 표시");
                }
                using (BotBatchSession batch = new BotBatchSession(level, new[] { 771 }, (_, game) => { if (game != null) throw new IOException("완료 기록 저장 실패"); }))
                {
                    for (int i = 0; batch.NeedsAdvance && i < 100000; i++) batch.Advance();
                    Check(batch.Record.status == BotBatchStatus.Error && batch.UnsavedGame != null && batch.Current == null && batch.Record.Recorded == 1,
                        "판 저장 실패는 다음 판 실행 차단·미저장 결과 유지");
                }
                Check(JsonUtility.ToJson(level) == original && EditorUtility.IsDirty(level) == dirty, "반복·중지·저장 실패 후 원본 JSON·dirty 보존");
                Store(level);
                Persisted(level);
                File.WriteAllLines(Evidence + "/core-actions.jsonl", uninterrupted);
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            finally { if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            File.WriteAllLines(Evidence + "/core-results.txt", Results);
            if (failure != null) Debug.LogException(failure);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        /// <param name="level">검증 전용 레벨. 실제 에셋은 수정하지 않는다.</param>
        private static void Store(LevelDefinition level)
        {
            string directory = "Library/Match/AutoPlayVerification/" + Guid.NewGuid().ToString("N");
            BotBatchStore store = new BotBatchStore(directory);
            Check(store.LoadLatest() == null, "기록 없는 저장소는 이전 시험 없음");
            using BotBatchSession batch = new BotBatchSession(level, new[] { 771, 772 }, store.Save);
            string initial = JsonUtility.ToJson(batch.Record);
            while (batch.NeedsAdvance && batch.Record.finished < 1) batch.Advance();
            Check(batch.Record.finished == 1 && batch.SaveError == null, "판별 파일과 묶음 요약 실제 저장");
            string manifest = Path.Combine(directory, batch.Record.id, "batch.json");
            // 판 파일 교체 후 요약 교체 전에 프로세스가 끝난 상태를 검사 소유 파일로 재현한다.
            File.WriteAllText(manifest, initial);
            BotBatchRecord recovered = store.LoadLatest();
            Check(recovered.finished == 1 && recovered.basicFinished == 1 && recovered.planningFinished == 0 &&
                recovered.status == BotBatchStatus.Interrupted && recovered.Unrun == 3, "요약보다 먼저 확정된 판 복구·중단 표시");
            Check(recovered.definitionJson == JsonUtility.ToJson(level) && recovered.seeds.SequenceEqual(new[] { 771, 772 }) &&
                BotBatchStore.SameVersions(recovered), "정의 사본·시드·모든 규칙 버전 왕복");
            recovered.planningVersion = "old";
            Check(!BotBatchStore.SameVersions(recovered), "규칙 버전 차이는 완전 재현으로 표시하지 않음");
            batch.Advance(); batch.Pause(); batch.Stop();
            recovered = store.LoadLatest();
            Check(recovered.finished == 1 && recovered.stopped == 1 && recovered.Unrun == 2 && recovered.status == BotBatchStatus.Stopped,
                "일시정지 후 중지 기록 왕복과 완료 판 보존");
            string gamePath = Path.Combine(directory, batch.Record.id, "000000.json");
            string savedGame = File.ReadAllText(gamePath);
            File.WriteAllText(gamePath, "{}");
            bool corrupt = false;
            try { store.LoadLatest(); } catch (InvalidDataException) { corrupt = true; }
            Check(corrupt, "손상된 판 기록을 빈 결과로 숨기지 않음");
            File.WriteAllText(gamePath, savedGame);
            File.Delete(Path.Combine(directory, batch.Record.id, "000001.json"));
            bool missing = false;
            try { store.LoadLatest(); } catch (InvalidDataException) { missing = true; }
            Check(missing, "완료 요약에 있는 판 누락 감지");
            BotBatchStore lockedStore = new BotBatchStore(directory + "/locked");
            using BotBatchSession lockedBatch = new BotBatchSession(level, new[] { 771 }, lockedStore.Save);
            string lockedPath = Path.Combine(directory, "locked", lockedBatch.Record.id, "batch.json");
            string committed = File.ReadAllText(lockedPath);
            using (FileStream handle = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Check(!lockedBatch.Pause() && lockedBatch.Record.status == BotBatchStatus.Error && lockedBatch.SaveError != null,
                    "실제 파일 잠금이 지속되면 제한된 재시도 후 명시적 저장 오류");
            }
            Check(File.Exists(lockedPath) && File.ReadAllText(lockedPath) == committed,
                "원자 교체 실패가 이전 확정 파일을 삭제하거나 훼손하지 않음");
        }

        /// <param name="pass">실제로 확인한 조건.</param><param name="description">검증 근거.</param>
        private static void Check(bool pass, string description)
        {
            if (!pass) throw new InvalidOperationException(description);
            Results.Add("PASS " + description);
        }
    }
}
