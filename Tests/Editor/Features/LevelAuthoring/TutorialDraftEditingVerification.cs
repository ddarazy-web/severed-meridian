using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Editing;
using Levels;
using Tutorial;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class TutorialDraftEditingVerification
    {
        public static void Run()
        {
            try
            {
                Type editing = typeof(LevelDefinition).Assembly.GetType("LevelAuthoring.Runtime.TutorialDraftEditing");
                Check(editing != null, "JSON tutorial transaction API exists");
                var workspace = new AuthoringToolWorkspace();
                workspace.Open(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt"), "Cancel");
                var session = workspace.Session;
                session.SelectLevel(session.Documents.First(doc => doc.Kind == "level" && (int)doc.Data["levelNumber"] == 2).Id);
                string id = session.SelectedLevelId, before = session.Get(id).Data.ToString();
                object Call(string name, params object[] args) => editing.GetMethod(name).Invoke(null, args);
                Action<LevelDefinition> change = level =>
                {
                    level.Tutorial.flow = null; level.Tutorial.bindings.Clear(); level.Tutorial.steps.Clear();
                    level.Tutorial.completionId = "keep-completion";
                    level.Tutorial.steps.Add(new TutorialStepDefinition { authoringId = "stable-step", instructions = "안내", conditions =
                        new System.Collections.Generic.List<TutorialConditionDefinition> { new TutorialConditionDefinition { authoringId = "stable-condition", kind = TutorialConditionKind.SuccessfulSwap } } });
                };
                Call("EditLevel", session, "튜토리얼 초안", change);
                var edited = session.Get(id).Data;
                Check((string)edited["tutorial"]["steps"][0]["authoringId"] == "stable-step", "typed edit preserves authored ID");
                foreach (var field in Newtonsoft.Json.Linq.JObject.Parse(before).Properties().Where(field => field.Name != "tutorial"))
                    Check(Newtonsoft.Json.Linq.JToken.DeepEquals(field.Value, edited[field.Name]), "unrelated level field preserved: " + field.Name);
                string after = edited.ToString();
                try { Call("EditLevel", session, "실패", (Action<LevelDefinition>)(level => { level.Tutorial.steps.Clear(); throw new InvalidOperationException("expected"); })); }
                catch (TargetInvocationException) { }
                Check(session.Get(id).Data.ToString() == after, "failed typed edit is atomic");
                session.Undo(); Check(session.Get(id).Data.ToString() == before, "one undo restores original JSON");
                session.Redo();
                string flowId = (string)Call("CreateFlow", session, "공유 구성");
                Check((string)session.Get(id).Data["tutorial"]["flowId"] == flowId, "flow creation and connection share transaction");
                Call("EditFlow", session, flowId, "공유 설명", (Action<TutorialFlowDefinition>)(flow => flow.steps[0].instructions = "공유 원본 수정"));
                Call("Detach", session);
                Check(session.Get(id).Data["tutorial"]["flowId"].Type == Newtonsoft.Json.Linq.JTokenType.Null &&
                    (string)session.Get(id).Data["tutorial"]["steps"][0]["instructions"] == "공유 원본 수정" &&
                    (string)session.Get(id).Data["tutorial"]["completionId"] == "keep-completion", "detach expands shared content and preserves completion identity");
                string sample = (string)Call("CreateSample", session, "내 샘플", "설명", -1);
                Call("ApplySample", session, sample);
                var steps = session.Get(id).Data["tutorial"]["steps"];
                Check(steps.Count() == 2 && (string)steps[0]["authoringId"] != (string)steps[1]["authoringId"], "saved sample applies as independent steps");
                session.Undo(); Check(session.Get(id).Data["tutorial"]["steps"].Count() == 1, "sample application is one undo");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}
