using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.U2D;

namespace GameScreen.Editor
{
    public static partial class PuzzleLevelTransitionVerification
    {
        private static object Private(PuzzleGameSession session, string name)
            => typeof(PuzzleGameSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
        private static async UniTask ReadyOrResult(PuzzleGameSession session, bool result)
        {
            float deadline = Time.realtimeSinceStartup + 45;
            while (!(result ? session.ResultReady : session.CanAcceptInput) && !session.HasFailed && Time.realtimeSinceStartup < deadline) await UniTask.Yield();
            Check(result ? session.ResultReady : session.CanAcceptInput, "실제 Update 대기 " + (result ? "결과" : "입력"));
        }
        private static async UniTask WinAt(PuzzleGameSession session, int number)
        {
            LevelDefinition fixture = (LevelDefinition)typeof(Levels.Editor.PowerEffectVerification)
                .GetMethod("Make", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"levelNumber\":" + number + ",\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", fixture);
                foreach (BoardCoordinate at in new[] { new BoardCoordinate(4, 4), new BoardCoordinate(7, 7) })
                    typeof(Levels.Editor.PowerEffectVerification).GetMethod("Place", BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null, new object[] { fixture, at, at.Row == 4 ? InitialBlockKind.Rocket : InitialBlockKind.Drone, RocketDirection.Horizontal, RabbitColor.Type1 });
                Check(Levels.Editor.LevelSupplyEditing.AddTopSources(fixture) == null, "검사 판 실제 상단 공급구");
                typeof(PuzzleGameSession).GetField("initialBytes", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, LevelPackCodec.Encode(new[] { fixture }));
                typeof(PuzzleGameSession).GetField("levelNumber", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, number);
                await session.RestartAsync(CancellationToken.None); session.enabled = true;
                await ReadyOrResult(session, false);
                Check(session.TryActivate(new BoardCoordinate(4, 4)), "검사 판 실제 파워 발동 " + number);
                await ReadyOrResult(session, true);
                Check(session.Outcome.Kind == BoardOutcomeKind.Won && session.State.LevelNumber == number, "실제 승리 번호 " + number);
            }
            finally { UnityEngine.Object.Destroy(fixture); }
        }

