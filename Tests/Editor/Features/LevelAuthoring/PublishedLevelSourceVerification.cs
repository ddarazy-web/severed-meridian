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
        public static async void Run()
        {
            string root = "Logs/LobbyStage02/source-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(root);
            int exit = 1;
            try
            {
                Type type = typeof(LevelPackCodec).Assembly.GetType("Levels.PublishedLevelSource");
                Check(type != null, "Scoped published source missing");
                byte[] generation = File.ReadAllBytes("Assets/Data/LevelPacks/content-generation.bytes");
                string hash = ContentPackGenerationCodec.Decode(generation).SourceHash;
                var reads = new List<string>();
                bool damagePack = false;
                Func<string, CancellationToken, UniTask<byte[]>> read = (address, token) =>
                {
                    token.ThrowIfCancellationRequested(); reads.Add(address);
                    string path = address == ContentPackGenerationCodec.Address ? "Assets/Data/LevelPacks/content-generation.bytes" :
                        address == Elements.ElementContentPackCodec.Address ? Elements.Editor.ElementContentPackBuild.OutputPath :
                        address == LevelPackCodec.Address(1) ? "Assets/Data/LevelPacks/levels-000001.bytes" : throw new Exception("Unexpected pack " + address);
                    byte[] bytes = File.ReadAllBytes(path);
                    if (damagePack && address == LevelPackCodec.Address(1)) bytes[bytes.Length - 1] ^= 1;
                    return UniTask.FromResult(bytes);
                };
                object Create(string expected) => Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { expected, read }, null);
                async UniTask<LevelDefinition> Load(object source, int number, CancellationToken token = default)
                    => await (UniTask<LevelDefinition>)type.GetMethod("LoadAsync").Invoke(source, new object[] { number, token });
                object source = Create(hash);
                try
                {
                    foreach (int number in new[] { 1, 2, 15, 1 })
                    {
                        LevelDefinition level = await Load(source, number);
                        Check(level.LevelNumber == number, "Wrong level"); UnityEngine.Object.DestroyImmediate(level);
                    }
                    Check(reads.Count == 3 && reads[0] == ContentPackGenerationCodec.Address && reads[1] == Elements.ElementContentPackCodec.Address && reads[2] == LevelPackCodec.Address(1), "Same interval reloaded or unexpected data read");
                    bool cancelled = false;
                    try { await Load(source, 1, new CancellationToken(true)); } catch (OperationCanceledException) { cancelled = true; }
                    Check(cancelled && reads.Count == 3, "Cancelled request read assets");
                }
                finally { ((IDisposable)source).Dispose(); }
                reads.Clear(); source = Create("different-generation");
                bool rejected = false;
                try { await Load(source, 1); } catch (InvalidOperationException) { rejected = true; }
                finally { ((IDisposable)source).Dispose(); }
                Check(rejected && reads.Count == 1, "Mismatched lobby generation loaded game packs");
                reads.Clear(); source = Create(hash); damagePack = true;
                bool damaged = false;
                try
                {
                    try { await Load(source, 1); } catch (InvalidOperationException) { damaged = true; }
                    Check(damaged && reads.Count == 3, "Damaged pack hash accepted");
                    damagePack = false;
                    LevelDefinition retried = await Load(source, 1);
                    try { Check(retried.LevelNumber == 1 && reads.Count == 4, "Retry after damaged pack failed or reread common data"); }
                    finally { UnityEngine.Object.DestroyImmediate(retried); }
                }
                finally { ((IDisposable)source).Dispose(); }
                File.WriteAllText(Path.Combine(root, "result.txt"), "PASS generation gate; three initial reads; same interval reuse; cancellation before read; corrupted pack hash rejected; corrected retry succeeds"); exit = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(root, "result.txt"), "FAIL " + error); Debug.LogException(error); }
            EditorApplication.Exit(exit);
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
