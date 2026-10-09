using System;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using LevelTool;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class PackedToolPlayVerification
    {
        private static bool enabled, started;
        private static EnterPlayModeOptions options;
        private static int builder, exit;
        private static double deadline;
        public static void Run()
        {
            enabled = EditorSettings.enterPlayModeOptionsEnabled; options = EditorSettings.enterPlayModeOptions;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            builder = settings.ActivePlayModeDataBuilderIndex;
            int fast = settings.DataBuilders.FindIndex(value => value is BuildScriptFastMode);
            if (fast < 0) throw new Exception("Fast Play Mode builder missing");
            settings.ActivePlayModeDataBuilderIndex = fast;
            EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            LevelToolRecoveryIsolation.Configure(System.IO.Path.GetFullPath("Logs/GameAuthoringStage06/packed-recovery-" + Guid.NewGuid().ToString("N")));
            started = false; exit = 0; deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += Tick; EditorApplication.playModeStateChanged += State;
            LevelTool.Editor.LevelToolLauncher.Launch();
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > deadline) { exit = 1; Debug.LogError("packed tool test timed out"); EditorApplication.ExitPlaymode(); return; }
            if (!EditorApplication.isPlaying || started) return;
            var screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
            if (screen == null || (bool)typeof(LevelToolScreen).GetField("busy", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(screen)) return;
            started = true; Exercise(screen).Forget(error => { Debug.LogException(error); exit = 1; EditorApplication.ExitPlaymode(); });
        }
        private static async UniTask Exercise(LevelToolScreen screen)
        {
            var prepare = typeof(LevelTool.Editor.LevelToolLauncher).GetMethod("CreateDefaultWorkspace", BindingFlags.Static | BindingFlags.NonPublic);
            if (prepare == null) throw new Exception("FAIL selected default workspace handoff missing");
            string defaultState = (string)prepare.Invoke(null, null);
            var offer = typeof(LevelToolScreen).GetMethod("OfferDefaultWorkspace");
            if (offer == null) throw new Exception("FAIL safe default workspace offer missing");
            offer.Invoke(screen, new object[] { defaultState });
            if (screen.Workspace.Session == null || screen.Workspace.Folder != AuthoringSourceSelection.ReadFolder(AuthoringSourceSelection.DefaultConfiguration))
                throw new Exception("FAIL empty tool did not open selected default");
            typeof(LevelToolScreen).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, null);
            var root = screen.GetComponent<UIDocument>().rootVisualElement;
            var source = root.Q<DropdownField>("trial-source");
            if (source == null) throw new Exception("FAIL tool trial source selector missing");
            source.value = "생성된 MemoryPack";
            await (UniTask)typeof(LevelToolScreen).GetMethod("CheckPackStatus").Invoke(screen, null);
            if (!root.Q<Label>("trial-pack-status").text.Contains("일치")) throw new Exception("FAIL current pack status");
            var session = screen.Workspace.Session;
            session.Apply("미저장 팩 검사", data => data[session.SelectedLevelId].Data["moveCount"] = (int)data[session.SelectedLevelId].Data["moveCount"] + 1);
            typeof(LevelToolScreen).GetMethod("Refresh", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, null);
            if (!root.Q<Label>("trial-pack-status").text.Contains("다릅니다")) throw new Exception("FAIL stale pack status");
            string state = session.ExportState();
            offer.Invoke(screen, new object[] { defaultState });
            if (session.ExportState() != state || !root.Q<Button>("open-incoming-workspace").text.Contains("기본")) throw new Exception("FAIL default offer overwrote dirty work");
            await screen.BeginPlay();
            if (screen.TestSession == null || !screen.TestSession.IsReady || !screen.TestSession.PlayContext.IsTest)
                throw new Exception("FAIL packed game test startup");
            screen.ReturnToEditor();
            if (session.ExportState() != state) throw new Exception("FAIL packed trial changed draft");
            Debug.Log("PASS actual Addressables fast-play packed trial, stale label, test context and draft preservation");
            EditorApplication.ExitPlaymode();
        }
        private static void State(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= State;
            AddressableAssetSettingsDefaultObject.Settings.ActivePlayModeDataBuilderIndex = builder;
            EditorSettings.enterPlayModeOptionsEnabled = enabled; EditorSettings.enterPlayModeOptions = options;
            EditorApplication.Exit(exit);
        }
    }
}




