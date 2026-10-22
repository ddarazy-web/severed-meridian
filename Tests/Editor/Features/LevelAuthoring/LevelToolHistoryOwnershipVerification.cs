using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class LevelToolHistoryOwnershipVerification
    {
        public static void RunContracts() => Run(false);
        public static void RunSharing() => Run(true);
        private static void Run(bool sharing)
        {
            string folder = Path.GetFullPath("Logs/LevelToolRecoveryPerformance/history-" + Guid.NewGuid().ToString("N"));
            try
            {
                var workspace = new AuthoringToolWorkspace(); workspace.Create(folder, LevelToolTestProject.Create());
                var repository = new ContentSnapshotStore(folder);
                StoredContentSnapshot input = repository.Read();
                var session = new AuthoringEditSession(input, workspace.Session.SelectedLevelId);
                string id = session.SelectedLevelId;
                session.SelectCells(new[] { 1, 2 });
                string initial = session.ExportState();
                input.Snapshot.Get(id).Data["displayName"] = "outside";
                input.Snapshot.Documents.Single(doc => doc.Id == id).Data["displayName"] = "outside";
                session.Get(id).Data["displayName"] = "outside";
                session.Documents.Single(doc => doc.Id == id).Data["displayName"] = "outside";
                session.SelectedCells[0] = 80;
                session.CreateSnapshot().Get(id).Data["displayName"] = "outside";
                Check(session.ExportState() == initial, "public inputs and getters isolate mutable documents and selection");
                IDictionary<string, ContentDocument> retained = null;
                session.Apply("first", docs => { retained = docs; docs[id].Data["displayName"] = "first"; });
                string first = session.ExportState();
                retained[id].Data["displayName"] = "late mutation"; retained.Clear();
                Check(session.ExportState() == first, "retained callback cannot mutate current or history");
                session.EditLevel("second", data => data["displayName"] = "second");
                string second = session.ExportState();
                bool failed = false;
                try { session.Apply("failure", docs => { docs[id].Data["displayName"] = "partial"; throw new InvalidOperationException(); }); }
                catch (InvalidOperationException) { failed = true; }
                Check(failed && session.ExportState() == second, "callback failure preserves full state");
                failed = false;
                try { session.Apply("invalid remove", docs => docs.Remove(id)); } catch (ContentFormatException) { failed = true; }
                Check(failed && session.ExportState() == second, "selected document removal is atomic");
                session.Apply("no change", docs => retained = docs);
                retained[id].Data["displayName"] = "later";
                Check(session.ExportState() == second, "no-op retains history and isolates callback");
                var fork = session.Fork(); fork.EditLevel("fork", data => data["displayName"] = "fork");
                Check(session.ExportState() == second && fork.CanUndo, "fork mutations are isolated");
                fork.Undo(); Check((string)fork.Get(id).Data["displayName"] == "second", "fork Undo retains baseline");
                var multi = session.Fork();
                string projectId = multi.Documents.First(doc => doc.Kind == "project").Id;
                multi.Apply("multiple documents", docs => { docs[id].Data["displayName"] = "multi"; docs[projectId].Data["temporaryDraft"] = true; });
                string multiState = multi.ExportState(); multi.Undo();
                Check((string)multi.Get(id).Data["displayName"] == "second" && multi.Get(projectId).Data["temporaryDraft"] == null,
                    "one Undo restores both changed documents");
                multi.Redo(); Check(multi.ExportState() == multiState, "multi-document Redo preserves full draft state");
                if (sharing) AssertShared(session);
                session.Undo(); Check((string)session.Get(id).Data["displayName"] == "first", "Undo restores prior document");
                session.Redo(); Check(session.ExportState() == second, "Redo restores complete history and selection");
                session.Undo(); session.EditLevel("branch", data => data["displayName"] = "branch");
                Check(!session.CanRedo, "new edit after Undo clears Redo");
                session.Apply("add draft", docs => docs.Add("draft-extra", new ContentDocument("tutorialFlow", "draft-extra", new JObject())));
                session.Apply("remove draft", docs => docs.Remove("draft-extra"));
                session.Undo(); Check(session.Documents.Any(doc => doc.Id == "draft-extra"), "Undo deletion restores incomplete draft");
                session.Redo(); Check(!session.Documents.Any(doc => doc.Id == "draft-extra"), "Redo deletion removes draft");
                session.Save(repository); Check(!session.IsDirty, "save establishes clean baseline");
                session.EditLevel("after save", data => data["displayName"] = "unsaved"); session.Undo();
                Check(!session.IsDirty, "Undo after save returns clean baseline");
                session.Discard(); Check(!session.IsDirty && !session.CanUndo && !session.CanRedo, "Discard restores saved baseline and clears history");
                var restored = AuthoringEditSession.Restore(second);
                Check(restored.ExportState() == second, "v1 roundtrip exact text remains compatible");
                if (sharing) AssertShared(restored);
                restored.Get(id).Data["displayName"] = "external";
                restored.Undo(); restored.Redo(); Check(restored.ExportState() == second, "restored history remains isolated");
                Debug.Log("PASS history ownership sharing=" + sharing + " " + folder); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void AssertShared(AuthoringEditSession session)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object current = typeof(AuthoringEditSession).GetField("current", flags).GetValue(session);
            var documents = (Dictionary<string, ContentDocument>)current.GetType().GetField("Documents", flags).GetValue(current);
            string stableId = documents.Values.First(doc => doc.Kind == "project").Id;
            foreach (object state in (IEnumerable)typeof(AuthoringEditSession).GetField("undo", flags).GetValue(session))
            {
                var previous = (Dictionary<string, ContentDocument>)state.GetType().GetField("Documents", flags).GetValue(state);
                Check(ReferenceEquals(documents[stableId], previous[stableId]), "unchanged document is shared across internal history");
            }
        }
        private static void Check(bool ok, string message)
        { if (!ok) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}
