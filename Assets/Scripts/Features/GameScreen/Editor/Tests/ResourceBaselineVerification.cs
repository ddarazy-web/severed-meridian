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
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.U2D;

namespace GameScreen.Editor
{
    public static class ResourceBaselineVerification
    {
        private const string Evidence = "Logs/ElementFramework/Stage10";
        private static readonly List<string> Results = new List<string>();
        private static readonly List<string> Observations = new List<string>();
        private static readonly List<LevelDefinition> Owned = new List<LevelDefinition>();
        private static object Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        private static Dictionary<string, BoardSpriteAtlas> Atlases(PuzzleArtwork art) => (Dictionary<string, BoardSpriteAtlas>)Field(art, "atlases");
        private static AsyncOperationHandle<SpriteAtlas> Handle(BoardSpriteAtlas atlas) => (AsyncOperationHandle<SpriteAtlas>)Field(atlas, "handle");
        private static UniTask Effects(PuzzleArtwork art, IEnumerable<string> paths, CancellationToken token)
            => (UniTask)typeof(PuzzleArtwork).GetMethod("PrepareEffectsAsync", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(art, new object[] { paths, token });
        private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException(name); Results.Add("PASS " + name); }
        private static LevelDefinition New()
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>(); Owned.Add(level);
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", level); return level;
        }
        private static LevelRuntimeState Build(LevelDefinition level)
        {
            LevelStateBuildResult result = LevelStateBuilder.Build(level, 12345);
            if (!result.IsBuilt) throw new InvalidOperationException(string.Join(" | ", result.Issues)); return result.State;
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 Editor에서만 실행한다.");
            RunAsync().Forget(error => { Debug.LogException(error); EditorApplication.Exit(1); });
        }

