using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;

namespace Products.Editor
{
    public enum BuildToolIssueLevel { Pass, Error, Warning, Unknown }

    public sealed class BuildToolIssue
    {
        public readonly BuildToolIssueLevel Level;
        public readonly string Location, Message, Action;
        public BuildToolIssue(BuildToolIssueLevel level, string location, string message, string action)
        { Level = level; Location = location; Message = message; Action = action; }
    }

    public sealed class BuildToolReport
    {
        public readonly List<BuildToolIssue> Issues = new List<BuildToolIssue>();
        public bool HasErrors => Issues.Any(issue => issue.Level == BuildToolIssueLevel.Error);
        public bool IsDeferred => Issues.Count > 0 && Issues.All(issue => issue.Level == BuildToolIssueLevel.Unknown);
        public string Summary => IsDeferred ? "사전 검사 보류 · 실제 빌드 미검증" : HasErrors ? "차단 오류 있음 · 실제 빌드 미검증" : "확인한 설정 검사 완료 · 실제 빌드 미검증";
    }

    public static class BuildToolPreflight
    {
        public static BuildToolReport Inspect(BuildToolProfileInfo profile, BuildToolPlatforms platforms = null)
        {
            var report = new BuildToolReport();
            if (profile == null)
            {
                report.Issues.Add(new BuildToolIssue(BuildToolIssueLevel.Error, "프로필", "선택한 프로필이 없습니다.", "목록에서 프로필을 선택하세요."));
                return report;
            }
            report.Issues.AddRange(profile.ReadIssues);
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                report.Issues.Add(new BuildToolIssue(BuildToolIssueLevel.Unknown, "에디터 상태", "컴파일·갱신 또는 Play Mode 중입니다. 검사를 보류했습니다.", "편집 모드에서 처리가 끝난 뒤 다시 검사하세요."));
                return report;
            }
            ProductDefinition product = profile.Product;
            if (product == null) return report;
            if (profile.ProductName != product.ProductName || profile.ApplicationId != product.ApplicationId)
                report.Issues.Add(new BuildToolIssue(BuildToolIssueLevel.Error, profile.Path, "실제 앱 이름 또는 앱 식별자가 제품 계약과 다릅니다.", "Build Profile의 PlayerSettings와 제품 등록 값을 확인하세요."));
            else report.Issues.Add(new BuildToolIssue(BuildToolIssueLevel.Pass, profile.Path, "앱 이름·식별자 일치", "조회만 수행했습니다."));
            if (profile.IsCopy)
                report.Issues.Add(new BuildToolIssue(BuildToolIssueLevel.Warning, profile.Path, "등록 제품의 복제·별도 경로 프로필입니다.", "원본 프로필과 GUID가 다릅니다. 2단계 실행 연결 전에 사용할 프로필을 확인하세요."));
            if (!(platforms ?? BuildToolPlatforms.Default).TryGet(profile.Target, out var platform, out string reason))
            {
                report.Issues.Add(new BuildToolIssue(BuildToolIssueLevel.Error, "플랫폼 등록", reason, "중복 없이 플랫폼 처리기를 등록하세요."));
                return report;
            }
            bool supported = BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(profile.Target), profile.Target);
            report.Issues.Add(new BuildToolIssue(supported ? BuildToolIssueLevel.Pass : BuildToolIssueLevel.Error, "플랫폼 모듈",
                supported ? "현재 에디터에 대상 빌드 모듈이 있습니다." : "현재 에디터에서 대상 빌드 모듈을 지원하지 않거나 설치되지 않았습니다.",
                supported ? "모듈 유무만 확인했으며 SDK·컴파일 성공을 의미하지 않습니다." : "Unity Hub의 플랫폼 모듈과 호스트 지원을 확인하세요. 자동 설치하지 않습니다."));
            if (EditorUserBuildSettings.activeBuildTarget != profile.Target)
                report.Issues.Add(new BuildToolIssue(BuildToolIssueLevel.Unknown, "활성 플랫폼", "선택 대상은 현재 활성 플랫폼과 다릅니다. 전환하지 않았습니다.", "실행 연결 단계에서 플랫폼 전환 후 컴파일을 검증해야 합니다."));
            try
            {
                ProductProfiles.ValidateBuildOptions(BuildOptions.None);
                report.Issues.Add(new BuildToolIssue(BuildToolIssueLevel.Pass, "제품 경계", "테스트 어셈블리 포함 금지 설정 확인", "실제 전체 의존성 감사는 빌드 실행 단계에서 수행합니다."));
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings == null) throw new InvalidOperationException("Addressables 설정이 없습니다.");
                string id = settings.profileSettings.GetProfileId(product.Name);
                if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("이 제품의 Addressables 프로필이 없습니다.");
                string expectedBuild = "[UnityEngine.AddressableAssets.Addressables.BuildPath]/" + product.ContentKey;
                string expectedLoad = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/" + product.ContentKey;
                if (settings.profileSettings.GetValueByName(id, "Local.BuildPath") != expectedBuild ||
                    settings.profileSettings.GetValueByName(id, "Local.LoadPath") != expectedLoad)
                    throw new InvalidOperationException("제품별 Addressables 출력·로드 경로 계약이 다릅니다.");
                report.Issues.Add(new BuildToolIssue(BuildToolIssueLevel.Pass, "Addressables", "선택 제품의 콘텐츠 프로필·경로 확인", "활성 콘텐츠 프로필과 경로는 변경하지 않았습니다."));
            }
            catch (Exception error)
            {
                report.Issues.Add(new BuildToolIssue(BuildToolIssueLevel.Error, "공통 설정", error.Message, "제품 설정과 콘텐츠 프로필을 확인하세요. 자동 갱신하지 않았습니다."));
            }
            platform.Inspect(profile, report.Issues);
            report.Issues.Add(new BuildToolIssue(BuildToolIssueLevel.Unknown, "검증 범위", "전체 의존성·데이터 변환·실제 Player/Addressables 빌드는 수행하지 않았습니다.", "1단계는 설정 조회입니다. 실행·옵션 적용은 후속 단계에서 연결합니다."));
            return report;
        }
    }
}
