using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelTool;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolAdvancedVerification
    {
        private static bool previousEnabled;
        private static EnterPlayModeOptions previousOptions;
        private static double deadline;
        private static bool exercised, waitingBotArtwork;
        private static int exitCode;
        private static string recoveryFolder;
        private static int tutorialCheck, artworkFrames;
        private static RenderTexture artworkTexture, previousTexture;
        private static PanelSettings artworkPanel;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void Run()
        { tutorialCheck = 0; RunCore(); }
        public static void RunTutorial()
        { tutorialCheck = 1; RunCore(); }
        public static void RunSharedTutorial()
        { tutorialCheck = 2; RunCore(); }
        public static void RunTutorialSamples()
        { tutorialCheck = 3; RunCore(); }
        public static void RunBot()
        { tutorialCheck = 4; RunCore(); }
        public static void RunMulti()
        { tutorialCheck = 5; RunCore(); }
        public static void RunVisual()
        { tutorialCheck = 6; RunCore(); }
        public static void RunRecords()
        { tutorialCheck = 7; RunCore(); }
        public static void RunHandoff()
        { tutorialCheck = 8; RunCore(); }
        private static void RunCore()
        {
            previousEnabled = EditorSettings.enterPlayModeOptionsEnabled; previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            recoveryFolder = Path.GetFullPath("Logs/GameAuthoringStage05/advanced-" + Guid.NewGuid().ToString("N"));
            LevelToolRecoveryIsolation.Configure(recoveryFolder);
            deadline = EditorApplication.timeSinceStartup + 120; exercised = false; waitingBotArtwork = false; artworkFrames = 0; exitCode = 0;
            EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Changed;
            EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("advanced UI test timeout");
                if (exercised || !EditorApplication.isPlaying) return;
                LevelToolScreen screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                if (screen == null || (bool)typeof(LevelToolScreen).GetField("busy", Flags).GetValue(screen)) return;
                if (waitingBotArtwork)
                {
                    if (tutorialCheck == 6)
                    {
                        if (!LevelToolVisualExercise.Verify(screen)) return;
                        exercised = true; EditorApplication.ExitPlaymode(); return;
                    }
                    if (!LevelToolBotExercise.VerifyArtwork(screen)) return;
                    if (artworkTexture == null)
                    {
                        artworkPanel = screen.GetComponent<UIDocument>().panelSettings;
                        previousTexture = artworkPanel.targetTexture;
                        artworkTexture = new RenderTexture(1280, 800, 0); artworkTexture.Create(); artworkPanel.targetTexture = artworkTexture;
                    }
                    var ui = screen.GetComponent<UIDocument>().rootVisualElement;
                    ui.Q<ScrollView>("board-scroll").ScrollTo(ui.Q("history-trial-board"));
                    if (++artworkFrames < 8) return;
                    if (ui.Q("history-trial-board").worldBound.width < 324) throw new Exception("FAIL replay board columns clipped by narrow panel");
                    var toolbar = (VisualElement)typeof(LevelToolScreen).GetField("toolbar", Flags).GetValue(screen);
                    var body = (VisualElement)typeof(LevelToolScreen).GetField("body", Flags).GetValue(screen);
                    if (toolbar.Query<Button>().ToList().Max(button => button.worldBound.yMax) > body.worldBound.yMin) throw new Exception("FAIL toolbar buttons overlap board area");
                    RenderTexture previous = RenderTexture.active;
                    var image = new Texture2D(1280, 800, TextureFormat.RGBA32, false);
                    try
                    {
                        RenderTexture.active = artworkTexture; image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply();
                        File.WriteAllBytes("Logs/GameAuthoringStage05/trial-board-screen.png", image.EncodeToPNG());
                    }
                    finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
                    exercised = true; EditorApplication.ExitPlaymode(); return;
                }
                LevelToolRecoveryIsolation.Verify(screen, recoveryFolder);
                screen.Workspace.Open(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt"), "Cancel");
                screen.Workspace.Session.SelectLevel(screen.Workspace.Session.Documents.First(doc => doc.Kind == "level" && (int)doc.Data["levelNumber"] == 2).Id);
                typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
                if (tutorialCheck > 0)
                {
                    if (tutorialCheck == 8) LevelToolHandoffExercise.Run(screen);
                    else if (tutorialCheck == 7) LevelToolRecordsExercise.Run(screen);
                    else if (tutorialCheck == 6) { LevelToolVisualExercise.Prepare(screen); waitingBotArtwork = true; return; }
                    else if (tutorialCheck == 5) LevelToolMultiExercise.Run(screen);
                    else if (tutorialCheck == 4) { LevelToolBotExercise.Run(screen); waitingBotArtwork = true; return; }
                    else if (tutorialCheck == 3) LevelToolTutorialSamplesExercise.Run(screen);
                    else if (tutorialCheck == 2) LevelToolSharedTutorialExercise.Run(screen);
                    else LevelToolTutorialExercise.Run(screen);
                    exercised = true; EditorApplication.ExitPlaymode(); return;
                }
                LevelToolSupplyExercise.Run(screen);
                LevelToolFlowExercise.Run(screen);
                LevelToolConnectionExercise.Run(screen);
                LevelToolShapeExercise.Run(screen);
                var host = new UnityEngine.UIElements.VisualElement(); string chosen = null;
                screen.GetComponent<UIDocument>().rootVisualElement.Add(host);
                typeof(LevelToolScreen).GetMethod("Choice", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[]
                { host, "duplicate-choice", "중복 이름", new[] { "one", "two" }, new[] { "같은 이름", "같은 이름" }, "one", (Action<string>)(value => chosen = value) });
                var choice = host.Q<UnityEngine.UIElements.DropdownField>("duplicate-choice");
                if (choice.choices.Distinct().Count() != 2) throw new Exception("FAIL duplicate display names cannot be distinguished");
                choice.value = choice.choices[1];
                if (chosen != "two") throw new Exception("FAIL duplicate name selects wrong ID");
                host.RemoveFromHierarchy();
                typeof(LevelToolScreen).GetField("flowTool", Flags).SetValue(screen, "path");
                var route = (System.Collections.Generic.List<int>)typeof(LevelToolScreen).GetField("flowRoute", Flags).GetValue(screen);
                route.Add(20);
                typeof(LevelToolScreen).GetField("wireDrawing", Flags).SetValue(screen, true);
                var wire = (System.Collections.Generic.List<int>)typeof(LevelToolScreen).GetField("wireRoute", Flags).GetValue(screen);
                wire.Add(24);
                Button create = screen.GetComponent<UIDocument>().rootVisualElement.Q<Button>("new-level");
                typeof(Clickable).GetMethod("Invoke", Flags).Invoke(create.clickable, new object[] { null });
                if (route.Count != 0 || wire.Count != 0 || (bool)typeof(LevelToolScreen).GetField("wireDrawing", Flags).GetValue(screen) ||
                    typeof(LevelToolScreen).GetField("flowTool", Flags).GetValue(screen) != null)
                    throw new Exception("FAIL unfinished input leaked into another level");
                exercised = true; EditorApplication.ExitPlaymode();
            }
            catch (Exception error) { Debug.LogException(error); exitCode = 1; exercised = true; EditorApplication.ExitPlaymode(); }
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            if (artworkPanel != null) artworkPanel.targetTexture = previousTexture;
            if (artworkTexture != null) { artworkTexture.Release(); UnityEngine.Object.DestroyImmediate(artworkTexture); artworkTexture = null; }
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Changed;
            EditorSettings.enterPlayModeOptionsEnabled = previousEnabled; EditorSettings.enterPlayModeOptions = previousOptions;
            EditorApplication.Exit(exitCode);
        }
    }
}
