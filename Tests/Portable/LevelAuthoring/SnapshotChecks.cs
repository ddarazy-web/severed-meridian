using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;
internal static partial class Program
{
    private static void SnapshotChecks(ContentDocument project, ContentDocument shape)
    {
        ((JArray)shape.Data["cells"]).Add(true);
        var source = new ContentSnapshot(new[] { project, shape });
        shape.Data["displayName"] = "外部 변경";
        Check((string)source.Get(shape.Id).Data["displayName"] == "빈 모양", "snapshot isolates input");
        var copy = source.Get(shape.Id); copy.Data["displayName"] = "읽은 사본 수정";
        Check((string)source.Get(shape.Id).Data["displayName"] == "빈 모양", "snapshot isolates returned document");
        string root = Path.GetFullPath(Path.Combine("ContentData", "Trials", "game-authoring-stage-02", "snapshot-" + Guid.NewGuid().ToString("N")));
        var repository = new ContentSnapshotStore(root);
        var first = repository.Publish(source, null);
        Check(repository.Read().Snapshot.Get(shape.Id).Kind == "shape", "snapshot publish and read");
        Reject(() => repository.Publish(source, null), "snapshot existing project conflict");
        var modifiedProject = project;
        modifiedProject.Data["sourceIds"] = new JArray(new JObject { ["sourceGuid"] = "0123456789abcdef0123456789abcdef", ["documentId"] = shape.Id });
        var modified = new ContentSnapshot(new[] { modifiedProject, shape });
        var second = repository.Publish(modified, first.Hash);
        Check(repository.ReadBackup().Hash == first.Hash, "whole snapshot backup");
        Check(repository.Read().Snapshot.Get(shape.Id).Id == shape.Id, "generation keeps stable ID");
        foreach (SnapshotPublishPhase phase in Enum.GetValues(typeof(SnapshotPublishPhase)))
        {
            var failing = new ContentSnapshotStore(root, current => { if (current == phase) throw new IOException("Injected"); });
            try { failing.Publish(source, second.Hash); throw new Exception("injection skipped"); } catch (IOException) { }
            var unchanged = repository.Read();
            Check(unchanged.Hash == second.Hash, "partial publish preserves project " + phase);
            Check(((JArray)unchanged.Snapshot.Project.Data["sourceIds"]).Count == 1, "partial publish preserves ID mapping " + phase);
        }
        Reject(() => new ContentSnapshot(new[] { project, shape, shape }), "duplicate document ID");
        var brokenProject = ContentJson.Read(ContentJson.Write(project));
        brokenProject.Data["defaultCatalogId"] = shape.Id;
        Reject(() => new ContentSnapshot(new[] { brokenProject, shape }), "reference wrong kind");
        brokenProject.Data["defaultCatalogId"] = "missing";
        Reject(() => new ContentSnapshot(new[] { brokenProject, shape }), "reference missing");
        var current = repository.Read();
        var manifest = current.Snapshot.Project;
        string oldPath = (string)manifest.Data["documents"][0]["path"];
        string newPath = oldPath.Replace("0000.json", "다른 이름.json");
        File.Move(Path.Combine(root, oldPath), Path.Combine(root, newPath));
        manifest.Data["documents"][0]["path"] = newPath;
        new JsonContentStore(root).Save(manifest, "project.json", new JsonContentStore(root).Read("project.json").Hash);
        Check(repository.Read().Snapshot.Get(shape.Id).Id == shape.Id, "rename preserves document identity");
        foreach (bool duringPublish in new[] { false, true })
        {
            var before = repository.Read();
            var fileStore = new JsonContentStore(root);
            string childPath = (string)before.Snapshot.Project.Data["documents"][0]["path"];
            string manifestHash = fileStore.Read("project.json").Hash;
            Action editChild = () =>
            {
                var child = fileStore.Read(childPath);
                child.Document.Data["displayName"] = "외부 수정 " + duringPublish;
                fileStore.Save(child.Document, childPath, child.Hash);
            };
            var publisher = new ContentSnapshotStore(root, phase =>
            {
                if (duringPublish && phase == SnapshotPublishPhase.BeforeProject) editChild();
            });
            if (!duringPublish) editChild();
            Reject(() => publisher.Publish(before.Snapshot, before.Hash), "child edit conflict " + duringPublish);
            Check(fileStore.Read("project.json").Hash == manifestHash, "child conflict preserves manifest " + duringPublish);
            Check((string)repository.Read().Snapshot.Get(shape.Id).Data["displayName"] == "외부 수정 " + duringPublish,
                "child conflict preserves external content " + duringPublish);
        }
    }
}
