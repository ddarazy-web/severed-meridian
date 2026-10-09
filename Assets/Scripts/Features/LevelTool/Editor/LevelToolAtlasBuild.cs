// [UNITY-SKILL:SPRITEATLAS]
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace LevelTool.Editor
{
    public sealed class LevelToolAtlasBuild : BuildPlayerProcessor
    {
        public const string Sources = "Assets/Textures/LevelTool/UI/Sprites";
        public const string AtlasPath = "Assets/Textures/LevelTool/UI/LevelToolUI.spriteatlasv2";
        public const string GroupName = "Level Tool UI";
        public override int callbackOrder => -1100;
        public override void PrepareForBuild(BuildPlayerContext context) => Generate();

        public static void ApplyInclusion(bool isTool)
        {
            var group = AddressableAssetSettingsDefaultObject.Settings?.FindGroup(GroupName);
            if (group == null) return;
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema.IncludeInBuild == isTool) return;
            schema.IncludeInBuild = isTool;
            EditorUtility.SetDirty(schema); AssetDatabase.SaveAssetIfDirty(schema);
        }

        public static void Generate()
        {
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            var paths = Directory.GetFiles(Sources, "*.png").Select(path => path.Replace('\\', '/')).OrderBy(path => path).ToArray();
            if (paths.Length == 0) throw new InvalidOperationException("레벨툴 UI 원본을 확인하세요.");
            foreach (string path in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = path.EndsWith("icons.png") ? Vector4.zero : new Vector4(16, 16, 16, 16);
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.alphaSource = path.EndsWith("icons.png") ? TextureImporterAlphaSource.FromGrayScale : TextureImporterAlphaSource.FromInput;
                importer.isReadable = false; importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
                var spriteSettings = new TextureImporterSettings(); importer.ReadTextureSettings(spriteSettings);
                spriteSettings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(spriteSettings);
                importer.SaveAndReimport();
            }
            var asset = new SpriteAtlasAsset();
            asset.Add(paths.Select(path => AssetDatabase.LoadAssetAtPath<Texture2D>(path)).Cast<UnityEngine.Object>().ToArray());
            SpriteAtlasAsset.Save(asset, AtlasPath); AssetDatabase.ImportAsset(AtlasPath);
            var atlas = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath);
            var packing = atlas.packingSettings;
            packing.enableRotation = false; packing.enableTightPacking = false; packing.padding = 4;
            atlas.packingSettings = packing;
            var texture = atlas.textureSettings; texture.generateMipMaps = false; texture.readable = false;
            texture.filterMode = FilterMode.Bilinear; atlas.textureSettings = texture;
            var platform = atlas.GetPlatformSettings("DefaultTexturePlatform");
            platform.maxTextureSize = 512; platform.textureCompression = TextureImporterCompression.Uncompressed;
            platform.format = TextureImporterFormat.RGBA32; atlas.SetPlatformSettings(platform);
            atlas.includeInBuild = false; atlas.SaveAndReimport();
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(GroupName) ?? settings.CreateGroup(GroupName, false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundle = group.GetSchema<BundledAssetGroupSchema>();
            bundle.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundle.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
            bundle.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AtlasPath), group).address = "LevelToolUI";
            Products.Editor.ProductProfiles.SynchronizeContentPaths();
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
        }
    }
}
