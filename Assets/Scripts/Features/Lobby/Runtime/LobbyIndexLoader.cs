using System;
using System.Collections.ObjectModel;
using System.Threading;
using Cysharp.Threading.Tasks;
using Levels;

namespace Lobby
{
    public sealed class LobbyLevel
    {
        public string Id { get; }
        public int Number { get; }
        public string Name { get; }
        internal LobbyLevel(LevelLobbyEntry entry)
        { Id = entry.LevelId; Number = entry.LevelNumber; Name = entry.DisplayName; }
    }

    public sealed class LobbyIndexSnapshot
    {
        public string Generation { get; }
        public ReadOnlyCollection<LobbyLevel> Levels { get; }
        internal LobbyIndexSnapshot(string generation, LevelLobbyIndex index)
        {
            Generation = generation;
            LobbyLevel[] levels = new LobbyLevel[index.Entries.Length];
            for (int i = 0; i < levels.Length; i++) levels[i] = new LobbyLevel(index.Entries[i]);
            Levels = Array.AsReadOnly(levels);
        }
    }

    public static class LobbyIndexLoader
    {
        public static UniTask<LobbyIndexSnapshot> LoadAsync(CancellationToken token = default)
            => LoadAsync(ContentPackAssetLoader.LoadAsync, token);

        internal static async UniTask<LobbyIndexSnapshot> LoadAsync(
            Func<string, CancellationToken, UniTask<byte[]>> read, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ContentPackGeneration generation = ContentPackGenerationCodec.Decode(
                await read(ContentPackGenerationCodec.Address, token));
            token.ThrowIfCancellationRequested();
            if (Array.IndexOf(generation.Addresses, LevelLobbyIndexCodec.Address) < 0)
                throw new InvalidOperationException("레벨 목록이 없습니다. 제작 데이터를 갱신해 주세요.");
            byte[] bytes = await read(LevelLobbyIndexCodec.Address, token);
            token.ThrowIfCancellationRequested();
            ContentPackGenerationCodec.Verify(generation, LevelLobbyIndexCodec.Address, bytes);
            return new LobbyIndexSnapshot(generation.SourceHash, LevelLobbyIndexCodec.Decode(bytes));
        }
    }
}
