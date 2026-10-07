using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using GameScreen;
using GameScreen.Editor;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace Elements.Editor
{
    /// <summary>동일한 등록 데이터로 실제 편집·시험·월드 보드의 이미지 선택을 비교한다.</summary>
    [InitializeOnLoad]
    public static class ElementVisualBoardVerification
    {
        private const string PlayKey = "ElementFramework.Phase04.VisualBoards";
        private static bool metadata;
        private static bool legacy;
        private static bool flights;
        private static bool ordering;
        private static bool worldReset;
        static ElementVisualBoardVerification()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PlayKey, false)) return;
                metadata = SessionState.GetBool(PlayKey + ".Metadata", false); SessionState.EraseBool(PlayKey + ".Metadata");
                legacy = SessionState.GetBool(PlayKey + ".Legacy", false); SessionState.EraseBool(PlayKey + ".Legacy");
                flights = SessionState.GetBool(PlayKey + ".Flights", false); SessionState.EraseBool(PlayKey + ".Flights");
                ordering = SessionState.GetBool(PlayKey + ".Ordering", false); SessionState.EraseBool(PlayKey + ".Ordering");
                worldReset = SessionState.GetBool(PlayKey + ".WorldReset", false); SessionState.EraseBool(PlayKey + ".WorldReset");
                SessionState.SetBool(PlayKey, false); RunAsync().Forget(Debug.LogException);
            };
        }
        private static readonly List<string> Results = new List<string>();
        private static readonly List<UnityEngine.Object> Owned = new List<UnityEngine.Object>();
        private static void Check(bool pass, string message)
        { if (!pass) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static BoardCoordinate C(int column) => new BoardCoordinate(0, column);
        private static ElementDefinition Body(string id) => new ElementDefinition(new ElementId(id), id,
            new ElementPlacementProfile(1, 2), null, new ElementDamageSourcePolicy(true, true, false, true), null,
            new ElementDamageAggregationPolicy(false), new ElementRemovalMissionProfile(MissionKind.Crate), ElementReactionBehavior.Durability);
        private static T Keep<T>(T value) where T : UnityEngine.Object { Owned.Add(value); return value; }
        private static ElementVisualCatalogDto Visuals()
        {
            ElementVisualCatalogDto dto = new ElementVisualCatalogDto
            {
            definitions = new[]
            {
                new ElementVisualDefinitionDto { key = "fixture.wood", states = new[] { new ElementVisualFrameDto { durability = 1, path = "Obstacles/Crate/crate-durability-1-v1-256", size = .96f, effectAnimations = ElementVisualVerification.FixtureEffects("legacy.crate") } } },
                new ElementVisualDefinitionDto { key = "fixture.metal", states = new[] { new ElementVisualFrameDto { durability = 1, path = "Obstacles/Scrap/scrap-durability-1-v1-256", size = .96f, effectAnimations = ElementVisualVerification.FixtureEffects("legacy.scrap") } } }
            },
            bindings = new[]
            {
                new ElementVisualBindingDto { id = "fixture.board.wood", visualKey = "fixture.wood" },
                new ElementVisualBindingDto { id = "fixture.board.metal", visualKey = "fixture.metal" },
                new ElementVisualBindingDto { id = "fixture.board.shared", visualKey = "fixture.wood" }
            }
            };
            if (metadata)
            {
                ElementVisualFrameDto frame = dto.definitions[1].states[0];
                frame.path = "PowerBlocks/collection-drone-rotor-4frames-v1";
                frame.size = 1.2f; frame.pivotX = .25f; frame.pivotY = .75f;
                frame.offsetX = .1f; frame.offsetY = -.15f; frame.angle = 32; frame.order = 47;
                frame.sheetColumns = frame.sheetRows = 2; frame.sheetFrame = 3;
            }
            return dto;
        }
        public static void RunMetadata()
        { SessionState.SetBool(PlayKey + ".Metadata", true); Run(); }
        public static void RunLegacy()
        { SessionState.SetBool(PlayKey + ".Legacy", true); Run(); }
        public static void RunFlights()
        { SessionState.SetBool(PlayKey + ".Flights", true); Run(); }
        public static void RunOrdering()
        { SessionState.SetBool(PlayKey + ".Ordering", true); Run(); }
        public static void RunWorldReset()
        { SessionState.SetBool(PlayKey + ".WorldReset", true); Run(); }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            SessionState.SetBool(PlayKey, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        private static async UniTask RunAsync()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); Owned.Clear(); int exit = 0; PuzzleArtwork artwork = null;
            try
            {
                ConstructorInfo constructor = typeof(PuzzleArtwork).GetConstructor(new[] { typeof(ElementVisualCatalog) });
                Check(constructor != null, "월드 아트가 명시적 시각 카탈로그를 소비함");
                if (worldReset) { await VerifyWorldReset(); return; }
                if (ordering) { await VerifyOrdering(); return; }
                if (flights) { await VerifyFlights(); return; }
                if (legacy) { await VerifyLegacy(); return; }
                ElementVisualCatalogAsset visuals = Keep(ScriptableObject.CreateInstance<ElementVisualCatalogAsset>());
                JsonUtility.FromJsonOverwrite("{\"catalog\":" + JsonUtility.ToJson(Visuals()) + "}", visuals);
                ElementCatalogAsset rules = Keep(ScriptableObject.CreateInstance<ElementCatalogAsset>());
                SerializedObject authored = new SerializedObject(rules);
                SerializedProperty definitions = authored.FindProperty("definitions"); definitions.arraySize = 3;
                string[] ids = { "fixture.board.wood", "fixture.board.metal", "fixture.board.shared" };
                for (int i = 0; i < ids.Length; i++)
                {
                    ElementDefinitionAsset definition = Keep(ScriptableObject.CreateInstance<ElementDefinitionAsset>());
                    JsonUtility.FromJsonOverwrite("{\"definition\":" + JsonUtility.ToJson(PackedElementDefinition.FromDefinition(Body(ids[i]))) + "}", definition);
                    definitions.GetArrayElementAtIndex(i).objectReferenceValue = definition;
                }
                SerializedProperty visualSource = authored.FindProperty("visuals");
                Check(visualSource != null, "기존 게임 진입 설정에 제작 시각 원본 연결 경계 존재");
                visualSource.objectReferenceValue = visuals; authored.ApplyModifiedPropertiesWithoutUndo();
                LevelDefinition level = Keep(ScriptableObject.CreateInstance<LevelDefinition>());
                JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"missions\":[{\"kind\":1,\"count\":3}],\"elements\":[" + string.Join(",", ids.Select((id, index) =>
                    JsonUtility.ToJson(new ElementPlacementDefinition { definitionId = id, instanceId = "body-" + index,
                        layer = PlacementLayer.Obstacle, coordinate = C(index), durability = 1 }))) + "]}", level);
                if (metadata)
                    JsonUtility.FromJsonOverwrite("{\"elements\":" + JsonUtility.ToJson(new ElementPlacementList { values = level.Elements.Concat(new[]
                    {
                        new ElementPlacementDefinition { definitionId = "cover.web", layer = PlacementLayer.Cover, coordinate = C(1), durability = 1 },
                        new ElementPlacementDefinition { definitionId = "floor.dust", layer = PlacementLayer.Dust, coordinate = C(1), durability = 1 }
                    }).ToArray() }).Replace("{\"values\":", "").TrimEnd('}') + "}", level);
                SerializedObject levelSource = new SerializedObject(level);
                levelSource.FindProperty("elementCatalog").objectReferenceValue = rules; levelSource.ApplyModifiedPropertiesWithoutUndo();
                MethodInfo create = typeof(ElementCatalogAsset).GetMethod("CreateVisualCatalog");
                Check(create != null, "제작 원본은 값 카탈로그로 전달됨");
                ElementVisualCatalog catalog = (ElementVisualCatalog)create.Invoke(rules, null);
                string fingerprint = LevelStateBuilder.Fingerprint(level);
                byte[] bytes = LevelPackCodec.Snapshot(level);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                Check(built.IsBuilt, "새 ID 보드 구성 " + string.Join(";", built.Issues));
                LevelRuntimeState state = built.State;
                int draws = state.Random.DrawCount;
                artwork = (PuzzleArtwork)constructor.Invoke(new object[] { catalog });
                await artwork.PrepareAsync(state, CancellationToken.None);
                await LevelBoardArtwork.Warmup("Obstacles/Crate/"); await LevelBoardArtwork.Warmup("Obstacles/Scrap/");
                if (metadata)
                {
                    await LevelBoardArtwork.Warmup("PowerBlocks/"); await LevelBoardArtwork.Warmup("Obstacles/Web/"); await LevelBoardArtwork.Warmup("Obstacles/Dust/");
                    Sprite sheet = artwork.Get("PowerBlocks/collection-drone-rotor-4frames-v1");
                    ElementVisualResolver resolver = new ElementVisualResolver(catalog);
                    Rect firstFrame = default;
                    for (int frame = 1; frame <= 4; frame++)
                    {
                        ElementVisualFrame selected = resolver.Resolve(new ElementId("power.drone"), new ElementVisualState(-1, 0, 0, 0, 0, frame, 1));
                        Sprite cropped = artwork.GetVisual(selected);
                        if (frame == 1) firstFrame = cropped.rect;
                        Results.Add("INFO sheet=" + sheet.rect + " packed=" + sheet.textureRect + " frame=" + cropped.rect +
                            " sameTexture=" + (cropped.texture == sheet.texture) + " cached=" + ReferenceEquals(cropped, artwork.GetVisual(selected)));
                        Check(cropped.rect.width == sheet.rect.width / 2 && cropped.rect.height == sheet.rect.height / 2 &&
                            ReferenceEquals(cropped, artwork.GetVisual(selected)) && cropped.texture == sheet.texture,
                            "native 아틀라스 시트 프레임 " + frame + " 실제 사분면 크기/캐시/동일 텍스처");
                        Check(Mathf.Approximately(cropped.rect.x, firstFrame.x + (frame - 1) % 2 * firstFrame.width) &&
                            Mathf.Approximately(cropped.rect.y, firstFrame.y - (frame - 1) / 2 * firstFrame.height),
                            "등록 프레임의 왼쪽 위부터 행/열 선택 " + frame);
                    }
                    PuzzleArtwork sibling = new PuzzleArtwork(catalog);
                    try
                    {
                        await sibling.PrepareAsync(state, CancellationToken.None);
                        ElementVisualFrame chosen = resolver.Resolve(new ElementId("power.drone"), new ElementVisualState(-1, 0, 0, 0, 0, 1, 1));
                        Sprite ownFrame = artwork.GetVisual(chosen), otherFrame = sibling.GetVisual(chosen);
                        Check(!ReferenceEquals(ownFrame, otherFrame) && ownFrame.texture == otherFrame.texture, "두 아트 소유자는 텍스처를 공유하고 프레임 Sprite는 독립 소유");
                        sibling.Dispose(); await UniTask.Yield(); await UniTask.Yield();
                        Check(otherFrame == null && ownFrame != null && artwork.GetVisual(chosen) == ownFrame, "한 소유자 Dispose는 자기 프레임만 파기하고 다른 소유자를 유지");
                    }
                    finally { sibling.Dispose(); }
                }
                LevelBoardView edit = new LevelBoardView(); edit.Display(level, null);
                GameObject boardObject = Keep(UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Game/Puzzle/PuzzleWorldBoard.prefab")));
                PuzzleWorldBoard world = boardObject.GetComponent<PuzzleWorldBoard>(); world.Draw(state, artwork);
                MethodInfo bind = typeof(RuntimeBoardArtwork).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                    .Single(method => method.Name == "Bind" && method.GetParameters().Length == 4);
                string[] expected = { "crate-durability-1-v1-256", "scrap-durability-1-v1-256", "crate-durability-1-v1-256" };
                if (metadata) expected[1] = "collection-drone-rotor-4frames-v1(Clone)#frame-2-2-3";
                for (int i = 0; i < 3; i++)
                {
                    Sprite editor = edit.ArtworkAt(C(i), "board-content-art").style.backgroundImage.value.sprite;
                    Label test = new Label("test"); bind.Invoke(null, new object[] { test, state.CellAt(C(i)), state, catalog });
                    Sprite playback = test.Q("runtime-content").style.backgroundImage.value.sprite;
                    Sprite runtime = world.OccupantAt(C(i)).sprite;
                    Results.Add("INFO " + ids[i] + " editor=" + editor?.name + " test=" + playback?.name + " world=" + runtime?.name);
                    string name = metadata && i == 1 ? expected[i] : expected[i] + "(Clone)";
                    Check(editor != null && playback != null && runtime != null && editor.name == name &&
                        playback.name == name && runtime.name == name, "세 보드의 실제 ID 그림/공유 별칭 " + ids[i]);
                    if (metadata && i == 1)
                    {
                        foreach (VisualElement image in new[] { edit.ArtworkAt(C(i), "board-content-art"), test.Q("runtime-content") })
                        {
                            Check(Mathf.Approximately(image.style.scale.value.value.x, 1.2f) &&
                                Mathf.Approximately(image.style.rotate.value.angle.value, -32) &&
                                Mathf.Approximately(image.style.translate.value.x.value, 42) &&
                                Mathf.Approximately(image.style.translate.value.y.value, 48), "편집/시험 시트의 피벗·크기·회전·오프셋 적용 " + image.name);
                            VisualElement cover = image.name == "runtime-content" ? test.Q("runtime-cover") : edit.ArtworkAt(C(i), "board-cover-art");
                            Check(image.parent.IndexOf(image) > image.parent.IndexOf(cover), "편집/시험 등록 order47은 덮개20보다 앞 " + image.name);
                        }
                        SpriteRenderer renderer = world.OccupantAt(C(i));
                        Check(renderer.sortingOrder == 47 && Quaternion.Angle(renderer.transform.localRotation, Quaternion.Euler(0, 0, 32)) < .01f &&
                            Vector3.Distance(renderer.transform.localPosition, PuzzleWorldBoard.CellPosition(C(i)) + new Vector3(.42f, -.48f, 0)) < .001f,
                            "월드 시트의 피벗·회전·오프셋·order 적용");
                    }
                }
                PuzzleEditorLaunchRequest request = PuzzleEditorLaunchRequest.Capture(level, PuzzleEditorLevelSource.Asset, 12345);
                JsonUtility.FromJsonOverwrite("{\"catalog\":" + JsonUtility.ToJson(Visuals()).Replace(metadata ? "collection-drone-rotor" : "scrap-durability-1", "changed") + "}", visuals);
                Check(LevelStateBuilder.Fingerprint(level) == fingerprint && LevelPackCodec.Snapshot(level).SequenceEqual(bytes) &&
                    state.Random.DrawCount == draws, "시각 원본 편집은 규칙 지문 팩2 난수를 바꾸지 않음");
                MethodInfo requestVisuals = typeof(PuzzleEditorLaunchRequest).GetMethod("CreateVisualCatalog");
                Check(requestVisuals != null, "게임 진입 요청에 규칙 팩과 별도인 시각 값 전달 경계");
                ElementVisualCatalog requestCatalog = (ElementVisualCatalog)requestVisuals.Invoke(request, null);
                ElementVisualFrame retained = new ElementVisualResolver(requestCatalog).Resolve(new ElementId("fixture.board.metal"),
                    new ElementVisualState(0, 1, 0, 0, 0, 0, 1));
                Check(retained.Path == (metadata ? "PowerBlocks/collection-drone-rotor-4frames-v1" : "Obstacles/Scrap/scrap-durability-1-v1-256"), "진입 요청은 제작 원본 변경 후에도 당시 값 보존");
                visualSource.objectReferenceValue = null; authored.ApplyModifiedPropertiesWithoutUndo();
                edit.Display(level, null);
                Check(edit.CellAt(C(0)).tooltip.Contains("fixture.board.wood") &&
                    edit.CellAt(C(0)).tooltip.Contains("시각") && edit.ArtworkAt(C(0), "board-content-art").style.backgroundImage.value.sprite == null,
                    "미등록 시각 ID는 편집 오류로 보이고 기존 종류 그림으로 숨기지 않음");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                foreach (UnityEngine.Object owned in Owned.AsEnumerable().Reverse()) if (owned != null) UnityEngine.Object.DestroyImmediate(owned);
                artwork?.Dispose(); LevelBoardArtwork.ReleaseAll();
                Directory.CreateDirectory("Logs/ElementFramework/Phase04");
                File.WriteAllLines("Logs/ElementFramework/Phase04/" + (worldReset ? "world-slot-reset-results.txt" : ordering ? "visual-ordering-results.txt" : flights ? "visual-flight-native-results.txt" : legacy ? "visual-legacy-board-results.txt" : metadata ? "visual-metadata-results.txt" : "visual-board-results.txt"), Results);
                EditorApplication.Exit(exit);
            }
        }
        private static async UniTask VerifyFlights()
        {
            foreach (InitialBlockKind kind in new[] { InitialBlockKind.Rocket, InitialBlockKind.Drone })
            {
                LevelDefinition level = Keep((LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null));
                BoardCoordinate origin = new BoardCoordinate(4, 4);
                typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                    new object[] { level, origin, kind, RocketDirection.Horizontal, RabbitColor.Type1 });
                string id = "fixture.native.flight." + kind.ToString().ToLowerInvariant();
                RuntimeContent content = kind == InitialBlockKind.Rocket ? RuntimeContent.Rocket : RuntimeContent.Drone;
                ElementDefinition rule = ElementDefinition.CreateSupply(new ElementId(id), "별도 비행 정의",
                    new ElementSupplyProfile(ElementSupplyBehavior.Power, content));
                ElementCatalog rules = new ElementCatalog(LegacyElementDefinitions.DefaultCatalog.Definitions.Concat(new[] { rule }));
                LevelElementMigration.Apply(level);
                level.Elements.Single(item => item.layer == PlacementLayer.Block && item.coordinate.Equals(origin)).definitionId = id;
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345, rules);
                Check(built.IsBuilt, kind + " native 검사 실제 스키마5 신규 ID 입력 유효 " + string.Join(";", built.Issues));
                LevelRuntimeState before = built.State;
                ElementVisualFrameDto[] states = Enumerable.Range(0, kind == InitialBlockKind.Rocket ? 4 : 5).Select(frame =>
                    new ElementVisualFrameDto { frame = frame, path = frame % 2 == 0 ? "Obstacles/Crate/crate-durability-1-v1-256" : "Obstacles/Scrap/scrap-durability-1-v1-256",
                        size = 1.3f, pivotX = .25f, pivotY = .75f, offsetX = .17f, offsetY = -.12f, angle = 23, order = 49,
                        effectAnimations = (kind == InitialBlockKind.Rocket ? new[] { "trail", "impact" } : new[] { "impact" })
                            .Select(key => new ElementVisualEffectDto { key = key, frames = new[] { new ElementVisualFrameDto {
                                path = "Obstacles/Crate/crate-durability-1-v1-256", order = 40 } } }).ToArray() }).ToArray();
                ElementVisualCatalog catalog = LegacyElementVisuals.WithOverrides(new ElementVisualCatalogDto {
                    definitions = new[] { new ElementVisualDefinitionDto { key = id, states = states } },
                    bindings = new[] { new ElementVisualBindingDto { id = id, visualKey = id } } });
                PuzzleArtwork art = new PuzzleArtwork(catalog); GameObject boardObject = null;
                try
                {
                    ElementResourcePlan plan = ElementResourcePlan.Create(before, catalog);
                    Check(plan.Frames.Count(frame => states.Any(selected => selected.path == frame.Path) && frame.Order == 49) == states.Length,
                        kind + " 초기 신규 파워의 모든 비행 프레임을 시작 준비에 포함");
                    await art.PrepareAsync(before, CancellationToken.None);
                    BoardActionExecutor executor = new BoardActionExecutor(before); BoardActionResult action = executor.Activate(origin);
                    Check(action.IsApplied, kind + " native 신규 ID 발동 유효");
                    PuzzleEffectTimeline timeline = new PuzzleEffectTimeline(before, action.Changes, action.Effects, action.PowerTrace);
                    Type type = typeof(PuzzleWorldBoard).Assembly.GetType("GameScreen.PuzzlePowerPlayback");
                    object playback = Activator.CreateInstance(type, true);
                    MethodInfo prepare = type.GetMethod("PrepareAsync", BindingFlags.Instance | BindingFlags.NonPublic);
                    MethodInfo begin = type.GetMethod("Begin", BindingFlags.Instance | BindingFlags.NonPublic);
                    MethodInfo reset = type.GetMethod("Reset", BindingFlags.Instance | BindingFlags.NonPublic);
                    await (UniTask)prepare.Invoke(playback, new object[] { before, executor.State, art, timeline, CancellationToken.None });
                    boardObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Game/Puzzle/PuzzleWorldBoard.prefab"));
                    PuzzleWorldBoard board = boardObject.GetComponent<PuzzleWorldBoard>();
                    object[] clips = ((System.Collections.IEnumerable)type.GetField("clips", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playback)).Cast<object>().ToArray();
                    MethodInfo effectAt = typeof(PuzzleWorldBoard).GetMethod("EffectAt", BindingFlags.Instance | BindingFlags.NonPublic);
                    for (int index = 0; index < clips.Length; index++) effectAt.Invoke(board, new object[] { index });
                    begin.Invoke(playback, new object[] { board });
                    int objects = boardObject.GetComponentsInChildren<Transform>(true).Length;
                    int addresses = art.AtlasCount;
                    for (int repeat = 0; repeat < 5; repeat++)
                    {
                        SpriteRenderer flight = boardObject.GetComponentsInChildren<SpriteRenderer>(true).First(image =>
                            image.transform.parent.name == (kind == InitialBlockKind.Rocket ? "Rocket-flight" : "Drone-hover") && image.enabled);
                        Check(flight.sprite.name.Contains(kind == InitialBlockKind.Rocket ? "crate-durability-1" : "scrap-durability-1") &&
                            flight.GetComponentInParent<UnityEngine.Rendering.SortingGroup>().sortingOrder == 49 &&
                            Mathf.Abs(Mathf.DeltaAngle(flight.transform.localEulerAngles.z, 23)) < .01f,
                            kind + " native 등록 그림/회전/order 재생 " + repeat);
                        Check(flight.maskInteraction == SpriteMaskInteraction.None && !flight.flipX && !flight.flipY &&
                            flight.transform.localPosition.sqrMagnitude > 0, kind + " native 등록 피벗/오프셋 적용 " + repeat);
                        reset.Invoke(playback, null);
                        Check(boardObject.GetComponentsInChildren<SpriteRenderer>(true).Where(image => image.name == "Effect-playback")
                            .All(image => !image.enabled && image.sprite == null), kind + " 취소·반환 Sprite 참조/잔상0 " + repeat);
                        await (UniTask)prepare.Invoke(playback, new object[] { before, executor.State, art, timeline, CancellationToken.None });
                        begin.Invoke(playback, new object[] { board });
                        Check(boardObject.GetComponentsInChildren<Transform>(true).Length == objects && art.AtlasCount == addresses,
                            kind + " 준비한 native 슬롯 반복 추가 생성/주소 증가0 " + repeat);
                    }
                    HashSet<string> shown = new HashSet<string>();
                    bool registeredImpact = false;
                    MethodInfo tick = type.GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic);
                    for (float time = 0; time <= timeline.Duration + .04f; time += .02f)
                    {
                        foreach (SpriteRenderer image in boardObject.GetComponentsInChildren<SpriteRenderer>(true).Where(image => image.enabled &&
                            (image.transform.parent.name == "Rocket-flight" || image.transform.parent.name == "Drone-hover" || image.transform.parent.name == "Drone-flight")))
                            shown.Add(image.sprite.name);
                        foreach (SpriteRenderer image in boardObject.GetComponentsInChildren<SpriteRenderer>(true).Where(image => image.enabled &&
                            image.transform.parent.name == (kind == InitialBlockKind.Rocket ? "rocket-impact" : "drone-impact")))
                            registeredImpact |= image.sprite.name.Contains("crate-durability-1") &&
                                image.GetComponentInParent<UnityEngine.Rendering.SortingGroup>().sortingOrder == 40;
                        tick.Invoke(playback, new object[] { .02f });
                    }
                    Check(shown.Any(name => name.Contains("crate-durability-1")) && shown.Any(name => name.Contains("scrap-durability-1")),
                        kind + " native 시간 진행 중 등록 프레임 그림 교대");
                    Check(registeredImpact, kind + " native 타격 효과도 신규 ID의 등록 그림 재생");
                    Check(boardObject.GetComponentsInChildren<SpriteRenderer>(true).Where(image => image.name == "Effect-playback")
                        .All(image => !image.enabled && image.sprite == null), kind + " 정상 종료 효과 Sprite 잔상0");
                    Check(boardObject.GetComponentsInChildren<Transform>(true).Length == objects && art.AtlasCount == addresses,
                        kind + " 정상 종료 준비 용량/주소 유지");
                    reset.Invoke(playback, null);
                    Sprite[] ownedFrames = clips.Where(clip => clip.GetType().GetField("VisualFrames", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clip) != null)
                        .SelectMany(clip => (Sprite[])clip.GetType().GetField("Frames", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(clip)).Distinct().ToArray();
                    SpriteRenderer[] images = boardObject.GetComponentsInChildren<SpriteRenderer>(true);
                    UnityEngine.Object.Destroy(boardObject); boardObject = null; await UniTask.Yield(); await UniTask.Yield();
                    Check(images.All(image => image == null), kind + " 보드 Destroy 후 native 표시 객체 소멸");
                    art.Dispose(); await UniTask.Yield(); await UniTask.Yield();
                    Check(art.AtlasCount == 0 && ownedFrames.All(sprite => sprite == null), kind + " 아트 Dispose 후 native 프레임/아틀라스 소유권 반환");
                }
                finally { if (boardObject != null) UnityEngine.Object.DestroyImmediate(boardObject); art.Dispose(); }
                await UniTask.Yield(); await UniTask.Yield();
            }
        }
        private static async UniTask VerifyWorldReset()
        {
            LevelDefinition large = Keep((LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null));
            LevelDefinition small = Keep((LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null));
            BoardCoordinate bodyAt = new BoardCoordinate(4, 4), inactive = new BoardCoordinate(0, 0);
            LevelObstacleEditing.Apply(large, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { bodyAt });
            LevelObstacleEditing.Apply(large, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = 2 }, new[] { bodyAt });
            LevelFlowEditing.SetWalls(large, new[] { new BoardEdge(new BoardCoordinate(6, 6), new BoardCoordinate(6, 7)) }, false);
            LevelObstacleEditing.Apply(small, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { inactive });
            LevelBoardEditing.Apply(small, LevelBrush.Deactivate, RabbitColor.Type1, new[] { inactive });
            LevelStateBuildResult big = LevelStateBuilder.Build(large, 12345), tiny = LevelStateBuilder.Build(small, 12345);
            Check(big.IsBuilt && tiny.IsBuilt && big.State.Obstacles.Count == 1 && big.State.Flow.Walls.Count == 1,
                "본체/장식/비활성 슬롯 반복 입력 유효 big=" + big.IsBuilt + " tiny=" + tiny.IsBuilt +
                " bodies=" + (big.IsBuilt ? big.State.Obstacles.Count : -1) + " walls=" + (big.IsBuilt ? big.State.Flow.Walls.Count : -1) +
                " issues=" + string.Join(";", big.Issues.Concat(tiny.Issues)));
            using PuzzleArtwork art = new PuzzleArtwork();
            await art.PrepareAsync(big.State, CancellationToken.None); await art.PrepareAsync(tiny.State, CancellationToken.None);
            Scene scene = SceneManager.CreateScene("Phase04-owned-world-slot-scene");
            GameObject boardObject = null;
            try
            {
                boardObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Game/Puzzle/PuzzleWorldBoard.prefab"));
                SceneManager.MoveGameObjectToScene(boardObject, scene);
                PuzzleWorldBoard board = boardObject.GetComponent<PuzzleWorldBoard>(); board.Draw(big.State, art);
                object Read(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
                PuzzleCellView[] cells = ((System.Collections.IEnumerable)Read(board, "cells")).Cast<PuzzleCellView>().ToArray();
                SpriteRenderer[] bodyPool = ((System.Collections.IEnumerable)Read(board, "bodies")).Cast<SpriteRenderer>().ToArray();
                SpriteRenderer[] decorationPool = ((System.Collections.IEnumerable)Read(board, "decorations")).Cast<SpriteRenderer>().ToArray();
                SpriteRenderer floor = (SpriteRenderer)Read(cells[0], "floor");
                int[] capacity = boardObject.GetComponentsInChildren<Transform>(true).Select(value => value.GetInstanceID()).ToArray();
                string input = JsonUtility.ToJson(large); int random = big.State.Random.DrawCount;
                for (int repeat = 0; repeat < 5; repeat++)
                {
                    foreach (SpriteRenderer image in boardObject.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        image.color = Color.magenta; image.flipX = image.flipY = true; image.sortingOrder = 999;
                        image.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                        image.transform.localPosition = Vector3.one * 9; image.transform.localRotation = Quaternion.Euler(0, 0, 73);
                        image.transform.localScale = Vector3.one * 5;
                    }
                    foreach (PuzzleCellView cell in cells) { cell.transform.localRotation = Quaternion.Euler(0, 0, 19); cell.transform.localScale = Vector3.one * 2; }
                    board.Draw(big.State, art);
                    Check(floor.color == Color.white && !floor.flipX && !floor.flipY && floor.sortingOrder == 0 &&
                        floor.maskInteraction == SpriteMaskInteraction.None && floor.transform.localPosition == Vector3.zero && floor.transform.localRotation == Quaternion.identity,
                        "바닥 슬롯 재사용의 색/프레임/order/마스크/변환 오염0 " + repeat);
                    Check(cells.All(cell => cell.transform.localScale == Vector3.one && cell.transform.localRotation == Quaternion.identity), "셀 루트 재사용의 축척/회전 오염0 " + repeat);
                    Check(decorationPool.All(image => image.color == Color.white && !image.flipX && !image.flipY && image.maskInteraction == SpriteMaskInteraction.None &&
                        image.transform.localRotation == Quaternion.identity), "장식 슬롯 재사용의 색/뒤집기/마스크/회전 오염0 " + repeat);
                    board.Draw(tiny.State, art);
                    Check(bodyPool.Concat(decorationPool).All(image => !image.gameObject.activeSelf && !image.enabled && image.sprite == null && image.color == Color.white &&
                        image.sortingOrder == 0 && !image.flipX && !image.flipY && image.maskInteraction == SpriteMaskInteraction.None &&
                        image.transform.localPosition == Vector3.zero && image.transform.localScale == Vector3.one && image.transform.localRotation == Quaternion.identity),
                        "반환된 본체/장식의 참조/표시/변환 오염0 " + repeat);
                    Check(!cells[0].gameObject.activeSelf && cells[0].GetComponentsInChildren<SpriteRenderer>(true).All(image => image.sprite == null && !image.enabled && image.sortingOrder == 0),
                        "비활성 셀 반환의 네 층 Sprite/order 잔류0 " + repeat);
                    Check(boardObject.GetComponentsInChildren<Transform>(true).Select(value => value.GetInstanceID()).SequenceEqual(capacity), "준비 용량 안 보드 반복의 추가 생성0 " + repeat);
                }
                board.Draw(big.State, art);
                BoardCoordinate selectedAt = new BoardCoordinate(0, 1);
                SpriteRenderer selected = board.OccupantAt(selectedAt); int originalOrder = selected.sortingOrder;
                SpriteRenderer neighbor = board.OccupantAt(new BoardCoordinate(0, 2)); neighbor.sortingOrder = 90;
                board.Preview(selectedAt, Vector3.right * .2f);
                Check(selected.sortingOrder > neighbor.sortingOrder, "선택 블록은 높은 order의 정지 이웃보다 최상단");
                board.ClearPreview(); Check(selected.sortingOrder == originalOrder, "손을 놓으면 선택 전 등록 order 복원");
                Check(JsonUtility.ToJson(large) == input && big.State.Random.DrawCount == random, "풀 반복/선택의 원본/규칙 난수 무변경");
                SpriteRenderer[] renderers = boardObject.GetComponentsInChildren<SpriteRenderer>(true);
                await SceneManager.UnloadSceneAsync(scene).ToUniTask(); await UniTask.DelayFrame(2);
                Check(boardObject == null && renderers.All(image => image == null) && cells.All(cell => cell == null), "실제 메모리 씬 Unload 후 셀/본체/장식 렌더러 잔류0");
                Check(art.AtlasCount > 0 && art.Get("Blocks/rabbit-pink-v1-256") != null, "보드 씬 Unload는 외부 아트 소유권을 반환하지 않음");
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) await SceneManager.UnloadSceneAsync(scene).ToUniTask();
                if (boardObject != null) UnityEngine.Object.Destroy(boardObject);
            }
        }
        private static async UniTask VerifyOrdering()
        {
            LevelDefinition level = Keep(ScriptableObject.CreateInstance<LevelDefinition>());
            JsonUtility.FromJsonOverwrite("{\"schemaVersion\":4,\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]," +
                "\"obstacles\":[{\"id\":\"large\",\"coordinate\":{\"row\":3,\"column\":3},\"kind\":4,\"durability\":1,\"color\":0}]," +
                "\"initialBlocks\":[{\"coordinate\":{\"row\":3,\"column\":5},\"kind\":1,\"fixedColor\":0}]," +
                "\"covers\":[{\"coordinate\":{\"row\":3,\"column\":5},\"kind\":0,\"durability\":1}]}", level);
            LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
            Check(built.IsBuilt, "2×2의 시각 확장과 인접 덮개 입력 유효 " + string.Join(";", built.Issues));
            await LevelBoardArtwork.Warmup("Obstacles/MetalRodBox/"); await LevelBoardArtwork.Warmup("Obstacles/Web/");
            LevelBoardView board = new LevelBoardView(); board.Display(level, null);
            VisualElement body = board.LargeBodies.Single();
            VisualElement cover = board.ArtworkAt(new BoardCoordinate(3, 5), "board-cover-art");
            Check(body.style.backgroundImage.value.sprite != null && cover.style.backgroundImage.value.sprite != null,
                "인접한 실제 본체와 덮개 Sprite 표시");
            ElementVisualFrame bodyFrame = (ElementVisualFrame)body.userData, coverFrame = (ElementVisualFrame)cover.userData;
            Check(bodyFrame.Size > 2 && coverFrame.Order > bodyFrame.Order, "2×2의 인접 영역 확장과 등록 덮개 정렬 우선");
            VisualElement common = body.parent, bodyBranch = body, coverBranch = cover;
            while (common != null)
            {
                coverBranch = cover;
                while (coverBranch.parent != null && coverBranch.parent != common) coverBranch = coverBranch.parent;
                if (coverBranch.parent == common) break;
                common = common.parent;
            }
            while (bodyBranch.parent != common) bodyBranch = bodyBranch.parent;
            Check(common != null && common.IndexOf(coverBranch) > common.IndexOf(bodyBranch), "편집 보드의 인접 덮개20은 2×2 본체10보다 앞에 합성");
            Check(body.parent == cover.parent && cover.parent.parent == board &&
                board.IndexOf(board.AnnotationAt(new BoardCoordinate(3, 5), "board-art-badge").parent.parent) > board.IndexOf(cover.parent),
                "실제 이미지의 공통 합성 층과 숫자/오류 최상단 유지");
            LevelElementMigration.Apply(level); board.Display(level, null);
            body = board.LargeBodies.Single(); cover = board.ArtworkAt(new BoardCoordinate(3, 5), "board-cover-art");
            Check(body.parent == cover.parent && body.parent.IndexOf(cover) > body.parent.IndexOf(body), "스키마5도 인접 덮개와 대형 본체의 공통 정렬");
            ElementVisualCatalogDto visualDto = LegacyElementVisuals.Catalog.ToDto();
            foreach (ElementVisualFrameDto frame in visualDto.definitions.Single(definition => definition.key == "legacy.rod-box").states) frame.order = 47;
            ElementVisualCatalogAsset visual = Keep(ScriptableObject.CreateInstance<ElementVisualCatalogAsset>());
            JsonUtility.FromJsonOverwrite("{\"catalog\":" + JsonUtility.ToJson(visualDto) + "}", visual);
            ElementCatalogAsset rules = Keep(ScriptableObject.CreateInstance<ElementCatalogAsset>());
            SerializedObject ruleSource = new SerializedObject(rules); ruleSource.FindProperty("visuals").objectReferenceValue = visual;
            ruleSource.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject source = new SerializedObject(level); source.FindProperty("elementCatalog").objectReferenceValue = rules;
            source.ApplyModifiedPropertiesWithoutUndo();
            string input = JsonUtility.ToJson(level); byte[] packed = LevelPackCodec.Snapshot(level);
            for (int repeat = 0; repeat < 3; repeat++)
            {
                board.Display(level, null); body = board.LargeBodies.Single(); cover = board.ArtworkAt(new BoardCoordinate(3, 5), "board-cover-art");
                Check(((ElementVisualFrame)body.userData).Order == 47 && body.parent == cover.parent && body.parent.IndexOf(body) > body.parent.IndexOf(cover),
                    "반복 표시도 등록 본체47을 덮개20보다 앞에 합성 " + repeat);
            }
            Check(JsonUtility.ToJson(level) == input && LevelPackCodec.Snapshot(level).SequenceEqual(packed), "정렬 반복 후 원본/규칙 팩 무변경");
        }
        private static async UniTask VerifyLegacy()
        {
            ElementVisualCatalogAsset visual = Keep(ScriptableObject.CreateInstance<ElementVisualCatalogAsset>());
            ElementVisualCatalogDto dto = new ElementVisualCatalogDto
            {
                definitions = new[] { new ElementVisualDefinitionDto { key = "fixture.legacy", states = new[] {
                    new ElementVisualFrameDto { path = "Obstacles/Scrap/scrap-durability-1-v1-256", size = 1.2f,
                        pivotX = .25f, pivotY = .75f, offsetX = .1f, offsetY = -.15f, angle = 32, order = 47 } } } },
                bindings = new[] { "supply.normal.fixed", "power.rocket", "obstacle.crate.wood", "cover.web", "floor.dust", "obstacle.metal-rod-box" }
                    .Select(id => new ElementVisualBindingDto { id = id, visualKey = "fixture.legacy" }).ToArray()
            };
            JsonUtility.FromJsonOverwrite("{\"catalog\":" + JsonUtility.ToJson(dto) + "}", visual);
            ElementCatalogAsset rules = Keep(ScriptableObject.CreateInstance<ElementCatalogAsset>());
            SerializedObject authored = new SerializedObject(rules);
            authored.FindProperty("visuals").objectReferenceValue = visual; authored.ApplyModifiedPropertiesWithoutUndo();
            LevelDefinition level = Keep(ScriptableObject.CreateInstance<LevelDefinition>());
            JsonUtility.FromJsonOverwrite("{\"schemaVersion\":4,\"missions\":[{\"kind\":1,\"count\":1}]," +
                "\"initialBlocks\":[{\"coordinate\":{\"row\":0,\"column\":0},\"kind\":1,\"fixedColor\":0}," +
                "{\"coordinate\":{\"row\":0,\"column\":1},\"kind\":2,\"rocketDirection\":0}]," +
                "\"obstacles\":[{\"id\":\"crate\",\"coordinate\":{\"row\":0,\"column\":2},\"kind\":0,\"durability\":1}]," +
                "\"covers\":[{\"coordinate\":{\"row\":0,\"column\":3},\"kind\":0,\"durability\":1}]," +
                "\"dust\":[{\"coordinate\":{\"row\":0,\"column\":4},\"durability\":1}]}", level);
            SerializedObject source = new SerializedObject(level); source.FindProperty("elementCatalog").objectReferenceValue = rules;
            source.ApplyModifiedPropertiesWithoutUndo();
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle,
                Kind = (int)ObstacleKind.Appliance, Durability = 1, Color = RabbitColor.Type1 }, new[] { new BoardCoordinate(3, 3) });
            string original = JsonUtility.ToJson(level); byte[] packed = LevelPackCodec.Snapshot(level);
            await LevelBoardArtwork.Warmup("Obstacles/Scrap/");
            LevelBoardView board = new LevelBoardView(); board.Display(level, null);
            for (int column = 0; column < 5; column++)
            {
                VisualElement image = board.ArtworkAt(C(column), column == 3 ? "board-cover-art" : column == 4 ? "board-dust-art" : "board-content-art");
                Check(image.style.backgroundImage.value.sprite?.name == "scrap-durability-1-v1-256(Clone)", "구형 편집 층도 등록 시각 별칭 선택 " + column);
                Check(Mathf.Approximately(image.style.scale.value.value.x, 1.2f) &&
                    Mathf.Approximately(image.style.rotate.value.angle.value, -32), "구형 편집 층의 등록 크기·회전 적용 " + column);
            }
            VisualElement body = board.LargeBodies.Single();
            Check(body.style.backgroundImage.value.sprite?.name == "scrap-durability-1-v1-256(Clone)" &&
                Mathf.Approximately(body.style.width.value.value, LevelBoardView.CellSize * 1.2f) &&
                Mathf.Approximately(body.style.rotate.value.angle.value, -32), "구형 2×2도 등록 이미지·전체 크기·회전 적용");
            Check(JsonUtility.ToJson(level) == original && LevelPackCodec.Snapshot(level).SequenceEqual(packed), "구형 표시 조회는 원본·팩 무변경");
            dto.definitions[0].states[0].durability = 9;
            JsonUtility.FromJsonOverwrite("{\"catalog\":" + JsonUtility.ToJson(dto) + "}", visual);
            board.Display(level, null);
            Check(board.ArtworkAt(C(2), "board-content-art").style.backgroundImage.value.sprite == null &&
                board.CellAt(C(2)).tooltip.Contains("obstacle.crate.wood") && board.CellAt(C(2)).tooltip.Contains("내구도=1"),
                "구형 상태 누락은 ID 오류로 표시·기존 이미지 대체 금지");
            authored.FindProperty("visuals").objectReferenceValue = null; authored.ApplyModifiedPropertiesWithoutUndo();
            await LevelBoardArtwork.Warmup("Blocks/"); await LevelBoardArtwork.Warmup("PowerBlocks/");
            await LevelBoardArtwork.Warmup("Obstacles/Crate/"); await LevelBoardArtwork.Warmup("Obstacles/MetalRodBox/");
            board.Display(level, null);
            VisualElement rocket = board.ArtworkAt(C(1), "board-content-art");
            Check(rocket.style.backgroundImage.value.sprite?.name == "cleaning-rocket-horizontal-v1(Clone)" &&
                Mathf.Approximately(rocket.style.scale.value.value.x, 1.12f) &&
                Mathf.Approximately(rocket.style.rotate.value.angle.value, 0), "구형 기본 로켓 비율 유지·이전 회전 제거");
            body = board.LargeBodies.Single();
            Check(Mathf.Approximately(body.style.width.value.value, LevelBoardView.CellSize * BoardArtworkLayout.LargeObstacleSize) &&
                Mathf.Approximately(body.style.rotate.value.angle.value, 0), "구형 기본 2×2 크기 유지·이전 회전 제거");
        }
        [Serializable] private sealed class ElementPlacementList { public ElementPlacementDefinition[] values; }
    }
}
