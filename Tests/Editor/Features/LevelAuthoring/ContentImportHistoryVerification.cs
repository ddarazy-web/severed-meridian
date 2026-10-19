using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace LevelAuthoring.Editor
{
    public static class ContentImportHistoryVerification
    {
        public static void Run() => Verify(false);
        public static void RunWithImportUI() => Verify(true);
        private static void Verify(bool includeImportUI)
        {
            string root = Path.GetFullPath("Logs/ImportHistoryVerification/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            int checks = 0;
            void Check(bool condition, string name)
            {
                if (!condition) throw new Exception(name);
                checks++; File.AppendAllText(Path.Combine(root, "result.txt"), "PASS " + name + "\n");
            }
            void Reject(Action action, string name)
            {
                bool rejected = false;
                try { action(); } catch (Exception) { rejected = true; }
                Check(rejected, name);
            }
            string Write(string path, string content)
            {
                path = Path.Combine(root, path); Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, content); return path;
            }
            try
            {
                string selected = Path.Combine(root, "ContentData/Candidates/main");
                var snapshot = new ContentSnapshotStore(Path.GetFullPath("ContentData/Candidates/main")).Read().Snapshot;
                new ContentSnapshotStore(selected).Publish(snapshot, null);
                string configuration = Write("ProjectSettings/AuthoringSource.json", new JObject
                {
                    ["version"] = 1, ["folder"] = "../ContentData/Candidates/main", ["projectId"] = snapshot.Project.Id
                }.ToString());
                string old = Write("ContentData/Candidates/import-20261019-100000-" + new string('a', 32) + "/project.json", "old");
                string review = Write("ContentData/Candidates/import-20261019-100000-" + new string('b', 32) + "/project.json", "review");
                review = Path.GetDirectoryName(review);
                string package = Write("ContentData/Candidates/.package-" + new string('c', 32) + "/input.zip", "temporary");
                string backup = Write("ProjectSettings/AuthoringSource.json.previous-" + new string('d', 32), "backup");
                string temporary = Write("Assets/Data/LevelPacks/levels-000001.bytes.pending-" + new string('e', 32), "pending");
                string temporaryMeta = Write("Assets/Data/LevelPacks/levels-000001.bytes.pending-" + new string('e', 32) + ".meta", "meta");
                string pack = Write("Assets/Data/LevelPacks/levels-000001.bytes", "published pack");
                string unknown = Write("ContentData/Candidates/personal/project.json", "not owned");
                string zip = Write("source.zip", "original zip");
                string configBefore = File.ReadAllText(configuration);

                ImportHistoryPlan plan = ContentImportHistory.Preview(root, review);
                File.AppendAllText(Path.Combine(root, "result.txt"), string.Join("\n", plan.Entries.Select(value => value.Path)) + "\n");
                Check(plan.Entries.Count == 5 && plan.FileCount == 5, "only unused candidate, package, backup and temporary selected");
                Check(plan.Bytes == 6 + 9 + 7 + 3 + 4, "preview byte count");
                Check(File.Exists(old) && File.Exists(backup), "preview does not delete");
                Check(!plan.Entries.Any(value => value.Path == selected || value.Path == review), "current source and review protected");

                Check(ContentImportHistory.Preview(root, Path.Combine(root, "ContentData/Candidates")).Entries.Count == 3, "review parent protects all descendant candidates");
                File.WriteAllText(backup, "changed");
                Reject(() => ContentImportHistory.Delete(plan, review), "changed files abort deletion");
                Check(File.Exists(old), "no partial delete on stale preview");
                plan = ContentImportHistory.Preview(root, review);
                using (var held = new FileStream(configuration + ".import.lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    Reject(() => ContentImportHistory.Delete(plan, review), "active import blocks cleanup");
                string journal = Write("Library/AuthoringPacks/publication.json", "recovery");
                Reject(() => ContentImportHistory.Preview(root, review), "recovery journal blocks cleanup");
                File.Delete(journal);
                using (var held = new FileStream(journal + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    Reject(() => ContentImportHistory.Delete(plan, review), "active publication blocks cleanup");
                using (var held = new FileStream(old, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    Reject(() => ContentImportHistory.Preview(root, review), "open candidate files block cleanup");
                Reject(() => ContentImportHistory.Delete(plan, Path.GetDirectoryName(old)), "changed review target invalidates preview");
                File.WriteAllText(configuration, configBefore + "\n");
                Reject(() => ContentImportHistory.Delete(plan, review), "changed source selection invalidates preview");
                File.WriteAllText(configuration, configBefore);

                plan = ContentImportHistory.Preview(root, review);
                ContentImportHistory.Delete(plan, review);
                Check(!File.Exists(old) && !File.Exists(package) && !File.Exists(backup) && !File.Exists(temporary) && !File.Exists(temporaryMeta), "reviewed history deleted");
                Check(Directory.Exists(selected) && File.Exists(Path.Combine(review, "project.json")) && File.Exists(pack) &&
                    File.Exists(unknown) && File.Exists(zip) && File.ReadAllText(configuration) == configBefore, "source, review, packs, unknown folders and original ZIP preserved");
                Check(new ContentSnapshotStore(selected).Read().Snapshot.Project.Id == snapshot.Project.Id, "source still readable");
                Check(ContentImportHistory.Preview(root, review).Entries.Count == 0, "repeat cleanup has nothing to delete");
                File.AppendAllText(Path.Combine(root, "result.txt"), "PASS " + checks + " checks / FAIL 0\n");
                if (includeImportUI) ExternalProjectImportVerification.RunUI(); else EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                File.AppendAllText(Path.Combine(root, "result.txt"), "FAIL " + error);
                UnityEngine.Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
    }
}
