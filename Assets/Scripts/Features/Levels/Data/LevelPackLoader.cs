using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Threading;
using Elements;

namespace Levels
{
    public static class LevelPackLoader
    {
        // 반환한 메모리 레벨은 호출자가 사용을 마친 뒤 Destroy한다. 번들 핸들은 여기서 해제한다.
        public static async UniTask<LevelDefinition> LoadAsync(int number, CancellationToken token = default)
        {
            ElementContentData content = await ElementContentPackLoader.LoadAsync(token);
            var handle = Addressables.LoadAssetAsync<TextAsset>(LevelPackCodec.Address(number));
            try
            {
                TextAsset resource = await handle.ToUniTask(cancellationToken: token);
                token.ThrowIfCancellationRequested();
                return LevelPackCodec.ReadLevel(resource.bytes, number, content);
            }
            finally { if (handle.IsValid()) Addressables.Release(handle); }
        }
    }
}
