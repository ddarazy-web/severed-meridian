using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elements;
using GameScreen;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LevelAuthoring.Editor
{
    public static partial class LobbyUiVerification
    {
        private static bool gameMeasurements;
        private static string measurementOutput;
        public static void RunBoundaryMeasurements()
        {
            verifyDataFailure = true;
            RunGameMeasurements();
        }

        private static void MemoryBoundary(string label)
        {
            if (!gameMeasurements) return;
            var value = new { label, nativeBytes = Profiler.GetTotalAllocatedMemoryLong(), managedBytes = Profiler.GetMonoUsedSizeLong(),
                sessions = UnityEngine.Object.FindObjectsByType<PuzzleGameSession>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                operations = UnityEngine.AddressableAssets.Addressables.ResourceManager.GetType().GetProperty("OperationCacheCount", Private).GetValue(UnityEngine.AddressableAssets.Addressables.ResourceManager) };
            File.AppendAllText(Path.Combine(measurementOutput, "boundaries.jsonl"), Newtonsoft.Json.JsonConvert.SerializeObject(value) + "\n");
        }

        private static async Cysharp.Threading.Tasks.UniTask MeasureSessionBoundaries(PuzzleGameSession game)
        {
            await WaitForStableGame(game);
            MemoryBoundary("restart-before");
            var oldArtwork = Field<PuzzleArtwork>(game, "artwork");
            await game.RestartAsync(System.Threading.CancellationToken.None);
            await WaitForStableGame(game);
            Check(oldArtwork.AtlasCount == 0 && game.State.LevelNumber == 1, "Restart retained artwork or changed level");
            MemoryBoundary("restart-after");
            // 승리 재현은 메모리 상태만 주입한다. 플레이 기록·정식 레벨은 변경하지 않는다.
            var executor = Field<Simulation.BoardActionExecutor>(game, "executor");
            var outcome = Activator.CreateInstance(typeof(Simulation.BoardOutcome), Private, null,
                new object[] { Simulation.BoardOutcomeKind.Won, "measurement", game.State, 0 }, null);
            typeof(Simulation.BoardActionExecutor).GetProperty("Outcome").SetValue(executor, outcome);
            typeof(Simulation.BoardActionExecutor).GetProperty("Phase").SetValue(executor, Simulation.BoardActionPhase.Stopped);
            Check(game.CanAdvanceLevel, "Next fixture not ready");
            MemoryBoundary("next-before");
            oldArtwork = Field<PuzzleArtwork>(game, "artwork");
            Check(!await game.AdvanceLevelAsync(new System.Threading.CancellationToken(true)), "Cancelled next succeeded");
            Check(game.State.LevelNumber == 1 && oldArtwork.AtlasCount > 0, "Cancelled next replaced current level");
            MemoryBoundary("next-cancelled");
            Check(await game.AdvanceLevelAsync(System.Threading.CancellationToken.None), "Next registered level failed");
            await WaitForStableGame(game);
            Check(game.State.LevelNumber == 2 && oldArtwork.AtlasCount == 0, "Next retained old artwork or wrong level");
            MemoryBoundary("next-after");
        }

        public static void RunGameMeasurements()
        {
            if (!Application.isBatchMode)
            {
                Type gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                EditorWindow.GetWindow(gameView).Show();
                EditorWindow.GetWindow(gameView).Focus();
                Application.runInBackground = true;
            }
            gameMeasurements = verifyRelease = true;
            measurementOutput = "Logs/LobbyStage03/game-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            Directory.CreateDirectory(measurementOutput);
            File.WriteAllText(Path.Combine(measurementOutput, "environment.txt"),
                $"Unity {Application.unityVersion}; {SystemInfo.operatingSystem}; {SystemInfo.graphicsDeviceName}\n" +
                "New Editor Play session. Round0 first game; rounds1-3 same level warm repeats; rounds4-9 levels1/2/15. Native/managed memory includes Editor and instrumentation. Peak sampled once per Editor update; not device/process RSS or a sub-frame allocation peak. Camera callback is render completion, not display presentation. firstGameCameraMs=-1 means batch Editor did not render an observable game camera; it is unmeasured, not instant.\n");
            RunStartup();
        }

        private sealed class GameMeasurement : IDisposable
        {
            private readonly double started = EditorApplication.timeSinceStartup;
            private readonly int readsStart;
            private readonly List<string> reads;
            private readonly Sample sample;
            private double returning;
            private double lobbyReady;
            private bool disposed;
            private PuzzleGameSession game;
            private Lobby.LobbyScreen restoredScreen;

            public GameMeasurement(int round, int level, List<string> reads)
            {
                this.reads = reads; readsStart = reads.Count;
                sample = new Sample { round = round, level = level, width = Screen.width, height = Screen.height,
                    nativeBefore = Profiler.GetTotalAllocatedMemoryLong(), managedBefore = Profiler.GetMonoUsedSizeLong() };
                sample.nativePeak = sample.nativeBefore; sample.managedPeak = sample.managedBefore;
                EditorApplication.update += Observe;
                RenderPipelineManager.endCameraRendering += Rendered;
                Camera.onPostRender += RenderedBuiltin;
            }

            private void Observe()
            {
                sample.nativePeak = Math.Max(sample.nativePeak, Profiler.GetTotalAllocatedMemoryLong());
                sample.managedPeak = Math.Max(sample.managedPeak, Profiler.GetMonoUsedSizeLong());
            }

            private void Rendered(ScriptableRenderContext context, Camera camera) => RenderedBuiltin(camera);
            private void RenderedBuiltin(Camera camera)
            {
                if (sample.firstGameCameraMs >= 0 || camera.gameObject.scene.path != "Assets/Scenes/PuzzleGame.unity") return;
                if (game == null) game = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                if (game != null && game.IsReady) sample.firstGameCameraMs = (EditorApplication.timeSinceStartup - started) * 1000;
            }

            public void Ready(PuzzleGameSession session)
            {
                game = session;
                sample.selectedToInteractionMs = (EditorApplication.timeSinceStartup - started) * 1000;
                sample.dataMs = game.InitialDataMilliseconds; sample.boardMs = game.InitialBoardMilliseconds;
                sample.artworkMs = game.InitialArtworkMilliseconds; sample.sessionReadyMs = game.InitialReadyMilliseconds;
                sample.sessionInteractionMs = game.InitialInteractionMilliseconds;
                sample.metadataMs = game.LastPublishedLoadTiming.MetadataMilliseconds;
                sample.packReadMs = game.LastPublishedLoadTiming.PackReadMilliseconds;
                sample.packDecodeMs = game.LastPublishedLoadTiming.PackDecodeMilliseconds;
                sample.nativeReady = Profiler.GetTotalAllocatedMemoryLong(); sample.managedReady = Profiler.GetMonoUsedSizeLong();
                var artwork = Field<PuzzleArtwork>(game, "artwork");
                var loaded = ((IDictionary)typeof(PuzzleArtwork).GetField("atlases", Private).GetValue(artwork)).Keys.Cast<string>().OrderBy(value => value).ToArray();
                var plan = ElementResourcePlan.Create(game.State, Field<ElementVisualCatalog>(game, "visualCatalog"));
                Check(loaded.SequenceEqual(plan.Addresses.OrderBy(value => value)), "Actual atlases differ from resource plan");
                sample.atlases = loaded;
                if (sample.round == 0 && verifyDataFailure)
                {
                    var costs = new List<object>();
                    foreach (DictionaryEntry entry in (IDictionary)typeof(PuzzleArtwork).GetField("atlases", Private).GetValue(artwork))
                    {
                        var handle = Field<AsyncOperationHandle<SpriteAtlas>>(entry.Value, "handle");
                        SpriteAtlas atlas = handle.Result;
                        var sprites = new Sprite[atlas.spriteCount];
                        atlas.GetSprites(sprites);
                        try
                        {
                            var textures = sprites.Where(sprite => sprite != null).Select(sprite => sprite.texture).Distinct().ToArray();
                            costs.Add(new { address = entry.Key, includedSprites = atlas.spriteCount,
                                textures = textures.Select(texture => new { texture.name, texture.width, texture.height,
                                    format = texture.format.ToString(), nativeBytes = Profiler.GetRuntimeMemorySizeLong(texture) }).ToArray() });
                        }
                        finally { foreach (Sprite sprite in sprites) if (sprite != null) UnityEngine.Object.Destroy(sprite); }
                    }
                    File.WriteAllText(Path.Combine(measurementOutput, "atlas-costs.json"), Newtonsoft.Json.JsonConvert.SerializeObject(costs, Newtonsoft.Json.Formatting.Indented));
                }
            }

            public void Returning() => returning = EditorApplication.timeSinceStartup;
            public void LobbyReady(Lobby.LobbyScreen screen)
            {
                restoredScreen = screen;
                lobbyReady = EditorApplication.timeSinceStartup;
                sample.returnToLobbyMs = (lobbyReady - returning) * 1000;
                sample.listRestoreMs = screen.LastRestoreMilliseconds;
            }
            public void Finished()
            {
                Observe();
                sample.returnAndDrainMs = (EditorApplication.timeSinceStartup - returning) * 1000;
                sample.postReadyDrainMs = (EditorApplication.timeSinceStartup - lobbyReady) * 1000;
                sample.cancelledReadDrainMs = restoredScreen.LastRestoreReadDrainMilliseconds;
                sample.nativeReturned = Profiler.GetTotalAllocatedMemoryLong(); sample.managedReturned = Profiler.GetMonoUsedSizeLong();
                sample.reads = reads.Skip(readsStart).ToArray();
                File.AppendAllText(Path.Combine(measurementOutput, "samples.jsonl"), Newtonsoft.Json.JsonConvert.SerializeObject(sample) + "\n");
            }

            public void Dispose()
            {
                if (disposed) return; disposed = true;
                EditorApplication.update -= Observe;
                RenderPipelineManager.endCameraRendering -= Rendered;
                Camera.onPostRender -= RenderedBuiltin;
            }

            private sealed class Sample
            {
                public int round, level, width, height;
                public double firstGameCameraMs = -1, selectedToInteractionMs, dataMs, boardMs, artworkMs, sessionReadyMs, sessionInteractionMs, metadataMs, packReadMs, packDecodeMs, returnAndDrainMs;
                public double returnToLobbyMs, listRestoreMs, postReadyDrainMs, cancelledReadDrainMs;
                public long nativeBefore, managedBefore, nativePeak, managedPeak, nativeReady, managedReady, nativeReturned, managedReturned;
                public string[] atlases, reads;
            }
        }
    }
}
