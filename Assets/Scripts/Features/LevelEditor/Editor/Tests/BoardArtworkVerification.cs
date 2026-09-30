using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static class BoardArtworkVerification
    {
        /// <summary>메모리 레벨로 아트 대응, 겹침 순서, 삭제 후 초기화와 데이터 보존을 확인한다.</summary>
        public static void Run() => RunAsync().Forget(Debug.LogException);

        private static async UniTask RunAsync()
        {
            List<string> results = new List<string>();
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                LevelBoardArtwork.ReleaseAll();
                await LevelBoardArtwork.Warmup();
                Require(LevelBoardArtwork.RequestedAtlasCount == 1, "일반 블록만 요청하면 아틀라스 하나만 로드", results);
                LevelBoardArtwork.Cover(CoverKind.Web);
                await LevelBoardArtwork.Warmup("Obstacles/Web/");
                Require(LevelBoardArtwork.RequestedAtlasCount == 2, "거미줄 추가 시 해당 아틀라스만 추가 로드", results);
                LevelBoardArtwork.ReleaseAll();
                Require(LevelBoardArtwork.RequestedAtlasCount == 0, "아틀라스 소유권 해제", results);
                foreach (var sources in BoardAtlasPrebuild.SourcePaths().GroupBy(BoardAtlasPrebuild.AddressForPath))
                {
                    await LevelBoardArtwork.Warmup(sources.First().Substring("Assets/Textures/".Length));
                    foreach (var locator in UnityEngine.AddressableAssets.Addressables.ResourceLocators)
                        if (locator.Locate(sources.Key, typeof(UnityEngine.U2D.SpriteAtlas), out var locations))
                            foreach (var location in locations)
                                Require(location.ProviderId.Contains("BundledAssetProvider"), "실제 번들 로드 " + sources.Key, results);
                    using (BoardSpriteAtlas probe = new BoardSpriteAtlas(sources.Key))
                    {
                        await probe.LoadAsync();
                        HashSet<Texture2D> packedTextures = new HashSet<Texture2D>();
                        foreach (string path in sources)
                            foreach (Sprite source in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                            {
                                Sprite sprite = probe.Get(source.name);
                                Require(sprite != null, "아틀라스 항목 " + source.name, results);
                                if (sprite != null) packedTextures.Add(sprite.texture);
                            }
                        Require(packedTextures.Count > 0, "종류별 텍스처 존재 " + sources.Key, results);
                        foreach (Texture2D texture in packedTextures)
                        {
                            Require(texture.width <= 2048 && texture.height <= 2048 &&
                                Mathf.IsPowerOfTwo(texture.width) && Mathf.IsPowerOfTwo(texture.height), "페이지 크기 제한 " + sources.Key, results);
                            results.Add("INFO " + sources.Key + " texture " + texture.width + "x" + texture.height);
                        }
                    }
                }
                Require(EditorSettings.spritePackerMode == SpritePackerMode.SpriteAtlasV2, "SpriteAtlasV2 패킹 활성", results);
                foreach (ObstacleKind kind in Enum.GetValues(typeof(ObstacleKind)))
                {
                    int maximum = LevelPlacementRules.MaxDurability(kind);
                    for (int durability = 1; durability <= maximum; durability++)
                        for (int color = 0; color < (kind == ObstacleKind.Appliance || kind == ObstacleKind.ColorLock ? 5 : 1); color++)
                        {
                            ObstaclePlacementDefinition obstacle = JsonUtility.FromJson<ObstaclePlacementDefinition>(
                                "{\"kind\":" + (int)kind + ",\"durability\":" + durability + ",\"color\":" + color + "}");
                            Require(LevelBoardArtwork.Obstacle(obstacle) != null, kind + " 내구 " + durability + " 색 " + color, results);
                        }
                }
                for (int charge = 3; charge <= 5; charge++)
                    Require(LevelBoardArtwork.Obstacle(JsonUtility.FromJson<ObstaclePlacementDefinition>("{\"kind\":5,\"requiredCharge\":" + charge + "}")) != null,
                        "발전기 충전 목표 " + charge, results);
                LevelBoardView board = new LevelBoardView();
                for (int i = 0; i < 5; i++)
                {
                    InitialBlockKind kind = i < 2 ? InitialBlockKind.Rocket : (InitialBlockKind)(i + 1);
                    LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Kind = (int)kind, Direction = (RocketDirection)(i % 2) }, new[] { new BoardCoordinate(0, i) });
                }
                LevelBoardEditing.Apply(level, LevelBrush.Fixed, RabbitColor.Type1, new[] { new BoardCoordinate(1, 0), new BoardCoordinate(1, 1) });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 2 }, new[] { new BoardCoordinate(1, 0) });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Mold, Durability = 1 }, new[] { new BoardCoordinate(1, 1) });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 2 }, new[] { new BoardCoordinate(1, 0) });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Safe, Durability = 1 }, new[] { new BoardCoordinate(2, 0) });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Appliance, Durability = 9 }, new[] { new BoardCoordinate(4, 0) });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Generator, RequiredCharge = 4 }, new[] { new BoardCoordinate(4, 3) });
                string before = JsonUtility.ToJson(level);
                board.Display(level, null);
                for (int i = 0; i < 5; i++)
                    Require(board.CellAt(new BoardCoordinate(0, i)).style.backgroundImage.value.sprite != null, "파워 보드 표시 " + i, results);
                VisualElement web = board.CellAt(new BoardCoordinate(1, 0));
                Require(web.style.backgroundImage.value.sprite == LevelBoardArtwork.Dust, "먼지 바닥 이미지", results);
                Require(web.Q<VisualElement>("board-content-art").style.backgroundImage.value.sprite == LevelBoardArtwork.Rabbit(RabbitColor.Type1), "먼지 위 토끼 보존", results);
                Require(web.Q<VisualElement>("board-cover-art").style.backgroundImage.value.sprite == LevelBoardArtwork.Cover(CoverKind.Web), "최신 거미줄 겹침", results);
                Require(web.Q<Label>("board-art-badge").text == "2", "덮개 내구도 보존", results);
                VisualElement mold = board.CellAt(new BoardCoordinate(1, 1));
                Require(mold.style.backgroundImage.value.sprite == null && mold.Q<VisualElement>("board-cover-art").style.backgroundImage.value.sprite != null, "곰팡이 아래 블록 은폐", results);
                Require(board.CellAt(new BoardCoordinate(2, 0)).style.backgroundImage.value.sprite.name.Contains("recovery-capsule"), "금고를 열린 캡슐로 표시", results);
                VisualElement bodies = board.Q<VisualElement>("large-bodies");
                Require(bodies.childCount == 2 && bodies.Children().All(body => body.style.backgroundImage.value.sprite != null), "2×2 장애물별 이미지 하나", results);
                Require(JsonUtility.ToJson(level) == before, "표시 전후 레벨 데이터 보존", results);
                board.SetErrors(new[] { new BoardCoordinate(1, 0) });
                Require(web.Q<Label>("board-art-badge").text.Contains("!"), "이미지 위 오류 표시", results);
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Erase = true }, new[] { new BoardCoordinate(1, 0) });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Dust, Erase = true }, new[] { new BoardCoordinate(1, 0) });
                LevelBoardEditing.Apply(level, LevelBrush.Erase, RabbitColor.Type1, new[] { new BoardCoordinate(1, 0) });
                board.Display(level, null);
                Require(web.style.backgroundImage.value.sprite == null && web.Q<VisualElement>("board-cover-art").style.display.value == DisplayStyle.None &&
                    web.Q<VisualElement>("board-content-art").style.display.value == DisplayStyle.None, "삭제 후 이전 이미지 제거", results);
            }
            catch (Exception error) { results.Add("FAIL " + error); }
            finally
            {
                UnityEngine.Object.DestroyImmediate(level);
                Directory.CreateDirectory("Logs/BoardArtworkVerification");
                File.WriteAllLines("Logs/BoardArtworkVerification/results.txt", results);
                Debug.Log(string.Join("\n", results));
                EditorApplication.Exit(results.Any(result => result.StartsWith("FAIL")) ? 1 : 0);
            }
        }

        private static void Require(bool condition, string label, List<string> results)
        {
            results.Add((condition ? "PASS " : "FAIL ") + label);
        }
    }
}
