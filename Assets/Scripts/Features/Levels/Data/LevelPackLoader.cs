using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Elements;

namespace Levels
{
    public static class LevelPackLoader
    {
        // 반환한 메모리 레벨은 호출자가 사용을 마친 뒤 Destroy한다. 번들 핸들은 여기서 해제한다.
        public static UniTask<LevelDefinition> LoadAsync(int number, CancellationToken token = default)
            => LoadAsync(number, ContentPackAssetLoader.LoadAsync, token);

        internal static async UniTask<LevelDefinition> LoadAsync(int number,
            Func<string, CancellationToken, UniTask<byte[]>> read, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var generation = ContentPackGenerationCodec.Decode(await read(ContentPackGenerationCodec.Address, token));
            ElementContentData content = await ElementContentPackLoader.LoadAsync(generation, read, token);
            string address = LevelPackCodec.Address(number);
            byte[] bytes = await read(address, token);
            token.ThrowIfCancellationRequested();
            ContentPackGenerationCodec.Verify(generation, address, bytes);
            return LevelPackCodec.ReadLevel(bytes, number, content);
        }
    }
}
