using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEditor.Compilation;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;

namespace Products.Editor
{
    public static partial class ProductProfiles
    {
        public static string AuditAll()
        {
            StringBuilder report = new StringBuilder();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new BuildFailedException("Addressables 설정이 없습니다.");
            string[] commonRoots = AssetDatabase.GetAllAssetPaths().Where(path => (path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal)) &&
                !AssetDatabase.IsValidFolder(path) && !path.Contains("/Editor/") && (path.Contains("/Resources/") || path.StartsWith("Assets/StreamingAssets/", StringComparison.Ordinal))).ToArray();
            
            List<string> addressableRoots = new List<string>();
            foreach (AddressableAssetGroup group in settings.groups.Where(value => value != null))
            {
                BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null || !schema.IncludeInBuild) continue;
                foreach (AddressableAssetEntry entry in group.entries)
                {
                    List<AddressableAssetEntry> entries = new List<AddressableAssetEntry>();
                    entry.GatherAllAssets(entries, true, true, false);
                    addressableRoots.AddRange(entries.Select(value => value.AssetPath));
                }
            }
            foreach (ProductDefinition definition in Definitions)
            {
                BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(definition.ProfilePath);
                ValidateProfile(profile);
                SerializedObject player = new SerializedObject(ProfileSettings(profile, false));
                if (player.FindProperty("productName").stringValue != definition.ProductName)
                    throw new BuildFailedException(definition.Name + " 제품 이름/저장 경로 설정이 다릅니다.");
                SerializedProperty identifiers = player.FindProperty("applicationIdentifier");
                string platform = definition.Target == BuildTarget.Android ? "Android" : definition.Target == BuildTarget.iOS ? "iPhone" : "Standalone";
                bool identifierMatches = false;
                for (int index = 0; identifiers != null && index < identifiers.arraySize; index++)
                {
                    SerializedProperty pair = identifiers.GetArrayElementAtIndex(index);
                    if (pair.FindPropertyRelative("first").stringValue == platform && pair.FindPropertyRelative("second").stringValue == definition.ApplicationId)
                        identifierMatches = true;
                }
                if (!identifierMatches) throw new BuildFailedException(definition.Name + " 앱 식별자 구성이 다릅니다.");
                string profileId = settings.profileSettings.GetProfileId(definition.Name);
                if (string.IsNullOrEmpty(profileId)) throw new BuildFailedException(definition.Name + " 콘텐츠 프로필이 없습니다.");
                string buildPath = settings.profileSettings.GetValueByName(profileId, "Local.BuildPath");
                string loadPath = settings.profileSettings.GetValueByName(profileId, "Local.LoadPath");
                if (!buildPath.Contains("/" + definition.Symbol + "/") || !loadPath.Contains("/" + definition.Symbol + "/"))
                    throw new BuildFailedException(definition.Name + " 콘텐츠 출력/로드 경로가 분리되지 않았습니다.");
                SerializedProperty preloadedProperty = player.FindProperty("preloadedAssets");
                if (preloadedProperty == null || !preloadedProperty.isArray) throw new BuildFailedException("프로필 사전 로드 설정을 읽을 수 없습니다.");
                List<string> preloadedList = new List<string>();
                for (int index = 0; index < preloadedProperty.arraySize; index++)
                {
                    UnityEngine.Object asset = preloadedProperty.GetArrayElementAtIndex(index).objectReferenceValue;
                    if (asset != null) preloadedList.Add(AssetDatabase.GetAssetPath(asset));
                }
                string[] preloaded = preloadedList.ToArray();
                string[] roots = commonRoots.Concat(preloaded).Concat(addressableRoots).Concat(new[] { definition.Scene }).Where(value => !string.IsNullOrEmpty(value)).Distinct().ToArray();
                string[] dependencies = AssetDatabase.GetDependencies(roots, true);
                foreach (string path in dependencies) ValidateDependency(path, definition.IsTool);
                // StreamingAssets는 직렬화 참조 분석 밖에서도 그대로 복사되므로 원본 JSON을 별도로 거절한다.
                foreach (string path in commonRoots.Where(value => value.StartsWith("Assets/StreamingAssets/", StringComparison.Ordinal)))
                    if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) throw new BuildFailedException("제작 JSON을 StreamingAssets에 넣을 수 없습니다: " + path);
                PluginImporter[] plugins = PluginImporter.GetAllImporters().Where(value => value.GetCompatibleWithPlatform(definition.Target)).ToArray();
                foreach (PluginImporter plugin in plugins)
                {
                    ValidateDependency(plugin.assetPath, definition.IsTool);
                    PropertyInfo explicitProperty = typeof(PluginImporter).GetProperty("IsExplicitlyReferenced", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    report.AppendLine("  platform-compatible plugin=" + plugin.assetPath + "; native=" + plugin.isNativePlugin + "; explicitReference=" + explicitProperty?.GetValue(plugin) + "; defineConstraints=" + string.Join(",", plugin.DefineConstraints));
                }
                report.AppendLine(definition.Name + " | " + definition.Symbol + " | " + definition.Target + " | " + definition.Scene + " | " + definition.Output);
                report.AppendLine("  productName=" + definition.ProductName + "; dependencies=" + dependencies.Length + "; resources/streaming=" + commonRoots.Length + "; preloaded=" + preloaded.Length + "; addressableRoots=" + addressableRoots.Count);
                report.AppendLine("  moduleInstalled=" + BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(definition.Target), definition.Target) + "; platformCompatiblePluginCount=" + plugins.Length);
                report.AppendLine("  contentBuild=" + buildPath + "; contentLoad=" + loadPath);
            }
            UnityEditor.Compilation.Assembly[] playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            string[] testSources = playerAssemblies.SelectMany(value => value.sourceFiles).Where(value => value.Replace('\\', '/').Contains("/__ProjectTests/") || value.Replace('\\', '/').Contains("/Tests/")).ToArray();
            if (testSources.Length > 0) throw new BuildFailedException("Player 어셈블리에 테스트 소스가 포함됩니다: " + string.Join(", ", testSources));
            string[] nunitReferences = playerAssemblies.SelectMany(value => value.compiledAssemblyReferences).Where(value => Path.GetFileName(value).Equals("nunit.framework.dll", StringComparison.OrdinalIgnoreCase)).Distinct().ToArray();
            report.AppendLine("현재 활성 플랫폼 PlayerWithoutTestAssemblies: assemblies=" + playerAssemblies.Length + "; testSources=" + testSources.Length + "; nunitReferences=" + string.Join(", ", nunitReferences));
            ValidateBuildOptions(BuildOptions.None);
            Type testFilterType = TypeCache.GetTypesDerivedFrom<IFilterBuildAssemblies>().FirstOrDefault(value => value.FullName == "UnityEditor.TestRunner.TestBuildAssemblyFilter");
            if (testFilterType == null) throw new BuildFailedException("설치된 Unity Test Framework의 제품 빌드 제외 필터를 확인할 수 없습니다.");
            IFilterBuildAssemblies testFilter = (IFilterBuildAssemblies)Activator.CreateInstance(testFilterType, true);
            string[] candidateAssemblies = playerAssemblies.Select(value => value.outputPath).Concat(playerAssemblies.SelectMany(value => value.compiledAssemblyReferences)).Distinct().ToArray();
            string[] filtered = testFilter.OnFilterAssemblies(BuildOptions.None, candidateAssemblies);
            if (filtered.Any(value => value.Contains("nunit.framework") || value.Contains("UnityEngine.TestRunner")))
                throw new BuildFailedException("일반 제품 빌드의 테스트 라이브러리 제외 필터가 적용되지 않습니다.");
            report.AppendLine("설치 Unity TestBuildAssemblyFilter(BuildOptions.None) 적용 후 NUnit/TestRunner=0. 이 검사는 빌드 실행이 아닌 필터 입력/출력 검사.");
            foreach (string root in new[] { "Assets/Scripts/Features/LevelAuthoring", "Assets/Scripts/Features/LevelTool" })
                foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories).Select(value => value.Replace('\\', '/')).Where(value => !value.Contains("/Editor/")))
                    if (!File.ReadAllText(path).TrimStart('\ufeff', ' ', '\r', '\n', '\t').StartsWith("#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR", StringComparison.Ordinal))
                        throw new BuildFailedException("게임에서 제작 코드가 컴파일되지 않도록 제품 경계가 필요합니다: " + path);
            report.AppendLine("앱 식별자는 프로젝트 구성값이며 스토어 등록/예약 완료를 뜻하지 않음. 검사 범위: 설정/직렬화 의존성/플러그인 포함 규칙. Player/Addressables 빌드, iOS Xcode, Steam SDK/스토어 인증 및 실기 실행은 수행하지 않음.");
            return report.ToString();
        }

        public static void ValidateBuildOptions(BuildOptions options)
        {
            PropertyInfo testRunner = typeof(PlayerSettings).GetProperty("playModeTestRunnerEnabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (testRunner == null || (bool)testRunner.GetValue(null) || (options & BuildOptions.IncludeTestAssemblies) != 0)
                throw new BuildFailedException("제품 빌드는 테스트 어셈블리 포함 및 Play Mode Test Runner 모드를 사용할 수 없습니다.");
        }

        public static void ValidateDependency(string path, bool isTool)
        {
            string normalized = path.Replace('\\', '/');
            Type type = AssetDatabase.GetMainAssetTypeAtPath(normalized);
            string[] authoringTypes = { "LevelDefinition", "ElementDefinitionAsset", "ElementCatalogAsset", "ElementVisualCatalogAsset", "TutorialFlowDefinition", "TutorialUserSampleDefinition", "LevelShapePreset" };
            if ((type != null && authoringTypes.Contains(type.Name)) || normalized.Contains("/__ProjectTests/") || normalized.Contains("/Tests/") || normalized.StartsWith("ContentData/", StringComparison.Ordinal) ||
                (!isTool && (normalized.Contains("/LevelTool/") || normalized.Contains("/LevelAuthoring/") || normalized == "Assets/Scenes/LevelTool.unity")))
                throw new BuildFailedException("제품에 포함할 수 없는 제작 원본/도구/테스트 의존성: " + path);
        }
    }

    public sealed class ProductBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -2000;
        public void OnPreprocessBuild(BuildReport report)
        {
            ProductProfiles.ValidateBuildOptions(report.summary.options);
            ProductDefinition definition = ProductProfiles.ValidateProfile(BuildProfile.GetActiveBuildProfile());
            if (definition.Target != report.summary.platform || !string.Equals(Path.GetFullPath(definition.Output), Path.GetFullPath(report.summary.outputPath), StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("선택한 제품의 플랫폼/출력 경로가 다릅니다. Tools/Products에서 제품을 선택하세요.");
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || settings.activeProfileId != settings.profileSettings.GetProfileId(definition.Name))
                throw new BuildFailedException("Build Profile과 Addressables 제품 선택이 다릅니다. Tools/Products에서 제품을 선택하세요.");
            ProductProfiles.ApplyContentPaths(definition);
            ProductProfiles.AuditAll();
        }
    }
}
