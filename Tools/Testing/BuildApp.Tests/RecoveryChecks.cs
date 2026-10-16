using System.IO;
using System.Text.Json.Nodes;
using System.Diagnostics;
using MoonRabbit.BuildApp;

internal static class RecoveryChecks
{
    public static void Run(string fixture,Action<bool,string> check)
    {
        string project=Path.Combine(fixture,"recovery");
        string id=Guid.NewGuid().ToString("N"), directory=Path.Combine(project,"Library","MatchBuildApp",id);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"request.json"),new JsonObject {["version"]=2,["requestId"]=id,["operation"]="build"}.ToJsonString());
        var state=BuildRecovery.Inspect(project);
        check(state.NeedsAttention&&!state.Active,"orphan request detected without retry");
        File.WriteAllText(Path.Combine(directory,"result.json"),new JsonObject {["version"]=2,["requestId"]=id,["exitCode"]=0}.ToJsonString());
        check(!BuildRecovery.Inspect(project).NeedsAttention,"matching terminal response detected");
        File.WriteAllText(Path.Combine(directory,"result.json"),"{broken");
        check(BuildRecovery.Inspect(project).NeedsAttention,"corrupt response requires attention");
        File.WriteAllText(Path.Combine(directory,"result.json"),new JsonObject {["version"]=2,["requestId"]="wrong",["exitCode"]=0}.ToJsonString());
        check(BuildRecovery.Inspect(project).NeedsAttention,"wrong result ID rejected on restart");
        string secret=Path.Combine(directory,new string('A',64)+".bin");File.WriteAllText(secret,"dummy ciphertext");File.SetLastWriteTimeUtc(secret,DateTime.UtcNow.AddDays(-2));
        string gate=Path.Combine(project,"Logs","ExternalBuild","operation.lock");Directory.CreateDirectory(Path.GetDirectoryName(gate)!);
        using(var locked=File.Open(gate,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
        {
            check(BuildRecovery.Inspect(project).Active,"live host lock detected");
            check(BuildRecovery.CleanupSecrets(project)==0&&File.Exists(secret),"active request ciphertext protected");
        }
        check(BuildRecovery.CleanupSecrets(project)==1&&!File.Exists(secret),"abandoned ciphertext safely cleaned");
        File.WriteAllText(secret,"fresh fixture");
        check(BuildRecovery.CleanupSecrets(project)==0&&File.Exists(secret),"new request transfer protected");
        BuildRecovery.WriteOwner(directory,"host-owner.json",Process.GetCurrentProcess());
        check(BuildRecovery.Inspect(project).Active,"live PID and start time recognized");
        var owner=JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"host-owner.json")))!;owner["startUtcTicks"]=1L;
        File.WriteAllText(Path.Combine(directory,"host-owner.json"),owner.ToJsonString());
        check(!BuildRecovery.Inspect(project).Active,"reused PID does not match old owner");
        File.WriteAllText(Path.Combine(directory,"host-owner.json"),"{broken");
        check(BuildRecovery.Inspect(project).Active,"unreadable owner conservatively protected");
        File.Delete(Path.Combine(directory,"host-owner.json"));
        string journal=Path.Combine(project,"Library","MatchBuildTool","current.json");Directory.CreateDirectory(Path.GetDirectoryName(journal)!);
        File.WriteAllText(journal,"{broken");
        check(BuildRecovery.Inspect(project).RunnerBlocked,"corrupt runner blocks acknowledgment");
        string before=File.ReadAllText(journal);BuildRecovery.Acknowledge(project);
        check(File.ReadAllText(journal)==before&&BuildRecovery.Inspect(project).RunnerBlocked,"recovery never rewrites runner state");
        File.Delete(journal);BuildRecovery.Acknowledge(project);
        check(!BuildRecovery.Inspect(project).NeedsAttention,"explicit acknowledgment permits next request");
        string early=Path.Combine(project,"Library","MatchBuildApp",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(early);
        string earlyCipher=Path.Combine(early,new string('B',64)+".bin");File.WriteAllText(earlyCipher,"early fixture");File.SetLastWriteTimeUtc(earlyCipher,DateTime.UtcNow.AddDays(-2));
        check(BuildRecovery.CleanupSecrets(project)==1&&!File.Exists(earlyCipher),"crash before request JSON still cleans aged cipher");
        var placed=WindowPlacement.Clamp(new System.Windows.Rect(2800,100,1100,700),new System.Windows.Rect(2560,0,2560,1392));
        check(placed.X==2800,"secondary monitor location retained");
        var disconnected=WindowPlacement.Clamp(new System.Windows.Rect(90000,90000,1100,700),new System.Windows.Rect(0,0,1280,752));
        check(disconnected.Right<=1280&&disconnected.Bottom<=752,"disconnected monitor position clamped");
    }
}
