using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Board;
using Elements;
using Levels;
using Levels.Editor;
using Simulation;

namespace GameScreen.Editor
{
    public static class PuzzleEffectSlotVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static bool combinations;
        private static void Check(bool pass, string message)
        { if (!pass) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static T Field<T>(object owner, string name) => (T)owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        public static void RunDamageClips()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0;
            try
            {
                foreach (string suffix in new[] { "a", "b" })
                {
                    LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    PuzzleArtwork art = null;
                    try
                    {
                        BoardCoordinate origin = new BoardCoordinate(4, 4), target = new BoardCoordinate(4, 6);
                        typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                            new object[] { level, origin, InitialBlockKind.Rocket, RocketDirection.Horizontal, RabbitColor.Type1 });
                        LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Block, Erase = true }, new[] { target });
                        LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = 2 }, new[] { target });
                        Elements.Editor.LevelElementMigration.Apply(level);
                        string id = "fixture.damage." + suffix;
                        level.Elements.Single(item => item.layer == PlacementLayer.Obstacle && item.coordinate.Equals(target)).definitionId = id;
                        ElementDefinition rule = new ElementDefinition(new ElementId(id), id, new ElementPlacementProfile(1, 2), null,
                            new ElementDamageSourcePolicy(true, true, false, true), null, new ElementDamageAggregationPolicy(false),
                            new ElementRemovalMissionProfile(MissionKind.Crate), ElementReactionBehavior.Durability);
                        LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345,
                            new ElementCatalog(LegacyElementDefinitions.DefaultCatalog.Definitions.Concat(new[] { rule })));
                        Check(built.IsBuilt, "타격 효과 신규 ID 입력 유효 " + suffix + " " + string.Join(";", built.Issues));
                        string path = "Effects/GeneratorCharge/charge-pulse-0" + (suffix == "a" ? "1" : "2") + "-v1-256";
                        string visualJson = "{\"definitions\":[{\"key\":\"" + id + "\",\"states\":[{\"path\":\"Obstacles/Crate/crate-durability-1-v1-256\",\"effectAnimations\":[{\"key\":\"damage\",\"frames\":[{\"path\":\"" + path + "\",\"size\":1.3,\"order\":48}]}]}]}],\"bindings\":[{\"id\":\"" + id + "\",\"visualKey\":\"" + id + "\"}]}";
                        art = new PuzzleArtwork(LegacyElementVisuals.WithOverrides(JsonUtility.FromJson<ElementVisualCatalogDto>(visualJson)));
                        BoardActionExecutor executor = new BoardActionExecutor(built.State); BoardActionResult action = executor.Activate(origin);
                        Check(action.IsApplied, "타격 효과 실제 로켓 발동 " + suffix + " " + action.Message);
                        Type type = typeof(PuzzleWorldBoard).Assembly.GetType("GameScreen.PuzzlePowerPlayback");
                        object playback = Activator.CreateInstance(type, true);
                        type.GetField("art", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(playback, art);
                        type.GetField("final", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(playback, executor.State);
                        type.GetField("timeline", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(playback,
                            new PuzzleEffectTimeline(built.State, action.Changes, action.Effects, action.PowerTrace));
                        type.GetMethod("BuildClips", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { built.State });
                        object[] clips = ((IEnumerable)type.GetField("clips", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playback)).Cast<object>().ToArray();
                        Check(clips.Any(clip => Field<string[]>(clip, "Paths").Contains(path)), "같은 본체 행동의 두 ID가 실제 타격 클립에서 별도 등록 효과 선택 " + suffix);
                        Check(clips.Where(clip => Field<string[]>(clip, "Paths").Contains(path)).All(clip =>
                            Field<ElementVisualFrame[]>(clip, "VisualFrames").Single().Order == 48), "타격 클립의 등록 프레임 정렬 보존 " + suffix);
                    }
                    finally { art?.Dispose(); UnityEngine.Object.DestroyImmediate(level); }
                }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            File.WriteAllLines("Logs/ElementFramework/Phase04/effect-damage-clips-results.txt", Results);
            EditorApplication.Exit(exit);
        }
        public static void RunCombinations()
        { combinations = true; RunClips(); }
        public static void RunClips()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0;
            try
            {
                foreach (InitialBlockKind kind in new[] { InitialBlockKind.Rocket, InitialBlockKind.Drone })
                {
                    LevelDefinition level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                    PuzzleArtwork art = null;
                    try
                    {
                        BoardCoordinate origin = new BoardCoordinate(4, 4);
                        typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                            new object[] { level, origin, kind, RocketDirection.Horizontal, RabbitColor.Type1 });
                        RuntimeContent content = kind == InitialBlockKind.Rocket ? RuntimeContent.Rocket : RuntimeContent.Drone;
                        string id = "fixture.flight." + kind.ToString().ToLowerInvariant();
                        ElementDefinition rule = ElementDefinition.CreateSupply(new ElementId(id), "별도 비행 그림",
                            new ElementSupplyProfile(ElementSupplyBehavior.Power, content));
                        BoardCoordinate second = new BoardCoordinate(4, 5);
                        if (combinations)
                            typeof(PowerEffectVerification).GetMethod("Place", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                                new object[] { level, second, InitialBlockKind.Bomb, RocketDirection.Horizontal, RabbitColor.Type1 });
                        Elements.Editor.LevelElementMigration.Apply(level);
                        level.Elements.Single(item => item.layer == PlacementLayer.Block && item.coordinate.Equals(origin)).definitionId = id;
                        LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345,
                            new ElementCatalog(LegacyElementDefinitions.DefaultCatalog.Definitions.Concat(new[] { rule })));
                        Check(built.IsBuilt, kind + " 실제 스키마5 신규 ID 입력 유효 " + string.Join(";", built.Issues));
                        LevelRuntimeState before = built.State;
                        ElementVisualFrameDto[] states = Enumerable.Range(0, kind == InitialBlockKind.Rocket ? 4 : 5)
                            .Select(frame => new ElementVisualFrameDto { frame = frame, path = "Obstacles/Crate/crate-durability-1-v1-256",
                                size = 1.3f, offsetX = .17f, offsetY = -.12f, angle = 23, order = 49,
                                effectAnimations = (kind == InitialBlockKind.Rocket ? new[] { "trail", "impact" } : new[] { "impact" })
                                    .Select(key => new ElementVisualEffectDto { key = key, frames = new[] {
                                        new ElementVisualFrameDto { path = "Obstacles/Crate/crate-durability-1-v1-256", order = 48 } } }).ToArray() }).ToArray();
                        art = new PuzzleArtwork(LegacyElementVisuals.WithOverrides(new ElementVisualCatalogDto {
                            definitions = new[] { new ElementVisualDefinitionDto { key = id, states = states } },
                            bindings = new[] { new ElementVisualBindingDto { id = id, visualKey = id } } }));
                        BoardActionExecutor executor = new BoardActionExecutor(before);
                        BoardActionResult action = combinations ? executor.Swap(origin, second) : executor.Activate(origin);
                        Check(action.IsApplied, kind + " 신규 ID 실제 발동 유효 " + action.Message);
                        if (combinations) Check(action.PowerTrace.Combination != null, kind + " 실제 파워 조합 실행");
                        Type playbackType = typeof(PuzzleWorldBoard).Assembly.GetType("GameScreen.PuzzlePowerPlayback");
                        object playback = Activator.CreateInstance(playbackType, true);
                        playbackType.GetField("art", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(playback, art);
                        playbackType.GetField("final", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(playback, executor.State);
                        playbackType.GetField("timeline", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(playback,
                            new PuzzleEffectTimeline(before, action.Changes, action.Effects, action.PowerTrace));
                        playbackType.GetMethod("BuildClips", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(playback, new object[] { before });
                        object[] flights = ((IEnumerable)playbackType.GetField("clips", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playback))
                            .Cast<object>().Where(clip => Field<string>(clip, "Label").Contains(kind == InitialBlockKind.Rocket ? "Rocket-flight" : "Drone-") &&
                                !Field<string>(clip, "Label").Contains("impact")).ToArray();
                        Check(flights.Length > 0, kind + " 비행 클립 존재");
                        foreach (object clip in flights)
                        {
                            FieldInfo selected = clip.GetType().GetField("VisualFrames", BindingFlags.Instance | BindingFlags.NonPublic);
                            Check(selected != null, kind + " 비행 클립은 등록 프레임을 보관");
                            ElementVisualFrame[] frames = (ElementVisualFrame[])selected.GetValue(clip);
                            Check(frames != null && frames.Length == 4 && frames.All(frame => frame.Path == states[0].path && frame.Order == 49 && frame.Size == 1.3f),
                                kind + " 실제 원본 ID의 비행 그림/크기/정렬 선택");
                        }
                        object[] impacts = ((IEnumerable)playbackType.GetField("clips", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playback))
                            .Cast<object>().Where(clip => Field<string>(clip, "Label") == (kind == InitialBlockKind.Rocket ? "rocket-impact" : "drone-impact")).ToArray();
                        if (!combinations) Check(impacts.Length > 0 && impacts.All(clip => Field<string[]>(clip, "Paths").Length == 1 && Field<string[]>(clip, "Paths")[0] == states[0].path &&
                            Field<ElementVisualFrame[]>(clip, "VisualFrames").Single().Order == 48), kind + " 타격 효과도 원본 신규 ID의 등록 값을 사용");
                    }
                    finally { art?.Dispose(); UnityEngine.Object.DestroyImmediate(level); }
                }
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            File.WriteAllLines("Logs/ElementFramework/Phase04/" + (combinations ? "effect-combination-visual-results.txt" : "effect-visual-clips-results.txt"), Results);
            EditorApplication.Exit(exit);
        }
        public static void RunSwap()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0; GameObject root = null; LevelDefinition level = null;
            try
            {
                level = (LevelDefinition)typeof(PowerEffectVerification).GetMethod("Make", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                Check(built.IsBuilt, "교환 표시 검사 입력 유효");
                LevelRuntimeState display = (LevelRuntimeState)Activator.CreateInstance(typeof(LevelRuntimeState),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { built.State }, null);
                BoardCoordinate first = new BoardCoordinate(4, 4), second = new BoardCoordinate(4, 5);
                RuntimeCell a = display.CellAt(first), b = display.CellAt(second);
                ElementDefinition one = ElementDefinition.CreateSupply(new ElementId("fixture.swap.first"), "첫 그림",
                    new ElementSupplyProfile(ElementSupplyBehavior.FixedNormal, RuntimeContent.Normal));
                ElementDefinition two = ElementDefinition.CreateSupply(new ElementId("fixture.swap.second"), "둘째 그림",
                    new ElementSupplyProfile(ElementSupplyBehavior.FixedNormal, RuntimeContent.Normal));
                PropertyInfo element = typeof(RuntimeCell).GetProperty("ContentElement", BindingFlags.Instance | BindingFlags.NonPublic);
                element.SetValue(a, one); element.SetValue(b, two);
                RabbitColor? colorA = a.Color, colorB = b.Color;
                CoverKind? coverA = a.Cover, coverB = b.Cover;
                int random = display.Random.DrawCount;
                root = new GameObject("Phase04 swap presentation"); PuzzleGameSession session = root.AddComponent<PuzzleGameSession>();
                typeof(PuzzleGameSession).GetField("presentationBefore", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, display);
                typeof(PuzzleGameSession).GetMethod("SwapPresentationState", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(session, new object[] { first, second });
                Check(ReferenceEquals(element.GetValue(a), two) && ReferenceEquals(element.GetValue(b), one),
                    "교환 표시 사본은 서로 다른 실제 요소 정의 ID를 점유자와 함께 이동");
                Check(a.Color == colorB && b.Color == colorA && a.Cover == coverA && b.Cover == coverB &&
                    display.Random.DrawCount == random && !ReferenceEquals(element.GetValue(built.State.CellAt(first)), two),
                    "교환 표시의 색 이동/덮개 고정 및 원본/난수 무변경");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (level != null) UnityEngine.Object.DestroyImmediate(level);
            }
            Directory.CreateDirectory("Logs/ElementFramework/Phase04");
            File.WriteAllLines("Logs/ElementFramework/Phase04/swap-visual-id-results.txt", Results);
            EditorApplication.Exit(exit);
        }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0;
            GameObject board = null, prefab = null; Sprite sprite = null;
            try
            {
                board = new GameObject("Phase04 effect pool"); prefab = new GameObject("Effect template", typeof(SpriteRenderer));
                sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
                Type type = typeof(PuzzleWorldBoard).Assembly.GetType("GameScreen.PuzzleEffectSprite");
                object slot = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
                    new object[] { board.transform, prefab.GetComponent<SpriteRenderer>(), sprite }, null);
                MethodInfo paint = type.GetMethod("Paint", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo hide = type.GetMethod("Hide", BindingFlags.Instance | BindingFlags.NonPublic);
                Transform root = Field<Transform>(slot, "root"); SpriteRenderer image = Field<SpriteRenderer>(slot, "image");
                SpriteMask mask = Field<SpriteMask>(slot, "mask"); SortingGroup group = Field<SortingGroup>(slot, "group");
                paint.Invoke(slot, new object[] { sprite, new Vector3(2, 3, 0), 2f, 70f, true, 3, "Drone-hover" });
                image.color = Color.magenta; image.sortingOrder = 999; image.flipX = image.flipY = true;
                image.transform.localRotation = Quaternion.Euler(0, 0, 53);
                group.sortingLayerID = SortingLayer.NameToID("Default");
                mask.isCustomRangeActive = true; mask.alphaCutoff = .95f;
                mask.transform.localRotation = Quaternion.Euler(0, 0, 31);
                hide.Invoke(slot, null);
                Check(!root.gameObject.activeSelf && !image.enabled && image.sprite == null && mask.sprite == null &&
                    !mask.gameObject.activeSelf, "효과 반환 시 Sprite/마스크 참조와 표시 제거");
                Check(image.color == Color.white && image.sortingOrder == 0 && !image.flipX && !image.flipY &&
                    image.maskInteraction == SpriteMaskInteraction.None, "효과 반환 시 색/order/뒤집기/마스크 상호작용 초기화");
                Check(root.localPosition == Vector3.zero && root.localScale == Vector3.one && root.localRotation == Quaternion.identity &&
                    image.transform.localPosition == Vector3.zero && image.transform.localScale == Vector3.one &&
                    image.transform.localRotation == Quaternion.identity && mask.transform.localRotation == Quaternion.identity,
                    "효과 반환 시 루트/이미지/마스크 변환 초기화");
                int objects = board.GetComponentsInChildren<Transform>(true).Length;
                for (int repeat = 0; repeat < 30; repeat++)
                {
                    paint.Invoke(slot, new object[] { sprite, Vector3.one, 1f, 0f, true, repeat % 4, "Drone-flight" });
                    Check(mask.sprite == sprite && !mask.isCustomRangeActive && Mathf.Approximately(mask.alphaCutoff, .5f) &&
                        image.maskInteraction == SpriteMaskInteraction.VisibleInsideMask && group.sortingOrder == 45,
                        "시트 효과 재사용 " + repeat + " 마스크와 프레임 복구");
                    paint.Invoke(slot, new object[] { sprite, Vector3.zero, 1f, 0f, false, 0, "Impact" });
                    Check(image.transform.localPosition == Vector3.zero && image.transform.localRotation == Quaternion.identity &&
                        image.maskInteraction == SpriteMaskInteraction.None && !mask.gameObject.activeSelf && group.sortingOrder == 40,
                        "단일 효과 재사용 " + repeat + " 이전 시트/회전/order 잔상0");
                    hide.Invoke(slot, null);
                }
                Check(board.GetComponentsInChildren<Transform>(true).Length == objects, "준비한 효과 슬롯 반복 추가 생성0");
                ElementVisualCatalog catalog = ElementVisualCatalog.FromDto(new ElementVisualCatalogDto {
                    definitions = new[] { new ElementVisualDefinitionDto { key = "effect.flight", states = new[] {
                        new ElementVisualFrameDto { path = "PowerBlocks/fixture", size = 1.2f, offsetX = .1f,
                            offsetY = -.2f, pivotX = .25f, pivotY = .75f, angle = 32, order = 47 } } } },
                    bindings = new[] { new ElementVisualBindingDto { id = "fixture.flight", visualKey = "effect.flight" } } });
                ElementVisualFrame visual = new ElementVisualResolver(catalog).Resolve(new ElementId("fixture.flight"),
                    new ElementVisualState(-1, 0, 0, 0, 0, 0, 1));
                MethodInfo paintVisual = type.GetMethod("PaintVisual", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(paintVisual != null, "이동 효과 슬롯은 등록 시각 프레임을 소비");
                paintVisual.Invoke(slot, new object[] { sprite, visual, new Vector3(2, 3, 0), .92f, 180f, "Rocket-flight" });
                Check(group.sortingOrder == 47 && image.sortingOrder == 0 && image.sprite == sprite &&
                    Mathf.Approximately(image.transform.localScale.x, 1.2f * .92f), "효과 슬롯의 등록 크기/정렬 적용");
                Check(Mathf.Abs(Mathf.DeltaAngle(root.localEulerAngles.z + image.transform.localEulerAngles.z, 212)) < .01f &&
                    image.transform.localPosition.sqrMagnitude > 0 && !mask.gameObject.activeSelf &&
                    image.maskInteraction == SpriteMaskInteraction.None, "이동 효과의 등록 회전/피벗/오프셋 적용·시트 잔상0");
                hide.Invoke(slot, null);
                Check(image.sprite == null && group.sortingOrder == 40 && image.transform.localPosition == Vector3.zero,
                    "등록 시각 효과 반환 후 정렬/피벗 잔류0");
                UnityEngine.Object.DestroyImmediate(board); board = null;
                Check(image == null && mask == null && root == null && group == null && sprite != null,
                    "보드 Destroy 시 슬롯/마스크 파기 및 외부 Sprite 소유권 보존");
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                if (board != null) UnityEngine.Object.DestroyImmediate(board);
                if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
                if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite);
            }
            Directory.CreateDirectory("Logs/ElementFramework/Phase04");
            File.WriteAllLines("Logs/ElementFramework/Phase04/effect-slot-results.txt", Results);
            EditorApplication.Exit(exit);
        }
    }
}
