using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Elements
{
    public static class ElementContentPackLoader
    {
        // 팩 안의 값만 반환한다. TextAsset과 Addressables 핸들의 소유권은 남기지 않는다.
        public static async UniTask<ElementContentData> LoadAsync(CancellationToken token = default)
        {
            var handle = Addressables.LoadAssetAsync<TextAsset>(ElementContentPackCodec.Address);
            try
            {
                TextAsset asset = await handle.ToUniTask(cancellationToken: token);
                token.ThrowIfCancellationRequested();
                return ElementContentPackCodec.Decode(asset.bytes);
            }
            finally { if (handle.IsValid()) Addressables.Release(handle); }
        }
    }
}
