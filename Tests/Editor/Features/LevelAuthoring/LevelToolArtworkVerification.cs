using System;
using System.IO;
using System.Linq;
using LevelTool.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace LevelAuthoring.Editor
{
    public static class LevelToolArtworkVerification
    {
        public static void Prepare()
        {
            try
            {
                foreach (string name in new[] { "panel", "button", "input", "icons" })
                {
                    string path = LevelToolAtlasBuild.Sources + "/" + name + ".png";
                    if (File.Exists(path)) continue;
                    var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    source.LoadImage(File.ReadAllBytes("ArtSources/LevelTool/UI/" + name + ".png"));
                    int width = name == "icons" ? 256 : 128, height = name == "panel" ? 128 : name == "icons" ? 256 : 64;
                    int left = 0, bottom = 0, right = source.width - 1, top = source.height - 1;
                    if (name != "icons")
                    {
                        left = source.width; bottom = source.height; right = top = 0;
                        Color32[] pixels = source.GetPixels32();
                        for (int y = 0; y < source.height; y++) for (int x = 0; x < source.width; x++)
                        {
                            if (pixels[y * source.width + x].a < 8) continue;
                            left = Math.Min(left, x); bottom = Math.Min(bottom, y);
                            right = Math.Max(right, x); top = Math.Max(top, y);
                        }
                    }
                    var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    // 생성 원본은 보존하고 투명 여백 및 배포 해상도만 정규화한다.
                    for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                        result.SetPixel(x, y, source.GetPixelBilinear((left + (x + .5f) * (right - left + 1) / width) / source.width,
                            (bottom + (y + .5f) * (top - bottom + 1) / height) / source.height));
                    result.Apply(); File.WriteAllBytes(path, result.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(result); UnityEngine.Object.DestroyImmediate(source);
                }
                AssetDatabase.Refresh(); LevelToolAtlasBuild.Generate();
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(LevelToolAtlasBuild.AtlasPath);
                SpriteAtlasUtility.PackAtlases(new[] { atlas }, BuildTarget.StandaloneWindows64);
                if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2 || atlas.spriteCount != 4)
                    throw new Exception("레벨툴 전용 아틀라스 패킹 실패");
                foreach (string name in new[] { "panel", "button", "input", "icons" })
                {
                    var sprite = atlas.GetSprite(name);
                    if (sprite == null || !Mathf.IsPowerOfTwo((int)sprite.rect.width) || !Mathf.IsPowerOfTwo((int)sprite.rect.height))
                        throw new Exception("스프라이트/2의 승수 크기 오류 " + name);
                    if (name != "icons" && sprite.border != new Vector4(16, 16, 16, 16)) throw new Exception("9슬라이스 누락");
                    UnityEngine.Object.DestroyImmediate(sprite);
                }
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                var group = settings.FindGroup(LevelToolAtlasBuild.GroupName);
                if (group.entries.Count != 1) throw new Exception("레벨툴 그룹에 불필요한 항목 포함");
                try
                {
                    foreach (var product in Products.Editor.ProductProfiles.Definitions)
                    {
                        Products.Editor.ProductProfiles.ApplyContentPaths(product);
                        if (group.GetSchema<BundledAssetGroupSchema>().IncludeInBuild != product.IsTool) throw new Exception("제품 격리 실패");
                        Debug.Log("PASS UI atlas product inclusion " + product.Symbol + "=" + product.IsTool);
                    }
                }
                finally { Products.Editor.ProductProfiles.SynchronizeContentPaths(); }
                if (((SpriteAtlasImporter)AssetImporter.GetAtPath(LevelToolAtlasBuild.AtlasPath)).includeInBuild)
                    throw new Exception("Player 직접 포함 중복");
                if (Levels.Editor.BoardAtlasPrebuild.SourcePaths().Any(path => path.StartsWith("Assets/Textures/LevelTool/")))
                    throw new Exception("게임 아틀라스 소스 중복");
                foreach (var product in Products.Editor.ProductProfiles.Definitions.Where(value => !value.IsTool))
                {
                    var profile = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>(product.ProfilePath);
                    foreach (var scene in profile.scenes.Where(value => value.enabled))
                        if (AssetDatabase.GetDependencies(scene.path, true).Any(path => path.StartsWith("Assets/Textures/LevelTool/")))
                            throw new Exception("게임 씬에 레벨툴 이미지 의존성 포함");
                }
                Debug.Log("PASS LevelTool UI: 4 sprites, power-of-two, 9-slice, atlas preview, product isolation; no Player or bundle build");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
