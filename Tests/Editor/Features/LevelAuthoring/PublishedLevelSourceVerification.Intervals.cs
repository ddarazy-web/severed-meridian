using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Levels;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static partial class PublishedLevelSourceVerification
    {
        public static async void RunIntervals()
        {
            string output = "Logs/LobbyStage03/intervals-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            Directory.CreateDirectory(output);
            var result = new List<string>();
            LevelDefinition fixture = null;
            PublishedLevelSource source = null;
            int exit = 1;
            try
            {
                byte[] first = File.ReadAllBytes("Assets/Data/LevelPacks/levels-000001.bytes");
                fixture = LevelPackCodec.ReadLevel(first, 1);
                JsonUtility.FromJsonOverwrite("{\"levelNumber\":51}", fixture);
                var packs = new Dictionary<string, byte[]>
                {
                    [LevelPackCodec.Address(1)] = first,
                    [LevelPackCodec.Address(51)] = LevelPackCodec.Snapshot(fixture),
                    [Elements.ElementContentPackCodec.Address] = File.ReadAllBytes(Elements.Editor.ElementContentPackBuild.OutputPath)
                };
                string generation = new string('a', 64);
                packs[ContentPackGenerationCodec.Address] = ContentPackGenerationCodec.Encode(generation, packs);
                var reads = new Dictionary<string, int>();
                Func<string, CancellationToken, UniTask<byte[]>> read = (address, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    reads[address] = reads.TryGetValue(address, out int count) ? count + 1 : 1;
                    return UniTask.FromResult(packs[address]);
                };
                source = (PublishedLevelSource)Activator.CreateInstance(typeof(PublishedLevelSource), BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { generation, read }, null);
                FieldInfo retained = typeof(PublishedLevelSource).GetField("current", BindingFlags.Instance | BindingFlags.NonPublic);
                object initialPack = null;
                foreach (int number in new[] { 1, 2, 51, 1 })
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    LevelDefinition level = await source.LoadAsync(number, CancellationToken.None);
                    timer.Stop();
                    try { Check(level.LevelNumber == number, "Wrong interval level"); }
                    finally { UnityEngine.Object.DestroyImmediate(level); }
                    object current = retained.GetValue(source);
                    if (number == 2) Check(ReferenceEquals(current, initialPack) && source.LastLoadTiming.ReusedPack, "Same interval decoded twice");
                    if (number == 51) Check(!ReferenceEquals(current, initialPack) && !source.LastLoadTiming.ReusedPack, "New interval did not replace cache");
                    if (number == 1 && initialPack == null) initialPack = current;
                    result.Add($"PASS level={number}; loadMs={timer.Elapsed.TotalMilliseconds:F3}; reused={source.LastLoadTiming.ReusedPack}; retainedFirst={((PublishedLevelPack)current).FirstLevel}");
                }
                Check(reads[LevelPackCodec.Address(1)] == 2 && reads[LevelPackCodec.Address(51)] == 1, "Old interval retained or new interval prefetched");
                Check(reads[ContentPackGenerationCodec.Address] == 1 && reads[Elements.ElementContentPackCodec.Address] == 1 && reads.Count == 4, "Unexpected common reads");
                source.Dispose();
                Check(retained.GetValue(source) == null, "Disposed source retains pack");
                result.Add("PASS only one retained interval; returning to evicted interval rereads it; metadata/common read once; disposal clears retained pack; all fixtures in memory");
                exit = 0;
            }
            catch (Exception error) { result.Add("FAIL " + error); }
            finally
            {
                source?.Dispose();
                if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
                File.WriteAllLines(Path.Combine(output, "result.txt"), result);
                EditorApplication.Exit(exit);
            }
        }
    }
}
