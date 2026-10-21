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
                // Addressables 작업 자체는 취소할 수 없다. 완료 전에 awaiter를 반환하면
                // 늦은 완료 콜백이 재사용된 UniTask awaiter에 전달될 수 있으므로 완료 후 취소를 적용한다.
                TextAsset asset = await handle.ToUniTask();
                token.ThrowIfCancellationRequested();
                return asset.bytes;
            }
            finally { if (handle.IsValid()) Addressables.Release(handle); }
        }
    }
}
