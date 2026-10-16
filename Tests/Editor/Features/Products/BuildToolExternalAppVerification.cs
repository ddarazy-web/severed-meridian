using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Products.Editor;
using UnityEditor;
using UnityEditor.Build.Profile;

public static class BuildToolExternalAppVerification
{
    public static object Main()
    {
        var cross = JObject.Parse(File.ReadAllText("Temp/BuildAppCrossRuntime.json"));
        string id = (string)cross["requestId"];
        string folder="Assets/__ExternalApp-"+Guid.NewGuid().ToString("N");
        string root="Temp/ExternalApp-"+Guid.NewGuid().ToString("N"); Directory.CreateDirectory(root);
        string preferences=Path.Combine(root,"preferences.json");
        var type=typeof(BuildToolProfiles).Assembly.GetType("Products.Editor.BuildToolSigningPasswords",true);
        var rootField=type.GetField("Root",BindingFlags.Static|BindingFlags.NonPublic);
        object originalRoot=rootField.GetValue(null);
        var active=BuildProfile.GetActiveBuildProfile(); string activeVersion=PlayerSettings.bundleVersion;
        AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));
        try
        {
            rootField.SetValue(null,root);
            var product=ProductProfiles.Definitions.First(p=>p.Target==BuildTarget.Android);
            string path=folder+"/fixture.asset"; Check(AssetDatabase.CopyAsset(product.ProfilePath,path),"copy profile");
            var profile=BuildToolProfiles.Discover().Single(p=>p.Path==path);
            var before=BuildToolExternalSettings.Read(profile,preferences);
            var input=new JObject { ["version"]=2,["requestId"]=id,["operation"]="save-signing",["profileGuid"]=profile.Guid,
                ["expectedRevision"]=before["revision"],["keystorePath"]="fixture-only.keystore",["alias"]="fixture-key",["customKeystore"]=true,["credentialsFile"]=cross["credentialsFile"] };
            Func<string,string,Task<string[]>> reader=(store,password)=>{Check(password=="fixture-store-secret","NET8 to Unity DPAPI compatibility");return Task.FromResult(new[]{"fixture-key"});};
            var saved=BuildToolAndroidExternalSigning.Execute(profile,input,preferences,reader).GetAwaiter().GetResult();
            Check((string)saved["signing"]["alias"]=="fixture-key" && (bool)saved["signing"]["credentialsStored"],"signing stored in shared format");
            Check(!File.Exists((string)cross["credentialsFile"]),"one-time transfer deleted");
            string project=Path.GetFullPath(".").TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            object cipher=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{project+"\n"+profile.Guid+"\nfixture-only.keystore\nfixture-key",root},null);
            object[] args={null,null}; Check((bool)type.GetMethod("TryRead",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(cipher,args),"existing store readable");
            Check((string)args[1]=="fixture-key-secret","key password preserved");
            var invalid=(JObject)input.DeepClone(); invalid["credentialsFile"]=Path.GetFullPath(Path.Combine(root,"outside.bin")); File.WriteAllText((string)invalid["credentialsFile"],"fixture");
            try { BuildToolAndroidExternalSigning.Execute(profile,invalid,preferences,reader).GetAwaiter().GetResult(); throw new Exception("outside path accepted"); } catch(ArgumentException) { }
            Check(File.Exists((string)invalid["credentialsFile"]),"unrelated file not deleted");
            string directory=BuildToolExternalOperations.RequestDirectory(id); File.WriteAllText(Path.Combine(directory,"cancel.json"),new JObject {["requestId"]="wrong"}.ToString());
            Check(!BuildToolExternalOperations.CancellationRequested(input),"wrong cancellation ID ignored");
            File.WriteAllText(Path.Combine(directory,"cancel.json"),new JObject {["requestId"]=id}.ToString());
            Check(BuildToolExternalOperations.CancellationRequested(input),"matching cancellation recognized");
            var canonical=BuildToolProfiles.Discover().First(p=>p.Product!=null&&!p.IsCopy&&p.Target==BuildTarget.StandaloneWindows64);
            var backend=new CancelBackend(canonical);
            var runner=new BuildToolRunner(backend,new BuildToolRunStore(Path.Combine(root,"runner")),"fixture");
            runner.Start(canonical.Guid);
            Check(BuildToolExternalOperations.TryCancel(input,runner),"cancel reaches existing runner");
            runner.Step();
            Check(runner.Current.phase==BuildToolRunPhase.Canceled && backend.BuildCalls==0,"safe cancellation without build call");
            string transferPath=(string)cross["credentialsFile"];
            File.WriteAllText(transferPath,"corrupted fixture");
            input["expectedRevision"]=saved["revision"];
            try {BuildToolAndroidExternalSigning.Execute(profile,input,preferences,reader).GetAwaiter().GetResult();throw new Exception("corrupted cipher accepted");} catch(System.Security.Cryptography.CryptographicException) { }
            Check(!File.Exists(transferPath),"failed transfer cleaned");
            var state=BuildToolExternalSettings.Read(canonical,BuildToolExternalSettings.PreferencesPath);
            var request=new JObject {["version"]=2,["requestId"]=id,["operation"]="check",["profileGuid"]=canonical.Guid,["expectedRevision"]=state["revision"]};
            var report=BuildToolExternalOperations.Check(request); Check(report["options"]!=null,"v2 check uses registered options");
            request["expectedRevision"]="stale";
            try {BuildToolExternalOperations.Check(request);throw new Exception("stale revision accepted");} catch(InvalidOperationException e) when(e.Message.StartsWith("RevisionConflict:")) { }
            Check(BuildProfile.GetActiveBuildProfile()==active && PlayerSettings.bundleVersion==activeVersion,"active profile preserved");
            return "PASS external signing cross-runtime compatibility, store, transfer cleanup, path scope, cancellation IDs, v2 check/revision";
        }
        finally { rootField.SetValue(null,originalRoot); AssetDatabase.DeleteAsset(folder); }
    }
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);}
    sealed class CancelBackend : IBuildToolBackend
    {
        readonly BuildToolProfileInfo profile; public int BuildCalls;
        public CancelBackend(BuildToolProfileInfo profile){this.profile=profile;}
        public bool IsMock=>true; public bool IsBusy=>false; public bool HasCompilationErrors=>false;
        public BuildToolProfileInfo Read(string guid)=>profile;
        public void Validate(BuildToolProfileInfo value){}
        public string Fingerprint(BuildToolProfileInfo value)=>"fixture";
        public string CaptureSelection(BuildToolRequest request)=>"fixture";
        public void Select(BuildToolRequest request){}
        public bool IsReady(BuildToolRequest request,out string reason){reason=null;return true;}
        public BuildToolResult Build(BuildToolRequest request){BuildCalls++;throw new Exception("must not build");}
        public void Cleanup(BuildToolRequest request){}
    }
}
