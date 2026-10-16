using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using MoonRabbit.BuildApp;
using Products.Editor;

internal static class Program
{
    static int passed;
    [STAThread]
    static int Main()
    {
        var args=Environment.GetCommandLineArgs();
        if(args.Length==3 && args[1]=="--crash-client"){ProcessCrashChecks.Client(args[2]).GetAwaiter().GetResult();return 0;}
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; app.Resources = new ResourceDictionary { Source = new Uri("pack://application:,,,/MoonRabbit.BuildApp;component/Theme.xaml") };
        if(args.Contains("--keyboard-fixture")) { KeyboardFixture.Open(app); app.Run(); return 0; }
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
        RecoveryChecks.Run(fixture,Check);
        await ProcessCrashChecks.Run(fixture,Check);
        await ClientTransportChecks.Run(fixture,Check);
        await ProfileCacheChecks.Run(fixture,Check);
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
        var uiClient = new FakeClient();
        var window = new MainWindow(uiClient,prefsPath,false) { ShowInTaskbar=false,Title="빌드 도구 UI 검증 · 임시 데이터" };
        window.ViewModel.Confirm=_=>true; window.ViewModel.ChangeProject(fixture); await window.ViewModel.ReloadAsync();
        window.Show(); window.UpdateLayout();
        uiClient.Delay=new TaskCompletionSource(); var loading=window.ViewModel.ReloadAsync(); window.UpdateLayout();
        Check(window.ViewModel.IsLoadingProfiles && !window.ViewModel.CanUseWorkspace && ((Border)window.FindName("LoadingOverlay")).IsVisible,"profile loading overlays and blocks workspace");
        var loadingRoot=(FrameworkElement)window.Content;
        var loadingBitmap=new RenderTargetBitmap((int)loadingRoot.ActualWidth,(int)loadingRoot.ActualHeight,96,96,PixelFormats.Pbgra32);
        loadingBitmap.Render(loadingRoot); var loadingEncoder=new PngBitmapEncoder(); loadingEncoder.Frames.Add(BitmapFrame.Create(loadingBitmap));
        using(var loadingFile=File.Create(Path.Combine(project,"Logs","ExternalBuildStage03","loading-overlay.png"))) loadingEncoder.Save(loadingFile);
        uiClient.Delay.SetResult(); await loading; uiClient.Delay=null; window.UpdateLayout();
        Check(!((Border)window.FindName("LoadingOverlay")).IsVisible && window.ViewModel.CanUseWorkspace,"loading overlay clears on success");
        uiClient.Fail="MissingResult"; await window.ViewModel.ReloadAsync(); uiClient.Fail=null; window.UpdateLayout();
        Check(!window.ViewModel.IsLoadingProfiles && window.ViewModel.CanUseWorkspace && window.ViewModel.StatusIsError,"loading failure releases overlay and shows error");
        var cleanupMenu=Find<MenuItem>(window).Single(x=>Equals(x.Header,"로그 정리"));
        int requestsBeforeMenu=uiClient.Operations.Count;
        cleanupMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Check(((TabItem)window.FindName("LogManagementTab")).IsSelected && uiClient.Operations.Count==requestsBeforeMenu,"log menu opens management without cleanup or query");
        Find<TabControl>(window).Single().SelectedIndex=0;
        window.ViewModel.CustomKeystore=true; window.ViewModel.KeystorePath="fixture.keystore"; window.ViewModel.Alias="fixture-key";
        await window.ViewModel.SigningAsync(false,"fixture-only","fixture-only"); window.UpdateLayout();
        var aliasPicker=Find<ComboBox>(window).Single(); aliasPicker.SelectedIndex=0;
        await window.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Check(await window.ViewModel.SigningAsync(true,"fixture-only","fixture-only"),"fixture signing saved");
        await window.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Check(aliasPicker.Text=="fixture-key" && window.ViewModel.Alias=="fixture-key","selected alias survives item reset after signing save");
        var signingTabs=Find<TabControl>(window).Single(); signingTabs.SelectedIndex=1; window.UpdateLayout(); signingTabs.SelectedIndex=0; window.UpdateLayout();
        Check(window.ViewModel.Alias=="fixture-key" && window.ViewModel.KeystorePath=="fixture.keystore" && !window.ViewModel.SigningDirty,"signing survives tab round trip");
        window.ViewModel.Selected=window.ViewModel.Profiles[2]; window.UpdateLayout(); window.ViewModel.Selected=window.ViewModel.Profiles[0]; window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Check(window.ViewModel.Alias=="fixture-key" && window.ViewModel.CustomKeystore && !window.ViewModel.SigningDirty,"signing survives profile round trip");
        Check(window.ViewModel.HasSavedSigning && Find<TextBlock>(window).Count(x=>x.Text=="✓ 암호화 저장됨" && x.IsVisible)==2,"both saved password indicators restored");
        window.ViewModel.Alias="other-key";
        Check(!window.ViewModel.HasSavedSigning,"changed signing identity hides saved password indicators");
        window.ViewModel.Selected=window.ViewModel.Profiles[2]; window.ViewModel.Selected=window.ViewModel.Profiles[0];
        await window.ViewModel.ReloadAsync(); window.UpdateLayout();
        Check(window.ViewModel.HasSavedSigning && window.ViewModel.Alias=="fixture-key","saved signing restored after fresh settings query");
        Check(Find<MenuItem>(window).Any(x=>Equals(x.Header,"프로젝트")),"project actions moved to header menu");
        var wheelButton=new Button {Content="wheel target"};
        var inner=new TextBox {Text=string.Join("\n",Enumerable.Range(0,30)),Height=70,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var wheelContent=new StackPanel();wheelContent.Children.Add(wheelButton);wheelContent.Children.Add(inner);wheelContent.Children.Add(new Border {Height=1600});
        var outer=new ScrollViewer {Content=wheelContent};
        var wheelWindow=new MainWindow(new FakeClient(),Path.Combine(project,"wheel-prefs.json"),false){Content=outer,ShowInTaskbar=false};
        wheelWindow.Show();wheelWindow.UpdateLayout();
        wheelButton.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent});
        wheelWindow.UpdateLayout();Check(outer.VerticalOffset>0,"wheel over button scrolls containing pane");
        outer.ScrollToTop();inner.ScrollToEnd();wheelWindow.UpdateLayout();
        var innerScroll=Find<ScrollViewer>(inner).Single();
        innerScroll.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent});
        wheelWindow.UpdateLayout();Check(outer.VerticalOffset>0,"nested input at bottom forwards wheel to parent");
        outer.ScrollToTop();inner.ScrollToHome();wheelWindow.UpdateLayout();
        innerScroll.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent});
        wheelWindow.UpdateLayout();Check(innerScroll.VerticalOffset>0&&outer.VerticalOffset==0,"nested input scrolls before parent");
        wheelWindow.Close();
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
        list.Height=120;window.UpdateLayout();
        var profileScroll=Find<ScrollViewer>(list).Single();
        ((ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0)).RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent});
        window.UpdateLayout();Check(profileScroll.VerticalOffset>0,"wheel over profile item scrolls bounded list");
        list.Height=double.NaN;window.UpdateLayout();
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
        var help=Find<Button>(window).Single(x=>Equals(x.Content,"사용 안내"));help.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var helpWindow=Application.Current.Windows.OfType<Window>().Single(x=>x.Title=="외부 빌드 도구 · 사용 안내");
        Check(Find<TextBox>(helpWindow).Single().Text.Contains("로그 정리"),"packaged Markdown help opens inside app");helpWindow.Close();
        window.ViewModel.RefreshRecovery();tabs.SelectedIndex=2;window.UpdateLayout();
        Check(Find<Button>(window).Single(x=>Equals(x.Content,"상태 확인")).IsVisible,"recovery tab accessible");
        tabs.SelectedIndex=1;window.UpdateLayout();
        var logRoot=(FrameworkElement)window.Content;
        var logBitmap=new RenderTargetBitmap((int)logRoot.ActualWidth,(int)logRoot.ActualHeight,96,96,PixelFormats.Pbgra32);
        var logDrawing=new DrawingVisual(); using(var dc=logDrawing.RenderOpen()) dc.DrawRectangle(new VisualBrush(logRoot),null,new Rect(0,0,logRoot.ActualWidth,logRoot.ActualHeight));
        logBitmap.Render(logDrawing); var logEncoder=new PngBitmapEncoder();logEncoder.Frames.Add(BitmapFrame.Create(logBitmap));
        using(var file=File.Create(Path.Combine(images,"ui-logs.png"))) logEncoder.Save(file);
        window.Close();
        Check(WorkspacePreferences.Load(prefsPath).Width==1000,"last window size saved");
        int monitorIndex=0;
        var dragWindow=new MainWindow(new FakeClient(),Path.Combine(fixture,"drag.json"),false){ShowInTaskbar=false,Width=1240,Height=840};dragWindow.Show();
        await dragWindow.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        foreach(var destination in WindowPlacement.WorkAreas())
        {
            SetWindowPos(new WindowInteropHelper(dragWindow).Handle,IntPtr.Zero,(int)destination.X+10,(int)destination.Y+10,0,0,0x15);
            await dragWindow.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var moved=WindowPlacement.ReadBounds(dragWindow).Rectangle;
            Check(moved.Right<=destination.Right+1&&moved.Bottom<=destination.Bottom+1,"running window fits destination monitor");
        }
        dragWindow.Close();
        foreach(var area in WindowPlacement.WorkAreas())
        {
            string monitorPrefs=Path.Combine(fixture,$"monitor-{monitorIndex}.json");
            new WorkspacePreferences {ProjectPath=fixture,PhysicalBounds=new PixelBounds{X=(int)area.X+20,Y=(int)area.Y+20,Width=1000,Height=700}}.Save(monitorPrefs);
            var displayWindow=new MainWindow(new FakeClient(),monitorPrefs,false){ShowInTaskbar=false};displayWindow.Show();
            await displayWindow.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var physical=WindowPlacement.ReadBounds(displayWindow).Rectangle;
            Check(physical.Left>=area.Left-1&&physical.Top>=area.Top-1&&physical.Right<=area.Right+1&&physical.Bottom<=area.Bottom+1,"actual monitor "+monitorIndex+" placement");
            Console.WriteLine($"DISPLAY {monitorIndex}: DPI {VisualTreeHelper.GetDpi(displayWindow).PixelsPerInchX}, bounds {physical}");
            displayWindow.Close();
            var reopen=new MainWindow(new FakeClient(),monitorPrefs,false){ShowInTaskbar=false};reopen.Show();
            await reopen.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var restoredBounds=WindowPlacement.ReadBounds(reopen).Rectangle;
            Check(Math.Abs(restoredBounds.X-physical.X)<2&&Math.Abs(restoredBounds.Y-physical.Y)<2,"actual monitor "+monitorIndex+" restart restore");
            Check(Math.Abs(restoredBounds.Width-physical.Width)<2&&Math.Abs(restoredBounds.Height-physical.Height)<2&&restoredBounds.Right<=area.Right+1&&restoredBounds.Bottom<=area.Bottom+1,"actual monitor "+monitorIndex+" restart size retained");
            reopen.WindowState=WindowState.Maximized;
            await reopen.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            reopen.Close();
            var maximized=new MainWindow(new FakeClient(),monitorPrefs,false){ShowInTaskbar=false};maximized.Show();
            await maximized.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Check(maximized.WindowState==WindowState.Maximized,"maximized restart restore");maximized.Close();monitorIndex++;
        }
    }
    static IEnumerable<T> Find<T>(DependencyObject item) where T:DependencyObject
    { if(item is T typed) yield return typed; for(int i=0;i<VisualTreeHelper.GetChildrenCount(item);i++) foreach(var child in Find<T>(VisualTreeHelper.GetChild(item,i))) yield return child; }
    [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
}

internal sealed class FakeClient : IBuildToolClient
{
    public List<string> Operations { get; } = [];
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
        Operations.Add(request.Text("operation"));
        if(Delay!=null) await Delay.Task;
        if(Fail!=null) throw new BuildToolException(Fail,Fail);
        string operation=request.Text("operation"); JsonNode data;
        if(operation=="settings") data=new JsonObject {["profiles"]=profiles.DeepClone()};
        else if(operation=="signing-aliases") data=new JsonObject {["aliases"]=new JsonArray("fixture-key","fixture-alternate")};
        else if(operation=="save-signing") {
            var p=(JsonObject)profiles.Single(x=>x.Text("profileGuid")==request.Text("profileGuid"))!;
            p["signing"]=new JsonObject {["customKeystore"]=request["customKeystore"]?.DeepClone(),["keystorePath"]=request["keystorePath"]?.DeepClone(),["alias"]=request["alias"]?.DeepClone(),["credentialsStored"]=true};
            data=p.DeepClone();
        }
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
