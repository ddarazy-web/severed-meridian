using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Levels;
using Levels.Editor;
using Simulation;
using Tutorial;
using Tutorial.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameScreen.Editor
{
    public static partial class PuzzlePlayBoundaryVerification
    {
        private static readonly List<GameObject> sessionObjects = new List<GameObject>();
        private static bool previousOptionsEnabled;
        private static EnterPlayModeOptions previousOptions;
        private static SceneAsset previousStartScene;
        private static double sessionDeadline;

        private static void StartSessionVerification()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("임시 세션 검사는 별도 배치 실행에서만 허용됩니다.");
            previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousOptions = EditorSettings.enterPlayModeOptions;
            previousStartScene = EditorSceneManager.playModeStartScene;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            sessionDeadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += SessionWatchdog;
            EditorApplication.playModeStateChanged += OnSessionPlayMode;
            EditorApplication.EnterPlaymode();
        }

        private static void SessionWatchdog()
        {
            if (EditorApplication.timeSinceStartup < sessionDeadline) return;
            File.AppendAllText(Output + "/boundary-results.txt", "FAIL session verification timeout\n");
            RestoreSessionSettings(); EditorApplication.Exit(1);
        }

        private static void OnSessionPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= OnSessionPlayMode;
            VerifyCommonSessions().Forget(error =>
            {
                File.AppendAllText(Output + "/boundary-results.txt", "FAIL " + error + "\n");
                Debug.LogException(error); RestoreSessionSettings(); EditorApplication.Exit(1);
            });
        }

        private static void RestoreSessionSettings()
        {
            EditorApplication.update -= SessionWatchdog;
            EditorApplication.playModeStateChanged -= OnSessionPlayMode;
            EditorSettings.enterPlayModeOptionsEnabled = previousOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousOptions;
            EditorSceneManager.playModeStartScene = previousStartScene;
        }

        private static PuzzleGameSession NewSession()
        {
            GameObject root = new GameObject("Stage01BoundarySession"); sessionObjects.Add(root);
            PuzzleWorldBoard board = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PuzzleWorldBoard>("Assets/Prefabs/Game/Puzzle/PuzzleWorldBoard.prefab"), root.transform);
            GameObject cameraOwner = new GameObject("Camera"); cameraOwner.transform.SetParent(root.transform);
            Camera camera = cameraOwner.AddComponent<Camera>(); camera.orthographic = true;
            PuzzleGameSession session = root.AddComponent<PuzzleGameSession>(); session.Configure(board, camera);
            return session;
        }

        private static async UniTask DestroySessions()
        {
            foreach (GameObject owner in sessionObjects) if (owner != null) UnityEngine.Object.Destroy(owner);
            sessionObjects.Clear();
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            await UniTask.Yield();
        }

        private static async UniTask Stable(PuzzleGameSession session)
        {
            using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            await UniTask.WaitUntil(() => session.HasFailed || session.IsReady && !session.IsPresenting && !session.HasProgressFeedback &&
                (session.Phase == BoardActionPhase.Ready || session.Phase == BoardActionPhase.Stopped), cancellationToken: timeout.Token);
            Check(!session.HasFailed, "session stable: " + session.Message);
        }

        private static string StateSnapshot(PuzzleGameSession session)
        {
            string state = (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { session.State });
            return state + "|phase=" + session.Phase + "|moves=" + session.State.MovesRemaining + "|missions=" +
                string.Join(",", session.State.Missions.Select(mission => mission.Progress)) + "|tutorial=" + session.TutorialState?.State;
        }

        private static async UniTask MustFail(Func<UniTask> action, string name, bool cancellation = false)
        {
            Exception caught = null;
            try { await action(); } catch (Exception error) { caught = error; }
            Check(caught != null && (!cancellation || caught is OperationCanceledException), name);
        }

        private static async UniTask VerifyCommonSessions()
        {
            int exit = 0;
            LevelDefinition source = null;
            try
            {
                source = LevelTutorialDataVerification.Fixture(991701);
                source.Tutorial.flow = null; source.Tutorial.steps.Clear(); source.Tutorial.bindings.Clear();
                string original = JsonUtility.ToJson(source);
                int levelCount = Resources.FindObjectsOfTypeAll<LevelDefinition>().Length;
                PuzzlePlayRequest request = PuzzlePlayRequest.Capture(source, 12345, null);
                await VerifyAtlasCancellation(request);
                PuzzleGameSession common = NewSession(), legacy = NewSession();
                legacy.ConfigureTutorial(TutorialExecutionContext.CreateTest((int)TutorialRunMode.Never));
                // 두 세션 모두 첫 await 전에 시작해 Start의 자동 진입이 끼어들지 않게 한다.
                UniTask commonStart = common.InitializeAsync(request, PuzzlePlayContext.CreateTest(TutorialRunMode.Never), CancellationToken.None);
                UniTask legacyStart = legacy.InitializeAsync(request.CreateDefinition(), request.Seed, CancellationToken.None);
                await commonStart; await legacyStart; await Stable(common); await Stable(legacy);
                Check(common.PlayContext.IsTest && !common.LevelAdvanceEnabled, "test context disables formal advance");
                Check(StateSnapshot(common) == StateSnapshot(legacy), "common vs legacy initial board/moves/missions/tutorial");
                string initial = StateSnapshot(common);
                ActionCandidate swap = ActionQuery.Find(common.State).First(candidate => candidate.Second.HasValue);
                Check(common.TrySwap(swap.First, swap.Second.Value) && legacy.TrySwap(swap.First, swap.Second.Value), "both paths accept same legal swap");
                await Stable(common); await Stable(legacy);
                Check(StateSnapshot(common) == StateSnapshot(legacy), "common vs legacy swap and complete settlement");
                string settled = StateSnapshot(common);
                Check(common.SetPaused(true), "pause accepted");
                await UniTask.Delay(50);
                Check(common.IsPaused && StateSnapshot(common) == settled && !common.CanAcceptInput, "pause preserves state and blocks input");
                Check(common.SetPaused(false) && !common.IsPaused, "resume accepted");
                await common.RestartAsync(CancellationToken.None); await Stable(common);
                Check(StateSnapshot(common) == initial && common.PlayContext.IsTest, "restart preserves test policy and initial state");
                Check(common.TryUseItem(BoardItem.Shuffle), "test item action accepted");
                await Stable(common);
                await MustFail(() => common.InitializeAsync(request, PuzzlePlayContext.CreateTest(), CancellationToken.None), "duplicate initialization faults");
                Check(common.IsReady && common.PlayContext.IsTest, "duplicate preserves existing session");
                await DestroySessions();

                PuzzleGameSession canceled = NewSession();
                using (CancellationTokenSource token = new CancellationTokenSource())
                {
                    token.Cancel();
                    await MustFail(() => canceled.InitializeAsync(request, PuzzlePlayContext.CreateTest(), token.Token), "pre-canceled initialization faults", true);
                }
                await UniTask.Yield();
                Check(!canceled.IsReady && canceled.State == null, "pre-cancel blocks Start fallback");
                await DestroySessions();

                PuzzleGameSession loading = NewSession();
                using (CancellationTokenSource token = new CancellationTokenSource())
                {
                    UniTask pending = loading.InitializeAsync(request, PuzzlePlayContext.CreateTest(), token.Token);
                    Check(!loading.IsReady, "loading remains unpublished before cancellation");
                    token.Cancel(); await MustFail(() => pending, "loading cancellation faults", true);
                }
                await UniTask.Yield(); Check(!loading.IsReady, "canceled loading cannot become ready");
                await DestroySessions();

                PuzzleGameSession destroyedLoading = NewSession();
                UniTask destroyedPending = destroyedLoading.InitializeAsync(request, PuzzlePlayContext.CreateTest(), CancellationToken.None);
                UnityEngine.Object.Destroy(destroyedLoading.gameObject);
                await MustFail(() => destroyedPending, "destroy during loading faults as cancellation", true);
                await DestroySessions();
                PuzzleGameSession corrupt = NewSession();
                PuzzlePlayRequest corruptRequest = new PuzzlePlayRequest(new byte[] { 1, 2, 3 }, request.LevelNumber, request.Seed, null);
                await MustFail(() => corrupt.InitializeAsync(corruptRequest, PuzzlePlayContext.CreateTest(), CancellationToken.None), "corrupt snapshot faults");
                await UniTask.Yield();
                Check(corrupt.HasFailed && !corrupt.IsReady && corrupt.State == null, "corrupt snapshot blocks Start fallback");
                await DestroySessions();
                PuzzleGameSession missingRequest = NewSession();
                await MustFail(() => missingRequest.InitializeAsync((PuzzlePlayRequest)null, PuzzlePlayContext.CreateTest(), CancellationToken.None), "null request faults");
                await UniTask.Yield(); Check(!missingRequest.IsReady && missingRequest.State == null, "null request blocks Start fallback");
                await DestroySessions();
                PuzzleGameSession missingContext = NewSession();
                await MustFail(() => missingContext.InitializeAsync(request, null, CancellationToken.None), "null context faults");
                await UniTask.Yield(); Check(!missingContext.IsReady && missingContext.State == null, "null context blocks Start fallback");
                await DestroySessions();

                for (int repeat = 0; repeat < 3; repeat++)
                {
                    PuzzleGameSession repeated = NewSession();
                    await repeated.InitializeAsync(request, PuzzlePlayContext.CreateTest(TutorialRunMode.Never), CancellationToken.None);
                    Check(repeated.IsReady, "repeat initialization " + repeat);
                    PuzzleArtwork repeatedArt = (PuzzleArtwork)PrivateField(repeated, "artwork");
                    Board.BoardSpriteAtlas[] repeatedAtlases = AtlasOwners(repeatedArt);
                    int events = 0; repeated.Changed += () => events++;
                    await DestroySessions(); await UniTask.Yield();
                    Check(repeatedArt.AtlasCount == 0 && repeatedAtlases.All(atlas => !HandleValid(atlas)) &&
                        PrivateField(repeated, "Changed") == null && events == 0, "repeat releases atlases and subscribers " + repeat);
                }
                Check(Resources.FindObjectsOfTypeAll<LevelDefinition>().Length == levelCount, "owned definitions released after repeated destruction");
                Check(JsonUtility.ToJson(source) == original, "source data unchanged through actions/restart/cancel");
                await VerifyTutorialIsolation(source);
                Check(UnityEngine.Object.FindObjectsByType<PuzzleGameSession>(FindObjectsSortMode.None).Length == 0, "all test session objects released");
                File.AppendAllText(Output + "/boundary-results.txt", "PASS SessionBoundarySuiteComplete\n");
            }
            catch (Exception error)
            {
                exit = 1; File.AppendAllText(Output + "/boundary-results.txt", "FAIL session " + error + "\n"); Debug.LogException(error);
            }
            finally
            {
                await DestroySessions();
                if (source != null) UnityEngine.Object.Destroy(source);
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                RestoreSessionSettings(); EditorApplication.Exit(exit);
            }
        }

        private static async UniTask VerifyTutorialIsolation(LevelDefinition source)
        {
            string completion = "stage01-test-" + Guid.NewGuid().ToString("N");
            source.Tutorial.completionId = completion;
            source.Tutorial.steps.Add(new TutorialStepDefinition { instructions = "확인 후 계속" });
            string levelKey = "MoonRabbit.Tutorial.Completed." + source.LevelNumber;
            string identityKey = "MoonRabbit.Tutorial.Identity." + completion;
            bool hadLevel = PlayerPrefs.HasKey(levelKey); int levelValue = PlayerPrefs.GetInt(levelKey, 0);
            bool hadIdentity = PlayerPrefs.HasKey(identityKey); int identityValue = PlayerPrefs.GetInt(identityKey, 0);
            PuzzlePlayContext context = PuzzlePlayContext.CreateTest(TutorialRunMode.Automatic);
            PuzzleGameSession session = NewSession();
            await session.InitializeAsync(PuzzlePlayRequest.Capture(source, 12345, null), context, CancellationToken.None);
            await Stable(session);
            Check(session.TutorialState?.State == TutorialProgressState.AwaitDescription, "test tutorial starts");
            Check(session.TryAdvanceTutorial() && session.TutorialState.State == TutorialProgressState.Completed, "actual test tutorial completion");
            Check(!context.Tutorial.ShouldRun(source), "test completion retained in test memory");
            Check(session.SetPaused(true) && session.SetPaused(false), "completed tutorial pause/resume");
            await session.RestartAsync(CancellationToken.None); await Stable(session);
            Check(session.TutorialState == null, "automatic test restart respects in-memory completion");
            Check(session.TryUseItem(BoardItem.Shuffle), "item after tutorial completion"); await Stable(session);
            Check(PlayerPrefs.HasKey(levelKey) == hadLevel && PlayerPrefs.GetInt(levelKey, 0) == levelValue &&
                PlayerPrefs.HasKey(identityKey) == hadIdentity && PlayerPrefs.GetInt(identityKey, 0) == identityValue,
                "test tutorial/item/restart preserves formal completion PlayerPrefs");
            await DestroySessions();
        }

        private static object PrivateField(object owner, string name)
            => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        private static Board.BoardSpriteAtlas[] AtlasOwners(PuzzleArtwork art)
            => ((Dictionary<string, Board.BoardSpriteAtlas>)PrivateField(art, "atlases")).Values.ToArray();

        private static bool HandleValid(Board.BoardSpriteAtlas atlas)
            => ((UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<UnityEngine.U2D.SpriteAtlas>)PrivateField(atlas, "handle")).IsValid();

        private static async UniTask VerifyAtlasCancellation(PuzzlePlayRequest request)
        {
            PuzzleGameSession session = NewSession();
            PuzzleArtwork observed = null;
            Board.BoardSpriteAtlas[] atlases = null;
            bool fired = false;
            int changes = 0;
            session.Changed += () => changes++;
            Func<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation, string> previous =
                UnityEngine.AddressableAssets.Addressables.InternalIdTransformFunc;
            using CancellationTokenSource cancel = new CancellationTokenSource();
            try
            {
                UnityEngine.AddressableAssets.Addressables.InternalIdTransformFunc = location =>
                {
                    PuzzleArtwork art = (PuzzleArtwork)PrivateField(session, "artwork");
                    if (!fired && art != null && art.AtlasCount > 0 && (int)PrivateField(art, "pending") > 0)
                    {
                        fired = true; observed = art; atlases = AtlasOwners(art);
                        cancel.Cancel();
                    }
                    return previous == null ? location.InternalId : previous(location);
                };
                await MustFail(() => session.InitializeAsync(request, PuzzlePlayContext.CreateTest(), cancel.Token),
                    "cancel during real atlas request faults", true);
                Check(fired && observed != null && atlases.Length > 0, "cancellation observed inside pending artwork load");
                await UniTask.Yield(); await UniTask.Yield();
                Check(observed.AtlasCount == 0 && (int)PrivateField(observed, "pending") == 0 && atlases.All(atlas => !HandleValid(atlas)),
                    "canceled atlas requests complete and release owned handles");
                await DestroySessions();
                int endedChanges = changes;
                await UniTask.Yield(); await UniTask.Yield();
                Check(PrivateField(session, "Changed") == null && changes == endedChanges, "destroy clears subscribers and stops Changed callbacks");
            }
            finally { UnityEngine.AddressableAssets.Addressables.InternalIdTransformFunc = previous; }
        }
    }
}

