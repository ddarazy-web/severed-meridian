using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using LevelTool;
using UnityEditor;

namespace LevelAuthoring.Editor
{
    public static class LevelScopedDiscardVerification
    {
        public static void Run()
        {
            string report = Path.GetFullPath("Logs/LevelBrowser/scoped-discard-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".txt");
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            try
            {
                ContentSnapshot original = LevelToolTestProject.Create();
                ContentDocument a = original.Documents.Single(doc => doc.Kind == "level");
                ContentDocument project = original.Project;
                var bData = a.Data;
                bData["levelNumber"] = 2;
                ContentDocument b = new ContentDocument("level", "level-test-b", bData);
                ContentSnapshot snapshot = new ContentSnapshot(original.Documents.Concat(new[] { b }));
                StoredContentSnapshot stored = (StoredContentSnapshot)Activator.CreateInstance(typeof(StoredContentSnapshot),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { snapshot, "test-revision", "members" }, null);
                var session = new AuthoringEditSession(stored, a.Id);
                string baseline = (string)session.Get(a.Id).Data["displayName"];
                session.EditLevel("A", data => data["displayName"] = "A modified");
                session.ApplyToDocuments("B", new[] { b.Id }, docs => docs[b.Id].Data["displayName"] = "B modified");
                session.ApplyToDocuments("Shared", new[] { project.Id }, docs => docs[project.Id].Data["name"] = "Shared modified");
                MethodInfo discard = typeof(AuthoringEditSession).GetMethod("DiscardLevelChanges");
                if (discard == null) throw new Exception("Missing DiscardLevelChanges: project-wide Discard cannot preserve other work.");
                discard.Invoke(session, new object[] { a.Id });
                Check((string)session.Get(a.Id).Data["displayName"] == baseline, "A restored");
                Check((string)session.Get(b.Id).Data["displayName"] == "B modified", "B preserved");
                Check((string)session.Get(project.Id).Data["name"] == "Shared modified", "shared preserved");
                Check(session.IsDirty && session.CanUndo, "remaining dirty and undo preserved");
                session = AuthoringEditSession.Restore(session.ExportState());
                int undoCount = 0;
                while (session.CanUndo)
                {
                    session.Undo(); undoCount++;
                    Check((string)session.Get(a.Id).Data["displayName"] == baseline, "undo cannot revive A");
                }
                Check(undoCount == 2, "only meaningful B/shared undo remains");
                while (session.CanRedo)
                {
                    session.Redo();
                    Check((string)session.Get(a.Id).Data["displayName"] == baseline, "redo cannot revive A");
                }
                Check((string)session.Get(b.Id).Data["displayName"] == "B modified", "B redo restored");
                Check((string)session.Get(project.Id).Data["name"] == "Shared modified", "shared redo restored");
                session = new AuthoringEditSession(stored, a.Id);
                session.ApplyToDocuments("mixed", new[] { a.Id, b.Id, project.Id }, docs =>
                {
                    docs[a.Id].Data["displayName"] = "mixed A";
                    docs[b.Id].Data["displayName"] = "mixed B";
                    docs[project.Id].Data["name"] = "mixed shared";
                });
                session.ApplyToDocuments("future B", new[] { b.Id }, docs => docs[b.Id].Data["displayName"] = "future B");
                session.Undo();
                discard.Invoke(session, new object[] { a.Id });
                Check(session.CanRedo, "other redo remains");
                session.Undo();
                Check((string)session.Get(b.Id).Data["displayName"] == (string)b.Data["displayName"], "mixed B undo");
                Check((string)session.Get(project.Id).Data["name"] == (string)project.Data["name"], "mixed shared atomic undo");
                session.Redo();
                Check((string)session.Get(b.Id).Data["displayName"] == "mixed B" &&
                    (string)session.Get(project.Id).Data["name"] == "mixed shared", "mixed non-targets atomic redo");
                session.Redo();
                Check((string)session.Get(b.Id).Data["displayName"] == "future B", "preexisting redo retained");
                Check((string)session.Get(a.Id).Data["displayName"] == baseline, "mixed A never revived");
                string created = DocumentEditing.DuplicateLevel(session, a.Id, 3, "new");
                discard.Invoke(session, new object[] { created });
                Check(!session.DocumentIds.Contains(created), "unsaved level removed");
                session = AuthoringEditSession.Restore(session.ExportState());
                while (session.CanUndo) { session.Undo(); Check(!session.DocumentIds.Contains(created), "new level absent in undo"); }
                while (session.CanRedo) { session.Redo(); Check(!session.DocumentIds.Contains(created), "new level absent in redo"); }
                session = new AuthoringEditSession(stored, a.Id);
                string flow = LevelAuthoring.Runtime.TutorialDraftEditing.CreateFlow(session, "공유 구성");
                ContentSnapshot linked = session.CreateSnapshot();
                stored = (StoredContentSnapshot)Activator.CreateInstance(typeof(StoredContentSnapshot),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { linked, "linked", "members" }, null);
                session = new AuthoringEditSession(stored, a.Id);
                session.EditLevel("detach", data => data["tutorial"]["flowId"] = null);
                session.ApplyToDocuments("remove shared", new[] { flow }, docs => docs.Remove(flow));
                string beforeConflict = session.ExportState();
                bool rejected = false;
                try { discard.Invoke(session, new object[] { a.Id }); }
                catch (TargetInvocationException error) when (error.InnerException is ContentFormatException) { rejected = true; }
                Check(rejected && session.ExportState() == beforeConflict, "missing shared reference rejects discard atomically");
                session = new AuthoringEditSession((StoredContentSnapshot)Activator.CreateInstance(typeof(StoredContentSnapshot),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { snapshot, "original", "members" }, null), a.Id);
                flow = LevelAuthoring.Runtime.TutorialDraftEditing.CreateFlow(session, "저장된 공유 구성");
                string fixtureRoot = Path.GetFullPath("Logs/LevelBrowser");
                string ownedStore = Path.Combine(fixtureRoot, "store-" + Guid.NewGuid().ToString("N"));
                try { session.SaveAs(new ContentSnapshotStore(ownedStore)); }
                finally
                {
                    if (!Path.GetFullPath(ownedStore).StartsWith(fixtureRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Unsafe test cleanup path");
                    if (Directory.Exists(ownedStore)) Directory.Delete(ownedStore, true);
                }
                session.EditLevel("after save", data => data["displayName"] = "discard me");
                discard.Invoke(session, new object[] { a.Id });
                session = AuthoringEditSession.Restore(session.ExportState());
                while (session.CanUndo)
                {
                    session.Undo(); session.CreateSnapshot();
                    Check((string)session.Get(a.Id).Data["displayName"] != "discard me", "discarded post-save edit absent in undo");
                }
                Check(!session.DocumentIds.Contains(flow), "pre-save shared creation can still undo atomically");
                while (session.CanRedo) { session.Redo(); session.CreateSnapshot(); }
                File.WriteAllText(report, "PASS scoped discard, independent/mixed undo/redo, preexisting redo, new level, recovery, shared reference conflict");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                File.WriteAllText(report, "FAIL " + error);
                UnityEngine.Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
