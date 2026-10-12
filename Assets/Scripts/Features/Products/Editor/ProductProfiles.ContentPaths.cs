using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEngine.AddressableAssets;

namespace Products.Editor
{
    public static partial class ProductProfiles
    {
        [InitializeOnLoadMethod]
        private static void RestoreContentRoutingAfterReload()
        {
            EditorApplication.delayCall += SynchronizeContentPaths;
        }

        public static void SynchronizeContentPaths()
        {
            BuildProfile profile = BuildProfile.GetActiveBuildProfile();
            ProductDefinition definition = profile == null ? null : ValidateProfile(profile);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (definition != null && settings != null)
            {
                string id = settings.profileSettings.GetProfileId(definition.Name);
                if (string.IsNullOrEmpty(id)) throw new BuildFailedException("제품 구성 갱신을 먼저 실행하세요.");
                if (settings.activeProfileId != id)
                {
                    settings.activeProfileId = id;
                    EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
                }
            }
            ApplyContentPaths(definition);
        }

        // 번들 경로뿐 아니라 settings/catalog/content-state/report의 공통 루트도 제품별로 분리한다.
        public static void ApplyContentPaths(ProductDefinition definition)
        {
            global::LevelTool.Editor.LevelToolAtlasBuild.ApplyInclusion(definition != null && definition.IsTool);
            string library = definition == null ? "Library/com.unity.addressables/" : "Library/com.unity.addressables/Products/" + definition.ContentKey + "/";
            Addressables.LibraryPath = library;
            Addressables.BuildReportPath = library + "BuildReports/";
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null) settings.ContentStateBuildPath = definition == null ? string.Empty : library + "ContentState";
        }

        public static void PrepareContentBuild()
        {
            ProductDefinition definition = ValidateProfile(BuildProfile.GetActiveBuildProfile());
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || settings.activeProfileId != settings.profileSettings.GetProfileId(definition.Name))
                throw new BuildFailedException("콘텐츠를 생성할 제품을 Tools/Products에서 먼저 선택하세요.");
            ValidateBuildOptions(BuildOptions.None);
            ApplyContentPaths(definition);
            AuditAll();
        }
    }
}
