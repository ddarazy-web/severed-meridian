using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AutoPlay;
using LevelAuthoring.Documents;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using LevelTool;
using Levels;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class BalanceJsonVerification
    {
        private static BotMoveBalanceSession session;
        private static BotMoveBalanceStore store;
        private static ContentSnapshot snapshot;
        private static string levelId, original;
        private static double deadline;
        public static void Run()
        {
            try
            {
                var documents = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt")).Read().Snapshot.Documents;
                var level = documents.First(doc => doc.Kind == "level" && (int)doc.Data["levelNumber"] == 2);
                levelId = level.Id;
                JObject data = LevelToolDocuments.NewLevel();
                data["catalogId"] = level.Data["catalogId"].DeepClone(); data["levelNumber"] = 2; data["displayName"] = "JSON 추천 시험"; data["moveCount"] = 7;
                data["missions"] = new JArray(new JObject { ["kind"] = "Color", ["color"] = "Type1", ["count"] = 99 });
                snapshot = new ContentSnapshot(documents.Select(doc => doc.Id == levelId ? new ContentDocument("level", levelId, data) : doc));
                original = AuthoringTrialSource.Encode(snapshot, levelId);
                store = new BotMoveBalanceStore("Logs/GameAuthoringStage05/measured-balance-" + Guid.NewGuid().ToString("N"), source:
                    new BotTrialSourceContext(levelId, original, moves => AuthoringTrialSource.EncodeWithMoves(snapshot, levelId, moves)));
                using (var graph = new AuthoringObjectGraph(snapshot.Documents))
                    session = new BotMoveBalanceSession((LevelDefinition)graph.Resolve(levelId), store);
                deadline = EditorApplication.timeSinceStartup + 600;
                EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish(error); }
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("200판 추천 JSON 검사 시간 초과");
                var budget = Stopwatch.StartNew();
                while (budget.ElapsedMilliseconds < 30 && session.Record.trials.Count == 0 && session.NeedsAdvance) session.Advance();
                if (session.Record.status == BotBatchStatus.Error) throw new Exception(session.Record.message);
                if (session.Record.trials.Count == 0) return;
                session.Stop();
                string directory = Path.Combine(store.Root, session.Record.id);
                var restored = AuthoringTrialSource.Decode(File.ReadAllText(Path.Combine(directory, "source.context")));
                using (var graph = new AuthoringObjectGraph(restored.Snapshot.Documents))
                {
                    var level = (LevelDefinition)graph.Resolve(restored.LevelId);
                    var reader = store.OpenReader(level);
                    while (!reader.IsDone) reader.Advance();
                    if (reader.Error != null || reader.CheckedTrials != 1 || level.MoveCount != 7) throw new Exception("추천 복원 대조 실패: " + reader.Error);
                    var trial = reader.Record.trials.Single();
                    string trialDirectory = Path.Combine(directory, "trials", trial.batchId);
                    var variant = AuthoringTrialSource.Decode(File.ReadAllText(Path.Combine(trialDirectory, "source.context")));
                    using (var trialGraph = new AuthoringObjectGraph(variant.Snapshot.Documents))
                    {
                        var batch = new BotAnalysisReader(trialDirectory, (LevelDefinition)trialGraph.Resolve(variant.LevelId));
                        while (!batch.IsDone) batch.Advance();
                        if (batch.Error != null || batch.Games.Count != 200 || batch.InitialMoves != 1) throw new Exception("200판 이동 변형 복원 실패: " + batch.Error);
                    }
                }
                if (AuthoringTrialSource.Encode(snapshot, levelId) != original) throw new Exception("추천 시험이 JSON 원본을 변경함");
                UnityEngine.Debug.Log("PASS JSON 카탈로그 원본 해제 후 재구성 · 이동 1회 실측 200판 대조 · 원본 이동 7회 유지");
                File.WriteAllText("Logs/GameAuthoringStage05/measured-balance-path.txt", directory);
                Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Tick; session?.Dispose();
            if (error != null) UnityEngine.Debug.LogException(error);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
