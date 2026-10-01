using System;
using System.IO;
using System.Collections.Generic;
using Board;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>편집·플레이·재생 보드의 확정 아트 매핑. 실행 규칙에는 연결하지 않는다.</summary>
    internal static class LevelBoardArtwork
    {
        private static readonly string[] colors = { "pink", "yellow", "blue", "green", "purple" };
        private static readonly Dictionary<string, BoardSpriteAtlas> atlases = new Dictionary<string, BoardSpriteAtlas>();
        internal static int RequestedAtlasCount => atlases.Count;
        private static int users;
        internal static event Action Loaded;

        static LevelBoardArtwork()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseAll;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            // Play Mode 종료 시 Addressables 핸들은 무효화되지만 Editor 창은 유지된다.
            // 전환 전에 캐시를 비우고 전환 후 열린 보드가 새 핸들로 이미지를 요청하게 한다.
            if (change == PlayModeStateChange.ExitingEditMode || change == PlayModeStateChange.ExitingPlayMode)
                ReleaseAll();
            else if (change == PlayModeStateChange.EnteredEditMode || change == PlayModeStateChange.EnteredPlayMode)
                Loaded?.Invoke();
        }

        internal static async UniTask Warmup(string relativePath = "Blocks/")
        {
            string address = BoardSpriteAtlas.AddressFor(relativePath);
            if (!atlases.TryGetValue(address, out BoardSpriteAtlas atlas))
            {
                atlas = new BoardSpriteAtlas(address);
                atlases.Add(address, atlas);
            }
            if (!EditorApplication.isPlaying)
            {
                // 창 복원 중에는 초기 씬이 아직 열리는 중일 수 있으므로 로드를 다음 에디터 갱신으로 미룬다.
                UniTaskCompletionSource ready = new UniTaskCompletionSource();
                void Resume() { EditorApplication.update -= Resume; ready.TrySetResult(); }
                EditorApplication.update += Resume;
                await ready.Task;
            }
            await atlas.LoadAsync();
        }

        internal static void Acquire(bool notify = true)
        {
            users++;
            if (notify) Loaded?.Invoke();
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
            => Block(block.Kind, block.RocketDirection);

        internal static Sprite Block(InitialBlockKind kind, RocketDirection direction)
        {
            string file = kind switch
            {
                InitialBlockKind.Rocket => direction == RocketDirection.Horizontal ? "cleaning-rocket-horizontal-v1" :
                    direction == RocketDirection.Vertical ? "cleaning-rocket-vertical-v1" : null,
                InitialBlockKind.Bomb => "moon-bomb-v1",
                InitialBlockKind.Drone => "collection-drone-v1",
                InitialBlockKind.Magnet => "rainbow-magnet-v1",
                _ => null
            };
            return file == null ? null : Load("PowerBlocks/" + file + ".png");
        }

        internal static Sprite Obstacle(ObstaclePlacementDefinition obstacle)
            => Obstacle(obstacle, obstacle.Durability);

        internal static Sprite Obstacle(ObstaclePlacementDefinition obstacle, int durability, int charge = 0)
        {
            if (obstacle.Kind != ObstacleKind.Generator &&
                (durability < 1 || durability > LevelPlacementRules.MaxDurability(obstacle.Kind))) return null;
            int color = (int)obstacle.Color;
            string colorName = color >= 0 && color < colors.Length ? colors[color] : null;
            string file = obstacle.Kind switch
            {
                ObstacleKind.Crate => "Crate/crate-durability-" + durability + "-v1-256",
                ObstacleKind.Scrap => "Scrap/scrap-durability-" + durability + "-v1-256",
                ObstacleKind.Safe => "RecoveryCapsule/recovery-capsule-durability-" + durability + "-v1-256",
                ObstacleKind.ColorLock when colorName != null => "ColorLock/color-lock-" + colorName + "-durability-" + durability + "-v1-256",
                ObstacleKind.Appliance when colorName != null => "MetalRodBox/metal-rod-box-" + colorName + "-durability-" + durability + "-v1-256",
                ObstacleKind.Generator when obstacle.RequiredCharge >= 3 && obstacle.RequiredCharge <= 5 =>
                    "Generator/generator-charge-" + Mathf.Clamp(charge, 0, obstacle.RequiredCharge) + "-of-" + obstacle.RequiredCharge + "-v1-512",
                _ => null
            };
            return file == null ? null : Load("Obstacles/" + file + ".png");
        }

        internal static Sprite Cover(CoverKind kind, int durability = 3) => kind switch
        {
            CoverKind.Web when durability >= 1 && durability <= 3 => Load("Obstacles/Web/web-durability-" + durability + "-v2-256.png"),
            CoverKind.Mold => Load("Obstacles/Mold/mold-base-v1-256.png"),
            _ => null
        };

        internal static Sprite Dust(int durability) => durability >= 1 && durability <= 3
            ? Load("Obstacles/Dust/dust-durability-" + durability + "-v1-256.png") : null;

        internal static Sprite Recovery => Load("BoardDevices/Recovery/recovery-part-v1-256.png");
        internal static Sprite Arrival => Load("BoardDevices/Recovery/recovery-exit-v1-256.png");
        internal static Sprite Portal(int pair, bool exit)
        {
            string shape = (pair % 4) switch { 0 => "cyan-circle", 1 => "orange-triangle", 2 => "purple-diamond", _ => "green-plus" };
            return Load("BoardDevices/Portals/portal-" + shape + (exit ? "-exit" : "-entry") + "-v1-256.png");
        }
        internal static Sprite Wall(bool vertical) => Load("BoardTerrain/Walls/scrap-wall-" + (vertical ? "vertical" : "horizontal") + "-v1-256.png");
        internal static Sprite Wire(bool vertical) => Load("BoardDevices/Wiring/wire-" + (vertical ? "vertical" : "horizontal") + "-v1-256.png");
        internal static Sprite Terminal(int slot, bool connected)
        {
            string shape = (slot % 3) switch { 0 => "amber-triangle", 1 => "cyan-circle", _ => "purple-diamond" };
            return Load("BoardDevices/Wiring/terminal-" + shape + (connected ? "-on" : "-off") + "-v1-256.png");
        }
        internal static Sprite ChargePulse(int frame) => Load("Effects/GeneratorCharge/charge-pulse-0" + (frame % 4 + 1) + "-v1-256.png");

        // 네 사분면을 따로 고르면 구멍·막다른 길·복수의 오목한 모서리도 표시할 수 있다.
        internal static Sprite Floor(LevelDefinition level, BoardCoordinate cell, int quadrant)
        {
            int row = quadrant < 2 ? -1 : 1, column = quadrant % 2 == 0 ? -1 : 1;
            bool horizontal = !LevelFlowRules.Active(level, new BoardCoordinate(cell.Row + row, cell.Column));
            bool vertical = !LevelFlowRules.Active(level, new BoardCoordinate(cell.Row, cell.Column + column));
            string side = row < 0 ? "top" : "bottom", end = column < 0 ? "left" : "right";
            string tile = horizontal && vertical ? "outer-" + side + "-" + end : horizontal ? "edge-" + side : vertical ? "edge-" + end :
                !LevelFlowRules.Active(level, new BoardCoordinate(cell.Row + row, cell.Column + column)) ? "inner-" + side + "-" + end : "center";
            return Load("BoardTerrain/Floor/floor-" + tile + "-v1-256.png");
        }

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
