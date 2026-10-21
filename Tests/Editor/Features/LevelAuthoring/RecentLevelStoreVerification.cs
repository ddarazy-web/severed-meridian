using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Editing;
using UnityEditor;

namespace LevelAuthoring.Editor
{
    public static class RecentLevelStoreVerification
    {
        public static void Run()
        {
            string root = Path.GetFullPath("Logs/LevelBrowser");
            Directory.CreateDirectory(root);
            string owned = Path.Combine(root, "recent-fixture-" + Guid.NewGuid().ToString("N"));
            string result = "FAIL not run";
            int code = 1;
            try
            {
                Type type = typeof(AuthoringEditSession).Assembly.GetType("LevelAuthoring.Storage.RecentLevelStore");
                if (type == null) throw new Exception("Missing RecentLevelStore");
                object store = Activator.CreateInstance(type, owned);
                string projectA = Path.Combine(owned, "A"), projectB = Path.Combine(owned, "B");
                var record = type.GetMethod("RecordOpened");
                string[] Read(object instance, string folder) => (string[])type.GetMethod("Read").Invoke(instance, new object[] { folder, "project-id" });
                for (int i = 1; i <= 21; i++) record.Invoke(store, new object[] { projectA, "project-id", "level-" + i });
                string[] ids = Read(store, projectA);
                if (ids.Length != 20 || ids[0] != "level-21" || ids[19] != "level-2") throw new Exception("MRU capacity/order");
                record.Invoke(store, new object[] { projectA, "project-id", "level-10" });
                ids = Read(Activator.CreateInstance(type, owned), projectA);
                if (ids.Length != 20 || ids[0] != "level-10" || ids.Distinct().Count() != 20) throw new Exception("Restart/dedup");
                if (Read(store, projectB).Length != 0) throw new Exception("Copied project ID must be isolated by folder");
                record.Invoke(store, new object[] { projectB, "project-id", "level-other" });
                if (Read(store, projectA)[0] != "level-10") throw new Exception("Other project overwrote history");
                if (Read(store, projectA + Path.DirectorySeparatorChar)[0] != "level-10") throw new Exception("Trailing separator normalization");
                if (Path.DirectorySeparatorChar == '\\' && Read(store, projectA.ToUpperInvariant())[0] != "level-10") throw new Exception("Windows path casing");
                if (((string[])type.GetMethod("Read").Invoke(store, new object[] { projectA, "different-project" })).Length != 0) throw new Exception("Project ID isolation");
                string path = (string)type.GetMethod("RecordPath", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(store, new object[] { projectA, "project-id" });
                string backup = File.ReadAllText(path + ".previous");
                File.WriteAllText(path, "broken-current");
                if (Read(store, projectA).Length != 20 || type.GetProperty("ReadWarning").GetValue(store) == null) throw new Exception("Backup recovery warning");
                if (File.ReadAllText(path) != "broken-current") throw new Exception("Read changed corrupt original");
                record.Invoke(store, new object[] { projectA, "project-id", "recovered" });
                if (Read(store, projectA)[0] != "recovered" || File.ReadAllText(path + ".previous") != backup) throw new Exception("Recovery write preserves backup");
                if (!Directory.GetFiles(owned, "*.corrupt-*").Any(file => File.ReadAllText(file) == "broken-current")) throw new Exception("Corrupt original not preserved");
                File.WriteAllText(path, "broken-both-current"); File.WriteAllText(path + ".previous", "broken-backup");
                if (Read(store, projectA).Length != 0 || type.GetProperty("ReadWarning").GetValue(store) == null) throw new Exception("Both corrupt fallback");
                if (File.ReadAllText(path) != "broken-both-current" || File.ReadAllText(path + ".previous") != "broken-backup") throw new Exception("Both originals must remain");
                string longDirectory = Path.Combine(owned, new string('p', 167 - owned.Length - 1));
                object longStore = Activator.CreateInstance(type, longDirectory);
                record.Invoke(longStore, new object[] { projectA, "project-id", "first" });
                record.Invoke(longStore, new object[] { projectA, "project-id", "second" });
                if (Read(longStore, projectA)[0] != "second") throw new Exception("Long directory atomic save");
                string longPath = (string)type.GetMethod("RecordPath", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(longStore, new object[] { projectA, "project-id" });
                File.WriteAllText(longPath, "long-corrupt");
                record.Invoke(longStore, new object[] { projectA, "project-id", "third" });
                if (Read(longStore, projectA)[0] != "third" || !Directory.GetFiles(longDirectory, "*.corrupt-*").Any(file => File.ReadAllText(file) == "long-corrupt"))
                    throw new Exception("Long directory corrupt preservation");
                result = "PASS recent capacity/order/revisit/restart/project and path isolation/normalization/corruption fallback/original preservation/long directory atomic save";
                code = 0;
            }
            catch (Exception error) { result = "FAIL " + error; UnityEngine.Debug.LogException(error); }
            finally
            {
                // 이번 검사에서 소유한 경로와 알려진 파일만 정리한다. 보고서는 별도 경로에 보존한다.
                if (Directory.Exists(owned))
                {
                    foreach (string file in Directory.GetFiles(owned, "*", SearchOption.AllDirectories))
                    {
                        string name = Path.GetFileName(file);
                        if (name.EndsWith(".json", StringComparison.Ordinal) || name.EndsWith(".json.previous", StringComparison.Ordinal) || name.Contains(".json.corrupt-")) File.Delete(file);
                    }
                    foreach (string subdirectory in Directory.GetDirectories(owned).OrderByDescending(path => path.Length))
                        if (!Directory.EnumerateFileSystemEntries(subdirectory).Any()) Directory.Delete(subdirectory);
                    if (!Directory.EnumerateFileSystemEntries(owned).Any()) Directory.Delete(owned);
                }
                File.WriteAllText(Path.Combine(root, "recent-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".txt"), result);
            }
            EditorApplication.Exit(code);
        }
    }
}
