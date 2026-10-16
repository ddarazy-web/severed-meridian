using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.UIElements;

namespace Products.Editor
{
    public static class BuildToolDebugModeVerification
    {
        public static void Run()
        {
            try
            {
                var snapshot = new BuildToolStateSnapshot();
                foreach (var product in ProductProfiles.Definitions)
                {
                    var definitions = BuildToolOptions.Default.Available(product);
                    Check(definitions.Any(definition => definition.Id == BuildToolDebugMode.Id && !definition.DefaultEnabled), "debug option registered OFF by default: " + product.Name);
                }
                var backend = new MockBuildToolBackend();
                var choices = new Dictionary<string, bool> { [BuildToolDebugMode.Id] = true };
                var runner = BuildToolExecutionVerification.Create(backend); runner.Start(backend.Profile.Guid, false, choices); choices[BuildToolDebugMode.Id] = false;
                for (int i = 0; i < 10 && !runner.Current.IsTerminal; i++) runner.Step();
                Check(runner.Current.phase == BuildToolRunPhase.Succeeded && runner.Current.debugMode == true && runner.Store.Load().debugMode == true,
                    "debug ON is frozen and persisted without profile changes");
                Check(runner.Current.optionMutations.Count == 0 && backend.BuildCalls == 1, "debug mode does not mutate project settings or duplicate build");
                runner = BuildToolExecutionVerification.Create(backend); runner.Start(backend.Profile.Guid, false, choices);
                for (int i = 0; i < 10 && !runner.Current.IsTerminal; i++) runner.Step();
                Check(runner.Current.phase == BuildToolRunPhase.Succeeded && runner.Current.debugMode == false && backend.BuildCalls == 2, "debug OFF after ON has no previous request residue");
                BuildOptions inherited = BuildOptions.CompressWithLz4HC | BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler |
                    BuildOptions.EnableDeepProfilingSupport | BuildOptions.CustomConnectionID | BuildOptions.WaitForPlayerConnection;
                Check(BuildToolDebugMode.Apply(inherited, false) == BuildOptions.CompressWithLz4HC, "OFF clears inherited development debugger and profiler flags while preserving compression");
                Check(BuildToolDebugMode.Apply(BuildOptions.CompressWithLz4HC, true) == (BuildOptions.CompressWithLz4HC | BuildOptions.Development), "ON enables Development without enabling debugger attachment");

                var active = BuildProfile.GetActiveBuildProfile();
                Check(BuildToolDebugMode.ProfileOptionsFactory.ReturnType == typeof(BuildPlayerOptions), "installed Unity profile option API is available with expected signature");
                var info = active != null ? BuildToolProfiles.Read(active, AssetDatabase.GetAssetPath(active), AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(active))) : backend.Profile;
                var request = new BuildToolRequest { profileGuid = info.Guid, target = info.Target, output = Path.GetFullPath(info.Product.Output), debugMode = true };
                if (active != null)
                {
                var on = BuildToolDebugMode.CreateBuildOptions(request); request.debugMode = false; var off = BuildToolDebugMode.CreateBuildOptions(request);
                Check((on.options & BuildOptions.Development) != 0 && (off.options & BuildOptions.Development) == 0,
                    "installed Unity profile calculation produces ON OFF flags without invoking BuildPlayer");
                Check(on.scenes.SequenceEqual(active.scenes.Where(scene => scene.enabled).Select(scene => scene.path)) && on.scenes.SequenceEqual(off.scenes) &&
                    on.target == info.Target && off.target == info.Target && on.locationPathName == request.output && on.subtarget == off.subtarget,
                    "native profile scenes target subtarget and output are preserved");
                }
                else
                {
                    var flagsMethod = BuildToolDebugMode.ProfileOptionsFactory.DeclaringType.GetMethod("GetBuildOptions", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                    var flags = (BuildOptions)flagsMethod.Invoke(null, new object[] { info.Target, BuildPipeline.GetBuildTargetGroup(info.Target), request.output, BuildOptions.None });
                    Check((BuildToolDebugMode.Apply(flags, true) & BuildOptions.Development) != 0 && (BuildToolDebugMode.Apply(flags, false) & BuildOptions.Development) == 0,
                        "native option calculation flags support ON OFF without activating a profile");
                    Debug.Log("미검증: 현재 기본 프로필 상태이므로 활성 제품 기반 씬/서브타깃 계산은 실행하지 않았습니다.");
                }
                request.profileGuid = "not-active";
                BuildToolExecutionVerification.Reject(() => BuildToolDebugMode.CreateBuildOptions(request), "different active profile cannot bypass request binding");
                var window = ScriptableObject.CreateInstance<MatchBuildToolWindow>();
                try
                {
                    window.SelectionPath = Path.Combine(BuildToolExecutionVerification.NewRoot(), "debug-preferences.json"); window.Show(); window.CreateGUI(); window.SelectProfile(info.Guid);
                    var toggle = window.rootVisualElement.Q<Toggle>("option-debug-mode");
                    Check(toggle != null && !toggle.value && toggle.tooltip.Contains("Development Build"), "debug UI exposes default and effect");
                    toggle.value = true;
                    Check(BuildToolPreferences.Choices(window.SelectionPath, info.Guid)[BuildToolDebugMode.Id], "debug UI choice persists per profile");
                    window.rootVisualElement.Q<Toggle>("option-debug-mode").value = false;
                    Check(!BuildToolPreferences.Choices(window.SelectionPath, info.Guid)[BuildToolDebugMode.Id], "debug UI can turn stored ON back OFF");
                }
                finally { UnityEngine.Object.DestroyImmediate(window); }
                snapshot.AssertUnchanged(); Debug.Log("PASS BuildToolDebugModeVerification"); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void Check(bool value, string message) => BuildToolExecutionVerification.Check(value, message);
    }
}
