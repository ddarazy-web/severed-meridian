using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AutoPlay;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class BotAnalysisVerification
    {
        /// <summary>검사 소유 로그 영역만 사용하여 기록 손상·쓰기 실패·대량 조회를 검증한다.</summary>
        public static void Edges()
        {
            Results.Clear();
            Exception failure = null;
            try
            {
                BotAnalysisReader source = Load(File.ReadAllText(Evidence + "/source-path.txt"));
                string root = Path.GetFullPath(Evidence + "/edges-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                string copyPath = Path.Combine(root, "copy");
                using (BotAnalysisExport copy = new BotAnalysisExport(source, copyPath))
                { while (!copy.IsDone) copy.Advance(); Check(copy.Error == null, "경계 검사 소유 사본 준비: " + copy.Error); }
                string headerPath = Path.Combine(copyPath, "batch.json"), firstPath = Path.Combine(copyPath, "000000.json");
                string header = File.ReadAllText(headerPath), first = File.ReadAllText(firstPath);
                foreach (string fault in new[] { "summary", "duplicate-seed", "format", "duplicate-ordinal", "strategy", "action", "truncated" })
                {
                    BotBatchRecord record = JsonUtility.FromJson<BotBatchRecord>(header);
                    BotBatchGame game = JsonUtility.FromJson<BotBatchGame>(first);
                    if (fault == "summary") record.won++;
                    if (fault == "duplicate-seed") record.seeds[1] = record.seeds[0];
                    if (fault == "format") record.formatVersion++;
                    if (fault == "duplicate-ordinal") game.ordinal = 1;
                    if (fault == "strategy") game.strategy = BotStrategyKind.Planning;
                    if (fault == "action") game.actions[0].randomAfter = -1;
                    File.WriteAllText(headerPath, JsonUtility.ToJson(record));
                    File.WriteAllText(firstPath, fault == "truncated" ? "{broken" : JsonUtility.ToJson(game));
                    bool rejected = false;
                    try { Load(copyPath); } catch (Exception) { rejected = true; }
                    Check(rejected, "손상 기록 거절 " + fault);
                    File.WriteAllText(headerPath, header); File.WriteAllText(firstPath, first);
                }
                // 옛 기록에서 빠진 값은 실제 0수와 다르다. 요약에서 제외하고 사용자에게 없음으로 보여야 한다.
                string missing = System.Text.RegularExpressions.Regex.Replace(first, "\"usedMoves\"\\s*:\\s*-?\\d+,?", "");
                missing = System.Text.RegularExpressions.Regex.Replace(missing, "\"remainingMoves\"\\s*:\\s*-?\\d+,?", "");
                File.WriteAllText(firstPath, missing);
                BotAnalysisReader old = Load(copyPath);
                Check(old.Games[0].UsedMoves == -1 && old.Games[0].RemainingMoves == -1, "누락 이동 수는 0이 아닌 기록 없음");
                File.WriteAllText(firstPath, first);
                BotAnalysisReader loaded = Load(copyPath);
                string race = Path.Combine(root, "appeared");
                using (BotAnalysisExport export = new BotAnalysisExport(loaded, race))
                {
                    Directory.CreateDirectory(race); File.WriteAllText(Path.Combine(race, "keep.txt"), "keep");
                    while (!export.IsDone) export.Advance();
                    Check(export.Error != null && File.ReadAllText(Path.Combine(race, "keep.txt")) == "keep", "보관 도중 생긴 대상 폴더 보존·실패 보고");
                }
                string denied = Path.Combine(root, "write-failure");
                using (BotAnalysisExport export = new BotAnalysisExport(loaded, denied))
                {
                    // ACL을 바꾸지 않고 기록할 파일 위치를 폴더로 막아 실제 파일 쓰기 실패를 만든다.
                    string staging = Directory.GetDirectories(root, "write-failure.pending-*").Single();
                    Directory.CreateDirectory(Path.Combine(staging, "000000.json"));
                    export.Advance();
                    Check(export.IsDone && export.Error != null && !Directory.Exists(denied), "실제 파일 쓰기 실패를 완료로 표시하지 않음");
                }
                Check(Directory.GetDirectories(root, "*.pending-*").Length == 0 &&
                    File.ReadAllText(headerPath) == header && File.ReadAllText(firstPath) == first, "실패 임시 폴더 정리와 원본 바이트 보존");

                // 실제 플레이 부하가 아니라 파일 조회 부하다. 행동 기록을 복제한 합성 20,000판임을 증거에 남긴다.
                string large = Path.Combine(root, "large"); Directory.CreateDirectory(large);
                BotBatchRecord big = JsonUtility.FromJson<BotBatchRecord>(header);
                big.id = Guid.NewGuid().ToString("N"); big.seeds = Enumerable.Range(100000, BotBatchSession.MaximumCount).ToArray();
                big.finished = big.exhausted = big.Total; big.won = big.blocked = big.errors = big.stopped = 0;
                big.basicFinished = big.planningFinished = big.seeds.Length; big.status = BotBatchStatus.Completed;
                BotBatchGame template = source.ReadGame(0);
                template.batchId = big.id; template.outcome = BotSessionStatus.MovesExhausted;
                for (int i = 0; i < big.Total; i++)
                {
                    template.ordinal = i; template.seed = big.seeds[i / 2];
                    template.strategy = i % 2 == 0 ? BotStrategyKind.Basic : BotStrategyKind.Planning;
                    File.WriteAllText(Path.Combine(large, i.ToString("D6") + ".json"), JsonUtility.ToJson(template));
                }
                File.WriteAllText(Path.Combine(large, "batch.json"), JsonUtility.ToJson(big));
                long before = GC.GetTotalMemory(true); Stopwatch total = Stopwatch.StartNew(), step = new Stopwatch();
                BotAnalysisReader many = new BotAnalysisReader(large); double constructorMs = total.Elapsed.TotalMilliseconds, maximum = 0;
                while (!many.IsDone)
                {
                    step.Restart(); many.Advance(); step.Stop();
                    maximum = Math.Max(maximum, step.Elapsed.TotalMilliseconds);
                }
                total.Stop();
                Check(many.Error == null && many.Games.Count == 20000, "합성 20,000판 분할 조회");
                step.Restart();
                BotStrategyStatistics statistics = BotBatchStatistics.Calculate(many.Record, many.Games, many.Missions, BotStrategyKind.Basic);
                int filtered = many.Games.Count(g => g.Strategy == BotStrategyKind.Planning);
                step.Stop();
                Check(statistics.Normal == 10000 && filtered == 10000 && statistics.SuccessPercent == 0, "대량 전략 집계·필터 기대값");
                Stopwatch detail = Stopwatch.StartNew(); BotBatchGame last = many.ReadGame(19999); detail.Stop();
                Check(last.ordinal == 19999 && last.actions.Length == template.actions.Length, "대량 목록 끝 사례 상세 지연 읽기");
                File.WriteAllLines(Evidence + "/load-measurement.txt", new[] {
                    "SyntheticFiles=20000; NOT new played games", "Source=" + large,
                    $"ConstructorMs={constructorMs:F3}", $"ReadTotalMs={total.Elapsed.TotalMilliseconds:F3}",
                    $"MaxAdvanceMs={maximum:F3}", $"AggregateFilterMs={step.Elapsed.TotalMilliseconds:F3}",
                    $"DetailMs={detail.Elapsed.TotalMilliseconds:F3}", $"ManagedBefore={before}", $"ManagedAfter={GC.GetTotalMemory(true)}" });
                File.WriteAllText(Evidence + "/large-path.txt", large);
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); UnityEngine.Debug.LogException(error); }
            File.WriteAllLines(Evidence + "/edges-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}
