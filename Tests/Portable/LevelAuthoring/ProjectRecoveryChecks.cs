using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;

internal static partial class Program
{
    private static void ProjectRecoveryChecks()
    {
        Type recentType = typeof(AuthoringEditSession).Assembly.GetType("LevelAuthoring.Storage.RecentProjectStore");
        Check(recentType != null, "recent internal project storage exists");
        if (recentType == null) return;
        string root = Path.GetFullPath("ContentData/Trials/game-authoring-stage-04/recovery-" + Guid.NewGuid().ToString("N"));
        dynamic recent = Activator.CreateInstance(recentType, new object[] { root });
        Check(((string[])recent.Read()).Length == 0 && !Directory.Exists(root), "absent recent record reads empty without creating project");
        for (int index = 0; index < 12; index++) recent.RecordOpened("프로젝트" + index);
        string[] names = recent.Read();
        Check(names.Length == 10 && names[0] == "프로젝트11" && names[9] == "프로젝트2", "recent projects bounded latest first");
        recent.RecordOpened("프로젝트5");
        names = recent.Read();
        Check(names[0] == "프로젝트5" && names.Count(name => name == "프로젝트5") == 1, "recent reopen moves unique name to front");
        recent.RecordOpened("Case"); recent.RecordOpened("case");
        Check(((string[])recent.Read()).Count(name => name.Equals("case", StringComparison.OrdinalIgnoreCase)) == 1, "recent names deduplicate across platform casing");
        string recentPath = Path.Combine(root, "recent-projects.json");
        string before = File.ReadAllText(recentPath);
        foreach (string invalid in new[] { null, "", " ", "..", "../escape", "folder/name", "folder\\name", "C:\\project", "content://provider/project", "CON", "bad.", " name" })
            Reject(() => recent.RecordOpened(invalid), "recent rejects external or invalid name " + invalid);
        Check(File.ReadAllText(recentPath) == before && !Directory.GetFiles(root).Any(path => path.Contains(".pending-")), "rejected recent name preserves disk without temporary files");
        dynamic reopened = Activator.CreateInstance(recentType, new object[] { root });
        Check(((string[])reopened.Read()).SequenceEqual((string[])recent.Read()), "recent projects survive new store instance");
        string[] previous = JObject.Parse(File.ReadAllText(recentPath + ".previous"))["projects"].Values<string>().ToArray();
        File.WriteAllText(recentPath, "broken");
        Check(((string[])reopened.Read()).SequenceEqual(previous), "corrupt recent record recovers previous list");
        Check(!string.IsNullOrEmpty((string)reopened.ReadWarning), "recent fallback exposes warning");
        reopened.RecordOpened("복구 후 열기");
        File.WriteAllText(recentPath, "broken again");
        Check(((string[])reopened.Read()).SequenceEqual(previous), "recent write after corruption preserves last good backup");
        File.WriteAllText(recentPath + ".previous", "{}");
        Reject(() => reopened.Read(), "corrupt recent record and backup fail visibly");

        string fixture = File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt");
        string project = root + "-project";
        new ContentSnapshotStore(project).Publish(new ContentSnapshotStore(fixture).Read().Snapshot, null);
        AuthoringToolWorkspace workspace = new AuthoringToolWorkspace();
        workspace.Open(project, "Cancel");
        AuthoringEditSession session = workspace.Session;
        string id = session.SelectedLevelId;
        int moves = (int)session.Get(id).Data["moveCount"];
        session.SelectCells(new[] { 40, 41 });
        session.Apply("복구 첫 변경", docs => docs[id].Data["moveCount"] = moves + 1);
        workspace.Save();
        session.Apply("복구 미완성 변경", docs => docs[id].Data["moveCount"] = -1);
        session.Apply("복구 선택 변경", docs => docs[id].Data["moveCount"] = -2);
        session.Undo();
        Check(session.IsDirty && session.CanUndo && session.CanRedo && session.ValidationError != null, "recovery fixture has incomplete current saved baseline undo redo");
        string state = workspace.ExportState();
        ToolDraftStore drafts = new ToolDraftStore(root + "-draft");
        JObject view = new JObject
        {
            ["layer"] = "Cover", ["tool"] = "Paint", ["boardX"] = 250,
            ["pendingInputs"] = new JArray(new JObject
            {
                ["path"] = new JArray(0, 1), ["name"] = "move-count", ["type"] = "UnityEngine.UIElements.IntegerField",
                ["text"] = "미확정 입력", ["value"] = moves, ["cursor"] = 6, ["selection"] = 6, ["focused"] = true
            })
        };
        drafts.Save(state, view, 2L);
        string journal = File.ReadAllText(Path.Combine(root + "-draft", "draft.json"));
        bool failed = false;
        try { workspace.Open(project + "-missing", "Discard"); } catch (IOException) { failed = true; }
        Check(failed && ReferenceEquals(session, workspace.Session) && workspace.ExportState() == state && File.ReadAllText(Path.Combine(root + "-draft", "draft.json")) == journal,
            "failed project switch keeps current saved history selection and prior checkpoint");
        drafts.Save(state, new JObject { ["layer"] = "Obstacle" }, 1L);
        Check(File.ReadAllText(Path.Combine(root + "-draft", "draft.json")) == journal, "older checkpoint cannot overwrite current project record");
        Directory.Delete(project, true);
        AuthoringToolWorkspace recovered = new AuthoringToolWorkspace();
        JObject record = new ToolDraftStore(root + "-draft").Read();
        recovered.RestoreState((string)record["workspace"]);
        Check(recovered.Folder == project && recovered.ExportState() == state && recovered.Session.ValidationError != null,
            "missing project recovery preserves incomplete current saved history and selection without recreation");
        Check((string)record["view"]["pendingInputs"][0]["text"] == "미확정 입력" && (int)record["view"]["boardX"] == 250,
            "recovery retains view tool and pending input");
        session.Redo(); recovered.Session.Redo();
        Check(session.ExportState() == recovered.Session.ExportState(), "recovery redo matches original session");
        session.Undo(); recovered.Session.Undo(); session.Undo(); recovered.Session.Undo();
        Check(session.ExportState() == recovered.Session.ExportState() && !recovered.Session.IsDirty,
            "recovery undo reaches saved baseline preserving revision");
        ToolDraftStore malformedView = new ToolDraftStore(root + "-view-draft");
        malformedView.Save(state, new JObject { ["layer"] = "Cover" }, 1L);
        malformedView.Save(state, new JObject { ["layer"] = "Obstacle" }, 2L);
        string malformedViewPath = Path.Combine(root + "-view-draft", "draft.json");
        JObject malformedRecord = JObject.Parse(File.ReadAllText(malformedViewPath));
        malformedRecord["view"]["layer"] = 123;
        File.WriteAllText(malformedViewPath, malformedRecord.ToString());
        malformedView.Save(state, new JObject { ["layer"] = "Floor" }, 3L);
        File.WriteAllText(malformedViewPath, "broken");
        Check((string)malformedView.ReadPrevious()["view"]["layer"] == "Cover",
            "malformed view cannot replace last screen-restorable draft backup");
        Reject(() => malformedView.Save(state, new JObject { ["sharedTutorialDraft"] = "{}" }, 4L),
            "invalid shared tutorial recovery view rejected before checkpoint publication");
        drafts.Save(state, new JObject { ["layer"] = "Obstacle" }, 3L);
        string draftPath = Path.Combine(root + "-draft", "draft.json");
        File.WriteAllText(draftPath, "broken");
        JObject fallback = drafts.Read();
        Check((string)fallback["view"]["layer"] == "Cover", "corrupt draft falls back to previous checkpoint");
        var warning = typeof(ToolDraftStore).GetProperty("ReadWarning");
        var readPrevious = typeof(ToolDraftStore).GetMethod("ReadPrevious");
        Check(warning != null && readPrevious != null, "draft recovery exposes previous choice and warning");
        Check(!string.IsNullOrEmpty((string)warning.GetValue(drafts)) && JToken.DeepEquals(fallback, readPrevious.Invoke(drafts, null) as JObject),
            "draft previous choice matches warned fallback");
        new ToolDraftStore(root + "-draft").Save(state, new JObject { ["layer"] = "Floor" }, 4L);
        File.WriteAllText(draftPath, "broken again");
        Check((string)drafts.Read()["view"]["layer"] == "Cover", "saving after corruption preserves last good draft backup");
        File.WriteAllText(draftPath + ".previous", "{}");
        Reject(() => drafts.Read(), "both corrupt drafts fail as content error");
        dynamic explicitDraft = drafts;
        Reject(() => explicitDraft.ReadPrevious(), "invalid explicit previous draft rejected");
    }
}
