using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameScreen;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using Levels;
using Levels.Editor;
using Simulation;
using Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
namespace LevelAuthoring.Editor
{
    public static class JsonPlayVerification
    {
        private static bool packed;
        private static string Output => packed ? "Logs/GameAuthoringStage06/packed-action-results.txt" : "Logs/GameAuthoringStage02/play-results.txt";
        public static void RunPacked() { packed = true; Run(); }
        private static readonly List<GameObject> Owners = new List<GameObject>();
        private static bool previousEnabled;
        private static EnterPlayModeOptions previousOptions;
        private static SceneAsset previousStart;
        private static double deadline;
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("별도 배치 검사에서 실행하세요.");
            File.WriteAllText(Output, "");
            previousEnabled = EditorSettings.enterPlayModeOptionsEnabled; previousOptions = EditorSettings.enterPlayModeOptions;
            previousStart = EditorSceneManager.playModeStartScene;
            EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update += Watchdog; EditorApplication.playModeStateChanged += Entered;
            EditorApplication.EnterPlaymode();
        }
        private static void Watchdog()
        {
            if (EditorApplication.timeSinceStartup < deadline) return;
            File.AppendAllText(Output, "FAIL timeout\n"); Finish(1);
        }
        private static void Entered(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= Entered;
            Execute().Forget(error => { File.AppendAllText(Output, "FAIL " + error + "\n"); Debug.LogException(error); Finish(1); });
        }
        private static void Finish(int code)
        {
            EditorApplication.update -= Watchdog; EditorApplication.playModeStateChanged -= Entered;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnabled; EditorSettings.enterPlayModeOptions = previousOptions;
            EditorSceneManager.playModeStartScene = previousStart;
            EditorApplication.Exit(code);
        }
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            File.AppendAllText(Output, "PASS " + name + "\n");
        }
        private static PuzzleGameSession CreateSession()
        {
            var owner = new GameObject("JsonStage02Session"); Owners.Add(owner);
            var board = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PuzzleWorldBoard>("Assets/Prefabs/Game/Puzzle/PuzzleWorldBoard.prefab"), owner.transform);
            var cameraOwner = new GameObject("Camera"); cameraOwner.transform.SetParent(owner.transform);
            var camera = cameraOwner.AddComponent<Camera>(); camera.orthographic = true;
            var session = owner.AddComponent<PuzzleGameSession>(); session.Configure(board, camera); return session;
        }
        private static async UniTask Clear()
        {
            foreach (var owner in Owners) if (owner != null) Object.Destroy(owner);
            Owners.Clear(); await UniTask.Yield(); await UniTask.Yield();
        }
        private static async UniTask Stable(PuzzleGameSession session)
        {
            await UniTask.Yield();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            await UniTask.WaitUntil(() => session.HasFailed || session.IsReady && !session.IsPresenting && !session.HasProgressFeedback &&
                (session.Phase == BoardActionPhase.Ready || session.Phase == BoardActionPhase.Stopped), cancellationToken: timeout.Token);
            if (session.HasFailed) throw new Exception(session.Message);
        }
        private static string State(PuzzleGameSession session)
        {
            string state = (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { session.State });
            return state + "|moves=" + session.State.MovesRemaining + "|missions=" + string.Join(",", session.State.Missions.Select(m => m.Progress)) +
                "|tutorial=" + session.TutorialState?.State + ":" + session.TutorialState?.StepIndex;
        }
        private static object Field(object value, string name) => value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value);
        private static async UniTask Execute()
        {
            try
            {
                var snapshot = new ContentSnapshotStore(packed ? AuthoringSourceSelection.ReadFolder(AuthoringSourceSelection.DefaultConfiguration) : File.ReadAllText("Logs/GameAuthoringStage02/latest-export.txt")).Read().Snapshot;
                var sources = LegacyContentExporter.Discover().Where(item => item.Kind == "level").OrderBy(item => ((LevelDefinition)item.Asset).LevelNumber).ToArray();
                int baseline = Resources.FindObjectsOfTypeAll<LevelDefinition>().Length;
                foreach (var source in sources)
                {
                    var level = (LevelDefinition)source.Asset;
                    string original = EditorJsonUtility.ToJson(level);
                    string id = (string)snapshot.Project.Data["sourceIds"].Single(entry => (string)entry["sourceGuid"] == source.Guid)["documentId"];
                    var jsonRequest = JsonPuzzlePlayAdapter.CreateRequest(snapshot, id, 12345);
                    string levelKey = "MoonRabbit.Tutorial.Completed." + level.LevelNumber;
                    string identityKey = "MoonRabbit.Tutorial.Identity." + level.Tutorial.completionId;
                    bool hadLevel = PlayerPrefs.HasKey(levelKey), hadIdentity = PlayerPrefs.HasKey(identityKey);
                    int savedLevel = PlayerPrefs.GetInt(levelKey), savedIdentity = PlayerPrefs.GetInt(identityKey);
                    var json = CreateSession(); var asset = CreateSession();
                    UniTask jsonStart = json.InitializeAsync(jsonRequest, PuzzlePlayContext.CreateTest(TutorialRunMode.Always), CancellationToken.None);
                    PuzzlePlayRequest comparison = PuzzlePlayRequest.Capture(level, 12345);
                    if (packed)
                    {
                        var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
                        Func<string, CancellationToken, UniTask<byte[]>> read = (address, token) =>
                        {
                            token.ThrowIfCancellationRequested();
                            string path = settings.groups.Where(group => group != null).SelectMany(group => group.entries).Single(entry => entry.address == address).AssetPath;
                            return UniTask.FromResult(AssetDatabase.LoadAssetAtPath<TextAsset>(path).bytes);
                        };
                        var method = typeof(PackedPuzzlePlayAdapter).GetMethod("CreateRequestAsync", BindingFlags.NonPublic | BindingFlags.Static);
                        comparison = (await (UniTask<PackedPuzzleTrial>)method.Invoke(null, new object[] { level.LevelNumber, 12345, read, CancellationToken.None })).Request;
                    }
                    UniTask assetStart = asset.InitializeAsync(comparison, PuzzlePlayContext.CreateTest(TutorialRunMode.Always), CancellationToken.None);
                    await jsonStart; await assetStart; await Stable(json); await Stable(asset);
                    Check(State(json) == State(asset), "initial board/supply/RNG/missions " + level.LevelNumber);
                    Check(json.TutorialState != null && json.TutorialState.State == TutorialProgressState.AwaitDescription, "real tutorial started " + level.LevelNumber);
                    foreach (var step in TutorialFlowResolver.Resolve(level).steps)
                    {
                        bool a, b;
                        if (step.kind == TutorialStepKind.Description) { a = json.TryAdvanceTutorial(); b = asset.TryAdvanceTutorial(); }
                        else if (step.kind == TutorialStepKind.Item) { a = json.TryUseItem(step.item, step.first); b = asset.TryUseItem(step.item, step.first); }
                        else { a = json.TrySwap(step.first, step.second); b = asset.TrySwap(step.first, step.second); }
                        Check(a && b, "same tutorial action accepted " + level.LevelNumber + " " + step.kind);
                        await Stable(json); await Stable(asset);
                        Check(State(json) == State(asset), "settlement/supply/power/tutorial " + level.LevelNumber + " " + step.kind);
                    }
                    Check(json.TutorialState.State == TutorialProgressState.Completed, "tutorial completed " + level.LevelNumber);
                    await json.RestartAsync(CancellationToken.None); await Stable(json);
                    Check(json.PlayContext.IsTest, "restart remains test " + level.LevelNumber);
                    Check(PlayerPrefs.HasKey(levelKey) == hadLevel && PlayerPrefs.HasKey(identityKey) == hadIdentity &&
                        PlayerPrefs.GetInt(levelKey) == savedLevel && PlayerPrefs.GetInt(identityKey) == savedIdentity, "formal completion unchanged " + level.LevelNumber);
                    Check(EditorJsonUtility.ToJson(level) == original && !EditorUtility.IsDirty(level), "SO unchanged " + level.LevelNumber);
                    await Clear();
                }
                Check(Resources.FindObjectsOfTypeAll<LevelDefinition>().Length == baseline, "level objects released after four runs");
                var firstLevel = snapshot.Documents.First(doc => doc.Kind == "level");
                var request = JsonPuzzlePlayAdapter.CreateRequest(snapshot, firstLevel.Id, 73);
                var cancelled = CreateSession();
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel(); bool rejected = false;
                    try { await cancelled.InitializeAsync(request, PuzzlePlayContext.CreateTest(), cancellation.Token); }
                    catch (OperationCanceledException) { rejected = true; }
                    Check(rejected, "cancelled JSON session propagates cancellation");
                    await UniTask.Yield(); Check(!cancelled.IsReady, "cancelled session has no default fallback");
                }
                await Clear();
                var invalid = CreateSession(); bool failed = false;
                try { await invalid.InitializeAsync(new PuzzlePlayRequest(new byte[] { 1, 2, 3 }, 1, 1, null), PuzzlePlayContext.CreateTest(), CancellationToken.None); }
                catch (Exception) { failed = true; }
                Check(failed, "invalid request fails"); await Clear();
                Check(Resources.FindObjectsOfTypeAll<LevelDefinition>().Length == baseline, "failure/cancel objects released");
                Check(Object.FindObjectsByType<PuzzleGameSession>(FindObjectsSortMode.None).Length == 0, "all sessions released");
                Finish(0);
            }
            catch { await Clear(); throw; }
        }
    }
}
