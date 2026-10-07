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
        private readonly Dictionary<(string name, int columns, int rows, int frame), Sprite> frames = new Dictionary<(string, int, int, int), Sprite>();
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

        public Sprite GetFrame(string name, int columns, int rows, int frame)
        {
            if (columns == 1 && rows == 1) return Get(name);
            if (columns < 1 || rows < 1 || frame < 0 || frame >= (long)columns * rows)
                throw new ArgumentOutOfRangeException(nameof(frame));
            Sprite source = Get(name);
            if (source == null) return null;
            (string name, int columns, int rows, int frame) key = (name, columns, rows, frame);
            if (frames.TryGetValue(key, out Sprite cached)) return cached;
            if (source.packed && (source.packingRotation != SpritePackingRotation.None || source.packingMode != SpritePackingMode.Rectangle))
                throw new InvalidOperationException("시트 프레임에는 회전 없는 사각 패킹이 필요합니다: " + name);
            // 아틀라스 텍스처를 공유한다. 등록 프레임은 준비 시 한 번 만들고 같은 소유자가 반환한다.
            Rect rectangle = SheetRectangle(source);
            float width = rectangle.width / columns, height = rectangle.height / rows;
            Rect selected = new Rect(rectangle.x + frame % columns * width,
                rectangle.y + (rows - 1 - frame / columns) * height, width, height);
            Sprite result = Sprite.Create(source.texture, selected, new Vector2(.5f, .5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            result.name = source.name + "#frame-" + columns + "-" + rows + "-" + frame;
            frames.Add(key, result);
            return result;
        }

        private static Rect SheetRectangle(Sprite source)
        {
            // textureRect는 타이트 메시의 투명 여백을 제외할 수 있어 원래 시트 칸 경계가 아니다.
            // 정점의 원본 픽셀 좌표와 UV 대응으로 회전 없는 전체 사각형을 복원한다.
            Vector2[] vertices = source.vertices, uv = source.uv;
            int left = 0, right = 0, bottom = 0, top = 0;
            for (int i = 1; i < vertices.Length; i++)
            {
                if (vertices[i].x < vertices[left].x) left = i;
                if (vertices[i].x > vertices[right].x) right = i;
                if (vertices[i].y < vertices[bottom].y) bottom = i;
                if (vertices[i].y > vertices[top].y) top = i;
            }
            float scaleX = (uv[right].x - uv[left].x) * source.texture.width / ((vertices[right].x - vertices[left].x) * source.pixelsPerUnit);
            float scaleY = (uv[top].y - uv[bottom].y) * source.texture.height / ((vertices[top].y - vertices[bottom].y) * source.pixelsPerUnit);
            float x = uv[left].x * source.texture.width - (vertices[left].x * source.pixelsPerUnit + source.pivot.x) * scaleX;
            float y = uv[bottom].y * source.texture.height - (vertices[bottom].y * source.pixelsPerUnit + source.pivot.y) * scaleY;
            return new Rect(Mathf.Round(x), Mathf.Round(y), Mathf.Round(source.rect.width * scaleX), Mathf.Round(source.rect.height * scaleY));
        }

        private void OnAtlasRequested(string tag, Action<SpriteAtlas> callback)
        {
            if (tag == address) BindAsync(callback).Forget(Debug.LogException);
        }

        private async UniTask BindAsync(Action<SpriteAtlas> callback)
        {
            try { await LoadAsync(); }
            catch (ObjectDisposedException) { return; }
            if (!disposed) callback(handle.Result);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SpriteAtlasManager.atlasRequested -= OnAtlasRequested;
            foreach (Sprite sprite in frames.Values)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(sprite);
                else UnityEngine.Object.DestroyImmediate(sprite);
            }
            frames.Clear();
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
