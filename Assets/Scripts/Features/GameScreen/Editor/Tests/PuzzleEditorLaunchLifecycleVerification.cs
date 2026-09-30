using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Levels;
using Levels.Editor;
using Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static class PuzzleEditorLaunchLifecycleVerification
    {
        private const string Key = "StageThree.LifecycleTest.";
        private const string Output = "Logs/PuzzleEditorLaunchVerification/lifecycle-results.txt";
        private const int First = 900001;
        private const string Marker = "StageThree Unsaved Scene Marker";
        static PuzzleEditorLaunchLifecycleVerification()
        {
            EditorApplication.update += Observe;
            SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (SessionState.GetInt(Key + "phase", 0) == 1 && SessionState.GetInt(Key + "case", 0) == 10)
                    SessionState.SetBool(Key + "cancelEnteredPlay", true);
                if (SessionState.GetInt(Key + "phase", 0) == 1 && SessionState.GetInt(Key + "case", 0) == 8 && scene.path == PuzzleGameAssets.ScenePath)
                {
                    SessionState.SetBool(Key + "earlyExit", true);
                    EditorApplication.ExitPlaymode();
                }
            };
        }

        [MenuItem("Tools/Match/게임 실행 왕복 검증")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PuzzleEditorLauncher.IsBusy) throw new Exception("게임 종료 후 검사하세요.");
            if (File.Exists(LevelPackBuild.FilePath(First))) throw new Exception("검사 전용 구간이 사용 중입니다.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output)); File.WriteAllText(Output, "");
            SessionState.SetString(Key + "override", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(Key + "optionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Key + "options", (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetString(Key + "activeScene", SceneManager.GetActiveScene().path);
            LevelDefinition real = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
            SessionState.SetString(Key + "realJson", JsonUtility.ToJson(real));
            SessionState.SetBool(Key + "realDirty", EditorUtility.IsDirty(real));
            SessionState.SetString(Key + "fileHash", Hash("Assets/Data/Levels/Level_01.asset"));
            SessionState.SetString(Key + "packHash", Hash(LevelPackBuild.FilePath(1)));
            SessionState.SetInt(Key + "definitionCount", Resources.FindObjectsOfTypeAll<LevelDefinition>().Length);
            Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject marker = new GameObject(Marker); SceneManager.MoveGameObjectToScene(marker, scratch);
            EditorSceneManager.MarkSceneDirty(scratch);
            SessionState.SetString(Key + "scenes", Scenes());
            SessionState.SetInt(Key + "case", 0);
            SessionState.SetBool(Key + "cancelEnteredPlay", false);
            SessionState.SetBool(Key + "earlyExit", false);
            try { BeginCase(); } catch (Exception error) { Fail(error); }
        }

        private static void BeginCase()
        {
            int index = SessionState.GetInt(Key + "case", 0);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = index >= 4 ? EnterPlayModeOptions.DisableDomainReload : EnterPlayModeOptions.None;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/game.unity");
            SessionState.SetFloat(Key + "deadline", (float)EditorApplication.timeSinceStartup + 90);
            SessionState.SetInt(Key + "phase", 1);
            if (index == 9)
            {
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(PuzzleGameAssets.ScenePath);
                EditorApplication.EnterPlaymode(); return;
            }
            LevelDefinition source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset"));
            source.hideFlags = HideFlags.DontSave;
            int number = First + index % 2;
            JsonUtility.FromJsonOverwrite("{\"levelNumber\":" + number + ",\"moveCount\":23}", source);
            File.WriteAllBytes(LevelPackBuild.FilePath(number), LevelPackCodec.Encode(new[] { source }));
            JsonUtility.FromJsonOverwrite("{\"moveCount\":37}", source);
            EditorUtility.SetDirty(source);
            SessionState.SetString(Key + "sourceJson", JsonUtility.ToJson(source));
            SessionState.SetInt(Key + "source", source.GetInstanceID());
            SessionState.SetBool(Key + "sourceDirty", EditorUtility.IsDirty(source));
            LevelEditorWindow owner = ScriptableObject.CreateInstance<LevelEditorWindow>();
            SessionState.SetInt(Key + "owner", owner.GetInstanceID());
            owner.ShowUtility(); owner.CreateGUI();
            owner.SetLevel(source);
            owner.CreateGUI();
            PuzzleEditorLevelSource mode = index % 4 >= 2 ? PuzzleEditorLevelSource.MemoryPack : PuzzleEditorLevelSource.Asset;
            int seed = 12345 + index;
            PuzzleEditorLaunchRequest request = PuzzleEditorLaunchRequest.Capture(source, mode, seed);
            LevelDefinition baseline = request.CreateDefinition();
            try
            {
                StartingBoardSearch search = new StartingBoardSearch(baseline, seed);
                while (!search.IsDone) search.Advance(128);
                if (search.Status != StartingBoardStatus.Success) throw new Exception(search.Message);
                SessionState.SetString(Key + "baseline", Snapshot(search.State));
            }
            finally { UnityEngine.Object.DestroyImmediate(baseline); }
            if (index < 8)
            {
                owner.rootVisualElement.Q<PopupField<string>>("game-level-source").value = mode == PuzzleEditorLevelSource.Asset ? "에셋" : "MemoryPack";
                owner.rootVisualElement.Q<IntegerField>("game-level-seed").value = seed;
                Button button = owner.rootVisualElement.Q<Button>("game-play-level");
                using NavigationSubmitEvent click = NavigationSubmitEvent.GetPooled(); click.target = button; button.SendEvent(click);
                Check(PuzzleEditorLauncher.IsBusy, "EditorButtonLaunch case=" + index);
            }
            else PuzzleEditorLauncher.Launch(request, owner.GetInstanceID());
            bool rejected = false;
            try { PuzzleEditorLauncher.Launch(request, owner.GetInstanceID()); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "DoubleLaunchRejected case=" + index);
            if (index == 10) EditorApplication.ExitPlaymode();
        }

        private static void Observe()
        {
            int phase = SessionState.GetInt(Key + "phase", 0);
            if (phase == 0) return;
            if (phase == 98)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) Cleanup();
                return;
            }
            try
            {
                int index = SessionState.GetInt(Key + "case", 0);
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "deadline", 0)) throw new Exception("Play 왕복 시간 초과 case=" + index);
                if (phase == 1 && index == 8 && SessionState.GetBool(Key + "earlyExit", false) && !EditorApplication.isPlayingOrWillChangePlaymode)
                { SessionState.SetInt(Key + "phase", 2); return; }
                if (phase == 1 && index == 10 && !EditorApplication.isPlayingOrWillChangePlaymode && !PuzzleEditorLauncher.IsBusy)
                { SessionState.SetInt(Key + "phase", 2); return; }
                if (phase == 1 && EditorApplication.isPlaying)
                {
                    if (index == 8) return;
                    PuzzleGameSession session = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                    if (session == null || !session.CanAcceptInput) return;
                    Check(UnityEngine.Object.FindObjectsByType<PuzzleGameSession>(FindObjectsSortMode.None).Length == 1, "SingleSession case=" + index);
                    if (index == 9)
                    {
                        Check(session.State.LevelNumber == 1, "NextOrdinaryPlayUsesDefault");
                        LevelDefinition normal = LevelPackCodec.ReadLevel(File.ReadAllBytes(LevelPackBuild.FilePath(1)), 1);
                        try
                        {
                            StartingBoardSearch search = new StartingBoardSearch(normal, 12345);
                            while (!search.IsDone) search.Advance(128);
                            Check(Snapshot(session.State) == Snapshot(search.State), "DefaultSeed12345");
                        }
                        finally { UnityEngine.Object.Destroy(normal); }
                    }
                    else
                    {
                        Check(session.State.LevelNumber == First + index % 2 && session.State.MovesRemaining == (index % 4 >= 2 ? 23 : 37), "SelectedNumberAndSource case=" + index);
                        Check(Snapshot(session.State) == SessionState.GetString(Key + "baseline", ""), "SnapshotMatchesDirectExecutor case=" + index);
                        Check(SessionState.GetString("Puzzle.EditorLaunch.bytes", "") == "", "RequestConsumedOnce case=" + index);
                    }
                    if (index == 11)
                    {
                        LevelEditorWindow closed = EditorUtility.InstanceIDToObject(SessionState.GetInt(Key + "owner", 0)) as LevelEditorWindow;
                        closed.Close();
                        LevelDefinition closedSource = EditorUtility.InstanceIDToObject(SessionState.GetInt(Key + "source", 0)) as LevelDefinition;
                        if (closedSource != null) UnityEngine.Object.DestroyImmediate(closedSource);
                    }
                    SessionState.SetInt(Key + "phase", 2); EditorApplication.ExitPlaymode();
                }
                else if (phase == 2 && !EditorApplication.isPlayingOrWillChangePlaymode && !PuzzleEditorLauncher.IsBusy)
                {
                    Check(Scenes() == SessionState.GetString(Key + "scenes", ""), "UnsavedScenesPreserved case=" + index);
                    string expectedOverride = index == 9 ? PuzzleGameAssets.ScenePath : "Assets/Scenes/game.unity";
                    Check(AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == expectedOverride, "ExistingStartSceneOverridePreserved case=" + index);
                    if (index != 9 && index != 11)
                    {
                        LevelDefinition source = EditorUtility.InstanceIDToObject(SessionState.GetInt(Key + "source", 0)) as LevelDefinition;
                        LevelEditorWindow owner = EditorUtility.InstanceIDToObject(SessionState.GetInt(Key + "owner", 0)) as LevelEditorWindow;
                        Check(source != null && JsonUtility.ToJson(source) == SessionState.GetString(Key + "sourceJson", "") && EditorUtility.IsDirty(source) == SessionState.GetBool(Key + "sourceDirty", false), "UnsavedSourceAndDirtyPreserved case=" + index);
                        Check(owner != null && owner.CurrentLevel == source && owner.WorkspaceTab == 0, "OriginalWindowSelectionRestored case=" + index);
                        if (index < 8)
                        {
                            string message = (string)typeof(LevelEditorWindow).GetField("gameLaunchMessage", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
                            Check(message.Contains("종료하고 편집"), "NormalExitIsNotReportedAsCancellation case=" + index + " message=" + message);
                            Check(owner.rootVisualElement.Q<IntegerField>("game-level-seed").value == 12345 + index &&
                                owner.rootVisualElement.Q<PopupField<string>>("game-level-source").value == (index % 4 >= 2 ? "MemoryPack" : "에셋"), "WindowSourceAndSeedSurviveReload case=" + index);
                        }
                        owner.Close(); UnityEngine.Object.DestroyImmediate(source);
                    }
                    Check(SessionState.GetString("Puzzle.EditorLaunch.bytes", "") == "", "NoStaleRequest case=" + index);
                    if (index == 8) Check(SessionState.GetBool(Key + "earlyExit", false), "ExitDuringLoad");
                    if (index == 10) Check(!SessionState.GetBool(Key + "cancelEnteredPlay", false), "CancelledEntryLeavesNoRequest");
                    if (index == 11)
                    {
                        Check(EditorUtility.InstanceIDToObject(SessionState.GetInt(Key + "owner", 0)) == null, "ClosedWindowNotRecreated");
                        Check(!Resources.FindObjectsOfTypeAll<LevelDefinition>().Any(d => d.LevelNumber >= First && d.LevelNumber <= First + 1), "SnapshotDestroyedOnExit");
                        Cleanup(); File.AppendAllText(Output, "PASS LifecycleSuiteComplete\n"); return;
                    }
                    SessionState.SetInt(Key + "case", index + 1); BeginCase();
                }
            }
            catch (Exception error) { Fail(error); }
        }

        private static string Scenes() => string.Join("|", Enumerable.Range(0, SceneManager.sceneCount).Select(i =>
        { Scene scene = SceneManager.GetSceneAt(i); return scene.path + ":" + scene.name + ":" + scene.isDirty + ":" + (scene == SceneManager.GetActiveScene()); }));
        private static string Snapshot(object state) => (string)typeof(LevelInitialStateVerification).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new[] { state });
        private static string Hash(string path) { using SHA256 sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
        private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); File.AppendAllText(Output, "PASS " + name + "\n"); }
        private static void Cleanup()
        {
            SessionState.SetInt(Key + "phase", 0);
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + "optionsEnabled", false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Key + "options", 0);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key + "override", ""));
            LevelEditorWindow owner = EditorUtility.InstanceIDToObject(SessionState.GetInt(Key + "owner", 0)) as LevelEditorWindow;
            if (owner != null) owner.Close();
            LevelDefinition source = EditorUtility.InstanceIDToObject(SessionState.GetInt(Key + "source", 0)) as LevelDefinition;
            if (source != null) UnityEngine.Object.DestroyImmediate(source);
            GameObject marker = GameObject.Find(Marker);
            if (marker != null) EditorSceneManager.CloseScene(marker.scene, true);
            Scene active = SceneManager.GetSceneByPath(SessionState.GetString(Key + "activeScene", ""));
            if (active.IsValid()) SceneManager.SetActiveScene(active);
            string path = LevelPackBuild.FilePath(First);
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            else if (File.Exists(path)) File.Delete(path);
            LevelDefinition real = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
            Check(JsonUtility.ToJson(real) == SessionState.GetString(Key + "realJson", "") && EditorUtility.IsDirty(real) == SessionState.GetBool(Key + "realDirty", false), "RealAssetMemoryAndDirtyUnchanged");
            Check(Hash("Assets/Data/Levels/Level_01.asset") == SessionState.GetString(Key + "fileHash", "") && Hash(LevelPackBuild.FilePath(1)) == SessionState.GetString(Key + "packHash", ""), "RealAssetAndPackFilesUnchanged");
        }
        private static void Fail(Exception error)
        {
            File.AppendAllText(Output, "FAIL " + error + "\n");
            SessionState.SetInt(Key + "phase", 98);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.ExitPlaymode();
            }
            else Cleanup();
            Debug.LogException(error);
        }
    }
}
