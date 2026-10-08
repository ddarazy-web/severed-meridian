using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Levels;
using Simulation;
using UnityEditor;
using UnityEngine;
namespace Tutorial.Editor
{
    public static class TutorialLegacyConversionVerification
    {
        public static void Capture() => Execute(true);
        public static void Run() => Execute(false);
        private static void Execute(bool baseline)
        {
            var trace = new List<string>(); bool failed = false;
            try
            {
                for (int number = 1; number <= 4; number++)
                {
                    LevelDefinition asset = AssetDatabase.LoadAssetAtPath<LevelDefinition>($"Assets/Data/Levels/Level_{number:D2}.asset");
                    string original = JsonUtility.ToJson(asset);
                    LevelDefinition copy = LevelPackCodec.Copy(asset);
                    try { Replay("asset" + number, copy, trace); } finally { UnityEngine.Object.DestroyImmediate(copy); }
                    if (JsonUtility.ToJson(asset) != original) throw new Exception("원본 변경");
                }
                foreach (string version in new[] { "v3", "v4" })
                {
                    LevelDefinition level = LevelPackCodec.ReadLevel(File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-" + version + ".bytes"), 1);
                    try { Replay(version, level, trace); } finally { UnityEngine.Object.DestroyImmediate(level); }
                }
                LevelDefinition sample = TutorialSampleBoards.All.First(value => value.Id == "damage").CreateBoard();
                try
                {
                    LevelDefinition packed = LevelPackCodec.ReadLevel(LevelPackCodec.Snapshot(sample), sample.LevelNumber);
                    try { Replay("v5", packed, trace); } finally { UnityEngine.Object.DestroyImmediate(packed); }
                }
                finally { UnityEngine.Object.DestroyImmediate(sample); }
                string path = "Logs/Tutorial/Composer03/legacy-before.txt";
                if (baseline)
                { if (File.Exists(path)) throw new Exception("기존 비교 기준은 덮어쓰지 않음"); File.WriteAllLines(path, trace); }
                else if (!File.ReadAllLines(path).SequenceEqual(trace)) throw new Exception("이전/현재 재생 추적 불일치");
                trace.Add("PASS 실제1~4/팩3~5 · 행동/진행/보드/내구도/이동/공급/RNG 동일");
            }
            catch (Exception error) { failed = true; trace.Add("FAIL " + error); }
            Directory.CreateDirectory("Logs/Tutorial/Composer03"); File.WriteAllLines("Logs/Tutorial/Composer03/legacy-" + (baseline ? "baseline" : "comparison") + ".txt", trace);
            EditorApplication.Exit(failed ? 1 : 0);
        }
        private static void Replay(string name, LevelDefinition level, List<string> trace)
        {
            using TutorialBoardAdapter adapter = TutorialBoardAdapter.Prepare(level, StartingBoardBuilder.Build(level, level.Tutorial.seed).State);
            BoardActionExecutor executor = adapter.Executor;
            for (int round = 0; round < 60 && adapter.Progress.State != TutorialProgressState.Completed; round++)
            {
                for (int i = 0; executor.HasPendingCascade && i < 100; i++) adapter.ObserveCascade(executor.AdvanceCascade());
                adapter.Tick(true, false);
                TutorialProgressSnapshot step = adapter.Progress.Snapshot;
                if (step.State == TutorialProgressState.Completed) break;
                if (step.State == TutorialProgressState.Error || step.State == TutorialProgressState.Cancelled) throw new Exception(name + ": " + step.Message);
                trace.Add(name + ":" + step.StepIndex + ":" + step.Kind + ":" + step.First + ":" + step.Second);
                TutorialInput input = step.State == TutorialProgressState.AwaitDescription ? TutorialInput.Next() :
                    step.Item.HasValue ? TutorialInput.UseItem(step.Item.Value, step.First, step.Second) : TutorialInput.Swap(step.First.Value, step.Second.Value);
                if (!adapter.TryBegin(input)) throw new Exception("승인 실패");
                if (input.Kind != TutorialInputKind.Next)
                {
                    bool success = input.Kind == TutorialInputKind.Item ? ((ItemUseResult)typeof(BoardActionExecutor).GetMethod("UseApprovedFreeItem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(executor, new object[] { input.Item.Value, input.First, input.Second })).IsApplied : executor.Swap(input.First.Value, input.Second.Value).IsApplied;
                    adapter.ReportAction(success); if (!success) throw new Exception("실행 실패");
                    for (int i = 0; executor.HasPendingCascade && i < 100; i++) adapter.ObserveCascade(executor.AdvanceCascade());
                }
                trace.Add("board:" + string.Join(";", executor.State.Cells.Select(cell => $"{cell.Coordinate}/{cell.Content}/{cell.Color}/{cell.RocketDirection}/{cell.CoverDurability}/{cell.DustDurability}")));
                trace.Add("targets:" + string.Join(";", TutorialTargetQuery.Capture(executor.State).Select(target => $"{target.Definition.Id}/{target.Durability}/{string.Join(",", target.Cells)}")));
                trace.Add("supply:" + string.Join(";", executor.State.Supply.Sources.Select(source => $"{source.Coordinate}/{source.ItemIndex}/{source.ItemConsumed}")));
                trace.Add($"moves:{executor.State.MovesRemaining}/rng:{executor.State.Random.DrawCount}/mission:{string.Join(",", executor.State.Missions.Select(mission => mission.Remaining))}");
                adapter.Tick(true, false);
            }
            if (adapter.Progress.State != TutorialProgressState.Completed) throw new Exception(name + " 미완료");
            trace.Add(name + ":completed");
        }
    }
}
