// [UNITY-SKILL:SPRITEATLAS]
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;

namespace Board
{
    /// <summary>보드가 사용하는 동안만 아틀라스 한 개를 유지한다. 소유자는 사용 종료 시 Dispose한다.</summary>
    public sealed class BoardSpriteAtlas : IDisposable
    {
        public const string Prefix = "MoonRabbitBoard-";
        private readonly string address;
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private AsyncOperationHandle<SpriteAtlas> handle;
        private bool requested;
        private bool disposed;
        public bool IsLoaded => !disposed && requested && handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded;

        public BoardSpriteAtlas(string address)
        {
            this.address = address;
            SpriteAtlasManager.atlasRequested += OnAtlasRequested;
        }

        public static string AddressFor(string relativePath)
        {
            string[] parts = relativePath.Replace('\\', '/').Split('/');
            bool byKind = parts[0] == "Obstacles" || parts[0] == "BoardTerrain" || parts[0] == "BoardDevices" || parts[0] == "Effects";
            return Prefix + (byKind ? parts[0] + "-" + parts[1] : parts[0]);
        }

        public async UniTask LoadAsync()
        {
            if (disposed) throw new ObjectDisposedException(nameof(BoardSpriteAtlas));
            if (!requested)
            {
                handle = Addressables.LoadAssetAsync<SpriteAtlas>(address);
                requested = true;
            }
#if UNITY_EDITOR
            // 편집 모드의 로컬 아틀라스는 게임 루프 갱신 없이도 미리보기에 사용할 수 있어야 한다.
            // 최초 요청에서만 완료시키며 Play Mode와 플레이어는 기존 비동기 로드를 유지한다.
            if (!Application.isPlaying && !handle.IsDone) handle.WaitForCompletion();
#endif
            await handle.ToUniTask();
            if (disposed) throw new ObjectDisposedException(nameof(BoardSpriteAtlas));
        }

        public Sprite Get(string name)
        {
            if (!IsLoaded) return null;
            if (!sprites.TryGetValue(name, out Sprite sprite))
            {
                sprite = handle.Result.GetSprite(name);
                if (sprite != null) sprites.Add(name, sprite);
            }
            return sprite;
        }

        private void OnAtlasRequested(string tag, Action<SpriteAtlas> callback)
        {
            if (tag == address) BindAsync(callback).Forget(Debug.LogException);
        }

        private async UniTask BindAsync(Action<SpriteAtlas> callback)
        {
            await LoadAsync();
            callback(handle.Result);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SpriteAtlasManager.atlasRequested -= OnAtlasRequested;
            foreach (Sprite sprite in sprites.Values)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(sprite);
                else UnityEngine.Object.DestroyImmediate(sprite);
            }
            sprites.Clear();
            if (requested && handle.IsValid()) Addressables.Release(handle);
        }
    }
}
