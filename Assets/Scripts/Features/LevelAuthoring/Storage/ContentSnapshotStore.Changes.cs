#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Validation;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Storage
{
    public sealed partial class ContentSnapshotStore
    {
        // 편집 저장은 바뀐 문서만 검사·기록한다. 프로젝트 전체 검증/변환은 명시적 작업이다.
        internal ContentProjectDirectory PublishChanges(ContentProjectDirectory baseline, ContentDocument project,
            IReadOnlyCollection<ContentDocument> changed, IReadOnlyCollection<string> removed, IReadOnlyDictionary<string, string> sourceHashes,
            Dictionary<string, ContentDocument> allDocuments)
        {
            if (!string.Equals(store.Root, baseline.Root, StringComparison.OrdinalIgnoreCase))
                throw new ContentFormatException("다른 폴더에는 사본 저장을 사용하세요.");
            var updates = changed.ToDictionary(document => document.Id, StringComparer.Ordinal);
            var deleted = new HashSet<string>(removed, StringComparer.Ordinal);
            foreach (ContentDocument document in changed)
            {
                ContentDocumentValidator.Validate(document);
                ContentDocumentValidator.ValidateDocumentReferences(document, allDocuments);
            }
            ContentDocumentValidator.Validate(project);
            ContentDocumentValidator.ValidateDocumentReferences(project, allDocuments);
            var knownNumbers = new Dictionary<int, string>();
            foreach (ContentDocument candidate in allDocuments.Values)
            {
                ContentDocumentInfo info = candidate.Info;
                if (info.Kind != "level") continue;
                if (knownNumbers.TryGetValue(info.LevelNumber, out string other))
                {
                    if (updates.ContainsKey(info.Id) || updates.ContainsKey(other))
                        throw new ContentFormatException("중복 레벨 번호입니다: " + info.LevelNumber);
                }
                else knownNumbers.Add(info.LevelNumber, info.Id);
            }
            CheckDirectoryExpected(baseline, changed.Select(document => document.Id).Concat(removed), sourceHashes);
            string generation = "snapshots/" + Guid.NewGuid().ToString("N");
            var manifest = new JArray(); var listing = new JArray();
            checkpoint?.Invoke(SnapshotPublishPhase.BeforeDocuments);
            int number = 0;
            foreach (ProjectDocumentEntry entry in baseline.Entries)
            {
                if (deleted.Contains(entry.Id)) continue;
                if (updates.Remove(entry.Id, out ContentDocument document)) WriteChange(document);
                else
                {
                    manifest.Add(new JObject { ["id"] = entry.Id, ["path"] = entry.RelativePath });
                    listing.Add(ContentDirectoryIndex.Entry(entry));
                }
            }
            foreach (ContentDocument document in updates.Values.OrderBy(document => document.Id, StringComparer.Ordinal)) WriteChange(document);
            project = project.Copy(); project.Data["documents"] = manifest;
            ContentDocumentValidator.Validate(project);
            checkpoint?.Invoke(SnapshotPublishPhase.BeforeProject);
            CheckDirectoryExpected(baseline, changed.Select(document => document.Id).Concat(removed), sourceHashes);
            ContentDirectoryIndex.Write(store, JsonContentStore.DocumentHash(project), listing);
            ContentFile published = store.Save(project, "project.json", baseline.ManifestHash);
            return new ContentProjectDirectory(store, published);

            void WriteChange(ContentDocument document)
            {
                string path = generation + "/" + (number++).ToString("D4") + ".json";
                ContentFile file = store.Save(document, path, null);
                manifest.Add(new JObject { ["id"] = document.Id, ["path"] = path });
                listing.Add(ContentDirectoryIndex.Entry(document.Info, path, file.Hash));
                checkpoint?.Invoke(SnapshotPublishPhase.AfterDocument);
            }
        }

        private void CheckDirectoryExpected(ContentProjectDirectory baseline, IEnumerable<string> changedOrRemoved, IReadOnlyDictionary<string, string> sourceHashes)
        {
            if (store.ReadHash("project.json") != baseline.ManifestHash)
                throw new ContentFormatException("프로젝트 목록이 외부에서 바뀌었습니다. 덮어쓰지 않았습니다.");
            foreach (string id in changedOrRemoved)
            {
                ProjectDocumentEntry original = baseline.FindEntry(id);
                if (original == null) continue;
                if (!sourceHashes.TryGetValue(id, out string expected) || expected == null || store.ReadHash(original.RelativePath) != expected)
                    throw new ContentFormatException("편집한 문서가 외부에서 바뀌었습니다. 덮어쓰지 않았습니다: " + original.RelativePath);
            }
        }
    }
}
#endif
