// [UNITY-SKILL:SPRITEATLAS]
using System.IO;
using System.Linq;
using Board;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Levels.Editor
{
    public sealed class BoardAtlasPrebuild : BuildPlayerProcessor
    {
        public const string AtlasDirectory = "Assets/Textures/Atlases";
        public static string AtlasPath(string address) => AtlasDirectory + "/" + address + ".spriteatlasv2";
        public static string AddressForPath(string path) => BoardSpriteAtlas.AddressFor(path.Substring("Assets/Textures/".Length));
        public override int callbackOrder => -1000;
        public override void PrepareForBuild(BuildPlayerContext context) => Generate();

        public static string[] SourcePaths() => Directory.GetFiles("Assets/Textures", "*.png", SearchOption.AllDirectories)
            .Select(path => path.Replace('\\', '/')).Where(path =>
                path.StartsWith("Assets/Textures/Blocks/") ||
                path.StartsWith("Assets/Textures/BoardTerrain/") || path.StartsWith("Assets/Textures/BoardDevices/") ||
                path.StartsWith("Assets/Textures/Effects/GeneratorCharge/") ||
                path.Contains("/Animations/") ||
                path.StartsWith("Assets/Textures/Obstacles/") && !new[] { "Crate", "Scrap", "RecoveryCapsule", "MetalRodBox", "ColorLock", "Generator", "Web", "Mold", "Dust", "Lock" }.Contains(path.Split('/')[3]) ||
                path.StartsWith("Assets/Textures/PowerBlocks/") && (!path.Contains("4frames") || ((TextureImporter)AssetImporter.GetAtPath(path)).spriteImportMode == SpriteImportMode.Multiple) && !path.Contains("launch-frame-1-") ||
                path.Contains("/Crate/crate-durability-") || path.Contains("/Scrap/scrap-durability-") ||
                path.Contains("/RecoveryCapsule/recovery-capsule-durability-") || path.Contains("/MetalRodBox/metal-rod-box-") ||
                path.Contains("/ColorLock/color-lock-") && path.Contains("-durability-") ||
                path.Contains("/Generator/generator-charge-") || path.Contains("/Web/web-durability-") && path.EndsWith("-v2-256.png") ||
                path.EndsWith("/Mold/mold-base-v1-256.png") || path.Contains("/Dust/dust-durability-") && path.EndsWith("-v1-256.png"))
            .OrderBy(path => path).ToArray();

        public static void Generate()
        {
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            string[] paths = SourcePaths();
            foreach (string path in paths)
            {
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                if (importer.spriteImportMode != SpriteImportMode.Multiple) importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                if (importer.spriteImportMode != SpriteImportMode.Multiple)
                    importer.maxTextureSize = path.Contains("/Generator/") ? 512 : 256;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                TextureImporterSettings spriteSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(spriteSettings);
                spriteSettings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(spriteSettings);
                importer.SaveAndReimport();
            }
            Directory.CreateDirectory(AtlasDirectory);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            AddressableAssetGroup group = settings.FindGroup("Board Artwork") ?? settings.CreateGroup("Board Artwork", false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            BundledAssetGroupSchema bundle = group.GetSchema<BundledAssetGroupSchema>();
            // 아틀라스를 나누어도 번들을 합치면 모든 종류가 함께 로드되므로 개별 패킹한다.
            bundle.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            bundle.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundle.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            string[] addresses = paths.Select(AddressForPath).Distinct().ToArray();
            foreach (string oldPath in Directory.GetFiles(AtlasDirectory, "MoonRabbitBoard*.spriteatlasv2"))
            {
                if (addresses.Contains(Path.GetFileNameWithoutExtension(oldPath))) continue;
                settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(oldPath));
                AssetDatabase.DeleteAsset(oldPath.Replace('\\', '/'));
            }
            foreach (var sources in paths.GroupBy(AddressForPath))
            {
                string atlasPath = AtlasPath(sources.Key);
                SpriteAtlasAsset asset = new SpriteAtlasAsset();
                // 텍스처를 등록하면 Multiple로 잘린 애니메이션 프레임도 모두 포함한다.
                asset.Add(sources.Select(path => AssetDatabase.LoadAssetAtPath<Texture2D>(path)).Cast<UnityEngine.Object>().ToArray());
                SpriteAtlasAsset.Save(asset, atlasPath);
                AssetDatabase.ImportAsset(atlasPath);
                SpriteAtlasImporter atlasImporter = (SpriteAtlasImporter)AssetImporter.GetAtPath(atlasPath);
                SpriteAtlasPackingSettings packing = atlasImporter.packingSettings;
                packing.enableRotation = false;
                packing.enableTightPacking = false;
                packing.padding = 4;
                atlasImporter.packingSettings = packing;
                SpriteAtlasTextureSettings texture = atlasImporter.textureSettings;
                texture.generateMipMaps = false;
                texture.readable = false;
                texture.filterMode = FilterMode.Bilinear;
                atlasImporter.textureSettings = texture;
                TextureImporterPlatformSettings platform = atlasImporter.GetPlatformSettings("DefaultTexturePlatform");
                platform.maxTextureSize = 2048;
                platform.textureCompression = TextureImporterCompression.Uncompressed;
                platform.format = TextureImporterFormat.RGBA32;
                atlasImporter.SetPlatformSettings(platform);
                atlasImporter.includeInBuild = false;
                atlasImporter.SaveAndReimport();
                settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(atlasPath), group).address = sources.Key;
            }
            // 별도 전처리가 콘텐츠를 먼저 빌드하므로 플레이어 빌드의 중복 자동 빌드는 끈다.
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("Board atlas source count: " + paths.Length + ", packer: " + EditorSettings.spritePackerMode);
        }

        public static void Prepare()
        {
            Generate();
            // 에디터 미리보기 패킹만 명시적으로 수행한다. 플레이어에서는 빌드 파이프라인이 처리한다.
            SpriteAtlas[] atlases = SourcePaths().Select(AddressForPath).Distinct()
                .Select(address => AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath(address))).ToArray();
            SpriteAtlasUtility.PackAtlases(atlases, EditorUserBuildSettings.activeBuildTarget, false);
            BoardAtlasContentBuild.Build();
            EditorApplication.Exit(0);
        }
    }
}
