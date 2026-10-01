using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEngine;

namespace Levels.Editor
{
    public sealed class LevelPackBuild : BuildPlayerProcessor
    {
        public const string OutputFolder = "Assets/Data/LevelPacks";
        public const string BuilderPath = "Assets/AddressableAssetsData/DataBuilders/LevelPackAddressablesBuilder.asset";
        public override int callbackOrder => -950;
        public override void PrepareForBuild(BuildPlayerContext context) => Generate();

        public static string FilePath(int number) => OutputFolder + "/" + Path.GetFileName(LevelPackCodec.Address(number)) + ".bytes";

        public static void Generate()
        {
            // 전체 변환을 먼저 검증한다. 중복 번호나 스키마 오류로 기존 산출물을 일부만 바꾸지 않는다.
            LevelDefinition[] levels = AssetDatabase.FindAssets("t:LevelDefinition", new[] { LevelAssetOperations.DefaultFolder })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<LevelDefinition>).ToArray();
            Dictionary<string, byte[]> outputs = levels.GroupBy(level => LevelPackCodec.FirstLevel(level.LevelNumber))
                .ToDictionary(group => FilePath(group.Key), group => LevelPackCodec.Encode(group));
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            foreach (string guid in AssetDatabase.FindAssets("t:LevelDefinition")) settings.RemoveAssetEntry(guid);
            ValidateExclusion(settings);
            Directory.CreateDirectory(OutputFolder);
            foreach (var output in outputs)
                if (!File.Exists(output.Key) || !File.ReadAllBytes(output.Key).SequenceEqual(output.Value)) File.WriteAllBytes(output.Key, output.Value);
            foreach (string obsolete in Directory.GetFiles(OutputFolder, "levels-*.bytes").Select(path => path.Replace('\\', '/')))
                if (!outputs.ContainsKey(obsolete))
                {
                    settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(obsolete));
                    AssetDatabase.DeleteAsset(obsolete);
                }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AddressableAssetGroup group = settings.FindGroup("Level Packs") ?? settings.CreateGroup("Level Packs", false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            BundledAssetGroupSchema bundle = group.GetSchema<BundledAssetGroupSchema>();
            bundle.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            bundle.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundle.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            foreach (var output in outputs)
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(output.Key), group).address = "Levels/" + Path.GetFileNameWithoutExtension(output.Key);
            LevelPackAddressablesBuilder builder = AssetDatabase.LoadAssetAtPath<LevelPackAddressablesBuilder>(BuilderPath);
            if (builder == null)
            {
                builder = ScriptableObject.CreateInstance<LevelPackAddressablesBuilder>();
                AssetDatabase.CreateAsset(builder, BuilderPath);
                settings.AddDataBuilder(builder);
            }
            settings.ActivePlayerDataBuilderIndex = settings.DataBuilders.IndexOf(builder);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(group);
            AssetDatabase.SaveAssetIfDirty(bundle);
            AssetDatabase.SaveAssetIfDirty(builder);
            AssetDatabase.SaveAssetIfDirty(settings);
            Debug.Log($"MemoryPack: {levels.Length} levels, {outputs.Count} packs (50 levels/range)");
        }

        public static void ValidateExclusion(AddressableAssetSettings settings)
        {
            List<string> roots = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToList();
            roots.AddRange(PlayerSettings.GetPreloadedAssets().Where(asset => asset != null).Select(AssetDatabase.GetAssetPath));
            roots.AddRange(AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/") && path.Contains("/Resources/") &&
                !path.Contains("/Editor/") && !AssetDatabase.IsValidFolder(path)));
            foreach (AddressableAssetGroup group in settings.groups.Where(group => group != null))
                foreach (AddressableAssetEntry entry in group.entries)
                {
                    List<AddressableAssetEntry> entries = new List<AddressableAssetEntry>();
                    entry.GatherAllAssets(entries, true, true, false);
                    roots.AddRange(entries.Select(asset => asset.AssetPath));
                }
            string[] dependencies = AssetDatabase.GetDependencies(roots.Where(path => !string.IsNullOrEmpty(path)).Distinct().ToArray(), true);
            string[] originals = dependencies.Where(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(LevelDefinition)).ToArray();
            if (originals.Length > 0) throw new BuildFailedException("레벨 원본이 빌드 리소스에서 참조됩니다. 씬/프리팹/Resources/Addressables 폴더 참조를 제거하고 레벨 번호로 로드하세요:\n" + string.Join("\n", originals));
        }

        public static void Prepare()
        {
            Generate();
            BoardAtlasContentBuild.Build();
            EditorApplication.Exit(0);
        }
    }
}
