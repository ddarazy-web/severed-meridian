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
        public static void PublishSelected(string configurationPath, Action afterRegistration = null)
        {
            RecoverPending();
            string selected = File.ReadAllText(configurationPath);
            string folder = AuthoringSourceSelection.ReadFolder(configurationPath);
            var source = new Storage.ContentSnapshotStore(folder).Read();
            AuthoringSourceSelection.ValidateResources(source.Snapshot, true);
            Publish(folder, () =>
            {
                afterRegistration?.Invoke();
                if (File.ReadAllText(configurationPath) != selected || new Storage.ContentSnapshotStore(folder).Read().Hash != source.Hash)
                    throw new InvalidOperationException("변환 중 정식 원본 또는 선택 설정이 변경됐습니다.");
            });
        }

        public static void Publish(string sourceFolder, Action afterRegistration = null)
        {
            string root = Path.GetFullPath(".");
            RecoverPending();
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
                            if ((entry.address.StartsWith("Levels/levels-", StringComparison.Ordinal) || entry.address == ContentPackGenerationCodec.Address ||
                                entry.address == LevelLobbyIndexCodec.Address) && !paths.Contains(entry.AssetPath))
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
                RefreshRegistration(assets);
            }
        }

        private static void RecoverPending()
        {
            if (!File.Exists("Library/AuthoringPacks/publication.json")) return;
            string[] assets = Directory.GetFiles("Assets/AddressableAssetsData", "*.asset", SearchOption.AllDirectories)
                .Select(path => path.Replace('\\', '/')).ToArray();
            // 이전 세대 복구는 새 원본의 유효성에 의존하지 않는다.
            try { JsonPackPublication.Recover(Path.GetFullPath(".")); }
            finally { RefreshRegistration(assets); }
        }

        private static void RefreshRegistration(string[] assets)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            foreach (string path in assets) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            // 디스크 복원만으로는 Addressables의 비직렬화 조회 캐시가 무효화되지 않는다.
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;
            foreach (var group in settings.groups.Where(group => group != null))
                group.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, false, false);
        }
    }
}
