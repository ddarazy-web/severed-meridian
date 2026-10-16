using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Nodes;

namespace MoonRabbit.BuildApp;

public sealed class BuildWorkspaceViewModel : Observable
{
    private readonly IBuildToolClient client;
    private bool busy, applying, dirty, signingDirty, statusIsError;
    private ProfileItem? selected;
    private string projectPath = "", unityPath = "", status = "프로젝트를 선택하고 프로필을 불러오세요.", lastQuery = "아직 조회하지 않음";
    private string versionName = "", versionCode = "", keystorePath = "", alias = "", logText = "", logPath = "", outputPath = "";
    private bool customKeystore;
    private string operation = "";
    private int operationGeneration;
    private DateTimeOffset profilesQueriedAt;
    private readonly List<ActionCommand> commands = [];
    public Func<string, bool> Confirm { get; set; } = _ => false;
    public ObservableCollection<ProfileItem> Profiles { get; } = [];
    public ObservableCollection<OptionItem> Options { get; } = [];
    public ObservableCollection<string> Aliases { get; } = [];
    public LogCleanupViewModel Logs { get; } = new();
    private RecoveryState recovery=new(false,false,false,"이전 작업 상태를 확인하세요.");
    public string RecoveryReport=>recovery.Report;
    public ActionCommand InspectRecovery {get;}
    public ActionCommand AcknowledgeRecovery {get;}
    public ActionCommand CleanupTransfers {get;}
    public ActionCommand Reload { get; }
    public ActionCommand Save { get; }
    public ActionCommand Check { get; }
    public ActionCommand Build { get; }
    public ActionCommand Cancel { get; }
    public ActionCommand PreviewLogs { get; }
    public ActionCommand CleanLogs { get; }

