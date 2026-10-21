#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Storage
{
    public sealed class ProjectDocumentEntry
    {
        public string Id { get; }
        public string RelativePath { get; }
        public ContentDocumentInfo Info { get; internal set; }
        public string ContentHash { get; internal set; }
        internal ProjectDocumentEntry(string id, string relativePath) { Id = id; RelativePath = relativePath; }
    }

    // 목록은 manifest만 사용한다. 미선택 문서의 존재 여부나 본문 유효성을 검사했다고 간주하지 않는다.
    public sealed class ContentProjectDirectory : IDisposable
    {
        private readonly JsonContentStore store;
        private readonly ContentFile manifest;
        private readonly Dictionary<string, ProjectDocumentEntry> entries;
        private readonly Dictionary<string, ContentDocument> loaded = new Dictionary<string, ContentDocument>(StringComparer.Ordinal);
        private ImmutableContentBodyStore bodies;
        private bool disposed;
        public IReadOnlyList<ProjectDocumentEntry> Entries { get; }
        public ContentDocument Project => manifest.Document.Copy();
        public string ManifestHash => manifest.Hash;
        public string MetadataWarning { get; private set; }
        internal string Root => store.Root;
        internal bool IsBackup { get; }
        internal ProjectDocumentEntry FindEntry(string id) => entries.TryGetValue(id, out var entry) ? entry : null;
        internal Dictionary<string, ContentDocument> CreateDocumentReferences()
        {
            var references = entries.Values.ToDictionary(entry => entry.Id, entry => ContentDocument.FromReference(
                entry.Info ?? new ContentDocumentInfo(entry.Id, "unknown", 0, 0, Path.GetFileName(entry.RelativePath)),
                () => ReadOwnedDocument(entry.Id), () => entry.Info, () => entry.ContentHash,
                (info, hash) => { entry.Info = info; entry.ContentHash = hash; }), StringComparer.Ordinal);
            references.Add(manifest.Document.Id, manifest.Document.Copy());
            foreach (ProjectDocumentEntry entry in Entries)
                references[entry.Id].RecoveryReference = () => RecoveryDocumentReference.Capture(store, entry);
            return references;
        }

        internal ContentProjectDirectory(JsonContentStore store, ContentFile manifest, bool isBackup = false)
        {
            if (manifest.Document.Kind != "project") throw new ContentFormatException("project.json의 종류가 project가 아닙니다.");
            this.store = store; this.manifest = manifest; IsBackup = isBackup;
            ProjectDocumentEntry[] items = ((JArray)manifest.Document.Data["documents"])
                .Select(entry => new ProjectDocumentEntry((string)entry["id"], (string)entry["path"])).ToArray();
            Entries = Array.AsReadOnly(items);
            entries = items.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
            MetadataWarning = ContentDirectoryIndex.Apply(store, manifest.Hash, entries);
        }

        internal void ApplyListing(ContentProjectDirectory candidate)
        {
            if (Root != candidate.Root || ManifestHash != candidate.ManifestHash)
                throw new ContentFormatException("프로젝트가 변경되어 목록을 적용하지 않았습니다. 다시 열어 주세요.");
            foreach (ProjectDocumentEntry entry in Entries)
                if (entry.ContentHash != null && entry.ContentHash != candidate.FindEntry(entry.Id)?.ContentHash)
                    throw new ContentFormatException("외부에서 변경된 문서가 있습니다. 현재 편집은 유지합니다: " + entry.RelativePath);
            foreach (ProjectDocumentEntry entry in Entries)
            {
                ProjectDocumentEntry source = candidate.FindEntry(entry.Id);
                entry.Info = source.Info; entry.ContentHash = source.ContentHash;
            }
            MetadataWarning = candidate.MetadataWarning;
        }

        public ContentDocument ReadDocument(string id) => ReadOwnedDocument(id).Copy();

        private ContentDocument ReadOwnedDocument(string id)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ContentProjectDirectory));
            if (id == manifest.Document.Id) return Project;
            if (id == null || !entries.TryGetValue(id, out ProjectDocumentEntry entry))
                throw new ContentFormatException("없는 문서 ID: " + id);
            if (loaded.TryGetValue(id, out ContentDocument existing)) return existing;
            // 자기 저장 뒤에도 Undo가 참조하는 이전 세대 파일을 읽을 수 있어야 한다.
            // 알려진 본문 해시로 해당 버전을 확인하며 manifest 충돌은 저장 직전에 검사한다.
            ContentFile file = store.Read(entry.RelativePath);
            if (entry.ContentHash != null && file.Hash != entry.ContentHash)
                throw new ContentFormatException("목록 작성 이후 문서가 외부에서 바뀌었습니다: " + entry.RelativePath);
            ContentDocument document = file.Document;
            if (document.Id != id || document.Kind == "project")
                throw new ContentFormatException("목록의 ID와 문서가 다릅니다: " + entry.RelativePath);
            // 최초 선택 시에만 읽은 버전을 고정한다. 이후 원본 변경이 이미 연 문서에 섞이지 않는다.
            bodies ??= new ImmutableContentBodyStore(Path.Combine(Path.GetTempPath(), "MoonRabbitJunkyard", "AuthoringBodies"));
            loaded.Add(id, bodies.Capture(document));
            entry.Info = document.Info; entry.ContentHash = file.Hash;
            return loaded[id];
        }

        internal string VerifySavedContent(ContentDocument baseline)
        {
            ProjectDocumentEntry entry = FindEntry(baseline.Id);
            if (entry == null) return null;
            ContentFile file = store.Read(entry.RelativePath);
            if (entry.ContentHash != null && entry.ContentHash != file.Hash ||
                file.Document.Kind != baseline.Kind || file.Document.Id != baseline.Id || !JToken.DeepEquals(file.Document.Data, baseline.Data))
                throw new ContentFormatException("편집한 문서가 복구 당시 저장본과 다릅니다. 덮어쓰지 않았습니다: " + entry.RelativePath);
            return file.Hash;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; loaded.Clear(); bodies?.Dispose();
        }
    }
}
#endif
