using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace LevelAuthoring.Editor
{
    public static class DocumentOwnershipVerification
    {
        public static void Run()
        {
            int exit = 1;
            string report = Path.GetFullPath("Logs/DocumentOwnership-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                var detached = new JObject { ["displayName"] = "owned" };
                var template = new ContentDocument("level", "owned", detached);
                const BindingFlags internalStatic = BindingFlags.Static | BindingFlags.NonPublic;
                const BindingFlags internalInstance = BindingFlags.Instance | BindingFlags.NonPublic;
                object metadata = typeof(ContentDocument).GetProperty("Info", internalInstance).GetValue(template);
                var lazy = (ContentDocument)typeof(ContentDocument).GetMethod("FromOwnedBody", internalStatic).Invoke(null,
                    new object[] { metadata, (Func<JObject>)(() => detached), null });
                var materialized = (ContentDocument)typeof(ContentDocument).GetMethod("Materialize", internalInstance).Invoke(lazy, null);
                Check(ReferenceEquals(materialized.Data, detached), "Fresh private body was copied again");
                MethodInfo copy = typeof(ContentDocument).GetMethod("Copy", internalInstance);
                Check(copy != null, "Document copy boundary missing");
                var ordinaryCopy = (ContentDocument)copy.Invoke(template, null);
                ordinaryCopy.Data["displayName"] = "copy";
                Check((string)template.Data["displayName"] == "owned", "Materialized public copy is shared");
                JObject selection = JObject.Parse(File.ReadAllText("ProjectSettings/AuthoringSource.json"));
                string folder = Path.GetFullPath(Path.Combine("ProjectSettings", (string)selection["folder"]));
                StoredContentSnapshot stored = new ContentSnapshotStore(folder).Read();
                ContentDocument level = stored.Snapshot.Documents.First(doc => doc.Kind == "level");
                string name = (string)level.Data["displayName"];
                var first = new AuthoringEditSession(stored, level.Id);
                var second = new AuthoringEditSession(stored, level.Id);
                // 내부 참조의 공유 여부와 외부 변경 격리를 함께 검사한다.
                object firstState = Field(first, "current");
                var firstDocuments = (Dictionary<string, ContentDocument>)Field(firstState, "Documents");
                var secondDocuments = (Dictionary<string, ContentDocument>)Field(Field(second, "current"), "Documents");
                var snapshotDocuments = (Dictionary<string, ContentDocument>)Field(stored.Snapshot, "documents");
                Check(Field(snapshotDocuments[level.Id], "readBody") != null, "Store retained eagerly materialized level body");
                Check(ReferenceEquals(firstDocuments[level.Id], snapshotDocuments[level.Id]), "Session duplicated immutable snapshot body");
                Check(ReferenceEquals(firstDocuments[level.Id], secondDocuments[level.Id]), "Sessions duplicated immutable body");
                first.Get(level.Id).Data["displayName"] = "outside";
                stored.Snapshot.Get(level.Id).Data["displayName"] = "outside snapshot";
                Check((string)first.Get(level.Id).Data["displayName"] == name, "Mutable query leaked into session");
                first.EditLevel("rename", data => data["displayName"] = "changed");
                Check((string)second.Get(level.Id).Data["displayName"] == name, "Edit leaked into second session");
                Check((string)stored.Snapshot.Get(level.Id).Data["displayName"] == name, "Edit changed save baseline");
                first.Undo();
                Check((string)first.Get(level.Id).Data["displayName"] == name, "Undo lost original body");
                first.Redo();
                Check((string)first.Get(level.Id).Data["displayName"] == "changed", "Redo lost edited body");
                ContentSnapshot editedSnapshot = first.CreateSnapshot();
                var editedDocuments = (Dictionary<string, ContentDocument>)Field(editedSnapshot, "documents");
                var currentDocuments = (Dictionary<string, ContentDocument>)Field(Field(first, "current"), "Documents");
                Check(ReferenceEquals(editedDocuments[level.Id], currentDocuments[level.Id]), "CreateSnapshot duplicated published body");
                first.EditLevel("rename again", data => data["displayName"] = "next");
                Check((string)editedSnapshot.Get(level.Id).Data["displayName"] == "changed", "Later edit changed captured snapshot");
                ContentDocument[] publicCopies = editedSnapshot.Documents;
                var fromPublic = new ContentSnapshot(publicCopies);
                publicCopies.First(doc => doc.Id == level.Id).Data["displayName"] = "outside constructor";
                Check((string)fromPublic.Get(level.Id).Data["displayName"] == "changed", "Public constructor retained mutable input");
                File.WriteAllText(report, "PASS: immutable sharing, public copy isolation, session isolation, Undo/Redo");
                exit = 0;
            }
            catch (Exception error) { File.WriteAllText(report, error.ToString()); UnityEngine.Debug.LogException(error); }
            finally { EditorApplication.Exit(exit); }
        }

        private static object Field(object owner, string name) => owner.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(owner);
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
