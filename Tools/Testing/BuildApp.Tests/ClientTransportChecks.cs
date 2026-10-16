using System.IO;
using System.Text.Json.Nodes;
using MoonRabbit.BuildApp;

internal static class ClientTransportChecks
{
    public static async Task Run(string fixture, Action<bool,string> check)
    {
        string host=Path.Combine(fixture,"Tools","Build"); Directory.CreateDirectory(host);
        await File.WriteAllTextAsync(Path.Combine(host,"Invoke-BuildTool.ps1"), """
param($ProjectPath,$RequestPath,$UnityPath)
$r=Get-Content -LiteralPath $RequestPath -Raw | ConvertFrom-Json
$dir=Split-Path $RequestPath
if($r.operation -eq 'missing'){exit 0}
if($r.operation -eq 'signing-aliases'){
  if(!(Test-Path -LiteralPath $r.credentialsFile)){exit 9}
  $cipher=[IO.File]::ReadAllBytes($r.credentialsFile)
  if([Text.Encoding]::UTF8.GetString($cipher).Contains('transport-secret')){exit 10}
}
if($r.operation -eq 'build'){
  @{requestId=$r.requestId;status='fixture waiting';logPath='';output=''} | ConvertTo-Json | Set-Content (Join-Path $dir 'progress.json')
  $limit=[DateTime]::UtcNow.AddSeconds(10)
  while(!(Test-Path (Join-Path $dir 'cancel.json')) -and [DateTime]::UtcNow -lt $limit){Start-Sleep -Milliseconds 50}
  if(!(Test-Path (Join-Path $dir 'cancel.json'))){exit 11}
}
$id=$r.requestId
if($r.operation -eq 'wrong-id'){$id='wrong'}
@{version=2;requestId=$id;exitCode=0;data=@{phase='Canceled'}} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $dir 'result.json')
""");
        var client=new BuildToolClient();
        await client.SendAsync(fixture,"",new JsonObject {["operation"]="signing-aliases"},secrets:new SecretInput("transport-secret","transport-key"));
        check(!Directory.EnumerateFiles(Path.Combine(fixture,"Library"),"*.bin",SearchOption.AllDirectories).Any(),"transport encrypted payload deleted after success");
        foreach(string op in new[]{"missing","wrong-id"})
        {
            try { await client.SendAsync(fixture,"",new JsonObject {["operation"]=op},secrets:new SecretInput("transport-secret","transport-key")); throw new Exception("invalid result accepted"); }
            catch(BuildToolException e){check(e.Code==(op=="missing"?"MissingResult":"InvalidResult"),"transport "+op);}
            check(!Directory.EnumerateFiles(Path.Combine(fixture,"Library"),"*.bin",SearchOption.AllDirectories).Any(),"transport encrypted payload deleted after failure");
        }
        bool reported=false;
        var progress=new Progress<BuildProgress>(_=>{reported=true;client.RequestCancel();});
        await client.SendAsync(fixture,"",new JsonObject {["operation"]="build"},progress);
        check(reported,"transport progress and cancellation file");
        check(!Directory.EnumerateFiles(Path.Combine(fixture,"Library"),"*.json",SearchOption.AllDirectories).Any(p=>File.ReadAllText(p).Contains("transport-secret")),"transport JSON never contains plaintext password");
    }
}
