using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Build;
using UnityEditor.Build.Player;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace Products.Editor
{
    public static class AndroidLevelToolCompilationVerification
    {
        public static void Run()
        {
            try
            {
                var picker = AssetImporter.GetAtPath("Assets/Scripts/Features/LevelTool/Runtime/LevelToolDocumentPicker.java") as PluginImporter;
                if (picker == null || !picker.DefineConstraints.SequenceEqual(new[] { "PRODUCT_LEVEL_EDITOR" }) ||
                    picker.GetCompatibleWithAnyPlatform() || !picker.GetCompatibleWithPlatform(BuildTarget.Android) || picker.GetCompatibleWithEditor())
                    throw new BuildFailedException("문서 선택기 Java 플러그인은 Android 레벨툴에만 포함해야 합니다.");
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                    throw new BuildFailedException("Android 모듈이 없어 Android 레벨툴 스크립트 컴파일을 검증하지 못했습니다.");
                ProductDefinition definition = ProductProfiles.ValidateSelection(new[] { "PRODUCT_LEVEL_EDITOR" }, BuildTarget.Android);
                BuildProfile tool = AssetDatabase.LoadAssetAtPath<BuildProfile>(definition.ProfilePath);
                if (ProductProfiles.ValidateProfile(tool) != definition)
                    throw new BuildFailedException("Android 레벨툴 프로필 계약이 다릅니다.");
                ProductProfiles.ValidateBuildOptions(BuildOptions.None);
                BuildProfile selected = BuildProfile.GetActiveBuildProfile();
                BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
                string content = AddressableAssetSettingsDefaultObject.Settings.activeProfileId;
                string output = EditorUserBuildSettings.GetBuildLocation(target);
                byte[] globals = File.ReadAllBytes("ProjectSettings/ProjectSettings.asset");
                string directory = "Logs/AndroidLevelToolStage01/PlayerScripts";
                Directory.CreateDirectory(directory);
                ScriptCompilationSettings input = new ScriptCompilationSettings
                {
                    target = BuildTarget.Android,
                    group = BuildTargetGroup.Android,
                    options = ScriptCompilationOptions.None,
                    extraScriptingDefines = tool.scriptingDefines
                };
                // Android Player 조건으로 스크립트만 컴파일하며 씬, 콘텐츠 번들, APK는 생성하지 않는다.
                ScriptCompilationResult result = PlayerBuildInterface.CompilePlayerScripts(input, directory);
                try
                {
                    if (result.assemblies == null || result.assemblies.Count == 0 ||
                        !result.assemblies.Any(value => Path.GetFileName(value) == "Assembly-CSharp.dll"))
                        throw new BuildFailedException("Android 레벨툴 Player 스크립트 컴파일 결과가 없습니다.");
                    foreach (string assembly in result.assemblies)
                    {
                        string name = Path.GetFileName(assembly);
                        if (!File.Exists(Path.Combine(directory, name)))
                            throw new BuildFailedException("컴파일 결과 파일이 없습니다: " + name);
                        if (name.Contains("-Editor") || name.Contains("ProjectTests"))
                            throw new BuildFailedException("Android Player 컴파일에 Editor/프로젝트 검사 어셈블리가 포함됩니다: " + name);
                    }
                    File.WriteAllLines("Logs/AndroidLevelToolStage01/android-player-script-assemblies.txt", result.assemblies);
                }
                finally { result.typeDB?.Dispose(); }
                if (BuildProfile.GetActiveBuildProfile() != selected || EditorUserBuildSettings.activeBuildTarget != target ||
                    AddressableAssetSettingsDefaultObject.Settings.activeProfileId != content || EditorUserBuildSettings.GetBuildLocation(target) != output ||
                    !globals.SequenceEqual(File.ReadAllBytes("ProjectSettings/ProjectSettings.asset")))
                    throw new BuildFailedException("스크립트 컴파일이 제품 선택 또는 전역 설정을 변경했습니다.");
                Debug.Log("AndroidLevelToolCompilationVerification PASS: Android Player scripts + PRODUCT_LEVEL_EDITOR; assemblies=" + result.assemblies.Count + "; no APK/Player/content bundle build");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
