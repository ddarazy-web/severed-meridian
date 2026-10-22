using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;

internal static partial class Program
{
    private static void NativeStartChecks()
    {
        string root = Path.GetFullPath(Path.Combine("Logs", "NativeStartChecks", Guid.NewGuid().ToString("N")));
        try
        {
            var project = new ContentDocument("project", "project-start", new JObject
            {
                ["name"] = "첫 프로젝트", ["contentVersion"] = 1, ["defaultCatalogId"] = null,
                ["resources"] = new JArray(), ["documents"] = new JArray(), ["sourceIds"] = new JArray()
            });
            var workspace = new AuthoringToolWorkspace();
            Check(workspace.Create(root, new ContentSnapshot(new[] { project })), "create project without implicit level");
            Check(workspace.Session.SelectedLevelId == null && !workspace.Session.IsDirty, "empty clean session");
            workspace.Save();
            var reopened = new AuthoringToolWorkspace();
            Check(reopened.Open(root, "Cancel") && reopened.Session.SelectedLevelId == null, "reopen empty project");
            var restored = new AuthoringToolWorkspace();
            restored.RestoreState(reopened.ExportState());
            Check(restored.Session.SelectedLevelId == null, "restore empty session");
            Check(reopened.OpenListing(root, "Cancel"), "empty listing opens");
            Check(reopened.Directory.Entries.Count == 0, "empty listing has no levels");
            reopened.BeginEditingProject();
            var session = reopened.Session;
            var source = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt").Trim()).Read();
            JObject template = source.Snapshot.Documents.First(doc => doc.Kind == "level").Data;
            // 검사 원본의 공유 참조 대신 독립적인 최소 보드만 사용한다.
            template["catalogId"] = null;
            if (template["tutorial"] is JObject tutorial) tutorial["flowId"] = null;
            string id = DocumentEditing.AddLevel(session, template, 1, "첫 레벨");
            Check(session.IsDirty && session.SelectedLevelId == id, "first level stays dirty in memory");
            using (var listing = new ContentSnapshotStore(root).ReadDirectory())
                Check(listing.Entries.Count == 0, "first level not implicitly saved");
            session.Undo();
            Check(session.SelectedLevelId == null && session.DocumentIdsOfKind("level").Length == 0, "undo first level returns to empty project");
            session.Redo();
            Check(session.SelectedLevelId == id, "redo first level restores selection");
            var roundtrip = AuthoringEditSession.Restore(session.ExportState());
            Check(roundtrip.SelectedLevelId == id && roundtrip.IsDirty, "lazy first level history restores");
            roundtrip.Undo();
            Check(roundtrip.SelectedLevelId == null, "restored history contains empty state");
            reopened.Save();
            var saved = new ContentSnapshotStore(root).Read();
            Check(saved.Snapshot.Get(id).Data.ToString() == session.Get(id).Data.ToString(), "first level saved without data loss");
            string beforeCopy = session.Get(id).Data.ToString();
            string copy = DocumentEditing.DuplicateLevel(session, id, 2, "복제 레벨");
            foreach (string field in new[] { "elements", "missions", "moveCount", "tutorial", "elementSupply" })
                Check(JToken.DeepEquals(session.Get(id).Data[field], session.Get(copy).Data[field]), "duplicate preserves " + field);
            Check(session.Get(id).Data.ToString() == beforeCopy, "duplicate leaves source unchanged");
            string stateBeforeFailure = session.ExportState();
            try { reopened.Open(Path.Combine(root, "missing-project"), "Discard"); throw new Exception("invalid project accepted"); }
            catch (DirectoryNotFoundException) { }
            Check(session.ExportState() == stateBeforeFailure && ReferenceEquals(session, reopened.Session), "failed open preserves pending work");
            Check(!reopened.Open(root, "Cancel") && session.ExportState() == stateBeforeFailure, "cancel project switch preserves pending work");
            reopened.Save();
            var persisted = new AuthoringToolWorkspace(); persisted.Open(root, "Cancel");
            Check(persisted.Session.Get(copy).Data.ToString() == session.Get(copy).Data.ToString(), "duplicate survives project reopen");
            var internalRecent = new RecentProjectStore(Path.Combine(root, "state"));
            internalRecent.RecordOpened("첫 프로젝트");
            var folderRecent = new RecentProjectStore(Path.Combine(root, "state"), true);
            folderRecent.RecordOpened(root);
            Check(new RecentProjectStore(Path.Combine(root, "state"), true).Read().Single() == root, "Windows recent folder survives reopen");
            Check(internalRecent.Read().Single() == "첫 프로젝트", "Android recent names preserved separately");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
