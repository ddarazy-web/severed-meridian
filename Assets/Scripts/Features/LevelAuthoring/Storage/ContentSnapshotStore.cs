#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using LevelAuthoring.Documents;

namespace LevelAuthoring.Storage
{
    internal enum SnapshotPublishPhase { BeforeDocuments, AfterDocument, BeforeProject }
    public sealed class StoredContentSnapshot
    {
        public ContentSnapshot Snapshot { get; }
        public string Hash { get; }
        internal string ProjectHash { get; }
        internal string MemberHashes { get; }
        internal StoredContentSnapshot(ContentSnapshot snapshot, string projectHash, string memberHashes)
        {
            Snapshot = snapshot; ProjectHash = projectHash; MemberHashes = memberHashes;
            using (var algorithm = SHA256.Create())
                Hash = BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(projectHash + "\n" + memberHashes))).Replace("-", "").ToLowerInvariant();
        }
    }

    // 각 세대의 문서는 독립 경로에 쓰고 project.json만 마지막에 교체한다.
    // 중단된 세대는 공개 목록에 없으므로 재열기/복구에서 사용하지 않는다.
    public sealed class ContentSnapshotStore
    {
        private readonly JsonContentStore store;
        private readonly Action<SnapshotPublishPhase> checkpoint;
        public ContentSnapshotStore(string root) : this(root, null) { }
        internal ContentSnapshotStore(string root, Action<SnapshotPublishPhase> checkpoint)
        { store = new JsonContentStore(root); this.checkpoint = checkpoint; }

        public StoredContentSnapshot Read() => Load(store.Read("project.json"));
        public StoredContentSnapshot ReadBackup() => Load(store.ReadBackup("project.json"));
        public StoredContentSnapshot Publish(ContentSnapshot snapshot, string expectedHash)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var previous = CheckExpected(expectedHash);
            var project = snapshot.Project;
            var documents = snapshot.Documents.Where(document => document.Kind != "project").OrderBy(document => document.Id, StringComparer.Ordinal).ToArray();
            string generation = "snapshots/" + Guid.NewGuid().ToString("N");
            var entries = new JArray();
            checkpoint?.Invoke(SnapshotPublishPhase.BeforeDocuments);
            for (int index = 0; index < documents.Length; index++)
            {
                string path = generation + "/" + index.ToString("D4") + ".json";
                store.Save(documents[index], path, null);
                entries.Add(new JObject { ["id"] = documents[index].Id, ["path"] = path });
                checkpoint?.Invoke(SnapshotPublishPhase.AfterDocument);
            }
            project.Data["documents"] = entries;
            // 공개 전에 실제 기록한 모든 파일을 다시 읽고 참조까지 검증한다.
            var candidate = Load(new ContentFile(project, ""));
            checkpoint?.Invoke(SnapshotPublishPhase.BeforeProject);
            CheckExpected(expectedHash);
            var published = store.Save(project, "project.json", previous?.ProjectHash);
            return new StoredContentSnapshot(candidate.Snapshot, published.Hash, candidate.MemberHashes);
        }
        private StoredContentSnapshot CheckExpected(string expectedHash)
        {
            ContentFile project;
            try { project = store.Read("project.json"); }
            catch (FileNotFoundException) { return Missing(expectedHash); }
            catch (DirectoryNotFoundException) { return Missing(expectedHash); }
            if (expectedHash == null) throw new ContentFormatException("프로젝트가 이미 있습니다. 다시 읽은 뒤 저장하세요.");
            // 하위 파일 누락을 새 프로젝트로 취급하지 않도록 manifest 읽기와 분리한다.
            var current = Load(project);
            if (current.Hash != expectedHash) throw new ContentFormatException("외부에서 스냅샷이 변경됐습니다. 덮어쓰지 않았습니다.");
            return current;
        }
        private static StoredContentSnapshot Missing(string expectedHash)
        {
            if (expectedHash != null) throw new ContentFormatException("프로젝트가 삭제됐습니다. 다시 열어 주세요.");
            return null;
        }
        private StoredContentSnapshot Load(ContentFile project)
        {
            if (project.Document.Kind != "project") throw new ContentFormatException("project.json의 종류가 project가 아닙니다.");
            var documents = new List<ContentDocument> { project.Document };
            var hashes = new StringBuilder();
            foreach (var entry in (JArray)project.Document.Data["documents"])
            {
                var file = store.Read((string)entry["path"]);
                var document = file.Document;
                hashes.Append(file.Hash).Append('\n');
                if (document.Kind == "project" || document.Id != (string)entry["id"])
                    throw new ContentFormatException("공개 목록의 ID와 문서가 다릅니다: " + entry["path"]);
                documents.Add(document);
            }
            return new StoredContentSnapshot(new ContentSnapshot(documents), project.Hash, hashes.ToString());
        }
    }
}
#endif
