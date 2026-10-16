using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Products.Editor
{
    public sealed class UnityProfileBuildBackend : IBuildToolBackend
    {
        public bool IsMock => false;
        public bool IsBusy => EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer;
        public bool HasCompilationErrors => EditorUtility.scriptCompilationFailed;

        public BuildToolProfileInfo Read(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return BuildToolProfiles.Read(AssetDatabase.LoadAssetAtPath<BuildProfile>(path), path, guid);
        }

        public void Validate(BuildToolProfileInfo profile)
        {
            if (profile?.Profile == null || profile.Product == null) throw new InvalidOperationException("등록된 제품 프로필을 다시 선택하세요.");
            if (profile.IsCopy) throw new InvalidOperationException("복제 프로필은 실행하지 않습니다. 등록된 원본 프로필을 선택하세요.");
            _ = BuildToolDebugMode.ProfileOptionsFactory;
            var report = BuildToolPreflight.Inspect(profile);
            if (report.HasErrors || report.IsDeferred)
                throw new InvalidOperationException(string.Join("\n", report.Issues.Where(issue => issue.Level == BuildToolIssueLevel.Error || report.IsDeferred).Select(issue => issue.Message)));
            if (!(AddressableAssetSettingsDefaultObject.Settings.ActivePlayerDataBuilder is Levels.Editor.LevelPackAddressablesBuilder))
                throw new InvalidOperationException("기존 Level MemoryPack + AssetBundles 빌더를 선택해야 합니다.");
        }

        public string Fingerprint(BuildToolProfileInfo profile)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
            {
                string source = File.ReadAllText(profile.Path) + "\n" + EditorJsonUtility.ToJson(profile.Profile) + "\n" +
                    EditorJsonUtility.ToJson(ProductProfiles.ProfileSettings(profile.Profile, false));
                return Convert.ToBase64String(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(source)));
            }
        }

        public string CaptureSelection(BuildToolRequest request)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var product = ProductProfiles.ValidateSelection(new[] { request.symbol }, request.target);
            string library = "Library/com.unity.addressables/Products/" + product.ContentKey + "/";
            return new JObject
            {
                ["before"] = CaptureRouting(request),
                ["expected"] = new JObject
                {
                    ["profile"] = request.profileGuid, ["target"] = (int)request.target,
                    ["content"] = settings.profileSettings.GetProfileId(product.Name), ["library"] = library,
                    ["report"] = library + "BuildReports/", ["state"] = library + "ContentState", ["customState"] = library + "ContentState",
                    ["tool"] = product.IsTool, ["output"] = request.output, ["mode"] = (int)AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer
                }
            }.ToString();
        }

        public void Select(BuildToolRequest request) => ProductProfiles.Select(request.symbol, request.target);

        public bool IsReady(BuildToolRequest request, out string reason)
        {
            var snapshot = JObject.Parse(request.selectionSnapshot);
            var before = (JObject)snapshot["before"]; var expected = (JObject)snapshot["expected"];
            var current = CaptureRouting(request);
            string profile = (string)current["profile"];
            if (profile != request.profileGuid && profile != (string)before["profile"])
                throw new InvalidOperationException("외부에서 활성 프로필이 변경됐습니다. 자동으로 덮어쓰지 않습니다.");
            if (profile != request.profileGuid || (int)current["target"] != (int)request.target)
            { reason = "활성 프로필·플랫폼 반영 대기"; return false; }
            foreach (string key in new[] { "content", "library", "report", "state", "customState", "tool" })
                if (!JToken.DeepEquals(current[key], expected[key]))
                    throw new InvalidOperationException("선택 후 콘텐츠 상태가 예상과 다릅니다: " + key);
            if ((int)current["mode"] != (int)before["mode"])
                throw new InvalidOperationException("실행 전에 Addressables 자동 빌드 설정이 외부에서 변경됐습니다.");
            string output = (string)current["output"];
            if (!SameOutput(output, request.output))
            {
                if (output != (string)before["output"] && !string.IsNullOrEmpty(output))
                    throw new InvalidOperationException("실행 전에 출력 설정이 외부에서 변경됐습니다.");
                // 프로필 전환 도중 재로드로 Select의 마지막 줄이 중단된 경우만 마무리한다.
                EditorUserBuildSettings.SetBuildLocation(request.target, request.output);
            }
            reason = null; return true;
        }

        public BuildToolResult Build(BuildToolRequest request)
        {
            if (request.isMock || !request.userConfirmed || !request.callStarted || request.phase != BuildToolRunPhase.Building)
                throw new InvalidOperationException("명시적으로 확인한 실제 빌드 요청이 아닙니다.");
            var profile = Read(request.profileGuid); Validate(profile);
            if (Fingerprint(profile) != (request.effectiveFingerprint ?? request.fingerprint)) throw new InvalidOperationException("실행 직전 프로필 파일이 변경됐습니다.");
            BuildPlayerOptions? debugOptions = request.debugMode.HasValue ? BuildToolDebugMode.CreateBuildOptions(request) : (BuildPlayerOptions?)null;
            BuildToolPlatforms.Default.TryGet(request.target, out var platform, out string reason);
            if (platform == null) throw new InvalidOperationException(reason);
            Directory.CreateDirectory(platform.OutputIsDirectory ? request.output : Path.GetDirectoryName(request.output));
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
            BuildReport report = debugOptions.HasValue ? BuildPipeline.BuildPlayer(debugOptions.Value) : BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
            {
                buildProfile = profile.Profile, locationPathName = request.output, options = BuildOptions.None
            });
            if (report == null) throw new InvalidDataException("Unity BuildReport가 없습니다. 결과를 확인하세요.");
            if (report.summary.result != BuildResult.Succeeded && report.summary.result != BuildResult.Cancelled && report.summary.result != BuildResult.Failed)
                throw new InvalidDataException("Unity가 확정된 빌드 결과를 제공하지 않았습니다. 결과를 확인하세요.");
            string errors = string.Join("\n", report.steps.SelectMany(step => step.messages)
                .Where(message => message.type == LogType.Error || message.type == LogType.Exception).Select(message => message.content));
            return new BuildToolResult { outcome = report.summary.result == BuildResult.Succeeded ? BuildToolRunPhase.Succeeded :
                report.summary.result == BuildResult.Cancelled ? BuildToolRunPhase.Canceled : BuildToolRunPhase.Failed,
                error = errors, bytes = report.summary.totalSize };
        }

        public void Cleanup(BuildToolRequest request)
        {
            var snapshot = JObject.Parse(request.selectionSnapshot);
            var before = (JObject)snapshot["before"]; var expected = (JObject)snapshot["expected"];
            var current = CaptureRouting(request); var errors = new List<string>();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (request.modeTouched)
                RestoreOwned("mode", current, before, expected, () =>
                {
                    settings.BuildAddressablesWithPlayerBuild = (AddressableAssetSettings.PlayerBuildOption)(int)before["mode"];
                    EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
                }, errors);
            if (request.selectionStarted && !request.selectionEstablished)
            {
                if ((string)current["profile"] == (string)before["profile"] && (int)current["target"] == (int)before["target"])
                {
                    RestoreOwned("content", current, before, expected, () => settings.activeProfileId = (string)before["content"], errors);
                    RestoreOwned("library", current, before, expected, () => Addressables.LibraryPath = (string)before["library"], errors);
                    RestoreOwned("report", current, before, expected, () => Addressables.BuildReportPath = (string)before["report"], errors);
                    RestoreOwned("tool", current, before, expected, () =>
                    {
                        var schema = settings.FindGroup(global::LevelTool.Editor.LevelToolAtlasBuild.GroupName).GetSchema<BundledAssetGroupSchema>();
                        schema.IncludeInBuild = (bool)before["tool"]; EditorUtility.SetDirty(schema); AssetDatabase.SaveAssetIfDirty(schema);
                    }, errors);
                    RestoreOwned("output", current, before, expected, () => EditorUserBuildSettings.SetBuildLocation(request.target, (string)before["output"]), errors);
                    var serialized = new SerializedObject(settings);
                    RestoreOwned("state", current, before, expected, () => serialized.FindProperty("m_ContentStateBuildPath").stringValue = (string)before["state"], errors);
                    RestoreOwned("customState", current, before, expected, () => serialized.FindProperty("m_CustomContentStateBuildPath").stringValue = (string)before["customState"], errors);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
                }
                else if ((string)current["profile"] == request.profileGuid && (int)current["target"] == (int)request.target)
                {
                    try { if (!IsReady(request, out string reason)) errors.Add(reason); }
                    catch (Exception error) { errors.Add(error.Message); }
                }
                else errors.Add("활성 제품·플랫폼이 외부에서 변경됐거나 전환이 끝나지 않았습니다. 소유권을 확인한 뒤 정리하세요.");
            }
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        }

        private static void RestoreOwned(string key, JObject current, JObject before, JObject expected, Action restore, List<string> errors)
        {
            if (JToken.DeepEquals(current[key], before[key])) return;
            if (!JToken.DeepEquals(current[key], expected[key])) { errors.Add("외부 변경을 보존했습니다: " + key); return; }
            try { restore(); } catch (Exception error) { errors.Add(key + ": " + error.Message); }
        }

        private static JObject CaptureRouting(BuildToolRequest request)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var serialized = new SerializedObject(settings);
            var active = BuildProfile.GetActiveBuildProfile();
            return new JObject
            {
                ["profile"] = active == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(active)), ["target"] = (int)EditorUserBuildSettings.activeBuildTarget,
                ["content"] = settings.activeProfileId, ["library"] = Addressables.LibraryPath, ["report"] = Addressables.BuildReportPath,
                ["state"] = serialized.FindProperty("m_ContentStateBuildPath").stringValue,
                ["customState"] = serialized.FindProperty("m_CustomContentStateBuildPath").stringValue,
                ["tool"] = settings.FindGroup(global::LevelTool.Editor.LevelToolAtlasBuild.GroupName).GetSchema<BundledAssetGroupSchema>().IncludeInBuild,
                ["output"] = EditorUserBuildSettings.GetBuildLocation(request.target), ["mode"] = (int)settings.BuildAddressablesWithPlayerBuild
            };
        }

        private static bool SameOutput(string actual, string expected) => !string.IsNullOrEmpty(actual) &&
            string.Equals(Path.GetFullPath(actual), expected, StringComparison.OrdinalIgnoreCase);
    }
}
