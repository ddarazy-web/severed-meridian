using System.IO;
using System.Text.Json.Nodes;

namespace MoonRabbit.BuildApp;

internal static class ProfileCache
{
    internal static string Location(string project) => Path.Combine(project, "UserSettings", "MatchBuildAppProfiles.json");
    internal static bool TryRead(string project, out JsonArray profiles, out DateTimeOffset queriedAt)
    {
        profiles = []; queriedAt = default;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(Location(project))) is not JsonObject data) return false;
            if (data["version"]?.GetValue<int>() != 1 ||
                !string.Equals(data.Text("project"), Path.GetFullPath(project), StringComparison.OrdinalIgnoreCase) ||
                !DateTimeOffset.TryParse(data.Text("queriedAt"), out queriedAt) || data["profiles"] is not JsonArray items) return false;
            if (items.Any(item => item is not JsonObject)) return false;
            profiles = new JsonArray(items.Select(item => (JsonNode)Snapshot((JsonObject)item!)).ToArray());
            return profiles.All(item => item.Text("profileGuid").Length > 0 && item.Text("revision").Length > 0);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException or InvalidCastException or FormatException or ArgumentException) { return false; }
    }
    internal static void Write(string project, IEnumerable<JsonObject> profiles, DateTimeOffset queriedAt)
    {
        // 화면에 필요한 공개 메타데이터만 저장한다. 비밀번호·입력값·전송 payload는 저장하지 않는다.
        var data = new JsonObject {
            ["version"] = 1, ["project"] = Path.GetFullPath(project), ["queriedAt"] = queriedAt.ToString("O"),
            ["profiles"] = new JsonArray(profiles.Select(item => (JsonNode)Snapshot(item)).ToArray()) };
        string path = Location(project); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, data.ToJsonString()); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static JsonObject Snapshot(JsonObject profile)
    {
        var result = new JsonObject();
        foreach (string key in new[] { "profileGuid", "name", "target", "productName", "applicationId", "output", "revision" }) result[key] = profile.Text(key);
        result["appVersion"] = new JsonObject { ["name"] = profile["appVersion"].Text("name"), ["code"] = profile["appVersion"].Text("code"), ["codeLabel"] = profile["appVersion"].Text("codeLabel") };
        if (profile["signing"] is JsonObject signing)
            result["signing"] = new JsonObject { ["customKeystore"] = signing.Flag("customKeystore"), ["keystorePath"] = signing.Text("keystorePath"), ["alias"] = signing.Text("alias"), ["credentialsStored"] = signing.Flag("credentialsStored") };
        result["options"] = new JsonArray((profile["options"] as JsonArray ?? []).Select(option => (JsonNode)new JsonObject {
            ["id"] = option.Text("id"), ["label"] = option.Text("label"), ["description"] = option.Text("description"),
            ["on"] = option.Text("on", "ON"), ["off"] = option.Text("off", "OFF"), ["effective"] = option.Flag("effective") }).ToArray());
        return result;
    }
}
