using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameScreen;
using GameScreen.Navigation;
using Levels;
using Lobby;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class LobbyEditorEntryVerification
    {
        private static bool priorEnabled;
        private static EnterPlayModeOptions priorOptions;
        private static UnityEditor.SceneAsset priorScene;
        private static int phase, sourceIndex, exit;
        private static double deadline;
        private static string original, output;
        private static LevelDefinition source;

        public static void Run()
        {
            output = "Logs/LobbyStage02/editor-entry.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, "");
            priorEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            priorOptions = EditorSettings.enterPlayModeOptions;
            priorScene = UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset"));
            original = JsonUtility.ToJson(source);
            phase = sourceIndex = exit = 0;
            deadline = EditorApplication.timeSinceStartup + 150;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (phase == 99)
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    Finish(); return;
                }
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Editor entry timeout");
                if (phase == 0)
                {
                    if (source == null)
                    {
                        source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset"));
                        Check(JsonUtility.ToJson(source) == original, "source reloaded unchanged");
                    }
                    var mode = sourceIndex == 0 ? GameScreen.Editor.PuzzleEditorLevelSource.Asset : GameScreen.Editor.PuzzleEditorLevelSource.MemoryPack;
                    var request = GameScreen.Editor.PuzzleEditorLaunchRequest.Capture(source, mode, 12345);
                    GameScreen.Editor.PuzzleEditorLauncher.Launch(request, 0);
                    phase = 1;
                }
                else if (phase == 1 && EditorApplication.isPlaying)
                {
                    var game = UnityEngine.Object.FindFirstObjectByType<PuzzleGameSession>();
                    if (game == null || !game.IsReady) return;
                    Check(game.State.LevelNumber == 1 && game.PlayContext.IsTest && !game.PlayContext.Tutorial.UsesPlayerProgress, "selected level and isolated Editor context " + sourceIndex);
                    Check(UnityEngine.Object.FindObjectsByType<PuzzleGameSession>(FindObjectsSortMode.None).Length == 1, "single session " + sourceIndex);
                    Check(SessionState.GetString("Puzzle.EditorLaunch.bytes", "") == "", "request consumed " + sourceIndex);
                    phase = 2; EditorApplication.ExitPlaymode();
                }
                else if (phase == 2 && !EditorApplication.isPlayingOrWillChangePlaymode && !GameScreen.Editor.PuzzleEditorLauncher.IsBusy)
                {
                    Check(UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene == priorScene, "previous launch scene restored " + sourceIndex);
                    Check(JsonUtility.ToJson(source) == original, "source unchanged " + sourceIndex);
                    if (++sourceIndex < 2) phase = 0;
                    else { phase = 99; Finish(); }
                }
            }
            catch (Exception error)
            {
                exit = 1; phase = 99; File.AppendAllText(output, "FAIL " + error + "\n");
                if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
                else Finish();
            }
        }

        private static void Check(bool value, string label)
        {
            if (!value) throw new Exception(label);
            File.AppendAllText(output, "PASS " + label + "\n");
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            EditorSettings.enterPlayModeOptionsEnabled = priorEnabled;
            EditorSettings.enterPlayModeOptions = priorOptions;
            UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = priorScene;
            if (source != null) UnityEngine.Object.DestroyImmediate(source);
            EditorApplication.Exit(exit);
        }
    }

    public static class PublishedSessionEntryVerification
    {
        public static async void Run()
        {
            string root = "Logs/LobbyStage02/session-entry-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(root);
            GameObject owner = null;
            int exit = 1;
            try
            {
                MethodInfo initialize = typeof(PuzzleGameSession).GetMethod("InitializePublishedAsync");
                Check(initialize != null, "Published game entry missing");
                var index = new LevelLobbyIndex { Entries = new[]
                {
                    new LevelLobbyEntry { LevelId = "one", LevelNumber = 1, DisplayName = "첫 레벨" },
                    new LevelLobbyEntry { LevelId = "last", LevelNumber = 15, DisplayName = "마지막" }
                } };
                var snapshot = (LobbyIndexSnapshot)Activator.CreateInstance(typeof(LobbyIndexSnapshot), BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { "test-generation", index }, null);
                var route = new LobbyGameRoute(snapshot);
                Check(route.TryCreateRequest("one", 1, snapshot.Generation, out var request), "Request failed");
                owner = new GameObject("PublishedSessionEntryVerification"); owner.SetActive(false);
                var session = owner.AddComponent<PuzzleGameSession>();
                bool cancelled = false;
                try { await (UniTask)initialize.Invoke(session, new object[] { request, route, 12345, new CancellationToken(true) }); }
                catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled, "Entry swallowed cancellation");
                Check((bool)typeof(PuzzleGameSession).GetField("started", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session), "Start can fall back after cancelled explicit entry");
                Check(session.State == null && !session.CanAcceptInput, "Cancelled entry created a board");
                bool duplicate = false;
                try { await (UniTask)initialize.Invoke(session, new object[] { request, route, 12345, CancellationToken.None }); }
                catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { duplicate = true; }
                catch (InvalidOperationException) { duplicate = true; }
                Check(duplicate, "Duplicate entry accepted");
                File.WriteAllText(Path.Combine(root, "result.txt"), "PASS explicit cancellation propagation; no board; no Start fallback; duplicate rejected");
                exit = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(root, "result.txt"), "FAIL " + error); Debug.LogException(error); }
            finally { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); }
            EditorApplication.Exit(exit);
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