        private static async UniTask RunAsync()
        {
            Directory.CreateDirectory(Evidence); Results.Clear(); Observations.Clear(); int exit = 0;
            try
            {
                LevelDefinition basic = New();
                await Prepare("basic", basic, new[] { "Blocks/", "PowerBlocks/", PuzzleArtworkPaths.Floor });
                foreach (bool maintain in new[] { false, true })
                {
                    LevelDefinition supply = New();
                    JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)MissionKind.Recovery + ",\"count\":1}]}", supply);
                    Levels.Editor.LevelFlowEditing.SetArrival(supply, new BoardCoordinate(8, 0), false);
                    Levels.Editor.LevelSupplyEditing.PlaceSources(supply, new[] { new BoardCoordinate(0, 0), new BoardCoordinate(0, 2) });
                    if (maintain)
                    {
                        Check(Levels.Editor.LevelSupplyEditing.SetSourceProperty(supply, new[] { 0 }, "mode", (int)SupplyMode.MaintainScrap) == null, "고철 유지 fixture");
                        Check(Levels.Editor.LevelSupplyEditing.SetSourceProperty(supply, new[] { 1 }, "mode", (int)SupplyMode.MaintainRecovery) == null, "회수 유지 fixture");
                    }
                    else
                    {
                        for (int n = 0; n < 2; n++)
                        {
                            Check(Levels.Editor.LevelSupplyEditing.SetSourceProperty(supply, new[] { n }, "mode", (int)SupplyMode.Fixed) == null, "고정 fixture " + n);
                            Check(Levels.Editor.LevelSupplyEditing.SetItems(supply, n, new[] { new SupplyItem(n == 0 ? SupplyKind.Scrap : SupplyKind.Recovery) }) == null, "고정 목록 fixture " + n);
                        }
                    }
                    await Prepare("supply-" + maintain, supply, new[] { "Blocks/", "PowerBlocks/", PuzzleArtworkPaths.Floor, "Obstacles/Scrap/", PuzzleArtworkPaths.Recovery });
                }
                LevelDefinition layered = (LevelDefinition)typeof(Levels.Editor.GeneratorVerification).GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { ObstacleKind.Crate, 3 }); Owned.Add(layered);
                JsonUtility.FromJsonOverwrite("{\"covers\":[{\"coordinate\":{\"row\":2,\"column\":2},\"kind\":0,\"durability\":2}],\"dust\":[{\"coordinate\":{\"row\":2,\"column\":3},\"durability\":2}]}", layered);
                Levels.Editor.LevelFlowEditing.SetWalls(layered, new[] { new BoardEdge(new BoardCoordinate(0, 0), new BoardCoordinate(1, 0)) }, false);
                Levels.Editor.LevelFlowEditing.SetPortal(layered, new BoardCoordinate(1, 1), new BoardCoordinate(2, 1));
                await Prepare("layers-devices", layered, new[] { "Blocks/", "PowerBlocks/", PuzzleArtworkPaths.Floor, "Obstacles/Generator/", "Obstacles/Crate/", "Obstacles/Web/", "Obstacles/Dust/", "BoardTerrain/Walls/", "BoardDevices/Portals/", "BoardDevices/Wiring/" });
                LevelDefinition powers = New();
                JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[{\"coordinate\":{\"row\":2,\"column\":1},\"kind\":2},{\"coordinate\":{\"row\":2,\"column\":2},\"kind\":3},{\"coordinate\":{\"row\":2,\"column\":3},\"kind\":4},{\"coordinate\":{\"row\":2,\"column\":4},\"kind\":5}]}", powers);
                await Prepare("initial-powers", powers, new[] { "Blocks/", "PowerBlocks/", PuzzleArtworkPaths.Floor });
                ObstacleKind[] kinds = { ObstacleKind.Crate, ObstacleKind.Scrap, ObstacleKind.Safe, ObstacleKind.ColorLock, ObstacleKind.Appliance };
                MissionKind[] missions = { MissionKind.Crate, MissionKind.Scrap, MissionKind.Safe, MissionKind.ColorLock, MissionKind.Appliance };
                string[] categories = { "Crate", "Scrap", "RecoveryCapsule", "ColorLock", "MetalRodBox" };
                for (int n = 0; n < kinds.Length; n++)
                {
                    LevelDefinition level = New();
                    typeof(Levels.Editor.FixedObstacleVerification).GetMethod("Obstacle", BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null, new object[] { level, kinds[n], 1, new BoardCoordinate(3, 3), RabbitColor.Type1 });
                    JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":" + (int)missions[n] + ",\"count\":1}]}", level);
                    await Prepare("body-mission-" + kinds[n], level, new[] { "Blocks/", "PowerBlocks/", PuzzleArtworkPaths.Floor, "Obstacles/" + categories[n] + "/" });
                }
                LevelDefinition layers = New();
                JsonUtility.FromJsonOverwrite("{\"initialBlocks\":[{\"coordinate\":{\"row\":3,\"column\":3},\"kind\":0},{\"coordinate\":{\"row\":5,\"column\":5},\"kind\":0}]}", layers);
                JsonUtility.FromJsonOverwrite("{\"covers\":[{\"coordinate\":{\"row\":3,\"column\":3},\"kind\":0,\"durability\":1},{\"coordinate\":{\"row\":5,\"column\":5},\"kind\":1,\"durability\":1}],\"dust\":[{\"coordinate\":{\"row\":4,\"column\":4},\"durability\":1}],\"missions\":[{\"kind\":2,\"count\":1},{\"kind\":4,\"count\":1},{\"kind\":8,\"count\":1}]}", layers);
                await Prepare("layer-missions", layers, new[] { "Blocks/", "PowerBlocks/", PuzzleArtworkPaths.Floor, "Obstacles/Web/", "Obstacles/Dust/", "Obstacles/Mold/" });
                await EffectInventory();
                await Sharing(Build(basic)); await Cancellation(Build(basic)); await DuringLoad(false); await DuringLoad(true); await Failure();
            }
            catch (Exception error) { Results.Add("FAIL " + error); Debug.LogException(error); exit = 1; }
            finally
            {
                foreach (LevelDefinition level in Owned) if (level != null) UnityEngine.Object.DestroyImmediate(level);
                File.WriteAllLines(Evidence + "/baseline-results.txt", Results);
                File.WriteAllLines(Evidence + "/resource-observations.jsonl", Observations);
            }
            EditorApplication.Exit(exit);
        }

        private static async UniTask Prepare(string name, LevelDefinition level, string[] paths)
        {
            PuzzleArtwork art = new PuzzleArtwork();
            try
            {
                bool missing = false; try { art.Get(PuzzleArtworkPaths.Power(InitialBlockKind.Bomb, default)); } catch (InvalidOperationException) { missing = true; }
                Check(missing && art.Get(null) == null, name + " 준비 전 조회 거절/null");
                LevelRuntimeState state = Build(level); string before = JsonUtility.ToJson(level);
                await art.PrepareAsync(state, CancellationToken.None);
                string[] expected = paths.Select(BoardSpriteAtlas.AddressFor).Distinct().OrderBy(s => s).ToArray();
                Check(Atlases(art).Keys.OrderBy(s => s).SequenceEqual(expected), name + " 실제 준비 주소/불필요 종류 제외");
                BoardSpriteAtlas[] references = Atlases(art).Values.ToArray();
                Check(references.All(a => a.IsLoaded && Handle(a).IsValid()), name + " 실제 로드 핸들");
                Sprite first = art.Get(PuzzleArtworkPaths.Power(InitialBlockKind.Bomb, default));
                Check(ReferenceEquals(first, art.Get(PuzzleArtworkPaths.Power(InitialBlockKind.Bomb, default))), name + " Sprite 캐시 재사용");
                await art.PrepareAsync(state, CancellationToken.None);
                Check(references.SequenceEqual(Atlases(art).Values) && before == JsonUtility.ToJson(level), name + " 중복 준비 캐시/입력 보존");
                Record(name, level, art, "prepare twice", references);
                await Effects(art, new[] { "Effects/Match/Animations/match-pink-frame-01-v1-256", "Effects/Match/Animations/match-pink-frame-01-v1-256", null }, CancellationToken.None);
                Check(art.AtlasCount == expected.Length + 1, name + " 효과 주소 중복 제거/추가1");
                references = Atlases(art).Values.ToArray(); Record(name + "-effects", level, art, "duplicate effect paths", references);
                AsyncOperationHandle<SpriteAtlas>[] handles = references.Select(Handle).ToArray();
                art.Dispose();
                Check(art.AtlasCount == 0 && references.All(a => !a.IsLoaded) && handles.All(h => !h.IsValid()) && first == null, name + " 실제 Dispose 핸들/클론 해제");
                bool disposed = false; try { art.Get(PuzzleArtworkPaths.Floor); } catch (ObjectDisposedException) { disposed = true; }
                Check(disposed && art.Get(null) == null, name + " Dispose 조회 거절/null");
                disposed = false; try { await art.PrepareAsync(state, CancellationToken.None); } catch (ObjectDisposedException) { disposed = true; }
                Check(disposed, name + " Dispose 준비 거절");
                Record(name + "-released", level, art, "dispose", references);
            }
            finally { art.Dispose(); }
        }

        private static async UniTask EffectInventory()
        {
            string[] paths = Directory.GetFiles("Assets/Textures/Effects", "*.png", SearchOption.AllDirectories)
                .Select(path => path.Replace(Path.DirectorySeparatorChar, '/').Substring("Assets/Textures/".Length).Replace(".png", "")).ToArray();
            PuzzleArtwork art = new PuzzleArtwork();
            try
            {
                await Effects(art, paths.Concat(paths).Concat(new string[] { null }), CancellationToken.None);
                Check(Atlases(art).Keys.OrderBy(s => s).SequenceEqual(paths.Select(BoardSpriteAtlas.AddressFor).Distinct().OrderBy(s => s)), "전체 효과 프레임 주소 중복 제거");
                foreach (string path in paths) Check(art.Get(path) != null, "실제 효과 Sprite " + path);
                BoardSpriteAtlas[] atlases = Atlases(art).Values.ToArray();
                Record("effect-inventory", null, art, string.Join(" | ", paths), atlases);
                art.Dispose(); Check(atlases.All(a => !Handle(a).IsValid()), "전체 효과 핸들 해제");
            }
            finally { art.Dispose(); }
        }
        private static async UniTask Sharing(LevelRuntimeState state)
        {
            PuzzleArtwork previous = new PuzzleArtwork(), candidate = new PuzzleArtwork();
            try
            {
                await previous.PrepareAsync(state, CancellationToken.None);
                await candidate.PrepareAsync(state, CancellationToken.None);
                BoardSpriteAtlas[] originals = Atlases(previous).Values.ToArray(), replacements = Atlases(candidate).Values.ToArray();
                Check(originals.Zip(replacements, (a, b) => Handle(a).Equals(Handle(b))).All(value => value), "두 소유자 동일 Addressables 핸들 공유");
                string path = PuzzleArtworkPaths.Power(InitialBlockKind.Bomb, default);
                Sprite oldSprite = previous.Get(path), newSprite = candidate.Get(path);
                Check(!ReferenceEquals(oldSprite, newSprite), "두 소유자 Sprite 클론 독립");
                previous.Dispose();
                Check(oldSprite == null && newSprite != null && replacements.All(a => a.IsLoaded) && candidate.Get(path) == newSprite, "이전 소유자 해제 후 후보 핸들/클론 유지");
                Record("two-owners-previous-released", null, candidate, "both native prepares; dispose previous only", replacements);
                candidate.Dispose();
                Record("two-owners-final-immediate", null, candidate, "last owner released; native deferred callbacks pending", replacements);
                UniTaskCompletionSource tick = new UniTaskCompletionSource(); int remaining = 3;
                EditorApplication.CallbackFunction update = null;
                update = () => { if (--remaining > 0) return; EditorApplication.update -= update; tick.TrySetResult(); };
                EditorApplication.update += update; await tick.Task;
                Check(newSprite == null && replacements.All(a => !Handle(a).IsValid()), "마지막 소유자 해제 후 공유 핸들 반환");
                Record("two-owners-final-released", null, candidate, "dispose last owner", replacements);
            }
            finally { previous.Dispose(); candidate.Dispose(); }
        }
        private static async UniTask Cancellation(LevelRuntimeState state)
        {
            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                source.Cancel(); PuzzleArtwork art = new PuzzleArtwork();
                try
                {
                    bool canceled = false; try { await art.PrepareAsync(state, source.Token); } catch (OperationCanceledException) { canceled = true; }
                    Check(canceled && art.AtlasCount == 0 && (int)Field(art, "pending") == 0, "로드 전 취소 요청0/보류0");
                    Record("pre-cancel", null, art, "canceled token before prepare", Array.Empty<BoardSpriteAtlas>());
                }
                finally { art.Dispose(); }
            }
        }

        private static async UniTask DuringLoad(bool cancel)
        {
            PuzzleArtwork art = new PuzzleArtwork(); BoardSpriteAtlas observed = null; bool fired = false; int retained = -1;
            Func<IResourceLocation, string> previous = Addressables.InternalIdTransformFunc;
            using (CancellationTokenSource source = new CancellationTokenSource())
            {
                Addressables.InternalIdTransformFunc = location =>
                {
                    if (!fired && (int)Field(art, "pending") > 0 && art.AtlasCount > 0)
                    {
                        fired = true; observed = Atlases(art).Values.First();
                        if (cancel) source.Cancel(); else art.Dispose();
                        retained = art.AtlasCount;
                        Record("during-load-hook-" + cancel, null, art, "native InternalId transform; unchanged ID; cancel or Dispose", new[] { observed });
                    }
                    return previous == null ? location.InternalId : previous(location);
                };
                try
                {
                    bool rejected = false;
                    try { await Effects(art, new[] { "Effects/Match/Animations/match-pink-frame-01-v1-256" }, source.Token); }
                    catch (OperationCanceledException) { rejected = true; }
                    catch (ObjectDisposedException) { rejected = true; }
                    Check(fired && retained == 1 && rejected && (int)Field(art, "pending") == 0, "실제 보류 로드 훅/중단/보류 정리 " + cancel);
                    if (cancel) Check(art.AtlasCount == 1 && observed.IsLoaded, "로드 중 취소는 완료 핸들 소유 유지");
                    else Check(art.AtlasCount == 0 && !Handle(observed).IsValid(), "보류 Dispose 완료 뒤 핸들 반환");
                    art.Dispose(); Check(art.AtlasCount == 0 && !Handle(observed).IsValid(), "중단 후 최종 해제 " + cancel);
                    Record("during-load-released-" + cancel, null, art, "native load completed; final Dispose", new[] { observed });
                }
                finally { Addressables.InternalIdTransformFunc = previous; art.Dispose(); }
            }
        }

        private static async UniTask Failure()
        {
            PuzzleArtwork art = new PuzzleArtwork();
            try
            {
                Exception failure = null;
                try { await Effects(art, new[] { "Effects/__EF10Missing__/missing" }, CancellationToken.None); } catch (Exception error) { failure = error; }
                Check(failure != null && art.AtlasCount == 1 && (int)Field(art, "pending") == 0, "누락 주소 실제 실패/보류 정리");
                BoardSpriteAtlas atlas = Atlases(art).Values.Single(); Record("missing-address", null, art, failure.GetType().Name + ": " + failure.Message, new[] { atlas });
                art.Dispose(); Record("missing-address-dispose-immediate", null, art, "owner released; deferred completion may retain handle", new[] { atlas });
                UniTaskCompletionSource tick = new UniTaskCompletionSource(); int remaining = 3;
                EditorApplication.CallbackFunction update = null;
                update = () => { if (--remaining > 0) return; EditorApplication.update -= update; tick.TrySetResult(); };
                EditorApplication.update += update; await tick.Task;
                Check(art.AtlasCount == 0 && !Handle(atlas).IsValid(), "실패 핸들 지연 콜백 후 Dispose 해제");
                Record("missing-address-released", null, art, "three native Editor updates after Dispose", new[] { atlas });
            }
            finally { art.Dispose(); }
        }

        [Serializable] private sealed class Observation
        {
            public string name, input, levelJson;
            public string[] addresses, providers;
            public int seed = 12345;
            public int atlasCount, pending;
            public bool[] loaded, handleValid;
        }
        private static void Record(string name, LevelDefinition level, PuzzleArtwork art, string input, BoardSpriteAtlas[] handles)
            => Observations.Add(JsonUtility.ToJson(new Observation { name = name, input = input, levelJson = level == null ? null : JsonUtility.ToJson(level),
                providers = Atlases(art).Keys.SelectMany(address => Addressables.ResourceLocators.SelectMany(locator => locator.Locate(address, typeof(SpriteAtlas), out IList<IResourceLocation> locations) ? locations.Select(location => location.ProviderId + ": " + location.InternalId) : Array.Empty<string>())).Distinct().ToArray(),
                addresses = Atlases(art).Keys.OrderBy(s => s).ToArray(), atlasCount = art.AtlasCount, pending = (int)Field(art, "pending"),
                loaded = handles.Select(a => a.IsLoaded).ToArray(), handleValid = handles.Select(a => Handle(a).IsValid()).ToArray() }));
    }
}
