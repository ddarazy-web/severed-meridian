using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.UIElements;

namespace Products.Editor
{
    public static class BuildToolWindowVerification
    {
        private static MatchBuildToolWindow window;
        private static BuildToolStateSnapshot original, withCopy;
        private static string copyPath, selectionPath, copyGuid, selected;
        private static int phase;
        private static Rect lastWindowBounds;
        private static double next, deadline;
        private const string ReloadJob = "Match.BuildTool.WindowVerification.Reload";

        [InitializeOnLoadMethod]
        private static void ResumeReloadVerification()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(ReloadJob, "")))
            {
                deadline = EditorApplication.timeSinceStartup + 40;
                EditorApplication.update += VerifyReload;
            }
        }

        public static void Run()
        {
            deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("window verification timeout");
                if (EditorApplication.isUpdating || EditorApplication.isCompiling) return;
                switch (phase)
                {
                    case 0:
                        original = new BuildToolStateSnapshot();
                        copyPath = "Assets/Settings/BuildProfiles/__BuildToolVerification-" + Guid.NewGuid().ToString("N") + ".asset";
                        selectionPath = "Logs/BuildToolStage01/selection-" + Guid.NewGuid().ToString("N") + ".json";
                        Directory.CreateDirectory(Path.GetDirectoryName(selectionPath)); File.WriteAllText(selectionPath, "not-json");
                        BuildToolPreflightVerification.Check(AssetDatabase.CopyAsset(ProductProfiles.Definitions[0].ProfilePath, copyPath), "owned temporary profile copied");
                        copyGuid = AssetDatabase.AssetPathToGUID(copyPath); withCopy = new BuildToolStateSnapshot();
                        window = ScriptableObject.CreateInstance<MatchBuildToolWindow>(); window.SelectionPath = selectionPath;
                        window.RestoreWindowBounds();
                        BuildToolPreflightVerification.Check(window.position.size == new Vector2(1040, 760), "new window uses readable default size");
                        window.Show(); window.CreateGUI(); window.position = new Rect(40, 40, 1040, 760);
                        Next(); break;
                    case 1:
                        MatchBuildToolWindow.Open(); MatchBuildToolWindow.Open();
                        BuildToolPreflightVerification.Check(Resources.FindObjectsOfTypeAll<MatchBuildToolWindow>().Length == 1, "menu reuses same window");
                        var menu = typeof(MatchBuildToolWindow).GetMethod("Open").GetCustomAttributes(typeof(MenuItem), false).Cast<MenuItem>().Single();
                        BuildToolPreflightVerification.Check(menu.menuItem == "Match/Build/빌드 도구", "exact menu path");
                        var root = window.rootVisualElement;
                        BuildToolPreflightVerification.Check(root.Q<Button>("build-product").text.Contains("빌드") && root.Q<Button>("build-product").tooltip.Contains("확인"), "build requires explicit confirmation");
                        BuildToolPreflightVerification.Check(root.Q<Label>("build-tool-notice").text.Contains("이전 선택"), "invalid local preference explained");
                        selected = AssetDatabase.AssetPathToGUID(ProductProfiles.Definitions.Last().ProfilePath);
                        window.SelectProfile(selected); window.RunPreflight();
                        BuildToolPreflightVerification.Check(root.Q<Toggle>("option-debug-mode") != null, "registered debug option appears after valid profile selection");
                        BuildToolPreflightVerification.Check(new[] { "profiles-section", "product-section", "options-section", "checks-section", "execution-section" }
                            .All(name => root.Q(name) != null), "five clearly named workflow sections are present");
                        BuildToolPreflightVerification.Check(root.ClassListContains("build-tool") && root.styleSheets.count > 0 && root.Q<Button>("profile-" + selected).ClassListContains("selected"),
                            "theme stylesheet and explicit selected profile styling applied");
                        BuildToolPreflightVerification.Check(new[] { "refresh-profiles", "inspect-profile", "build-product", "cancel-build", "resolve-build", "open-build-log", "open-build-output" }
                            .All(name => !string.IsNullOrWhiteSpace(root.Q<Button>(name).tooltip)), "workflow actions explain their purpose with tooltips");
                        BuildToolPreflightVerification.Check(root.Q<Label>("execution-headline").text.Contains("대기") && !root.Q<Button>("open-build-output").enabledSelf,
                            "empty execution state is clear and unavailable output stays disabled");
                        BuildToolPreflightVerification.Check(root.Q<Label>("profile-information").text.Contains(ProductProfiles.Definitions.Last().ApplicationId), "selected actual profile detail shown");
                        BuildToolPreflightVerification.Check(File.ReadAllText(selectionPath).Contains(selected), "last GUID stored in owned local record");
                        withCopy.AssertUnchanged();
                        window.position = new Rect(40, 40, 640, 540); Next(); break;
                    case 2:
                        var body = window.rootVisualElement.Q("build-tool-body");
                        var detail = window.rootVisualElement.Q("profile-detail");
                        BuildToolPreflightVerification.Check(body.worldBound.width > 0 && detail.worldBound.width > 0 && detail.worldBound.xMax <= window.rootVisualElement.worldBound.xMax + 1,
                            "narrow window detail stays inside visible area");
                        BuildToolPreflightVerification.Check(body.worldBound.height >= 259 &&
                            window.rootVisualElement.Q<ScrollView>("profile-list").contentViewport.worldBound.height >= 80,
                            "minimum window preserves usable profile and detail viewports");
                        BuildToolPreflightVerification.Check(window.rootVisualElement.Q<Button>("build-product").resolvedStyle.minHeight.value >= 30 &&
                            window.rootVisualElement.Q<Label>("selected-profile-name").resolvedStyle.fontSize >= 17, "primary controls and product heading stay readable");
                        var scroll = (ScrollView)detail;
                        BuildToolPreflightVerification.Check(scroll.contentContainer.worldBound.height > scroll.contentViewport.worldBound.height, "long inspection results have scrollable overflow");
                        scroll.scrollOffset = new Vector2(0, 100);
                        BuildToolPreflightVerification.Check(scroll.scrollOffset.y > 0, "inspection result scroll responds");
                        lastWindowBounds = window.position;
                        window.Close(); window = ScriptableObject.CreateInstance<MatchBuildToolWindow>(); window.SelectionPath = selectionPath;
                        window.RestoreWindowBounds();
                        window.Show(); window.CreateGUI(); Next(); break;
                    case 3:
                        BuildToolPreflightVerification.Check(window.position.size == new Vector2(640, 540), "last resized window size survives recreation");
                        BuildToolPreflightVerification.Check(window.position == lastWindowBounds, "last window position and size survive recreation");
                        BuildToolPreflightVerification.Check(window.rootVisualElement.Q<Label>("profile-information").text.Contains(selected), "selection survives window recreation");
                        window.SelectProfile(copyGuid); window.RunPreflight(); withCopy.AssertUnchanged();
                        string renamed = "__BuildToolRenamed-" + Guid.NewGuid().ToString("N");
                        string error = AssetDatabase.RenameAsset(copyPath, renamed);
                        BuildToolPreflightVerification.Check(string.IsNullOrEmpty(error), "owned profile renamed");
                        copyPath = AssetDatabase.GUIDToAssetPath(copyGuid); window.ReloadProfiles();
                        BuildToolPreflightVerification.Check(window.rootVisualElement.Q<Label>("selected-profile-name").text == renamed, "GUID selection survives asset rename");
                        BuildToolPreflightVerification.Check(AssetDatabase.DeleteAsset(copyPath), "owned profile deleted"); copyPath = null;
                        window.RunPreflight();
                        BuildToolPreflightVerification.Check(!window.rootVisualElement.Q<Label>("profile-information").text.Contains(copyGuid), "deleted selection not reused");
                        original.AssertUnchanged();
                        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                        var savedProfiles = typeof(MatchBuildToolWindow).GetField("profiles", flags).GetValue(window);
                        typeof(MatchBuildToolWindow).GetField("profiles", flags).SetValue(window, new System.Collections.Generic.List<BuildToolProfileInfo>());
                        typeof(MatchBuildToolWindow).GetMethod("DrawProfiles", flags).Invoke(window, null);
                        typeof(MatchBuildToolWindow).GetMethod("DrawDetail", flags).Invoke(window, null);
                        BuildToolPreflightVerification.Check(!window.rootVisualElement.Q<Button>("inspect-profile").enabledSelf &&
                            window.rootVisualElement.Q("profile-list").Query<Label>().ToList().Any(label => label.text.Contains("없습니다")), "empty list remains usable and explains missing profiles");
                        var bad = BuildToolProfiles.Read(null, "missing.asset", "unreadable-guid");
                        typeof(MatchBuildToolWindow).GetField("profiles", flags).SetValue(window, new System.Collections.Generic.List<BuildToolProfileInfo> { bad });
                        typeof(MatchBuildToolWindow).GetField("selectedGuid", flags).SetValue(window, bad.Guid);
                        typeof(MatchBuildToolWindow).GetMethod("DrawDetail", flags).Invoke(window, null);
                        BuildToolPreflightVerification.Check(window.rootVisualElement.Q<Label>("preflight-summary").text.Contains("오류") &&
                            window.rootVisualElement.Q<Label>("profile-information").text.Contains("missing.asset"), "error entry displays failure and actual path");
                        typeof(MatchBuildToolWindow).GetField("profiles", flags).SetValue(window, savedProfiles); window.ReloadProfiles();
                        string preferences = "UserSettings/MatchBuildTool.json";
                        var job = new JObject { ["backup"] = File.Exists(preferences) ? Convert.ToBase64String(File.ReadAllBytes(preferences)) : null,
                            ["selectionPath"] = selectionPath, ["expectedGuid"] = selected,
                            ["files"] = JObject.FromObject(BuildToolStateSnapshot.CaptureFiles()), ["routing"] = CaptureRouting(),
                            ["width"] = EditorPrefs.HasKey("Match.BuildTool.WindowSize." + Path.GetFullPath(preferences) + ".Width") ? (float?)EditorPrefs.GetFloat("Match.BuildTool.WindowSize." + Path.GetFullPath(preferences) + ".Width") : null,
                            ["height"] = EditorPrefs.HasKey("Match.BuildTool.WindowSize." + Path.GetFullPath(preferences) + ".Height") ? (float?)EditorPrefs.GetFloat("Match.BuildTool.WindowSize." + Path.GetFullPath(preferences) + ".Height") : null,
                            ["x"] = EditorPrefs.HasKey("Match.BuildTool.WindowSize." + Path.GetFullPath(preferences) + ".X") ? (float?)EditorPrefs.GetFloat("Match.BuildTool.WindowSize." + Path.GetFullPath(preferences) + ".X") : null,
                            ["y"] = EditorPrefs.HasKey("Match.BuildTool.WindowSize." + Path.GetFullPath(preferences) + ".Y") ? (float?)EditorPrefs.GetFloat("Match.BuildTool.WindowSize." + Path.GetFullPath(preferences) + ".Y") : null };
                        SessionState.SetString(ReloadJob, job.ToString());
                        window.SelectionPath = preferences; window.SelectProfile(selected);
                        EditorApplication.update -= Update;
                        EditorUtility.RequestScriptReload(); break;
                }
            }
            catch (Exception error) { Finish(error); }
        }

        internal static string CaptureRouting()
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            return AssetDatabase.GetAssetPath(BuildProfile.GetActiveBuildProfile()) + "|" + EditorUserBuildSettings.activeBuildTarget + "|" + settings.activeProfileId +
                "|" + UnityEngine.AddressableAssets.Addressables.LibraryPath + "|" + UnityEngine.AddressableAssets.Addressables.BuildReportPath + "|" + settings.ContentStateBuildPath;
        }

        private static void VerifyReload()
        {
            JObject job = JObject.Parse(SessionState.GetString(ReloadJob, ""));
            Exception failure = null;
            try
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                var restored = Resources.FindObjectsOfTypeAll<MatchBuildToolWindow>().SingleOrDefault();
                if (restored?.rootVisualElement.Q<Label>("profile-information") == null)
                {
                    if (EditorApplication.timeSinceStartup < deadline) return;
                    throw new Exception("window restoration after script reload timed out");
                }
                BuildToolPreflightVerification.Check(restored.rootVisualElement.Q<Label>("profile-information").text.Contains((string)job["expectedGuid"]), "real script reload restores selected GUID");
                BuildToolPreflightVerification.Check((string)job["routing"] == CaptureRouting(), "script reload preserves active product and content routing");
                var files = BuildToolStateSnapshot.CaptureFiles(); var expected = job["files"].ToObject<System.Collections.Generic.Dictionary<string, string>>();
                BuildToolPreflightVerification.Check(files.Count == expected.Count && expected.All(pair => files.TryGetValue(pair.Key, out string hash) && hash == pair.Value), "script reload preserves project settings files");
            }
            catch (Exception error) { failure = error; }
            EditorApplication.update -= VerifyReload; SessionState.EraseString(ReloadJob);
            foreach (var restored in Resources.FindObjectsOfTypeAll<MatchBuildToolWindow>()) restored.Close();
            string preferences = "UserSettings/MatchBuildTool.json";
            foreach (string dimension in new[] { "Width", "Height", "X", "Y" })
            {
                string key = "Match.BuildTool.WindowSize." + Path.GetFullPath(preferences) + "." + dimension;
                var value = job[dimension.ToLowerInvariant()];
                if (value.Type == JTokenType.Null) EditorPrefs.DeleteKey(key); else EditorPrefs.SetFloat(key, (float)value);
                EditorPrefs.DeleteKey("Match.BuildTool.WindowSize." + Path.GetFullPath((string)job["selectionPath"]) + "." + dimension);
            }
            if (job["backup"].Type == JTokenType.Null) { if (File.Exists(preferences)) File.Delete(preferences); }
            else File.WriteAllBytes(preferences, Convert.FromBase64String((string)job["backup"]));
            if (File.Exists((string)job["selectionPath"])) File.Delete((string)job["selectionPath"]);
            if (failure == null) Debug.Log("PASS BuildToolWindowVerification (including real script reload)"); else Debug.LogException(failure);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static void Next() { phase++; next = EditorApplication.timeSinceStartup + 1; }
        private static void Finish(Exception error)
        {
            EditorApplication.update -= Update;
            if (window != null) window.Close();
            if (!string.IsNullOrEmpty(selectionPath))
            {
                EditorPrefs.DeleteKey("Match.BuildTool.WindowSize." + Path.GetFullPath(selectionPath) + ".Width");
                EditorPrefs.DeleteKey("Match.BuildTool.WindowSize." + Path.GetFullPath(selectionPath) + ".Height");
                EditorPrefs.DeleteKey("Match.BuildTool.WindowSize." + Path.GetFullPath(selectionPath) + ".X");
                EditorPrefs.DeleteKey("Match.BuildTool.WindowSize." + Path.GetFullPath(selectionPath) + ".Y");
            }
            if (!string.IsNullOrEmpty(copyPath)) AssetDatabase.DeleteAsset(copyPath);
            if (!string.IsNullOrEmpty(selectionPath) && File.Exists(selectionPath)) File.Delete(selectionPath);
            if (error == null) Debug.Log("PASS BuildToolWindowVerification"); else Debug.LogException(error);
            EditorApplication.Exit(error == null ? 0 : 1);
        }
    }
}
