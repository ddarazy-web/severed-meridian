using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Levels;

namespace Elements
{
    public static class ElementContentPackLoader
    {
        public static async UniTask<ElementContentData> LoadAsync(CancellationToken token = default)
        {
            var generation = ContentPackGenerationCodec.Decode(await ContentPackAssetLoader.LoadAsync(ContentPackGenerationCodec.Address, token));
            return await LoadAsync(generation, ContentPackAssetLoader.LoadAsync, token);
        }

        // 레벨 로더가 확보한 세대를 그대로 사용하여 두 팩 사이의 재조회 혼합을 막는다.
        internal static async UniTask<ElementContentData> LoadAsync(ContentPackGeneration generation,
            Func<string, CancellationToken, UniTask<byte[]>> read, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            byte[] bytes = await read(ElementContentPackCodec.Address, token);
            token.ThrowIfCancellationRequested();
            ContentPackGenerationCodec.Verify(generation, ElementContentPackCodec.Address, bytes);
            return ElementContentPackCodec.Decode(bytes);
        }
    }
}