    public BuildWorkspaceViewModel(IBuildToolClient client)
    {
        this.client = client;
        Reload = Command(() => RunCommand(() => ReloadAsync()), () => CanEdit && ProjectPath.Length > 0);
        Save = Command(() => RunCommand(SaveAsync), () => CanEdit && Selected != null && Dirty);
        Check = Command(() => RunCommand(() => ExecuteProductAsync(false)), () => CanEdit && Selected != null);
        Build = Command(() => RunCommand(() => ExecuteProductAsync(true)), () => CanEdit && Selected != null);
        Cancel = Command(CancelCurrent, () => Busy && operation == "build");
        PreviewLogs = Command(() => RunCommand(() => CleanupAsync(true)), () => CanEdit && ProjectPath.Length > 0);
        CleanLogs = Command(() => RunCommand(() => CleanupAsync(false)), () => CanEdit && Logs.HasPreview);
        Logs.PropertyChanged += (_, _) => RefreshCommands();
        InspectRecovery=Command(RefreshRecovery,()=>!Busy&&ProjectPath.Length>0);
        AcknowledgeRecovery=Command(()=>{
            if(!Confirm("실행 로그를 확인했나요? 결과가 없는 외부 요청을 확인됨으로 표시합니다. 기존 Unity 실행기 기록은 변경하지 않습니다."))return;
            try{BuildRecovery.Acknowledge(ProjectPath);RefreshRecovery();}catch(Exception e){Error(e);}
        },()=>!Busy&&!recovery.Active&&!recovery.RunnerBlocked&&recovery.NeedsAttention);
        CleanupTransfers=Command(()=>{
            if(!Confirm("종료된 요청의 24시간 이상 지난 암호화 임시 파일만 정리할까요? 사용자 서명 저장소와 로그는 삭제하지 않습니다."))return;
            try{int removed=BuildRecovery.CleanupSecrets(ProjectPath);RefreshRecovery();Status=$"암호화 임시 파일 {removed}개 정리. 실행 중·최근 파일은 보존했습니다.";}catch(Exception e){Error(e);}
        },()=>!Busy&&ProjectPath.Length>0&&!recovery.Active&&!recovery.RunnerBlocked);
    }
    public void RefreshRecovery()
    {
        if(Busy)return;
        recovery=BuildRecovery.Inspect(ProjectPath);Changed(nameof(RecoveryReport));RefreshCommands();
    }
    private ActionCommand Command(Action action, Func<bool> can) { var command = new ActionCommand(action, can); commands.Add(command); return command; }
    private async void RunCommand(Func<Task<bool>> action) { try { await action(); } catch (Exception error) { Error(error); } }
    private void RefreshCommands() { foreach (var command in commands) command.Refresh(); }
    public bool Busy { get => busy; private set { if (Set(ref busy, value)) { Changed(nameof(CanEdit)); Changed(nameof(IsLoadingProfiles)); Changed(nameof(CanUseWorkspace)); RefreshCommands(); } } }
    public bool IsLoadingProfiles => Busy && operation == "settings";
    public bool CanUseWorkspace => !IsLoadingProfiles;
    public bool CanEdit => !Busy;
    public bool Dirty { get => dirty; private set { if (Set(ref dirty, value)) { Changed(nameof(SaveHint)); RefreshCommands(); } } }
    public bool SigningDirty { get => signingDirty; private set { Set(ref signingDirty, value); Changed(nameof(SaveHint)); } }
    public string SaveHint => Dirty || SigningDirty ? "저장하지 않은 변경이 있습니다." : "프로필에 저장된 설정을 사용합니다.";
    public string ProjectPath { get => projectPath; private set => Set(ref projectPath, value); }
    public string UnityPath { get => unityPath; set => Set(ref unityPath, value); }
    public string Status { get => status; private set => Set(ref status, value); }
    public bool StatusIsError { get => statusIsError; private set => Set(ref statusIsError, value); }
    public string LastQuery { get => lastQuery; private set => Set(ref lastQuery, value); }
    public string LogText { get => logText; private set => Set(ref logText, value); }
    public string LogPath { get => logPath; private set => Set(ref logPath, value); }
    public string OutputPath { get => outputPath; private set => Set(ref outputPath, value); }
    public ProfileItem? Selected { get => selected; set { if (value == selected) return; if (Busy || !DiscardChanges()) { Changed(); return; } Apply(value); } }
    public string VersionName { get => versionName; set { if (Set(ref versionName, value) && !applying) Dirty = true; } }
    public string VersionCode { get => versionCode; set { if (Set(ref versionCode, value) && !applying) Dirty = true; } }
    public string CodeLabel => Selected?.Data["appVersion"].Text("codeLabel") ?? "";
    public bool HasVersionCode => CodeLabel.Length > 0;
    public bool HasSigning => Selected?.Data["signing"] is JsonObject;
    public string ProductName => Selected?.Data.Text("productName") ?? "프로필을 선택하세요";
    public string ApplicationId => Selected?.Data.Text("applicationId") ?? "";
    public string SigningState => Selected?.Data["signing"].Flag("credentialsStored") == true ? "암호화된 비밀번호 저장됨 · 유효성 미검증" : "저장된 비밀번호 없음";
    public bool HasSavedSigning => CustomKeystore && Selected?.Data["signing"].Flag("credentialsStored") == true
        && KeystorePath == Selected.Data["signing"].Text("keystorePath") && Alias == Selected.Data["signing"].Text("alias");
    public string SigningPasswordHint => HasSavedSigning
        ? "비밀번호가 암호화되어 저장되어 있습니다. 재입력 없이 빌드할 수 있습니다. 변경하거나 Alias를 조회할 때만 다시 입력하세요."
        : "서명 저장 시 Keystore와 키 비밀번호를 입력하세요.";
    private void RefreshSigningHint() { Changed(nameof(HasSavedSigning)); Changed(nameof(SigningPasswordHint)); }
    public string KeystorePath { get => keystorePath; set { if (Set(ref keystorePath, value)) { if (!applying) { SigningDirty = true; Aliases.Clear(); } RefreshSigningHint(); } } }
    public string Alias { get => alias; set { if (Set(ref alias, value)) { if (!applying) SigningDirty = true; RefreshSigningHint(); } } }
    public bool CustomKeystore { get => customKeystore; set { if (Set(ref customKeystore, value)) { if (!applying) SigningDirty = true; RefreshSigningHint(); } } }
    public void MarkSigningInputChanged() { if (!applying && HasSigning) SigningDirty = true; }
    private bool DiscardChanges() => !(Dirty || SigningDirty) || Confirm("저장하지 않은 변경을 버리고 이동할까요? 취소하면 현재 편집을 유지합니다.");
    public bool ChangeProject(string path)
    {
        if (Busy || !DiscardChanges()) return false;
        ProjectPath = path; UnityPath = WorkspacePreferences.DetectUnity(path);
        Profiles.Clear(); Apply(null); Logs.Reset(); LastQuery = "아직 조회하지 않음"; RefreshCommands(); return true;
    }
    private void Apply(ProfileItem? profile)
    {
        applying = true;
        selected = profile; Changed(nameof(Selected));
        VersionName = profile?.Data["appVersion"].Text("name") ?? ""; VersionCode = profile?.Data["appVersion"].Text("code") ?? "";
        KeystorePath = profile?.Data["signing"].Text("keystorePath") ?? "";
        CustomKeystore = profile?.Data["signing"].Flag("customKeystore") ?? false;
        // Clearing a selected ComboBox item writes empty Text back through the binding.
        // Restore the saved value after rebuilding its items.
        string savedAlias = profile?.Data["signing"].Text("alias") ?? "";
        Aliases.Clear(); if (savedAlias.Length > 0) Aliases.Add(savedAlias);
        Alias = savedAlias;
        Options.Clear();
        foreach (JsonNode? option in profile?.Data["options"] as JsonArray ?? [])
        {
            var item = new OptionItem { Id = option.Text("id"), Label = option.Text("label"), Description = option.Text("description"), On = option.Text("on", "ON"), Off = option.Text("off", "OFF"), Enabled = option.Flag("effective") };
            item.PropertyChanged += (_, e) => { if (!applying && e.PropertyName == nameof(OptionItem.Enabled)) Dirty = true; };
            Options.Add(item);
        }
        OutputPath = profile?.Data.Text("output") ?? ""; Dirty = SigningDirty = false; applying = false;
        foreach (string name in new[] { nameof(CodeLabel), nameof(HasVersionCode), nameof(HasSigning), nameof(ProductName), nameof(ApplicationId), nameof(SigningState) }) Changed(name);
        RefreshSigningHint();
        RefreshCommands();
    }
    public async Task<bool> ReloadAsync(string? preferredGuid = null)
    {
        if (Busy || !DiscardChanges()) return false;
        string? previous = preferredGuid ?? Selected?.Guid;
        return await RunAsync("settings", async progress =>
        {
            JsonObject response = await client.SendAsync(ProjectPath, UnityPath, new() { ["operation"] = "settings" }, progress);
            var items = response["data"]?["profiles"] as JsonArray ?? throw new IOException("프로필 목록이 없습니다.");
            Profiles.Clear();
            foreach (JsonObject profile in items.OfType<JsonObject>()) Profiles.Add(new(profile.Text("profileGuid"), profile.Text("name"), profile.Text("target"), profile));
            Apply(Profiles.FirstOrDefault(p => p.Guid == previous) ?? Profiles.FirstOrDefault());
            profilesQueriedAt = DateTimeOffset.Now;
            LastQuery = "마지막 조회 " + profilesQueriedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"); Status = $"프로필 {Profiles.Count}개를 불러왔습니다.";
            SaveProfileCache();
        });
    }
    public Task<bool> InitializeProfilesAsync(string? preferredGuid = null)
    {
        if (Busy || !DiscardChanges()) return Task.FromResult(false);
        if (!ProfileCache.TryRead(ProjectPath, out var items, out profilesQueriedAt)) return ReloadAsync(preferredGuid);
        Profiles.Clear();
        foreach (JsonObject profile in items.OfType<JsonObject>()) Profiles.Add(new(profile.Text("profileGuid"), profile.Text("name"), profile.Text("target"), profile));
        Apply(Profiles.FirstOrDefault(p => p.Guid == preferredGuid) ?? Profiles.FirstOrDefault());
        LastQuery = "저장된 조회 " + profilesQueriedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        StatusIsError = false;
        Status = "이전 조회 결과를 표시합니다. 최신 설정이 필요하면 프로젝트 → 프로필 불러오기(F5)를 선택하세요.";
        return Task.FromResult(true);
    }
    private void SaveProfileCache()
    {
        try { ProfileCache.Write(ProjectPath, Profiles.Select(p => p.Data), profilesQueriedAt); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Status += " 다음 실행용 캐시를 저장하지 못했습니다."; }
    }
    public async Task<bool> SaveAsync()
    {
        if (Busy || Selected == null) return false;
        if (string.IsNullOrWhiteSpace(VersionName)) { Error(new ArgumentException("Version Name을 입력하세요.")); return false; }
        JsonObject version = new() { ["name"] = VersionName };
        if (HasVersionCode) version["code"] = VersionCode;
        JsonObject options = new(); foreach (var item in Options) options[item.Id] = item.Enabled;
        JsonObject request = ProductRequest("save-settings"); request["appVersion"] = version; request["options"] = options;
        return await RunAsync("save-settings", async progress =>
        {
            var pendingSigning = (SigningDirty, KeystorePath, Alias, CustomKeystore);
            JsonObject response = await client.SendAsync(ProjectPath, UnityPath, request, progress);
            ReplaceProfile((JsonObject)response["data"]!);
            if (pendingSigning.SigningDirty)
            { KeystorePath = pendingSigning.KeystorePath; Alias = pendingSigning.Alias; CustomKeystore = pendingSigning.CustomKeystore; SigningDirty = true; }
            Status = "버전과 옵션을 프로필에 저장했습니다.";
            SaveProfileCache();
        });
    }
    private void ReplaceProfile(JsonObject data)
    {
        var profile = new ProfileItem(data.Text("profileGuid"), data.Text("name"), data.Text("target"), data);
        int index = Profiles.ToList().FindIndex(p => p.Guid == profile.Guid);
        if (index >= 0) Profiles[index] = profile; else Profiles.Add(profile);
        Apply(profile);
    }
    public JsonObject ProductRequest(string operation) => new() { ["operation"] = operation, ["profileGuid"] = Selected?.Guid, ["expectedRevision"] = Selected?.Data.Text("revision") };
    public async Task<bool> ExecuteProductAsync(bool build)
    {
        if (Busy || Selected == null) return false;
        if (SigningDirty) { Error(new InvalidOperationException("서명 변경을 먼저 저장하거나 다시 조회하세요.")); return false; }
        if (Dirty && (!Confirm("변경한 버전·옵션을 저장한 후 " + (build ? "빌드" : "검사") + "할까요?") || !await SaveAsync())) return false;
        if (build && !Confirm($"실제 제품 빌드를 시작할까요?\n\n{Selected.Name}\n{VersionName}" + (HasVersionCode ? " / " + VersionCode : "") + "\n" + OutputPath + "\n" + string.Join("\n", Options.Select(o => o.Label + ": " + o.StateLabel)))) return false;
        string action = build ? "build" : "check";
        return await RunAsync(action, async progress =>
        {
            LogText = "";
            JsonObject response = await client.SendAsync(ProjectPath, UnityPath, ProductRequest(action), progress);
            JsonNode? data = response["data"];
            if (data.Text("logPath").Length > 0) LogPath = data.Text("logPath");
            if (data.Text("output").Length > 0) OutputPath = data.Text("output");
            Status = build ? "빌드 요청 완료 · " + data.Text("phase", "결과 확인") : "설정 사전 검사 완료 · 실제 빌드·서명 성공 검증은 아닙니다.";
        });
    }
    public async Task<bool> SigningAsync(bool save, string storePassword, string keyPassword)
    {
        if (Busy || Selected == null || !HasSigning) return false;
        if (Dirty) { Error(new InvalidOperationException("버전·옵션 변경을 먼저 저장하세요.")); return false; }
        if (CustomKeystore && (string.IsNullOrWhiteSpace(KeystorePath) || string.IsNullOrEmpty(storePassword))) { Error(new ArgumentException("Keystore 경로와 비밀번호를 입력하세요.")); return false; }
        var request = ProductRequest(save ? "save-signing" : "signing-aliases");
        request["keystorePath"] = KeystorePath; request["alias"] = Alias; request["customKeystore"] = CustomKeystore;
        return await RunAsync(request.Text("operation"), async progress =>
        {
            var response = await client.SendAsync(ProjectPath, UnityPath, request, progress, CustomKeystore ? new(storePassword, keyPassword) : null);
            if (save) { ReplaceProfile((JsonObject)response["data"]!); Status = "서명 설정과 암호화 비밀번호를 저장했습니다."; SaveProfileCache(); }
            else { Aliases.Clear(); foreach (JsonNode? item in response["data"]?["aliases"] as JsonArray ?? []) Aliases.Add(item!.GetValue<string>()); Status = $"Alias {Aliases.Count}개를 조회했습니다. 사용할 키를 선택하세요."; }
        });
    }
    public async Task<bool> CleanupAsync(bool preview)
    {
        if (Busy) return false;
        try
        {
            if (!preview && (!Logs.CanClean(ProjectPath) || !Confirm("미리 확인한 프로젝트 Logs의 오래된 항목을 정리할까요?\n" + ProjectPath + "\n" + Logs.PreviewSummary + "\n실행 시 보호 조건을 다시 검사합니다."))) return false;
            var request = Logs.Request(preview);
            return await RunAsync(request.Text("operation"), async progress =>
            {
                var response = await client.SendAsync(ProjectPath, UnityPath, request, progress);
                bool success = Logs.Apply(ProjectPath, preview, response);
                StatusIsError = !success; Status = Logs.Report.Split('\n')[0];
            });
        }
        catch (Exception error) { Error(error); return false; }
    }
    private async Task<bool> RunAsync(string action, Func<IProgress<BuildProgress>, Task> body)
    {
        if (Busy) return false;
        if(client is BuildToolClient)
        {
            RefreshRecovery();
            if(recovery.Active || (action!="settings" && action!="check" && recovery.BlocksExecution))
            { Error(new InvalidOperationException("이전 작업/복구 탭을 확인하세요. 진행 중이거나 확인이 필요한 작업을 자동 재실행하지 않습니다."));return false; }
        }
        operation = action; Busy = true; StatusIsError = false;
        int generation = ++operationGeneration;
        Status = action.StartsWith("cleanup") ? "로그 보호 조건을 확인하는 중…" : "Unity 연결 중… 최초 시작·컴파일에는 시간이 걸릴 수 있습니다.";
        var progress = new Progress<BuildProgress>(p => { if (!Busy || generation != operationGeneration) return; Status = p.Status; if (!string.IsNullOrEmpty(p.LogPath)) LogPath = p.LogPath; if (!string.IsNullOrEmpty(p.Output)) OutputPath = p.Output; if (!string.IsNullOrEmpty(p.LogText)) { string text = LogText + p.LogText; LogText = text.Length > 65536 ? text[^65536..] : text; } });
        try { await body(progress); return !StatusIsError; }
        catch (Exception error) { Error(error); return false; }
        finally { Busy = false; operation = ""; RefreshCommands(); }
    }
    private void CancelCurrent()
    {
        try { client.RequestCancel(); Status = "취소 요청 전달 중 · 동기 빌드 중이면 안전한 지점까지 기다립니다."; }
        catch (Exception error) { Error(error); }
    }
    public void Error(Exception error)
    {
        StatusIsError = true;
        Status = error is BuildToolException { Code: "RevisionConflict" } ? "설정이 외부에서 변경됐습니다. 현재 입력을 확인한 뒤 다시 불러오세요. 덮어쓰지 않았습니다." : error.Message;
    }
}
