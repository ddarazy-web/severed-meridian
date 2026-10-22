using System;
using System.IO;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using Tutorial;
using LevelTool;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class SharedTutorialDraftVerification
    {
        public static void Run()
        {
            string owned = Path.GetFullPath("Logs/SharedTutorialDraft/" + Guid.NewGuid().ToString("N"));
            string data = Path.Combine(owned, "data"); Directory.CreateDirectory(owned);
            int exit = 1;
            try
            {
                Type type = typeof(TutorialDraftEditing).Assembly.GetType("LevelAuthoring.Runtime.SharedTutorialDraft");
                Check(type != null, "isolated shared tutorial draft exists");
                var workspace = new AuthoringToolWorkspace(); workspace.Create(data, LevelToolTestProject.Create());
                string levelId = workspace.Session.SelectedLevelId;
                workspace.OpenListing(data, "Cancel"); workspace.OpenLevel(levelId);
                var parent = workspace.Session;
                TutorialDraftEditing.EditLevel(parent, "prepare", level =>
                {
                    level.Tutorial.flow = null; level.Tutorial.bindings.Clear(); level.Tutorial.steps.Clear();
                    level.Tutorial.steps.Add(new TutorialStepDefinition { authoringId = "stable", instructions = "original" });
                });
                string flowId = TutorialDraftEditing.CreateFlow(parent, "shared");
                object draft = Activator.CreateInstance(type, parent);
                var session = (AuthoringEditSession)type.GetProperty("Session").GetValue(draft);
                Check(!session.CanUndo, "shared draft starts with its own empty undo history");
                TutorialDraftEditing.EditFlow(session, flowId, "draft text", flow => flow.steps[0].instructions = "changed");
                Check((string)parent.Get(flowId).Data["steps"][0]["instructions"] == "original", "draft edits do not affect source");
                session.Undo(); Check((string)session.Get(flowId).Data["steps"][0]["instructions"] == "original", "draft undo is isolated"); session.Redo();
                string recovery = (string)type.GetMethod("ExportState").Invoke(draft, null);
                draft = type.GetMethod("Restore").Invoke(null, new object[] { recovery });
                session = (AuthoringEditSession)type.GetProperty("Session").GetValue(draft);
                Check(session.CanUndo && (string)session.Get(flowId).Data["steps"][0]["instructions"] == "changed", "shared draft restores content and local history");
                type.GetMethod("Apply").Invoke(draft, new object[] { parent });
                Check((string)parent.Get(flowId).Data["steps"][0]["instructions"] == "changed" && (string)parent.Get(flowId).Data["steps"][0]["authoringId"] == "stable", "explicit apply changes shared source preserving IDs");
                parent.Undo(); Check((string)parent.Get(flowId).Data["steps"][0]["instructions"] == "original", "one parent undo restores source");
                TutorialDraftEditing.EditFlow(parent, flowId, "other editor", flow => flow.steps[0].instructions = "external");
                bool rejected = false;
                try { type.GetMethod("Apply").Invoke(draft, new object[] { parent }); } catch (TargetInvocationException error) { rejected = error.InnerException is InvalidOperationException; }
                Check(rejected && (string)parent.Get(flowId).Data["steps"][0]["instructions"] == "external", "conflicting source is preserved and apply rejected");
                File.WriteAllText(Path.Combine(owned, "result.txt"), "PASS shared draft v2 isolation, recovery, apply, Undo and conflict"); exit = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(owned, "result.txt"), "FAIL " + error); Debug.LogException(error); }
            finally { if (Directory.Exists(data)) Directory.Delete(data, true); }
            EditorApplication.Exit(exit);
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}