        private static async UniTask BoundaryChecks(PuzzleGameSession session)
        {
            string path = "Assets/Stage13-owned-" + Guid.NewGuid().ToString("N") + ".bytes";
            IResourceLocator[] original = Addressables.ResourceLocators.ToArray();
            ResourceLocationMap map = new ResourceLocationMap("Stage13-boundary-map");
            FixtureLocator locator = new FixtureLocator(original, map);
            AssetDatabaseProvider provider = Addressables.ResourceManager.ResourceProviders.OfType<AssetDatabaseProvider>().First();
            float previousDelay = provider.GetLoadDelay();
            LevelDefinition level = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset"));
            MethodInfo background = typeof(PuzzleGameSession).GetMethod("OnApplicationPause", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                JsonUtility.FromJsonOverwrite("{\"levelNumber\":51}", level);
                BoardCoordinate anchor = new BoardCoordinate(6, 0);
                Levels.Editor.LevelObstacleEditing.Apply(level, new Levels.Editor.PlacementBrush { Layer = Levels.PlacementLayer.Block, Erase = true }, Levels.LevelPlacementRules.Footprint(anchor, 2));
                Levels.Editor.LevelObstacleEditing.Apply(level, new Levels.Editor.PlacementBrush { Layer = Levels.PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Safe, Durability = 6 }, new[] { anchor });
                byte[] valid = LevelPackCodec.Encode(new[] { level });
                File.WriteAllBytes(path, valid); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                map.Add(LevelPackCodec.Address(51), new ResourceLocationBase("Stage13-boundary", path, provider.ProviderId, typeof(TextAsset)));
                foreach (IResourceLocator old in original) Addressables.RemoveResourceLocator(old);
                Addressables.AddResourceLocator(locator);
                await WinAt(session, 50);
                object state = session.State, bytes = Private(session, "initialBytes"), outcome = session.Outcome;
                PuzzleArtwork art = (PuzzleArtwork)Private(session, "artwork");
                int seed = (int)Private(session, "seed"), atlasCount = art.AtlasCount;
                PropertyInfo cache = Addressables.ResourceManager.GetType().GetProperty("OperationCacheCount", BindingFlags.NonPublic | BindingFlags.Instance);
                int cacheBefore = (int)cache.GetValue(Addressables.ResourceManager);
                // 실제 로드가 진행 중일 때 취소한다. 로더가 핸들을 반환할 때까지 기다린다.
                provider.SetLoadDelay(.5f);
                using (CancellationTokenSource cancellation = new CancellationTokenSource())
                {
                    UniTask<bool> pending = session.AdvanceLevelAsync(cancellation.Token);
                    Check(session.IsChangingLevel, "실제 팩 로드 중 취소 지점");
                    cancellation.Cancel();
                    Check(!await pending && session.ResultReady && ReferenceEquals(session.State, state) && ReferenceEquals(bytes, Private(session, "initialBytes")), "로드 중 취소 승리·Retry 보존");
                }
                provider.SetLoadDelay(previousDelay);
                for (int iteration = 0; iteration < 3; iteration++)
                {
                    using CancellationTokenSource cancellation = new CancellationTokenSource();
                    UniTask<bool> pending = session.AdvanceLevelAsync(cancellation.Token);
                    background.Invoke(session, new object[] { true });
                    await UniTask.Delay(500, ignoreTimeScale: true);
                    Check(session.IsChangingLevel && ReferenceEquals(session.State, state), "후보 준비 후 background 전환 대기 " + iteration);
                    cancellation.Cancel();
                    Check(!await pending && !session.IsChangingLevel && ReferenceEquals(outcome, session.Outcome) && art.AtlasCount == atlasCount,
                        "후보 취소 반복 기존 아틀라스·승리 보존 " + iteration);
                    background.Invoke(session, new object[] { false });
                    await UniTask.Yield();
                    Check((int)cache.GetValue(Addressables.ResourceManager) <= cacheBefore, "후보 취소 반복 ResourceManager 캐시 잔류0 " + iteration);
                }
                File.WriteAllBytes(path, new byte[] { 1, 2, 3 }); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Check(!await session.AdvanceLevelAsync(CancellationToken.None) && ReferenceEquals(bytes, Private(session, "initialBytes")) && session.ResultReady,
                    "잘못된 실제 bytes 로드 오류·Retry 보존");
                File.WriteAllBytes(path, valid); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                string atlasAddress = BoardSpriteAtlas.AddressFor("Blocks/");
                map.Add(atlasAddress, new ResourceLocationBase("Stage13-missing-atlas", "Assets/Stage13-absent.spriteatlas", provider.ProviderId, typeof(SpriteAtlas)));
                Check(!await session.AdvanceLevelAsync(CancellationToken.None) && ReferenceEquals(state, session.State) && art.AtlasCount == atlasCount && session.ResultReady,
                    "실제 아틀라스 준비 실패 기존 승리·자원 보존");
                map.Locations.Remove(atlasAddress);
                Check(await session.AdvanceLevelAsync(CancellationToken.None) && session.State.LevelNumber == 51 && (int)Private(session, "seed") == seed,
                    "실제 Addressables 50→51 오류 후 재시도·동일 시드");
                await ReadyOrResult(session, false);
                MethodInfo snapshot = typeof(Levels.Editor.LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.Static | BindingFlags.NonPublic);
                string initial = (string)snapshot.Invoke(null, new object[] { session.State });
                await session.RestartAsync(CancellationToken.None);
                Check(session.State.LevelNumber == 51 && initial == (string)snapshot.Invoke(null, new object[] { session.State }), "51 레벨 Retry 전체 시작 상태");
                await WinAt(session, 50);
                art = (PuzzleArtwork)Private(session, "artwork");
                UniTask<bool> dying = session.AdvanceLevelAsync(CancellationToken.None);
                background.Invoke(session, new object[] { true });
                await UniTask.Delay(500, ignoreTimeScale: true);
                int changed = 0; session.Changed += () => changed++;
                // 원본 씬을 저장하지 않고 실제 Single 로드로 이전 씬과 후보 수명을 종료한다.
                await UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(PuzzleGameAssets.ScenePath,
                    new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
                Check(!await dying && changed == 0 && art.AtlasCount == 0 && session == null,
                    "후보 준비 중 실제 씬 종료·세션 파괴·늦은 화면 갱신0·기존 자원 반환");
                await ReadyOrResult(UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>(), false);
            }
            finally
            {
                provider.SetLoadDelay(previousDelay);
                if (session != null) background.Invoke(session, new object[] { false });
                Addressables.RemoveResourceLocator(locator);
                foreach (IResourceLocator old in original) Addressables.AddResourceLocator(old);
                AssetDatabase.DeleteAsset(path); UnityEngine.Object.Destroy(level);
                Check(!File.Exists(path) && !File.Exists(path + ".meta") && Addressables.ResourceLocators.SequenceEqual(original), "경계 검사 파일·locator·delay cleanup 원복");
            }
        }
    }
}
