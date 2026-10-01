using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    public sealed class PuzzleArtwork : IDisposable
    {
        private readonly Dictionary<string, BoardSpriteAtlas> atlases = new Dictionary<string, BoardSpriteAtlas>();
        private bool disposed;
        private int pending;
        public int AtlasCount => atlases.Count;

        public async UniTask PrepareAsync(LevelRuntimeState state, CancellationToken cancellationToken)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PuzzleArtwork));
            HashSet<string> addresses = new HashSet<string>();
            // 기본 파워는 초기 배치에 없어도 매칭 결과로 생길 수 있다.
            string[] common = { "Blocks/", "PowerBlocks/", PuzzleArtworkPaths.Floor };
            foreach (string path in common) addresses.Add(BoardSpriteAtlas.AddressFor(path));
            foreach (RuntimeMission mission in state.Missions) Add(PuzzleArtworkPaths.Mission(mission.Definition));
            // 초기판에 없어도 생성구에서 나올 수 있는 종류를 공급 정의에서 준비한다.
            if (state.Supply.Sources.Any(source => source.Mode == SupplyMode.MaintainScrap || source.Items.Any(item => item.Kind == SupplyKind.Scrap)))
                Add("Obstacles/Scrap/");
            if (state.Supply.Sources.Any(source => source.Mode == SupplyMode.MaintainRecovery || source.Items.Any(item => item.Kind == SupplyKind.Recovery)))
                Add(PuzzleArtworkPaths.Recovery);
            foreach (RuntimeCell cell in state.Cells)
            {
                Add(PuzzleArtworkPaths.Content(cell)); Add(PuzzleArtworkPaths.Cover(cell)); Add(PuzzleArtworkPaths.Dust(cell.DustDurability));
                if (cell.Content == RuntimeContent.Obstacle && cell.ObstacleIndex.HasValue)
                    Add(PuzzleArtworkPaths.Obstacle(state.Obstacles[cell.ObstacleIndex.Value]));
            }
            if (state.Flow.Walls.Count > 0) Add(PuzzleArtworkPaths.Wall(false));
            if (state.Flow.Portals.Count > 0) Add(PuzzleArtworkPaths.Portal(0, false));
            if (state.Flow.Arrivals.Count > 0) Add(PuzzleArtworkPaths.Arrival);
            if (state.Connections.Count > 0) Add(PuzzleArtworkPaths.Wire(false));
            await LoadAsync(addresses, cancellationToken);
            void Add(string path) { if (path != null) addresses.Add(BoardSpriteAtlas.AddressFor(path)); }
        }

        internal UniTask PrepareEffectsAsync(IEnumerable<string> paths, CancellationToken cancellationToken)
            => LoadAsync(paths.Where(path => path != null).Select(BoardSpriteAtlas.AddressFor).Distinct(), cancellationToken);

        private async UniTask LoadAsync(IEnumerable<string> addresses, CancellationToken cancellationToken)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PuzzleArtwork));
            pending++;
            try
            {
                foreach (string address in addresses)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (disposed) throw new OperationCanceledException();
                    if (!atlases.TryGetValue(address, out BoardSpriteAtlas atlas))
                    {
                        atlas = new BoardSpriteAtlas(address);
                        atlases.Add(address, atlas);
                    }
                    // 로더가 핸들을 사용 중일 때 해제하지 않고 완료 후 소유권을 반환한다.
                    await atlas.LoadAsync();
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (disposed) throw new OperationCanceledException();
            }
            finally
            {
                pending--;
                if (disposed && pending == 0) Release();
            }
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
