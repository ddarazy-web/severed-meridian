using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LevelAuthoring.Editing;
using UnityEditor;

namespace LevelAuthoring.Editor
{
    public static class LevelBrowserPerformanceVerification
    {
        public static void Run()
        {
            string folder = Path.GetFullPath("Logs/LevelBrowser");
            Directory.CreateDirectory(folder);
            var report = new StringBuilder("Metadata index only; excludes workspace document loading and debounce.\n");
            try
            {
                foreach (int count in new[] { 1000, 10000 })
                {
                    for (int run = 0; run < 4; run++)
                    {
                        var index = new LevelBrowserIndex();
                        var timer = Stopwatch.StartNew();
                        for (int i = 1; i <= count; i++) index.Upsert("level-" + i, i, "튜토리얼 " + (i % 2 == 0 ? "로켓 " : "폭탄 ") + i);
                        index.Search("");
                        double prepare = timer.Elapsed.TotalMilliseconds;
                        double[] samples = new double[100];
                        for (int i = 0; i < samples.Length; i++)
                        {
                            timer.Restart();
                            index.Search(i % 3 == 0 ? "500" : i % 3 == 1 ? "100~800" : "로켓");
                            samples[i] = timer.Elapsed.TotalMilliseconds;
                        }
                        Array.Sort(samples);
                        if (index.SortBuildCount != 1) throw new Exception("Searching rebuilt sorted index");
                        long revision = index.Revision;
                        if (index.Upsert("level-2", 2, "튜토리얼 로켓 2") || index.Revision != revision)
                            throw new Exception("Unchanged metadata invalidates index");
                        if (run == 0) continue;
                        report.AppendLine($"count={count} run={run} prepare_ms={prepare:F3} median_ms={samples[50]:F3} p95_ms={samples[94]:F3} max_ms={samples[99]:F3}");
                        if (samples[50] > 200) throw new Exception("Search median exceeds 200ms");
                    }
                }
                report.AppendLine("PASS metadata search budget and no redundant rebuild");
                File.WriteAllText(Path.Combine(folder, "performance-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".txt"), report.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                report.AppendLine("FAIL " + error);
                File.WriteAllText(Path.Combine(folder, "performance-failure-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".txt"), report.ToString());
                UnityEngine.Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
    }
}
