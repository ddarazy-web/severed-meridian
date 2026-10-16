using System.Collections.Generic;
using UnityEditor;

namespace Products.Editor
{
    public sealed class WindowsBuildPlatform : IBuildToolPlatform
    {
        public BuildTarget Target => BuildTarget.StandaloneWindows64;
        public string PlayerSettingsKey => "Standalone";
        public bool OutputIsDirectory => false;
        public void Inspect(BuildToolProfileInfo profile, List<BuildToolIssue> issues)
        {
            issues.Add(new BuildToolIssue(BuildToolIssueLevel.Unknown, "Windows", "실행 파일 생성·배포 후 실행은 확인하지 않았습니다.",
                profile.Product.IsTool ? "레벨툴 UI·오프라인 매뉴얼의 배포 검증은 후속 단계입니다." : "Steam SDK·스토어 연결 성공은 별도 검증이 필요합니다."));
        }
    }
}
