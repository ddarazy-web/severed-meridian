using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Board;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class BotBatchVerification
    {
        public static void Load() => RunLoad(false);
        public static void ReplayLoad() => RunLoad(true);

        /// <summary>요청 기본값인 100개 시드×두 전략을 실제 완주하고 독립 프로세스로 같은 조건을 재현한다.</summary>
        /// <param name="replay">첫 실행의 시드 묶음으로 재검증할지 여부.</param>
        private static void RunLoad(bool replay)
        {
            LevelDefinition level = null;
            Exception failure = null;
            Results.Clear(); Directory.CreateDirectory(Evidence);
            string prefix = replay ? "load-replay" : "load";
            try
            {
                BoardCoordinate[] cells = Enumerable.Range(0, 16).Select(i => new BoardCoordinate(i / 4, i % 4)).ToArray();
                level = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { cells });
                JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[],\"moveCount\":3,\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
                string source = JsonUtility.ToJson(level); bool dirty = EditorUtility.IsDirty(level);
                int[] seeds = replay ? File.ReadAllLines(Evidence + "/load-seeds.txt").Select(int.Parse).ToArray() : BotBatchSession.NewSeeds(100);
                if (!replay)
                {
                    File.WriteAllLines(Evidence + "/load-seeds.txt", seeds.Select(s => s.ToString()));
                    File.WriteAllText(Evidence + "/load-process.txt", Process.GetCurrentProcess().Id.ToString());
                }
                else Check(File.ReadAllText(Evidence + "/load-process.txt") != Process.GetCurrentProcess().Id.ToString(), "재현은 독립 Unity 프로세스");
                string recordsPath = Evidence + "/" + prefix + "-actions.jsonl";
                File.WriteAllText(recordsPath, "");
                BotBatchStore store = new BotBatchStore("Library/Match/AutoPlayLoadVerification/" + Guid.NewGuid().ToString("N"));
                using BotBatchSession batch = new BotBatchSession(level, seeds, (record, game) => {
                    store.Save(record, game);
                    if (game == null) return;
                    // 실행 ID는 서로 달라야 한다. 비교 파일에서는 이것만 제거하고 모든 실제 행동·결과를 대조한다.
                    BotBatchGame copy = JsonUtility.FromJson<BotBatchGame>(JsonUtility.ToJson(game)); copy.batchId = "independent-comparison";
                    File.AppendAllText(recordsPath, JsonUtility.ToJson(copy) + "\n");
                });
                Stopwatch total = Stopwatch.StartNew();
                double maxUnit = 0, maxPause = 0, maxStop = 0;
                long memoryBefore = GC.GetTotalMemory(true), maxMemory = memoryBefore;
                int units = 0, pauses = 0;
                while (batch.NeedsAdvance && units < 10000000)
                {
                    long start = Stopwatch.GetTimestamp(); batch.Advance(); units++;
                    maxUnit = Math.Max(maxUnit, (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency);
                    if (replay && batch.NeedsAdvance && units % 127 == 0)
                    {
                        start = Stopwatch.GetTimestamp(); batch.Pause();
                        maxPause = Math.Max(maxPause, (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency);
                        for (int idle = 0; idle < 5; idle++) batch.Advance();
                        batch.Resume(); pauses++;
                    }
                    if (units % 100 == 0) maxMemory = Math.Max(maxMemory, GC.GetTotalMemory(false));
                    if (units % 5000 == 0) File.WriteAllText(Evidence + "/" + prefix + "-progress.txt", $"Units={units}, Finished={batch.Record.finished}/200, Status={batch.Record.status}");
                }
                total.Stop();
                Check(batch.Record.status == BotBatchStatus.Completed && batch.Record.finished == 200 && batch.Record.errors == 0,
                    "실제 기본 200판 완주: " + batch.Record.message);
                Check(batch.Record.basicFinished == 100 && batch.Record.planningFinished == 100 && batch.Record.Unrun == 0,
                    "기본·계획 각각 100판 · 미실행 없음");
                Check(batch.Record.won + batch.Record.exhausted + batch.Record.blocked == 200, "종료 종류 집계와 완료 판 수 일치");
                BotBatchRecord saved = store.LoadLatest();
                Check(saved.finished == 200 && saved.status == BotBatchStatus.Completed, "기본 부하의 파일 기록 200개 재검증");
                Check(JsonUtility.ToJson(level) == source && EditorUtility.IsDirty(level) == dirty, "200판 실행 후 원본 JSON·dirty 보존");
                using (BotBatchSession stop = new BotBatchSession(level, new[] { seeds[0] }, store.Save))
                {
                    stop.Advance(); stop.Pause(); long start = Stopwatch.GetTimestamp(); stop.Stop();
                    maxStop = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                    Check(stop.Record.status == BotBatchStatus.Stopped && !stop.NeedsAdvance, "실측 중지 요청 후 실행 차단");
                }
                File.WriteAllText(Evidence + "/" + prefix + "-measurement.txt",
                    $"Board=4x4, Moves=3, Mission=Color100, Seeds=100, Strategies=2\nElapsedMs={total.Elapsed.TotalMilliseconds:F3}\nUnits={units}\nMaxAdvanceMs={maxUnit:F3}\nMaxPauseMs={maxPause:F3}\nStopMs={maxStop:F3}\nManagedBefore={memoryBefore}\nManagedPeak={maxMemory}\nManagedAfterCollection={GC.GetTotalMemory(true)}\nPauses={pauses}\n" + JsonUtility.ToJson(batch.Record, true));
                if (replay)
                {
                    Check(pauses > 0 && File.ReadAllText(recordsPath) == File.ReadAllText(Evidence + "/load-actions.jsonl"),
                        "독립 프로세스·일시정지 분할을 바꿔도 200판의 전 행동·난수·종료 기록 동일");
                }
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            finally { if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            File.WriteAllLines(Evidence + "/" + prefix + "-results.txt", Results);
            if (failure != null) UnityEngine.Debug.LogException(failure);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}
