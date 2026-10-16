using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MoonRabbit.BuildApp;

public sealed record RecoveryState(bool Active, bool NeedsAttention, bool RunnerBlocked, string Report)
{
    public bool BlocksExecution => Active || NeedsAttention || RunnerBlocked;
}

public static class BuildRecovery
{
    private static string Root(string project) => Path.Combine(project,"Library","MatchBuildApp");
    private static bool Linked(string path) => File.Exists(path)||Directory.Exists(path) ? (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0 : false;
    private static IEnumerable<string> Requests(string project)
    {
        string root=Root(project);
        if(!Directory.Exists(root)) return [];
        if(Linked(project)||Linked(Path.Combine(project,"Library"))||Linked(root)) throw new IOException("링크된 작업 기록은 자동 처리하지 않습니다.");
        return Directory.EnumerateDirectories(root).Where(p=>Regex.IsMatch(Path.GetFileName(p),"^[a-zA-Z0-9_-]{1,80}$")&&!Linked(p));
    }
    private static bool Locked(string path)
    {
        if(!File.Exists(path)) return false;
        try { using var probe=File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None); return false; }
        catch(IOException){return true;} catch(UnauthorizedAccessException){return true;}
    }
    private static bool OwnerAlive(string directory)
    {
        foreach(string name in new[]{"client-owner.json","host-owner.json","unity-owner.json"})
        {
            string path=Path.Combine(directory,name);
            if(!File.Exists(path)) continue;
            try
            {
                var state=JsonNode.Parse(File.ReadAllText(path))!;
                int pid=state["pid"]!.GetValue<int>();long ticks=state["startUtcTicks"]!.GetValue<long>();
                using var process=Process.GetProcessById(pid);
                if(!process.HasExited && process.StartTime.ToUniversalTime().Ticks==ticks) return true;
            }
            catch(ArgumentException) { }
            catch(Exception) { return true; }
        }
        return false;
    }
    private static bool Terminal(string directory,out string description)
    {
        description="결과 없음 · 확인 필요";
        string path=Path.Combine(directory,"result.json");
        if(!File.Exists(path)) return false;
        try
        {
            var data=JsonNode.Parse(File.ReadAllText(path))!;
            if(data["version"]?.GetValue<int>()!=2 || data.Text("requestId")!=Path.GetFileName(directory) || data["exitCode"]==null) return false;
            description=data["exitCode"]!.GetValue<int>()==0?"완료":"실패/취소 · 결과 확인"; return true;
        }
        catch(Exception){description="손상된 결과 · 확인 필요";return false;}
    }
    public static RecoveryState Inspect(string project)
    {
        if(string.IsNullOrWhiteSpace(project)) return new(false,false,false,"프로젝트를 선택하세요.");
        try
        {
            bool active=Locked(Path.Combine(project,"Logs","ExternalBuild","operation.lock"))||Locked(Path.Combine(project,"Temp","UnityLockfile"));
            bool attention=false,runnerBlocked=false;var lines=new List<string>();
            string journal=Path.Combine(project,"Library","MatchBuildTool","current.json");
            if(File.Exists(journal))
            {
                try
                {
                    var data=JsonNode.Parse(File.ReadAllText(journal))!;
                    runnerBlocked=data["version"]?.GetValue<int>()!=1 || !Guid.TryParseExact(data.Text("id"),"N",out _) || data["phase"]?.GetValue<int>() is not (6 or 7 or 8);
                }
                catch(Exception){runnerBlocked=true;}
                if(runnerBlocked) lines.Add("기존 실행기 확인 필요: Unity의 Match → Build → 빌드 도구에서 미완료 요청을 확인하세요. 자동 실행·복원하지 않습니다.");
            }
            foreach(string directory in Requests(project).OrderByDescending(Directory.GetLastWriteTimeUtc))
            {
                bool terminal=Terminal(directory,out string description);
                bool owned=OwnerAlive(directory);
                active|=owned;
                bool acknowledged=File.Exists(Path.Combine(directory,"acknowledged.txt"));
                attention|=!terminal&&!owned&&!acknowledged;
                if(lines.Count<22) lines.Add(Path.GetFileName(directory)+" · "+(owned?"실행 중":acknowledged&&!terminal?"사용자 확인됨":description));
            }
            string title=active?"기존 작업/에디터 사용 중 · 새 실행 보류":attention||runnerBlocked?"중단 기록 확인 필요":"진행 중인 작업 없음";
            return new(active,attention,runnerBlocked,title+"\n"+string.Join("\n",lines));
        }
        catch(Exception error){return new(false,true,true,"작업 기록을 확인할 수 없습니다: "+error.Message);}
    }
    public static void WriteOwner(string directory,string name,Process process)
    {
        string path=Path.Combine(directory,name);
        File.WriteAllText(path+".tmp",new JsonObject {["pid"]=process.Id,["startUtcTicks"]=process.StartTime.ToUniversalTime().Ticks}.ToJsonString());
        File.Move(path+".tmp",path,true);
    }
    public static bool HasLiveOwner(string directory)=>OwnerAlive(directory);
    public static void Acknowledge(string project)
    {
        var state=Inspect(project);if(state.Active||state.RunnerBlocked)return;
        foreach(string directory in Requests(project))
            if(!Terminal(directory,out _)&&!OwnerAlive(directory)) File.WriteAllText(Path.Combine(directory,"acknowledged.txt"),DateTime.UtcNow.ToString("O"));
    }
    public static int CleanupSecrets(string project)
    {
        if(Inspect(project).Active||Inspect(project).RunnerBlocked)return 0;
        string gate=Path.Combine(project,"Logs","ExternalBuild","operation.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(gate)!);
        FileStream guard;
        try{guard=File.Open(gate,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){return 0;}
        using(guard)
        {
            if(Locked(Path.Combine(project,"Temp","UnityLockfile")))return 0;
            int deleted=0;
            foreach(string directory in Requests(project))
            {
                if(OwnerAlive(directory))continue;
                // 전송 준비와 호스트 시작 사이의 짧은 구간도 보호한다.
                foreach(string path in Directory.EnumerateFiles(directory,"*.bin"))
                {
                    if(!Regex.IsMatch(Path.GetFileName(path),"^[A-F0-9]{64}\\.bin$")||Linked(path)||File.GetLastWriteTimeUtc(path)>DateTime.UtcNow.AddDays(-1))continue;
                    using(var probe=File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
                    File.Delete(path);deleted++;
                }
            }
            return deleted;
        }
    }
}
