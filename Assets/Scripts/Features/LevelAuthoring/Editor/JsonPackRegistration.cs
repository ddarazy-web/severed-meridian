using System;
using System.IO;
using System.Linq;
using Elements;
using Elements.Editor;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace LevelAuthoring.Editor
{
    public static class JsonPackRegistration
    {
        public static void Publish(string sourceFolder, Action afterRegistration = null)
        {
            string root = Path.GetFullPath(".");
            JsonPackPublication.Recover(root);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("Addressables 설정이 없습니다.");
            var levels = settings.FindGroup("Level Packs");
            var elements = settings.FindGroup("Element Data Packs");
            // 이 프로젝트의 기존 그룹을 사용한다. 등록 중 새 설정 파일을 암묵적으로 만들지 않는다.
            if (levels == null || elements == null || levels.GetSchema<BundledAssetGroupSchema>() == null || elements.GetSchema<BundledAssetGroupSchema>() == null)
                throw new InvalidOperationException("Level Packs / Element Data Packs 그룹과 번들 스키마가 필요합니다.");
            string[] assets = Directory.GetFiles("Assets/AddressableAssetsData", "*.asset", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/')).ToArray();
            foreach (string path in assets)
                if (EditorUtility.IsDirty(AssetDatabase.LoadMainAssetAtPath(path)))
                    throw new InvalidOperationException("저장되지 않은 Addressables 설정이 있습니다: " + path);
            LevelPackBuild.ValidateExclusion(settings);
            try
            {
                JsonPackPublication.PublishWithRegistration(sourceFolder, root,
                    assets.SelectMany(path => new[] { path, path + ".meta" }).ToArray(), paths =>
                    {
                        foreach (string path in paths) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                        foreach (var group in new[] { levels, elements })
                        {
                            var schema = group.GetSchema<BundledAssetGroupSchema>();
                            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                            schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                            schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                            EditorUtility.SetDirty(schema);
                        }
                        foreach (var entry in levels.entries.ToArray())
                            if ((entry.address.StartsWith("Levels/levels-", StringComparison.Ordinal) || entry.address == ContentPackGenerationCodec.Address) && !paths.Contains(entry.AssetPath))
                                settings.RemoveAssetEntry(entry.guid);
                        foreach (string path in paths)
                        {
                            var group = path == ElementContentPackBuild.OutputPath ? elements : levels;
                            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
                            entry.address = path == ElementContentPackBuild.OutputPath ? ElementContentPackCodec.Address : "Levels/" + Path.GetFileNameWithoutExtension(path);
                            EditorUtility.SetDirty(group);
                        }
                        EditorUtility.SetDirty(settings);
                        // 이동 전 그룹까지 저장하되 작업 시작 전에 깨끗한 설정만 허용한다.
                        foreach (string path in assets) AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(path));
                        afterRegistration?.Invoke();
                    }, null);
            }
            finally
            {
                // 실패 복구로 디스크가 돌아온 경우 로드된 그룹/주소도 다시 읽는다.
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                foreach (string path in assets) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
