using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Storage;
using Levels;
using Levels.Editor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelAuthoring.Editor
{
    // 승인된 6단계 전환을 한 번 실행하고, 재실행은 기존 JSON을 덮지 않고 확인만 한다.
    public static class ProductionJsonAdoption
    {
        public static void Run()
        {
            try
            {
                const string folder = "ContentData/Candidates/main";
                const string backup = "ContentData/Backups/game-authoring-stage-06/before-adoption.json";
                if (!File.Exists(backup))
                {
                    string[] paths = new[] { "Assets/AddressableAssetsData", "Assets/Data/LevelPacks", "Assets/Data/ElementPacks" }
                        .SelectMany(root => Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                        .Concat(new[] { AuthoringSourceSelection.DefaultConfiguration, AuthoringSourceSelection.DefaultConfiguration + ".previous", JsonContentPackBuild.GenerationPath, JsonContentPackBuild.GenerationPath + ".meta" })
                        .Select(path => path.Replace('\\', '/')).Distinct().ToArray();
                    var entries = new JArray(paths.Select(path => new JObject { ["path"] = path,
                        ["bytes"] = File.Exists(path) ? Convert.ToBase64String(File.ReadAllBytes(path)) : null }));
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    File.WriteAllText(backup, new JObject { ["version"] = 1, ["files"] = entries }.ToString());
                }
                if (!File.Exists(Path.Combine(folder, "project.json"))) LegacyContentExporter.ExportCandidate(folder);
                if (!File.Exists(AuthoringSourceSelection.DefaultConfiguration))
                    AuthoringSourceSelection.Adopt(folder, AuthoringSourceSelection.DefaultConfiguration);
                string selected = AuthoringSourceSelection.ReadFolder(AuthoringSourceSelection.DefaultConfiguration);
                if (!string.Equals(selected, Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("다른 제작 원본이 선택되어 있습니다. 자동 교체하지 않습니다.");
                LevelPackBuild.Generate();
                var source = new ContentSnapshotStore(selected).Read();
                var expected = JsonContentPackBuild.CreateBytes(source.Snapshot);
                foreach (var pair in expected)
                    if (!File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)) throw new Exception("생성 바이트 불일치: " + pair.Key);
                Debug.Log("PASS production JSON adopted and registered: " + source.Snapshot.Documents.Length + " documents, " + expected.Count + " outputs");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
