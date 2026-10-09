using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace Products.Editor
{
    public sealed class ProductDefinition
    {
        public readonly string Name, Symbol, Scene, Output, ProductName, ApplicationId;
        public readonly BuildTarget Target;
        public bool IsTool => Symbol == "PRODUCT_LEVEL_EDITOR";
        public string ProfilePath => "Assets/Settings/BuildProfiles/" + Name + ".asset";
        public ProductDefinition(string name, string symbol, BuildTarget target, string scene, string output, string productName, string applicationId)
        { Name = name; Symbol = symbol; Target = target; Scene = scene; Output = output; ProductName = productName; ApplicationId = applicationId; }
    }

    public static partial class ProductProfiles
    {
        public static readonly ProductDefinition[] Definitions =
        {
            new ProductDefinition("Android Game", "PRODUCT_ANDROID_GAME", BuildTarget.Android, "Assets/Scenes/PuzzleGame.unity", "Builds/Android/Game.apk", "Moon Rabbit Junk Shop", "com.twelvethwarriors.moonrabbits"),
            new ProductDefinition("iOS Game", "PRODUCT_IOS_GAME", BuildTarget.iOS, "Assets/Scenes/PuzzleGame.unity", "Builds/iOS", "Moon Rabbit Junk Shop", "com.twelvethwarriors.moonrabbits"),
            new ProductDefinition("Windows Steam Game", "PRODUCT_STEAM_GAME", BuildTarget.StandaloneWindows64, "Assets/Scenes/PuzzleGame.unity", "Builds/Steam/MoonRabbit.exe", "Moon Rabbit Junk Shop", "com.twelvethwarriors.moonrabbits"),
            new ProductDefinition("Windows Level Editor", "PRODUCT_LEVEL_EDITOR", BuildTarget.StandaloneWindows64, "Assets/Scenes/LevelTool.unity", "Builds/LevelTool/MoonRabbitLevelTool.exe", "Moon Rabbit Level Editor", "com.twelvethwarriors.moonrabbits.leveleditor")
        };

        public static ProductDefinition ValidateSelection(string[] symbols, BuildTarget target)
        {
            string[] products = symbols.Where(value => value.StartsWith("PRODUCT_", StringComparison.Ordinal)).Distinct().ToArray();
            ProductDefinition definition = products.Length == 1 ? Definitions.FirstOrDefault(value => value.Symbol == products[0]) : null;
            if (definition == null || definition.Target != target)
                throw new BuildFailedException("제품 심볼은 대상 플랫폼에 맞는 하나만 선택해야 합니다: " + string.Join(", ", products) + " / " + target);
            return definition;
        }

        public static ProductDefinition ValidateProfile(BuildProfile profile)
        {
            if (profile == null) throw new BuildFailedException("제품 Build Profile을 선택하세요.");
            SerializedObject data = new SerializedObject(profile);
            BuildTarget target = (BuildTarget)data.FindProperty("m_BuildTarget").intValue;
            ProductDefinition definition = ValidateSelection(profile.scriptingDefines, target);
            string[] scenes = profile.scenes.Where(value => value.enabled).Select(value => value.path).ToArray();
            if (!profile.overrideGlobalScenes || scenes.Length != 1 || scenes[0] != definition.Scene || AssetDatabase.LoadAssetAtPath<SceneAsset>(definition.Scene) == null)
                throw new BuildFailedException(definition.Name + " 시작 씬이 제품 계약과 다릅니다.");
            string[] globals = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target))).Split(';');
            if (globals.Any(value => value.StartsWith("PRODUCT_", StringComparison.Ordinal)))
                throw new BuildFailedException("제품 심볼은 전역 Player Settings가 아닌 Build Profile에만 둡니다.");
            return definition;
        }

        // Unity 6000.3은 프로필별 PlayerSettings 편집 API를 공개하지 않는다. 지원 멤버가 사라지면 설정을 추측하지 않고 중단한다.
        internal static UnityEngine.Object ProfileSettings(BuildProfile profile, bool create)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            PropertyInfo property = typeof(BuildProfile).GetProperty("playerSettings", flags);
            MethodInfo creator = typeof(BuildProfile).GetMethod("CreatePlayerSettingsFromGlobal", flags);
            if (property == null || creator == null) throw new BuildFailedException("현재 Unity의 Build Profile PlayerSettings API를 확인해야 합니다.");
            UnityEngine.Object value = property.GetValue(profile) as UnityEngine.Object;
            if (value == null && create) { creator.Invoke(profile, null); value = property.GetValue(profile) as UnityEngine.Object; }
            if (value == null) throw new BuildFailedException(profile.name + "에 제품별 PlayerSettings가 없습니다. 제품 구성 갱신을 실행하세요.");
            return value;
        }

        [MenuItem("Tools/Products/제품 구성 갱신 (빌드 안 함)")]
        public static void Configure()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new BuildFailedException("Addressables 설정이 없습니다.");
            foreach (ProductDefinition definition in Definitions)
            {
                BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(definition.ProfilePath);
                ValidateProfile(profile);
                SerializedObject player = new SerializedObject(ProfileSettings(profile, true));
                SerializedProperty name = player.FindProperty("productName");
                SerializedProperty identifiers = player.FindProperty("applicationIdentifier");
                if (name == null || identifiers == null || !identifiers.isArray) throw new BuildFailedException("제품별 PlayerSettings 직렬화 형식을 확인해야 합니다.");
                name.stringValue = definition.ProductName;
                string platform = definition.Target == BuildTarget.Android ? "Android" : definition.Target == BuildTarget.iOS ? "iPhone" : "Standalone";
                bool found = false;
                for (int index = 0; index < identifiers.arraySize; index++)
                {
                    SerializedProperty pair = identifiers.GetArrayElementAtIndex(index);
                    if (pair.FindPropertyRelative("first").stringValue != platform) continue;
                    pair.FindPropertyRelative("second").stringValue = definition.ApplicationId; found = true;
                }
                if (!found) throw new BuildFailedException("제품 식별자 플랫폼 항목이 없습니다: " + platform);
                if (definition.IsTool)
                {
                    SerializedProperty resizable = player.FindProperty("resizableWindow");
                    SerializedProperty fullscreenMode = player.FindProperty("fullscreenMode");
                    SerializedProperty width = player.FindProperty("defaultScreenWidth");
                    SerializedProperty height = player.FindProperty("defaultScreenHeight");
                    if (resizable == null || fullscreenMode == null || width == null || height == null)
                        throw new BuildFailedException("레벨툴 창 PlayerSettings 직렬화 형식을 확인해야 합니다.");
                    resizable.boolValue = true;
                    fullscreenMode.intValue = (int)FullScreenMode.Windowed;
                    width.intValue = 1280;
                    height.intValue = 800;
                }
                player.ApplyModifiedPropertiesWithoutUndo();
                typeof(BuildProfile).GetMethod("SerializePlayerSettings", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(profile, null);
                EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
                string id = settings.profileSettings.GetProfileId(definition.Name);
                if (string.IsNullOrEmpty(id)) id = settings.profileSettings.AddProfile(definition.Name, settings.activeProfileId);
                settings.profileSettings.SetValue(id, "Local.BuildPath", "[UnityEngine.AddressableAssets.Addressables.BuildPath]/" + definition.Symbol + "/[BuildTarget]");
                settings.profileSettings.SetValue(id, "Local.LoadPath", "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/" + definition.Symbol + "/[BuildTarget]");
            }
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            Debug.Log("네 제품의 설정·콘텐츠 경로 구성을 저장했습니다. 빌드는 실행하지 않았습니다.");
        }

        public static void Select(string symbol)
        {
            ProductDefinition definition = Definitions.Single(value => value.Symbol == symbol);
            BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(definition.ProfilePath);
            ValidateProfile(profile); ProfileSettings(profile, false);
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(definition.Target), definition.Target))
                throw new BuildFailedException("설치되지 않은 플랫폼 모듈: " + definition.Target);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            string id = settings.profileSettings.GetProfileId(definition.Name);
            if (string.IsNullOrEmpty(id)) throw new BuildFailedException("제품 구성 갱신을 먼저 실행하세요.");
            settings.activeProfileId = id;
            ApplyContentPaths(definition);
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            BuildProfile.SetActiveBuildProfile(profile);
            // 출력 위치는 전환한 프로필에 적용한다. Unity는 부모 폴더가 없는 상대 경로를 빈 값으로 기록하므로 절대 경로를 전달한다.
            EditorUserBuildSettings.SetBuildLocation(definition.Target, Path.GetFullPath(definition.Output));
            Debug.Log(definition.Name + " 선택: " + definition.Output + " (빌드 안 함)");
        }
        [MenuItem("Tools/Products/선택/Android 게임")] public static void Android() => Select("PRODUCT_ANDROID_GAME");
        [MenuItem("Tools/Products/선택/iOS 게임")] public static void IOS() => Select("PRODUCT_IOS_GAME");
        [MenuItem("Tools/Products/선택/Steam 게임")] public static void Steam() => Select("PRODUCT_STEAM_GAME");
        [MenuItem("Tools/Products/선택/Windows 레벨툴")] public static void LevelTool() => Select("PRODUCT_LEVEL_EDITOR");
    }
}
