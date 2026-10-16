using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Products.Editor
{
    public static class BuildToolPreflightVerification
    {
        public static void Run()
        {
            try
            {
                var before = new BuildToolStateSnapshot();
                var deferred = new BuildToolReport();
                deferred.Issues.Add(new BuildToolIssue(BuildToolIssueLevel.Unknown, "에디터 상태", "검사 보류", "나중에 검사"));
                Check(!deferred.Summary.Contains("완료") && deferred.Summary.Contains("보류"), "deferred report must not claim completed inspection");
                var profiles = BuildToolProfiles.Discover();
                var guids = ProductProfiles.Definitions.Select(product => AssetDatabase.AssetPathToGUID(product.ProfilePath)).ToArray();
                Func<string, BuildProfile> loader = path => path == ProductProfiles.Definitions[0].ProfilePath
                    ? throw new IOException("simulated asset load failure") : AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
                var discover = typeof(BuildToolProfiles).GetMethod("Discover", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { typeof(System.Collections.Generic.IEnumerable<string>), typeof(Func<string, BuildProfile>) }, null);
                Check(discover != null, "asset read boundary can exercise loader failure");
                var isolated = (System.Collections.Generic.List<BuildToolProfileInfo>)discover.Invoke(null, new object[] { guids, loader });
                Check(isolated.Count == guids.Length && isolated.Single(item => item.Guid == guids[0]).ReadIssues.Any(issue => issue.Message.Contains("simulated")) &&
                    isolated.Where(item => item.Guid != guids[0]).All(item => item.ReadIssues.Count == 0), "one loader exception preserves healthy profile list");
                foreach (var product in ProductProfiles.Definitions)
                {
                    var item = profiles.Single(value => value.Path == product.ProfilePath);
                    Check(item.Guid == AssetDatabase.AssetPathToGUID(product.ProfilePath) && item.Product == product, "GUID and product " + product.Name);
                    Check(item.ReadIssues.Count == 0 && item.ApplicationId == product.ApplicationId && item.ProductName == product.ProductName,
                        "actual settings read without repair " + product.Name);
                    Check(!item.IsCopy && item.Scenes.SequenceEqual(new[] { product.Scene }), "scene and original profile " + product.Name);
                    var result = BuildToolPreflight.Inspect(item);
                    Check(result.Issues.Any(issue => issue.Level == BuildToolIssueLevel.Unknown) && result.Summary.Contains("미검증"), "never claims a build passed " + product.Name);
                    bool supported = BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(item.Target), item.Target);
                    Check(result.Issues.Any(issue => issue.Location == "플랫폼 모듈" && issue.Level == (supported ? BuildToolIssueLevel.Pass : BuildToolIssueLevel.Error)), "module state " + product.Name);
                    if (item.Target != EditorUserBuildSettings.activeBuildTarget)
                        Check(result.Issues.Any(issue => issue.Location == "활성 플랫폼" && issue.Level == BuildToolIssueLevel.Unknown), "inactive target is deferred, not invalid");
                }
                Check(BuildToolPreflight.Inspect(null).HasErrors, "no selection rejected");
                var unreadable = BuildToolProfiles.Read(null, "missing.asset", "missing");
                Check(unreadable.ReadIssues.Count > 0 && unreadable.Target == BuildTarget.NoTarget, "unreadable profile isolated without guessed target");
                var original = profiles.Single(item => item.Product?.Name == "Android Game" && !item.IsCopy);
                foreach (string[] symbols in new[] { new[] { "PRODUCT_UNKNOWN" }, new[] { "PRODUCT_IOS_GAME" },
                    new[] { "PRODUCT_ANDROID_GAME", "PRODUCT_LEVEL_EDITOR" }, Array.Empty<string>() })
                {
                    var clone = UnityEngine.Object.Instantiate(original.Profile);
                    try
                    {
                        clone.scriptingDefines = symbols;
                        var item = BuildToolProfiles.Read(clone, "memory/invalid.asset", "invalid");
                        Check(item.Product == null && BuildToolPreflight.Inspect(item).HasErrors, "invalid product symbols " + string.Join(",", symbols));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(clone); }
                }
                var wrongScene = UnityEngine.Object.Instantiate(original.Profile);
                try
                {
                    wrongScene.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/LevelTool.unity", true) };
                    Check(BuildToolPreflight.Inspect(BuildToolProfiles.Read(wrongScene, "memory/scene.asset", "scene")).HasErrors, "wrong scene rejected");
                }
                finally { UnityEngine.Object.DestroyImmediate(wrongScene); }
                var emptySettings = ScriptableObject.CreateInstance<BuildProfile>();
                try
                {
                    var data = new SerializedObject(emptySettings); data.FindProperty("m_BuildTarget").intValue = (int)BuildTarget.Android; data.ApplyModifiedPropertiesWithoutUndo();
                    emptySettings.scriptingDefines = new[] { original.Product.Symbol }; emptySettings.overrideGlobalScenes = true;
                    emptySettings.scenes = new[] { new EditorBuildSettingsScene(original.Product.Scene, true) };
                    Check(BuildToolProfiles.Read(emptySettings, "memory/no-settings.asset", "no-settings").ReadIssues.Count > 0, "missing settings not generated");
                    Check(typeof(BuildProfile).GetProperty("playerSettings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).GetValue(emptySettings) == null,
                        "inspection leaves missing player settings absent");
                }
                finally { UnityEngine.Object.DestroyImmediate(emptySettings); }
                var mismatch = BuildToolProfiles.Read(original.Profile, original.Path, original.Guid); mismatch.ApplicationId = "invalid.identifier";
                Check(BuildToolPreflight.Inspect(mismatch).Issues.Any(issue => issue.Level == BuildToolIssueLevel.Error && issue.Message.Contains("식별자")), "identifier mismatch rejected");
                var copied = BuildToolProfiles.Read(original.Profile, "Assets/copied.asset", "copy-guid");
                Check(copied.IsCopy && BuildToolPreflight.Inspect(copied).Issues.Any(issue => issue.Level == BuildToolIssueLevel.Warning), "copy is distinct and explained");
                Check(BuildToolPreflight.Inspect(original, new BuildToolPlatforms()).Issues.Any(issue => issue.Location == "플랫폼 등록" && issue.Level == BuildToolIssueLevel.Error), "unregistered handler rejected");
                var duplicate = new BuildToolPlatforms(new AndroidBuildPlatform(), new AndroidBuildPlatform());
                Check(BuildToolPreflight.Inspect(original, duplicate).Issues.Any(issue => issue.Message.Contains("중복")), "duplicate handler rejected");
                BuildToolPlatforms.Default.TryGet(BuildTarget.Android, out var android, out _);
                BuildToolPlatforms.Default.TryGet(BuildTarget.StandaloneWindows64, out var windows, out _);
                Check(android is AndroidBuildPlatform && windows is WindowsBuildPlatform, "platform dispatch registered");
                foreach (var product in ProductProfiles.Definitions)
                    Check(BuildToolPlatforms.Default.TryGet(product.Target, out var handler, out _) && ReferenceEquals(handler, product.Target == BuildTarget.Android ? android : product.Target == BuildTarget.StandaloneWindows64 ? windows : GetIos()), "products share platform handler " + product.Name);
                before.AssertUnchanged();
                Debug.Log("PASS BuildToolPreflightVerification");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static IBuildToolPlatform GetIos() { BuildToolPlatforms.Default.TryGet(BuildTarget.iOS, out var value, out _); return value; }
        internal static void Check(bool value, string message)
        {
            if (!value) throw new Exception(message);
            Debug.Log("PASS " + message);
        }
    }

    internal sealed class BuildToolStateSnapshot
    {
        private readonly string state;
        private readonly Dictionary<string, string> files;
        internal BuildToolStateSnapshot() { state = CaptureState(); files = CaptureFiles(); }
        internal void AssertUnchanged()
        {
            BuildToolPreflightVerification.Check(state == CaptureState(), "active profile, target, memory settings, outputs and content routing unchanged");
            var after = CaptureFiles();
            BuildToolPreflightVerification.Check(files.Count == after.Count && files.All(pair => after.TryGetValue(pair.Key, out string hash) && hash == pair.Value), "profile, global settings and Addressables files unchanged");
        }
        private static string CaptureState()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var active = BuildProfile.GetActiveBuildProfile();
            var values = new List<string> { active == null ? "none" : AssetDatabase.GetAssetPath(active), EditorUserBuildSettings.activeBuildTarget.ToString(),
                settings.activeProfileId, Addressables.LibraryPath, Addressables.BuildReportPath, settings.ContentStateBuildPath, EditorJsonUtility.ToJson(settings) };
            foreach (var definition in ProductProfiles.Definitions)
            {
                var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(definition.ProfilePath);
                values.Add(EditorJsonUtility.ToJson(profile)); values.Add(EditorJsonUtility.ToJson(ProductProfiles.ProfileSettings(profile, false)));
                values.Add(EditorUserBuildSettings.GetBuildLocation(definition.Target));
                values.Add(PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(definition.Target))));
            }
            var schema = settings.FindGroup(global::LevelTool.Editor.LevelToolAtlasBuild.GroupName).GetSchema<BundledAssetGroupSchema>();
            values.Add(EditorJsonUtility.ToJson(schema));
            return string.Join("\n", values);
        }
        internal static Dictionary<string, string> CaptureFiles()
        {
            var paths = Directory.GetFiles("Assets/Settings/BuildProfiles", "*", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets/AddressableAssetsData", "*", SearchOption.AllDirectories))
                .Concat(new[] { "ProjectSettings/ProjectSettings.asset", "ProjectSettings/EditorBuildSettings.asset" });
            using (var hash = System.Security.Cryptography.SHA256.Create())
                return paths.ToDictionary(path => path, path => Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path))));
        }
    }
}
