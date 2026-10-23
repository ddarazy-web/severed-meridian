using System;
using System.IO;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelAuthoring.Documents;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class LevelTutorialWorkVerification
    {
        public static void Run()
        {
            int exit = 0;
            string folder = Path.GetFullPath("Logs/NativeLevelToolStage03/work-" + Guid.NewGuid().ToString("N"));
            try
            {
                var workspace = new AuthoringToolWorkspace(); workspace.Create(folder, LevelToolTestProject.Create());
                AuthoringEditSession session = workspace.Session;
                string id = session.SelectedLevelId;
                session.EditLevel("작업 전 변경", data => data["displayName"] = "작업 전 이름");
                JObject baseline = session.Get(id).Data;
                MethodInfo begin = typeof(AuthoringEditSession).GetMethod("BeginLevelWork");
                Check(begin != null, "level-scoped work checkpoint exists");
                object checkpoint = begin.Invoke(session, null);
                session.EditLevel("튜토리얼 작업", data =>
                {
                    data["tutorial"]["completionId"] = "cancel-me";
                    data["board"]["cells"][0]["isActive"] = false;
                    data["elementSupply"]["sources"] = new JArray();
                });
                session.EditLevel("작업 중 다른 설정", data => data["moveCount"] = 37);
                baseline["moveCount"] = 37;
                session.ApplyToDocuments("다른 레벨 추가", new[] { "other-level" }, docs =>
                {
                    JObject other = LevelToolDocuments.NewLevel(); other["levelNumber"] = 2;
                    docs["other-level"] = new ContentDocument("level", "other-level", other);
                });
                string projectId = session.DocumentIdsOfKind("project")[0];
                session.ApplyToDocuments("공유 정보 변경", new[] { projectId }, docs => docs[projectId].Data["name"] = "유지할 공유 변경");
                typeof(AuthoringEditSession).GetMethod("CancelLevelWork").Invoke(session, new[] { checkpoint });
                Check(JToken.DeepEquals(session.Get(id).Data, baseline), "cancel restores tutorial and board but preserves unrelated level settings");
                Check((string)session.Get(projectId).Data["name"] == "유지할 공유 변경" && session.IsDirty, "cancel preserves shared changes and pre-existing dirty level");
                Check((int)session.Get("other-level").Data["levelNumber"] == 2, "cancel preserves another level created during work");
                session.Undo();
                Check((string)session.Get(id).Data["tutorial"]["completionId"] != "cancel-me", "Undo does not revive cancelled tutorial");
                session.Redo();
                Check(JToken.DeepEquals(session.Get(id).Data, baseline), "Redo retains work-start level without cancelled changes");
                var completed = session.BeginLevelWork();
                LevelAuthoring.Runtime.TutorialDraftEditing.EditLevel(session, "새 튜토리얼", level =>
                {
                    level.Tutorial.steps.Clear();
                    level.Tutorial.steps.Add(new Tutorial.TutorialStepDefinition { instructions = "저장 후 다시 열기" });
                });
                session.CompleteLevelWork(completed);
                Check(session.IsDirty, "completing tutorial work does not save files");
                JObject completedData = session.Get(id).Data;
                workspace.Save(); workspace.Reload("Discard");
                Check(!workspace.Session.IsDirty && JToken.DeepEquals(workspace.Session.Get(id).Data, completedData), "explicit save and reopen retain completed tutorial and unrelated settings");
            }
            catch (Exception error) { Debug.LogException(error); exit = 1; }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
            EditorApplication.Exit(exit);
        }
        private static void Check(bool value, string message)
        { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}
