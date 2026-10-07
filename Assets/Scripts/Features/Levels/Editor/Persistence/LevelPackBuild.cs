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
using Elements;
using MemoryPack;
using Elements.Editor;

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
            Dictionary<string, byte[]> outputs = CreatePackBytes(levels);
            byte[] contentBytes = ElementContentPackBuild.CreateDefaultBytes();
            foreach (ElementCatalogAsset catalog in levels.Select(level => level.ElementCatalog).Where(value => value != null).Distinct())
                ElementContentAuthoring.ValidatePlanning(catalog);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            ValidateExclusion(settings, true);
            foreach (string guid in AuthoringGuids()) settings.RemoveAssetEntry(guid);
            ValidateExclusion(settings);
            ElementContentPackBuild.WriteAndRegister(contentBytes, settings);
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
            => ValidateExclusion(settings, false);

        private static void ValidateExclusion(AddressableAssetSettings settings, bool omitDirectOriginals)
        {
            List<string> roots = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToList();
            roots.AddRange(PlayerSettings.GetPreloadedAssets().Where(asset => asset != null).Select(AssetDatabase.GetAssetPath));
            roots.AddRange(AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/") && path.Contains("/Resources/") &&
                !path.Contains("/Editor/") && !AssetDatabase.IsValidFolder(path)));
            foreach (AddressableAssetGroup group in settings.groups.Where(group => group != null))
                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (omitDirectOriginals && IsAuthoringType(AssetDatabase.GetMainAssetTypeAtPath(entry.AssetPath))) continue;
                    List<AddressableAssetEntry> entries = new List<AddressableAssetEntry>();
                    entry.GatherAllAssets(entries, true, true, false);
                    roots.AddRange(entries.Select(asset => asset.AssetPath));
                }
            string[] dependencies = AssetDatabase.GetDependencies(roots.Where(path => !string.IsNullOrEmpty(path)).Distinct().ToArray(), true);
            string[] originals = dependencies.Where(path => IsAuthoringType(AssetDatabase.GetMainAssetTypeAtPath(path))).ToArray();
            if (originals.Length > 0) throw new BuildFailedException("레벨/요소 제작 원본이 빌드 리소스에서 참조됩니다. 씬/프리팹/Resources/Addressables 폴더 참조를 제거하고 팩으로 로드하세요:\n" + string.Join("\n", originals));
        }

        internal static bool IsAuthoringType(Type type) => type == typeof(LevelDefinition) || type == typeof(ElementCatalogAsset) || type == typeof(ElementDefinitionAsset) || type == typeof(ElementVisualCatalogAsset);
        private static IEnumerable<string> AuthoringGuids() => new[] { "t:LevelDefinition", "t:ElementCatalogAsset", "t:ElementDefinitionAsset", "t:ElementVisualCatalogAsset" }
            .SelectMany(filter => AssetDatabase.FindAssets(filter)).Distinct();

        // 선검증과 메모리 인코딩만 수행한다. 디스크/Addressables 변경은 Generate가 별도로 소유한다.
        public static Dictionary<string, byte[]> CreatePackBytes(IEnumerable<LevelDefinition> levels)
        {
            return levels.GroupBy(level => LevelPackCodec.FirstLevel(level.LevelNumber)).ToDictionary(group => FilePath(group.Key), group =>
            {
                Dictionary<ElementId, ElementDefinition> definitions = new Dictionary<ElementId, ElementDefinition>();
                foreach (ElementDefinition definition in group.SelectMany(level => level.CreateElementCatalog().Definitions))
                {
                    if (definitions.TryGetValue(definition.Id, out ElementDefinition previous))
                    {
                        if (!MemoryPackSerializer.Serialize(PackedElementDefinition.FromDefinition(previous))
                            .SequenceEqual(MemoryPackSerializer.Serialize(PackedElementDefinition.FromDefinition(definition))))
                            throw new ArgumentException($"레벨 구간의 정의 ID 충돌: {definition.Id.Value}");
                    }
                    else definitions.Add(definition.Id, definition);
                }
                return LevelPackCodec.Encode(group, new ElementCatalog(definitions.Values));
            });
        }

        public static void Prepare()
        {
            Generate();
            BoardAtlasContentBuild.Build();
            EditorApplication.Exit(0);
        }
    }
}
