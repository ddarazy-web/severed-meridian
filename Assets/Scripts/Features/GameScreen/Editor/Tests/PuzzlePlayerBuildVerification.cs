using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameScreen.Editor
{
    public static class PuzzlePlayerBuildVerification
    {
        private const string Output = "Logs/PuzzleIntegrationVerification/";

        [MenuItem("Tools/Match/플레이어 빌드 씬 제외 검증")]
        public static void VerifyExclusion()
        {
            const string folder = "Assets/__PuzzlePlayerExclusionVerification";
            if (AssetDatabase.IsValidFolder(folder)) throw new InvalidOperationException("검증 폴더가 이미 있습니다.");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 실행하세요.");
            var results = new List<string>();
            Scene previous = SceneManager.GetActiveScene();
            Scene fixture = default;
            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
            try
            {
                AssetDatabase.CreateFolder("Assets", "__PuzzlePlayerExclusionVerification");
                fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var owner = new GameObject("Forbidden reference", typeof(RectTransform), typeof(Button));
                SceneManager.MoveGameObjectToScene(owner, fixture);
                var serialized = new SerializedObject(owner.GetComponent<Button>());
                SerializedProperty calls = serialized.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                calls.arraySize = 1;
                calls.GetArrayElementAtIndex(0).FindPropertyRelative("m_Target").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                string direct = folder + "/Direct.unity";
                EditorSceneManager.SaveScene(fixture, direct);
                EditorBuildSettings.scenes = Array.Empty<EditorBuildSettingsScene>();
                CheckRejected(direct, "명시적 빌드 씬 직접 원본 참조 차단");
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(owner, folder + "/Indirect.prefab");
                UnityEngine.Object.DestroyImmediate(owner);
                PrefabUtility.InstantiatePrefab(prefab, fixture);
                string indirect = folder + "/Indirect.unity";
                EditorSceneManager.SaveScene(fixture, indirect);
                CheckRejected(indirect, "명시적 빌드 씬 프리팹 간접 원본 참조 차단");
                ValidateScenes(new[] { PuzzleGameAssets.ScenePath });
                results.Add("PASS 정상 게임 씬 허용");
                void CheckRejected(string scene, string label)
                {
                    bool rejected = false;
                    try { ValidateScenes(new[] { scene }); }
                    catch (BuildFailedException) { rejected = true; }
                    results.Add((rejected ? "PASS " : "FAIL ") + label);
                }
            }
            catch (Exception error) { results.Add("FAIL " + error); }
            finally
            {
                EditorBuildSettings.scenes = previousScenes;
                if (fixture.IsValid() && fixture.isLoaded) EditorSceneManager.CloseScene(fixture, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                AssetDatabase.DeleteAsset(folder);
                Directory.CreateDirectory(Output);
                File.WriteAllLines(Output + "build-exclusion-results.txt", results);
            }
        }

        internal static void ValidateScenes(string[] scenes)
        {
            EditorBuildSettingsScene[] previous = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = scenes.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
                LevelPackBuild.ValidateExclusion(AddressableAssetSettingsDefaultObject.Settings);
            }
            finally { EditorBuildSettings.scenes = previous; }
        }

        internal static T WithAddressablesSettings<T>(Func<T> action)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (EditorUtility.IsDirty(settings)) throw new InvalidOperationException("미저장 Addressables 설정을 보존하기 위해 중단합니다.");
            int playerBuilder = settings.ActivePlayerDataBuilderIndex;
            int playBuilder = settings.ActivePlayModeDataBuilderIndex;
            string profile = settings.activeProfileId;
            var buildWithPlayer = settings.BuildAddressablesWithPlayerBuild;
            string remoteCatalog;
            // 공개 getter는 빈 경로를 초기화하므로 직렬화된 원래 값을 읽는다.
            using (var data = new SerializedObject(settings))
                remoteCatalog = data.FindProperty("m_RemoteCatalogBuildPath.m_Id").stringValue;
            try { return action(); }
            finally
            {
                settings.ActivePlayerDataBuilderIndex = playerBuilder;
                settings.ActivePlayModeDataBuilderIndex = playBuilder;
                settings.activeProfileId = profile;
                settings.BuildAddressablesWithPlayerBuild = buildWithPlayer;
                using (var data = new SerializedObject(settings))
                {
                    data.FindProperty("m_RemoteCatalogBuildPath.m_Id").stringValue = remoteCatalog;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                // 생성 팩/아틀라스 등록은 유지하고 사용자의 실행 옵션만 되돌린다.
                EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            }
        }

        [MenuItem("Tools/Match/빌드 설정 복원 검증")]
        public static void VerifySettingsRestoration()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (EditorUtility.IsDirty(settings)) throw new InvalidOperationException("미저장 Addressables 설정이 있습니다.");
            string original = EditorJsonUtility.ToJson(settings);
            int originalBuilder = settings.ActivePlayerDataBuilderIndex;
            var results = new List<string>();
            try
            {
                foreach (bool fail in new[] { false, true })
                {
                    int alternate = settings.DataBuilders.FindIndex(builder => builder is UnityEditor.AddressableAssets.Build.DataBuilders.BuildScriptPackedMode && !(builder is LevelPackAddressablesBuilder));
                    if (alternate < 0) throw new InvalidOperationException("검증용 기본 콘텐츠 빌더가 없습니다.");
                    settings.ActivePlayerDataBuilderIndex = alternate;
                    settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
                    using (var data = new SerializedObject(settings))
                    {
                        data.FindProperty("m_RemoteCatalogBuildPath.m_Id").stringValue = "";
                        data.ApplyModifiedPropertiesWithoutUndo();
                    }
                    EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
                    try
                    {
                        WithAddressablesSettings(() =>
                        {
                            settings.ActivePlayerDataBuilderIndex = originalBuilder;
                            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                            _ = settings.RemoteCatalogBuildPath;
                            EditorUtility.SetDirty(settings);
                            AssetDatabase.SaveAssetIfDirty(settings);
                            if (fail) throw new InvalidOperationException("검증용 빌드 실패");
                            return 0;
                        });
                    }
                    catch (InvalidOperationException error) when (error.Message == "검증용 빌드 실패") { }
                    using var restored = new SerializedObject(settings);
                    bool pass = settings.ActivePlayerDataBuilderIndex == alternate &&
                        settings.BuildAddressablesWithPlayerBuild == AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer &&
                        restored.FindProperty("m_RemoteCatalogBuildPath.m_Id").stringValue == "" && !EditorUtility.IsDirty(settings);
                    results.Add((pass ? "PASS " : "FAIL ") + "다른 Addressables 초기 선택 복원/실패=" + fail);
                }
                EditorUtility.SetDirty(settings);
                bool invoked = false, rejected = false;
                try { WithAddressablesSettings(() => { invoked = true; return 0; }); }
                catch (InvalidOperationException) { rejected = true; }
                results.Add((rejected && !invoked && EditorUtility.IsDirty(settings) ? "PASS " : "FAIL ") + "미저장 Addressables 설정 거절/보존");
            }
            finally
            {
                EditorJsonUtility.FromJsonOverwrite(original, settings);
                EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
                Directory.CreateDirectory(Output);
                File.WriteAllLines(Output + "build-settings-results.txt", results);
            }
        }

        [MenuItem("Tools/Match/Android 개발 빌드 검증")]
        public static void RunAndroid()
        {
            const string apk = "Builds/Stage05/Android/PuzzleStage05.apk";
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 빌드하세요.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android || !BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Android 대상/모듈을 확인하세요. 다른 플랫폼으로 대체하지 않습니다.");
            if (PlayerSettings.Android.useCustomKeystore) throw new InvalidOperationException("상용 서명 설정이 선택되어 있습니다. 설정 변경 없이 중단합니다.");
            if (File.Exists(apk)) throw new InvalidOperationException("기존 APK를 덮어쓰지 않습니다: " + apk);
            if (Resources.FindObjectsOfTypeAll<LevelDefinition>().Any(d => EditorUtility.IsPersistent(d) && EditorUtility.IsDirty(d)))
                throw new InvalidOperationException("미저장 레벨 에셋을 보존하기 위해 중단합니다.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("미저장 씬을 보존하기 위해 중단합니다.");
            EditorBuildSettingsScene[] previousScenes = EditorBuildSettings.scenes;
            bool previousBundle = EditorUserBuildSettings.buildAppBundle;
            bool previousExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            bool previousLayout = ProjectConfigData.GenerateBuildLayout;
            var previousFormat = ProjectConfigData.BuildLayoutReportFileFormat;
            var results = new List<string>();
            try
            {
                Directory.CreateDirectory(Output); Directory.CreateDirectory(Path.GetDirectoryName(apk));
                string[] scenes = { PuzzleGameAssets.ScenePath };
                ValidateScenes(scenes);
                EditorBuildSettings.scenes = scenes.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                ProjectConfigData.GenerateBuildLayout = true;
                ProjectConfigData.BuildLayoutReportFileFormat = ProjectConfigData.ReportFileFormat.JSON;
                File.WriteAllText(Output + "android-build-running.txt", DateTime.UtcNow.ToString("O"));
                BuildReport report = WithAddressablesSettings(() => BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes, target = BuildTarget.Android, locationPathName = apk,
                    options = BuildOptions.Development | BuildOptions.DetailedBuildReport
                }));
                File.WriteAllText(Output + "android-build-summary.txt", "result=" + report.summary.result + "\nerrors=" + report.summary.totalErrors + "\nwarnings=" + report.summary.totalWarnings + "\nbytes=" + report.summary.totalSize + "\nduration=" + report.summary.totalTime + "\nscenes=" + string.Join(";", scenes) + "\n");
                File.WriteAllLines(Output + "android-build-files.txt", report.GetFiles().Select(f => f.role + "\t" + f.path + "\t" + f.size));
                string[] assets = report.packedAssets.SelectMany(p => p.contents).Select(a => a.sourceAssetPath).Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderBy(p => p).ToArray();
                File.WriteAllLines(Output + "android-packed-assets.txt", assets);
                if (report.summary.result != BuildResult.Succeeded || !File.Exists(apk)) throw new Exception("Android 개발 빌드 실패: " + report.summary.result);
                if (assets.Length == 0) throw new Exception("플레이어 포함 에셋 보고서가 비어 있습니다.");
                if (assets.Any(p => AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(LevelDefinition))) throw new Exception("플레이어에 원본 LevelDefinition이 포함되었습니다.");
                using SHA256 sha = SHA256.Create();
                using FileStream stream = File.OpenRead(apk);
                File.WriteAllText(Output + "android-apk-sha256.txt", BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") + "  " + apk);
                results.Add("PASS Android Development APK");
                results.Add("PASS 플레이어 포함 에셋에 원본 LevelDefinition 없음");
            }
            catch (Exception error) { results.Add("FAIL " + error); Debug.LogException(error); }
            finally
            {
                EditorBuildSettings.scenes = previousScenes;
                EditorUserBuildSettings.buildAppBundle = previousBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExport;
                ProjectConfigData.GenerateBuildLayout = previousLayout;
                ProjectConfigData.BuildLayoutReportFileFormat = previousFormat;
                File.WriteAllLines(Output + "android-build-results.txt", results);
                if (File.Exists(Output + "android-build-running.txt")) File.Delete(Output + "android-build-running.txt");
            }
        }
    }
}
