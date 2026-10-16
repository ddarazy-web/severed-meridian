using System;
using System.Globalization;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine.UIElements;

namespace Products.Editor
{
    public static class BuildToolVersionSettings
    {
        public sealed class Values
        {
            public string Name, Code, CodeLabel;
        }

        private static IBuildToolVersionCode CodeSettings(BuildToolProfileInfo profile)
        {
            if (!BuildToolPlatforms.Default.TryGet(profile.Target, out IBuildToolPlatform platform, out string reason)) throw new InvalidOperationException(reason);
            return platform as IBuildToolVersionCode;
        }

        private static SerializedProperty CodeProperty(SerializedObject settings, IBuildToolVersionCode platform)
        {
            if (platform == null) return null;
            return platform.FindVersionCode(settings) ?? throw new InvalidOperationException("프로필의 빌드 번호 저장 필드를 찾지 못했습니다.");
        }

        public static Values Read(BuildToolProfileInfo profile)
        {
            IBuildToolVersionCode platform = CodeSettings(profile);
            using var settings = new SerializedObject(ProductProfiles.ProfileSettings(profile.Profile, false));
            SerializedProperty code = CodeProperty(settings, platform);
            return new Values { Name = settings.FindProperty("bundleVersion").stringValue, CodeLabel = platform?.VersionCodeLabel,
                Code = code == null ? null : code.propertyType == SerializedPropertyType.Integer ? code.intValue.ToString(CultureInfo.InvariantCulture) : code.stringValue };
        }

        public static void Validate(BuildToolProfileInfo profile, string name, string code)
        {
            name = name?.Trim(); code = code?.Trim();
            if (string.IsNullOrEmpty(name) || name.Contains("\n") || name.Contains("\r")) throw new ArgumentException("Version Name을 한 줄로 입력하세요. 예: 1.0.0");
            CodeSettings(profile)?.ValidateVersionCode(code);
        }

        public static void Save(BuildToolProfileInfo profile, string name, string code)
        {
            if (BuildToolController.Runner.IsLocked || BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("빌드·컴파일·Play Mode 중에는 버전을 변경할 수 없습니다.");
            name = name?.Trim(); code = code?.Trim();
            Validate(profile, name, code);
            IBuildToolVersionCode platform = CodeSettings(profile);
            MethodInfo serialize = typeof(BuildProfile).GetMethod("SerializePlayerSettings", BindingFlags.Instance | BindingFlags.NonPublic);
            if (serialize == null) throw new InvalidOperationException("프로필 설정 저장 API를 확인해야 합니다.");
            using var settings = new SerializedObject(ProductProfiles.ProfileSettings(profile.Profile, false));
            SerializedProperty number = CodeProperty(settings, platform);
            settings.FindProperty("bundleVersion").stringValue = name;
            if (number != null)
            {
                if (number.propertyType == SerializedPropertyType.Integer) number.intValue = int.Parse(code, CultureInfo.InvariantCulture);
                else number.stringValue = code;
            }
            // 별도 개인 캐시 대신 빌드가 실제로 읽는 프로필에 저장하여 UI와 BAT의 값을 일치시킨다.
            settings.ApplyModifiedPropertiesWithoutUndo(); serialize.Invoke(profile.Profile, null);
            EditorUtility.SetDirty(profile.Profile); AssetDatabase.SaveAssetIfDirty(profile.Profile);
        }

        public static void Draw(VisualElement parent, BuildToolProfileInfo profile, Action changed)
        {
            var section = new VisualElement { name = "version-settings" }; parent.Add(section);
            try
            {
                Values saved = Read(profile);
                var name = new TextField("Version Name") { name = "build-version-name", value = saved.Name, isDelayed = true, tooltip = "사용자에게 표시할 앱 버전입니다. 예: 1.0.0. 자동으로 증가하지 않습니다." };
                section.Add(name);
                TextField code = null;
                if (saved.CodeLabel != null)
                {
                    code = new TextField(saved.CodeLabel) { name = "build-version-code", value = saved.Code, isDelayed = true,
                        tooltip = "스토어에서 빌드를 구분하는 번호입니다. 해당 프로필에만 저장하며 자동으로 증가하지 않습니다." };
                    section.Add(code);
                }
                var help = new Label("버전은 Enter 또는 다른 항목 선택 시 프로필에 자동 저장됩니다."); help.AddToClassList("caption"); section.Add(help);
                var feedback = new HelpBox("", HelpBoxMessageType.Error); feedback.style.display = DisplayStyle.None; section.Add(feedback);
                Action persist = () =>
                {
                    try { Save(profile, name.value, code?.value); changed(); }
                    catch (Exception error)
                    {
                        name.SetValueWithoutNotify(saved.Name); code?.SetValueWithoutNotify(saved.Code);
                        feedback.text = "저장하지 않았습니다. " + error.Message; feedback.style.display = DisplayStyle.Flex;
                    }
                };
                name.RegisterValueChangedCallback(_ => persist()); code?.RegisterValueChangedCallback(_ => persist());
                section.SetEnabled(!BuildToolController.Runner.IsLocked);
            }
            catch (Exception error) { section.Add(new HelpBox(error.Message, HelpBoxMessageType.Error)); }
        }
    }
}
