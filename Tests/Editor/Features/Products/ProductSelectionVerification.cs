using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Products.Editor
{
    public static class ProductSelectionVerification
    {
        public static void Run()
        {
            BuildProfile previous = BuildProfile.GetActiveBuildProfile();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            string previousContent = settings.activeProfileId;
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string previousOutput = EditorUserBuildSettings.GetBuildLocation(target);
            string previousLibrary = Addressables.LibraryPath, previousReport = Addressables.BuildReportPath;
            string previousResolvedState = settings.ContentStateBuildPath;
            SerializedObject serialized = new SerializedObject(settings);
            string previousState = serialized.FindProperty("m_ContentStateBuildPath").stringValue;
            string previousCustomState = serialized.FindProperty("m_CustomContentStateBuildPath").stringValue;
            byte[] global = File.ReadAllBytes("ProjectSettings/ProjectSettings.asset");
            Exception failure = null;
            try
            {
                Debug.Log("Product selection active target=" + target + "; no platform switch");
                ProductDefinition[] compatible = ProductProfiles.Definitions.Where(value => value.Target == target).ToArray();
                Check(compatible.Length > 0, "current target has product definitions");
                string[] catalogRoots = ProductProfiles.Definitions.Select(value =>
                {
                    ProductProfiles.ApplyContentPaths(value);
                    Check(Addressables.LibraryPath.Contains("/Products/" + value.Symbol + "/"), "library isolated " + value.Symbol);
                    Check(Addressables.BuildReportPath.StartsWith(Addressables.LibraryPath, StringComparison.Ordinal), "report isolated " + value.Symbol);
                    Check(settings.ContentStateBuildPath.StartsWith(Addressables.LibraryPath, StringComparison.Ordinal), "content state isolated " + value.Symbol);
                    return Addressables.BuildPath;
                }).ToArray();
                Check(catalogRoots.Distinct().Count() == 4, "four distinct initialization/catalog roots without building");
                ProductProfiles.ApplyContentPaths(null);
                Check(Addressables.LibraryPath == "Library/com.unity.addressables/" && Addressables.BuildReportPath == "Library/com.unity.addressables/BuildReports/" && string.IsNullOrEmpty(settings.ContentStateBuildPath), "default content paths restored");
                foreach (ProductDefinition definition in compatible)
                {
                    ProductProfiles.Select(definition.Symbol);
                    BuildProfile selected = BuildProfile.GetActiveBuildProfile();
                    Check(selected != null && selected.scriptingDefines.Contains(definition.Symbol), "Build Profile selected " + definition.Symbol);
                    Check(settings.activeProfileId == settings.profileSettings.GetProfileId(definition.Name), "Addressables Profile matched " + definition.Symbol);
                    string selectedOutput = EditorUserBuildSettings.GetBuildLocation(target);
                    Debug.Log("Selected output actual=" + selectedOutput + "; expected=" + definition.Output);
                    Check(!string.IsNullOrEmpty(selectedOutput) && string.Equals(Path.GetFullPath(selectedOutput), Path.GetFullPath(definition.Output), StringComparison.OrdinalIgnoreCase), "output matched " + definition.Symbol);
                    Check(Addressables.BuildPath.Contains("/Products/" + definition.Symbol + "/"), "catalog root matched " + definition.Symbol);
                    ProductProfiles.PrepareContentBuild();
                }
                Debug.Log("Not selected in this run: " + string.Join(", ", ProductProfiles.Definitions.Except(compatible).Select(value => value.Name)));
            }
            catch (Exception error) { failure = error; }
            finally
            {
                BuildProfile.SetActiveBuildProfile(previous);
                Addressables.LibraryPath = previousLibrary; Addressables.BuildReportPath = previousReport;
                settings.activeProfileId = previousContent;
                serialized.Update();
                serialized.FindProperty("m_ContentStateBuildPath").stringValue = previousState;
                serialized.FindProperty("m_CustomContentStateBuildPath").stringValue = previousCustomState;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
                EditorUserBuildSettings.SetBuildLocation(target, previousOutput);
            }
            try
            {
                if (failure != null) throw failure;
                Check(BuildProfile.GetActiveBuildProfile() == previous && settings.activeProfileId == previousContent && EditorUserBuildSettings.GetBuildLocation(target) == previousOutput, "previous selections restored");
                serialized.Update();
                Check(Addressables.LibraryPath == previousLibrary && Addressables.BuildReportPath == previousReport && settings.ContentStateBuildPath == previousResolvedState &&
                    serialized.FindProperty("m_ContentStateBuildPath").stringValue == previousState && serialized.FindProperty("m_CustomContentStateBuildPath").stringValue == previousCustomState, "previous content routes restored");
                Check(global.SequenceEqual(File.ReadAllBytes("ProjectSettings/ProjectSettings.asset")), "global settings unchanged");
                Debug.Log("ProductSelectionVerification PASS (immediate selection only; no build or domain reload execution)"); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        static void Check(bool condition, string text) { if (!condition) throw new Exception(text); Debug.Log("PASS " + text); }
    }
}
