using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static class CandidateMigrationVerification
    {
        public static void Run()
        {
            try
            {
                MethodInfo export = typeof(LegacyContentExporter).GetMethod("ExportCandidate", new[] { typeof(string) });
                Check(export != null, "explicit candidate migration entry exists");
                string folder = Path.GetFullPath("ContentData/Candidates/verification-" + Guid.NewGuid().ToString("N"));
                var sources = LegacyContentExporter.Discover();
                var original = sources.SelectMany(source => new[] { AssetDatabase.GetAssetPath(source.Asset), AssetDatabase.GetAssetPath(source.Asset) + ".meta" })
                    .Distinct().ToDictionary(path => path, File.ReadAllBytes);
                var first = (StoredContentSnapshot)export.Invoke(null, new object[] { folder });
                var report = JObject.Parse(File.ReadAllText(Path.Combine(folder, "migration-report.json")));
                Check((string)report["snapshotHash"] == first.Hash, "migration report identifies published candidate");
                Check(report["sources"].Count() == sources.Length, "all authoring sources inventoried");
                foreach (var source in report["sources"])
                {
                    Check(!string.IsNullOrEmpty((string)source["sha256"]) && !string.IsNullOrEmpty((string)source["metaSha256"]), "source and meta hashes recorded");
                    string id = (string)source["documentId"];
                    Check(first.Snapshot.Get(id).Kind == (string)source["kind"], "source mapping resolves correct document kind");
                    if ((string)source["kind"] == "element") Check(!string.IsNullOrEmpty((string)source["planningDocument"]), "element planning provenance recorded");
                }
                var second = (StoredContentSnapshot)export.Invoke(null, new object[] { folder });
                Check(JToken.DeepEquals(first.Snapshot.Project.Data["sourceIds"], second.Snapshot.Project.Data["sourceIds"]), "candidate re-export preserves source IDs");
                Check(first.Snapshot.Project.Id == second.Snapshot.Project.Id, "candidate project ID stable");
                Check(new ContentSnapshotStore(folder).ReadBackup().Hash == first.Hash, "previous candidate remains recoverable");
                string reportBefore = File.ReadAllText(Path.Combine(folder, "migration-report.json"));
                try { LegacyContentExporter.ExportCandidate(folder, sources, () => throw new IOException("이관 중단 주입")); throw new Exception("interruption accepted"); }
                catch (IOException) { }
                Check(new ContentSnapshotStore(folder).Read().Hash == second.Hash && File.ReadAllText(Path.Combine(folder, "migration-report.json")) == reportBefore,
                    "interrupted candidate preserves published snapshot and report");
                try { LegacyContentExporter.ExportCandidate(folder, sources.Concat(new[] { sources[0] }).ToArray(), null); throw new Exception("duplicate accepted"); }
                catch (LevelAuthoring.Documents.ContentFormatException) { }
                Check(new ContentSnapshotStore(folder).Read().Hash == second.Hash, "duplicate source does not partially publish");
                var edited = sources[0].Asset;
                string name = edited.name;
                try
                {
                    try { LegacyContentExporter.ExportCandidate(folder, sources, () => { edited.name = name + " pending"; EditorUtility.SetDirty(edited); }); throw new Exception("changed source accepted"); }
                    catch (LevelAuthoring.Documents.ContentFormatException) { }
                }
                finally { edited.name = name; EditorUtility.ClearDirty(edited); }
                Check(new ContentSnapshotStore(folder).Read().Hash == second.Hash, "concurrent unsaved source change blocks publication");
                foreach (var file in original) Check(File.ReadAllBytes(file.Key).SequenceEqual(file.Value), "source bytes preserved: " + file.Key);
                try { export.Invoke(null, new object[] { Path.GetFullPath("Assets/Data") }); throw new Exception("unsafe destination accepted"); }
                catch (TargetInvocationException error) when (error.InnerException is LevelAuthoring.Documents.ContentFormatException) { }
                Type selection = typeof(LegacyContentExporter).Assembly.GetType("LevelAuthoring.Editor.AuthoringSourceSelection");
                Check(selection != null, "validated source adoption exists");
                string configuration = Path.GetFullPath("Logs/GameAuthoringStage06/source-" + Guid.NewGuid().ToString("N") + ".json");
                selection.GetMethod("Adopt").Invoke(null, new object[] { folder, configuration });
                Check((string)selection.GetMethod("ReadFolder").Invoke(null, new object[] { configuration }) == folder, "adopted source reopens independently");
                string savedConfiguration = File.ReadAllText(configuration);
                var altered = JObject.Parse(reportBefore); altered["snapshotHash"] = "stale";
                File.WriteAllText(Path.Combine(folder, "migration-report.json"), altered.ToString());
                try
                {
                    try { selection.GetMethod("Adopt").Invoke(null, new object[] { folder, configuration }); throw new Exception("stale candidate adopted"); }
                    catch (TargetInvocationException error) when (error.InnerException is LevelAuthoring.Documents.ContentFormatException) { }
                    Check(File.ReadAllText(configuration) == savedConfiguration, "failed adoption preserves selected source");
                }
                finally { File.WriteAllText(Path.Combine(folder, "migration-report.json"), reportBefore); }
                string localConfiguration = Path.Combine(folder, "source-selection.json");
                selection.GetMethod("Adopt").Invoke(null, new object[] { folder, localConfiguration });
                string relocated = folder + "-relocated";
                foreach (string file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    string target = Path.Combine(relocated, Path.GetRelativePath(folder, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(file, target);
                }
                Check((string)selection.GetMethod("ReadFolder").Invoke(null, new object[] { Path.Combine(relocated, "source-selection.json") }) == relocated,
                    "relative selection and stable IDs survive folder relocation");
                string projectPath = Path.Combine(relocated, "project.json"), originalProject = File.ReadAllText(projectPath);
                var unsupported = JObject.Parse(originalProject); unsupported["schemaVersion"] = 999;
                File.WriteAllText(projectPath, unsupported.ToString());
                try
                {
                    try { selection.GetMethod("ReadFolder").Invoke(null, new object[] { Path.Combine(relocated, "source-selection.json") }); throw new Exception("unsupported source silently accepted"); }
                    catch (TargetInvocationException error) when (error.InnerException is LevelAuthoring.Documents.ContentFormatException) { }
                }
                finally { File.WriteAllText(projectPath, originalProject); }
                File.WriteAllText("Logs/GameAuthoringStage06/latest-candidate.txt", relocated);
                string selected = AuthoringSourceSelection.ReadFolder(AuthoringSourceSelection.DefaultConfiguration);
                string selectedHash = new ContentSnapshotStore(selected).Read().Hash;
                try { LegacyContentExporter.ExportCandidate(selected, sources, () => throw new IOException("selected export reached publication")); throw new Exception("selected export accepted"); }
                catch (LevelAuthoring.Documents.ContentFormatException) { }
                Check(new ContentSnapshotStore(selected).Read().Hash == selectedHash, "adopted source rejects re-export before publication");
                var changed = second.Snapshot.Documents;
                changed.First(document => document.Kind == "level").Data["moveCount"] = 123;
                var changedStored = new ContentSnapshotStore(folder).Publish(new LevelAuthoring.Documents.ContentSnapshot(changed), second.Hash);
                try { LegacyContentExporter.ExportCandidate(folder); throw new Exception("edited candidate overwritten"); }
                catch (LevelAuthoring.Documents.ContentFormatException) { }
                Check(new ContentSnapshotStore(folder).Read().Hash == changedStored.Hash, "edited candidate preserved");
                Debug.Log("PASS candidate migration"); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void Check(bool value, string text) { if (!value) throw new Exception("FAIL " + text); Debug.Log("PASS " + text); }
    }
}
