using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using Products.Editor;

namespace MoonRabbit.BuildApp;

public sealed class BuildToolClient : IBuildToolClient
{
    private string? requestDirectory, requestId;
    private int running;
    public async Task<JsonObject> SendAsync(string project, string unity, JsonObject request, IProgress<BuildProgress>? progress = null, SecretInput? secrets = null)
    {
        if (Interlocked.Exchange(ref running, 1) != 0) throw new BuildToolException("Busy", "이미 작업을 실행 중입니다.");
        string? encryptedPath = null;
        try
        {
            project = Path.GetFullPath(project).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string host = Path.Combine(project, "Tools", "Build", "Invoke-BuildTool.ps1");
            if (!File.Exists(host)) throw new BuildToolException("InvalidProject", "외부 빌드 연결부가 있는 Unity 프로젝트를 선택하세요.");
            requestId = Guid.NewGuid().ToString("N");
            requestDirectory = Path.Combine(project, "Library", "MatchBuildApp", requestId);
            Directory.CreateDirectory(requestDirectory);
            BuildRecovery.WriteOwner(requestDirectory,"client-owner.json",Process.GetCurrentProcess());
            var input = (JsonObject)request.DeepClone(); input["version"] = 2; input["requestId"] = requestId;
            if (secrets != null)
            {
                var cipher = new BuildToolSigningPasswords("external-transfer\n" + project + "\n" + requestId, requestDirectory);
                cipher.Save(secrets.StorePassword, secrets.KeyPassword);
                encryptedPath = cipher.FilePath;
                input["credentialsFile"] = encryptedPath;
            }
            string requestPath = Path.Combine(requestDirectory, "request.json");
            await File.WriteAllTextAsync(requestPath, input.ToJsonString());
            var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = project };
            foreach (string argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", host, "-ProjectPath", project, "-RequestPath", requestPath }) start.ArgumentList.Add(argument);
            if (!string.IsNullOrWhiteSpace(unity)) { start.ArgumentList.Add("-UnityPath"); start.ArgumentList.Add(unity); }
            if (request.Text("operation") == "build") start.ArgumentList.Add("-PreserveLogs");
            using var process = Process.Start(start) ?? throw new IOException("실행 호스트를 시작하지 못했습니다.");
            Task stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
            Task exit = process.WaitForExitAsync();
            long offset = 0;
            string previousLogPath = "";
            while (!exit.IsCompleted)
            {
                await Task.WhenAny(exit, Task.Delay(350));
                string statePath = Path.Combine(requestDirectory, "progress.json");
                try
                {
                    if (File.Exists(statePath) && JsonNode.Parse(await File.ReadAllTextAsync(statePath)) is JsonObject state && state.Text("requestId") == requestId)
                    {
                        string logPath = state.Text("logPath");
                        if (logPath != previousLogPath) { offset = 0; previousLogPath = logPath; }
                        string addition = "";
                        if (File.Exists(logPath))
                        {
                            using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            if (offset > stream.Length) offset = 0;
                            stream.Seek(Math.Max(offset, stream.Length - 32768), SeekOrigin.Begin);
                            byte[] bytes = new byte[32768]; int count = await stream.ReadAsync(bytes);
                            offset = stream.Position; addition = Encoding.UTF8.GetString(bytes, 0, count);
                        }
                        progress?.Report(new(state.Text("status", "Unity 준비 중…"), logPath, state.Text("output"), addition));
                    }
                }
                catch (Exception error) when (error is IOException or System.Text.Json.JsonException) { }
            }
            await Task.WhenAll(exit, stdout, stderr);
            string resultPath = Path.Combine(requestDirectory, "result.json");
            if (!File.Exists(resultPath)) throw new BuildToolException("MissingResult", "결과 파일 없이 종료되었습니다. 자동 재실행하지 않습니다. 작업 폴더: " + requestDirectory);
            var result = JsonNode.Parse(await File.ReadAllTextAsync(resultPath)) as JsonObject ?? throw new IOException("잘못된 결과입니다.");
            if (result.Text("requestId") != requestId || result["version"]?.GetValue<int>() != 2) throw new BuildToolException("InvalidResult", "요청과 결과의 식별자가 다릅니다.");
            if (result["exitCode"]?.GetValue<int>() != 0 || process.ExitCode != 0)
            {
                string message = result.Text("message", "작업이 완료되지 않았습니다.");
                if (result["data"] is JsonObject data && request.Text("operation").StartsWith("cleanup")) return result;
                throw new BuildToolException(result.Text("errorCode", "OperationFailed"), message);
            }
            return result;
        }
        finally
        {
            try
            {
                if(requestDirectory!=null) File.Delete(Path.Combine(requestDirectory,"client-owner.json"));
                if (encryptedPath != null && File.Exists(encryptedPath) && (requestDirectory==null||!BuildRecovery.HasLiveOwner(requestDirectory))) File.Delete(encryptedPath);
            }
            finally { requestDirectory = requestId = null; Interlocked.Exchange(ref running, 0); }
        }
    }
    public void RequestCancel()
    {
        if (requestDirectory == null || requestId == null) return;
        string path = Path.Combine(requestDirectory, "cancel.json");
        File.WriteAllText(path + ".tmp", new JsonObject { ["requestId"] = requestId }.ToJsonString());
        File.Move(path + ".tmp", path, true);
    }
}
