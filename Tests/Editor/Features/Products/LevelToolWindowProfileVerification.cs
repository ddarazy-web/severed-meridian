using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace Products.Editor
{
    public static class LevelToolWindowProfileVerification
    {
        const string ToolPath = "Assets/Settings/BuildProfiles/Windows Level Editor.asset";

        public static void Run()
        {
            try
            {
                Type rules = AppDomain.CurrentDomain.GetAssemblies().Select(value => value.GetType("Products.Editor.ProductProfiles")).First(value => value != null);
                MethodInfo settingsAdapter = rules.GetMethod("ProfileSettings", BindingFlags.Static | BindingFlags.NonPublic);
                BuildProfile tool = AssetDatabase.LoadAssetAtPath<BuildProfile>(ToolPath);
                Check(tool != null && settingsAdapter != null, "tool profile and existing settings adapter exist");
                UnityEngine.Object settings = (UnityEngine.Object)settingsAdapter.Invoke(null, new object[] { tool, false });
                string unrelatedToolSettings = WithoutWindowSettings(EditorJsonUtility.ToJson(settings));
                string[] paths =
                {
                    "ProjectSettings/ProjectSettings.asset",
                    "Assets/Settings/BuildProfiles/Android Game.asset",
                    "Assets/Settings/BuildProfiles/iOS Game.asset",
                    "Assets/Settings/BuildProfiles/Windows Steam Game.asset",
                    "Assets/Settings/BuildProfiles/Android Game.asset.meta",
                    "Assets/Settings/BuildProfiles/iOS Game.asset.meta",
                    "Assets/Settings/BuildProfiles/Windows Steam Game.asset.meta",
                    ToolPath + ".meta"
                };
                Dictionary<string, byte[]> preserved = paths.ToDictionary(path => path, File.ReadAllBytes);
                for (int iteration = 0; iteration < 2; iteration++)
                {
                    rules.GetMethod("Configure").Invoke(null, null);
                    settings = (UnityEngine.Object)settingsAdapter.Invoke(null, new object[] { tool, false });
                    SerializedObject player = new SerializedObject(settings);
                    Check(player.FindProperty("resizableWindow").boolValue, "Windows Level Editor is resizable");
                    Check(player.FindProperty("fullscreenMode").intValue == (int)FullScreenMode.Windowed, "Windows Level Editor uses Unity FullScreenMode.Windowed");
                    Check(player.FindProperty("defaultScreenWidth").intValue == 1280 && player.FindProperty("defaultScreenHeight").intValue == 800, "Windows Level Editor defaults to 1280 x 800");
                    Check(unrelatedToolSettings == WithoutWindowSettings(EditorJsonUtility.ToJson(settings)), "unrelated tool PlayerSettings preserved");
                    foreach (KeyValuePair<string, byte[]> entry in preserved)
                        Check(entry.Value.SequenceEqual(File.ReadAllBytes(entry.Key)), "preserved " + entry.Key);
                    Debug.Log("PASS Configure iteration " + (iteration + 1));
                }
                Debug.Log("LevelToolWindowProfileVerification PASS (actual OS window behavior requires a separately authorized Player build)");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        static string WithoutWindowSettings(string json)
        {
            return Regex.Replace(json, "\\\"(?:resizableWindow|fullscreenMode|defaultScreenWidth|defaultScreenHeight)\\\":(?:true|false|-?[0-9]+)", string.Empty);
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Debug.Log("PASS " + message);
        }
    }
}
