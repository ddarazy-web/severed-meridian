using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Levels;
using UnityEditor;
using UnityEngine;
namespace LevelAuthoring.Editor
{
    public static class LobbyLoaderVerification
    {
        public static async void Run()
        {
            string root = "Logs/LobbyStage01/loader-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(root);
            int exit = 1;
            try
            {
                Type loader = typeof(LevelPackCodec).Assembly.GetType("Lobby.LobbyIndexLoader");
                Check(loader != null, "Lobby index-only loader missing");
                MethodInfo method = loader.GetMethods(BindingFlags.Static | BindingFlags.NonPublic).Single(value => value.Name == "LoadAsync");
                byte[] bytes = LevelLobbyIndexCodec.Encode(new[] {
                    new LevelLobbyEntry { LevelId = "b", LevelNumber = 9, DisplayName = "한글 이름" },
                    new LevelLobbyEntry { LevelId = "a", LevelNumber = 1, DisplayName = "" } });
                byte[] generation = ContentPackGenerationCodec.Encode(new string('a', 64), new Dictionary<string, byte[]> { [LevelLobbyIndexCodec.Address] = bytes });
                var reads = new List<string>();
                Func<string, CancellationToken, UniTask<byte[]>> read = (address, token) =>
                { token.ThrowIfCancellationRequested(); reads.Add(address); return UniTask.FromResult(address == ContentPackGenerationCodec.Address ? generation : address == LevelLobbyIndexCodec.Address ? bytes : throw new Exception("Forbidden game resource " + address)); };
                // 구체 반환 타입 없이 완료·예외를 기다리는 검사 경계.
                async UniTask<Lobby.LobbyIndexSnapshot> Invoke(CancellationToken token)
                {
                    return await (UniTask<Lobby.LobbyIndexSnapshot>)method.Invoke(null, new object[] { read, token });
                }
                var loaded = await Invoke(CancellationToken.None);
                Check(loaded.Generation == new string('a', 64) && loaded.Levels[0].Id == "a" && loaded.Levels[1].Number == 9, "Selection metadata lost");
                Check(reads.SequenceEqual(new[] { ContentPackGenerationCodec.Address, LevelLobbyIndexCodec.Address }), "Read other resources");
                Check(LevelLobbyIndexCodec.Decode(bytes).Entries[0].LevelNumber == 1, "Not ordered");
                Check(LevelLobbyIndexCodec.Decode(LevelLobbyIndexCodec.Encode(Array.Empty<LevelLobbyEntry>())).Entries.Length == 0, "Empty rejected");
                bool rejected = false;
                bytes[0] ^= 1;
                try { await Invoke(CancellationToken.None); } catch { rejected = true; }
                Check(rejected, "Corruption accepted");
                reads.Clear(); rejected = false;
                try { await Invoke(new CancellationToken(true)); } catch { rejected = true; }
                Check(rejected && reads.Count == 0, "Cancelled load read data");
                bytes[0] ^= 1;
                generation = ContentPackGenerationCodec.Encode(new string('b', 64), new Dictionary<string, byte[]> { ["Levels/levels-00001-00050"] = bytes });
                reads.Clear(); rejected = false;
                try { await Invoke(CancellationToken.None); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && reads.SequenceEqual(new[] { ContentPackGenerationCodec.Address }), "Missing index scanned other packs");
                generation = ContentPackGenerationCodec.Encode(new string('a', 64), new Dictionary<string, byte[]> { [LevelLobbyIndexCodec.Address] = bytes });
                bytes = LevelLobbyIndexCodec.Encode(new[] { new LevelLobbyEntry { LevelId = "new-generation", LevelNumber = 1, DisplayName = "다른 세대" } });
                rejected = false;
                try { await Invoke(CancellationToken.None); } catch { rejected = true; }
                Check(rejected, "Index from another publication accepted");
                foreach (var invalid in new[] {
                    new LevelLobbyIndex { FormatVersion = 99, Entries = Array.Empty<LevelLobbyEntry>() },
                    new LevelLobbyIndex { Entries = new[] { new LevelLobbyEntry { LevelId="a", LevelNumber=1, DisplayName="" }, new LevelLobbyEntry { LevelId="a", LevelNumber=2, DisplayName="" } } },
                    new LevelLobbyIndex { Entries = new[] { new LevelLobbyEntry { LevelId="a", LevelNumber=1, DisplayName="" }, new LevelLobbyEntry { LevelId="b", LevelNumber=1, DisplayName="" } } }
                })
                {
                    rejected = false;
                    try { LevelLobbyIndexCodec.Decode(MemoryPack.MemoryPackSerializer.Serialize(invalid)); } catch (ArgumentException) { rejected = true; }
                    Check(rejected, "Invalid version/duplicate metadata accepted");
                }
                using (var cancellation = new CancellationTokenSource())
                {
                    reads.Clear();
                    read = (address, token) => { reads.Add(address); cancellation.Cancel(); return UniTask.FromResult(generation); };
                    rejected = false;
                    try { await Invoke(cancellation.Token); } catch (OperationCanceledException) { rejected = true; }
                    Check(rejected && reads.Count == 1, "Cancellation after generation continued loading");
                }
                File.WriteAllText(Path.Combine(root, "result.txt"), "PASS only two addresses; source IDs/generation; sorted/empty; corruption; missing index without scan; mixed publication; version/duplicate rejection; pre/mid cancellation"); exit = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(root, "result.txt"), "FAIL " + error); Debug.LogException(error); }
            EditorApplication.Exit(exit);
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
