using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;

namespace Products.Editor
{
    public sealed class BuildToolProfileInfo
    {
        public string Guid { get; internal set; }
        public string Path { get; internal set; }
        public string Name { get; internal set; }
        public BuildProfile Profile { get; internal set; }
        public BuildTarget Target { get; internal set; } = BuildTarget.NoTarget;
        public ProductDefinition Product { get; internal set; }
        public string[] Symbols { get; internal set; } = Array.Empty<string>();
        public string[] Scenes { get; internal set; } = Array.Empty<string>();
        public string ApplicationId { get; internal set; }
        public string ProductName { get; internal set; }
        public bool IsCopy => Product != null && !string.Equals(Path, Product.ProfilePath, StringComparison.OrdinalIgnoreCase);
        public List<BuildToolIssue> ReadIssues { get; } = new List<BuildToolIssue>();
    }

    public static class BuildToolProfiles
    {
        public static List<BuildToolProfileInfo> Discover()
        {
            return Discover(AssetDatabase.FindAssets("t:BuildProfile", new[] { "Assets" }), AssetDatabase.LoadAssetAtPath<BuildProfile>);
        }

        internal static List<BuildToolProfileInfo> Discover(IEnumerable<string> guids, Func<string, BuildProfile> loadProfile)
        {
            return guids
                .Select(guid =>
                {
                    string path = guid;
                    try
                    {
                        path = AssetDatabase.GUIDToAssetPath(guid);
                        return Read(loadProfile(path), path, guid);
                    }
                    catch (Exception error)
                    {
                        var failed = new BuildToolProfileInfo { Guid = guid, Path = path, Name = path };
                        failed.ReadIssues.Add(new BuildToolIssue(BuildToolIssueLevel.Error, path, error.Message, "해당 에셋을 확인하세요. 다른 프로필은 계속 조회할 수 있습니다."));
                        return failed;
                    }
                }).OrderBy(item => item.Name, StringComparer.Ordinal).ThenBy(item => item.Path, StringComparer.Ordinal).ToList();
        }

        public static BuildToolProfileInfo Read(BuildProfile profile, string path, string guid)
        {
            var item = new BuildToolProfileInfo { Profile = profile, Path = path, Guid = guid, Name = profile == null ? path : profile.name };
            try
            {
                if (profile == null) throw new InvalidOperationException("Build Profile 에셋을 읽을 수 없습니다.");
                var data = new SerializedObject(profile);
                var target = data.FindProperty("m_BuildTarget");
                if (target == null) throw new InvalidOperationException("프로필의 대상 플랫폼을 읽을 수 없습니다.");
                item.Target = (BuildTarget)target.intValue;
                item.Symbols = profile.scriptingDefines ?? Array.Empty<string>();
                item.Scenes = (profile.scenes ?? Array.Empty<EditorBuildSettingsScene>()).Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
                item.Product = ProductProfiles.ValidateSelection(item.Symbols, item.Target);
                // 계약 오류가 있어도 제품 정보와 실제 PlayerSettings는 가능한 범위까지 표시한다.
                try { ProductProfiles.ValidateProfile(profile); }
                catch (Exception error) { item.ReadIssues.Add(new BuildToolIssue(BuildToolIssueLevel.Error, path, error.Message, "Build Profile의 제품 심볼·시작 씬·전역 심볼을 확인하세요.")); }
                if (BuildToolPlatforms.Default.TryGet(item.Target, out var platform, out string reason))
                {
                    var settings = new SerializedObject(ProductProfiles.ProfileSettings(profile, false));
                    var name = settings.FindProperty("productName");
                    var identifiers = settings.FindProperty("applicationIdentifier");
                    if (name == null || identifiers == null || !identifiers.isArray)
                        throw new InvalidOperationException("제품별 PlayerSettings 형식을 읽을 수 없습니다.");
                    item.ProductName = name.stringValue;
                    for (int i = 0; i < identifiers.arraySize; i++)
                    {
                        var pair = identifiers.GetArrayElementAtIndex(i);
                        if (pair.FindPropertyRelative("first").stringValue == platform.PlayerSettingsKey)
                            item.ApplicationId = pair.FindPropertyRelative("second").stringValue;
                    }
                }
                else item.ReadIssues.Add(new BuildToolIssue(BuildToolIssueLevel.Error, path, reason, "해당 플랫폼 처리기를 등록하세요."));
            }
            catch (Exception error)
            {
                item.ReadIssues.Add(new BuildToolIssue(BuildToolIssueLevel.Error, path, error.Message, "프로필과 제품 등록 정보를 확인하세요. 자동 수정하지 않았습니다."));
            }
            return item;
        }
    }
}
