using System.Collections.Generic;
using UnityEditor;

namespace Products.Editor
{
    public sealed class AndroidBuildPlatform : IBuildToolPlatform
    {
        public BuildTarget Target => BuildTarget.Android;
        public string PlayerSettingsKey => "Android";
        public bool OutputIsDirectory => false;
        public void Inspect(BuildToolProfileInfo profile, List<BuildToolIssue> issues)
        {
            if (EditorUserBuildSettings.buildAppBundle)
                issues.Add(new BuildToolIssue(BuildToolIssueLevel.Error, "Android 출력", "현재 App Bundle 설정은 고정 APK 출력 계약과 다릅니다.", "APK 설정을 사용하세요. AAB 선택 옵션은 아직 제공하지 않습니다."));
            issues.Add(new BuildToolIssue(BuildToolIssueLevel.Unknown, "Android", "SDK·JDK·NDK·서명과 APK/AAB 생성은 확인하지 않았습니다.",
                "플랫폼 전환·실행 단계에서 확인합니다. GPG·플러그인 옵션은 아직 등록되지 않았습니다."));
        }
    }
}
