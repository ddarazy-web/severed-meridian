using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace LevelAuthoring.Editor
{
    public static class StreamingPublicationVerification
    {
        private const BindingFlags Internal = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
        public static void Run()
        {
            string owned = Path.GetFullPath("Logs/StreamingPublication/" + Guid.NewGuid().ToString("N"));
            string data = Path.Combine(owned, "data"); Directory.CreateDirectory(owned);
            int exit = 1;
            try
            {
                ContentSnapshot template = LevelToolDocuments.NewProject();
                var documents = template.Documents.ToList();
                ContentDocument original = documents.Single(doc => doc.Kind == "level");
                for (int number = 2; number <= 4; number++)
                {
                    JObject body = (JObject)original.Data.DeepClone(); body["levelNumber"] = number;
                    documents.Add(new ContentDocument("level", "stream-level-" + number, body));
                }
                int reads = 0, written = 0;
                var delayed = documents.Select(doc => doc.Kind != "level" ? doc :
                    (ContentDocument)typeof(ContentDocument).GetMethod("FromOwnedBody", Internal).Invoke(null, new object[] {
                        typeof(ContentDocument).GetProperty("Info", Internal).GetValue(doc),
                        (Func<JObject>)(() => { reads++; return (JObject)doc.Data.DeepClone(); }), null })).ToArray();
                var snapshot = (ContentSnapshot)typeof(ContentSnapshot).GetMethod("FromOwnedDocuments", Internal).Invoke(null, new object[] { delayed });
                Check(reads <= documents.Count(doc => doc.Kind == "level") * 2, "Reference validation reread each level unnecessarily");
                string expectedFingerprint = LegacyFingerprint(snapshot);
                Check(ContentSnapshotFingerprint.Compute(snapshot) == expectedFingerprint, "Fingerprint format changed");
                reads = 0;
                MethodInfo infoReader = typeof(ContentSnapshot).GetMethod("GetDocumentInfo");
                Check(infoReader != null, "Snapshot metadata reader missing");
                foreach (ContentDocument doc in delayed) infoReader.Invoke(snapshot, new object[] { doc.Id });
                Check(reads == 0, "Metadata query read level bodies");
                ContentSnapshotStore store = Instrumented(data, phase =>
                {
                    if (phase == "BeforeDocuments") Check(reads == 0, "Publish read every body before writing began");
                    if (phase == "AfterDocument") { written++; Check(reads <= written, "Publish eagerly read later bodies"); }
                });
                StoredContentSnapshot saved = store.Publish(snapshot, null);
                Check(written == documents.Count - 1, "Not every member was published");
                Check(new ContentSnapshotStore(data).Read().Hash == saved.Hash, "Published revision mismatch");
                Check(ContentSnapshotFingerprint.Compute(saved.Snapshot) == expectedFingerprint, "Generation changed content fingerprint");
                string manifestPath = Path.Combine(data, "project.json");
                string manifest = File.ReadAllText(manifestPath);
                string member = Path.Combine(data, (string)saved.Snapshot.Project.Data["documents"][0]["path"]);
                string before = File.ReadAllText(member);
                File.AppendAllText(member, "\n");
                bool conflict = false;
                try { new ContentSnapshotStore(data).Publish(snapshot, saved.Hash); } catch (ContentFormatException) { conflict = true; }
                Check(conflict && File.ReadAllText(manifestPath) == manifest && File.ReadAllText(member) == before + "\n", "External change overwritten");
                File.WriteAllText(member, before, new UTF8Encoding(false));
                bool interrupted = false;
                try { Instrumented(data, phase => { if (phase == "AfterDocument") throw new InvalidOperationException("injected"); }).Publish(snapshot, saved.Hash); }
                catch (InvalidOperationException) { interrupted = true; }
                Check(interrupted && File.ReadAllText(manifestPath) == manifest && new ContentSnapshotStore(data).Read().Hash == saved.Hash, "Interrupted save changed public snapshot");
                var invalid = snapshot.Documents.ToList();
                invalid.First(doc => doc.Kind == "level").Data["catalogId"] = "broken-catalog";
                invalid.Add(new ContentDocument("catalog", "broken-catalog", new JObject {
                    ["displayName"] = "Broken", ["definitionIds"] = new JArray("missing-element"), ["visualId"] = null }));
                bool referencesRejected = false;
                try { new ContentSnapshot(invalid); } catch (ContentFormatException) { referencesRejected = true; }
                Check(referencesRejected, "Missing shared member was not reported as content error");
                File.WriteAllText(Path.Combine(owned, "result.txt"), "PASS demand-driven write, identical fingerprint/revision, external conflict, interrupted publication"); exit = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(owned, "result.txt"), error.ToString()); UnityEngine.Debug.LogException(error); }
            finally { if (Directory.Exists(data)) Directory.Delete(data, true); EditorApplication.Exit(exit); }
        }
        private static ContentSnapshotStore Instrumented(string path, Action<string> checkpoint)
        {
            ConstructorInfo constructor = typeof(ContentSnapshotStore).GetConstructors(Internal).Single();
            Type delegateType = constructor.GetParameters()[1].ParameterType;
            ParameterExpression parameter = Expression.Parameter(delegateType.GenericTypeArguments[0]);
            Delegate callback = Expression.Lambda(delegateType, Expression.Invoke(Expression.Constant(checkpoint),
                Expression.Call(parameter, typeof(object).GetMethod("ToString"))), parameter).Compile();
            return (ContentSnapshotStore)constructor.Invoke(new object[] { path, callback });
        }
        private static string LegacyFingerprint(ContentSnapshot snapshot)
        {
            ContentDocument[] docs = snapshot.Documents;
            docs.Single(doc => doc.Kind == "project").Data["documents"] = new JArray();
            byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\n", docs.OrderBy(doc => doc.Id, StringComparer.Ordinal).Select(ContentJson.Write)));
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
