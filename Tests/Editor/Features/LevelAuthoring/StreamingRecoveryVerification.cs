using System;
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

namespace LevelAuthoring.Editor
{
    public static class StreamingRecoveryVerification
    {
        private sealed class IncrementalReader : StringReader
        {
            internal IncrementalReader(string value) : base(value) { }
            public override string ReadToEnd() => throw new Exception("Recovery called ReadToEnd");
            public override int Read(char[] buffer, int index, int count) => base.Read(buffer, index, Math.Min(count, 31));
        }
        public static void Run()
        {
            string owned = Path.GetFullPath("Logs/StreamingRecovery/" + Guid.NewGuid().ToString("N"));
            string data = Path.Combine(owned, "data"); Directory.CreateDirectory(owned);
            int exit = 1;
            try
            {
                var workspace = new AuthoringToolWorkspace(); workspace.Create(data, LevelToolDocuments.NewProject());
                AuthoringEditSession source = workspace.Session;
                source.EditLevel("first", level => level["displayName"] = "첫 변경");
                source.EditLevel("unfinished", level => level["moveCount"] = -1);
                source.Undo(); source.SelectCells(new[] { 1, 2 });
                string expected = source.ExportState();
                MethodInfo restore = typeof(AuthoringEditSession).GetMethod("Restore", new[] { typeof(TextReader) });
                Check(restore != null, "Streaming session recovery API missing");
                AuthoringEditSession recovered;
                using (var reader = new IncrementalReader(expected)) recovered = (AuthoringEditSession)restore.Invoke(null, new object[] { reader });
                Check(recovered.ExportState() == expected, "v1 state changed during streamed recovery");
                object state = typeof(AuthoringEditSession).GetField("current", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(recovered);
                var docs = (Dictionary<string, ContentDocument>)state.GetType().GetField("Documents", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
                Check(typeof(ContentDocument).GetField("readBody", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(docs[recovered.SelectedLevelId]) != null,
                    "Recovery retained materialized level body");
                source.Redo(); recovered.Redo();
                Check(source.ExportState() == recovered.ExportState(), "Unfinished draft or Redo lost");
                source.Undo(); recovered.Undo();
                Check(source.ExportState() == recovered.ExportState(), "Undo history changed");
                JObject root = JObject.Parse(expected);
                string reordered = new JObject(root.Properties().Reverse().Select(property => new JProperty(property.Name, property.Value.DeepClone()))).ToString();
                using (var reader = new IncrementalReader(reordered))
                    Check(((AuthoringEditSession)restore.Invoke(null, new object[] { reader })).ExportState() == expected, "Field order became mandatory");
                bool rejected = false;
                try { using var reader = new IncrementalReader(expected.Substring(0, expected.Length - 3)); restore.Invoke(null, new object[] { reader }); }
                catch (TargetInvocationException error) { rejected = error.InnerException is ContentFormatException; }
                Check(rejected, "Truncated recovery accepted");
                MethodInfo write = typeof(AuthoringEditSession).GetMethod("WriteState", new[] { typeof(TextWriter) });
                Check(write != null, "Streaming session export API missing");
                using (var output = new StringWriter())
                {
                    write.Invoke(source, new object[] { output });
                    Check(output.ToString() == source.ExportState(), "Stream export changed v1 bytes");
                }
                MethodInfo restoreWorkspace = typeof(AuthoringToolWorkspace).GetMethod("RestoreState", new[] { typeof(TextReader) });
                Check(restoreWorkspace != null, "Streaming workspace recovery API missing");
                string expectedWorkspace = workspace.ExportState();
                MethodInfo writeWorkspace = typeof(AuthoringToolWorkspace).GetMethod("WriteState", new[] { typeof(TextWriter) });
                Check(writeWorkspace != null, "Streaming workspace write API missing");
                using (var output = new StringWriter())
                {
                    writeWorkspace.Invoke(workspace, new object[] { output });
                    Check(output.ToString() == expectedWorkspace, "Workspace streaming write changed v1 bytes");
                }
                var target = new AuthoringToolWorkspace();
                using (var reader = new IncrementalReader(expectedWorkspace)) restoreWorkspace.Invoke(target, new object[] { reader });
                Check(target.ExportState() == expectedWorkspace, "Workspace stream recovery differs");
                rejected = false;
                try { using var reader = new IncrementalReader(expectedWorkspace.Substring(0, expectedWorkspace.Length - 3)); restoreWorkspace.Invoke(target, new object[] { reader }); }
                catch (TargetInvocationException error) { rejected = error.InnerException is ContentFormatException; }
                Check(rejected && target.ExportState() == expectedWorkspace, "Failed recovery replaced active workspace");
                // 같은 이력 본문은 보관소에서 다시 해석하지 않고 같은 버전을 재사용해야 한다.
                MethodInfo readDocuments = typeof(AuthoringEditSession).GetMethod("ReadDocuments", BindingFlags.Static | BindingFlags.NonPublic);
                object pool = Activator.CreateInstance(readDocuments.GetParameters()[1].ParameterType);
                Type bodiesType = typeof(ContentDocument).Assembly.GetType("LevelAuthoring.Storage.ImmutableContentBodyStore");
                using (var bodies = (IDisposable)Activator.CreateInstance(bodiesType, BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object[] { Path.Combine(data, "bodies") }, null))
                {
                    JToken entries = root["current"]["documents"];
                    Dictionary<string, ContentDocument> first;
                    using (var reader = entries.CreateReader())
                    { reader.Read(); first = (Dictionary<string, ContentDocument>)readDocuments.Invoke(null, new object[] { reader, pool, bodies }); }
                    var reorderedEntries = (JArray)entries.DeepClone();
                    foreach (JObject entry in reorderedEntries)
                        entry["data"] = new JObject(((JObject)entry["data"]).Properties().Reverse()
                            .Select(property => new JProperty(property.Name, property.Value.DeepClone())));
                    using (var reader = reorderedEntries.CreateReader())
                    {
                        reader.Read();
                        var equivalent = (Dictionary<string, ContentDocument>)readDocuments.Invoke(null, new object[] { reader, pool, bodies });
                        Check(first.All(pair => ReferenceEquals(pair.Value, equivalent[pair.Key])), "Reordered equivalent body lost version sharing");
                    }
                    foreach (ContentDocument document in first.Values.Where(value => value.Kind == "level"))
                        typeof(ContentDocument).GetField("readBody", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(document,
                            (Func<JObject>)(() => throw new Exception("Repeated history parsed archived body")));
                    using (var reader = entries.CreateReader())
                    {
                        reader.Read();
                        var repeated = (Dictionary<string, ContentDocument>)readDocuments.Invoke(null, new object[] { reader, pool, bodies });
                        Check(first.All(pair => ReferenceEquals(pair.Value, repeated[pair.Key])), "Repeated history version was duplicated");
                    }
                }
                string lazyFolder = Path.Combine(data, "lazy");
                var lazyFixture = new AuthoringToolWorkspace(); lazyFixture.Create(lazyFolder, LevelToolDocuments.NewProject());
                string firstId = lazyFixture.Session.SelectedLevelId;
                string secondId = DocumentEditing.DuplicateLevel(lazyFixture.Session, firstId, 2, "미확인 복구"); lazyFixture.Save();
                foreach (string index in Directory.GetFiles(Path.Combine(lazyFolder, ".authoring", "listings"), "*.json")) File.Delete(index);
                var lazy = new AuthoringToolWorkspace(); lazy.OpenListing(lazyFolder, "Cancel"); lazy.OpenLevel(firstId);
                Check(lazy.Session.GetDocumentInfo(secondId).Kind == "unknown", "Lazy recovery fixture loaded unselected document");
                lazy.Session.EditLevel("복구 초안", body => body["displayName"] = "recover and save");
                string lazyState = LegacyWorkspace(lazy);
                var loadedBodies = (Dictionary<string, ContentDocument>)typeof(ContentProjectDirectory)
                    .GetField("loaded", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(lazy.Directory);
                foreach (ContentDocument document in loadedBodies.Values)
                    typeof(ContentDocument).GetField("readBody", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(document,
                        (Func<JObject>)(() => throw new Exception("Repeated recovery parsed a loaded reference body")));
                Check(LegacyWorkspace(lazy) == lazyState, "Repeated lazy recovery changed v1 contents");
                using (var directory = new ContentSnapshotStore(lazyFolder).ReadDirectory())
                    File.Delete(Path.Combine(lazyFolder, directory.Entries.Single(entry => entry.Id == secondId).RelativePath));
                var lazyRestored = new AuthoringToolWorkspace(); lazyRestored.RestoreState(lazyState);
                Check(lazyRestored.Session.GetDocumentInfo(secondId).Kind == "level", "Recovery wrote unknown document kind");
                Check(lazyRestored.Session.IsDirty, "Recovery lost lazy dirty state");
                lazyRestored.Save(); Check(!lazyRestored.Session.IsDirty, "Restored lazy session cannot save");
                string recoveredState = LegacyWorkspace(lazyRestored);
                using (var directory = new ContentSnapshotStore(lazyFolder).ReadDirectory())
                {
                    var files = new JsonContentStore(lazyFolder);
                    string relative = directory.Entries.Single(entry => entry.Id == firstId).RelativePath;
                    ContentFile disk = files.Read(relative); disk.Document.Data["displayName"] = "external after recovery";
                    files.Save(disk.Document, relative, disk.Hash);
                }
                var conflict = new AuthoringToolWorkspace(); conflict.RestoreState(recoveredState);
                conflict.Session.EditLevel("외부 변경 보호", body => body["displayName"] = "must not overwrite");
                rejected = false;
                try { conflict.Save(); } catch (ContentFormatException) { rejected = true; }
                Check(rejected && conflict.Session.IsDirty, "Restored save overwrote external document change");
                File.WriteAllText(Path.Combine(owned, "result.txt"), "PASS v1 streamed recovery, lazy body, unfinished draft, selection/Undo/Redo, field order, truncation; unopened legacy document and restored save"); exit = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(owned, "result.txt"), error.ToString()); UnityEngine.Debug.LogException(error); }
            finally { if (Directory.Exists(data)) Directory.Delete(data, true); EditorApplication.Exit(exit); }
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static string LegacyWorkspace(AuthoringToolWorkspace workspace)
        {
            using var text = new StringWriter();
            using var writer = new Newtonsoft.Json.JsonTextWriter(text);
            typeof(AuthoringEditSession).GetMethod("WriteLegacyState", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(workspace.Session, new object[] { writer });
            writer.Flush();
            return new JObject { ["version"] = 1, ["folder"] = workspace.Folder, ["recovery"] = workspace.IsRecovery,
                ["session"] = JObject.Parse(text.ToString()) }.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
