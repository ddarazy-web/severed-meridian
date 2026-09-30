using System;
using System.IO;
using System.Collections.Generic;
using Board;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>편집 보드에서만 사용하는 확정 아트 매핑. 선택 메뉴와 실행 규칙에는 연결하지 않는다.</summary>
    internal static class LevelBoardArtwork
    {
        private static readonly string[] colors = { "pink", "yellow", "blue", "green", "purple" };
        private static readonly Dictionary<string, BoardSpriteAtlas> atlases = new Dictionary<string, BoardSpriteAtlas>();
        internal static int RequestedAtlasCount => atlases.Count;
        private static int users;
        internal static event Action Loaded;

        static LevelBoardArtwork() => AssemblyReloadEvents.beforeAssemblyReload += ReleaseAll;

        internal static async UniTask Warmup(string relativePath = "Blocks/")
        {
            string address = BoardSpriteAtlas.AddressFor(relativePath);
            if (!atlases.TryGetValue(address, out BoardSpriteAtlas atlas))
            {
                atlas = new BoardSpriteAtlas(address);
                atlases.Add(address, atlas);
            }
            await atlas.LoadAsync();
        }

        internal static void Acquire()
        {
            users++;
            Loaded?.Invoke();
        }

        private static async UniTask LoadAndNotify(string relativePath)
        {
            try
            {
                await Warmup(relativePath);
                Loaded?.Invoke();
            }
            catch (ObjectDisposedException) { } // 로드 도중 패널이 닫히면 다시 그리지 않는다.
        }

        internal static void Release()
        {
            if (users > 0 && --users == 0) ReleaseAll();
        }

        internal static void ReleaseAll()
        {
            foreach (BoardSpriteAtlas atlas in atlases.Values) atlas.Dispose();
            atlases.Clear();
        }

        internal static Sprite Rabbit(RabbitColor color) => (int)color >= 0 && (int)color < colors.Length
            ? Load("Blocks/rabbit-" + colors[(int)color] + "-v1-256.png") : null;

        internal static Sprite Block(InitialBlockDefinition block)
        {
            string file = block.Kind switch
            {
                InitialBlockKind.Rocket => block.RocketDirection == RocketDirection.Horizontal ? "cleaning-rocket-horizontal-v1" :
                    block.RocketDirection == RocketDirection.Vertical ? "cleaning-rocket-vertical-v1" : null,
                InitialBlockKind.Bomb => "moon-bomb-v1",
                InitialBlockKind.Drone => "collection-drone-v1",
                InitialBlockKind.Magnet => "rainbow-magnet-v1",
                _ => null
            };
            return file == null ? null : Load("PowerBlocks/" + file + ".png");
        }

        internal static Sprite Obstacle(ObstaclePlacementDefinition obstacle)
        {
            if (obstacle.Kind != ObstacleKind.Generator &&
                (obstacle.Durability < 1 || obstacle.Durability > LevelPlacementRules.MaxDurability(obstacle.Kind))) return null;
            int color = (int)obstacle.Color;
            string colorName = color >= 0 && color < colors.Length ? colors[color] : null;
            string file = obstacle.Kind switch
            {
                ObstacleKind.Crate => "Crate/crate-durability-" + obstacle.Durability + "-v1-256",
                ObstacleKind.Scrap => "Scrap/scrap-durability-" + obstacle.Durability + "-v1-256",
                ObstacleKind.Safe => "RecoveryCapsule/recovery-capsule-durability-" + obstacle.Durability + "-v1-256",
                ObstacleKind.ColorLock when colorName != null => "ColorLock/color-lock-" + colorName + "-durability-" + obstacle.Durability + "-v1-256",
                ObstacleKind.Appliance when colorName != null => "MetalRodBox/metal-rod-box-" + colorName + "-durability-" + obstacle.Durability + "-v1-256",
                ObstacleKind.Generator when obstacle.RequiredCharge >= 3 && obstacle.RequiredCharge <= 5 =>
                    "Generator/generator-charge-0-of-" + obstacle.RequiredCharge + "-v1-512",
                _ => null
            };
            return file == null ? null : Load("Obstacles/" + file + ".png");
        }

        internal static Sprite Cover(CoverKind kind) => kind switch
        {
            CoverKind.Web => Load("Obstacles/Web/web-base-v2.png"),
            CoverKind.Mold => Load("Obstacles/Mold/mold-base-v1-256.png"),
            _ => null
        };

        internal static Sprite Dust => Load("Obstacles/Dust/dust-base-v1-256.png");

        private static Sprite Load(string relativePath)
        {
            string address = BoardSpriteAtlas.AddressFor(relativePath);
            if (!atlases.TryGetValue(address, out BoardSpriteAtlas atlas))
            {
                LoadAndNotify(relativePath).Forget(Debug.LogException);
                return null;
            }
            return atlas.Get(Path.GetFileNameWithoutExtension(relativePath));
        }
    }
}
