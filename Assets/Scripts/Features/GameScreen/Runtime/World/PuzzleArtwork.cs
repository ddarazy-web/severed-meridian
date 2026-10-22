using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Simulation;
using UnityEngine;
using Elements;

namespace GameScreen
{
    public sealed class PuzzleArtwork : IDisposable
    {
        private readonly Dictionary<string, BoardSpriteAtlas> atlases = new Dictionary<string, BoardSpriteAtlas>();
        private bool disposed;
        private int pending;
        public int AtlasCount => atlases.Count;
        public ElementVisualLookup Visuals { get; }
        public PuzzleArtwork() : this(LegacyElementVisuals.Catalog) { }
        public PuzzleArtwork(ElementVisualCatalog catalog) { Visuals = new ElementVisualLookup(catalog); }
        public Sprite GetVisual(ElementVisualFrame frame)
        {
            if (frame == null) return null;
            Get(frame.Path);
            return atlases[BoardSpriteAtlas.AddressFor(frame.Path)].GetFrame(frame.Path.Substring(frame.Path.LastIndexOf('/') + 1),
                frame.SheetColumns, frame.SheetRows, frame.SheetFrame);
        }

        public async UniTask PrepareAsync(LevelRuntimeState state, CancellationToken cancellationToken)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PuzzleArtwork));
            ElementResourcePlan plan = ElementResourcePlan.Create(state, Visuals.Resolver.Catalog);
            await LoadAsync(plan.Addresses, cancellationToken);
            plan.ValidateResources(path => Get(path) != null);
            plan.ValidateFrames(frame => GetVisual(frame) != null);
        }

        internal UniTask PrepareEffectsAsync(IEnumerable<string> paths, CancellationToken cancellationToken)
            => LoadAsync(paths.Where(path => path != null).Select(BoardSpriteAtlas.AddressFor).Distinct(), cancellationToken);

        private async UniTask LoadAsync(IEnumerable<string> addresses, CancellationToken cancellationToken)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PuzzleArtwork));
            pending++;
            try
            {
                var batch = new List<UniTask<Exception>>(4);
                foreach (string address in addresses)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (disposed) throw new OperationCanceledException();
                    if (!atlases.TryGetValue(address, out BoardSpriteAtlas atlas))
                    {
                        atlas = new BoardSpriteAtlas(address);
                        atlases.Add(address, atlas);
                    }
                    batch.Add(LoadAtlasAsync(atlas));
                    if (batch.Count == 4)
                    {
                        await CompleteBatchAsync(batch);
                        batch.Clear();
                    }
                }
                if (batch.Count > 0) await CompleteBatchAsync(batch);
                cancellationToken.ThrowIfCancellationRequested();
                if (disposed) throw new OperationCanceledException();
            }
            finally
            {
                pending--;
                if (disposed && pending == 0) Release();
            }
        }

        private static async UniTask<Exception> LoadAtlasAsync(BoardSpriteAtlas atlas)
        {
            try { await atlas.LoadAsync(); return null; }
            catch (Exception error) { return error; }
        }

        private static async UniTask CompleteBatchAsync(List<UniTask<Exception>> batch)
        {
            // 한 요청이 실패해도 같은 묶음의 모든 핸들이 끝난 뒤 실패를 전달한다.
            // 취소/Dispose가 아직 실행 중인 Addressables 핸들을 먼저 해제하지 않게 한다.
            Exception[] errors = await UniTask.WhenAll(batch);
            foreach (Exception error in errors)
                if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        }

        public Sprite Get(string path)
        {
            if (path == null) return null;
            if (disposed) throw new ObjectDisposedException(nameof(PuzzleArtwork));
            string name = path.Substring(path.LastIndexOf('/') + 1);
            Sprite sprite = atlases.TryGetValue(BoardSpriteAtlas.AddressFor(path), out BoardSpriteAtlas atlas) ? atlas.Get(name) : null;
            if (sprite == null) throw new InvalidOperationException("월드 보드 이미지가 준비되지 않았습니다: " + path);
            return sprite;
        }
        public void Dispose()
        {
            disposed = true;
            if (pending == 0) Release();
        }
        private void Release()
        {
            foreach (BoardSpriteAtlas atlas in atlases.Values) atlas.Dispose();
            atlases.Clear();
        }
    }
}
