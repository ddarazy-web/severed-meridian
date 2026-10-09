using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace LevelAuthoring.Editor
{
    public static partial class LegacyContentExporter
    {
        public static StoredContentSnapshot ExportCandidate(string candidateRoot) => ExportCandidate(candidateRoot, Discover(), null);

        internal static StoredContentSnapshot ExportCandidate(string candidateRoot, IReadOnlyList<LegacyContentSource> sources, Action beforePublish)
        {
            string root = Path.GetFullPath(candidateRoot);
            string allowed = Path.GetFullPath("ContentData/Candidates") + Path.DirectorySeparatorChar;
            if (!root.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                throw new ContentFormatException("이관 후보는 ContentData/Candidates의 명시적 하위 폴더에 만드세요.");
            // 저장소의 링크/경로 검사를 보고서 쓰기보다 먼저 수행한다.
            _ = new ContentSnapshotStore(root);
            EnsureCandidateWritable(root);
            var inventory = new JArray();
            foreach (LegacyContentSource source in sources)
            {
                string path = AssetDatabase.GetAssetPath(source.Asset);
                if (string.IsNullOrEmpty(path) || EditorUtility.IsDirty(source.Asset))
                    throw new ContentFormatException("저장된 제작 원본만 이관할 수 있습니다: " + path);
                var entry = new JObject { ["sourceGuid"] = source.Guid, ["kind"] = source.Kind, ["path"] = path,
                    ["sha256"] = SourceHash(path), ["metaSha256"] = SourceHash(path + ".meta") };
                if (source.Asset is Elements.ElementDefinitionAsset element)
                {
                    entry["planningDocument"] = element.PlanningDocument;
                    entry["planningSection"] = element.PlanningSection;
                }
                inventory.Add(entry);
            }
            StoredContentSnapshot result = ExportToRoot(root, sources, () =>
            {
                beforePublish?.Invoke();
                EnsureCandidateWritable(root);
                foreach (JObject entry in inventory)
                {
                    string path = (string)entry["path"];
                    if (SourceHash(path) != (string)entry["sha256"] || SourceHash(path + ".meta") != (string)entry["metaSha256"])
                        throw new ContentFormatException("이관 중 원본이 변경됐습니다. 후보를 공개하지 않습니다: " + path);
                }
                if (sources.Any(source => EditorUtility.IsDirty(source.Asset)))
                    throw new ContentFormatException("이관 중 원본에 미저장 변경이 생겼습니다.");
            });
            var ids = result.Snapshot.Project.Data["sourceIds"].ToDictionary(item => (string)item["sourceGuid"], item => (string)item["documentId"]);
            foreach (JObject entry in inventory) entry["documentId"] = ids[(string)entry["sourceGuid"]];
            var report = new JObject { ["version"] = 1, ["snapshotHash"] = result.Hash, ["projectId"] = result.Snapshot.Project.Id,
                ["createdUtc"] = DateTime.UtcNow.ToString("O"), ["sources"] = inventory,
                ["resources"] = result.Snapshot.Project.Data["resources"].DeepClone() };
            string reportPath = Path.Combine(root, "migration-report.json");
            string pending = reportPath + ".pending-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(pending, report.ToString());
                if (File.Exists(reportPath)) File.Replace(pending, reportPath, reportPath + ".previous");
                else File.Move(pending, reportPath);
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
            return result;
        }

        internal static string SourceHash(string path)
        {
            using var algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        private static void EnsureCandidateWritable(string root)
        {
            if (File.Exists(AuthoringSourceSelection.DefaultConfiguration) &&
                string.Equals(root.TrimEnd(Path.DirectorySeparatorChar), AuthoringSourceSelection.ReadFolder(AuthoringSourceSelection.DefaultConfiguration).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new ContentFormatException("채택된 JSON 원본에는 재이관할 수 없습니다. 새 후보 폴더를 사용하세요.");
            if (!File.Exists(Path.Combine(root, "project.json"))) return;
            string reportPath = Path.Combine(root, "migration-report.json");
            if (!File.Exists(reportPath) || (string)JObject.Parse(File.ReadAllText(reportPath))["snapshotHash"] != new ContentSnapshotStore(root).Read().Hash)
                throw new ContentFormatException("이관 이후 편집된 후보는 덮어쓰지 않습니다. 새 후보 폴더를 사용하세요.");
        }
    }
}
