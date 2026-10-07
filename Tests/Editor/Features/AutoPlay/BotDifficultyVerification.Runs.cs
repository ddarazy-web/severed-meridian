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
    public static partial class BotDifficultyVerification
    {
        private static BotBatchSession batch;
        private static LevelDefinition runLevel;
        private static string runOriginal;
        private static string runRoot;
        private static int runIndex;
        private static int[] previousSeeds;
        private static Stopwatch runWatch;
        private static double slowestAdvance;

        /// <summary>세 대표 정의와 첫 정의의 새 시드 재시험을 실제 공통 실행기로 수행한다.</summary>
        public static void Runs()
        {
            Directory.CreateDirectory(Evidence); Results.Clear(); runIndex = 0;
            runRoot = Path.GetFullPath(Evidence + "/runs-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(Evidence + "/runs-path.txt", runRoot);
            EditorApplication.update += RunTick;
        }

        /// <summary>이미 완료한 두 묶음을 보존하고 배치 검사에서 멈춘 복합 정의부터 재개한다.</summary>
        public static void ResumeRuns()
        {
            runRoot = File.ReadAllText(Evidence + "/runs-path.txt");
            File.Copy(Evidence + "/runs-results.txt", Evidence + "/runs-first-attempt.txt", true);
            Results.Clear(); Results.AddRange(File.ReadAllLines(Evidence + "/runs-results.txt").Where(line => line.StartsWith("PASS ") || line.StartsWith("DATA ")));
            previousSeeds = JsonUtility.FromJson<BotBatchRecord>(File.ReadAllText(Directory.GetFiles(runRoot, "batch.json", SearchOption.AllDirectories).First())).seeds;
            runIndex = 2; EditorApplication.update += RunTick;
        }

        // 한 번의 실행 단위 사이에 Editor로 제어를 돌려준다. 시간 초과를 결과나 패배로 꾸미지 않는다.
        private static void RunTick()
        {
            try
            {
                if (batch == null)
                {
                    int size = runIndex == 2 ? 5 : 4;
                    runLevel = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null, new object[] { Enumerable.Range(0, size * size).Select(i => new BoardCoordinate(i / size, i % size)).ToArray() });
                    int moves = runIndex == 1 ? 1 : runIndex == 2 ? 8 : 6;
                    JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[],\"moveCount\":" + moves + ",\"missions\":[{\"kind\":0,\"color\":0,\"count\":3}]}", runLevel);
                    runLevel.name = new[] { "기본 수집", "한 수 도전", "상자와 거미줄", "기본 수집 새 시드" }[runIndex];
                    for (int column = 0; column < size; column++)
                        typeof(SettlementVerification).GetMethod("Source", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
                            new object[] { runLevel, new BoardCoordinate(0, column), SupplyExhaustion.Random, new[] { new SupplyItem(SupplyKind.RandomNormal, 1) } });
                    if (runIndex == 2)
                    {
                        LevelObstacleEditing.Apply(runLevel, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = 2 }, new[] { new BoardCoordinate(3, 1) });
                        // 덮개는 내부 블록을 지정해야 편집기가 허용한다. 무작위 시작 빈칸에 덮개만 놓지 않는다.
                        LevelObstacleEditing.Apply(runLevel, new PlacementBrush { Layer = PlacementLayer.Block, Kind = (int)InitialBlockKind.FixedNormal, Color = RabbitColor.Type1 }, new[] { new BoardCoordinate(3, 3) });
                        PlacementEditResult cover = LevelObstacleEditing.Apply(runLevel, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 2 }, new[] { new BoardCoordinate(3, 3) });
                        Check(cover.Changed == 1, "대표 정의 거미줄 배치: " + cover);
                        JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":1,\"count\":1},{\"kind\":2,\"count\":1}]}", runLevel);
                    }
                    runOriginal = JsonUtility.ToJson(runLevel);
                    int[] seeds = BotBatchSession.NewSeeds(100, previousSeeds);
                    previousSeeds = seeds;
                    BotBatchStore store = new BotBatchStore(runRoot);
                    batch = new BotBatchSession(runLevel, seeds, store.Save);
                    runWatch = Stopwatch.StartNew(); slowestAdvance = 0;
                }
                if (batch.NeedsAdvance)
                {
                    Stopwatch slice = Stopwatch.StartNew(); batch.Advance();
                    slowestAdvance = Math.Max(slowestAdvance, slice.Elapsed.TotalMilliseconds);
                    File.WriteAllText(Evidence + "/runs-progress.txt", runIndex + ": " + batch.Record.finished + "/200 " + batch.Record.status);
                    return;
                }
                Check(batch.Record.status == BotBatchStatus.Completed, "실제 묶음 완료 " + runIndex + ": " + batch.Record.message);
                BotAnalysisReader reader = new BotAnalysisReader(Path.Combine(runRoot, batch.Record.id));
                while (!reader.IsDone) reader.Advance();
                Check(reader.Error == null, "실제 기록 검증 " + runIndex + ": " + reader.Error);
                BotStrategyStatistics basic = BotBatchStatistics.Calculate(reader.Record, reader.Games, reader.Missions, BotStrategyKind.Basic);
                BotStrategyStatistics planning = BotBatchStatistics.Calculate(reader.Record, reader.Games, reader.Missions, BotStrategyKind.Planning);
                BotPairedStatistics pairs = BotBatchStatistics.Pair(reader.Record.seeds, reader.Games.Where(g => g.Strategy == BotStrategyKind.Basic), reader.Games.Where(g => g.Strategy == BotStrategyKind.Planning));
                BotDifficultyResult result = BotDifficultyRules.Evaluate(reader.Record, basic, planning, pairs, reader.InitialMoves);
                Check(basic.Normal == 100 && planning.Normal == 100, "두 전략 정상 100판 " + runIndex);
                Check(runOriginal == JsonUtility.ToJson(runLevel), "시험 후 원본 정의 보존 " + runIndex);
                Results.Add("DATA " + runIndex + " " + runLevel.name + " " + batch.Record.id + " 기본=" + basic.Won + "/100 계획=" + planning.Won + "/100 등급=" + result.Title + " 태그=" + string.Join(",", result.Tags) + " 초=" + runWatch.Elapsed.TotalSeconds.ToString("F2") + " 최대AdvanceMs=" + slowestAdvance.ToString("F2"));
                File.WriteAllLines(Evidence + "/runs-results.txt", Results);
                batch.Dispose(); batch = null; UnityEngine.Object.DestroyImmediate(runLevel); runLevel = null;
                if (++runIndex < 4) return;
                EditorApplication.update -= RunTick; EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/runs-results.txt", Results);
                batch?.Dispose(); if (runLevel != null) UnityEngine.Object.DestroyImmediate(runLevel);
                EditorApplication.update -= RunTick; UnityEngine.Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
    }
}
