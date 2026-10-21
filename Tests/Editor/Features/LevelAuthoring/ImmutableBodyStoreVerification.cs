using System;
using System.IO;
using System.Reflection;
using LevelAuthoring.Documents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace LevelAuthoring.Editor
{
    public static class ImmutableBodyStoreVerification
    {
        public static void Run()
        {
            string owned = Path.GetFullPath("Logs/ImmutableBodies/" + Guid.NewGuid().ToString("N"));
            string data = Path.Combine(owned, "data");
            Directory.CreateDirectory(data);
            IDisposable store = null;
            int exit = 1;
            try
            {
                Type type = typeof(ContentDocument).Assembly.GetType("LevelAuthoring.Storage.ImmutableContentBodyStore");
                Check(type != null, "Immutable body store is missing");
                store = (IDisposable)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new object[] { data }, null);
                MethodInfo capture = type.GetMethod("Capture", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var source = new ContentDocument("level", "isolation", new JObject { ["displayName"] = "original", ["nested"] = new JObject { ["value"] = 3 } });
                var captured = (ContentDocument)capture.Invoke(store, new object[] { source });
                string expectedBody = source.Data.ToString(Formatting.None);
                source.Data["displayName"] = "external change";
                JObject first = captured.Data;
                first["nested"]["value"] = 9;
                Check((string)captured.Data["displayName"] == "original", "Captured body depends on mutable source");
                Check((int)captured.Data["nested"]["value"] == 3, "A read changed immutable body");
                Check(!ReferenceEquals(first, captured.Data), "Reads expose shared mutable JSON");
                MethodInfo write = typeof(ContentDocument).GetMethod("WriteData", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(write != null, "Immutable body direct writer missing");
                using (var output = new StringWriter())
                using (var writer = new JsonTextWriter(output))
                {
                    write.Invoke(captured, new object[] { writer }); writer.Flush();
                    Check(output.ToString() == expectedBody, "Direct body write changed JSON");
                }
                MethodInfo deferred = typeof(ContentDocument).GetMethod("FromOwnedBody", BindingFlags.Static | BindingFlags.NonPublic);
                Check(deferred.GetParameters().Length == 3, "Deferred body writer contract missing");
                object info = typeof(ContentDocument).GetProperty("Info", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(captured);
                var noParse = (ContentDocument)deferred.Invoke(null, new object[] { info,
                    (Func<JObject>)(() => throw new Exception("Writer parsed body")),
                    (Action<JsonWriter>)(writer => writer.WriteRawValue(expectedBody)) });
                using (var output = new StringWriter())
                using (var writer = new JsonTextWriter(output)) { write.Invoke(noParse, new object[] { writer }); writer.Flush(); Check(output.ToString() == expectedBody, "Direct writer output mismatch"); }
                Check(Directory.GetFiles(data).Length == 1, "Bodies must share one owned backing file");
                for (int index = 0; index < 100; index++) capture.Invoke(store, new object[] { source });
                Check(Directory.GetFiles(data).Length == 1, "Per-document files created");
                store.Dispose(); store = null;
                Check(Directory.GetFiles(data).Length == 0, "Backing file not cleaned after release");
                bool closed = false;
                try { _ = captured.Data; } catch (ObjectDisposedException) { closed = true; }
                Check(closed, "Closed store silently returned different data");
                File.WriteAllText(Path.Combine(owned, "result.txt"), "PASS: source isolation, read isolation, shared backing file, disposal, no fallback");
                exit = 0;
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(owned, "result.txt"), error.ToString()); UnityEngine.Debug.LogException(error); }
            finally
            {
                store?.Dispose();
                // 이 검사에서 만든 사본만 제거하고 검증 결과는 보존한다.
                Directory.Delete(data, true);
                EditorApplication.Exit(exit);
            }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
