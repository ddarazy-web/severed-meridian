#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameScreen;
using Levels;
using UnityEngine;

namespace LevelAuthoring.Runtime
{
    public sealed class PackedPuzzleTrial
    {
        public PuzzlePlayRequest Request { get; }
        public string SourceHash { get; }
        internal PackedPuzzleTrial(PuzzlePlayRequest request, string sourceHash) { Request = request; SourceHash = sourceHash; }
    }

    public static class PackedPuzzlePlayAdapter
    {
        public static UniTask<PackedPuzzleTrial> CreateRequestAsync(int number, int seed, CancellationToken token)
            => CreateRequestAsync(number, seed, ContentPackAssetLoader.LoadAsync, token);

        internal static async UniTask<PackedPuzzleTrial> CreateRequestAsync(int number, int seed,
            Func<string, CancellationToken, UniTask<byte[]>> read, CancellationToken token)
        {
            ContentPackGeneration generation = null;
            // 실제 레벨 로더가 사용한 세대만 기록한다. 별도 재조회로 상태와 입력이 달라지지 않는다.
            var level = await LevelPackLoader.LoadAsync(number, async (address, cancellation) =>
            {
                byte[] bytes = await read(address, cancellation);
                if (address == ContentPackGenerationCodec.Address) generation = ContentPackGenerationCodec.Decode(bytes);
                return bytes;
            }, token);
            try { return new PackedPuzzleTrial(PuzzlePlayRequest.Capture(level, seed), generation.SourceHash); }
            finally
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(level);
                else UnityEngine.Object.DestroyImmediate(level);
            }
        }
    }
}
#endif
