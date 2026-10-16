using System.Collections.Generic;
using UnityEditor;

namespace Products.Editor
{
    public sealed class AndroidBuildPlatform : IBuildToolPlatform, IBuildToolPlatformSettings, IBuildToolBuildSettings
    {
        public BuildTarget Target => BuildTarget.Android;
        public string PlayerSettingsKey => "Android";
        public bool OutputIsDirectory => false;
        public string Output(ProductDefinition product, IReadOnlyList<BuildToolOptionState> options) => BuildToolAndroidBundle.Output(product, options);
        public void Apply(BuildToolRequest request) => BuildToolAndroidBundle.Apply(request, new BuildToolRunStore(BuildToolRunStore.DefaultRoot));
        public void Restore(BuildToolRequest request) => BuildToolAndroidBundle.Restore(request, new BuildToolRunStore(BuildToolRunStore.DefaultRoot));
        public void DrawSettings(UnityEngine.UIElements.VisualElement parent, BuildToolProfileInfo profile, System.Action changed)
            => BuildToolAndroidSigning.Draw(parent, profile, changed);
        public void Inspect(BuildToolProfileInfo profile, List<BuildToolIssue> issues)
        {
            var data = new SerializedObject(profile.Profile);
            var platform = data.FindProperty("m_PlatformBuildProfile");
            if (platform?.FindPropertyRelative("m_ExportAsGoogleAndroidProject")?.boolValue == true)
                issues.Add(new BuildToolIssue(BuildToolIssueLevel.Error, "Android 출력", "Gradle 프로젝트 내보내기가 켜져 있어 APK/AAB 파일을 만들 수 없습니다.", "프로필의 Export Project를 끄세요."));
            issues.Add(new BuildToolIssue(BuildToolIssueLevel.Unknown, "Android", "SDK·JDK·NDK·서명과 APK/AAB 생성은 확인하지 않았습니다.",
                "플랫폼 전환·실행 단계에서 확인합니다. GPG·플러그인 옵션은 아직 등록되지 않았습니다."));
        }
    }
}
