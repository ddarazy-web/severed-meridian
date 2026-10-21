using System;
using System.Collections;
using System.IO;
using System.Linq;
using LevelAuthoring.Editing;
using UnityEditor;

namespace LevelAuthoring.Editor
{
    public static class LevelBrowserIndexVerification
    {
        public static void Run()
        {
            string report = Path.GetFullPath("Logs/LevelBrowser/index-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".txt");
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            try
            {
                Type type = typeof(AuthoringEditSession).Assembly.GetType("LevelAuthoring.Editing.LevelBrowserIndex");
                if (type == null) throw new Exception("Missing LevelBrowserIndex");
                object index = Activator.CreateInstance(type);
                var upsert = type.GetMethod("Upsert");
                foreach (var item in new[] { ("b", 2, "Rocket"), ("a", 1, "기본 매칭"), ("c", 12, "rocket combo"), ("d", 2, "폭탄") })
                    upsert.Invoke(index, new object[] { item.Item1, item.Item2, item.Item3 });
                string[] Find(string query)
                {
                    object result = type.GetMethod("Search").Invoke(index, new object[] { query });
                    return ((IEnumerable)result).Cast<object>().Select(row => (string)row.GetType().GetProperty("Id").GetValue(row)).ToArray();
                }
                void Check(string query, params string[] expected)
                {
                    if (!Find(query).SequenceEqual(expected)) throw new Exception("Unexpected result: " + query);
                }
                Check("", "a", "b", "d", "c");
                Check("2", "b", "d"); Check(" 1 ~ 2 ", "a", "b", "d");
                Check("ROCKET", "b", "c"); Check("매칭", "a"); Check("999");
                foreach (string invalid in new[] { "3~1", "1~", "~2", "1~2~3", "999999999999999" })
                {
                    Find(invalid);
                    if (string.IsNullOrEmpty((string)type.GetProperty("SearchError").GetValue(index)))
                        throw new Exception("Expected search error: " + invalid);
                }
                upsert.Invoke(index, new object[] { "b", 20, "변경" });
                Check("2", "d"); Check("변경", "b");
                type.GetMethod("Remove").Invoke(index, new object[] { "d" }); Check("2");
                File.WriteAllText(report, "PASS index exact/range/name/order/invalid/update/remove");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                File.WriteAllText(report, "FAIL " + error);
                UnityEngine.Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
    }
}
