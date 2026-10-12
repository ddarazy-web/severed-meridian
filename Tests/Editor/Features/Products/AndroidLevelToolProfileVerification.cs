using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace Products.Editor
{
    public static class AndroidLevelToolProfileVerification
    {
        public static void Run()
        {
            try
            {
                ProductDefinition[] existing = ProductProfiles.Definitions.Where(value => value.Name != "Android Level Editor").ToArray();
                string[] paths = existing.SelectMany(value => new[] { value.ProfilePath, value.ProfilePath + ".meta" }).Concat(new[] { "ProjectSettings/ProjectSettings.asset" }).ToArray();
                Dictionary<string, byte[]> preserved = paths.ToDictionary(path => path, File.ReadAllBytes);
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                string selectedContent = settings.activeProfileId;
                string[] groupGuids = settings.groups.Where(value => value != null).Select(value => value.Guid).OrderBy(value => value).ToArray();
                BuildProfile selected = BuildProfile.GetActiveBuildProfile();
                BuildTarget selectedTarget = EditorUserBuildSettings.activeBuildTarget;
                string selectedOutput = EditorUserBuildSettings.GetBuildLocation(selectedTarget);
                MethodInfo adapter = typeof(ProductProfiles).GetMethod("ProfileSettings", BindingFlags.Static | BindingFlags.NonPublic);
                ProductDefinition android = ProductProfiles.ValidateSelection(new[] { "PRODUCT_LEVEL_EDITOR" }, BuildTarget.Android);
                string guid = null;
                for (int iteration = 0; iteration < 2; iteration++)
                {
                    ProductProfiles.Configure();
                    BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(android.ProfilePath);
                    Check(profile != null && ProductProfiles.ValidateProfile(profile) == android, "Android tool target, symbol and scene");
                    string currentGuid = AssetDatabase.AssetPathToGUID(android.ProfilePath);
                    Check(!string.IsNullOrEmpty(currentGuid) && existing.All(value => AssetDatabase.AssetPathToGUID(value.ProfilePath) != currentGuid), "new profile has independent GUID");
                    if (guid != null) Check(guid == currentGuid, "reconfiguration preserves new profile GUID");
                    guid = currentGuid;
                    SerializedObject player = new SerializedObject((UnityEngine.Object)adapter.Invoke(null, new object[] { profile, false }));
                    Check(player.FindProperty("productName").stringValue == "Moon Rabbit Level Tool", "Android tool app name");
                    SerializedProperty identifiers = player.FindProperty("applicationIdentifier");
                    bool matchingId = false;
                    for (int index = 0; index < identifiers.arraySize; index++)
                    {
                        SerializedProperty pair = identifiers.GetArrayElementAtIndex(index);
                        if (pair.FindPropertyRelative("first").stringValue == "Android")
                            matchingId = pair.FindPropertyRelative("second").stringValue == "com.twelvethwarriors.aos.moonrabbits.leveleditor";
                    }
                    Check(matchingId, "Android tool app identifier differs from game");
                    Check(player.FindProperty("defaultScreenOrientation").intValue == (int)UIOrientation.AutoRotation, "Android tool auto rotation");
                    foreach (string rotation in new[] { "allowedAutorotateToPortrait", "allowedAutorotateToPortraitUpsideDown", "allowedAutorotateToLandscapeRight", "allowedAutorotateToLandscapeLeft" })
                        Check(player.FindProperty(rotation).boolValue, "Android tool " + rotation);
                    BuildProfile game = AssetDatabase.LoadAssetAtPath<BuildProfile>(existing.Single(value => value.Target == BuildTarget.Android).ProfilePath);
                    SerializedObject gamePlayer = new SerializedObject((UnityEngine.Object)adapter.Invoke(null, new object[] { game, false }));
                    foreach (string window in new[] { "resizableWindow", "fullscreenMode", "defaultScreenWidth", "defaultScreenHeight" })
                        Check(player.FindProperty(window).type == "bool" ? player.FindProperty(window).boolValue == gamePlayer.FindProperty(window).boolValue : player.FindProperty(window).intValue == gamePlayer.FindProperty(window).intValue, "Android keeps source window setting " + window);
                    foreach (ProductDefinition definition in ProductProfiles.Definitions)
                    {
                        string id = settings.profileSettings.GetProfileId(definition.Name);
                        Check(!string.IsNullOrEmpty(id), "content profile " + definition.Name);
                        Check(settings.profileSettings.GetValueByName(id, "Local.BuildPath") == "[UnityEngine.AddressableAssets.Addressables.BuildPath]/" + definition.ContentKey, "build path " + definition.Name);
                        Check(settings.profileSettings.GetValueByName(id, "Local.LoadPath") == "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/" + definition.ContentKey, "load path " + definition.Name);
                    }
                    foreach (KeyValuePair<string, byte[]> entry in preserved)
                        Check(entry.Value.SequenceEqual(File.ReadAllBytes(entry.Key)), "preserved " + entry.Key);
                    Check(settings.groups.Where(value => value != null).Select(value => value.Guid).OrderBy(value => value).SequenceEqual(groupGuids), "content groups reused");
                    Check(BuildProfile.GetActiveBuildProfile() == selected && settings.activeProfileId == selectedContent && EditorUserBuildSettings.activeBuildTarget == selectedTarget && EditorUserBuildSettings.GetBuildLocation(selectedTarget) == selectedOutput, "configuration preserves selection and output");
                    Check(android.Output == "Builds/Android/LevelTool.apk", "Android output contract without building");
                }
                Debug.Log("AndroidLevelToolProfileVerification PASS (configuration only; no platform switch or build)");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Debug.Log("PASS " + message);
        }
    }
}
