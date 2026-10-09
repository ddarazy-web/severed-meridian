using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;

internal static partial class Program
{
    private static void ToolWorkspaceChecks()
    {
        Type type = typeof(AuthoringEditSession).Assembly.GetType("LevelAuthoring.Editing.AuthoringToolWorkspace");
        Check(type != null, "runtime tool workspace exists");
        if (type == null) return;
        dynamic workspace = Activator.CreateInstance(type);
        string fixture = File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt");
        string root = Path.GetFullPath("ContentData/Trials/game-authoring-stage-03/tool-" + Guid.NewGuid().ToString("N"));
        var store = new ContentSnapshotStore(root);
        store.Publish(new ContentSnapshotStore(fixture).Read().Snapshot, null);
        Check((bool)workspace.Open(root, "Cancel"), "tool opens clean workspace");
        AuthoringEditSession session = workspace.Session;
        string id = session.SelectedLevelId;
        int moves = (int)session.Get(id).Data["moveCount"];
        session.SelectCells(new[] { 40 });
        session.Apply("移動", docs => docs[id].Data["moveCount"] = moves + 1);
        Check(!(bool)workspace.Reload("Cancel") && ReferenceEquals(session, workspace.Session) && session.IsDirty,
            "cancel reload preserves dirty session and selection");
        bool rejected = false;
        try { workspace.Open(root + "-missing", "Discard"); }
        catch (IOException) { rejected = true; }
        Check(rejected && ReferenceEquals(session, workspace.Session) && session.SelectedCells.Single() == 40,
            "failed open with discard keeps draft and selection");
        workspace.Save();
        Check(!session.IsDirty && session.CanUndo, "tool save keeps undo history");
        session.Apply("수정", docs => docs[id].Data["moveCount"] = moves + 2);
        var disk = store.Read(); store.Publish(disk.Snapshot, disk.Hash);
        rejected = false;
        try { workspace.Save(); } catch (ContentFormatException) { rejected = true; }
        Check(rejected && session.IsDirty && session.CanUndo, "tool external conflict keeps draft");
        rejected = false;
        try { workspace.Open(fixture, "Save"); } catch (ContentFormatException) { rejected = true; }
        Check(rejected && ReferenceEquals(session, workspace.Session) && workspace.Folder == root,
            "failed save before switch does not switch folders");
        Check((bool)workspace.Reload("Discard") && !workspace.Session.IsDirty, "explicit discard reloads disk");
        Check((bool)workspace.OpenBackup(root, "Cancel") && workspace.IsRecovery,
            "backup opens in protected recovery workspace");
        rejected = false;
        try { workspace.Save(); } catch (ContentFormatException) { rejected = true; }
        Check(rejected && workspace.IsRecovery, "backup cannot overwrite damaged source");
        string copy = root + "-copy";
        workspace.SaveCopy(copy);
        Check(!workspace.IsRecovery && workspace.Folder == copy && File.Exists(Path.Combine(copy, "project.json")),
            "recovery saved to independent folder and becomes editable");
        dynamic reopened = Activator.CreateInstance(type);
        reopened.Open(copy, "Cancel");
        Check(reopened.Session.SelectedLevelId == workspace.Session.SelectedLevelId,
            "restart reopens saved project");
        AuthoringEditSession current = workspace.Session;
        string currentId = current.SelectedLevelId;
        current.Apply("새 프로젝트 전 변경", docs => docs[currentId].Data["moveCount"] = moves + 7);
        ContentSnapshot template = new ContentSnapshotStore(fixture).Read().Snapshot;
        string fresh = root + "-fresh";
        Check(!(bool)workspace.Create(fresh, template, "Cancel") && ReferenceEquals(current, workspace.Session) && !Directory.Exists(fresh),
            "cancel new project keeps draft and creates no files");
        rejected = false;
        try { workspace.Create(copy, template, "Discard"); } catch (ContentFormatException) { rejected = true; }
        Check(rejected && ReferenceEquals(current, workspace.Session) && current.IsDirty,
            "failed new project leaves dirty current project");
        Check((bool)workspace.Create(fresh, template, "Save") && workspace.Folder == fresh && !workspace.Session.IsDirty,
            "new project allowed from existing workspace");
        Check((int)new ContentSnapshotStore(copy).Read().Snapshot.Get(currentId).Data["moveCount"] == moves + 7,
            "save before new project persists old draft");
        current = workspace.Session; currentId = current.SelectedLevelId;
        current.Apply("다시 읽기 전 수정", docs => docs[currentId].Data["moveCount"] = moves + 9);
        Check((bool)workspace.Reload("Save") && (int)workspace.Session.Get(currentId).Data["moveCount"] == moves + 9,
            "save then reload same folder reads new values");
        current = workspace.Session;
        current.Apply("실패 보호", docs => docs[currentId].Data["moveCount"] = moves + 10);
        string beforeFailure = current.ExportState();
        string future = root + "-future";
        Directory.CreateDirectory(future);
        File.WriteAllText(Path.Combine(future, "project.json"), File.ReadAllText(Path.Combine(fresh, "project.json")).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 999"));
        Reject(() => workspace.Open(future, "Discard"), "unsupported workspace rejected");
        Check(current.ExportState() == beforeFailure && ReferenceEquals(current, workspace.Session), "unsupported open preserves draft selection history");
        string blockedPath = root + "-file"; File.WriteAllText(blockedPath, "not a directory");
        rejected = false;
        try { workspace.SaveCopy(blockedPath); } catch (IOException) { rejected = true; }
        Check(rejected && current.ExportState() == beforeFailure && workspace.Folder == fresh, "IO save failure preserves workspace");
        Reject(() => workspace.SaveCopy(copy), "save copy cannot overwrite another project");
        Check(current.ExportState() == beforeFailure, "failed copy preserves undo and revision");
        foreach (int version in new[] { 1, 2, 3 })
        {
            var legacy = new ContentSnapshot(template.Documents.Select(doc =>
            {
                if (doc.Kind != "level") return doc;
                var data = doc.Data;
                data["schemaVersion"] = version;
                return new ContentDocument(doc.Kind, doc.Id, data);
            }));
            string legacyFolder = root + "-legacy-" + version;
            var legacyStore = new ContentSnapshotStore(legacyFolder);
            var published = legacyStore.Publish(legacy, null);
            legacyStore.Publish(legacy, published.Hash);
            Reject(() => workspace.Open(legacyFolder, "Discard"), "tool rejects legacy version " + version);
            Reject(() => workspace.OpenBackup(legacyFolder, "Discard"), "tool rejects legacy backup " + version);
            Check(ReferenceEquals(current, workspace.Session) && current.ExportState() == beforeFailure && workspace.Folder == fresh,
                "legacy rejection preserves draft selection history " + version);
        }
        var export = type.GetMethod("ExportState");
        Check(export != null, "tool workspace exports recovery state");
        string recovery = (string)export.Invoke(workspace, null);
        dynamic recovered = Activator.CreateInstance(type);
        recovered.RestoreState(recovery);
        Check(recovered.Folder == workspace.Folder && recovered.Session.ExportState() == current.ExportState(),
            "workspace recovery preserves folder dirty selection and history");
        Reject(() => recovered.RestoreState("{}"), "bad recovery rejected");
        Check(recovered.Session.ExportState() == current.ExportState(), "bad recovery keeps current draft");
        var changed = new ContentSnapshotStore(fresh).Read();
        new ContentSnapshotStore(fresh).Publish(changed.Snapshot, changed.Hash);
        Reject(() => recovered.Save(), "recovered draft still detects external changes");
        dynamic backupWorkspace = Activator.CreateInstance(type);
        backupWorkspace.OpenBackup(fresh, "Cancel");
        recovered.RestoreState((string)backupWorkspace.ExportState());
        Check((bool)recovered.IsRecovery, "workspace recovery preserves protected backup mode");
        Reject(() => recovered.Save(), "restored backup cannot overwrite source");
        Type draftType = type.Assembly.GetType("LevelAuthoring.Storage.ToolDraftStore");
        Check(draftType != null, "durable tool draft store exists");
        dynamic draftStore = Activator.CreateInstance(draftType, new object[] { root + "-journal" });
        var view = new Newtonsoft.Json.Linq.JObject { ["layer"] = "Obstacle", ["boardX"] = 125 };
        draftStore.Save(recovery, view, 1L);
        Newtonsoft.Json.Linq.JObject draft = draftStore.Read();
        Check((string)draft["workspace"] == recovery && (int)draft["view"]["boardX"] == 125,
            "durable journal preserves workspace and view");
        string previousText = File.ReadAllText(Path.Combine(root + "-journal", "draft.json"));
        Reject(() => draftStore.Save("{}", view, 2L), "invalid journal rejected before replacing good draft");
        Check(File.ReadAllText(Path.Combine(root + "-journal", "draft.json")) == previousText, "invalid journal preserves disk");
        view["boardX"] = 200;
        draftStore.Save(recovery, view, 3L);
        draftStore.Save(recovery, new Newtonsoft.Json.Linq.JObject { ["boardX"] = 1 }, 2L);
        Check((int)draftStore.Read()["view"]["boardX"] == 200, "late older checkpoint cannot replace latest");
        File.WriteAllText(Path.Combine(root + "-journal", "draft.json"), "bad");
        draft = draftStore.Read();
        Check((int)draft["view"]["boardX"] == 125, "corrupt journal recovers previous good checkpoint");
    }
}
