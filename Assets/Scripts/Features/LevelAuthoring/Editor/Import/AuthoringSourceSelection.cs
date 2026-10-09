using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace LevelAuthoring.Editor
{
    // 제작 원본 선택은 Assets 밖에 보관한다. 실패한 후보 때문에 기존 선택을 잃지 않는다.
    public static class AuthoringSourceSelection
    {
        public const string DefaultConfiguration = "ProjectSettings/AuthoringSource.json";

        public static StoredContentSnapshot ValidateCandidate(string folder)
        {
            var stored = new ContentSnapshotStore(Path.GetFullPath(folder)).Read();
            var report = JObject.Parse(File.ReadAllText(Path.Combine(folder, "migration-report.json")));
            if ((int?)report["version"] != 1 || (string)report["snapshotHash"] != stored.Hash || (string)report["projectId"] != stored.Snapshot.Project.Id)
                throw new ContentFormatException("후보 이관 보고서와 현재 JSON이 다릅니다. 다시 이관·검증하세요.");
            var sources = LegacyContentExporter.Discover();
            var entries = (JArray)report["sources"] ?? throw new ContentFormatException("원본 이관 목록이 없습니다.");
            if (sources.Length != entries.Count || entries.Select(entry => (string)entry["sourceGuid"]).Distinct().Count() != entries.Count)
                throw new ContentFormatException("이관 목록의 원본 수 또는 GUID가 다릅니다.");
            var ids = stored.Snapshot.Project.Data["sourceIds"].ToDictionary(entry => (string)entry["sourceGuid"], entry => (string)entry["documentId"]);
            var resources = stored.Snapshot.Project.Data["resources"].ToDictionary(entry => (string)entry["path"], entry => (string)entry["id"]);
            var atlasSprites = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (string path in Levels.Editor.BoardAtlasPrebuild.SourcePaths())
            {
                string address = Levels.Editor.BoardAtlasPrebuild.AddressForPath(path);
                var atlas = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(Levels.Editor.BoardAtlasPrebuild.AtlasPath(address));
                if (atlas == null) continue;
                var texture = AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(path);
                if (!UnityEditor.U2D.SpriteAtlasExtensions.GetPackables(atlas).Contains(texture)) continue;
                foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<UnityEngine.Sprite>())
                    atlasSprites.Add(address + "|" + sprite.name);
            }
            foreach (string resource in resources.Keys)
                if (AssetDatabase.LoadMainAssetAtPath(resource) == null &&
                    !atlasSprites.Contains(Board.BoardSpriteAtlas.AddressFor(resource) + "|" + Path.GetFileName(resource)))
                    throw new ContentFormatException("후보 표현 리소스가 없습니다: " + resource);
            foreach (LegacyContentSource source in sources)
            {
                JObject entry = entries.OfType<JObject>().SingleOrDefault(item => (string)item["sourceGuid"] == source.Guid);
                string path = AssetDatabase.GetAssetPath(source.Asset);
                if (entry == null || (string)entry["path"] != path || (string)entry["kind"] != source.Kind || EditorUtility.IsDirty(source.Asset) ||
                    (string)entry["sha256"] != LegacyContentExporter.SourceHash(path) || (string)entry["metaSha256"] != LegacyContentExporter.SourceHash(path + ".meta"))
                    throw new ContentFormatException("후보 작성 후 원본이 바뀌었습니다: " + path);
                if (!ids.TryGetValue(source.Guid, out string id) || id != (string)entry["documentId"])
                    throw new ContentFormatException("원본의 문서 ID 연결이 다릅니다: " + path);
                ContentDocument expected = UnityAuthoringCodec.Write(source.Asset, source.Kind, id,
                    value => ids[AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value))], value => resources[value]);
                ContentDocument actual = stored.Snapshot.Get(id);
                // 메모리의 float와 JSON에서 읽은 double을 직접 비교하지 않고 같은 저장 계약으로 대조한다.
                if (ContentJson.Write(actual) != ContentJson.Write(expected))
                    throw new ContentFormatException("이관 후보의 필드가 원본과 다릅니다: " + path);
                if (source.Asset is Elements.ElementCatalogAsset catalog) Elements.Editor.ElementContentAuthoring.ValidatePlanning(catalog);
            }
            if (stored.Snapshot.Documents.Length != sources.Length + 1)
                throw new ContentFormatException("원본 목록과 후보 문서 수가 다릅니다.");
            return stored;
        }

        public static void Adopt(string folder, string configurationPath)
        {
            folder = Path.GetFullPath(folder); configurationPath = Path.GetFullPath(configurationPath);
            StoredContentSnapshot candidate = ValidateCandidate(folder);
            string directory = Path.GetDirectoryName(configurationPath);
            _ = new ContentSnapshotStore(directory);
            if (File.Exists(configurationPath) && (File.GetAttributes(configurationPath) & FileAttributes.ReparsePoint) != 0)
                throw new ContentFormatException("제작 원본 설정에 링크 파일을 사용할 수 없습니다.");
            string previous = File.Exists(configurationPath) ? File.ReadAllText(configurationPath) : null;
            var data = new JObject { ["version"] = 1, ["folder"] = Path.GetRelativePath(directory, folder).Replace('\\', '/'),
                ["projectId"] = candidate.Snapshot.Project.Id, ["adoptedHash"] = candidate.Hash };
            Directory.CreateDirectory(directory);
            string pending = configurationPath + ".pending-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(pending, data.ToString());
                if (new ContentSnapshotStore(folder).Read().Hash != candidate.Hash ||
                    (File.Exists(configurationPath) ? File.ReadAllText(configurationPath) : null) != previous)
                    throw new ContentFormatException("채택 중 후보 또는 기존 선택이 변경됐습니다.");
                if (previous == null) File.Move(pending, configurationPath);
                else File.Replace(pending, configurationPath, configurationPath + ".previous");
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }

        public static string ReadFolder(string configurationPath)
        {
            configurationPath = Path.GetFullPath(configurationPath);
            var data = JObject.Parse(File.ReadAllText(configurationPath));
            if ((int?)data["version"] != 1 || string.IsNullOrWhiteSpace((string)data["folder"]))
                throw new ContentFormatException("제작 원본 선택 형식이 잘못됐습니다.");
            string folder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(configurationPath), (string)data["folder"]));
            if (new ContentSnapshotStore(folder).Read().Snapshot.Project.Id != (string)data["projectId"])
                throw new ContentFormatException("선택한 폴더의 프로젝트 ID가 바뀌었습니다. 원본을 다시 선택하세요.");
            return folder;
        }
    }
}
