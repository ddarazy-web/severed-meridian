using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Products.Editor
{
    public static class BuildToolExecutionVerification
    {
        public static void Run()
        {
            try
            {
                var snapshot = new BuildToolStateSnapshot();
                var backend = new MockBuildToolBackend();
                var runner = Create(backend);
                runner.Start(backend.Profile.Guid);
                Check(runner.Store.Load().id == runner.Current.id && backend.SelectCalls == 0, "request persisted before mutation");
                Reject(() => runner.Start(backend.Profile.Guid), "double click cannot create second request");
                runner.Step(); Check(backend.SelectCalls == 1 && runner.Current.phase == BuildToolRunPhase.WaitingForEditor, "selection recorded once");
                backend.Busy = true; runner.Step(); Check(backend.BuildCalls == 0, "busy transition waits");
                backend.Busy = false; backend.Ready = false; runner.Step(); Check(runner.Current.phase == BuildToolRunPhase.WaitingForEditor, "target mismatch waits");
                backend.Ready = true; runner.Step(); Check(runner.Current.phase == BuildToolRunPhase.Ready, "readiness checked before invocation");
                runner.Step(); runner.Step();
                Check(runner.Current.phase == BuildToolRunPhase.Succeeded && backend.BuildCalls == 1 && backend.CleanupCalls == 1, "success and single invocation");
                Check(backend.SelectedValue == 1 && backend.TemporaryValue == 0 && runner.Current.finishedUtc != null, "selected product retained, temporary settings restored");
                Check(File.ReadAllText(runner.Current.logPath).Contains("모의") && runner.Current.result != null && runner.Current.ElapsedSeconds >= 0, "mock logs and timed result are explicit");
                foreach (var outcome in new[] { BuildToolRunPhase.Failed, BuildToolRunPhase.Canceled })
                {
                    backend = new MockBuildToolBackend { Outcome = outcome }; runner = Create(backend); Complete(runner, backend);
                    Check(runner.Current.phase == outcome && backend.CleanupCalls == 1 && backend.TemporaryValue == 0, "reported result and cleanup " + outcome);
                }
                backend = new MockBuildToolBackend { ThrowBuild = true }; runner = Create(backend); Complete(runner, backend);
                Check(runner.Current.phase == BuildToolRunPhase.NeedsAttention && runner.Current.error.Contains("build exception") && backend.CleanupCalls == 1, "build exception without receipt requires attention and cleans");
                backend = new MockBuildToolBackend { NullResult = true }; runner = Create(backend); Complete(runner, backend);
                Check(runner.Current.phase == BuildToolRunPhase.NeedsAttention && runner.IsLocked && backend.CleanupCalls == 1 && backend.TemporaryValue == 0, "missing result cleans but never claims completion");
                backend = new MockBuildToolBackend(); runner = Create(backend);
                backend.OnCapture = request => Directory.CreateDirectory(request.logPath);
                Reject(() => runner.Start(backend.Profile.Guid), "log failure after publication rejects start");
                Check(runner.IsLocked && runner.Current.phase == BuildToolRunPhase.NeedsAttention, "partially published request stays locked");
                var recovered = new BuildToolRunner(backend, runner.Store, "session"); recovered.Step();
                Check(recovered.IsLocked && backend.SelectCalls == 0 && backend.BuildCalls == 0, "partially published request never resumes execution");
                backend = new MockBuildToolBackend(); runner = Create(backend);
                backend.OnCapture = request => Directory.CreateDirectory(Path.ChangeExtension(request.logPath, ".json"));
                Reject(() => runner.Start(backend.Profile.Guid), "history failure after publication rejects start");
                recovered = new BuildToolRunner(backend, runner.Store, "session"); recovered.Step();
                Check(runner.IsLocked && recovered.IsLocked && backend.SelectCalls == 0 && backend.BuildCalls == 0, "failed history write persists nonexecuting attention state");
                backend = new MockBuildToolBackend(); runner = Create(backend);
                backend.BeforeBuild = request => { File.Delete(request.logPath); Directory.CreateDirectory(request.logPath); };
                Complete(runner, backend);
                Check(backend.CleanupCalls == 1 && backend.TemporaryValue == 0 && runner.IsLocked && runner.StoreError != null, "logging failure cannot bypass cleanup");
                backend = new MockBuildToolBackend { FailSelect = true }; runner = Create(backend);
                backend.BeforeSelect = request => { File.Delete(request.logPath); Directory.CreateDirectory(request.logPath); };
                runner.Start(backend.Profile.Guid); runner.Step();
                Check(backend.CleanupCalls == 1 && backend.SelectedValue == 0 && runner.IsLocked, "partial selection and logging failure still clean");
                backend = new MockBuildToolBackend { Outcome = BuildToolRunPhase.Preparing }; runner = Create(backend); Complete(runner, backend);
                Check(runner.Current.phase == BuildToolRunPhase.NeedsAttention && backend.CleanupCalls == 1, "invalid result cannot masquerade as a completed build");
                backend = new MockBuildToolBackend(); runner = Create(backend); runner.Start(backend.Profile.Guid); runner.Step();
                File.Delete(runner.Current.logPath); Directory.CreateDirectory(runner.Current.logPath); runner.Cancel();
                Check(backend.CleanupCalls == 1 && backend.SelectedValue == 0 && runner.IsLocked, "cancel still cleans when logging fails");
                backend = new MockBuildToolBackend { FailSelect = true }; runner = Create(backend); runner.Start(backend.Profile.Guid); runner.Step();
                Check(runner.Current.phase == BuildToolRunPhase.Failed && backend.SelectedValue == 0 && backend.BuildCalls == 0, "partial selection failure restores owned state");
                backend = new MockBuildToolBackend(); runner = Create(backend); runner.Start(backend.Profile.Guid); runner.Step(); backend.CompilationErrors = true; runner.Step();
                Check(runner.Current.phase == BuildToolRunPhase.Failed && backend.BuildCalls == 0, "compilation failure never builds");
                backend = new MockBuildToolBackend(); runner = Create(backend); runner.Start(backend.Profile.Guid); backend.Version = "changed"; runner.Step();
                Check(runner.Current.phase == BuildToolRunPhase.Failed && backend.SelectCalls == 0 && backend.BuildCalls == 0, "same GUID settings change rejected");
                backend = new MockBuildToolBackend(); runner = Create(backend); runner.Start(backend.Profile.Guid); backend.Exists = false; runner.Step();
                Check(runner.Current.phase == BuildToolRunPhase.Failed && backend.BuildCalls == 0, "deleted GUID rejected");
                backend = new MockBuildToolBackend(); runner = Create(backend); runner.Start(backend.Profile.Guid); runner.Step(); backend.Ready = false; runner.Cancel();
                Check(runner.Current.phase == BuildToolRunPhase.Canceled && backend.BuildCalls == 0 && backend.SelectedValue == 0, "waiting cancel cleans selection without invocation");
                backend = new MockBuildToolBackend { FailCleanup = true }; runner = Create(backend); Complete(runner, backend);
                Check(runner.Current.phase == BuildToolRunPhase.NeedsAttention && runner.Current.cleanupError != null && runner.IsLocked, "cleanup failure blocks new request");
                backend.FailCleanup = false; runner.ResolveInterrupted(); Check(runner.Current.IsTerminal && backend.BuildCalls == 1, "explicit cleanup never rebuilds");
                backend = new MockBuildToolBackend { Conflict = true }; runner = Create(backend); Complete(runner, backend);
                Check(runner.Current.phase == BuildToolRunPhase.NeedsAttention && backend.SelectedValue == 77, "external state conflict preserved");
                backend = new MockBuildToolBackend { RealMode = true }; runner = Create(backend);
                Reject(() => runner.Start(backend.Profile.Guid), "actual execution requires user confirmation");
                Check(backend.BuildCalls == 0 && backend.SelectCalls == 0, "confirmation refusal is side effect free");
                var real = new UnityProfileBuildBackend();
                var copy = BuildToolProfiles.Read(backend.Profile.Profile, "Assets/not-original.asset", "copy");
                Reject(() => real.Validate(copy), "copied profile cannot execute");
                var inMemory = UnityEngine.Object.Instantiate(backend.Profile.Profile);
                try
                {
                    var info = BuildToolProfiles.Read(inMemory, backend.Profile.Path, backend.Profile.Guid);
                    string before = real.Fingerprint(info);
                    inMemory.scriptingDefines = inMemory.scriptingDefines.Concat(new[] { "BUILD_TOOL_MEMORY_PROBE" }).ToArray();
                    Check(before != real.Fingerprint(info), "unsaved profile changes alter frozen fingerprint");
                }
                finally { UnityEngine.Object.DestroyImmediate(inMemory); }
                var restoreOwned = typeof(UnityProfileBuildBackend).GetMethod("RestoreOwned", BindingFlags.NonPublic | BindingFlags.Static);
                var beforeState = new JObject { ["mode"] = 0 }; var ownState = new JObject { ["mode"] = 1 };
                var foreignState = new JObject { ["mode"] = 77 }; var cleanupErrors = new List<string>(); int restores = 0;
                Action restore = () => restores++;
                restoreOwned.Invoke(null, new object[] { "mode", beforeState, beforeState, ownState, restore, cleanupErrors });
                Check(restores == 0 && cleanupErrors.Count == 0, "native cleanup skips already restored settings");
                restoreOwned.Invoke(null, new object[] { "mode", ownState, beforeState, ownState, restore, cleanupErrors });
                Check(restores == 1 && cleanupErrors.Count == 0, "native cleanup restores only owned expected value");
                restoreOwned.Invoke(null, new object[] { "mode", foreignState, beforeState, ownState, restore, cleanupErrors });
                Check(restores == 1 && cleanupErrors.Count == 1 && (int)foreignState["mode"] == 77, "native cleanup preserves foreign change and reports conflict");
                restoreOwned.Invoke(null, new object[] { "mode", ownState, beforeState, ownState, (Action)(() => throw new IOException("restore write failed")), cleanupErrors });
                Check(cleanupErrors.Count == 2 && cleanupErrors[1].Contains("restore write failed"), "native cleanup records restoration failure");
                var corruptRoot = NewRoot(); Directory.CreateDirectory(corruptRoot); File.WriteAllText(Path.Combine(corruptRoot, "current.json"), "broken-json");
                runner = new BuildToolRunner(new MockBuildToolBackend(), new BuildToolRunStore(corruptRoot), "session");
                Check(runner.IsLocked && runner.StoreError != null, "corrupt journal cannot automatically execute");
                string blocked = Path.Combine(NewRoot(), "not-directory"); Directory.CreateDirectory(Path.GetDirectoryName(blocked)); File.WriteAllText(blocked, "owned fixture");
                backend = new MockBuildToolBackend(); runner = new BuildToolRunner(backend, new BuildToolRunStore(blocked), "session");
                Reject(() => runner.Start(backend.Profile.Guid), "journal save failure blocks side effects");
                Check(backend.SelectCalls == 0 && backend.BuildCalls == 0, "failed persistence never starts selection");
                backend = new MockBuildToolBackend(); runner = Create(backend); runner.Start(backend.Profile.Guid);
                using (var held = new FileStream(runner.Store.CurrentPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var release = System.Threading.Tasks.Task.Run(() => { System.Threading.Thread.Sleep(35); held.Dispose(); });
                    runner.Store.Save(runner.Current); release.Wait();
                }
                Check(runner.Store.Load().id == runner.Current.id, "transient Windows journal sharing lock recovers without losing request");
                snapshot.AssertUnchanged();
                Debug.Log("PASS BuildToolExecutionVerification"); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        internal static string NewRoot() => Path.Combine("Logs/BuildToolStage02", Guid.NewGuid().ToString("N"));
        internal static BuildToolRunner Create(MockBuildToolBackend backend) => new BuildToolRunner(backend, new BuildToolRunStore(NewRoot()), "session");
        internal static void Complete(BuildToolRunner runner, MockBuildToolBackend backend)
        {
            runner.Start(backend.Profile.Guid);
            for (int i = 0; i < 6 && !runner.Current.IsTerminal && runner.Current.phase != BuildToolRunPhase.NeedsAttention; i++) runner.Step();
        }
        internal static void Check(bool value, string message)
        {
            if (!value) throw new Exception(message); Debug.Log("PASS " + message);
        }
        internal static void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); } catch (Exception) { rejected = true; }
            Check(rejected, message);
        }
    }

    internal sealed class MockBuildToolBackend : IBuildToolBackend
    {
        internal BuildToolProfileInfo Profile = BuildToolProfiles.Discover().First(item => item.Product?.Name == "Windows Level Editor" && !item.IsCopy);
        internal bool Busy, CompilationErrors, RealMode, FailSelect, ThrowBuild, FailCleanup, Conflict, NullResult;
        internal Action<BuildToolRequest> OnCapture, BeforeBuild, BeforeSelect;
        internal bool Ready = true, Exists = true;
        internal string Version = "unchanged";
        internal Func<string> CurrentFingerprint;
        internal int BuildCalls, SelectCalls, CleanupCalls, SelectedValue, TemporaryValue;
        internal BuildToolRunPhase Outcome = BuildToolRunPhase.Succeeded;
        public bool IsMock => !RealMode;
        public bool IsBusy => Busy;
        public bool HasCompilationErrors => CompilationErrors;
        public BuildToolProfileInfo Read(string guid) => Exists && guid == Profile.Guid ? Profile : throw new FileNotFoundException("profile missing");
        public void Validate(BuildToolProfileInfo profile)
        {
            if (profile?.Product == null || profile.IsCopy) throw new InvalidOperationException("invalid product profile");
        }
        public string Fingerprint(BuildToolProfileInfo profile) => CurrentFingerprint?.Invoke() ?? Version;
        public string CaptureSelection(BuildToolRequest request) { OnCapture?.Invoke(request); return "mock snapshot"; }
        public void Select(BuildToolRequest request) { SelectCalls++; SelectedValue = 1; BeforeSelect?.Invoke(request); if (FailSelect) throw new Exception("selection exception"); }
        public bool IsReady(BuildToolRequest request, out string reason) { reason = Ready ? null : "mock platform transition"; return Ready; }
        public BuildToolResult Build(BuildToolRequest request)
        {
            BuildCalls++; TemporaryValue = 1;
            BeforeBuild?.Invoke(request);
            if (ThrowBuild) throw new Exception("build exception");
            if (NullResult) return null;
            return new BuildToolResult { outcome = Outcome, error = Outcome == BuildToolRunPhase.Failed ? "reported failure" : null };
        }
        public void Cleanup(BuildToolRequest request)
        {
            CleanupCalls++;
            if (Conflict) { SelectedValue = 77; throw new Exception("external state conflict"); }
            if (FailCleanup) throw new Exception("cleanup exception");
            TemporaryValue = 0; SelectedValue = request.selectionEstablished ? 1 : 0;
        }
    }
}
