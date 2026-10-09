using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEngine;
using Elements;
using MemoryPack;

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
            // 검증하여 채택한 JSON만 사용한다. 원본 선택 실패를 구형 SO 검색으로 숨기지 않는다.
            string folder = LevelAuthoring.Editor.AuthoringSourceSelection.ReadFolder(LevelAuthoring.Editor.AuthoringSourceSelection.DefaultConfiguration);
            LevelAuthoring.Editor.JsonPackRegistration.Publish(folder);
            Debug.Log("선택한 JSON의 레벨·요소 MemoryPack 갱신 완료 (50레벨 구간)");
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
            string[] originals = dependencies.Where(path => IsAuthoringType(AssetDatabase.GetMainAssetTypeAtPath(path))).ToArray();
            if (originals.Length > 0) throw new BuildFailedException("레벨/요소 제작 원본이 빌드 리소스에서 참조됩니다. 씬/프리팹/Resources/Addressables 폴더 참조를 제거하고 팩으로 로드하세요:\n" + string.Join("\n", originals));
        }

        internal static bool IsAuthoringType(Type type) => type == typeof(LevelDefinition) || type == typeof(ElementCatalogAsset) || type == typeof(ElementDefinitionAsset) || type == typeof(ElementVisualCatalogAsset) ||
            type == typeof(Tutorial.TutorialFlowDefinition) || type == typeof(Tutorial.TutorialUserSampleDefinition) || type == typeof(LevelShapePreset);

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
                return LevelPackCodec.EncodeWithTutorial(group, new ElementCatalog(definitions.Values));
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
