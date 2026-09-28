using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class PlanningVerification
    {
        private static bool replayGame;
        public static void ReplayGame() { replayGame = true; Game(); }

        /// <summary>실제 계획 봇의 전체 행동열을 수동 실행과 다른 프로세스에서 각각 재현한다.</summary>
        public static void Game()
        {
            LevelDefinition level = null;
            Exception failure = null;
            Results.Clear(); Directory.CreateDirectory(Evidence);
            try
            {
                string proof = BotPlaySession.Version + "\n" + PlanningSearch.Version + "\n" + BoardActionExecutor.Version + "\n";
                double maximumStep = 0;
                for (int scenario = 0; scenario < 2; scenario++)
                {
                    int size = scenario == 0 ? 4 : 5;
                    BoardCoordinate[] active = Enumerable.Range(0, size * size).Select(i => new BoardCoordinate(i / size, i % size)).ToArray();
                    level = (LevelDefinition)typeof(SettlementVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { active });
                    JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[],\"moveCount\":3,\"missions\":[{\"kind\":0,\"color\":0,\"count\":100}]}", level);
                    if (scenario == 1)
                    {
                        JsonUtility.FromJsonOverwrite("{\"moveCount\":5,\"missions\":[{\"kind\":1,\"count\":1}]}", level);
                        LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = 2 },
                            new[] { new BoardCoordinate(3, 3) });
                        // 편집기의 새 장애물 ID는 매번 달라진다. 같은 정의의 재현 검사이므로
                        // 검사 소유 장애물만 고정 ID로 맞춘 뒤 지문과 실행 상태 전체를 비교한다.
                        SerializedObject serialized = new SerializedObject(level);
                        serialized.FindProperty("obstacles").GetArrayElementAtIndex(0).FindPropertyRelative("id").stringValue = "planning-replay-crate";
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    string original = JsonUtility.ToJson(level); bool dirty = EditorUtility.IsDirty(level);
                    int seed = scenario == 0 ? 771 : 9;
                    using BotPlaySession game = new BotPlaySession(level, seed, BotStrategyKind.Planning);
                    game.Begin(true);
                    Stopwatch elapsed = Stopwatch.StartNew();
                    int updates = 0;
                    while (game.NeedsAdvance && updates++ < 100000)
                    {
                        long before = Stopwatch.GetTimestamp(); game.Advance();
                        maximumStep = Math.Max(maximumStep, (Stopwatch.GetTimestamp() - before) * 1000d / Stopwatch.Frequency);
                    }
                    elapsed.Stop();
                    Check(!game.NeedsAdvance && game.Status != BotSessionStatus.Error && game.Outcome != null,
                        "전체 계획 판 " + scenario + " 정상 종료: " + game.Status + " / " + game.Message);
                    Check(game.Records.Count >= 2 && game.Records.All(record => record.Choice.Reason.Contains("가정")),
                        "전체 판 " + scenario + " 매 실제 관찰에서 다시 계획하고 행동 이유 기록");
                    BoardActionExecutor manual = new BoardActionExecutor(StartingBoardBuilder.Build(level, seed).State);
                    foreach (BotTurnRecord record in game.Records)
                    {
                        BotAction action = record.Choice.Action;
                        BoardActionResult applied = action.Kind == BotActionKind.Activate ? manual.Activate(action.First) : manual.Swap(action.First, action.Second.Value);
                        Check(applied.IsApplied, "판 " + scenario + " 수동 명령 재현 " + record.Turn);
                        for (int step = 0; manual.HasPendingCascade && step < 2000; step++) manual.AdvanceCascade();
                        Check(!manual.HasPendingCascade && manual.State.MovesRemaining == record.MovesAfter && manual.State.Random.DrawCount == record.RandomAfter,
                            "판 " + scenario + " 행동별 수동 비용·난수 일치 " + record.Turn);
                    }
                    Check(Snapshot(manual.State) == Snapshot(game.State) && Snapshot(manual.Outcome) == Snapshot(game.Outcome),
                        "판 " + scenario + " 전체 행동열의 수동 최종 상태·미션·결과 일치");
                    Check(JsonUtility.ToJson(level) == original && EditorUtility.IsDirty(level) == dirty, "판 " + scenario + " 원본 JSON·dirty 보존");
                    proof += game.DefinitionFingerprint + "\n" + seed + "\n" + Snapshot(game.Records) + "\n" + Snapshot(game.State) + "\n" + Snapshot(game.Outcome) + "\n";
                    File.AppendAllText(Evidence + (replayGame ? "/game-replay-measurement.txt" : "/game-measurement.txt"),
                        $"Scenario={scenario}, Actions={game.Records.Count}, Updates={updates}, ElapsedMs={elapsed.ElapsedMilliseconds}, MaxStepMs={maximumStep:F3}\n");
                    UnityEngine.Object.DestroyImmediate(level); level = null;
                }
                File.WriteAllText(Evidence + (replayGame ? "/game-second.txt" : "/game-first.txt"), proof);
                if (replayGame)
                {
                    Check(proof == File.ReadAllText(Evidence + "/game-first.txt"), "독립 프로세스에서 실제 계획 판의 전 행동·상태·난수·결과 재현");
                    Check(Process.GetCurrentProcess().Id.ToString() != File.ReadAllText(Evidence + "/game-first-process.txt"), "전체 판 재현은 서로 다른 Unity 프로세스");
                }
                else File.WriteAllText(Evidence + "/game-first-process.txt", Process.GetCurrentProcess().Id.ToString());
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); }
            finally { if (level != null) UnityEngine.Object.DestroyImmediate(level); }
            File.WriteAllLines(Evidence + (replayGame ? "/game-replay-results.txt" : "/game-results.txt"), Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}
