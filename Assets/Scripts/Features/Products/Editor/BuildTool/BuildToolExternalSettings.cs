using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Products.Editor
{
    public static class BuildToolExternalSettings
    {
        public const string PreferencesPath = "UserSettings/MatchBuildTool.json";

        public static JObject Read(BuildToolProfileInfo profile, string preferencesPath)
        {
            var saved = BuildToolPreferences.Choices(preferencesPath, profile.Guid);
            var definitions = BuildToolOptions.Default.Available(profile.Product).ToArray();
            var resolved = BuildToolOptions.Default.Resolve(profile.Product, saved);
            var version = BuildToolVersionSettings.Read(profile);
            BuildToolPlatforms.Default.TryGet(profile.Target, out var platform, out _);
            var data = new JObject {
                ["profileGuid"] = profile.Guid, ["name"] = profile.Name, ["target"] = profile.Target.ToString(),
                ["output"] = BuildToolPlatforms.Default.Output(profile.Product, resolved),
                ["productName"] = profile.ProductName, ["applicationId"] = profile.ApplicationId,
                ["appVersion"] = new JObject { ["name"] = version.Name, ["nameLabel"] = "Version Name", ["nameType"] = "string",
                    ["code"] = version.Code, ["codeLabel"] = version.CodeLabel, ["codeType"] = version.CodeLabel == null ? null : "string" },
                ["options"] = new JArray(definitions.Select(option => new JObject {
                    ["id"] = option.Id, ["version"] = option.Version, ["label"] = option.Label, ["description"] = option.Tooltip,
                    ["supported"] = true, ["default"] = option.DefaultEnabled,
                    ["saved"] = saved.TryGetValue(option.Id, out bool value) ? new JValue(value) : JValue.CreateNull(),
                    ["effective"] = resolved.Single(state => state.id == option.Id).enabled,
                    ["off"] = option.DisabledLabel ?? "OFF", ["on"] = option.EnabledLabel ?? "ON",
                    ["requires"] = new JArray(option.Requires), ["conflicts"] = new JArray(option.Conflicts) })),
                ["signing"] = platform is IBuildToolExternalSigning signing ? signing.ReadSigning(profile) : null
            };
            using var hash = SHA256.Create();
            // 프로필 전체와 개인 설정도 포함해 조회 이후 외부 변경을 덮어쓰지 않는다.
            string source = data.ToString() + File.ReadAllText(profile.Path) + (File.Exists(preferencesPath) ? File.ReadAllText(preferencesPath) : "");
            data["revision"] = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(source))).Replace("-", "");
            return data;
        }

        public static JObject Save(BuildToolProfileInfo profile, JObject patch, string preferencesPath)
        {
            if (BuildToolController.Runner.IsLocked || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Busy: 빌드·컴파일·Play Mode 중에는 저장할 수 없습니다.");
            var before = Read(profile, preferencesPath);
            if ((string)patch["expectedRevision"] != (string)before["revision"])
                throw new InvalidOperationException("RevisionConflict: 설정이 변경되었습니다. 다시 조회하세요.");
            string name = (string)before["appVersion"]["name"], code = (string)before["appVersion"]["code"];
            if (patch.TryGetValue("appVersion", out var versionToken))
            {
                if (!(versionToken is JObject version)) throw new ArgumentException("appVersion은 객체여야 합니다.");
                foreach (var field in version.Properties())
                {
                    if (field.Name != "name" && field.Name != "code") throw new ArgumentException("지원하지 않는 버전 필드입니다.");
                    if (field.Value.Type != JTokenType.String) throw new ArgumentException("버전 값은 문자열이어야 합니다.");
                }
                if (version.ContainsKey("name")) name = (string)version["name"];
                if (version.ContainsKey("code"))
                {
                    if ((string)before["appVersion"]["codeLabel"] == null) throw new ArgumentException("이 플랫폼은 버전 코드를 지원하지 않습니다.");
                    code = (string)version["code"];
                }
            }
            BuildToolVersionSettings.Validate(profile, name, code);
            var choices = BuildToolPreferences.Choices(preferencesPath, profile.Guid);
            var available = BuildToolOptions.Default.Available(profile.Product).Select(option => option.Id).ToHashSet();
            if (patch.TryGetValue("options", out var optionsToken))
            {
                if (!(optionsToken is JObject options)) throw new ArgumentException("options는 객체여야 합니다.");
                foreach (var option in options.Properties())
                {
                    if (!available.Contains(option.Name) || option.Value.Type != JTokenType.Boolean) throw new ArgumentException("지원하지 않는 옵션 또는 값입니다.");
                    choices[option.Name] = (bool)option.Value;
                }
            }
            BuildToolOptions.Default.Resolve(profile.Product, choices);
            byte[] asset = File.ReadAllBytes(profile.Path);
            byte[] preferences = File.Exists(preferencesPath) ? File.ReadAllBytes(preferencesPath) : null;
            // Unity의 SaveAssetIfDirty는 쓰기 실패를 예외 대신 로그로만 보고할 수 있다.
            if (patch.ContainsKey("appVersion"))
                using (File.Open(profile.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)) { }
            try
            {
                if (patch.ContainsKey("appVersion"))
                {
                    BuildToolVersionSettings.Save(profile, name, code);
                    var expected = BuildToolVersionSettings.Read(profile);
                    if ((expected.Name != (string)before["appVersion"]["name"] || expected.Code != (string)before["appVersion"]["code"]) &&
                        File.ReadAllBytes(profile.Path).SequenceEqual(asset)) throw new IOException("프로필 파일에 버전을 저장하지 못했습니다.");
                    AssetDatabase.ImportAsset(profile.Path, ImportAssetOptions.ForceUpdate);
                    profile.Profile = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>(profile.Path);
                    var persisted = BuildToolVersionSettings.Read(profile);
                    if (persisted.Name != expected.Name || persisted.Code != expected.Code) throw new IOException("저장된 프로필 버전이 요청과 다릅니다.");
                }
                if (patch.ContainsKey("options")) BuildToolPreferences.SaveChoices(preferencesPath, profile.Guid, choices);
                return Read(profile, preferencesPath);
            }
            catch (Exception failure)
            {
                try
                {
                    // 저장 도중 실패한 경우에만 자신이 변경한 파일을 원본으로 복구한다.
                    if (!File.ReadAllBytes(profile.Path).SequenceEqual(asset))
                        File.WriteAllBytes(profile.Path, asset);
                    if (patch.ContainsKey("appVersion"))
                    {
                        EditorUtility.ClearDirty(profile.Profile);
                        AssetDatabase.ImportAsset(profile.Path, ImportAssetOptions.ForceUpdate);
                        profile.Profile = AssetDatabase.LoadAssetAtPath<UnityEditor.Build.Profile.BuildProfile>(profile.Path);
                    }
                    if (preferences == null) { if (File.Exists(preferencesPath)) File.Delete(preferencesPath); }
                    else if (!File.ReadAllBytes(preferencesPath).SequenceEqual(preferences)) File.WriteAllBytes(preferencesPath, preferences);
                }
                catch (Exception recovery) { throw new IOException("RecoveryRequired: 저장 실패 후 복원 실패. " + failure.Message + " / " + recovery.Message, failure); }
                throw;
            }
        }
    }
}
