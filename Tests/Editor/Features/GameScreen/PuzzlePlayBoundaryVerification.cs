using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GameScreen.Editor
{
    [InitializeOnLoad]
    public static partial class PuzzlePlayBoundaryVerification
    {
        private const string Key = "Products.Stage01.Lifecycle";
        private const string Output = "Logs/GameAuthoringStage01";
        static PuzzlePlayBoundaryVerification()
        {
            EditorApplication.update += () =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                const string path = "Logs/PuzzleEditorLaunchVerification/lifecycle-results.txt";
                string result = File.Exists(path) ? File.ReadAllText(path) : "";
                bool failed = result.Contains("FAIL");
                if (!failed && !result.Contains("PASS LifecycleSuiteComplete")) return;
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (failed && SessionState.GetInt("StageThree.LifecycleTest.phase", 0) != 0) return;
                SessionState.SetBool(Key, false);
                Directory.CreateDirectory(Output);
                File.WriteAllText(Output + "/" + SessionState.GetString(Key + ".name", "lifecycle") + ".txt", result);
                EditorApplication.Exit(failed ? 1 : 0);
            };
        }

        public static void RunBaseline()
        {
            SessionState.SetString(Key + ".name", "baseline-lifecycle");
            SessionState.SetBool(Key, true);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath);
            PuzzleEditorLaunchLifecycleVerification.Run();
        }

        public static void RunCancellation()
        {
            SessionState.SetString(Key + ".name", "cancellation-lifecycle");
            SessionState.SetBool(Key, true);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath);
            PuzzleEditorLaunchLifecycleVerification.RunEarlyExit();
        }
        public static void RunLifecycle()
        {
            SessionState.SetString(Key + ".name", "final-lifecycle");
            SessionState.SetBool(Key, true);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(PuzzleGameAssets.ScenePath);
            PuzzleEditorLaunchLifecycleVerification.Run();
        }

        public static void Run()
        {
            Directory.CreateDirectory(Output);
            try
            {
                Assembly runtime = typeof(PuzzleGameSession).Assembly;
                Type request = runtime.GetType("GameScreen.PuzzlePlayRequest");
                Type context = runtime.GetType("GameScreen.PuzzlePlayContext");
                if (request == null || context == null)
                    throw new Exception("공통 Runtime 실행 요청/문맥이 없어 독립 제작 도구가 같은 계약으로 실행할 수 없습니다.");
                File.WriteAllText(Output + "/boundary-results.txt", "PASS Runtime contract exists\n");
                VerifyRequestAndPolicy();
                StartSessionVerification();
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/boundary-results.txt", "FAIL " + error);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            File.AppendAllText(Output + "/boundary-results.txt", "PASS " + name + "\n");
        }

        private static void VerifyRequestAndPolicy()
        {
            Levels.LevelDefinition source = Tutorial.Editor.LevelTutorialDataVerification.Fixture(991700);
            source.Tutorial.flow = null; source.Tutorial.steps.Clear();
            Levels.LevelDefinition copy = null;
            try
            {
                UnityEngine.JsonUtility.FromJsonOverwrite("{\"moveCount\":37}", source);
                Elements.ElementVisualCatalogDto visuals = new Elements.ElementVisualCatalogDto
                {
                    definitions = new[] { new Elements.ElementVisualDefinitionDto { key = "probe",
                        states = new[] { new Elements.ElementVisualFrameDto { path = "original", size = 1.25f } } } }
                };
                byte[] bytes = Levels.LevelPackCodec.Snapshot(source);
                PuzzlePlayRequest request = new PuzzlePlayRequest(bytes, source.LevelNumber, 8765, visuals);
                Array.Clear(bytes, 0, bytes.Length);
                visuals.definitions[0].states[0].path = "changed";
                UnityEngine.JsonUtility.FromJsonOverwrite("{\"moveCount\":49}", source);
                copy = request.CreateDefinition();
                Check(copy.MoveCount == 37 && request.Seed == 8765, "request freezes source bytes and seed");
                Check(!ReferenceEquals(copy.Board, source.Board), "request definition owns collections");
                Elements.ElementVisualCatalogDto first = request.CreateVisuals();
                Check(first.definitions[0].states[0].path == "original", "request freezes recursive visual DTO");
                first.definitions[0].states[0].size = 99;
                Check(request.CreateVisuals().definitions[0].states[0].size == 1.25f, "visual reads return independent copies");
                Check(source.MoveCount == 49, "request never owns authoring SO");

                source.Tutorial.steps.Add(new Tutorial.TutorialStepDefinition { instructions = "확인" });
                source.Tutorial.completionId = "stage01-policy-" + Guid.NewGuid().ToString("N");
                PuzzlePlayContext automatic = PuzzlePlayContext.CreateTest(Tutorial.TutorialRunMode.Automatic);
                Check(automatic.IsTest && !automatic.AllowLevelAdvance && automatic.Tutorial.ShouldRun(source), "automatic test starts unseen tutorial");
                automatic.Tutorial.Complete(source.LevelNumber, source.Tutorial);
                Check(!automatic.Tutorial.ShouldRun(source), "automatic test remembers own completion");
                Check(PuzzlePlayContext.CreateTest().Tutorial.ShouldRun(source), "new test context does not share completion");
                PuzzlePlayContext always = PuzzlePlayContext.CreateTest(Tutorial.TutorialRunMode.Always, true);
                always.Tutorial.Complete(source.LevelNumber, source.Tutorial);
                Check(always.AllowLevelAdvance && always.Tutorial.ShouldRun(source), "always mode and explicit pack navigation");
                Check(!PuzzlePlayContext.CreateTest(Tutorial.TutorialRunMode.Never).Tutorial.ShouldRun(source), "never mode preserved");
                bool rejected = false;
                try { PuzzlePlayContext.CreateTest(Tutorial.TutorialExecutionContext.CreatePlayer()); }
                catch (ArgumentException) { rejected = true; }
                Check(rejected, "test context rejects formal PlayerPrefs provider");
                PuzzlePlayContext player = PuzzlePlayContext.CreatePlayer();
                Check(!player.IsTest && player.AllowLevelAdvance, "formal policy remains explicit");
                string formalId = "stage01-formal-" + Guid.NewGuid().ToString("N");
                string formalKey = "MoonRabbit.Tutorial.Identity." + formalId;
                source.Tutorial.completionId = formalId;
                try
                {
                    Check(player.Tutorial.ShouldRun(source), "formal unseen tutorial starts");
                    player.Tutorial.Complete(source.LevelNumber, source.Tutorial);
                    Check(PlayerPrefs.GetInt(formalKey, 0) == 1 && !player.Tutorial.ShouldRun(source), "formal tutorial persists completion");
                }
                finally { PlayerPrefs.DeleteKey(formalKey); PlayerPrefs.Save(); }
                int migrated = 0;
                source.Tutorial.previousLevelNumbers = new System.Collections.Generic.List<int> { 42 };
                PuzzlePlayContext callbacks = PuzzlePlayContext.CreateTest(Tutorial.TutorialExecutionContext.CreateEditorWithIdentity(
                    Tutorial.TutorialRunMode.Automatic, number => number == 42, number => throw new Exception("unexpected level write"),
                    id => false, id => migrated++));
                Check(!callbacks.Tutorial.ShouldRun(source) && migrated == 1, "learning identity migration uses injected test storage only");
            }
            finally
            {
                if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
    }
}
