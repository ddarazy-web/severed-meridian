using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using LevelTool;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    public static partial class LevelBrowserDocumentPerformanceVerification
    {
        public static void RunRecovery1000() => RunRecovery(1000);
        public static void RunRecovery10000() => RunRecovery(10000);
        private static void RunRecovery(int levelCount)
        {
            string owned = Path.GetFullPath("Logs/LevelBrowserDocuments/recovery-" + Guid.NewGuid().ToString("N"));
            string data = Path.Combine(owned, "data"), fixture = Path.Combine(data, "project");
            Directory.CreateDirectory(owned); report = Path.Combine(owned, "measurements.jsonl"); count = levelCount; sample = 0;
            long before = GC.GetAllocatedBytesForCurrentThread(); GC.KeepAlive(new byte[65536]);
            allocationAvailable = GC.GetAllocatedBytesForCurrentThread() > before;
            guard = new System.Threading.Timer(_ =>
            {
                if (GetProcessMemoryInfo(new IntPtr(-1), out MemoryCounters memory, (uint)Marshal.SizeOf<MemoryCounters>()) &&
                    memory.WorkingSet.ToUInt64() < 6UL * 1024 * 1024 * 1024) return;
                File.WriteAllText(Path.Combine(owned, "protection.json"), activeOperation ?? "unknown"); TerminateProcess(new IntPtr(-1), 3);
            }, null, 1000, 1000);
            int exit = 1;
            try
            {
                JObject selection = JObject.Parse(File.ReadAllText("ProjectSettings/AuthoringSource.json"));
                string sourcePath = Path.GetFullPath(Path.Combine("ProjectSettings", (string)selection["folder"]));
                StoredContentSnapshot source = new ContentSnapshotStore(sourcePath).Read();
                Measure("fixture-write", () => WriteListingFixture(fixture, source.Snapshot.Documents, count));
                for (sample = 0; sample < 4; sample++)
                {
                var workspace = new AuthoringToolWorkspace(); workspace.OpenListing(fixture, "Cancel"); workspace.OpenLevel("listing-level-1");
                workspace.Session.EditLevel("복구 측정", body => body["displayName"] = "recovery measurement " + sample);
                workspace.Session.SelectCells(new[] { 40 });
                string recovery = Path.Combine(data, "workspace-v2.json");
                Measure("workspace-v2-stream-write-first", () =>
                { using var output = new StreamWriter(recovery, false, new UTF8Encoding(false)); workspace.WriteState(output); });
                File.AppendAllText(report, new JObject { ["kind"] = "recovery-file", ["levels"] = count,
                    ["bytes"] = new FileInfo(recovery).Length, ["sourceHash"] = source.Hash,
                    ["scope"] = "first plus three normal samples; source references and changed bodies; prior sample save generation retained",
                    ["sample"] = sample,
                    ["states"] = "current + saved + one undo", ["allocationAvailable"] = allocationAvailable
                }.ToString(Formatting.None) + "\n");
                var restored = new AuthoringToolWorkspace();
                Measure("workspace-v2-stream-restore", () => { using var input = new StreamReader(recovery); restored.RestoreState(input); });
                if (!restored.Session.IsDirty || restored.Session.SelectedLevelId != "listing-level-1" || restored.Session.SelectedCells.Single() != 40)
                    throw new Exception("Recovery changed selection or dirty state");
                restored.Session.Undo(); if (restored.Session.IsDirty) throw new Exception("Recovered Undo lost baseline");
                restored.Session.Redo(); if (!restored.Session.IsDirty) throw new Exception("Recovered Redo lost change");
                var draft = new ToolDraftStore(Path.Combine(data, "draft-" + sample));
                string draftText = File.ReadAllText(recovery);
                Measure("draft-wrapper-write", () => draft.Save(draftText, new JObject { ["layer"] = "Block" }, 1));
                var applied = new AuthoringToolWorkspace();
                Measure("draft-wrapper-read-and-apply", () =>
                {
                    object prepared = typeof(ToolDraftStore).GetMethod("ReadPrepared", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(draft, null);
                    prepared.GetType().GetMethod("ApplyTo", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(prepared, new object[] { applied, false });
                });
                if (!applied.Session.IsDirty || applied.Session.SelectedCells.Single() != 40) throw new Exception("Draft wrapper lost state");
                Measure("restored-incremental-save", restored.Save);
                if (restored.Session.IsDirty) throw new Exception("Restored save did not clear dirty state");
                }
                File.WriteAllText(Path.Combine(owned, "result.txt"), "PASS actual " + count + " levels; v2 stream and ToolDraftStore write/restore, dirty/selection/Undo/Redo and incremental save; first plus three normal samples");
                exit = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(owned, "result.txt"), "FAIL " + error); Debug.LogException(error); }
            finally { guard?.Dispose(); if (Directory.Exists(data)) Directory.Delete(data, true); }
            EditorApplication.Exit(exit);
        }

        public static void RunListings()
        {
            string owned = Path.GetFullPath("Logs/LevelBrowserDocuments/listing-" + Guid.NewGuid().ToString("N"));
            string data = Path.Combine(owned, "data"); Directory.CreateDirectory(owned);
            report = Path.Combine(owned, "measurements.jsonl");
            long before = GC.GetAllocatedBytesForCurrentThread(); GC.KeepAlive(new byte[65536]);
            allocationAvailable = GC.GetAllocatedBytesForCurrentThread() > before;
            guard = new System.Threading.Timer(_ =>
            {
                if (GetProcessMemoryInfo(new IntPtr(-1), out MemoryCounters memory, (uint)Marshal.SizeOf<MemoryCounters>()) &&
                    memory.WorkingSet.ToUInt64() < 6UL * 1024 * 1024 * 1024) return;
                File.WriteAllText(Path.Combine(owned, "protection.json"), activeOperation ?? "unknown");
                TerminateProcess(new IntPtr(-1), 3);
            }, null, 1000, 1000);
            int exit = 1;
            try
            {
                JObject selection = JObject.Parse(File.ReadAllText("ProjectSettings/AuthoringSource.json"));
                string sourcePath = Path.GetFullPath(Path.Combine("ProjectSettings", (string)selection["folder"]));
                StoredContentSnapshot source = new ContentSnapshotStore(sourcePath).Read();
                ContentDocument[] originals = source.Snapshot.Documents;
                foreach (int size in new[] { 1000, 10000 })
                {
                    count = size; sample = -1;
                    string fixture = Path.Combine(data, size.ToString());
                    string indexPath = null;
                    Measure("fixture-write", () => indexPath = WriteListingFixture(fixture, originals, size));
                    File.AppendAllText(report, new JObject { ["kind"] = "fixture", ["levels"] = size,
                        ["sourceHash"] = source.Hash, ["sourceLevels"] = originals.Count(doc => doc.Kind == "level"),
                        ["bytes"] = Directory.GetFiles(fixture, "*.json", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length),
                        ["unity"] = Application.unityVersion, ["os"] = Environment.OSVersion.ToString(),
                        ["cpuCount"] = Environment.ProcessorCount, ["allocationAvailable"] = allocationAvailable,
                        ["scope"] = "first in-process sample + three normal samples; OS disk cache not flushed; display preparation, not rendered frame"
                    }.ToString(Formatting.None) + "\n");
                    for (sample = 0; sample < 4; sample++) RunListingSample(fixture, indexPath, originals, size);
                }
                File.WriteAllText(Path.Combine(owned, "result.txt"), "PASS actual 1000/10000 files; all-body locks during listing/search; only selected levels unlocked; incremental save; four samples each");
                exit = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(owned, "result.txt"), "FAIL " + error); Debug.LogException(error); }
            finally
            {
                guard?.Dispose();
                if (Directory.Exists(data) && Path.GetFullPath(data).StartsWith(owned + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) Directory.Delete(data, true);
            }
            EditorApplication.Exit(exit);
        }

        private static string WriteListingFixture(string root, ContentDocument[] originals, int size)
        {
            Directory.CreateDirectory(Path.Combine(root, "bodies"));
            var entries = new JArray(); var records = new JArray();
            var utf8 = new UTF8Encoding(false);
            using var sha = SHA256.Create();
            string Hash(byte[] bytes) => BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            void Write(string id, string kind, int number, int version, string name, string json)
            {
                string path = "bodies/" + entries.Count + ".json";
                byte[] bytes = utf8.GetBytes(json); File.WriteAllBytes(Path.Combine(root, path), bytes);
                entries.Add(new JObject { ["id"] = id, ["path"] = path });
                records.Add(new JObject { ["id"] = id, ["path"] = path, ["kind"] = kind, ["number"] = number,
                    ["levelVersion"] = version, ["name"] = name, ["hash"] = Hash(bytes) });
            }
            foreach (ContentDocument document in originals.Where(doc => doc.Kind != "level" && doc.Kind != "project"))
                Write(document.Id, document.Kind, 0, 0, "", ContentJson.Write(document));
            ContentDocument[] levels = originals.Where(doc => doc.Kind == "level").ToArray();
            // 검증된 원본의 내용은 유지하고 루트 ID·번호·표시명만 치환한다. 본문 1만 개를 모으지 않는다.
            string[] templates = levels.Select(doc =>
            {
                var body = (JObject)doc.Data.DeepClone(); body["levelNumber"] = int.MaxValue; body["displayName"] = "__LISTING_NAME__";
                return new JObject { ["kind"] = "level", ["schemaVersion"] = 1, ["id"] = "__LISTING_ID__", ["data"] = body }.ToString(Formatting.None);
            }).ToArray();
            for (int i = 1; i <= size; i++)
            {
                string id = "listing-level-" + i, name = "대량 검사 " + i;
                string json = templates[(i - 1) % templates.Length].Replace("__LISTING_ID__", id).Replace("__LISTING_NAME__", name)
                    .Replace("\"levelNumber\":2147483647", "\"levelNumber\":" + i);
                Write(id, "level", i, (int)levels[(i - 1) % levels.Length].Data["schemaVersion"], name, json);
            }
            ContentDocument project = originals.Single(doc => doc.Kind == "project");
            JObject projectBody = (JObject)project.Data.DeepClone(); projectBody["documents"] = entries;
            byte[] manifest = utf8.GetBytes(ContentJson.Write(new ContentDocument("project", project.Id, projectBody)));
            File.WriteAllBytes(Path.Combine(root, "project.json"), manifest);
            string hash = Hash(manifest);
            string index = Path.Combine(root, ".authoring", "listings", hash + ".json"); Directory.CreateDirectory(Path.GetDirectoryName(index));
            File.WriteAllText(index, new JObject { ["version"] = 1, ["manifestHash"] = hash, ["entries"] = records }.ToString(Formatting.None), utf8);
            return index;
        }

        private static void RunListingSample(string fixture, string indexPath, ContentDocument[] originals, int size)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var locks = new Dictionary<string, FileStream>();
            GameObject host = null;
            try
            {
                using (var directory = new ContentSnapshotStore(fixture).ReadDirectory())
                    foreach (ProjectDocumentEntry entry in directory.Entries)
                        locks.Add(entry.Id, new FileStream(Path.Combine(fixture, entry.RelativePath), FileMode.Open, FileAccess.ReadWrite, FileShare.None));
                host = new GameObject("listing-performance"); host.SetActive(false);
                var screen = host.AddComponent<LevelToolScreen>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                MethodInfo prepare = typeof(LevelToolScreen).GetMethod("PrepareDisplay", flags);
                void Prepare() => prepare.Invoke(screen, null);
                Measure("listing-open-all-bodies-locked", () => screen.Workspace.OpenListing(fixture, "Cancel"));
                Measure("listing-display-all-bodies-locked", Prepare);
                var index = (LevelBrowserIndex)typeof(LevelToolScreen).GetField("levelIndex", flags).GetValue(screen);
                Measure("search-180-queries-all-bodies-locked", () =>
                {
                    for (int i = 0; i < 60; i++) { index.Search("125"); index.Search("120~140"); index.Search("대량"); }
                });
                if (index.Count != size) throw new Exception("Listing count differs");
                if (sample == 0)
                {
                    File.Move(indexPath, indexPath + ".held");
                    try { Measure("legacy-listing-all-bodies-locked", () => { screen.Workspace.OpenListing(fixture, "Cancel"); Prepare(); }); }
                    finally { File.Move(indexPath + ".held", indexPath); }
                    screen.Workspace.OpenListing(fixture, "Cancel"); Prepare();
                }
                // 편집에 필요한 공유 자료와 선택 레벨만 허용한다. 다른 모든 레벨은 계속 잠근다.
                foreach (ContentDocument doc in originals.Where(doc => doc.Kind != "project" && doc.Kind != "level")) { locks[doc.Id].Dispose(); locks.Remove(doc.Id); }
                void Unlock(string id) { locks[id].Dispose(); locks.Remove(id); }
                Unlock("listing-level-1");
                Measure("first-level-edit-open", () => screen.Workspace.OpenLevel("listing-level-1"));
                Measure("first-level-display-prepare", Prepare);
                Unlock("listing-level-" + size);
                Measure("last-level-edit-open", () => screen.Workspace.OpenLevel("listing-level-" + size));
                Measure("last-level-display-prepare", Prepare);
                var loaded = (IDictionary)typeof(ContentProjectDirectory).GetField("loaded", flags).GetValue(screen.Workspace.Directory);
                File.AppendAllText(report, new JObject { ["kind"] = "reads", ["levels"] = size, ["sample"] = sample,
                    ["listingBodyReads"] = 0, ["selectedAndReferenceBodies"] = loaded.Count,
                    ["ids"] = new JArray(loaded.Keys.Cast<string>()) }.ToString(Formatting.None) + "\n");
                screen.Workspace.Session.EditLevel("측정", body => body["displayName"] = "수정 " + sample);
                Measure("changed-level-save-other-levels-locked", screen.Workspace.Save);
            }
            finally
            {
                foreach (FileStream stream in locks.Values) stream.Dispose();
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
