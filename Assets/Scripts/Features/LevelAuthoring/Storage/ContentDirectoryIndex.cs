#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LevelAuthoring.Documents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Storage
{
    // 게임 JSON과 분리된 목록이다. manifest 해시에 묶어 오래된 세대의 목록을 섞지 않는다.
    internal static class ContentDirectoryIndex
    {
        private static string RelativePath(string hash) => ".authoring/listings/" + hash + ".json";
        internal static JObject Entry(ContentDocumentInfo info, string path, string hash) => new JObject
        {
            ["id"] = info.Id, ["path"] = path, ["kind"] = info.Kind, ["number"] = info.LevelNumber,
            ["levelVersion"] = info.LevelSchemaVersion, ["name"] = info.DisplayName, ["hash"] = hash
        };
        internal static JObject Entry(ProjectDocumentEntry entry) => Entry(entry.Info ??
            new ContentDocumentInfo(entry.Id, "unknown", 0, 0, Path.GetFileName(entry.RelativePath)), entry.RelativePath, entry.ContentHash);

        internal static void Write(JsonContentStore store, string manifestHash, JArray entries)
        {
            string path = store.Resolve(RelativePath(manifestHash));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = Path.Combine(Path.GetDirectoryName(path), ".pending-" + Guid.NewGuid().ToString("N"));
            try
            {
                var index = new JObject { ["version"] = 1, ["manifestHash"] = manifestHash, ["entries"] = entries };
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false, true).GetBytes(index.ToString(Formatting.None));
                    file.Write(bytes, 0, bytes.Length); file.Flush(true);
                }
                store.Resolve(RelativePath(manifestHash));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        internal static string Apply(JsonContentStore store, string manifestHash, Dictionary<string, ProjectDocumentEntry> entries)
        {
            string path = store.Resolve(RelativePath(manifestHash));
            if (!File.Exists(path)) return "목록 정보가 없습니다. 파일 식별자로 표시합니다.";
            try
            {
                JObject index = JObject.Parse(File.ReadAllText(path, new UTF8Encoding(false, true)));
                if ((int?)index["version"] != 1 || (string)index["manifestHash"] != manifestHash ||
                    !(index["entries"] is JArray records) || records.Count != entries.Count)
                    throw new ContentFormatException("현재 프로젝트와 목록 정보가 맞지 않습니다.");
                var parsed = new Dictionary<string, (ContentDocumentInfo Info, string Hash)>(StringComparer.Ordinal);
                foreach (JObject record in records)
                {
                    string id = (string)record["id"], kind = (string)record["kind"], hash = (string)record["hash"];
                    if (id == null || !entries.TryGetValue(id, out ProjectDocumentEntry entry) || (string)record["path"] != entry.RelativePath ||
                        string.IsNullOrWhiteSpace(kind) || kind == "project" ||
                        (hash == null ? kind != "unknown" : hash.Length != 64 || hash.Any(value => !(value >= '0' && value <= '9') && !(value >= 'a' && value <= 'f'))) ||
                        record["number"]?.Type != JTokenType.Integer || record["levelVersion"]?.Type != JTokenType.Integer || record["name"]?.Type != JTokenType.String)
                        throw new ContentFormatException("목록 항목이 잘못됐습니다.");
                    int number = (int)record["number"], version = (int)record["levelVersion"];
                    if (kind == "level" && (number < 1 || version < 1)) throw new ContentFormatException("목록의 레벨 번호/버전이 잘못됐습니다.");
                    if (kind == "unknown" && (number != 0 || version != 0)) throw new ContentFormatException("미확인 문서의 목록 값이 잘못됐습니다.");
                    parsed.Add(id, (kind == "unknown" ? null : new ContentDocumentInfo(id, kind, number, version, (string)record["name"]), hash));
                }
                foreach (var pair in parsed)
                { entries[pair.Key].Info = pair.Value.Info; entries[pair.Key].ContentHash = pair.Value.Hash; }
                return parsed.Values.Any(value => value.Info == null) ? "일부 문서의 목록 정보가 없습니다. 파일 식별자로 표시합니다." : null;
            }
            catch (Exception error) when (error is JsonException || error is ContentFormatException || error is ArgumentException ||
                error is InvalidCastException || error is FormatException || error is OverflowException)
            { return "목록 정보 오류 · 파일 식별자로 표시합니다: " + error.Message; }
        }
    }
}
#endif
