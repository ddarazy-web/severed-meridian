using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Products.Editor
{
    public sealed class IosBuildPlatform : IBuildToolPlatform, IBuildToolVersionCode
    {
        public BuildTarget Target => BuildTarget.iOS;
        public string PlayerSettingsKey => "iPhone";
        public bool OutputIsDirectory => true;
        public string VersionCodeLabel => "Build Number";
        public SerializedProperty FindVersionCode(SerializedObject settings)
        {
            SerializedProperty numbers = settings.FindProperty("buildNumber");
            for (int i = 0; numbers != null && i < numbers.arraySize; i++)
            {
                SerializedProperty entry = numbers.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == PlayerSettingsKey) return entry.FindPropertyRelative("second");
            }
            return null;
        }
        public void ValidateVersionCode(string value)
        {
            if (value == null || !System.Text.RegularExpressions.Regex.IsMatch(value, @"\A[0-9]+(\.[0-9]+){0,2}\z"))
                throw new System.ArgumentException("Build Number는 숫자 또는 점으로 구분한 최대 3개의 숫자로 입력하세요. 예: 17, 1.2.3");
        }
        public void Inspect(BuildToolProfileInfo profile, List<BuildToolIssue> issues)
        {
            if (Application.platform != RuntimePlatform.OSXEditor)
                issues.Add(new BuildToolIssue(BuildToolIssueLevel.Warning, "iOS 호스트", "현재 호스트는 macOS 에디터가 아닙니다. Xcode 서명·아카이브 완료를 확인할 수 없습니다.",
                    "Unity 프로젝트 생성 지원과 macOS의 Xcode 배포 검증을 구분하세요."));
            issues.Add(new BuildToolIssue(BuildToolIssueLevel.Unknown, "iOS", "iOS 프로젝트 생성·Xcode·서명·실기 실행은 미검증입니다.", "후속 실행 단계에서 해당 호스트의 지원 범위를 확인합니다."));
        }
    }
}
