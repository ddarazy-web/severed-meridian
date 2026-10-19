using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Elements;
using Elements.Editor;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using Levels;
using Simulation;
using Tutorial;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class ImportedPackPlayVerification
    {
        public static async void Run()
        {
            string root = Path.GetFullPath("Logs/LevelDataTransferStage03/play-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(root);
            try
            {
                string productAudit = Products.Editor.ProductProfiles.AuditAll();
                Check(Products.Editor.ProductProfiles.Definitions.All(product => productAudit.Contains(product.Name + " |")), "all game and tool product profiles audited without building");
                File.WriteAllText(Path.Combine(root, "product-audit.txt"), productAudit);
                var snapshot = new ContentSnapshotStore(AuthoringSourceSelection.ReadFolder(AuthoringSourceSelection.DefaultConfiguration)).Read().Snapshot;
                var outputs = JsonContentPackBuild.CreateBytes(snapshot);
                var bytes = outputs.ToDictionary(pair => pair.Key == ElementContentPackBuild.OutputPath ? ElementContentPackCodec.Address : "Levels/" + Path.GetFileNameWithoutExtension(pair.Key), pair => pair.Value);
                var loader = typeof(LevelPackLoader).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Single(method => method.Name == "LoadAsync" && method.GetParameters().Length == 3);
                Func<string, CancellationToken, UniTask<byte[]>> read = (address, token) => { token.ThrowIfCancellationRequested(); return UniTask.FromResult(bytes[address]); };
                using var graph = new AuthoringObjectGraph(snapshot.Documents);
                foreach (int number in new[] { 1, 3, 7 })
                {
                    var document = snapshot.Documents.Single(value => value.Kind == "level" && (int)value.Data["levelNumber"] == number);
                    var json = (LevelDefinition)graph.Resolve(document.Id);
                    var packed = await (UniTask<LevelDefinition>)loader.Invoke(null, new object[] { number, read, CancellationToken.None });
                    try
                    {
                        Check(LevelPackCodec.Snapshot(json).SequenceEqual(LevelPackCodec.Snapshot(packed)), "loader content " + number);
                        using var left = TutorialBoardAdapter.Prepare(json, StartingBoardBuilder.Build(json, json.Tutorial.seed).State);
                        using var right = TutorialBoardAdapter.Prepare(packed, StartingBoardBuilder.Build(packed, packed.Tutorial.seed).State);
                        Check(Capture(left.Executor) == Capture(right.Executor), "starting board and missions " + number);
                        Replay(left); Replay(right);
                        Check(left.IsReleased && right.IsReleased, "tutorial releases control " + number);
                        Check(left.Executor.Phase == BoardActionPhase.Ready && right.Executor.Phase == BoardActionPhase.Ready, "normal play ready " + number);
                        Check(Capture(left.Executor) == Capture(right.Executor), "completed tutorial board missions and supply " + number);
                        bool applied = false;
                        foreach (var cell in left.Executor.State.Cells)
                        {
                            foreach (var other in left.Executor.State.Cells.Where(value => Math.Abs(value.Coordinate.Row - cell.Coordinate.Row) + Math.Abs(value.Coordinate.Column - cell.Coordinate.Column) == 1))
                            {
                                if (!ActionQuery.Swap(left.Executor.State, cell.Coordinate, other.Coordinate).IsAllowed) continue;
                                Check(left.Executor.Swap(cell.Coordinate, other.Coordinate).IsApplied && right.Executor.Swap(cell.Coordinate, other.Coordinate).IsApplied, "normal swap applied " + number);
                                Drain(left); Drain(right);
                                Check(Capture(left.Executor) == Capture(right.Executor), "normal play result " + number);
                                applied = true; break;
                            }
                            if (applied) break;
                        }
                        Check(applied, "normal move available " + number);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(packed); }
                }
                File.WriteAllText(Path.Combine(root, "result.txt"), "PASS JSON and production MemoryPack loader play comparison levels 1,3,7; FAIL 0");
                Debug.Log("PASS imported play: " + root); EditorApplication.Exit(0);
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(root, "result.txt"), error.ToString()); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void Replay(TutorialBoardAdapter adapter)
        {
            for (int step = 0; step < 100 && adapter.Progress.State != TutorialProgressState.Completed; step++)
            {
                Drain(adapter); adapter.Tick(true, false);
                if (adapter.Progress.State == TutorialProgressState.Completed) break;
                var view = adapter.Progress.Snapshot;
                Check(!view.Item.HasValue, "representative tutorial uses swaps");
                var input = view.State == TutorialProgressState.AwaitDescription ? TutorialInput.Next() : TutorialInput.Swap(view.First.Value, view.Second.Value);
                Check(adapter.TryBegin(input), "tutorial action accepted");
                if (input.Kind != TutorialInputKind.Next)
                {
                    bool success = adapter.Executor.Swap(input.First.Value, input.Second.Value).IsApplied;
                    adapter.ReportAction(success); Check(success, "tutorial action applied"); Drain(adapter);
                }
                adapter.Tick(true, false);
            }
            Check(adapter.Progress.State == TutorialProgressState.Completed, "tutorial completed");
        }
        private static void Drain(TutorialBoardAdapter adapter)
        {
            for (int round = 0; adapter.Executor.HasPendingCascade && round < adapter.Executor.CascadeLimit * 2; round++) adapter.ObserveCascade(adapter.Executor.AdvanceCascade());
            Check(!adapter.Executor.HasPendingCascade, "cascade settled");
        }
        private static string Capture(BoardActionExecutor executor)
        {
            var state = executor.State;
            return state.MovesRemaining + "|" + string.Join(";", state.Cells.Select(cell => $"{cell.Coordinate.Row},{cell.Coordinate.Column}:{cell.IsActive}:{cell.Content}:{cell.Color}:{cell.RocketDirection}:{cell.Cover}:{cell.CoverDurability}:{cell.DustDurability}")) +
                "|" + string.Join(";", state.Missions.Select(mission => mission.Target + ":" + mission.Progress)) +
                "|" + string.Join(";", state.Supply.Sources.Select(source => source.Coordinate + ":" + source.Mode + ":" + source.ItemIndex));
        }
        private static void Check(bool condition, string text) { if (!condition) throw new Exception("FAIL " + text); Debug.Log("PASS " + text); }
    }
}
