using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Products.Editor
{
    // UI와 같은 실행기를 구동하며, 도메인 재로드 후에는 저장한 요청 ID만 감시한다.
    [InitializeOnLoad]
    public static class BuildToolBatch
    {
        private const string InputKey = "Match.BuildTool.Batch.Input";
        private const string IdKey = "Match.BuildTool.Batch.Id";
        private sealed class Input
        {
            public int version;
            public string operation, profile;
            public Dictionary<string, bool> options = new Dictionary<string, bool>();
            public bool useSavedSettings = true;
        }

        static BuildToolBatch() { EditorApplication.update += Poll; }

        public static void Run()
        {
            string path = null;
            try
            {
                if (!UnityEngine.Application.isBatchMode) throw new InvalidOperationException("외부 빌드 진입점은 batchmode 전용입니다.");
                if (SessionState.GetString(InputKey, "").Length > 0) return;
                string[] arguments = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(arguments, "-matchBuildRequest");
                if (index < 0 || index + 1 >= arguments.Length) throw new ArgumentException("외부 빌드 요청 파일이 필요합니다.");
                path = Path.GetFullPath(arguments[index + 1]);
                var document = JObject.Parse(File.ReadAllText(path));
                if ((int?)document["version"] == 2)
                {
                    var result = ExecuteSettings(document);
                    Finish(path, 0, result); return;
                }
                var input = document.ToObject<Input>();
                if (input == null || input.version != 1) throw new InvalidDataException("지원하지 않는 외부 빌드 요청입니다.");
                if (input.operation == "list")
                {
                    var profiles = ProductProfiles.Definitions.Select(product => new
                    {
                        name = product.Name, path = product.ProfilePath, target = product.Target.ToString(), output = product.Output,
                        options = BuildToolOptions.Default.Available(product).Select(option => new
                        { id = option.Id, label = option.Label, description = option.Tooltip, defaultEnabled = option.DefaultEnabled,
                            off = option.DisabledLabel ?? "OFF", on = option.EnabledLabel ?? "ON" }).ToArray()
                    }).ToArray();
                    Finish(path, 0, new { profiles }); return;
                }
                if (input.operation != "build" && input.operation != "check") throw new ArgumentException("지원하지 않는 실행 방식입니다.");
                var productDefinition = ProductProfiles.Definitions.SingleOrDefault(product => product.Name == input.profile);
                if (productDefinition == null) throw new ArgumentException("등록된 프로필 이름을 확인하세요: " + input.profile);
                string guid = AssetDatabase.AssetPathToGUID(productDefinition.ProfilePath);
                var backend = new UnityProfileBuildBackend();
                var profile = backend.Read(guid);
                backend.Validate(profile);
                var choices = input.useSavedSettings ? BuildToolPreferences.Choices("UserSettings/MatchBuildTool.json", guid) : new Dictionary<string, bool>();
                var available = BuildToolOptions.Default.Available(productDefinition).Select(option => option.Id).ToHashSet();
                foreach (var choice in input.options ?? new Dictionary<string, bool>())
                {
                    if (!available.Contains(choice.Key)) throw new ArgumentException("이 제품에서 사용할 수 없는 옵션: " + choice.Key);
                    choices[choice.Key] = choice.Value;
                }
                var resolved = BuildToolOptions.Default.Resolve(productDefinition, choices);
                if (input.operation == "check")
                {
                    Finish(path, 0, new { profile = input.profile, output = BuildToolPlatforms.Default.Output(productDefinition, resolved),
                        appVersion = BuildToolVersionSettings.Read(profile),
                        options = resolved.Select(option => new { option.id, option.enabled }).ToArray(),
                        scope = "설정 사전 검사만 완료. 플랫폼 전환·서명·실제 빌드 성공 검증은 아님." }); return;
                }
                var runner = BuildToolController.Runner;
                if (runner.IsLocked) throw new InvalidOperationException("미완료 빌드가 있습니다. 에디터 빌드 도구에서 결과 확인·정리 후 다시 실행하세요.");
                if (profile.Target == BuildTarget.Android) BuildToolAndroidSigning.RestoreForExternalBuild(profile);
                runner.Start(guid, true, choices);
                SessionState.SetString(IdKey, runner.Current.id);
                SessionState.SetString(InputKey, path);
            }
            catch (Exception error) { Finish(path, 2, new { error = error.Message }); }
        }

        public static JObject ExecuteSettings(JObject input)
        {
            if ((int?)input["version"] != 2 || input["requestId"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)input["requestId"]))
                throw new ArgumentException("InvalidRequest: version와 requestId를 확인하세요.");
            string operation = (string)input["operation"];
            if (operation != "settings" && operation != "save-settings") throw new ArgumentException("UnsupportedOperation: 지원하지 않는 요청입니다.");
            string[] fields = operation == "settings" ? new[] { "version", "requestId", "operation", "profileGuid" } :
                new[] { "version", "requestId", "operation", "profileGuid", "expectedRevision", "appVersion", "options" };
            if (input.Properties().Any(p => !fields.Contains(p.Name))) throw new ArgumentException("InvalidRequest: 지원하지 않는 필드입니다.");
            var profiles = ProductProfiles.Definitions.Select(product => new UnityProfileBuildBackend().Read(AssetDatabase.AssetPathToGUID(product.ProfilePath))).ToArray();
            string guid = (string)input["profileGuid"];
            if (operation == "settings" && guid == null)
                return new JObject { ["profiles"] = new JArray(profiles.Select(p => BuildToolExternalSettings.Read(p, BuildToolExternalSettings.PreferencesPath))) };
            var profile = profiles.SingleOrDefault(p => p.Guid == guid) ?? throw new ArgumentException("UnknownProfile: 등록된 프로필 GUID가 아닙니다.");
            return operation == "settings" ? BuildToolExternalSettings.Read(profile, BuildToolExternalSettings.PreferencesPath) :
                BuildToolExternalSettings.Save(profile, input, BuildToolExternalSettings.PreferencesPath);
        }

        private static void Poll()
        {
            string path = SessionState.GetString(InputKey, "");
            if (path.Length == 0) return;
            try
            {
                var runner = BuildToolController.Runner;
                var request = runner.Current;
                if (runner.StoreError != null) throw new InvalidOperationException(runner.StoreError);
                if (request == null || request.id != SessionState.GetString(IdKey, "")) throw new InvalidOperationException("외부 빌드 요청 ID가 달라 자동 실행을 중단합니다.");
                if (!request.IsTerminal && request.phase != BuildToolRunPhase.NeedsAttention) return;
                int code = request.phase == BuildToolRunPhase.Succeeded ? 0 : request.phase == BuildToolRunPhase.NeedsAttention ? 2 : 1;
                Finish(path, code, new { request.id, request.product, phase = request.phase.ToString(), request.output,
                    request.error, request.postprocessError, request.cleanupError, request.logPath, request.startedUtc, request.finishedUtc });
            }
            catch (Exception error) { Finish(path, 2, new { error = error.Message }); }
        }

        private static void Finish(string path, int code, object data)
        {
            SessionState.EraseString(InputKey); SessionState.EraseString(IdKey);
            try
            {
                if (path != null)
                {
                    var input = JObject.Parse(File.ReadAllText(path));
                    object response = new { version = 1, exitCode = code, data };
                    if ((int?)input["version"] == 2)
                    {
                        string message = code == 0 ? null : (string)JObject.FromObject(data)["error"];
                        string errorCode = message?.Split(':')[0];
                        if (errorCode != "RevisionConflict" && errorCode != "RecoveryRequired" && errorCode != "Busy" && errorCode != "UnknownProfile" && errorCode != "UnsupportedOperation") errorCode = code == 0 ? null : "InvalidRequest";
                        response = new { version = 2, requestId = (string)input["requestId"], exitCode = code, data = code == 0 ? data : null, errorCode, message };
                    }
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(path), "result.json"), JsonConvert.SerializeObject(response, Formatting.Indented));
                }
            }
            catch (Exception error) { UnityEngine.Debug.LogError("외부 빌드 결과 저장 실패: " + error.Message); code = 2; }
            EditorApplication.Exit(code);
        }
    }
}
