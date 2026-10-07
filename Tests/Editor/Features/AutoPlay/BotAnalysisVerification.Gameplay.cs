using System;
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
    public static partial class BotAnalysisVerification
    {
        /// <summary>실제로 새로 실행한 성공·라스트팡·랜덤 파워 공급 사례를 기록하고 재생한다.</summary>
        public static void Gameplay()
        {
            Results.Clear(); Exception failure = null;
            try
            {
                for (int mode = 0; mode < 3; mode++)
                {
                    LevelDefinition level = (LevelDefinition)typeof(ItemBoosterVerification).GetMethod("PlayFixture", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    try
                    {
                        JsonUtility.FromJsonOverwrite("{\"moveCount\":2,\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", level);
                        if (mode == 1)
                        {
                            foreach (int column in new[] { 0, 3, 6, 9 })
                                typeof(SettlementVerification).GetMethod("Source", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                                    new object[] { level, new BoardCoordinate(0, column), SupplyExhaustion.Random, new[] { new SupplyItem(SupplyKind.RandomPower, 3) } });
                        }
                        string original = JsonUtility.ToJson(level); bool dirty = EditorUtility.IsDirty(level);
                        using BotBatchSession conditions = new BotBatchSession(level, new[] { 771 }, (_, _) => { });
                        BotBatchRecord record = conditions.Record;
                        using BotPlaySession session = new BotPlaySession(level, 771);
                        if (mode == 2) session.RequestStop(); else session.Begin(true);
                        for (int step = 0; session.NeedsAdvance && step < 100000; step++) session.Advance();
                        Check(!session.NeedsAdvance, "실제 사례 종료 " + mode);
                        if (mode != 2)
                        {
                            Check(session.Status == BotSessionStatus.Won && session.State.Missions.All(m => m.Remaining == 0), "실제 성공과 미션 완료 " + mode);
                            BoardActionExecutor engine = (BoardActionExecutor)typeof(BotPlaySession).GetField("executor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);
                            Check(engine.LastPangWaves > 0, "라스트팡 실제 실행 " + mode);
                            if (mode == 1) Check(session.State.Supply.Sources.Any(s => s.ItemIndex > 0 || s.ItemConsumed > 0), "랜덤 파워 공급 실제 소비");
                        }
                        BotBatchGame game = (BotBatchGame)Activator.CreateInstance(typeof(BotBatchGame), BindingFlags.Instance | BindingFlags.NonPublic,
                            null, new object[] { record, session, session.Status, session.Message }, null);
                        record.status = BotBatchStatus.Stopped;
                        if (mode == 2) record.stopped = 1;
                        else { record.finished = record.basicFinished = record.won = 1; }
                        string folder = Path.GetFullPath(Evidence + "/gameplay-" + mode + "-" + record.id); Directory.CreateDirectory(folder);
                        File.WriteAllText(Path.Combine(folder, "batch.json"), JsonUtility.ToJson(record));
                        File.WriteAllText(Path.Combine(folder, "000000.json"), JsonUtility.ToJson(game));
                        BotAnalysisReader saved = Load(folder);
                        if (mode != 2)
                        {
                            BotStrategyStatistics stats = BotBatchStatistics.Calculate(saved.Record, saved.Games, saved.Missions, BotStrategyKind.Basic);
                            Check(game.remainingMoves > 0 && game.remainingMoves == game.actions.Last().movesAfter &&
                                stats.Remaining.Median == game.remainingMoves && saved.InitialMoves == 2,
                                "라스트팡 뒤에도 추천 입력은 실제 남은 이동 수·최초 이동 수 " + mode);
                        }
                        using BotRecordReplay replay = new BotRecordReplay(level, saved.Record, saved.ReadGame(0));
                        replay.Begin(true);
                        for (int step = 0; replay.NeedsAdvance && step < 100000; step++) replay.Advance();
                        Check(replay.Status == (mode == 2 ? BotReplayStatus.Partial : BotReplayStatus.Completed), "새 사례 저장·열기·재생 " + mode + ": " + replay.Message);
                        if (mode != 2)
                        {
                            MethodInfo snapshot = typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic);
                            Check((string)snapshot.Invoke(null, new object[] { replay.State }) == (string)snapshot.Invoke(null, new object[] { session.State }),
                                "최종 보드·공급·장애물·미션·난수 전체 대조 " + mode);
                        }
                        Check(JsonUtility.ToJson(level) == original && EditorUtility.IsDirty(level) == dirty, "새 사례 후 정의·dirty 보존 " + mode);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                }
            }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); UnityEngine.Debug.LogException(error); }
            File.WriteAllLines(Evidence + "/gameplay-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}
