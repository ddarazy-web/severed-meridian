using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Elements;
using Elements.Editor;
using LevelAuthoring.Storage;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class GenerationLoaderVerification
    {
        public static async void Run()
        {
            try
            {
                var method = typeof(LevelPackLoader).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                    .SingleOrDefault(value => value.Name == "LoadAsync" && value.GetParameters().Length == 3);
                Check(method != null, "runtime loader accepts one pack byte source");
                var snapshot = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage06/latest-candidate.txt")).Read().Snapshot;
                var outputs = JsonContentPackBuild.CreateBytes(snapshot);
                int number = (int)snapshot.Documents.First(doc => doc.Kind == "level").Data["levelNumber"];
                var bytes = new Dictionary<string, byte[]>
                {
                    [ContentPackGenerationCodec.Address] = outputs[JsonContentPackBuild.GenerationPath],
                    [ElementContentPackCodec.Address] = outputs[ElementContentPackBuild.OutputPath],
                    [LevelPackCodec.Address(number)] = outputs[LevelPackBuild.FilePath(number)]
                };
                var reads = new List<string>();
                Func<string, CancellationToken, UniTask<byte[]>> read = (address, token) =>
                { token.ThrowIfCancellationRequested(); reads.Add(address); return UniTask.FromResult(bytes[address]); };
                var loaded = await (UniTask<LevelDefinition>)method.Invoke(null, new object[] { number, read, CancellationToken.None });
                UnityEngine.Object.DestroyImmediate(loaded);
                Check(reads.SequenceEqual(new[] { ContentPackGenerationCodec.Address, ElementContentPackCodec.Address, LevelPackCodec.Address(number) }), "one manifest precedes both packs");
                foreach (string address in new[] { ElementContentPackCodec.Address, LevelPackCodec.Address(number) })
                {
                    byte[] original = bytes[address]; bytes[address] = (byte[])original.Clone(); bytes[address][0] ^= 1;
                    try
                    {
                        await (UniTask<LevelDefinition>)method.Invoke(null, new object[] { number, read, CancellationToken.None });
                        throw new Exception("mixed pack accepted");
                    }
                    catch (InvalidOperationException error) when (error.Message.Contains("제작 세대")) { }
                    finally { bytes[address] = original; }
                }
                reads.Clear();
                using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
                try
                {
                    await (UniTask<LevelDefinition>)method.Invoke(null, new object[] { number, read, cancellation.Token });
                    throw new Exception("cancelled load accepted");
                }
                catch (OperationCanceledException) { }
                Check(reads.Count == 0, "cancelled load acquires no assets");
                Debug.Log("PASS runtime generation loader"); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void Check(bool value, string text) { if (!value) throw new Exception("FAIL " + text); Debug.Log("PASS " + text); }
    }
}

