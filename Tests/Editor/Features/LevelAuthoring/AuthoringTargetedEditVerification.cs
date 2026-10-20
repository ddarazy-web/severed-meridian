using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class AuthoringTargetedEditVerification
    {
        private static string report;
        private static MethodInfo targeted;
        public static void Run()
        {
            string folder = Path.GetFullPath("Logs/LevelToolTargetedEdit/check-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); report = Path.Combine(folder, "results.txt");
            try
            {
                targeted = typeof(AuthoringEditSession).GetMethod("ApplyToDocuments");
                Check(targeted != null, "targeted document transaction exists");
                var workspace = new AuthoringToolWorkspace();
                workspace.Create(Path.Combine(folder, "project"), LevelToolDocuments.NewProject());
                var session = workspace.Session;
                string level = session.SelectedLevelId;
                string project = session.Documents.Single(doc => doc.Kind == "project").Id;
                string originalName = (string)session.Get(level).Data["displayName"];
                long projectVersion = session.GetDocumentVersion(project), levelVersion = session.GetDocumentVersion(level);
                JObject retained = null;
                session.EditLevel("변경", data => { retained = data; data["displayName"] = "변경 이름"; });
                Check(Copies(session) == 2, "single edit clones only draft and published target");
                retained["displayName"] = "외부 오염";
                retained["board"]["cells"][0]["isActive"] = false;
                Check((string)session.Get(level).Data["displayName"] == "변경 이름" &&
                    (bool)session.Get(level).Data["board"]["cells"][0]["isActive"], "retained callback object cannot mutate published document");
                Check(session.IsDirty && session.GetDocumentVersion(project) == projectVersion, "unrelated document version preserved");
                session.Undo();
                Check(!session.IsDirty && session.GetDocumentVersion(level) == levelVersion, "Undo restores saved content and version");
                session.Redo();
                Check((string)session.Get(level).Data["displayName"] == "변경 이름", "Redo restores isolated publication");
                string state = session.ExportState();
                session.EditLevel("같은 값", data => data["displayName"] = "변경 이름");
                Check(state == session.ExportState(), "same value creates no history");
                session.EditLevel("저장 값으로", data => data["displayName"] = originalName);
                Check(!session.IsDirty, "equal saved content clears dirty without reference equality");
                session.Undo();
                session.EditLevel("새 분기", data => data["displayName"] = "새 분기");
                Check(!session.CanRedo, "new edit removes Redo branch");
                var fork = session.Fork();
                fork.EditLevel("사본", data => data["displayName"] = "사본만");
                Check((string)session.Get(level).Data["displayName"] == "새 분기", "Fork edits remain independent");
                state = session.ExportState();
                JObject beforeLevel = session.Get(level).Data, beforeProject = session.Get(project).Data;
                Reject(() => Apply(session, new[] { level, project }, docs =>
                { docs[level].Data["displayName"] = "부분"; docs[project].Data["name"] = "부분"; throw new InvalidOperationException("rollback"); }));
                Check(state == session.ExportState(), "multi-document exception preserves documents selection and history");
                Apply(session, new[] { level, project }, docs =>
                { docs[level].Data["displayName"] = "두 문서"; docs[project].Data["name"] = "두 문서"; });
                Check(Copies(session) == 4, "two targets clone two drafts and two publications");
                session.Undo();
                Check(JToken.DeepEquals(beforeLevel, session.Get(level).Data) && JToken.DeepEquals(beforeProject, session.Get(project).Data), "one Undo restores both documents");
                session.Redo();
                state = session.ExportState();
                Reject(() => Apply(session, new[] { level }, docs => docs[project].Data["name"] = "범위 밖"));
                Reject(() => Apply(session, new[] { level }, docs => docs.Remove(level)));
                Reject(() => Apply(session, new[] { level }, docs => docs[level] = new ContentDocument("level", "wrong-id", beforeLevel)));
                Reject(() => Apply(session, new[] { level }, docs => docs["outside"] = new ContentDocument("level", "outside", beforeLevel)));
                Check(state == session.ExportState(), "invalid targets preserve complete session");
                var added = new ContentDocument("level", "added", beforeLevel);
                Apply(session, new[] { "added" }, docs => docs.Add("added", added));
                added.Data["displayName"] = "외부 추가 오염";
                Check((string)session.Get("added").Data["displayName"] != "외부 추가 오염", "new document publication is isolated");
                Apply(session, new[] { "added" }, docs => docs.Remove("added"));
                Check(!session.DocumentIds.Contains("added"), "targeted removal works");
                session.Undo(); Check(session.DocumentIds.Contains("added"), "Undo restores removed document");
                session.Redo();
                workspace.Save();
                session.Undo(); Check(session.IsDirty, "Undo after save is dirty");
                session.Redo(); Check(!session.IsDirty, "Redo to saved state is clean");
                state = session.ExportState();
                Apply(session, new[] { project }, docs => docs[project].Data["documents"] = new JArray("ignored-generation-path"));
                Check(state == session.ExportState() && !session.IsDirty, "generation paths preserve existing dirty and no-op semantics");
                session.Apply("큰 입력 준비", docs =>
                {
                    for (int i = 0; i < 100; i++)
                    {
                        var data = (JObject)beforeLevel.DeepClone(); data["levelNumber"] = i + 2;
                        string id = "unrelated-" + i;
                        docs.Add(id, new ContentDocument("level", id, data));
                    }
                });
                var versions = session.DocumentIds.Where(id => id != level).ToDictionary(id => id, session.GetDocumentVersion);
                session.EditLevel("큰 입력 한 문서", data => data["displayName"] = "확장 검사");
                Check(Copies(session) == 2 && versions.All(pair => session.GetDocumentVersion(pair.Key) == pair.Value),
                    "102 documents still copy only two target objects and retain unrelated versions");
                Check(AuthoringEditSession.Restore(session.ExportState()).ExportState() == session.ExportState(), "history export restore preserves targeted edits");
            }
            catch (Exception error) { File.AppendAllText(report, error + "\n"); Debug.LogException(error); EditorApplication.Exit(1); return; }
            EditorApplication.Exit(0);
        }
        private static int Copies(AuthoringEditSession session) =>
            (int)typeof(AuthoringEditSession).GetField("lastEditCopyCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
        private static void Apply(AuthoringEditSession session, string[] ids, Action<IDictionary<string, ContentDocument>> action) =>
            targeted.Invoke(session, new object[] { "대상 편집", ids, action });
        private static void Reject(Action action)
        {
            bool rejected = false;
            try { action(); } catch { rejected = true; }
            Check(rejected, "invalid transaction rejected");
        }
        private static void Check(bool value, string message)
        {
            File.AppendAllText(report, (value ? "PASS " : "FAIL ") + message + "\n");
            if (!value) throw new Exception(message);
        }
    }
}
