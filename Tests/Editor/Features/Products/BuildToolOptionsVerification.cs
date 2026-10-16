using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Products.Editor
{
    public static class BuildToolOptionsVerification
    {
        public static void Run()
        {
            try
            {
                var snapshot = new BuildToolStateSnapshot();
                var fixture = new OptionFixture();
                var choices = new Dictionary<string, bool> { ["feature"] = true, ["unknown"] = true };
                Check(fixture.Registry.Resolve(fixture.Backend.Profile.Product, choices).Single().enabled, "supported options resolve and unknown choices never execute");
                fixture.Handler.Definition.DefaultEnabled = true;
                Check(fixture.Registry.Resolve(fixture.Backend.Profile.Product, null).Single().enabled, "registered default applies without a saved choice");
                fixture.Handler.Definition.DefaultEnabled = false;
                fixture.Handler.Definition.AppliesTo = product => !product.IsTool;
                Check(fixture.Registry.Available(fixture.Backend.Profile.Product).Count == 0, "unsupported product hides option");
                fixture.Handler.Definition.AppliesTo = product => true;
                Reject(() => new BuildToolOptions(new[] { fixture.Handler, fixture.Handler }), "duplicate option ID rejected");
                fixture.Handler.Definition.Requires = new[] { "missing" };
                Reject(() => fixture.Registry.Resolve(fixture.Backend.Profile.Product, choices), "missing dependency rejected");
                var second = new TestBuildOption("second");
                fixture.Handler.Definition.Requires = new[] { "second" }; second.Definition.Requires = new[] { "feature" };
                var two = new BuildToolOptions(new[] { fixture.Handler, second }, fixture.Settings);
                Reject(() => two.Resolve(fixture.Backend.Profile.Product, new Dictionary<string, bool> { ["feature"] = true, ["second"] = true }), "dependency cycle rejected");
                second.Definition.Requires = Array.Empty<string>();
                Check(two.Resolve(fixture.Backend.Profile.Product, new Dictionary<string, bool> { ["feature"] = true, ["second"] = true })
                    .Select(state => state.id).SequenceEqual(new[] { "second", "feature" }), "dependencies determine stable execution order");
                Reject(() => two.Resolve(fixture.Backend.Profile.Product, choices), "enabled option cannot depend on OFF option");
                fixture.Handler.Definition.Requires = Array.Empty<string>(); second.Definition.Conflicts = new[] { "feature" };
                Reject(() => two.Resolve(fixture.Backend.Profile.Product, new Dictionary<string, bool> { ["feature"] = true, ["second"] = true }), "one-sided conflict rejected");
                second.Definition.Conflicts = Array.Empty<string>(); second.Definition.Settings = new[] { "config" };
                Reject(() => two.Resolve(fixture.Backend.Profile.Product, choices), "setting ownership conflicts even with OFF option");
                second.Definition.Settings = new[] { "profile.scenes" };
                Reject(() => new BuildToolOptions(new[] { second }, fixture.Settings), "reserved product setting cannot be claimed");
                second.Definition.Settings = Array.Empty<string>(); second.Definition.Symbols = new[] { "PRODUCT_BROKEN" };
                Reject(() => new BuildToolOptions(new[] { second }, fixture.Settings), "product symbols cannot become optional");

                fixture = new OptionFixture(); var runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, choices);
                choices["feature"] = false; runner.Step();
                Check(runner.Current.options.Single().enabled && fixture.Values["config"] == "on" && runner.Current.fingerprint != runner.Current.effectiveFingerprint,
                    "request freezes choice and separates original from predicted effective fingerprint");
                fixture.Drive(runner);
                Check(runner.Current.phase == BuildToolRunPhase.Succeeded && fixture.Backend.BuildCalls == 1 && fixture.Values["config"] == "off" && fixture.Values["plugin"] == "original", "all stages run once and restore originals");
                Check(fixture.Events.SequenceEqual(new[] { "PrepareCompilation", "BeforeBuild", "AfterBuild", "Cleanup" }), "stage order is explicit");
                Check(fixture.Writes.Skip(fixture.Writes.Count - 3).SequenceEqual(new[] { "plugin=on", "plugin=original", "config=off" }), "temporary mutations restore in reverse order");
                fixture.Events.Clear(); runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, choices); fixture.Drive(runner);
                Check(runner.Current.IsTerminal && fixture.Events.Contains("BeforeBuild") && fixture.Writes.Contains("plugin=off") && fixture.Values["plugin"] == "original", "OFF runs explicitly after ON without residue");
                runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, On()); fixture.Drive(runner);
                Check(runner.Current.phase == BuildToolRunPhase.Succeeded && fixture.Backend.BuildCalls == 3 && fixture.Values["config"] == "off", "repeated ON remains isolated");
                fixture.Backend.Profile = BuildToolProfiles.Discover().First(item => item.Product?.Name == "Android Game" && !item.IsCopy);
                runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, choices); fixture.Drive(runner);
                Check(runner.Current.phase == BuildToolRunPhase.Succeeded && runner.Current.product == "Android Game" && !runner.Current.options.Single().enabled &&
                    fixture.Values["config"] == "off" && fixture.Values["plugin"] == "original", "switching product uses its frozen OFF choice without previous product residue");

                fixture = new OptionFixture { FailPrepare = true }; runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, On()); runner.Step();
                Check(runner.Current.phase == BuildToolRunPhase.Failed && fixture.Backend.BuildCalls == 0 && fixture.Values["config"] == "off", "partial option preparation failure restores without build");
                fixture = new OptionFixture { FailPost = true }; runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, On()); fixture.Drive(runner);
                Check(runner.Current.phase == BuildToolRunPhase.Failed && runner.Current.result.outcome == BuildToolRunPhase.Succeeded && runner.Current.postprocessError != null && File.Exists(fixture.Output), "postprocess failure preserves Player success and partial output");
                var recoveryErrors = new List<string>();
                fixture = new OptionFixture(); runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, On());
                for (int i = 0; i < 8 && runner.Current.phase != BuildToolRunPhase.Postprocessing; i++) runner.Step();
                var interrupted = new BuildToolRunner(fixture.Backend, runner.Store, "new-session", fixture.Registry); interrupted.ResolveInterrupted();
                if (!(interrupted.Current.phase == BuildToolRunPhase.Failed && interrupted.Current.result.outcome == BuildToolRunPhase.Succeeded && interrupted.Current.postprocessError != null))
                    recoveryErrors.Add("restart cleanup cannot claim unexecuted postprocessing succeeded");
                fixture = new OptionFixture(); runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, On());
                for (int i = 0; i < 8 && runner.Current.phase != BuildToolRunPhase.Postprocessing; i++) runner.Step();
                fixture.Backend.CompilationErrors = true; runner.Step();
                if (!(runner.Current.phase == BuildToolRunPhase.Failed && !File.Exists(fixture.Output))) recoveryErrors.Add("compilation failure blocks postprocessing and still cleans");
                fixture = new OptionFixture(); runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, On());
                for (int i = 0; i < 8 && runner.Current.phase != BuildToolRunPhase.Postprocessing; i++) runner.Step();
                fixture.Registry.RunStage(BuildToolOptionStage.AfterBuild, runner.Current, runner.Store, fixture.Backend);
                runner.Current.phase = BuildToolRunPhase.Finalizing; runner.Store.Save(runner.Current); fixture.Values["plugin"] = "original"; runner.Step();
                if (!(runner.Current.phase == BuildToolRunPhase.Succeeded && fixture.Values["plugin"] == "original")) recoveryErrors.Add("cleanup accepts a multi-stage setting already at its original value");
                fixture = new OptionFixture(); runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, On());
                for (int i = 0; i < 8 && runner.Current.phase != BuildToolRunPhase.Postprocessing; i++) runner.Step();
                fixture.Values["plugin"] = "foreign"; runner.Step();
                if (!(runner.Current.phase == BuildToolRunPhase.NeedsAttention && fixture.Values["plugin"] == "foreign" && runner.Current.postprocessError != null))
                    recoveryErrors.Add("later stage cannot overwrite a foreign non-profile setting");
                fixture = new OptionFixture(); runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, On());
                for (int i = 0; i < 8 && runner.Current.phase != BuildToolRunPhase.Postprocessing; i++) runner.Step();
                fixture.Registry.RunStage(BuildToolOptionStage.AfterBuild, runner.Current, runner.Store, fixture.Backend);
                runner.Current.phase = BuildToolRunPhase.Finalizing; runner.Store.Save(runner.Current);
                using (var held = new FileStream(runner.Store.CurrentPath, FileMode.Open, FileAccess.Read, FileShare.Read)) { runner.Step(); }
                var afterRecordingFailure = new BuildToolRunner(fixture.Backend, runner.Store, "options-session", fixture.Registry); afterRecordingFailure.Step();
                if (!(afterRecordingFailure.Current.phase == BuildToolRunPhase.Succeeded && fixture.Values["plugin"] == "original" && fixture.Backend.BuildCalls == 1))
                    recoveryErrors.Add("restored settings reconcile after persistent journal-write failure and reconstruction");
                if (recoveryErrors.Count > 0) throw new Exception(string.Join("\n", recoveryErrors));
                Check(true, "restart outcome, compilation gate, foreign-setting protection and stale cleanup journal regressions");
                fixture = new OptionFixture { FailRestore = true }; runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, On()); fixture.Drive(runner);
                Check(runner.Current.phase == BuildToolRunPhase.NeedsAttention && fixture.Backend.CleanupCalls == 1, "option restore failure cannot skip backend cleanup");
                fixture.FailRestore = false; runner.ResolveInterrupted();
                Check(runner.Current.IsTerminal && fixture.Values["config"] == "off" && fixture.Backend.BuildCalls == 1, "explicit restore retries without another build");
                fixture = new OptionFixture(); runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, On()); runner.Step(); fixture.Backend.Version = "external"; runner.Step();
                Check(runner.Current.phase == BuildToolRunPhase.Failed && fixture.Backend.Version == "external" && fixture.Backend.BuildCalls == 0, "external profile edit never becomes new fingerprint baseline");
                fixture = new OptionFixture(); runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, On()); runner.Step(); fixture.Values["config"] = "foreign"; runner.Cancel();
                Check(runner.Current.phase == BuildToolRunPhase.NeedsAttention && fixture.Values["config"] == "foreign" && runner.Current.cleanupError.Contains("외부"), "foreign option setting is preserved and conflict reported");
                fixture = new OptionFixture(); runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, On()); runner.Step(); fixture.Handler.Definition.Version++; fixture.Drive(runner);
                Check(runner.Current.phase == BuildToolRunPhase.NeedsAttention && fixture.Backend.BuildCalls == 0 && fixture.Values["config"] == "off", "handler version change blocks execution but restores registered settings");
                fixture = new OptionFixture(); runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, On()); runner.Step(); runner.Cancel();
                Check(runner.Current.phase == BuildToolRunPhase.Canceled && fixture.Values["config"] == "off" && fixture.Backend.BuildCalls == 0, "cancel restores compilation preparation");
                fixture = new OptionFixture(); runner = fixture.Create();
                fixture.Backend.BeforeBuild = request => { File.Delete(request.logPath); Directory.CreateDirectory(request.logPath); };
                runner.Start(fixture.Backend.Profile.Guid, false, On()); fixture.Drive(runner);
                Check(runner.IsLocked && fixture.Values["config"] == "off" && fixture.Values["plugin"] == "original" && fixture.Backend.CleanupCalls == 1, "logging failure cannot bypass option or backend cleanup");

                fixture = new OptionFixture(); fixture.Values["symbols"] = "PRODUCT_LEVEL_EDITOR;UNRELATED;SHARED_SDK";
                fixture.Handler.Definition.Symbols = new[] { "SHARED_SDK" };
                second = new TestBuildOption("second"); second.Definition.Symbols = new[] { "SHARED_SDK" };
                fixture.Registry = new BuildToolOptions(new[] { fixture.Handler, second }, fixture.Settings);
                runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, new Dictionary<string, bool> { ["feature"] = false, ["second"] = true }); runner.Step();
                Check(fixture.Values["symbols"].Contains("SHARED_SDK") && fixture.Values["symbols"].Contains("PRODUCT_LEVEL_EDITOR") && fixture.Values["symbols"].Contains("UNRELATED"), "one shared symbol owner preserves dependency and product symbols");
                fixture.Drive(runner);
                Check(fixture.Values["symbols"] == "PRODUCT_LEVEL_EDITOR;UNRELATED;SHARED_SDK", "shared symbols restore exact original value");
                runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, new Dictionary<string, bool>()); runner.Step();
                Check(!fixture.Values["symbols"].Contains("SHARED_SDK"), "shared symbol removed only when every owner is OFF"); fixture.Drive(runner);
                fixture.Settings.Single(setting => setting.Key == BuildToolOptionSetting.SharedSymbols).ProtectedSymbols = new[] { "SHARED_SDK" };
                runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, new Dictionary<string, bool>()); runner.Step();
                Check(fixture.Values["symbols"].Contains("SHARED_SDK"), "external shared dependency is protected even when options are OFF"); fixture.Drive(runner);
                fixture = new OptionFixture(); runner = fixture.Create(); runner.Start(fixture.Backend.Profile.Guid, false, On()); runner.Cancel();
                Check(fixture.Events.Count == 0 && fixture.Backend.BuildCalls == 0, "unstarted option never receives a cleanup callback");

                fixture = new OptionFixture(); var window = ScriptableObject.CreateInstance<MatchBuildToolWindow>();
                try
                {
                    window.SelectionPath = Path.Combine(fixture.Root, "preferences.json"); window.OptionRegistry = fixture.Registry;
                    window.Show(); window.CreateGUI(); window.SelectProfile(fixture.Backend.Profile.Guid);
                    var toggle = window.rootVisualElement.Q<Toggle>("option-feature");
                    Check(toggle != null && toggle.tooltip.Contains("ON") && !toggle.value, "option UI shows label default and ON OFF help");
                    toggle.value = true;
                    Check(BuildToolPreferences.Choices(window.SelectionPath, fixture.Backend.Profile.Guid)["feature"], "option checkbox persists by GUID");
                    window.rootVisualElement.Q<Toggle>("option-feature").value = false;
                    Check(!BuildToolPreferences.Choices(window.SelectionPath, fixture.Backend.Profile.Guid)["feature"], "existing saved option changes from ON to OFF");
                    window.rootVisualElement.Q<Toggle>("option-feature").value = true;
                    string other = AssetDatabase.AssetPathToGUID(ProductProfiles.Definitions[0].ProfilePath); window.SelectProfile(other);
                    Check(!BuildToolPreferences.Choices(window.SelectionPath, other).ContainsKey("feature"), "product option choices remain isolated");
                    window.SelectProfile(fixture.Backend.Profile.Guid);
                    Check(window.rootVisualElement.Q<Toggle>("option-feature").value, "selection changes preserve stored choices");
                    BuildToolPreferences.SaveChoice(window.SelectionPath, fixture.Backend.Profile.Guid, "removed", true); window.RunPreflight();
                    Check(window.rootVisualElement.Query<HelpBox>().ToList().Any(box => box.text.Contains("적용하지 않는")), "obsolete choices explained without execution");
                }
                finally { UnityEngine.Object.DestroyImmediate(window); }
                snapshot.AssertUnchanged();
                Debug.Log("PASS BuildToolOptionsVerification"); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        internal static Dictionary<string, bool> On() => new Dictionary<string, bool> { ["feature"] = true };
        internal static void Check(bool value, string message) => BuildToolExecutionVerification.Check(value, message);
        internal static void Reject(Action action, string message) => BuildToolExecutionVerification.Reject(action, message);
    }

    internal sealed class TestBuildOption : IBuildToolOptionHandler
    {
        public BuildToolOptionDefinition Definition { get; }
        internal Action<BuildToolOptionStage, BuildToolOptionContext> Action;
        internal TestBuildOption(string id) { Definition = new BuildToolOptionDefinition { Id = id, Label = id, Tooltip = "ON: 사용 / OFF: 제외" }; }
        public bool Handles(BuildToolOptionStage stage) => true;
        public void Execute(BuildToolOptionStage stage, BuildToolOptionContext context) => Action?.Invoke(stage, context);
    }

    internal sealed class OptionFixture
    {
        internal readonly MockBuildToolBackend Backend = new MockBuildToolBackend();
        internal readonly Dictionary<string, string> Values = new Dictionary<string, string> { ["config"] = "off", ["plugin"] = "original", ["symbols"] = "PRODUCT_LEVEL_EDITOR" };
        internal readonly List<string> Events = new List<string>(), Writes = new List<string>();
        internal readonly string Root;
        internal string Output => Path.Combine(Root, "mock-output.txt");
        internal bool FailPrepare, FailPost, FailRestore, PausePrepare;
        internal readonly TestBuildOption Handler;
        internal readonly BuildToolOptionSetting[] Settings;
        internal BuildToolOptions Registry;
        internal OptionFixture(string root = null)
        {
            Root = root ?? BuildToolExecutionVerification.NewRoot();
            Backend.CurrentFingerprint = () => Backend.Version + "|" + Values["config"];
            Settings = new[] { Setting("config"), Setting("plugin"), Setting(BuildToolOptionSetting.SharedSymbols) };
            Handler = new TestBuildOption("feature"); Handler.Definition.Settings = new[] { "config", "plugin" };
            Handler.Action = (stage, context) =>
            {
                Events.Add(stage.ToString());
                if (stage == BuildToolOptionStage.PrepareCompilation)
                {
                    context.Apply("config", context.Enabled ? "on" : "off");
                    if (FailPrepare) throw new Exception("partial prepare failure");
                    if (PausePrepare) Backend.Busy = true;
                }
                if (stage == BuildToolOptionStage.BeforeBuild) context.Apply("plugin", context.Enabled ? "on" : "off");
                if (stage == BuildToolOptionStage.AfterBuild)
                {
                    File.WriteAllText(Output, "mock output"); context.Apply("plugin", "post");
                    if (FailPost) throw new Exception("post failure");
                }
            };
            Registry = new BuildToolOptions(new[] { Handler }, Settings);
        }
        private BuildToolOptionSetting Setting(string key)
        {
            string valueKey = key == BuildToolOptionSetting.SharedSymbols ? "symbols" : key;
            return new BuildToolOptionSetting { Key = key, Read = request => Values[valueKey],
                Write = (request, value) => { if (FailRestore && valueKey == "config" && value == "off") throw new IOException("restore failure"); Values[valueKey] = value; Writes.Add(valueKey + "=" + value); },
                PredictFingerprint = (request, value) => Backend.Version + "|" + (valueKey == "config" ? value : Values["config"]) };
        }
        internal BuildToolRunner Create() => new BuildToolRunner(Backend, new BuildToolRunStore(Path.Combine(Root, Guid.NewGuid().ToString("N"))), "options-session", Registry);
        internal void Drive(BuildToolRunner runner)
        { for (int i = 0; i < 20 && !runner.Current.IsTerminal && runner.Current.phase != BuildToolRunPhase.NeedsAttention; i++) runner.Step(); }
    }
}
