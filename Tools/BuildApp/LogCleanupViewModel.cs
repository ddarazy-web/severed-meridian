using System.Text;
using System.Text.Json.Nodes;

namespace MoonRabbit.BuildApp;

public sealed class LogCleanupViewModel : Observable
{
    private string days = "7", report = "미리보기에서 삭제 후보와 용량을 먼저 확인하세요.";
    private string? previewProject;
    private int previewDays;
    public string Days { get => days; set { if (Set(ref days, value)) { previewProject = null; Changed(nameof(HasPreview)); } } }
    public string Report { get => report; private set => Set(ref report, value); }
    public bool HasPreview => previewProject != null;
    public string PreviewSummary { get; private set; } = "";
    public int RetentionDays => int.TryParse(Days, out int value) && value is >= 1 and <= 3650 ? value : throw new ArgumentException("보관 기간은 1~3650일로 입력하세요.");
    public bool CanClean(string project) => previewProject == project && previewDays == RetentionDays;
    public JsonObject Request(bool preview) => new() { ["operation"] = preview ? "cleanup-preview" : "cleanup", ["retentionDays"] = RetentionDays };
    public void Reset() { previewProject = null; Changed(nameof(HasPreview)); Report = "미리보기에서 삭제 후보와 용량을 먼저 확인하세요."; }
    public bool Apply(string project, bool preview, JsonObject response)
    {
        JsonNode data = response["data"] ?? throw new InvalidOperationException("로그 정리 결과가 없습니다.");
        string status = data.Text("Status");
        PreviewSummary = $"보관 {RetentionDays}일 · 후보 {(data["CandidateDetails"] as JsonArray)?.Count ?? 0}개 · {(data["CandidateBytes"]?.GetValue<long>() ?? 0) / 1048576d:N2} MB";
        bool complete = status == "Complete";
        previewProject = preview && complete ? project : null; previewDays = RetentionDays; Changed(nameof(HasPreview));
        string title = status switch { "Complete" => preview ? "미리보기 완료 · 아직 삭제하지 않음" : "정리 완료", "Busy" => "정리 보류 · 프로젝트 사용 중이거나 미완료 작업이 있습니다.", "Partial" => "일부 정리 실패 · 아래 결과를 확인하세요.", _ => "정리 실패 · 아래 결과를 확인하세요." };
        var text = new StringBuilder(title + "\n범위: " + project + "\\Logs\n");
        text.AppendLine(PreviewSummary + $" · 실제 삭제 {data["DeletedEntries"] ?? JsonValue.Create(0)}개");
        text.AppendLine($"후보 용량: {(data["CandidateBytes"]?.GetValue<long>() ?? 0) / 1048576d:N2} MB  ·  삭제 용량: {(data["DeletedBytes"]?.GetValue<long>() ?? 0) / 1048576d:N2} MB");
        foreach (JsonNode? item in (data["CandidateDetails"] as JsonArray ?? []).Take(200)) text.AppendLine($"후보  {item.Text("Path")} ({item?["Bytes"]} bytes)");
        foreach (JsonNode? item in (data["Skipped"] as JsonArray ?? []).Take(200)) text.AppendLine("보호/제외  " + item);
        foreach (JsonNode? item in (data["Errors"] as JsonArray ?? []).Take(200)) text.AppendLine("실패  " + item);
        text.AppendLine("목록은 종류별 최대 200개 표시. 실행 시 보호 조건을 다시 확인합니다.");
        Report = text.ToString(); return complete;
    }
}
