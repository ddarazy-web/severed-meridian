using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Products.Editor
{
    public static class BuildToolExternalOperations
    {
        public static string RequestDirectory(string requestId)
        {
            if (requestId == null || !Regex.IsMatch(requestId, @"\A[a-zA-Z0-9_-]{1,80}\z")) throw new ArgumentException("InvalidRequest: requestId를 확인하세요.");
            return Path.GetFullPath(Path.Combine("Library", "MatchBuildApp", requestId));
        }
        public static BuildToolProfileInfo Profile(JObject input)
        {
            RequestDirectory((string)input["requestId"]);
            if ((int?)input["version"] != 2) throw new ArgumentException("InvalidRequest: version을 확인하세요.");
            string guid = (string)input["profileGuid"];
            var product = ProductProfiles.Definitions.SingleOrDefault(p => AssetDatabase.AssetPathToGUID(p.ProfilePath) == guid)
                ?? throw new ArgumentException("UnknownProfile: 등록된 프로필이 아닙니다.");
            var profile = new UnityProfileBuildBackend().Read(guid);
            if ((string)BuildToolExternalSettings.Read(profile, BuildToolExternalSettings.PreferencesPath)["revision"] != (string)input["expectedRevision"])
                throw new InvalidOperationException("RevisionConflict: 설정이 변경되었습니다. 다시 조회하세요.");
            return profile;
        }
        private static void ValidateFields(JObject input, params string[] extra)
        {
            string[] common = { "version", "requestId", "operation", "profileGuid", "expectedRevision" };
            if (input.Properties().Any(p => !common.Contains(p.Name) && !extra.Contains(p.Name))) throw new ArgumentException("InvalidRequest: 지원하지 않는 필드입니다.");
        }
        public static JObject Check(JObject input)
        {
            ValidateFields(input);
            var profile = Profile(input);
            new UnityProfileBuildBackend().Validate(profile);
            var choices = BuildToolPreferences.Choices(BuildToolExternalSettings.PreferencesPath, profile.Guid);
            var resolved = BuildToolOptions.Default.Resolve(profile.Product, choices);
            return new JObject { ["profile"] = profile.Name, ["output"] = BuildToolPlatforms.Default.Output(profile.Product, resolved),
                ["options"] = new JArray(resolved.Select(o => new JObject { ["id"] = o.id, ["enabled"] = o.enabled })),
                ["scope"] = "설정 검사 완료. 실제 빌드·서명 성공은 미검증입니다." };
        }
        public static void Start(JObject input)
        {
            ValidateFields(input);
            var profile = Profile(input);
            new UnityProfileBuildBackend().Validate(profile);
            var runner = BuildToolController.Runner;
            if (runner.IsLocked) throw new InvalidOperationException("Busy: 미완료 작업을 기존 빌드 도구에서 확인하세요.");
            var choices = BuildToolPreferences.Choices(BuildToolExternalSettings.PreferencesPath, profile.Guid);
            BuildToolOptions.Default.Resolve(profile.Product, choices);
            if (BuildToolPlatforms.Default.TryGet(profile.Target, out var platform, out _) && platform is IBuildToolExternalSigningActions signing) signing.RestoreForBuild(profile);
            runner.Start(profile.Guid, true, choices);
        }
        public static async Task<JObject> Signing(JObject input)
        {
            ValidateFields(input, "keystorePath", "alias", "customKeystore", "credentialsFile");
            var profile = Profile(input);
            if (BuildToolController.Runner.IsLocked || BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling)
                throw new InvalidOperationException("Busy: 작업 중에는 서명을 변경할 수 없습니다.");
            if (!BuildToolPlatforms.Default.TryGet(profile.Target, out var platform, out _) || !(platform is IBuildToolExternalSigningActions signing))
                throw new ArgumentException("UnsupportedOperation: 해당 플랫폼은 서명 편집을 지원하지 않습니다.");
            return await signing.ExecuteSigning(profile, input);
        }
        public static bool CancellationRequested(JObject input)
        {
            string path = Path.Combine(RequestDirectory((string)input["requestId"]), "cancel.json");
            try { return File.Exists(path) && (string)JObject.Parse(File.ReadAllText(path))["requestId"] == (string)input["requestId"]; }
            catch (Exception error) when (error is IOException || error is JsonException) { return false; }
        }
        public static bool TryCancel(JObject input, BuildToolRunner runner)
        {
            if (!runner.CanCancel || !CancellationRequested(input)) return false;
            runner.Cancel(); return true;
        }
        public static void Progress(JObject input, BuildToolRequest request, bool canCancel)
        {
            string directory = RequestDirectory((string)input["requestId"]); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "progress.json");
            var data = new JObject { ["requestId"] = (string)input["requestId"], ["runId"] = request.id,
                ["status"] = request.status, ["logPath"] = request.logPath, ["output"] = request.output, ["canCancel"] = canCancel };
            File.WriteAllText(path + ".tmp", data.ToString());
            if (File.Exists(path)) File.Replace(path + ".tmp", path, null); else File.Move(path + ".tmp", path);
        }
    }
}
