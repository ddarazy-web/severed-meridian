using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;

namespace Products.Editor
{
    public sealed class BuildToolAndroidBundle : IBuildToolOptionHandler
    {
        public const string Id = "android-app-bundle";
        public BuildToolOptionDefinition Definition { get; } = new BuildToolOptionDefinition
        {
            Id = Id, Label = "Android 빌드 형식", DisabledLabel = "APK", EnabledLabel = "AAB", DefaultEnabled = false,
            Tooltip = "APK: 기기에 직접 설치할 파일. AAB: Google Play에 업로드할 App Bundle. 출력 확장자도 함께 변경되며 선택은 프로필별로 기억합니다.",
            AppliesTo = product => product.Target == BuildTarget.Android
        };
        public bool Handles(BuildToolOptionStage stage) => stage == BuildToolOptionStage.BeforeBuild;
        public void Execute(BuildToolOptionStage stage, BuildToolOptionContext context) => context.Request.androidAppBundle = context.Enabled;

        public static string Output(ProductDefinition product, IReadOnlyList<BuildToolOptionState> options)
        {
            string output = product.Output;
            if (product.Symbol == "PRODUCT_ANDROID_GAME" || product.IsTool)
            {
                var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(product.ProfilePath);
                using var settings = new SerializedObject(ProductProfiles.ProfileSettings(profile, false));
                // 전역 PlayerSettings 대신 해당 제품 프로필의 버전을 사용한다.
                string version = settings.FindProperty("bundleVersion").stringValue;
                foreach (char invalid in Path.GetInvalidFileNameChars()) version = version.Replace(invalid, '_');
                int code = settings.FindProperty("AndroidBundleVersionCode").intValue;
                string prefix = product.IsTool ? "aos_moonrabbit_level_tool" : "moon_rabbit_junk";
                output = Path.Combine(Path.GetDirectoryName(output), $"{prefix}_{version}_{code}.apk");
            }
            return Path.ChangeExtension(output, options != null && options.Any(option => option.id == Id && option.enabled) ? ".aab" : ".apk");
        }

        internal static void Apply(BuildToolRequest request, BuildToolRunStore store)
        {
            if (request.androidAppBundleBefore.HasValue) throw new InvalidOperationException("이전 Android 형식 변경을 먼저 정리해야 합니다.");
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(request.profilePath);
            var data = new SerializedObject(profile);
            var property = data.FindProperty("m_PlatformBuildProfile")?.FindPropertyRelative("m_BuildAppBundle");
            if (property == null) throw new InvalidOperationException("Android 빌드 형식 설정 API를 확인해야 합니다.");
            request.androidAppBundleBefore = property.boolValue;
            // 중단돼도 원복할 수 있도록 변경 전에 기록한다. 비밀번호는 기록에 포함하지 않는다.
            store.Save(request);
            property.boolValue = request.androidAppBundle ?? false;
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
        }

        internal static void Restore(BuildToolRequest request, BuildToolRunStore store)
        {
            if (!request.androidAppBundleBefore.HasValue) return;
            var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(request.profilePath);
            var data = new SerializedObject(profile);
            var property = data.FindProperty("m_PlatformBuildProfile")?.FindPropertyRelative("m_BuildAppBundle");
            if (property == null) throw new InvalidOperationException("Android 빌드 형식 원복 대상을 확인하세요.");
            property.boolValue = request.androidAppBundleBefore.Value;
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            request.androidAppBundleBefore = null; store.Save(request);
        }
    }
}
