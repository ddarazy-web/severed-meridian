#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using LevelAuthoring.Documents;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Storage
{
    internal static class RecoveryDocumentReference
    {
        internal static JObject Capture(JsonContentStore store, ProjectDocumentEntry entry)
        {
            var value = new JObject { ["root"] = store.Root, ["path"] = entry.RelativePath, ["hash"] = entry.ContentHash };
            // 해시가 없는 구형 목록은 본문 대신 파일 정보로 고정한다. 최초 읽기 때 다시 확인한다.
            if (entry.ContentHash == null)
            {
                var file = new FileInfo(store.Resolve(entry.RelativePath));
                if (!file.Exists) throw new ContentFormatException("복구 참조 원본이 없습니다: " + entry.RelativePath);
                value["length"] = file.Length; value["modifiedUtc"] = file.LastWriteTimeUtc.Ticks;
            }
            return value;
        }

        internal static ContentDocument Restore(JObject reference, ContentDocumentInfo info, JsonContentStore store, ImmutableContentBodyStore bodies)
        {
            string root = store.Root;
            string referenceRoot = (string)reference["root"], path = (string)reference["path"], hash = (string)reference["hash"];
            if (!string.Equals(Path.GetFullPath(referenceRoot), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
                throw new ContentFormatException("복구 참조가 다른 작업 폴더를 가리킵니다.");
            JsonContentStore.ValidateRelativePath(path);
            if (hash != null && (hash.Length != 64 || System.Linq.Enumerable.Any(hash, c => !Uri.IsHexDigit(c))))
                throw new ContentFormatException("복구 참조 해시가 잘못됐습니다.");
            long? length = (long?)reference["length"], modified = (long?)reference["modifiedUtc"];
            if (hash == null && (length == null || length < 0 || modified == null))
                throw new ContentFormatException("복구 참조 버전 정보가 없습니다.");
            ContentDocument cached = null;
            ContentDocument Read()
            {
                if (cached != null) return cached;
                void CheckFile()
                {
                    var file = new FileInfo(store.Resolve(path));
                    if (!file.Exists || hash == null && (file.Length != length || file.LastWriteTimeUtc.Ticks != modified))
                        throw new ContentFormatException("복구 원본이 없거나 변경됐습니다: " + path);
                }
                CheckFile();
                ContentFile source = store.Read(path);
                CheckFile();
                if (hash != null && source.Hash != hash || source.Document.Id != info.Id ||
                    info.Kind != "unknown" && source.Document.Kind != info.Kind)
                    throw new ContentFormatException("복구 원본이 변경됐습니다: " + path);
                hash = source.Hash; info = source.Document.Info;
                cached = bodies.Capture(source.Document);
                return cached;
            }
            var document = ContentDocument.FromReference(info, Read, () => info, () => hash,
                (next, nextHash) => { info = next; });
            document.RecoveryReference = () => (JObject)reference.DeepClone();
            return document;
        }
    }
}
#endif
