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
    public static class BuildToolAndroidFormatVerification
    {
        public static void Run()
        {
            var original = new BuildToolStateSnapshot();
            string copy = "Assets/Settings/BuildProfiles/__FormatVerification-" + Guid.NewGuid().ToString("N") + ".asset";
            string preferences = Path.Combine(BuildToolExecutionVerification.NewRoot(), "preferences.json");
            MatchBuildToolWindow window = null;
            Exception failure = null;
            try
            {
                var sameOutput = typeof(UnityProfileBuildBackend).GetMethod("SameOutput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                BuildToolPreflightVerification.Check((bool)sameOutput.Invoke(null, new object[] { Path.GetFullPath("Builds/Android/Game.apk"), "Builds/Android/Game.apk" }),
                    "default APK path recognized after profile switch with AAB requested");
                foreach (var product in ProductProfiles.Definitions)
                {
                    var defaults = BuildToolOptions.Default.Resolve(product, null);
                    BuildToolPreflightVerification.Check(defaults.Any(option => option.id == BuildToolAndroidBundle.Id) == (product.Target == BuildTarget.Android), "format option only registered for Android: " + product.Name);
                    if (product.Target != BuildTarget.Android) continue;
                    var backend = new MockBuildToolBackend { Profile = BuildToolProfiles.Discover().Single(item => item.Path == product.ProfilePath) };
                    var store = new BuildToolRunStore(BuildToolExecutionVerification.NewRoot());
                    var runner = new BuildToolRunner(backend, store, "format-fixture");
                    runner.Start(backend.Profile.Guid, false, new Dictionary<string, bool> { [BuildToolAndroidBundle.Id] = true });
                    var savedVersion = BuildToolVersionSettings.Read(backend.Profile);
                    string expectedOutput = product.Symbol == "PRODUCT_ANDROID_GAME"
                        ? Path.Combine(Path.GetDirectoryName(product.Output), $"moon_rabbit_junk_{savedVersion.Name}_{savedVersion.Code}.aab")
                        : Path.Combine(Path.GetDirectoryName(product.Output), $"aos_moonrabbit_level_tool_{savedVersion.Name}_{savedVersion.Code}.aab");
                    BuildToolPreflightVerification.Check(runner.Current.output == Path.GetFullPath(expectedOutput), "AAB output frozen before platform selection: " + product.Name);
                    runner = new BuildToolRunner(backend, store, "format-fixture");
                    backend.BeforeBuild = request => BuildToolPreflightVerification.Check(request.androidAppBundle == true && request.output.EndsWith(".aab"), "reloaded request passes AAB to backend");
                    for (int i = 0; i < 12 && !runner.Current.IsTerminal; i++) runner.Step();
                    BuildToolPreflightVerification.Check(runner.Current.phase == BuildToolRunPhase.Succeeded && backend.BuildCalls == 1, "AAB mock lifecycle completes once");
                    backend.BeforeBuild = request => BuildToolPreflightVerification.Check(request.androidAppBundle == false && request.output.EndsWith(".apk"), "next APK request has no AAB residue");
                    runner.Start(backend.Profile.Guid, false, null);
                    BuildToolPreflightVerification.Check(runner.Current.output == Path.GetFullPath(Path.ChangeExtension(expectedOutput, ".apk")), "APK uses same versioned filename: " + product.Name);
                    for (int i = 0; i < 12 && !runner.Current.IsTerminal; i++) runner.Step();
                    BuildToolPreflightVerification.Check(runner.Current.phase == BuildToolRunPhase.Succeeded, "APK mock lifecycle completes");
                }
                if (!AssetDatabase.CopyAsset(ProductProfiles.Definitions[0].ProfilePath, copy)) throw new Exception("fixture profile copy failed");
                var native = new UnityProfileBuildBackend();
                string guid = AssetDatabase.AssetPathToGUID(copy);
                string fingerprint = native.Fingerprint(native.Read(guid));
                var nativeStore = new BuildToolRunStore(BuildToolExecutionVerification.NewRoot());
                var nativeRequest = new BuildToolRequest { id = Guid.NewGuid().ToString("N"), session = "format-fixture", profileGuid = guid, profilePath = copy,
                    startedUtc = DateTime.UtcNow.ToString("O"), phase = BuildToolRunPhase.Building, androidAppBundle = true };
                BuildToolAndroidBundle.Apply(nativeRequest, nativeStore);
                var data = new SerializedObject(AssetDatabase.LoadAssetAtPath<BuildProfile>(copy));
                BuildToolPreflightVerification.Check(data.FindProperty("m_PlatformBuildProfile").FindPropertyRelative("m_BuildAppBundle").boolValue, "native Android profile enables AAB for build");
                BuildToolAndroidBundle.Restore(nativeStore.Load(), nativeStore);
                BuildToolPreflightVerification.Check(native.Fingerprint(native.Read(guid)) == fingerprint && !nativeStore.Load().androidAppBundleBefore.HasValue,
                    "journal-based recovery restores original profile and clears mutation");
                nativeRequest = nativeStore.Load(); nativeRequest.androidAppBundle = false;
                BuildToolAndroidBundle.Apply(nativeRequest, nativeStore);
                BuildToolAndroidBundle.Restore(nativeRequest, nativeStore);
                BuildToolPreflightVerification.Check(native.Fingerprint(native.Read(guid)) == fingerprint, "APK apply and restore preserve profile fingerprint");
                AssetDatabase.DeleteAsset(copy);
                window = ScriptableObject.CreateInstance<MatchBuildToolWindow>(); window.SelectionPath = preferences; window.Show(); window.CreateGUI();
                string android = AssetDatabase.AssetPathToGUID(ProductProfiles.Definitions[0].ProfilePath);
                window.SelectProfile(android);
                var dropdown = window.rootVisualElement.Q<DropdownField>("option-" + BuildToolAndroidBundle.Id);
                BuildToolPreflightVerification.Check(dropdown.value == "APK", "Android format UI defaults to APK"); dropdown.value = "AAB";
                var version = BuildToolVersionSettings.Read(native.Read(android));
                string expectedName = $"moon_rabbit_junk_{version.Name}_{version.Code}";
                BuildToolPreflightVerification.Check(Path.GetFileName(BuildToolAndroidBundle.Output(ProductProfiles.Definitions[0], null)) == expectedName + ".apk",
                    "APK filename uses product profile version and code");
                BuildToolPreflightVerification.Check(window.rootVisualElement.Q<Label>("profile-information").text.Contains(expectedName + ".aab"), "AAB selection updates versioned output immediately");
                window.Close(); window = ScriptableObject.CreateInstance<MatchBuildToolWindow>(); window.SelectionPath = preferences; window.Show(); window.CreateGUI();
                BuildToolPreflightVerification.Check(window.rootVisualElement.Q<DropdownField>("option-" + BuildToolAndroidBundle.Id).value == "AAB", "format selection survives reopening");
                window.SelectProfile(AssetDatabase.AssetPathToGUID(ProductProfiles.Definitions.Last().ProfilePath));
                BuildToolPreflightVerification.Check(window.rootVisualElement.Q<DropdownField>("option-" + BuildToolAndroidBundle.Id).value == "APK", "other Android product keeps its own default");
                window.SelectProfile(AssetDatabase.AssetPathToGUID(ProductProfiles.Definitions[2].ProfilePath));
                BuildToolPreflightVerification.Check(window.rootVisualElement.Q("option-" + BuildToolAndroidBundle.Id) == null, "Windows has no APK AAB selection");
            }
            catch (Exception error) { failure = error; }
            finally
            {
                if (window != null) window.Close();
                AssetDatabase.DeleteAsset(copy);
                if (File.Exists(preferences)) File.Delete(preferences);
                foreach (string dimension in new[] { "Width", "Height", "X", "Y" })
                    EditorPrefs.DeleteKey("Match.BuildTool.WindowSize." + Path.GetFullPath(preferences) + "." + dimension);
            }
            try { original.AssertUnchanged(); } catch (Exception error) { failure = failure ?? error; }
            if (failure == null) Debug.Log("PASS BuildToolAndroidFormatVerification (no Player build)"); else Debug.LogException(failure);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }
}
