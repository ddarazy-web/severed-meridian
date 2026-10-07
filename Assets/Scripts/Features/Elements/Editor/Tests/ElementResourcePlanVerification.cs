using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using GameScreen;
using Levels;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;

namespace Elements.Editor
{
    /// <summary>공급과 생성 관계를 따라 필요한 자원만 수집하고 조회가 실행 값을 바꾸지 않는지 검사한다.</summary>
    [InitializeOnLoad]
    public static class ElementResourcePlanVerification
    {
        private const string PlayKey = "ElementFramework.Phase04.ResourcePlan";
        private static bool lifetime;
        static ElementResourcePlanVerification()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PlayKey, false)) return;
                SessionState.SetBool(PlayKey, false);
                lifetime = SessionState.GetBool(PlayKey + ".Lifetime", false); SessionState.EraseBool(PlayKey + ".Lifetime");
                RunChecks();
            };
        }
        private static readonly List<string> Results = new List<string>();
        private static void Check(bool pass, string message)
        { if (!pass) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static ElementDefinition Body(string id, MissionKind mission) => new ElementDefinition(new ElementId(id), id,
            new ElementPlacementProfile(1, 2), null, new ElementDamageSourcePolicy(true, true, false, true), null,
            new ElementDamageAggregationPolicy(false), new ElementRemovalMissionProfile(mission), ElementReactionBehavior.Durability);
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("미저장 씬 보존");
            Directory.CreateDirectory("Logs/ElementFramework/Phase04");
            SessionState.SetBool(PlayKey, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        public static void RunLifetime()
        { SessionState.SetBool(PlayKey + ".Lifetime", true); Run(); }
        private static async void RunChecks()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            Results.Clear(); int exit = 0; LevelDefinition level = null;
            try
            {
                if (lifetime) { await VerifyPendingLifetime(); return; }
                Type planType = typeof(ElementId).Assembly.GetType("Elements.ElementResourcePlan");
                Check(planType != null, "초기판과 생성 관계를 분리된 자원 계획으로 계산 가능");
                ElementDefinition body = Body("fixture.graph.body", MissionKind.Scrap), generated = Body("fixture.graph.generated", MissionKind.Crate);
                ElementDefinition supplied = ElementDefinition.CreateSupply(new ElementId("fixture.graph.supply"), "공급",
                    ElementSupplyProfile.ForObstacle(body.Id, "fixture-"));
                ElementDefinition bomb = ElementDefinition.CreateSupply(new ElementId("fixture.graph.bomb"), "폭탄",
                    new ElementSupplyProfile(ElementSupplyBehavior.Power, RuntimeContent.Bomb));
                ElementDefinition choice = ElementDefinition.CreateSupply(new ElementId("fixture.graph.choice"), "선택",
                    ElementSupplyProfile.ForRandomPower(new[] { bomb.Id, new ElementId("power.rocket") }));
                ElementDefinition normal = ElementDefinition.CreateSupply(new ElementId("fixture.graph.normal"), "일반",
                    new ElementSupplyProfile(ElementSupplyBehavior.RandomNormal, RuntimeContent.Normal));
                ElementDefinition recovery = ElementDefinition.CreateSupply(new ElementId("fixture.graph.recovery"), "부품",
                    new ElementSupplyProfile(ElementSupplyBehavior.Recovery, RuntimeContent.Recovery));
                ElementCatalog rules = new ElementCatalog(LegacyElementDefinitions.DefaultCatalog.Definitions.Concat(new[] { body, generated, supplied, bomb, choice, normal, recovery }));
                ElementVisualCatalog visuals = LegacyElementVisuals.WithOverrides(new ElementVisualCatalogDto
                {
                    definitions = new[]
                    {
                        new ElementVisualDefinitionDto { key = "fixture.graph.body", generates = new[] { generated.Id.Value, generated.Id.Value },
                            states = new[] { new ElementVisualFrameDto { path = "Obstacles/RecoveryCapsule/recovery-capsule-durability-1-v1-256", effects = new[] { "Effects/MetalBreak/Animations/metal-break-frame-01-v1-256" }, effectAnimations = ElementVisualVerification.FixtureEffects("legacy.scrap") } } },
                        new ElementVisualDefinitionDto { key = "fixture.graph.generated", generates = new[] { body.Id.Value },
                            states = new[] {
                                new ElementVisualFrameDto { durability = 1, path = "Obstacles/Crate/crate-durability-1-v1-256", effectAnimations = ElementVisualVerification.FixtureEffects("legacy.crate") },
                                new ElementVisualFrameDto { durability = 2, path = "Obstacles/Crate/crate-durability-1-v1-256", effectAnimations = ElementVisualVerification.FixtureEffects("legacy.crate") },
                                new ElementVisualFrameDto { durability = 13,
                                    path = LegacyElementVisuals.Catalog.ToDto().definitions.Single(definition => definition.key == "legacy.generator").states[0].path } } }
                    },
                    bindings = new[]
                    {
                        new ElementVisualBindingDto { id = body.Id.Value, visualKey = "fixture.graph.body" },
                        new ElementVisualBindingDto { id = generated.Id.Value, visualKey = "fixture.graph.generated" },
                        new ElementVisualBindingDto { id = bomb.Id.Value, visualKey = "legacy.bomb" },
                        new ElementVisualBindingDto { id = normal.Id.Value, visualKey = "legacy.normal" },
                        new ElementVisualBindingDto { id = recovery.Id.Value, visualKey = "legacy.recovery" }
                    }
                });
                ElementLevelSupplyDefinition supply = new ElementLevelSupplyDefinition
                {
                    scrapDefinitionId = supplied.Id.Value, scrapTarget = 2, scrapLimit = 5, recoveryDefinitionId = recovery.Id.Value, recoveryTarget = 1,
                    sources = new List<ElementSupplySourceDefinition>
                    {
                        new ElementSupplySourceDefinition { coordinate = new BoardCoordinate(0, 0), mode = SupplyMode.Fixed, exhaustion = SupplyExhaustion.Random,
                            randomDefinitionId = normal.Id.Value, items = new List<ElementSupplyItemDefinition> {
                                new ElementSupplyItemDefinition { definitionId = choice.Id.Value, count = 2 } } },
                        new ElementSupplySourceDefinition { coordinate = new BoardCoordinate(0, 1), mode = SupplyMode.Random, randomDefinitionId = normal.Id.Value },
                        new ElementSupplySourceDefinition { coordinate = new BoardCoordinate(0, 2), mode = SupplyMode.MaintainScrap, randomDefinitionId = normal.Id.Value },
                        new ElementSupplySourceDefinition { coordinate = new BoardCoordinate(0, 3), mode = SupplyMode.MaintainRecovery, randomDefinitionId = normal.Id.Value }
                    }
                };
                level = ScriptableObject.CreateInstance<LevelDefinition>();
                JsonUtility.FromJsonOverwrite("{\"schemaVersion\":5,\"flow\":{\"arrivals\":[{\"row\":8,\"column\":4}]},\"missions\":[{\"kind\":0,\"count\":20},{\"kind\":3,\"count\":2},{\"kind\":9,\"count\":3}],\"elementSupply\":" + JsonUtility.ToJson(supply) + "}", level);
                JsonUtility.FromJsonOverwrite("{\"colors\":[0,1,2,3]}", level);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345, rules);
                Check(built.IsBuilt, "고정/랜덤/유지 공급 입력 구성 " + string.Join(";", built.Issues));
                LevelRuntimeState state = built.State; int draws = state.Random.DrawCount; string input = JsonUtility.ToJson(level);
                object plan = planType.GetMethod("Create").Invoke(null, new object[] { state, visuals });
                string[] addresses = ((IReadOnlyList<string>)planType.GetProperty("Addresses").GetValue(plan)).ToArray();
                string[] ids = ((IReadOnlyList<ElementId>)planType.GetProperty("Definitions").GetValue(plan)).Select(id => id.Value).ToArray();
                Check(ids.Contains(body.Id.Value) && ids.Contains(generated.Id.Value) && ids.Contains(bomb.Id.Value) &&
                    ids.Contains(normal.Id.Value) && ids.Contains(recovery.Id.Value), "초기판에 없는 고정/랜덤/유지/행동 생성 종류 포함");
                Check(ids.Count(id => id == body.Id.Value) == 1 && ids.Count(id => id == generated.Id.Value) == 1 &&
                    addresses.Distinct().Count() == addresses.Length, "중복과 순환 생성은 한 번씩 방문/주소 요청");
                foreach (string address in new[] { "MoonRabbitBoard-Obstacles-RecoveryCapsule", "MoonRabbitBoard-Obstacles-Crate", "MoonRabbitBoard-BoardDevices-Recovery", "MoonRabbitBoard-Effects-MetalBreak" })
                    Check(addresses.Contains(address), "필요 주소 포함 " + address);
                foreach (string address in new[] { "MoonRabbitBoard-Obstacles-Web", "MoonRabbitBoard-Obstacles-Mold", "MoonRabbitBoard-Obstacles-Generator", "MoonRabbitBoard-Effects-MoldClear", "MoonRabbitBoard-Effects-GeneratorCharge" })
                    Check(!addresses.Contains(address), "미사용 별도 주소 제외 " + address);
                Check(ids.Contains("power.rocket") && ids.Contains("power.drone") && ids.Contains("power.magnet") && addresses.Contains("MoonRabbitBoard-Effects-Match") &&
                    addresses.Contains("MoonRabbitBoard-Effects-PowerCreation"), "매칭 부스터 파워 조합의 생성과 효과 준비");
                Check(state.Random.DrawCount == draws && JsonUtility.ToJson(level) == input, "계획 조회는 원본과 난수 무변경");
                Check(state.Colors.Count == 4 && ((ElementResourcePlan)plan).Paths.Contains("Blocks/rabbit-purple-v1-256"),
                    "네 색 레벨의 종료 공급 다섯 번째 색 포함·도달 불가 Generator 주소 제외");
                // 고정 고철과 개수 유지는 기존 규칙에서 상호 배타적이므로 별도 입력에서 검증한다.
                supply.sources[0].items.Add(new ElementSupplyItemDefinition { definitionId = supplied.Id.Value, count = 2 });
                supply.sources[2].mode = SupplyMode.Random;
                supply.scrapTarget = 0; supply.scrapLimit = 0;
                JsonUtility.FromJsonOverwrite("{\"elementSupply\":" + JsonUtility.ToJson(supply) + "}", level);
                built = LevelStateBuilder.Build(level, 12345, rules);
                Check(built.IsBuilt, "고정 본체 공급 별도 입력 구성 " + string.Join(";", built.Issues));
                plan = planType.GetMethod("Create").Invoke(null, new object[] { built.State, visuals });
                ids = ((IReadOnlyList<ElementId>)planType.GetProperty("Definitions").GetValue(plan)).Select(id => id.Value).ToArray();
                Check(ids.Contains(supplied.Id.Value) && ids.Contains(body.Id.Value) && ids.Contains(generated.Id.Value), "고정 본체 공급에서 생성 참조까지 탐색");
                await VerifyNative(built.State, visuals, (ElementResourcePlan)plan);
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                if (level != null) UnityEngine.Object.DestroyImmediate(level);
                Directory.CreateDirectory("Logs/ElementFramework/Phase04");
                File.WriteAllLines("Logs/ElementFramework/Phase04/" + (lifetime ? "resource-pending-lifetime-results.txt" : "resource-plan-results.txt"), Results);
                EditorApplication.Exit(exit);
            }
        }

        private static async UniTask VerifyPendingLifetime()
        {
            BoardSpriteAtlas first = new BoardSpriteAtlas(BoardSpriteAtlas.Prefix + "Blocks");
            BoardSpriteAtlas sibling = new BoardSpriteAtlas(BoardSpriteAtlas.Prefix + "Blocks");
            BoardSpriteAtlas shared = null;
            try
            {
                Debug.Log("Native pending lifetime probe begins");
                UniTask pending = first.LoadAsync();
                AsyncOperationHandle<SpriteAtlas> handle = (AsyncOperationHandle<SpriteAtlas>)typeof(BoardSpriteAtlas)
                    .GetField("handle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(first);
                Check(handle.IsValid() && !handle.IsDone, "실제 Addressables 요청이 아직 완료되지 않은 native pending 확보");
                first.Dispose();
                Check(handle.IsValid(), "pending 소유자 Dispose는 native 로드 완료 전 핸들을 반환하지 않음");
                bool cancelled = false;
                try { await pending.Timeout(TimeSpan.FromSeconds(15)); } catch (ObjectDisposedException) { cancelled = true; }
                await UniTask.DelayFrame(2);
                Check(cancelled && !handle.IsValid(), "로드 완료 뒤 취소 소유자의 핸들 반환과 완료 사용 거절");
                shared = new BoardSpriteAtlas(BoardSpriteAtlas.Prefix + "Blocks");
                await shared.LoadAsync().Timeout(TimeSpan.FromSeconds(15));
                await sibling.LoadAsync().Timeout(TimeSpan.FromSeconds(15));
                Sprite owned = shared.Get("rabbit-pink-v1-256");
                Sprite other = sibling.Get("rabbit-pink-v1-256");
                Check(owned != null && other != null && !ReferenceEquals(owned, other) && owned.texture == other.texture, "공유 텍스처와 소유자별 독립 클론");
                shared.Dispose(); await UniTask.DelayFrame(2);
                Check(owned == null && other != null && sibling.IsLoaded, "동일 아틀라스의 독립 소유자는 다른 소유자 반환 후에도 유지");
                sibling.Dispose(); await UniTask.DelayFrame(2);
                Check(other == null && !sibling.IsLoaded, "마지막 소유자 Dispose 후 실제 클론 파기");
                BoardSpriteAtlas callbackOwner = new BoardSpriteAtlas(BoardSpriteAtlas.Prefix + "Obstacles-Web");
                int callbackCount = 0, disposedErrors = 0;
                void Observe(string condition, string stackTrace, LogType type)
                { if (type == LogType.Exception) disposedErrors++; }
                Application.logMessageReceived += Observe;
                try
                {
                    typeof(BoardSpriteAtlas).GetMethod("OnAtlasRequested", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(callbackOwner, new object[] { BoardSpriteAtlas.Prefix + "Obstacles-Web", (Action<SpriteAtlas>)(_ => callbackCount++) });
                    AsyncOperationHandle<SpriteAtlas> callbackHandle = (AsyncOperationHandle<SpriteAtlas>)typeof(BoardSpriteAtlas)
                        .GetField("handle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(callbackOwner);
                    Check(callbackHandle.IsValid() && !callbackHandle.IsDone, "아틀라스 요청 콜백의 실제 native 지연 로드 확보");
                    callbackOwner.Dispose();
                    await UniTask.WaitUntil(() => !callbackHandle.IsValid() || callbackHandle.IsDone).Timeout(TimeSpan.FromSeconds(15));
                    await UniTask.DelayFrame(3);
                    Check(callbackCount == 0 && !callbackHandle.IsValid(), "해제된 소유자의 지연 콜백 전달0·native 핸들 반환");
                    Check(disposedErrors == 0, "정상 해제된 지연 콜백의 종료 예외 로그0");
                    using BoardSpriteAtlas live = new BoardSpriteAtlas(BoardSpriteAtlas.Prefix + "Obstacles-Web");
                    SpriteAtlas delivered = null;
                    typeof(BoardSpriteAtlas).GetMethod("OnAtlasRequested", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(live, new object[] { BoardSpriteAtlas.Prefix + "Obstacles-Web", (Action<SpriteAtlas>)(atlas => { delivered = atlas; callbackCount++; }) });
                    await UniTask.WaitUntil(() => callbackCount > 0).Timeout(TimeSpan.FromSeconds(15));
                    Sprite liveSprite = live.Get("web-durability-1-v2-256");
                    Check(callbackCount == 1 && delivered != null && liveSprite != null && live.IsLoaded, "유효 소유자의 실제 native 완료 콜백1회와 Sprite 조회 유지");
                    live.Dispose(); await UniTask.DelayFrame(2);
                    Check(liveSprite == null && callbackCount == 1 && disposedErrors == 0, "유효 콜백 후 반환의 클론 잔류0·추가 콜백0·예외0");
                }
                finally { Application.logMessageReceived -= Observe; callbackOwner.Dispose(); }
                await VerifyArtworkCandidate();
            }
            finally { first.Dispose(); sibling.Dispose(); shared?.Dispose(); }
        }

        private static async UniTask VerifyArtworkCandidate()
        {
            LevelDefinition currentLevel = ScriptableObject.CreateInstance<LevelDefinition>();
            LevelDefinition nextLevel = ScriptableObject.CreateInstance<LevelDefinition>();
            PuzzleArtwork current = new PuzzleArtwork(), candidate = new PuzzleArtwork(), replacement = new PuzzleArtwork();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", currentLevel);
                JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":1,\"count\":1}],\"obstacles\":[{\"id\":\"candidate\",\"coordinate\":{\"row\":4,\"column\":4},\"kind\":0,\"durability\":2}]}", nextLevel);
                LevelStateBuildResult currentBuilt = LevelStateBuilder.Build(currentLevel, 12345), nextBuilt = LevelStateBuilder.Build(nextLevel, 12345);
                Check(currentBuilt.IsBuilt && nextBuilt.IsBuilt, "현재/다음 아트 후보의 실제 레벨 입력 유효");
                await current.PrepareAsync(currentBuilt.State, CancellationToken.None).Timeout(TimeSpan.FromSeconds(20));
                Sprite previous = current.Get("Blocks/rabbit-pink-v1-256");
                Dictionary<string, BoardSpriteAtlas> CurrentAtlases(PuzzleArtwork owner) => (Dictionary<string, BoardSpriteAtlas>)typeof(PuzzleArtwork)
                    .GetField("atlases", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
                AsyncOperationHandle<SpriteAtlas> Handle(BoardSpriteAtlas atlas) => (AsyncOperationHandle<SpriteAtlas>)typeof(BoardSpriteAtlas)
                    .GetField("handle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(atlas);
                using CancellationTokenSource cancellation = new CancellationTokenSource();
                UniTask preparation = candidate.PrepareAsync(nextBuilt.State, cancellation.Token);
                KeyValuePair<string, BoardSpriteAtlas> loading = CurrentAtlases(candidate).First(pair => !Handle(pair.Value).IsDone);
                AsyncOperationHandle<SpriteAtlas> pendingHandle = Handle(loading.Value);
                Check(!CurrentAtlases(current).ContainsKey(loading.Key) && pendingHandle.IsValid() && !pendingHandle.IsDone,
                    "기존 소유자에 없는 후보 자원의 실제 native pending 확보");
                cancellation.Cancel(); candidate.Dispose();
                Check(candidate.AtlasCount > 0 && pendingHandle.IsValid(), "후보 취소/Dispose도 진행 중 native 요청의 완료까지 소유");
                bool cancelled = false;
                try { await preparation.Timeout(TimeSpan.FromSeconds(20)); } catch (OperationCanceledException) { cancelled = true; }
                await UniTask.DelayFrame(2);
                Check(cancelled && candidate.AtlasCount == 0 && !pendingHandle.IsValid(), "후보 native 완료 뒤 취소/보유 핸들 전량 반환");
                Check(previous != null && current.Get("Blocks/rabbit-pink-v1-256") == previous, "실패 후보 반환은 이전 소유자의 Sprite/핸들을 보존");
                ElementVisualCatalogDto missing = LegacyElementVisuals.Catalog.ToDto();
                foreach (ElementVisualFrameDto frame in missing.definitions.Single(definition => definition.key == "legacy.crate").states)
                    frame.effectAnimations = Array.Empty<ElementVisualEffectDto>();
                using PuzzleArtwork invalid = new PuzzleArtwork(ElementVisualCatalog.FromDto(missing));
                bool rejected = false;
                try { await invalid.PrepareAsync(nextBuilt.State, CancellationToken.None); }
                catch (ArgumentException error) { rejected = error.Message.Contains("obstacle.crate.wood") && error.Message.Contains("damage"); }
                Check(rejected && invalid.AtlasCount == 0 && previous != null, "필수 효과 누락 후보는 native 요청 전에 ID 오류·이전 소유자 유지");
                await replacement.PrepareAsync(nextBuilt.State, CancellationToken.None).Timeout(TimeSpan.FromSeconds(20));
                Sprite next = replacement.Get("Blocks/rabbit-pink-v1-256");
                Check(next != null && !ReferenceEquals(previous, next) && previous.texture == next.texture, "재준비된 후보는 텍스처 공유·클론 독립");
                current.Dispose(); await UniTask.DelayFrame(2);
                Check(previous == null && next != null && replacement.Get("Blocks/rabbit-pink-v1-256") == next, "후보 준비 성공 후 이전 소유자 반환·새 소유자 유지");
                int capacity = replacement.AtlasCount;
                for (int repeat = 0; repeat < 3; repeat++) await replacement.PrepareAsync(nextBuilt.State, CancellationToken.None);
                Check(replacement.AtlasCount == capacity && replacement.Get("Blocks/rabbit-pink-v1-256") == next, "같은 아트 재준비의 주소/클론 추가0");
                replacement.Dispose(); await UniTask.DelayFrame(2);
                Check(next == null && replacement.AtlasCount == 0, "아트 최종 Dispose의 클론/주소 잔류0");
            }
            finally
            {
                current.Dispose(); candidate.Dispose(); replacement.Dispose();
                UnityEngine.Object.DestroyImmediate(currentLevel); UnityEngine.Object.DestroyImmediate(nextLevel);
            }
        }

        private static async Cysharp.Threading.Tasks.UniTask VerifyNative(LevelRuntimeState state, ElementVisualCatalog visuals, ElementResourcePlan plan)
        {
            PuzzleArtwork art = new PuzzleArtwork(visuals);
            try
            {
                // 실제 게임과 동일한 Play Mode 설정으로 요청하며 원본 에셋을 다시 패킹하지 않는다.
                string atlasPath = "Assets/Textures/Atlases/MoonRabbitBoard-PowerBlocks.spriteatlasv2";
                byte[] atlasBytes = File.ReadAllBytes(atlasPath), metaBytes = File.ReadAllBytes(atlasPath + ".meta");
                Check(atlasBytes.SequenceEqual(File.ReadAllBytes(atlasPath)) && metaBytes.SequenceEqual(File.ReadAllBytes(atlasPath + ".meta")), "Play Mode 진입 후 파워 아틀라스 에셋/메타 보존");
                SpriteAtlas direct = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                Sprite directRotor = direct.GetSprite("collection-drone-rotor-4frames-v1");
                string directResult = "direct atlas=" + direct.GetInstanceID() + " count=" + direct.spriteCount + " rotor=" + (directRotor != null);
                if (directRotor != null) UnityEngine.Object.DestroyImmediate(directRotor);
                await art.PrepareAsync(state, CancellationToken.None);
                Dictionary<string, BoardSpriteAtlas> atlases = (Dictionary<string, BoardSpriteAtlas>)typeof(PuzzleArtwork).GetField("atlases", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(art);
                Check(atlases.Keys.OrderBy(value => value).SequenceEqual(plan.Addresses.OrderBy(value => value)), "실제 native 요청 집합: 필요한 주소 누락0·미사용 별도 종류 요청0");
                AsyncOperationHandle<SpriteAtlas>[] handles = atlases.Values.Select(atlas =>
                    (AsyncOperationHandle<SpriteAtlas>)typeof(BoardSpriteAtlas).GetField("handle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(atlas)).ToArray();
                Check(handles.All(handle => handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded), "계획 주소 모두 실제 Addressables 핸들 로드 성공");
                List<string> inventory = new List<string>();
                inventory.Add(directResult);
                string sourcePath = "Assets/Textures/PowerBlocks/collection-drone-rotor-4frames-v1.png";
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
                inventory.Add("source textureType=" + importer.textureType + " spriteMode=" + importer.spriteImportMode +
                    " sprites=" + string.Join(",", AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<Sprite>().Select(sprite => sprite.name)));
                foreach (AsyncOperationHandle<SpriteAtlas> handle in handles)
                {
                    inventory.Add("loaded atlas=" + handle.Result.GetInstanceID() + " path=" + AssetDatabase.GetAssetPath(handle.Result) + " count=" + handle.Result.spriteCount);
                    Sprite[] sprites = new Sprite[handle.Result.spriteCount];
                    handle.Result.GetSprites(sprites);
                    foreach (Sprite sprite in sprites)
                        if (sprite != null) { inventory.Add(handle.DebugName + " " + sprite.name); UnityEngine.Object.DestroyImmediate(sprite); }
                }
                File.WriteAllLines("Logs/ElementFramework/Phase04/resource-plan-native-inventory.txt", inventory);
                foreach (string path in plan.Paths) Check(art.Get(path) != null, "native 참조 Sprite 준비 " + path);
                int count = art.AtlasCount;
                await art.PrepareAsync(state, CancellationToken.None);
                Check(art.AtlasCount == count, "계획 재준비 중 별도 주소 추가0");
                File.WriteAllLines("Logs/ElementFramework/Phase04/resource-plan-native-addresses.txt", atlases.Keys.OrderBy(value => value));
                art.Dispose();
                await Cysharp.Threading.Tasks.UniTask.DelayFrame(3);
                Check(art.AtlasCount == 0 && handles.All(handle => !handle.IsValid()), "계획 native 핸들 Dispose 반환");
            }
            finally
            {
                List<string> locations = new List<string>();
                foreach (UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator locator in Addressables.ResourceLocators)
                    if (locator.Locate("MoonRabbitBoard-PowerBlocks", typeof(SpriteAtlas), out IList<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation> selectedLocations))
                        foreach (UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation location in selectedLocations)
                        {
                            locations.Add(locator.LocatorId + " provider=" + location.ProviderId + " internal=" + location.InternalId);
                            foreach (UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation dependency in location.Dependencies)
                                locations.Add("dependency provider=" + dependency.ProviderId + " internal=" + dependency.InternalId);
                        }
                File.WriteAllLines("Logs/ElementFramework/Phase04/resource-plan-native-locations.txt", locations);
                art.Dispose();
            }
        }
    }
}
