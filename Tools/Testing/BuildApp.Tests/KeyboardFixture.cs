using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using MoonRabbit.BuildApp;

// Manual keyboard checks use the production window with an in-memory transport only.
internal static class KeyboardFixture
{
    public static void Open(Application app)
    {
        string root = Path.Combine(WorkspacePreferences.DetectProject(Environment.CurrentDirectory),
            "Logs", "ExternalBuildStage03", "Keyboard-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), "한글 프로젝트 경로");
        Directory.CreateDirectory(root);
        string preferences = Path.Combine(root, "workspace.json");
        new WorkspacePreferences { ProjectPath=root, Width=1000, Height=700 }.Save(preferences);
        var client = new FakeClient();
        var window = new MainWindow(client, preferences, false) { Title="키보드 검사 · 임시 데이터만 사용 · 실제 빌드 없음" };
        window.ViewModel.ChangeProject(root);
        window.Loaded += async (_, _) => await window.ViewModel.ReloadAsync();
        window.Closed += (_, _) =>
        {
            // Persist only fake server metadata; never capture password input.
            var report = new JsonObject
            {
                ["transport"]="FakeClient (in memory)",
                ["operations"]=new JsonArray(client.Operations.Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray()),
                ["profiles"]=new JsonArray(window.ViewModel.Profiles.Select(x=>(JsonNode?)x.Data.DeepClone()).ToArray()),
                ["dirty"]=window.ViewModel.Dirty,
                ["signingDirty"]=window.ViewModel.SigningDirty,
                ["logRetentionDays"]=window.ViewModel.Logs.Days,
                ["realProductBuildCalls"]=0
            };
            File.WriteAllText(Path.Combine(root,"keyboard-result.json"),report.ToJsonString(new(){WriteIndented=true}));
            app.Shutdown();
        };
        window.Show();
    }
}
