using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Products.Editor
{
    public static class BuildToolExecutionReloadVerification
    {
        private const string ReloadKey = "Match.BuildTool.ExecutionReloadVerification";
        private static double deadline;

        [InitializeOnLoadMethod]
        private static void ResumeAfterReload()
        {
            if (SessionState.GetString(ReloadKey, "").Length == 0) return;
            deadline = EditorApplication.timeSinceStartup + 40;
            EditorApplication.update += FinishAfterReload;
        }

        public static void Run()
        {
            try
            {
                var state = new BuildToolStateSnapshot();
                var backend = new MockBuildToolBackend();
                var first = BuildToolExecutionVerification.Create(backend); first.Start(backend.Profile.Guid);
                first.Step();
                var resumed = new BuildToolRunner(backend, first.Store, "session");
                resumed.Step(); resumed.Step(); resumed.Step();
                BuildToolExecutionVerification.Check(backend.BuildCalls == 1 && backend.SelectCalls == 1 && resumed.Current.phase == BuildToolRunPhase.Succeeded,
                    "same-session reconstruction resumes without selecting or building twice");
                backend = new MockBuildToolBackend(); first = BuildToolExecutionVerification.Create(backend); first.Start(backend.Profile.Guid);
                var restarted = new BuildToolRunner(backend, first.Store, "different-session"); restarted.Step();
                BuildToolExecutionVerification.Check(restarted.Current.phase == BuildToolRunPhase.NeedsAttention && backend.SelectCalls == 0 && backend.BuildCalls == 0,
                    "editor restart cannot automatically resume mutation");
                restarted.ResolveInterrupted(); BuildToolExecutionVerification.Check(backend.BuildCalls == 0 && restarted.Current.IsTerminal, "explicit restart recovery only cleans");
                backend = new MockBuildToolBackend(); first = BuildToolExecutionVerification.Create(backend); first.Start(backend.Profile.Guid);
                first.Current.phase = BuildToolRunPhase.Selecting; first.Current.selectionStarted = true; first.Store.Save(first.Current);
                resumed = new BuildToolRunner(backend, first.Store, "session"); resumed.Step();
                BuildToolExecutionVerification.Check(resumed.Current.phase == BuildToolRunPhase.WaitingForEditor && backend.SelectCalls == 0, "interrupted selection reconciles instead of reapplying");
                backend = new MockBuildToolBackend(); first = BuildToolExecutionVerification.Create(backend); first.Start(backend.Profile.Guid);
                first.Current.phase = BuildToolRunPhase.Building; first.Current.callStarted = true; first.Store.Save(first.Current);
                resumed = new BuildToolRunner(backend, first.Store, "session"); resumed.Step(); resumed.Step();
                BuildToolExecutionVerification.Check(resumed.Current.phase == BuildToolRunPhase.NeedsAttention && backend.BuildCalls == 0 && resumed.Current.result == null,
                    "started invocation without receipt is neither retried nor reported successful");
                backend = new MockBuildToolBackend(); first = BuildToolExecutionVerification.Create(backend); first.Start(backend.Profile.Guid);
                first.Current.phase = BuildToolRunPhase.Finalizing; first.Current.result = new BuildToolResult { outcome = BuildToolRunPhase.Succeeded };
                first.Current.pendingOutcome = BuildToolRunPhase.Succeeded; first.Store.Save(first.Current);
                resumed = new BuildToolRunner(backend, first.Store, "session"); resumed.Step();
                BuildToolExecutionVerification.Check(resumed.Current.phase == BuildToolRunPhase.Succeeded && backend.BuildCalls == 0 && backend.CleanupCalls == 1, "persisted receipt resumes finalization only");
                backend = new MockBuildToolBackend(); first = BuildToolExecutionVerification.Create(backend); first.Start(backend.Profile.Guid);
                var realMode = new MockBuildToolBackend { RealMode = true };
                resumed = new BuildToolRunner(realMode, first.Store, "session"); resumed.Step();
                BuildToolExecutionVerification.Check(resumed.Current.phase == BuildToolRunPhase.NeedsAttention && realMode.BuildCalls == 0, "mock journal cannot become real execution");
                state.AssertUnchanged();
                backend = new MockBuildToolBackend(); first = BuildToolExecutionVerification.Create(backend); first.Start(backend.Profile.Guid); first.Step();
                SessionState.SetString(ReloadKey, new JObject { ["root"] = first.Store.Root, ["stage"] = 0,
                    ["files"] = JObject.FromObject(BuildToolStateSnapshot.CaptureFiles()), ["routing"] = BuildToolWindowVerification.CaptureRouting(),
                    ["session"] = BuildToolController.Session }.ToString());
                EditorUtility.RequestScriptReload();
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static void FinishAfterReload()
        {
            JObject job = JObject.Parse(SessionState.GetString(ReloadKey, ""));
            try
            {
                if (EditorApplication.isUpdating || EditorApplication.isCompiling)
                {
                    if (EditorApplication.timeSinceStartup < deadline) return;
                    throw new TimeoutException("reload did not settle");
                }
                EditorApplication.update -= FinishAfterReload;
                var backend = new MockBuildToolBackend { SelectCalls = 1, SelectedValue = 1 };
                var store = new BuildToolRunStore((string)job["root"]);
                var runner = new BuildToolRunner(backend, store, "session");
                if ((int)job["stage"] == 0)
                {
                    runner.Step(); runner.Step(); runner.Step();
                    BuildToolExecutionVerification.Check(runner.Current.phase == BuildToolRunPhase.Succeeded && backend.BuildCalls == 1 && backend.SelectCalls == 1,
                        "real script reload resumes pending mock request once");
                    backend = new MockBuildToolBackend(); runner = BuildToolExecutionVerification.Create(backend); runner.Start(backend.Profile.Guid);
                    runner.Current.phase = BuildToolRunPhase.Building; runner.Current.callStarted = true; runner.Store.Save(runner.Current);
                    job["root"] = runner.Store.Root; job["stage"] = 1;
                    SessionState.SetString(ReloadKey, job.ToString()); EditorUtility.RequestScriptReload(); return;
                }
                runner.Step(); runner.Step();
                BuildToolExecutionVerification.Check(runner.Current.phase == BuildToolRunPhase.NeedsAttention && backend.BuildCalls == 0,
                    "real script reload never reinvokes started build");
                BuildToolExecutionVerification.Check((string)job["session"] == BuildToolController.Session, "controller session identity survives reload");
                BuildToolExecutionVerification.Check((string)job["routing"] == BuildToolWindowVerification.CaptureRouting(), "actual reload with mock requests preserves routing");
                var expected = job["files"].ToObject<System.Collections.Generic.Dictionary<string, string>>(); var actual = BuildToolStateSnapshot.CaptureFiles();
                BuildToolExecutionVerification.Check(expected.Count == actual.Count && expected.All(pair => actual.TryGetValue(pair.Key, out string hash) && hash == pair.Value), "actual reload mock tests preserve project files");
                SessionState.EraseString(ReloadKey);
                Debug.Log("PASS BuildToolExecutionReloadVerification (including two actual script reloads)"); EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                EditorApplication.update -= FinishAfterReload; SessionState.EraseString(ReloadKey); Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
    }
}
