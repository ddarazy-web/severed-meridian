using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using MoonRabbit.BuildApp;

internal static class ProfileCacheChecks
{
    internal static async Task Run(string fixture, Action<bool,string> check)
    {
        string project=Path.Combine(fixture,"cached-project"); Directory.CreateDirectory(project);
        var client=new FakeClient(); var vm=new BuildWorkspaceViewModel(client){Confirm=_=>true}; vm.ChangeProject(project);
        check(await vm.InitializeProfilesAsync() && client.Calls==1,"cache cold start queries once");
        var cachedClient=new FakeClient(); var cached=new BuildWorkspaceViewModel(cachedClient){Confirm=_=>true}; cached.ChangeProject(project);
        check(await cached.InitializeProfilesAsync(vm.Profiles[2].Guid) && cachedClient.Calls==0 && cached.Selected?.Guid==vm.Profiles[2].Guid && !cached.Busy,"cache warm start skips Unity and restores selection");
        check(cached.LastQuery.StartsWith("저장된 조회") && cached.Status.Contains("F5"),"cache age and refresh instruction visible");
        check(cached.ProductRequest("build").Text("expectedRevision")=="initial","cached build retains revision validation");
        cached.VersionName="8.2.1"; await cached.SaveAsync();
        var reopen=new BuildWorkspaceViewModel(new FakeClient()); reopen.ChangeProject(project); await reopen.InitializeProfilesAsync(cached.Selected!.Guid);
        check(reopen.VersionName=="8.2.1" && !reopen.Dirty,"successful save refreshes cached source");
        check(await cached.ReloadAsync() && cachedClient.Operations.Last()=="settings","F5 explicitly refreshes cached profiles");
        string before=File.ReadAllText(ProfileCache.Location(project)); cachedClient.Fail="MissingResult";
        check(!await cached.ReloadAsync() && before==File.ReadAllText(ProfileCache.Location(project)),"failed refresh preserves last successful cache");
        var profile=(JsonObject)cached.Profiles[0].Data.DeepClone(); profile["password"]="must-not-persist"; profile["signing"]!["storePassword"]="must-not-persist";
        ProfileCache.Write(project,[profile],DateTimeOffset.Now);
        check(!File.ReadAllText(ProfileCache.Location(project)).Contains("must-not-persist"),"cache whitelist excludes credentials");
        string other=Path.Combine(fixture,"other-project"); Directory.CreateDirectory(Path.Combine(other,"UserSettings")); File.Copy(ProfileCache.Location(project),ProfileCache.Location(other));
        check(!ProfileCache.TryRead(other,out _,out _),"cache cannot cross project boundaries");
        foreach(string corrupt in new[]{"invalid json","{\"version\":2}","null"})
        {
            File.WriteAllText(ProfileCache.Location(project),corrupt);
            var fallbackClient=new FakeClient(); var fallback=new BuildWorkspaceViewModel(fallbackClient); fallback.ChangeProject(project);
            check(await fallback.InitializeProfilesAsync() && fallbackClient.Calls==1,"invalid cache safely falls back to query");
        }
        string preferences=Path.Combine(project,"workspace.json"); new WorkspacePreferences{ProjectPath=project,ProfileGuid="profile-2"}.Save(preferences);
        var windowClient=new FakeClient(); var window=new MainWindow(windowClient,preferences,true){ShowInTaskbar=false};
        window.Show(); await window.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        check(windowClient.Calls==0 && window.ViewModel.Profiles.Count==5 && !window.ViewModel.IsLoadingProfiles,"actual window startup uses cache without loading overlay or requests");
        window.Close();
    }
}
