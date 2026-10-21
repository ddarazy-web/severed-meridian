#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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
            Hash = ComputeRevisionHash(projectHash, memberHashes);
        }
        internal static string ComputeRevisionHash(string projectHash, string memberHashes)
        {
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(projectHash + "\n" + memberHashes))).Replace("-", "").ToLowerInvariant();
        }
    }

    // 각 세대의 문서는 독립 경로에 쓰고 project.json만 마지막에 교체한다.
    // 중단된 세대는 공개 목록에 없으므로 재열기/복구에서 사용하지 않는다.
    public sealed partial class ContentSnapshotStore
    {
        private readonly JsonContentStore store;
        private readonly Action<SnapshotPublishPhase> checkpoint;
        public ContentSnapshotStore(string root) : this(root, null) { }
        internal ContentSnapshotStore(string root, Action<SnapshotPublishPhase> checkpoint)
        { store = new JsonContentStore(root); this.checkpoint = checkpoint; }

        public StoredContentSnapshot Read() => Load(store.Read("project.json"));
        internal StoredContentSnapshot ReadAndIndex(CancellationToken cancellationToken) => Load(store.Read("project.json"), true, cancellationToken);
        public ContentProjectDirectory ReadDirectory() => new ContentProjectDirectory(store, store.Read("project.json"));
        public ContentProjectDirectory ReadBackupDirectory() => new ContentProjectDirectory(store, store.ReadBackup("project.json"), true);
        // 사용자가 목록 생성을 요청한 경우에만 전체 파일을 순차적으로 읽는다.
        // 본문은 보관하지 않으며 취소·읽기 실패 시 기존 목록을 교체하지 않는다.
        public ContentProjectDirectory RebuildDirectoryIndex(CancellationToken cancellationToken, Action<int, int> progress = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var directory = ReadDirectory();
            var records = new JArray();
            int done = 0;
            progress?.Invoke(done, directory.Entries.Count);
            foreach (ProjectDocumentEntry entry in directory.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ContentFile file = store.Read(entry.RelativePath);
                if (file.Document.Id != entry.Id || file.Document.Kind == "project")
                    throw new ContentFormatException("목록의 ID와 문서가 다릅니다: " + entry.RelativePath);
                records.Add(ContentDirectoryIndex.Entry(file.Document.Info, entry.RelativePath, file.Hash));
                progress?.Invoke(++done, directory.Entries.Count);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (store.ReadHash("project.json") != directory.ManifestHash)
                throw new ContentFormatException("목록을 만드는 동안 프로젝트가 변경되었습니다. 다시 열어 주세요.");
            ContentDirectoryIndex.Write(store, directory.ManifestHash, records);
            return ReadDirectory();
        }
        public StoredContentSnapshot ReadBackup() => Load(store.ReadBackup("project.json"));
        public StoredContentSnapshot Publish(ContentSnapshot snapshot, string expectedHash)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var previous = CheckExpected(expectedHash);
            var project = snapshot.Project;
            string[] ids = snapshot.DocumentIds.Where(id => id != project.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            string generation = "snapshots/" + Guid.NewGuid().ToString("N");
            var entries = new JArray();
            var directoryEntries = new JArray();
            checkpoint?.Invoke(SnapshotPublishPhase.BeforeDocuments);
            for (int index = 0; index < ids.Length; index++)
            {
                ContentDocument document = snapshot.Get(ids[index]);
                string path = generation + "/" + index.ToString("D4") + ".json";
                ContentFile saved = store.Save(document, path, null);
                entries.Add(new JObject { ["id"] = document.Id, ["path"] = path });
                directoryEntries.Add(ContentDirectoryIndex.Entry(document.Info, path, saved.Hash));
                checkpoint?.Invoke(SnapshotPublishPhase.AfterDocument);
            }
            project.Data["documents"] = entries;
            // 공개 전에 실제 기록한 모든 파일을 다시 읽고 참조까지 검증한다.
            var candidate = Load(new ContentFile(project, ""));
            checkpoint?.Invoke(SnapshotPublishPhase.BeforeProject);
            CheckExpected(expectedHash);
            // 목록 정보도 공개 전에 기록한다. 시작 시 본문을 다시 열어 목록을 만들지 않는다.
            ContentDirectoryIndex.Write(store, JsonContentStore.DocumentHash(project), directoryEntries);
            var published = store.Save(project, "project.json", previous);
            return new StoredContentSnapshot(candidate.Snapshot, published.Hash, candidate.MemberHashes);
        }
        private string CheckExpected(string expectedHash)
        {
            ContentFile project;
            try { project = store.Read("project.json"); }
            catch (FileNotFoundException) { return Missing(expectedHash); }
            catch (DirectoryNotFoundException) { return Missing(expectedHash); }
            if (expectedHash == null) throw new ContentFormatException("프로젝트가 이미 있습니다. 다시 읽은 뒤 저장하세요.");
            if (project.Document.Kind != "project") throw new ContentFormatException("project.json의 종류가 project가 아닙니다.");
            // 하위 파일 누락을 새 프로젝트로 취급하지 않도록 manifest 읽기와 분리한다.
            var hashes = new StringBuilder();
            foreach (var entry in (JArray)project.Document.Data["documents"])
                hashes.Append(store.ReadHash((string)entry["path"])).Append('\n');
            if (StoredContentSnapshot.ComputeRevisionHash(project.Hash, hashes.ToString()) != expectedHash)
                throw new ContentFormatException("외부에서 스냅샷이 변경됐습니다. 덮어쓰지 않았습니다.");
            return project.Hash;
        }
        private static string Missing(string expectedHash)
        {
            if (expectedHash != null) throw new ContentFormatException("프로젝트가 삭제됐습니다. 다시 열어 주세요.");
            return null;
        }
        private StoredContentSnapshot Load(ContentFile project, bool writeIndex = false, CancellationToken cancellationToken = default)
        {
            if (project.Document.Kind != "project") throw new ContentFormatException("project.json의 종류가 project가 아닙니다.");
            var documents = new List<ContentDocument> { project.Document };
            var hashes = new StringBuilder();
            JArray index = writeIndex ? new JArray() : null;
            var bodies = new ImmutableContentBodyStore(Path.Combine(Path.GetTempPath(), "MoonRabbitJunkyard", "AuthoringBodies"));
            try
            {
                foreach (var entry in (JArray)project.Document.Data["documents"])
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var file = store.Read((string)entry["path"]);
                    var document = file.Document;
                    hashes.Append(file.Hash).Append('\n');
                    if (document.Kind == "project" || document.Id != (string)entry["id"])
                        throw new ContentFormatException("공개 목록의 ID와 문서가 다릅니다: " + entry["path"]);
                    documents.Add(document.Kind == "level" ? bodies.Capture(document) : document);
                    index?.Add(ContentDirectoryIndex.Entry(document.Info, (string)entry["path"], file.Hash));
                }
                // 전체 검증은 유지하되 레벨 본문은 한 번에 한 문서씩만 펼친다.
                var snapshot = new StoredContentSnapshot(ContentSnapshot.FromValidatedDocuments(documents), project.Hash, hashes.ToString());
                cancellationToken.ThrowIfCancellationRequested();
                // ZIP 검사 중 읽은 정보로 목록을 만든다. 등록 뒤 다시 모든 파일을 열지 않는다.
                if (writeIndex) ContentDirectoryIndex.Write(store, project.Hash, index);
                return snapshot;
            }
            catch { bodies.Dispose(); throw; }
        }
    }
}
#endif
