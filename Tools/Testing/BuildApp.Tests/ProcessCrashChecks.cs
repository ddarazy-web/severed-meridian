using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using MoonRabbit.BuildApp;

internal static class ProcessCrashChecks
{
    public static async Task Client(string project)
    {
        await new BuildToolClient().SendAsync(project,"",new JsonObject {["operation"]="build"},secrets:new SecretInput("crash-fixture-only","dummy-key"));
    }
    public static async Task Run(string fixture,Action<bool,string> check)
    {
        string project=Path.Combine(fixture,"crash 한글 path"),host=Path.Combine(project,"Tools","Build");Directory.CreateDirectory(host);
        await File.WriteAllTextAsync(Path.Combine(host,"Invoke-BuildTool.ps1"),"""
param($ProjectPath,$RequestPath,$UnityPath)
$r=Get-Content -LiteralPath $RequestPath -Raw | ConvertFrom-Json
$dir=Split-Path $RequestPath
$me=[Diagnostics.Process]::GetCurrentProcess()
@{pid=$me.Id;startUtcTicks=$me.StartTime.ToUniversalTime().Ticks}|ConvertTo-Json|Set-Content (Join-Path $dir 'host-owner.json')
$gatePath=Join-Path $ProjectPath 'Logs/ExternalBuild/operation.lock'
New-Item -ItemType Directory -Force (Split-Path $gatePath)|Out-Null
$gate=[IO.File]::Open($gatePath,'OpenOrCreate','ReadWrite','None')
try{
 [IO.File]::WriteAllText((Join-Path $ProjectPath 'started.txt'),'1')
 $limit=[DateTime]::UtcNow.AddSeconds(60)
 while(!(Test-Path (Join-Path $ProjectPath 'release.txt')) -and [DateTime]::UtcNow -lt $limit){Start-Sleep -Milliseconds 50}
 [IO.File]::WriteAllText((Join-Path $dir 'result.json'),(@{version=2;requestId=$r.requestId;exitCode=0;data=@{phase='Succeeded'}}|ConvertTo-Json -Depth 5))
}finally{$gate.Dispose()}
""");
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
        start.ArgumentList.Add("--crash-client");start.ArgumentList.Add(project);
        using var child=Process.Start(start)!;
        try
        {
            await Until(()=>File.Exists(Path.Combine(project,"started.txt")));
            child.Kill(); await child.WaitForExitAsync();
            check(BuildRecovery.Inspect(project).Active,"parent crash keeps host operation active");
            string secret=Directory.EnumerateFiles(Path.Combine(project,"Library"),"*.bin",SearchOption.AllDirectories).Single();
            File.SetLastWriteTimeUtc(secret,DateTime.UtcNow.AddDays(-2));
            check(BuildRecovery.CleanupSecrets(project)==0 && File.Exists(secret),"parent crash does not delete live host secret");
            File.WriteAllText(Path.Combine(project,"release.txt"),"release");
            await Until(()=>!BuildRecovery.Inspect(project).Active);
            check(!BuildRecovery.Inspect(project).NeedsAttention,"restarted observer reads host completion");
            check(BuildRecovery.CleanupSecrets(project)==1&&!File.Exists(secret),"dead process orphan secret cleaned");
            check(Directory.EnumerateDirectories(Path.Combine(project,"Library","MatchBuildApp")).Count()==1,"observer never duplicates build request");
        }
        finally {File.WriteAllText(Path.Combine(project,"release.txt"),"release");if(!child.HasExited){child.Kill();await child.WaitForExitAsync();}}
    }
    static async Task Until(Func<bool> condition)
    {
        var watch=Stopwatch.StartNew();while(!condition()){if(watch.Elapsed>TimeSpan.FromSeconds(20))throw new TimeoutException("fixture process timeout");await Task.Delay(100);}
    }
}
