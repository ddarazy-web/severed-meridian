using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Products.Editor
{
    public static class BuildToolOptionsReloadVerification
    {
        private const string JobKey = "Match.BuildTool.OptionsReloadVerification";
        private static double deadline;
        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (SessionState.GetString(JobKey, "").Length == 0) return;
            deadline = EditorApplication.timeSinceStartup + 50; EditorApplication.update += AfterReload;
        }

        public static void Run()
        {
            try
            {
                var snapshot = new BuildToolStateSnapshot();
                var fixture = new OptionFixture { PausePrepare = true }; var runner = fixture.Create();
                runner.Start(fixture.Backend.Profile.Guid, false, BuildToolOptionsVerification.On()); runner.Step();
                var restarted = new BuildToolRunner(fixture.Backend, runner.Store, "other-session", fixture.Registry); restarted.Step();
                BuildToolOptionsVerification.Check(restarted.Current.phase == BuildToolRunPhase.NeedsAttention && fixture.Backend.BuildCalls == 0,
                    "restart never resumes an option-mutated request");
                fixture.Backend.Busy = false; restarted.ResolveInterrupted();
                BuildToolOptionsVerification.Check(fixture.Values["config"] == "off" && fixture.Backend.BuildCalls == 0, "explicit restart cleanup restores options without building");
                snapshot.AssertUnchanged();

                fixture = new OptionFixture { PausePrepare = true }; runner = fixture.Create();
                runner.Start(fixture.Backend.Profile.Guid, false, BuildToolOptionsVerification.On()); runner.Step();
                runner.Current.options[0].completed.Remove(BuildToolOptionStage.PrepareCompilation);
                runner.Current.optionMutations[0].applied = false; runner.Store.Save(runner.Current);
                var job = new JObject { ["stage"] = 0, ["root"] = fixture.Root, ["store"] = runner.Store.Root,
                    ["values"] = JObject.FromObject(fixture.Values), ["files"] = JObject.FromObject(BuildToolStateSnapshot.CaptureFiles()),
                    ["routing"] = BuildToolWindowVerification.CaptureRouting(), ["session"] = BuildToolController.Session };
                SessionState.SetString(JobKey, job.ToString()); EditorUtility.RequestScriptReload();
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private static void AfterReload()
        {
            try
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                { if (EditorApplication.timeSinceStartup < deadline) return; throw new TimeoutException("options reload did not settle"); }
                EditorApplication.update -= AfterReload;
                var job = JObject.Parse(SessionState.GetString(JobKey, ""));
                var fixture = new OptionFixture((string)job["root"]);
                foreach (var pair in job["values"].ToObject<Dictionary<string, string>>()) fixture.Values[pair.Key] = pair.Value;
                fixture.Backend.SelectedValue = 1;
                var runner = new BuildToolRunner(fixture.Backend, new BuildToolRunStore((string)job["store"]), "options-session", fixture.Registry);
                if ((int)job["stage"] == 0)
                {
                    runner.Step();
                    BuildToolOptionsVerification.Check(fixture.Writes.Count == 0 && fixture.Values["config"] == "on", "reload reconciles applied setting without a second write");
                    for (int i = 0; i < 8 && runner.Current.phase != BuildToolRunPhase.Postprocessing; i++) runner.Step();
                    BuildToolOptionsVerification.Check(runner.Current.phase == BuildToolRunPhase.Postprocessing && fixture.Backend.BuildCalls == 1 && runner.Current.result.outcome == BuildToolRunPhase.Succeeded,
                        "receipt persists before postprocessing reload");
                    job["stage"] = 1; job["values"] = JObject.FromObject(fixture.Values);
                    SessionState.SetString(JobKey, job.ToString()); EditorUtility.RequestScriptReload(); return;
                }
                fixture.Backend.BuildCalls = 1; fixture.Drive(runner);
                BuildToolOptionsVerification.Check(runner.Current.phase == BuildToolRunPhase.Succeeded && fixture.Backend.BuildCalls == 1 && fixture.Values["config"] == "off" && fixture.Values["plugin"] == "original",
                    "postprocessing reload preserves receipt and restores settings without another build");
                BuildToolOptionsVerification.Check(File.Exists(fixture.Output), "postprocessed mock output remains after cleanup");
                BuildToolOptionsVerification.Check((string)job["session"] == BuildToolController.Session && (string)job["routing"] == BuildToolWindowVerification.CaptureRouting(), "option reload preserves controller session and real routing");
                var expected = job["files"].ToObject<Dictionary<string, string>>(); var actual = BuildToolStateSnapshot.CaptureFiles();
                BuildToolOptionsVerification.Check(expected.Count == actual.Count && expected.All(pair => actual.TryGetValue(pair.Key, out string hash) && hash == pair.Value), "option reload preserves real project files");
                SessionState.EraseString(JobKey); Debug.Log("PASS BuildToolOptionsReloadVerification (two actual script reloads)"); EditorApplication.Exit(0);
            }
            catch (Exception error)
            { EditorApplication.update -= AfterReload; SessionState.EraseString(JobKey); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
