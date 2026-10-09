using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class RegisteredPackVerification
    {
        public static void Run()
        {
            try
            {
                Type type = typeof(JsonPackPublication).Assembly.GetType("LevelAuthoring.Editor.JsonPackRegistration");
                if (type == null) throw new Exception("FAIL Addressables publication adapter missing");
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                var schema = settings.FindGroup("Level Packs").GetSchema<UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema>();
                var originalMode = schema.BundleMode;
                var pendingMode = originalMode == UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema.BundlePackingMode.PackTogether
                    ? UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema.BundlePackingMode.PackSeparately
                    : UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                try
                {
                    schema.BundleMode = pendingMode; EditorUtility.SetDirty(schema);
                    try { JsonPackRegistration.Publish(Path.GetFullPath("Logs/GameAuthoringStage06/missing-source-" + Guid.NewGuid().ToString("N"))); throw new Exception("FAIL dirty settings accepted"); }
                    catch (InvalidOperationException error) when (error.Message.StartsWith("저장되지 않은 Addressables 설정")) { }
                    if (schema.BundleMode != pendingMode || !EditorUtility.IsDirty(schema)) throw new Exception("FAIL unsaved settings lost during preflight");
                    Debug.Log("PASS dirty Addressables settings preserved before refresh/recovery");
                }
                finally { schema.BundleMode = originalMode; EditorUtility.ClearDirty(schema); }
                var paths = Directory.GetFiles("Assets/AddressableAssetsData", "*", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles("Assets/Data/LevelPacks", "*"))
                    .Concat(Directory.GetFiles("Assets/Data/ElementPacks", "*")).ToArray();
                var originals = paths.ToDictionary(path => path, File.ReadAllBytes);
                bool hadManifest = File.Exists(JsonContentPackBuild.GenerationPath);
                string priorManifestAddress = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(JsonContentPackBuild.GenerationPath))?.address;
                bool checkpoint = false;
                try
                {
                    type.GetMethod("Publish").Invoke(null, new object[] { File.ReadAllText("Logs/GameAuthoringStage06/latest-candidate.txt"), (Action)(() =>
                    {
                        string guid = AssetDatabase.AssetPathToGUID(JsonContentPackBuild.GenerationPath);
                        if (settings.FindAssetEntry(guid)?.address != Levels.ContentPackGenerationCodec.Address)
                            throw new Exception("FAIL manifest address missing at registration checkpoint");
                        checkpoint = true;
                        throw new IOException("실제 등록 실패 주입");
                    }) });
                    throw new Exception("FAIL registration failure ignored");
                }
                catch (TargetInvocationException error) when (error.InnerException is IOException failure && failure.Message == "실제 등록 실패 주입") { }
                if (!checkpoint) throw new Exception("FAIL actual registration checkpoint not reached");
                foreach (var pair in originals)
                    if (!File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)) throw new Exception("FAIL rollback " + pair.Key);
                if (!hadManifest && (File.Exists(JsonContentPackBuild.GenerationPath) || File.Exists(JsonContentPackBuild.GenerationPath + ".meta")))
                    throw new Exception("FAIL newly registered manifest remains after rollback");
                var group = AddressableAssetSettingsDefaultObject.Settings.FindGroup("Level Packs");
                if (AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(JsonContentPackBuild.GenerationPath))?.address != priorManifestAddress)
                    throw new Exception("FAIL in-memory registration survives rollback");
                Debug.Log("PASS actual Addressables registration rollback bytes GUID entries and loaded objects");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}

