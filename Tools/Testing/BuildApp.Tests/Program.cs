using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using MoonRabbit.BuildApp;
using Products.Editor;

internal static class Program
{
    static int passed;
    [STAThread]
    static int Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; app.Resources = new ResourceDictionary { Source = new Uri("pack://application:,,,/MoonRabbit.BuildApp;component/Theme.xaml") };
        int code = 1;
        app.Dispatcher.InvokeAsync(async () => {
            try { if (Environment.GetCommandLineArgs().Contains("--live-readonly")) await LiveReadOnly(); else await Run(); Console.WriteLine($"PASS {passed} WPF checks"); code = 0; }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally { app.Shutdown(); }
        });
        app.Run(); return code;
    }
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
    static async Task LiveReadOnly()
    {
        string project = WorkspacePreferences.DetectProject(Environment.CurrentDirectory);
        var client = new BuildToolClient();
        string unity = WorkspacePreferences.DetectUnity(project);
        var result = await client.SendAsync(project, unity, new JsonObject { ["operation"]="settings" });
        var profiles = (JsonArray)result["data"]!["profiles"]!;
        Check(profiles.Count==5,"real client settings: five products");
        var selected = profiles.First(p=>p.Text("target")=="StandaloneWindows64")!;
        var checkedResult = await client.SendAsync(project,unity,new JsonObject { ["operation"]="check",["profileGuid"]=selected.Text("profileGuid"),["expectedRevision"]=selected.Text("revision") });
        Check(checkedResult["data"]?["options"] is JsonArray,"real client v2 readonly check");
        var effective=(JsonArray)checkedResult["data"]!["options"]!;
        Check(((JsonArray)selected["options"]!).All(option=>effective.Any(value=>value.Text("id")==option.Text("id") && value.Flag("enabled")==option.Flag("effective"))),"real check matches displayed effective options");
    }
    static async Task Run()
    {
        string project = WorkspacePreferences.DetectProject(Environment.CurrentDirectory);
        string fixture = Path.Combine(project,"Temp","BuildAppTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(fixture);
        string prefsPath = Path.Combine(fixture,"workspace.json");
        await ClientTransportChecks.Run(fixture,Check);
        var preferences = new WorkspacePreferences { ProjectPath=fixture, ProfileGuid="profile-1", Width=1120, Height=750, Left=90000, Top=90000 };
        preferences.Save(prefsPath);
        var restored = WorkspacePreferences.Load(prefsPath);
        Check(restored.Width==1120 && restored.ProfileGuid=="profile-1", "workspace state persistence");
        Check(!File.ReadAllText(prefsPath).Contains("VersionName"), "no duplicated build settings");
        var fake = new FakeClient(); var vm = new BuildWorkspaceViewModel(fake) { Confirm=_=>true };
        vm.ChangeProject(fixture); Check(await vm.ReloadAsync(), "catalog load");
        Check(vm.Profiles.Count==5 && vm.HasSigning && vm.HasVersionCode, "five profiles and registered capabilities");
        vm.Selected = vm.Profiles[2]; Check(!vm.HasSigning && !vm.HasVersionCode, "Windows unsupported fields hidden");
        vm.Selected = vm.Profiles[0]; vm.VersionName="2.0.0"; vm.Options[0].Enabled=true;
        Check(vm.Dirty, "editing marks dirty");
        vm.Confirm=_=>false; vm.Selected=vm.Profiles[2]; Check(vm.Selected==vm.Profiles[0] && vm.Dirty,"discard cancellation preserves edit");
        vm.Confirm=_=>true; Check(await vm.SaveAsync() && !vm.Dirty && vm.VersionName=="2.0.0", "save applies returned source");
        Check(fake.LastRequest!["appVersion"].Text("name")=="2.0.0" && fake.LastRequest["options"]!.Flag("debug-mode"),"save payload matches UI");
        vm.VersionName="3.0.0"; vm.KeystorePath="pending.keystore";
        Check(await vm.SaveAsync() && vm.SigningDirty && vm.KeystorePath=="pending.keystore", "version save preserves pending signing edit");
        await vm.ReloadAsync();
        fake.Fail="RevisionConflict"; vm.VersionName="4.0.0";
        Check(!await vm.SaveAsync() && vm.Dirty && vm.StatusIsError && vm.VersionName=="4.0.0", "conflict preserves user draft"); fake.Fail=null;
        vm.Confirm=_=>false; int calls=fake.Calls;
        Check(!await vm.ExecuteProductAsync(true) && fake.Calls==calls,"cancel save/build confirmation never executes");
        vm.Confirm=_=>true; await vm.ReloadAsync();
        Check(await vm.ExecuteProductAsync(false) && vm.Status.Contains("실제 빌드"),"check distinct from build success");
        fake.Delay=new TaskCompletionSource(); var pending=vm.ExecuteProductAsync(true);
        Check(vm.Busy && !vm.CanEdit && !vm.ChangeProject("other"),"busy guards project change");
        Check(!await vm.ReloadAsync(),"no concurrent reload");
        vm.Cancel.Execute(null); Check(fake.Canceled,"safe cancel request routed");
        fake.Delay.SetResult(); await pending; fake.Delay=null;
        fake.Fail="MissingResult"; Check(!await vm.ExecuteProductAsync(false) && vm.StatusIsError,"missing result surfaced"); fake.Fail=null;
        Check(await vm.CleanupAsync(true) && vm.Logs.HasPreview,"cleanup preview");
        vm.Logs.Days="14"; Check(!vm.Logs.HasPreview,"retention change invalidates preview");
        await vm.CleanupAsync(true); fake.CleanupStatus="Busy";
        Check(!await vm.CleanupAsync(false) && vm.Logs.Report.Contains("보류") && vm.StatusIsError,"busy cleanup is not success");
        fake.CleanupStatus="Complete"; await vm.CleanupAsync(true); Check(await vm.CleanupAsync(false),"fixture cleanup UI flow");
        vm.Logs.Days="0"; Check(!await vm.CleanupAsync(true),"invalid retention rejected");
        foreach(string status in new[]{"Partial","Error"}) {
            vm.Logs.Days="7"; fake.CleanupStatus=status;
            Check(!await vm.CleanupAsync(true) && !vm.Logs.HasPreview,"cleanup "+status+" not success");
        }
        var cipher = new BuildToolSigningPasswords("fixture-only",fixture);
        cipher.Save("fixture-store-secret","fixture-key-secret");
        Check(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(cipher.FilePath)).Contains("fixture-store-secret"),"encrypted storage contains no plain secret");
        Check(cipher.TryRead(out var a,out var b) && a=="fixture-store-secret" && b=="fixture-key-secret","shared DPAPI source roundtrip");
        string id=Guid.NewGuid().ToString("N"), transferDir=Path.Combine(project,"Library","MatchBuildApp",id);
        var transfer=new BuildToolSigningPasswords("external-transfer\n"+Path.GetFullPath(project).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+"\n"+id,transferDir);
        transfer.Save("fixture-store-secret","fixture-key-secret");
        File.WriteAllText(Path.Combine(project,"Temp","BuildAppCrossRuntime.json"),new JsonObject {["requestId"]=id,["credentialsFile"]=transfer.FilePath}.ToJsonString());
        Check(File.Exists(transfer.FilePath),"cross-runtime fixture prepared without plaintext payload");
        var window = new MainWindow(new FakeClient(),prefsPath,false) { ShowInTaskbar=false,Title="빌드 도구 UI 검증 · 임시 데이터" };
        window.ViewModel.Confirm=_=>true; window.ViewModel.ChangeProject(project); await window.ViewModel.ReloadAsync();
        window.Show(); window.UpdateLayout();
        Check(window.Left<SystemParameters.WorkArea.Right && window.Top<SystemParameters.WorkArea.Bottom,"off-screen placement restored");
        string images=Path.Combine(project,"Logs","ExternalBuildStage02"); Directory.CreateDirectory(images);
        foreach (var size in new[]{(1240d,840d),(1000d,700d)}) {
            window.Width=size.Item1; window.Height=size.Item2; await window.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle); window.UpdateLayout();
            var root=(FrameworkElement)window.Content;
            Check(root.ActualWidth>0 && root.ActualHeight>0,"window arranged");
            var build=Find<Button>(root).Single(x=>Equals(x.Content,"빌드 시작…"));
            var bounds=build.TransformToAncestor(root).TransformBounds(new Rect(0,0,build.ActualWidth,build.ActualHeight));
            Check(bounds.Bottom<=root.ActualHeight+1 && bounds.Right<=root.ActualWidth+1,"build action remains visible");
            double scale=size.Item1==1000?1.5:1;
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth*scale),(int)Math.Ceiling(root.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);
            var drawing=new DrawingVisual(); using(var dc=drawing.RenderOpen()) dc.DrawRectangle(new VisualBrush(root),null,new Rect(0,0,root.ActualWidth,root.ActualHeight));
            bitmap.Render(drawing); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file=File.Create(Path.Combine(images,$"ui-{size.Item1}.png")); encoder.Save(file);
        }
        var list=Find<ListBox>(window).Single(x=>x.Name=="ProfileList");
        Check(list.Items.Count==5,"profile list rendered");
        Check(list.ActualHeight>=200,"profile list not collapsed in minimum window");
        list.SelectedIndex=2; window.UpdateLayout();
        Check(window.ViewModel.Selected?.Guid=="profile-2" && !window.ViewModel.HasSigning,"rendered profile selection binding");
        Check(window.InputBindings.OfType<KeyBinding>().Count()==4,"four keyboard command bindings");
        var textBox=Find<TextBox>(window).First(x=>x.IsVisible && x.IsEnabled && !x.IsReadOnly);
        textBox.Focus(); Check(textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)),"keyboard focus traverses inputs");
        window.ViewModel.VersionName="9.0.0";
        window.ViewModel.Save.Execute(null);
        await window.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Check(!window.ViewModel.Dirty,"UI save command binding");
        var tabs=Find<TabControl>(window).Single(); tabs.SelectedIndex=1;
        await window.ViewModel.CleanupAsync(true); window.UpdateLayout();
        var preview=Find<Button>(window).Single(x=>Equals(x.Content,"후보 미리보기"));
        Check(preview.IsVisible && preview.IsEnabled,"logs tab controls accessible");
        var logRoot=(FrameworkElement)window.Content;
        var logBitmap=new RenderTargetBitmap((int)logRoot.ActualWidth,(int)logRoot.ActualHeight,96,96,PixelFormats.Pbgra32);
        var logDrawing=new DrawingVisual(); using(var dc=logDrawing.RenderOpen()) dc.DrawRectangle(new VisualBrush(logRoot),null,new Rect(0,0,logRoot.ActualWidth,logRoot.ActualHeight));
        logBitmap.Render(logDrawing); var logEncoder=new PngBitmapEncoder();logEncoder.Frames.Add(BitmapFrame.Create(logBitmap));
        using(var file=File.Create(Path.Combine(images,"ui-logs.png"))) logEncoder.Save(file);
        window.Close();
        Check(WorkspacePreferences.Load(prefsPath).Width==1000,"last window size saved");
    }
    static IEnumerable<T> Find<T>(DependencyObject item) where T:DependencyObject
    { if(item is T typed) yield return typed; for(int i=0;i<VisualTreeHelper.GetChildrenCount(item);i++) foreach(var child in Find<T>(VisualTreeHelper.GetChild(item,i))) yield return child; }
}

