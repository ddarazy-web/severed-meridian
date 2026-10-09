using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Levels
{
    internal static class ContentPackAssetLoader
    {
        // 바이트만 반환하고 성공·실패·취소 모두 Addressables 핸들을 해제한다.
        internal static async UniTask<byte[]> LoadAsync(string address, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var handle = Addressables.LoadAssetAsync<TextAsset>(address);
            try
            {
                TextAsset asset = await handle.ToUniTask(cancellationToken: token);
                token.ThrowIfCancellationRequested();
                return asset.bytes;
            }
            finally { if (handle.IsValid()) Addressables.Release(handle); }
        }
    }
}
