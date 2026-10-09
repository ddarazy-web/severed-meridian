using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using Tutorial;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class SharedTutorialDraftVerification
    {
        public static void Run()
        {
            try
            {
                Type type = typeof(TutorialDraftEditing).Assembly.GetType("LevelAuthoring.Runtime.SharedTutorialDraft");
                Check(type != null, "isolated shared tutorial draft exists");
                var workspace = new AuthoringToolWorkspace(); workspace.Open(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt"), "Cancel");
                var parent = workspace.Session;
                parent.SelectLevel(parent.Documents.First(doc => doc.Kind == "level" && (int)doc.Data["levelNumber"] == 2).Id);
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
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}
