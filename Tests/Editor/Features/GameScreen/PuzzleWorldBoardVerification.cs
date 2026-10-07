using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static class PuzzleWorldBoardVerification
    {
        private const string Key = "WorldBoardVerification.Running";
        private const string Output = "Logs/WorldBoardVerification/";
        private static readonly List<string> results = new List<string>();
        static PuzzleWorldBoardVerification()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                { SessionState.SetBool(Key, false); VerifyAsync().Forget(Debug.LogException); }
            };
        }
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            foreach (string path in new[] { PuzzleGameAssets.ScenePath }.Concat(Directory.GetFiles(PuzzleGameAssets.Folder, "*.prefab")))
                if (AssetDatabase.GetDependencies(path, true).Any(item => AssetDatabase.LoadAssetAtPath<LevelDefinition>(item) != null))
                    throw new Exception("LevelDefinition dependency: " + path);
            UnityEditor.AddressableAssets.Settings.AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            int packed = settings.DataBuilders.FindIndex(builder => builder is BuildScriptPackedPlayMode);
            if (packed < 0) throw new Exception("Packed play mode builder missing");
            SessionState.SetInt(Key + ".PreviousBuilder", settings.ActivePlayModeDataBuilderIndex);
            settings.ActivePlayModeDataBuilderIndex = packed;
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath);
            EditorApplication.EnterPlaymode();
        }

        private static async UniTask VerifyAsync()
        {
            PuzzleArtwork art = null;
            LevelDefinition fixture = null;
            try
            {
                PuzzleBoardPreview preview = UnityEngine.Object.FindFirstObjectByType<PuzzleBoardPreview>();
                UsePreviewOnly(preview);
                await Ready(preview);
                Check(preview.State.LevelNumber == 1, "MemoryPack 레벨 1 로드");
                Check(preview.State.Cells.Count == BoardDefinition.DefaultRows * BoardDefinition.DefaultColumns, "9×9 상태 전달");
                bool bundled = UnityEngine.AddressableAssets.Addressables.ResourceLocators.Any(locator =>
                    locator.Locate("MoonRabbitBoard-Blocks", typeof(UnityEngine.U2D.SpriteAtlas), out IList<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation> locations) && locations.Any(location => location.ProviderId.Contains("BundledAssetProvider")));
                Check(bundled, "실제 Addressables 번들에서 블록 아틀라스 로드");
                PuzzleWorldBoard board = UnityEngine.Object.FindFirstObjectByType<PuzzleWorldBoard>();
                Camera camera = (Camera)typeof(PuzzleBoardPreview).GetField("boardCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(preview);
                await UniTask.DelayFrame(3);
                File.WriteAllLines(Output + "renderers.txt", board.GetComponentsInChildren<SpriteRenderer>().Take(12).Select(item =>
                    item.name + " order=" + item.sortingOrder + " sprite=" + item.sprite?.name + " texture=" + item.sprite?.texture?.name + " shader=" + item.sharedMaterial.shader.name));
                Capture(camera, "level-01.png", 1280, 720);
                results.Add("PASS 씬/프리팹의 LevelDefinition 직접 참조 없음 (진입 전 검사)");

                fixture = Fixture();
                string before = JsonUtility.ToJson(fixture);
                LevelStateBuildResult built = LevelStateBuilder.Build(fixture, 123);
                if (!built.IsBuilt) throw new Exception("Fixture invalid: " + string.Join(" / ", built.Issues));
                LevelRuntimeState state = built.State;
                art = new PuzzleArtwork();
                await art.PrepareAsync(state, CancellationToken.None);
                board.Draw(state, art);
                Check(!board.transform.Find("Cell-" + (BoardDefinition.DefaultRows - 1) + "-0").gameObject.activeSelf, "비활성 칸 숨김");
                Check(board.GetComponentsInChildren<SpriteRenderer>().Count(item => item.name.StartsWith("Obstacle-")) == state.Obstacles.Count,
                    "2×2 포함 장애물당 이미지 하나");
                Transform large = board.transform.Find("Obstacle-" + state.Obstacles.First(item => item.Definition.Kind == ObstacleKind.Appliance).Definition.Id);
                Check(Vector3.Distance(large.localPosition, PuzzleWorldBoard.CellPosition(new BoardCoordinate(4, 0)) + new Vector3(0.5f, -0.5f, 0)) < 0.001f, "2×2 장애물 중심 좌표");
                Check(Mathf.Abs(large.GetComponent<SpriteRenderer>().bounds.size.x - 2.16f) < 0.01f, "2×2 장애물 확대 크기");
                Transform layered = board.transform.Find("Cell-1-0");
                Check(layered.Find("Cover").GetComponent<SpriteRenderer>().sprite != null, "거미줄 실제 이미지");
                Check(board.transform.Find("Cell-1-8/Cover").GetComponent<SpriteRenderer>().sprite != null, "곰팡이 실제 이미지");
                Check(layered.Find("Floor").GetComponent<SpriteRenderer>().sortingOrder < layered.Find("Dust").GetComponent<SpriteRenderer>().sortingOrder &&
                    layered.Find("Dust").GetComponent<SpriteRenderer>().sortingOrder < layered.Find("Content").GetComponent<SpriteRenderer>().sortingOrder &&
                    layered.Find("Content").GetComponent<SpriteRenderer>().sortingOrder < layered.Find("Cover").GetComponent<SpriteRenderer>().sortingOrder,
                    "바닥·먼지·내용물·덮개 순서");
                Check(board.GetComponentsInChildren<SpriteRenderer>().Any(item => item.name == "Wall" && item.sprite != null), "벽 표시");
                Check(board.GetComponentsInChildren<SpriteRenderer>().Count(item => item.name.StartsWith("Portal-")) == 2, "포털 입출구 표시");
                Check(board.GetComponentsInChildren<SpriteRenderer>().Count(item => item.name == "Wire") == state.Connections.Sum(item => item.Vertices.Count - 1), "연결선 경로 수");
                Check(board.GetComponentsInChildren<SpriteRenderer>().Count(item => item.name.StartsWith("Terminal-")) == 2, "양 끝 단자 표시");
                VerifySegments(board, state);

                int count = board.GetComponentsInChildren<Transform>(true).Length;
                board.Draw(state, art);
                Check(board.GetComponentsInChildren<Transform>(true).Length == count, "재표시 오브젝트 중복 없음");
                RuntimeCell generated = state.CellAt(new BoardCoordinate(0, 0));
                foreach (RuntimeContent kind in new[] { RuntimeContent.Rocket, RuntimeContent.Bomb, RuntimeContent.Drone, RuntimeContent.Magnet })
                {
                    Set(generated, "Content", kind);
                    foreach (RocketDirection direction in new[] { RocketDirection.Horizontal, RocketDirection.Vertical })
                    {
                        Set(generated, "RocketDirection", direction);
                        board.Draw(state, art);
                        Sprite sprite = board.transform.Find("Cell-0-0/Content").GetComponent<SpriteRenderer>().sprite;
                        Check(sprite != null && sprite.name.Contains(PuzzleArtworkPaths.Content(generated).Split('/').Last()), "사후 생성 " + kind + " " + direction);
                    }
                }
                Set(generated, "Content", RuntimeContent.Rocket);
                Set(generated, "RocketDirection", RocketDirection.Horizontal);
                Set(state.CellAt(new BoardCoordinate(0, 1)), "Content", RuntimeContent.Rocket);
                Set(state.CellAt(new BoardCoordinate(0, 1)), "RocketDirection", RocketDirection.Vertical);
                Set(state.CellAt(new BoardCoordinate(0, 2)), "Content", RuntimeContent.Bomb);
                Set(state.CellAt(new BoardCoordinate(0, 3)), "Content", RuntimeContent.Drone);
                Set(state.CellAt(new BoardCoordinate(0, 4)), "Content", RuntimeContent.Magnet);
                board.Draw(state, art);
                await UniTask.DelayFrame(3);
                Capture(camera, "representative-landscape.png", 1280, 720);
                Capture(camera, "representative-portrait.png", 720, 1280);
                Check(JsonUtility.ToJson(fixture) == before, "표시 후 원본 정의 보존");
                VerifyVariants(state, art);
                bool missingRejected = false;
                try { art.Get("Blocks/missing-world-verification-sprite"); }
                catch (InvalidOperationException error) { missingRejected = error.Message.Contains("missing-world-verification-sprite"); }
                Check(missingRejected, "누락 이미지는 경로가 포함된 오류로 보고");
                VerifySmallBoard(preview, board, camera, art);

                foreach (RuntimeCell cell in state.Cells)
                {
                    Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Cover", null); Set(cell, "DustDurability", 0);
                }
                board.Draw(state, art);
                Check(!board.GetComponentsInChildren<SpriteRenderer>().Any(item => item.name.StartsWith("Obstacle-")), "장애물 제거 후 잔상 없음");
                Check(!board.transform.Find("Cell-1-0/Cover").GetComponent<SpriteRenderer>().enabled, "덮개 제거 후 잔상 없음");
                art.Dispose(); Check(art.AtlasCount == 0, "아틀라스 해제");
                art = null;

                PuzzleArtwork interrupted = new PuzzleArtwork();
                UniTask pending = interrupted.PrepareAsync(built.State, CancellationToken.None);
                interrupted.Dispose();
                try { await pending; } catch (OperationCanceledException) { }
                Check(interrupted.AtlasCount == 0, "로드 중 Dispose 후 핸들 정리");
                using (CancellationTokenSource canceled = new CancellationTokenSource())
                using (PuzzleArtwork canceledArt = new PuzzleArtwork())
                {
                    canceled.Cancel();
                    bool rejected = false;
                    try { await canceledArt.PrepareAsync(state, canceled.Token); }
                    catch (OperationCanceledException) { rejected = true; }
                    Check(rejected && canceledArt.AtlasCount == 0, "취소된 로드가 리소스를 만들지 않음");
                }
                await VerifyLoadFailure(0, "레벨 0 표시 실패");
                await VerifyLoadFailure(49, "MemoryPack에 레벨 49이 없습니다");
                await VerifyLoadFailure(999951, "레벨 999951 표시 실패");
                await VerifyDestroyedPreview(board, camera);
                await EditorSceneManager.LoadSceneAsyncInPlayMode(PuzzleGameAssets.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                UsePreviewOnly(UnityEngine.Object.FindFirstObjectByType<PuzzleBoardPreview>());
                await UniTask.Yield();
                await Ready(UnityEngine.Object.FindFirstObjectByType<PuzzleBoardPreview>());
                Check(UnityEngine.Object.FindObjectsByType<PuzzleWorldBoard>(FindObjectsSortMode.None).Length == 1, "씬 재진입 보드 하나");
                results.Add("PASS 통합 검증 완료");
            }
            catch (Exception error) { results.Add("FAIL " + error); Debug.LogException(error); }
            finally
            {
                art?.Dispose();
                if (fixture != null) UnityEngine.Object.Destroy(fixture);
                File.WriteAllLines(Output + "results.txt", results);
                AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = SessionState.GetInt(Key + ".PreviousBuilder", 0);
                if (Application.isBatchMode) EditorApplication.Exit(results.Any(item => item.StartsWith("FAIL")) ? 1 : 0);
                else EditorApplication.ExitPlaymode();
            }
        }

        private static void UsePreviewOnly(PuzzleBoardPreview preview)
        {
            // 표시 회귀 검사는 플레이 세션 대신 보존된 1단계 Preview를 단독 실행한다.
            PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
            if (session != null) UnityEngine.Object.DestroyImmediate(session.gameObject);
            preview.enabled = true;
        }

        private static void VerifyVariants(LevelRuntimeState state, PuzzleArtwork art)
        {
            foreach (RuntimeObstacle body in state.Obstacles)
            {
                if (body.Definition.Kind == ObstacleKind.Generator)
                {
                    for (int required = 3; required <= 5; required++)
                        for (int charge = 0; charge <= required; charge++)
                            Check(art.Get("Obstacles/Generator/generator-charge-" + charge + "-of-" + required + "-v1-512") != null, "발전기 충전 " + charge + "/" + required);
                    continue;
                }
                for (int durability = 1; durability <= LevelPlacementRules.MaxDurability(body.Definition.Kind); durability++)
                {
                    Set(body, "Durability", durability);
                    string path = PuzzleArtworkPaths.Obstacle(body);
                    Check(art.Get(path) != null, body.Definition.Kind + " 내구도 " + durability);
                    if (body.Definition.Kind == ObstacleKind.ColorLock || body.Definition.Kind == ObstacleKind.Appliance)
                        foreach (string color in new[] { "yellow", "blue", "green", "purple" })
                            Check(art.Get(path.Replace("pink", color)) != null, body.Definition.Kind + " 내구도 " + durability + " " + color);
                }
            }
            for (int durability = 1; durability <= 3; durability++)
            {
                Check(art.Get(PuzzleArtworkPaths.Dust(durability)) != null, "먼지 단계 " + durability);
                Check(art.Get("Obstacles/Web/web-durability-" + durability + "-v2-256") != null, "거미줄 단계 " + durability);
            }
            for (int i = 0; i < 5; i++) Check(art.Get(PuzzleArtworkPaths.Rabbit((RabbitColor)i)) != null, "일반 색 " + i);
            for (int i = 0; i < 4; i++)
            {
                Check(art.Get(PuzzleArtworkPaths.Portal(i, false)) != null, "포털 입구 " + i);
                Check(art.Get(PuzzleArtworkPaths.Portal(i, true)) != null, "포털 출구 " + i);
            }
        }

        private static LevelDefinition Fixture()
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":12}]}", level);
            ObstacleKind[] kinds = { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance, ObstacleKind.Generator };
            BoardCoordinate[] positions = { new BoardCoordinate(2, 0), new BoardCoordinate(2, 2), new BoardCoordinate(2, 4), new BoardCoordinate(2, 6), new BoardCoordinate(4, 0), new BoardCoordinate(4, 3) };
            for (int i = 0; i < kinds.Length; i++)
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)kinds[i], Durability = kinds[i] == ObstacleKind.Generator ? 0 : LevelPlacementRules.MaxDurability(kinds[i]), RequiredCharge = 4 }, new[] { positions[i] });
            LevelBoardEditing.Apply(level, LevelBrush.Fixed, RabbitColor.Type1, new[] { new BoardCoordinate(1, 0), new BoardCoordinate(1, 8) });
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 2 }, new[] { new BoardCoordinate(1, 0) });
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 2 }, new[] { new BoardCoordinate(1, 0) });
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Mold, Durability = 1 }, new[] { new BoardCoordinate(1, 8) });
            LevelBoardEditing.Apply(level, LevelBrush.Deactivate, RabbitColor.Type1, new[] { new BoardCoordinate(BoardDefinition.DefaultRows - 1, 0) });
            LevelFlowEditing.SetPortal(level, new BoardCoordinate(6, 6), new BoardCoordinate(7, 7));
            LevelFlowEditing.SetMerge(level, new BoardCoordinate(7, 7), LevelFlowRules.Sources(level, new BoardCoordinate(7, 7)));
            LevelFlowEditing.SetArrival(level, new BoardCoordinate(BoardDefinition.DefaultRows - 1, BoardDefinition.DefaultColumns - 1), false);
            LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(new BoardCoordinate(6, 0), new BoardCoordinate(6, 1)),
                new BoardEdge(new BoardCoordinate(7, 0), new BoardCoordinate(7, 1)),
                new BoardEdge(new BoardCoordinate(7, 1), new BoardCoordinate(8, 1)) }, false);
            LevelSupplyEditing.PlaceRecovery(level, new[] { new BoardCoordinate(7, 8) });
            string error = LevelConnectionEditing.ConnectAuto(level, level.Obstacles[5].Id, level.Obstacles[4].Id, new BoardCoordinate(4, 4));
            if (error != null) throw new Exception(error);
            using (SerializedObject data = new SerializedObject(level))
            {
                BoardCoordinate[] route = { new BoardCoordinate(4, 4), new BoardCoordinate(3, 4), new BoardCoordinate(3, 3), new BoardCoordinate(3, 2), new BoardCoordinate(4, 2) };
                SerializedProperty vertices = data.FindProperty("connections").GetArrayElementAtIndex(0).FindPropertyRelative("vertices");
                vertices.arraySize = route.Length;
                for (int i = 0; i < route.Length; i++)
                {
                    vertices.GetArrayElementAtIndex(i).FindPropertyRelative("row").intValue = route[i].Row;
                    vertices.GetArrayElementAtIndex(i).FindPropertyRelative("column").intValue = route[i].Column;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            return level;
        }
        private static void Set(object target, string property, object value) => target.GetType().GetProperty(property).SetValue(target, value);
        private static void VerifySmallBoard(PuzzleBoardPreview preview, PuzzleWorldBoard board, Camera camera, PuzzleArtwork art)
        {
            LevelDefinition small = ScriptableObject.CreateInstance<LevelDefinition>();
            LevelRuntimeState previous = preview.State;
            Vector3 cameraPosition = camera.transform.position;
            try
            {
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", small);
                using (SerializedObject data = new SerializedObject(small))
                {
                    SerializedProperty cells = data.FindProperty("board.cells");
                    for (int i = 0; i < cells.arraySize; i++) cells.GetArrayElementAtIndex(i).FindPropertyRelative("isActive").boolValue = i / BoardDefinition.DefaultColumns < 2 && i % BoardDefinition.DefaultColumns < 2;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                LevelStateBuildResult built = LevelStateBuilder.Build(small, 1);
                if (!built.IsBuilt) throw new Exception(string.Join(" / ", built.Issues));
                board.Draw(built.State, art);
                Set(preview, "State", built.State);
                typeof(PuzzleBoardPreview).GetMethod("CenterCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(preview, null);
                Check(Vector3.Distance(camera.transform.position, PuzzleWorldBoard.CellPosition(new BoardCoordinate(0, 0)) + new Vector3(0.5f, -0.5f, -10)) < 0.001f, "작은 맵 활성 영역 중앙 정렬");
                Check(board.GetComponentsInChildren<PuzzleCellView>().Length == 4, "작은 맵 활성 칸만 표시");
                Check(Mathf.Abs(board.transform.Find("Cell-0-0/Floor").GetComponent<SpriteRenderer>().bounds.size.x - 1) < 0.001f, "작은 맵도 셀 크기 유지");
            }
            finally
            {
                Set(preview, "State", previous); camera.transform.position = cameraPosition;
                UnityEngine.Object.Destroy(small);
            }
        }
        private static async UniTask VerifyDestroyedPreview(PuzzleWorldBoard board, Camera camera)
        {
            int before = Resources.FindObjectsOfTypeAll<LevelDefinition>().Length;
            GameObject root = new GameObject("Destroyed during load");
            PuzzleBoardPreview preview = root.AddComponent<PuzzleBoardPreview>();
            preview.enabled = false;
            preview.Configure(board, camera);
            UniTask pending = (UniTask)typeof(PuzzleBoardPreview).GetMethod("LoadAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(preview, null);
            Check(pending.Status == UniTaskStatus.Pending, "레벨 로드 실제 대기 상태 확인");
            UnityEngine.Object.DestroyImmediate(root);
            await pending;
            await UniTask.DelayFrame(2);
            Check(!preview.IsReady && preview.Error == null && Resources.FindObjectsOfTypeAll<LevelDefinition>().Length == before,
                "레벨 로드 중 소유자 파괴: 화면 적용·임시 레벨 누수 없음");
        }
        private static void VerifySegments(PuzzleWorldBoard board, LevelRuntimeState state)
        {
            SpriteRenderer[] wires = board.GetComponentsInChildren<SpriteRenderer>().Where(item => item.name == "Wire").ToArray();
            BoardEdge[] segments = state.Connections.SelectMany(item => LevelFlowRules.Segments(item.Vertices)).ToArray();
            for (int i = 0; i < segments.Length; i++)
            {
                bool vertical = segments[i].A.Column == segments[i].B.Column;
                Vector3 expected = (PuzzleWorldBoard.VertexPosition(segments[i].A) + PuzzleWorldBoard.VertexPosition(segments[i].B)) * 0.5f;
                Check(Vector3.Distance(wires[i].transform.localPosition, expected) < 0.001f, "전선 꼭짓점 좌표 " + i);
                Check(wires[i].sprite.name.Contains(vertical ? "vertical" : "horizontal"), "전선 방향 " + i);
                VerifyVisibleSpan(wires[i], PuzzleArtworkPaths.Wire(vertical), vertical);
            }
            SpriteRenderer[] walls = board.GetComponentsInChildren<SpriteRenderer>().Where(item => item.name == "Wall").ToArray();
            for (int i = 0; i < walls.Length; i++)
            {
                BoardEdge segment = LevelFlowRules.WallSegment(state.Flow.Walls[i]);
                bool vertical = segment.A.Column == segment.B.Column;
                Check(Vector3.Distance(walls[i].transform.localPosition, (PuzzleWorldBoard.VertexPosition(segment.A) + PuzzleWorldBoard.VertexPosition(segment.B)) * 0.5f) < 0.001f, "벽 경계 좌표 " + i);
                VerifyVisibleSpan(walls[i], PuzzleArtworkPaths.Wall(vertical), vertical);
            }
        }
        private static void VerifyVisibleSpan(SpriteRenderer renderer, string path, bool vertical)
        {
            Texture2D texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes("Assets/Textures/" + path + ".png"));
                Color32[] pixels = texture.GetPixels32();
                int min = int.MaxValue, max = int.MinValue;
                for (int i = 0; i < pixels.Length; i++)
                    if (pixels[i].a > 100)
                    { int at = vertical ? i / texture.width : i % texture.width; min = Math.Min(min, at); max = Math.Max(max, at); }
                float visible = (vertical ? renderer.bounds.size.y / texture.height : renderer.bounds.size.x / texture.width) * (max - min + 1);
                Check(visible >= 1, "선분 가시 길이 >= 한 칸: " + path + " = " + visible);
            }
            finally { UnityEngine.Object.Destroy(texture); }
        }
        private static async UniTask VerifyLoadFailure(int number, string expected)
        {
            GameObject root = new GameObject("Invalid level test");
            PuzzleBoardPreview preview = root.AddComponent<PuzzleBoardPreview>();
            typeof(PuzzleBoardPreview).GetField("levelNumber", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(preview, number);
            float deadline = Time.realtimeSinceStartup + 30;
            try
            {
                while (preview.Error == null && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
                Check(preview.Error != null && preview.Error.Contains(expected) && !preview.IsReady && preview.State == null,
                    "오류 데이터 자동 대체 없음: " + number);
            }
            finally { UnityEngine.Object.Destroy(root); }
        }
        private static void Check(bool value, string label)
        {
            if (!value) throw new Exception(label);
            results.Add("PASS " + label);
        }
        private static async UniTask Ready(PuzzleBoardPreview preview)
        {
            if (preview == null) throw new Exception("Preview component missing");
            float deadline = Time.realtimeSinceStartup + 60;
            while (!preview.IsReady && preview.Error == null && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
            Check(preview.IsReady, "독립 씬 준비: " + preview.Error);
        }
        private static void Capture(Camera camera, string file, int width, int height)
        {
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24);
            RenderTexture previous = RenderTexture.active;
            RenderTexture oldTarget = camera.targetTexture;
            float oldSize = camera.orthographicSize;
            Rect oldRect = camera.rect;
            float oldAspect = camera.aspect;
            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                // UI용 부분 뷰포트 대신 검사 이미지 전체에 월드 보드를 캡처한다.
                camera.rect = new Rect(0, 0, 1, 1);
                camera.aspect = (float)width / height;
                camera.orthographicSize = (PuzzleWorldBoard.HalfHeight + 0.7f) / Mathf.Min(1, (float)width / height);
                Check(camera.pixelRect == new Rect(0, 0, width, height), "캡처 전체 뷰포트: " + file);
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(Output + file, image.EncodeToPNG());
                Check(image.GetPixels32().Count(pixel => pixel.r > 180 && pixel.g < 190 && pixel.b > 100) > 500,
                    "캡처에 토끼 색상 실제 렌더링: " + file);
            }
            finally
            {
                camera.targetTexture = oldTarget; camera.orthographicSize = oldSize;
                camera.rect = oldRect; camera.aspect = oldAspect;
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.Destroy(image);
            }
        }
    }
}
