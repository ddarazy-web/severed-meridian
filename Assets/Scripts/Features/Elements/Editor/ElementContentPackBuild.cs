using System.IO;
using System.Linq;
using System;
using System.Collections.Generic;
using MemoryPack;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Elements.Editor
{
    public static class ElementContentPackBuild
    {
        public const string OutputPath = "Assets/Data/ElementPacks/default-content.bytes";
        public static byte[] CreateDefaultBytes()
        {
            ElementCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<ElementCatalogAsset>(ElementContentAuthoring.CatalogPath);
            var sources = new[] { catalog }.Concat(AssetDatabase.FindAssets("t:LevelDefinition", new[] { Levels.Editor.LevelAssetOperations.DefaultFolder })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Levels.LevelDefinition>)
                .Select(level => level.ElementCatalog).Where(value => value != null)).Distinct();
            return CreateBytes(sources);
        }

        public static byte[] CreateBytes(IEnumerable<ElementCatalogAsset> sources)
        {
            var definitions = new Dictionary<string, PackedElementDefinition>(StringComparer.Ordinal);
            var visuals = new Dictionary<string, ElementVisualDefinitionDto>(StringComparer.Ordinal);
            var bindings = new Dictionary<string, ElementVisualBindingDto>(StringComparer.Ordinal);
            foreach (ElementCatalogAsset source in sources)
            {
                ElementContentAuthoring.ValidatePlanning(source);
                foreach (ElementDefinition definition in source.CreateCatalog().Definitions)
                {
                    PackedElementDefinition value = PackedElementDefinition.FromDefinition(definition);
                    if (definitions.TryGetValue(value.id, out PackedElementDefinition prior) &&
                        !MemoryPackSerializer.Serialize(prior).SequenceEqual(MemoryPackSerializer.Serialize(value)))
                        throw new ArgumentException("출시 카탈로그의 정의 ID 충돌: " + value.id);
                    definitions[value.id] = value;
                }
                ElementVisualCatalogDto dto = source.CreateVisualCatalog().ToDto();
                foreach (ElementVisualDefinitionDto value in dto.definitions)
                {
                    if (visuals.TryGetValue(value.key, out ElementVisualDefinitionDto prior) &&
                        !MemoryPackSerializer.Serialize(prior).SequenceEqual(MemoryPackSerializer.Serialize(value)))
                        throw new ArgumentException("출시 카탈로그의 시각 키 충돌: " + value.key);
                    visuals[value.key] = value;
                }
                foreach (ElementVisualBindingDto value in dto.bindings)
                {
                    if (bindings.TryGetValue(value.id, out ElementVisualBindingDto prior) && prior.visualKey != value.visualKey)
                        throw new ArgumentException("출시 카탈로그의 시각 ID 충돌: " + value.id);
                    bindings[value.id] = value;
                }
            }
            return ElementContentPackCodec.Encode(new ElementCatalog(definitions.Values.Select(value => value.ToDefinition())),
                ElementVisualCatalog.FromDto(new ElementVisualCatalogDto { definitions = visuals.Values.ToArray(), bindings = bindings.Values.ToArray() }));
        }
        // 데이터 변환만 수행한다. 이미지 번들이나 플레이어 빌드는 실행하지 않는다.
        public static void WriteAndRegister(byte[] bytes, AddressableAssetSettings settings)
        {
            _ = ElementContentPackCodec.Decode(bytes);
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            if (!File.Exists(OutputPath) || !File.ReadAllBytes(OutputPath).SequenceEqual(bytes)) File.WriteAllBytes(OutputPath, bytes);
            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceSynchronousImport);
            AddressableAssetGroup group = settings.FindGroup("Element Data Packs") ?? settings.CreateGroup("Element Data Packs", false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            BundledAssetGroupSchema bundle = group.GetSchema<BundledAssetGroupSchema>();
            bundle.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            bundle.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundle.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(OutputPath), group).address = ElementContentPackCodec.Address;
            EditorUtility.SetDirty(group); EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(bundle); AssetDatabase.SaveAssetIfDirty(group); AssetDatabase.SaveAssetIfDirty(settings);
        }

        [MenuItem("MATCH/데이터/요소 및 레벨 MemoryPack 갱신")]
        public static void Export() => Levels.Editor.LevelPackBuild.Generate();

        // 별도 Editor 자동화에서 초기 생성과 변환만 실행한다.
        public static void InitializeAndExport()
        {
            ElementContentAuthoring.Initialize(); Export();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
