using System;
using System.IO;
using System.Linq;
using Products.Editor;

public static class BuildToolExternalRecoveryVerification
{
    public static object Main()
    {
        var profile=BuildToolProfiles.Discover().First(p=>p.Product!=null&&!p.IsCopy&&p.Target==UnityEditor.BuildTarget.StandaloneWindows64);
        var backend=new FixtureBackend(profile);
        var store=new BuildToolRunStore(Path.Combine("Temp","ExternalRecovery-"+Guid.NewGuid().ToString("N")));
        var runner=new BuildToolRunner(backend,store,"first");runner.Start(profile.Guid);
        runner.Current.callStarted=true;runner.Current.phase=BuildToolRunPhase.Building;store.Save(runner.Current);
        Check(!runner.CanCancel,"synchronous build cancellation forbidden");
        var reloaded=new BuildToolRunner(backend,store,"second");reloaded.Step();
        Check(reloaded.Current.phase==BuildToolRunPhase.NeedsAttention&&backend.BuildCalls==0,"restart never repeats build API");
        var cancelStore=new BuildToolRunStore(Path.Combine(store.Root,"cancel"));
        var cancel=new BuildToolRunner(backend,cancelStore,"session");cancel.Start(profile.Guid);cancel.Cancel();cancel.Step();
        Check(cancel.Current.phase==BuildToolRunPhase.Canceled&&backend.BuildCalls==0,"safe cancel finishes without build");
        var terminal=new BuildToolRunner(backend,cancelStore,"new-session");terminal.Step();
        Check(terminal.Current.phase==BuildToolRunPhase.Canceled&&!terminal.CanCancel,"terminal restart stable and cannot recancel");
        File.WriteAllText(cancelStore.CurrentPath,"{broken");
        var damaged=new BuildToolRunner(backend,cancelStore,"new-session");damaged.Step();
        Check(damaged.IsLocked&&damaged.StoreError!=null&&backend.BuildCalls==0,"corrupt journal blocks new execution");
        return "PASS synchronous cancellation boundary, reload no duplicate API, safe cancel, terminal restart, corrupt journal; real build calls 0";
    }
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    sealed class FixtureBackend : IBuildToolBackend
    {
        readonly BuildToolProfileInfo profile;public int BuildCalls;
        public FixtureBackend(BuildToolProfileInfo p){profile=p;}
        public bool IsMock=>true;public bool IsBusy=>false;public bool HasCompilationErrors=>false;
        public BuildToolProfileInfo Read(string guid)=>profile;
        public void Validate(BuildToolProfileInfo p){}
        public string Fingerprint(BuildToolProfileInfo p)=>"fixture";
        public string CaptureSelection(BuildToolRequest r)=>"fixture";
        public void Select(BuildToolRequest r){}
        public bool IsReady(BuildToolRequest r,out string reason){reason=null;return true;}
        public BuildToolResult Build(BuildToolRequest r){BuildCalls++;throw new Exception("must not execute");}
        public void Cleanup(BuildToolRequest r){}
    }
}
