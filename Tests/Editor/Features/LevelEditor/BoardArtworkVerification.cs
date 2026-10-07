using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using Simulation;
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
                LevelDefinition coldLevel = ScriptableObject.CreateInstance<LevelDefinition>();
                LevelEditorWindow coldWindow = ScriptableObject.CreateInstance<LevelEditorWindow>();
                try
                {
                    LevelBoardEditing.Apply(coldLevel, LevelBrush.Fixed, RabbitColor.Type1, new[] { new BoardCoordinate(0, 0) });
                    coldWindow.ShowUtility();
                    coldWindow.SetLevel(coldLevel);
                    LevelBoardView coldBoard = coldWindow.rootVisualElement.Q<LevelBoardView>();
                    double deadline = EditorApplication.timeSinceStartup + 15;
                    while (coldBoard.ArtworkAt(new BoardCoordinate(0, 0), "board-content-art")?.style.backgroundImage.value.sprite == null && EditorApplication.timeSinceStartup < deadline)
                        await UniTask.Delay(100, DelayType.Realtime);
                    Require(coldBoard.ArtworkAt(new BoardCoordinate(0, 0), "board-content-art")?.style.backgroundImage.value.sprite != null,
                        "사전 로드 없이 편집 보드를 열면 일반 블록 이미지 표시", results);
                }
                finally
                {
                    coldWindow.Close();
                    UnityEngine.Object.DestroyImmediate(coldLevel);
                    LevelBoardArtwork.ReleaseAll();
                }
                await LevelBoardArtwork.Warmup();
                Require(LevelBoardArtwork.RequestedAtlasCount == 1, "일반 블록만 요청하면 아틀라스 하나만 로드", results);
                LevelBoardArtwork.Cover(CoverKind.Web);
                await LevelBoardArtwork.Warmup("Obstacles/Web/");
                Require(LevelBoardArtwork.RequestedAtlasCount == 2, "거미줄 추가 시 해당 아틀라스만 추가 로드", results);
                LevelBoardArtwork.ReleaseAll();
                Require(LevelBoardArtwork.RequestedAtlasCount == 0, "아틀라스 소유권 해제", results);
                LevelBoardArtwork.Acquire();
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
                await VerifyRuntimeArtwork(results);
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
                    Require(board.ArtworkAt(new BoardCoordinate(0, i), "board-content-art").style.backgroundImage.value.sprite != null, "파워 보드 표시 " + i, results);
                VisualElement web = board.CellAt(new BoardCoordinate(1, 0));
                Require(board.ArtworkAt(web, "board-dust-art").style.backgroundImage.value.sprite == LevelBoardArtwork.Dust(2), "먼지 내구도 2 바닥 이미지", results);
                Require(board.ArtworkAt(web, "board-content-art").style.backgroundImage.value.sprite == LevelBoardArtwork.Rabbit(RabbitColor.Type1), "먼지 위 토끼 보존", results);
                Require(board.ArtworkAt(web, "board-cover-art").style.backgroundImage.value.sprite == LevelBoardArtwork.Cover(CoverKind.Web, 2), "최신 거미줄 내구도 2 겹침", results);
                Require(((Label)board.AnnotationAt(new BoardCoordinate(1, 0), "board-art-badge")).text == "2", "덮개 내구도 보존", results);
                VisualElement mold = board.CellAt(new BoardCoordinate(1, 1));
                Require(mold.style.backgroundImage.value.sprite == null && board.ArtworkAt(mold, "board-cover-art").style.backgroundImage.value.sprite != null, "곰팡이 아래 블록 은폐", results);
                Require(board.ArtworkAt(new BoardCoordinate(2, 0), "board-content-art").style.backgroundImage.value.sprite.name.Contains("recovery-capsule"), "금고를 열린 캡슐로 표시", results);
                IReadOnlyList<VisualElement> bodies = board.LargeBodies;
                Require(bodies.Count == 2 && bodies.All(body => body.style.backgroundImage.value.sprite != null), "2×2 장애물별 이미지 하나", results);
                Require(JsonUtility.ToJson(level) == before, "표시 전후 레벨 데이터 보존", results);
                for (int durability = 1; durability <= 3; durability++)
                {
                    Require(LevelBoardArtwork.Dust(durability)?.name.Contains("durability-" + durability) == true, "먼지 단계 " + durability, results);
                    Require(LevelBoardArtwork.Cover(CoverKind.Web, durability)?.name.Contains("durability-" + durability + "-v2") == true, "거미줄 단계 " + durability, results);
                }
                Require(LevelBoardArtwork.Floor(level, new BoardCoordinate(0, 0), 0)?.name.Contains("outer-top-left") == true, "활성 보드 외곽 모서리", results);
                Require(LevelBoardArtwork.Floor(level, new BoardCoordinate(5, 5), 0)?.name.Contains("center") == true, "보드 내부 바닥", results);
                Require(web.Q("board-floor-art").childCount == 4 && board.IndexOf(web) < board.IndexOf(board.ArtworkAt(web, "board-dust-art").parent) &&
                    board.ArtworkAt(web, "board-dust-art").parent.IndexOf(board.ArtworkAt(web, "board-dust-art")) < board.ArtworkAt(web, "board-content-art").parent.IndexOf(board.ArtworkAt(web, "board-content-art")), "바닥·먼지·내용물 겹침 순서", results);
                for (int pair = 0; pair < 4; pair++)
                    Require(LevelBoardArtwork.Portal(pair, false) != null && LevelBoardArtwork.Portal(pair, true) != null, "포털 쌍 " + pair, results);
                for (int frame = 0; frame < 4; frame++) Require(LevelBoardArtwork.ChargePulse(frame) != null, "충전 빛 프레임 " + frame, results);
                LevelFlowEditing.SetPortal(level, new BoardCoordinate(6, 6), new BoardCoordinate(7, 7));
                LevelFlowEditing.SetArrival(level, new BoardCoordinate(8, 7), false);
                LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(new BoardCoordinate(6, 0), new BoardCoordinate(6, 1)) }, false);
                board.Display(level, null);
                Require(board.ArtworkAt(new BoardCoordinate(6, 6), "board-portal-art").style.backgroundImage.value.sprite == LevelBoardArtwork.Portal(0, false), "입구 보드 이미지", results);
                Require(board.ArtworkAt(new BoardCoordinate(7, 7), "board-portal-art").style.backgroundImage.value.sprite == LevelBoardArtwork.Portal(0, true), "출구 보드 이미지", results);
                Require(board.ArtworkAt(new BoardCoordinate(8, 7), "board-arrival-art").style.backgroundImage.value.sprite == LevelBoardArtwork.Arrival, "회수 도착 바닥", results);
                LevelFlowOverlay flow = new LevelFlowOverlay(); flow.Display(level, FlowTool.None);
                Require(flow.Q("wall-art-0")?.style.backgroundImage.value.sprite != null && flow.Q("wall-art-0").pickingMode == PickingMode.Ignore, "벽 이미지와 입력 통과", results);
                Require(LevelSupplyEditing.PlaceRecovery(level, new[] { new BoardCoordinate(8, 8) }) == null, "회수 부품 배치", results);
                Require(LevelConnectionEditing.ConnectAuto(level, level.Obstacles[2].Id, level.Obstacles[1].Id, new BoardCoordinate(4, 4)) == null, "검사용 발전기 전선 연결", results);
                before = JsonUtility.ToJson(level);
                board.Display(level, null); flow.Display(level, FlowTool.None);
                Require(board.ArtworkAt(new BoardCoordinate(8, 8), "board-content-art").style.backgroundImage.value.sprite == LevelBoardArtwork.Recovery, "회수 부품 보드 이미지", results);
                Require(flow.Q("wire-art-0-0")?.style.backgroundImage.value.sprite != null && flow.Q("terminal-start-art-0")?.style.backgroundImage.value.sprite != null &&
                    flow.Q("charge-art-0")?.style.backgroundImage.value.sprite != null, "전선·단자·충전 미리보기 이미지", results);
                LevelBoardEditing.Apply(level, LevelBrush.Deactivate, RabbitColor.Type1, new[] { new BoardCoordinate(7, 6) });
                board.Display(level, null);
                Require(board.CellAt(new BoardCoordinate(7, 6)).Q("board-floor-art").style.display.value == DisplayStyle.None, "비활성 칸 바닥 숨김", results);
                Require(LevelBoardArtwork.Floor(level, new BoardCoordinate(8, 7), 0)?.name.Contains("inner-top-left") == true, "구멍 주변 오목한 모서리", results);
                before = JsonUtility.ToJson(level);
                LevelEditorWindow window = ScriptableObject.CreateInstance<LevelEditorWindow>();
                try
                {
                    window.ShowUtility(); window.position = new Rect(30, 30, 1180, 820); window.SetLevel(level);
                    await UniTask.Delay(1800, DelayType.Realtime);
                    Directory.CreateDirectory("Logs/BotAnalysisVerification");
                    typeof(BotAnalysisVerification).GetMethod("CaptureAnalysis", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(null, new object[] { "priority-one-board.png" });
                    Require(JsonUtility.ToJson(level) == before, "애니메이션·실제 창 표시 후 데이터 보존", results);
                }
                finally { window.Close(); }
                board.SetErrors(new[] { new BoardCoordinate(1, 0) });
                Require(((Label)board.AnnotationAt(new BoardCoordinate(1, 0), "board-art-badge")).text.Contains("!"), "이미지 위 오류 표시", results);
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Erase = true }, new[] { new BoardCoordinate(1, 0) });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Dust, Erase = true }, new[] { new BoardCoordinate(1, 0) });
                LevelBoardEditing.Apply(level, LevelBrush.Erase, RabbitColor.Type1, new[] { new BoardCoordinate(1, 0) });
                board.Display(level, null);
                Require(web.style.backgroundImage.value.sprite == null && board.ArtworkAt(web, "board-cover-art").style.display.value == DisplayStyle.None &&
                    board.ArtworkAt(web, "board-content-art").style.display.value == DisplayStyle.None, "삭제 후 이전 이미지 제거", results);
            }
            catch (Exception error) { results.Add("FAIL " + error); }
            finally
            {
                LevelBoardArtwork.Release();
                UnityEngine.Object.DestroyImmediate(level);
                Directory.CreateDirectory("Logs/BoardArtworkVerification");
                File.WriteAllLines("Logs/BoardArtworkVerification/results.txt", results);
                Debug.Log(string.Join("\n", results));
                EditorApplication.Exit(results.Any(result => result.StartsWith("FAIL")) ? 1 : 0);
            }
        }

        private static async UniTask VerifyRuntimeArtwork(List<string> results)
        {
            LevelDefinition source = ScriptableObject.CreateInstance<LevelDefinition>();
            JsonUtility.FromJsonOverwrite("{\"levelNumber\":1,\"missions\":[{\"kind\":0,\"color\":0,\"count\":12}]}", source);
            try
            {
                InitialBlockKind[] kinds = { InitialBlockKind.Rocket, InitialBlockKind.Rocket, InitialBlockKind.Bomb, InitialBlockKind.Drone, InitialBlockKind.Magnet };
                for (int i = 0; i < kinds.Length; i++)
                    LevelObstacleEditing.Apply(source, new PlacementBrush { Layer = PlacementLayer.Block, Kind = (int)kinds[i], Direction = (RocketDirection)(i % 2) }, new[] { new BoardCoordinate(0, i) });
                LevelObstacleEditing.Apply(source, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Safe, Durability = 5 }, new[] { new BoardCoordinate(2, 0) });
                LevelStateBuildResult built = LevelStateBuilder.Build(source, 1);
                if (!built.IsBuilt) throw new InvalidOperationException(string.Join(" / ", built.Issues));
                LevelRuntimeState state = built.State;
                for (int i = 0; i < kinds.Length; i++)
                {
                    RuntimeCell cell = state.CellAt(new BoardCoordinate(0, i));
                    foreach (TextElement target in new TextElement[] { new Button(), new Label() })
                    {
                        target.text = LevelInitialStatePanel.CellText(cell, state);
                        RuntimeBoardArtwork.Bind(target, cell, state);
                        Sprite sprite = target.Q("runtime-content").style.backgroundImage.value.sprite;
                        Require(sprite != null && sprite.name == LevelBoardArtwork.Block(kinds[i], (RocketDirection)(i % 2)).name,
                            "플레이/재생 파워 이미지 " + target.GetType().Name + " " + i, results);
                        Require(target.text == "" && target.Q("runtime-art").pickingMode == PickingMode.Ignore,
                            "파워 문자 대체와 클릭 통과 " + i, results);
                    }
                }
                RuntimeCell obstacleCell = state.CellAt(new BoardCoordinate(2, 0));
                RuntimeObstacle obstacle = state.Obstacles[obstacleCell.ObstacleIndex.Value];
                typeof(RuntimeObstacle).GetProperty("Durability").SetValue(obstacle, 1);
                Label damaged = new Label(LevelInitialStatePanel.CellText(obstacleCell, state));
                RuntimeBoardArtwork.Bind(damaged, obstacleCell, state);
                Require(damaged.Q("runtime-content").style.backgroundImage.value.sprite.name.Contains("durability-1-"), "피격 후 현재 내구도 이미지", results);
                Require(source.Obstacles[0].Durability == 5, "이미지 갱신은 원본 내구도 보존", results);
                LevelEditorWindow.OpenWorkspace(2);
                LevelEditorWindow runtimeWindow = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().Single();
                try
                {
                    runtimeWindow.position = new Rect(30, 30, 1180, 820);
                    runtimeWindow.SetLevel(source);
                    LevelInitialStatePanel panel = runtimeWindow.ActiveSimulationPanel;
                    typeof(LevelInitialStatePanel).GetMethod("Build", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(panel, null);
                    await UniTask.Delay(1800, DelayType.Realtime);
                    for (int i = 0; i < kinds.Length; i++)
                        Require(panel.rootVisualElement.Q<Button>("initial-cell-0-" + i).Q("runtime-content").style.backgroundImage.value.sprite != null,
                            "실제 시뮬레이션 보드 파워 이미지 " + i, results);
                    RuntimeCell generated = panel.CurrentState.CellAt(new BoardCoordinate(1, 1));
                    typeof(RuntimeCell).GetProperty("Content").SetValue(generated, RuntimeContent.Rocket);
                    typeof(RuntimeCell).GetProperty("RocketDirection").SetValue(generated, RocketDirection.Vertical);
                    typeof(LevelInitialStatePanel).GetMethod("DisplayState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(panel, new object[] { null });
                    await UniTask.Delay(100, DelayType.Realtime);
                    Require(panel.rootVisualElement.Q<Button>("initial-cell-1-1").Q("runtime-content").style.backgroundImage.value.sprite.name.Contains("vertical"),
                        "실행 중 생성된 세로 로켓 이미지 갱신", results);
                    Directory.CreateDirectory("Logs/BotAnalysisVerification");
                    typeof(BotAnalysisVerification).GetMethod("CaptureAnalysis", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(null, new object[] { "runtime-artwork.png" });
                }
                finally { runtimeWindow.Close(); }
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        private static void Require(bool condition, string label, List<string> results)
        {
            results.Add((condition ? "PASS " : "FAIL ") + label);
            Debug.Log(results[results.Count - 1]);
        }
    }
}