internal sealed class FakeClient : IBuildToolClient
{
    public int Calls; public bool Canceled; public string? Fail; public string CleanupStatus="Complete"; public JsonObject? LastRequest; public TaskCompletionSource? Delay;
    private JsonArray profiles=new(Enumerable.Range(0,5).Select(i=>(JsonNode)new JsonObject {
        ["profileGuid"]="profile-"+i,["name"]=new[]{"Android Game","Android Level Editor","Windows Steam Game","Windows Level Editor","iOS Game"}[i],
        ["target"]=i<2?"Android":i==4?"iOS":"StandaloneWindows64",["productName"]="달 토끼 고물상",["applicationId"]="com.example.fixture",["output"]="Build/Fixture",["revision"]="initial",
        ["appVersion"]=new JsonObject {["name"]="1.0.0",["code"]=i<2||i==4?"1":null,["codeLabel"]=i<2?"Version Code":i==4?"Build Number":null},
        ["options"]=new JsonArray(new JsonObject {["id"]="debug-mode",["label"]="디버그 모드",["description"]="디버그 로그를 포함합니다. 제품별 옵션 정의를 그대로 표시합니다.",["on"]="ON",["off"]="OFF",["effective"]=false}),
        ["signing"]=i<2?new JsonObject {["customKeystore"]=false,["keystorePath"]="",["alias"]="",["credentialsStored"]=false}:null }).ToArray());
    public async Task<JsonObject> SendAsync(string project,string unity,JsonObject request,IProgress<BuildProgress>? progress=null,SecretInput? secrets=null)
    {
        Calls++; LastRequest=(JsonObject)request.DeepClone();
        if(Delay!=null) await Delay.Task;
        if(Fail!=null) throw new BuildToolException(Fail,Fail);
        string operation=request.Text("operation"); JsonNode data;
        if(operation=="settings") data=new JsonObject {["profiles"]=profiles.DeepClone()};
        else if(operation=="save-settings") {
            var p=(JsonObject)profiles.Single(x=>x.Text("profileGuid")==request.Text("profileGuid"))!;
            foreach(var pair in (JsonObject)request["appVersion"]!) p["appVersion"]![pair.Key]=pair.Value?.DeepClone();
            foreach(var option in (JsonArray)p["options"]!) option!["effective"]=request["options"]![option.Text("id")]?.DeepClone();
            p["revision"]=Guid.NewGuid().ToString("N"); data=p.DeepClone();
        } else if(operation.StartsWith("cleanup")) data=new JsonObject {["Status"]=CleanupStatus,["CandidateBytes"]=100L,["DeletedBytes"]=operation=="cleanup"?100L:0L,["CandidateDetails"]=new JsonArray(new JsonObject {["Path"]="fixture.log",["Bytes"]=100L}),["Skipped"]=new JsonArray("active.log"),["Errors"]=new JsonArray()};
        else data=new JsonObject {["phase"]=Canceled?"Canceled":"Succeeded",["output"]="Build/Fixture"};
        return new JsonObject {["version"]=2,["requestId"]="fixture",["exitCode"]=CleanupStatus=="Busy"?2:0,["data"]=data};
    }
    public void RequestCancel()=>Canceled=true;
}
