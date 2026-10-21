#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using LevelAuthoring.Validation;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Editing
{
    // 작성 중 오류는 수정할 수 있게 남기고 저장/시험할 때 전체 문서를 검증한다.
    public sealed partial class AuthoringEditSession
    {
        private sealed class State
        {
            internal Dictionary<string, ContentDocument> Documents;
            internal string LevelId;
            internal int[] Cells;
            internal bool BeforeSave;
        }
        private State current;
        private Dictionary<string, ContentDocument> saved;
        private string savedLevelId;
        private readonly Stack<State> undo = new Stack<State>();
        private readonly Stack<State> redo = new Stack<State>();
        private int lastEditCopyCount;
        internal ContentProjectDirectory DirectoryBaseline { get; private set; }
        public bool RequiresProjectValidation => DirectoryBaseline != null;
        public long MetadataVersion { get; private set; }
        public string Revision { get; private set; }
        public string SelectedLevelId => current.LevelId;
        public int[] SelectedCells => (int[])current.Cells.Clone();
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public bool IsDirty => !Equal(current.Documents, saved);
        public string ValidationError
        {
            get { try { CreateSnapshot(); return null; } catch (ContentFormatException error) { return error.Message; } }
        }
        public AuthoringEditSession(StoredContentSnapshot stored, string levelId)
        {
            if (stored == null) throw new ArgumentNullException(nameof(stored));
            current = new State { Documents = stored.Snapshot.ShareOwnedDocuments(), Cells = Array.Empty<int>() };
            SelectLevel(levelId); saved = current.Documents; savedLevelId = levelId; Revision = stored.Hash;
        }
        public AuthoringEditSession(ContentProjectDirectory directory, string levelId)
        {
            DirectoryBaseline = directory ?? throw new ArgumentNullException(nameof(directory));
            current = new State { Documents = directory.CreateDocumentReferences(), Cells = Array.Empty<int>() };
            SelectLevel(levelId); saved = current.Documents; savedLevelId = levelId; Revision = directory.ManifestHash;
        }
        public ContentDocument Get(string id)
        {
            if (id == null || !current.Documents.TryGetValue(id, out ContentDocument document)) throw new ContentFormatException("없는 문서 ID: " + id);
            bool unknown = document.Kind == "unknown";
            ContentDocument copy = Clone(document);
            if (unknown) MetadataVersion++;
            return copy;
        }
        public ContentDocument[] Documents => current.Documents.Values.Select(Clone).ToArray();
        private sealed class DocumentStamp
        {
            internal readonly long Value = System.Threading.Interlocked.Increment(ref nextDocumentStamp);
        }
        private static long nextDocumentStamp;
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ContentDocument, DocumentStamp> documentStamps =
            new System.Runtime.CompilerServices.ConditionalWeakTable<ContentDocument, DocumentStamp>();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Dictionary<string, ContentDocument>, DocumentStamp> stateStamps =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Dictionary<string, ContentDocument>, DocumentStamp>();
        public long ContentVersion => stateStamps.GetValue(current.Documents, _ => new DocumentStamp()).Value;
        public ContentDocumentInfo GetDocumentInfo(string id) => current.Documents[id].Info;
        internal void ApplyListing(ContentProjectDirectory candidate)
        {
            foreach (var pair in saved)
            {
                ProjectDocumentEntry entry = DirectoryBaseline.FindEntry(pair.Key);
                if (entry != null && entry.ContentHash == null && pair.Value.SourceHash != null &&
                    pair.Value.SourceHash != candidate.FindEntry(pair.Key)?.ContentHash)
                    throw new ContentFormatException("외부에서 변경된 문서가 있습니다. 현재 편집은 유지합니다: " + pair.Key);
            }
            DirectoryBaseline.ApplyListing(candidate);
            // 이미 읽은 과거 버전과 초안은 그대로 두고, 아직 읽지 않은 공유 핸들의 목록만 보충한다.
            foreach (ContentDocument document in current.Documents.Values.Concat(saved.Values).Distinct())
            {
                ProjectDocumentEntry entry = candidate.FindEntry(document.Id);
                if (entry != null) document.SupplyReferenceInfo(entry.Info, entry.ContentHash);
            }
            MetadataVersion++;
        }
        // 게시 후 불변인 문서의 식별자만 제공한다. 화면 캐시에 내부 JObject 참조를 노출하지 않는다.
        public string[] DocumentIds => current.Documents.Keys.ToArray();
        public string[] DocumentIdsOfKind(string kind) => current.Documents.Values.Where(doc => doc.Kind == kind).Select(doc => doc.Id).ToArray();
        // 그래프가 생성된 시점의 불변 게시 문서를 유지하되, 실제 요청한 문서의 사본만 반환한다.
        internal Func<string, ContentDocument> CaptureDocumentReader()
        {
            Dictionary<string, ContentDocument> published = current.Documents;
            return id => published.TryGetValue(id, out ContentDocument document) ? Clone(document) :
                throw new ContentFormatException("없는 제작 문서: " + id);
        }
        public long GetDocumentVersion(string id) => documentStamps.GetValue(current.Documents[id], _ => new DocumentStamp()).Value;
        public void SelectLevel(string id)
        {
            if (id == null || !current.Documents.TryGetValue(id, out ContentDocument document)) throw new ContentFormatException("없는 문서 ID: " + id);
            bool unknown = document.Kind == "unknown";
            document.EnsureLoaded();
            if (unknown) MetadataVersion++;
            if (document.Kind != "level") throw new ContentFormatException("레벨 문서를 선택하세요.");
            current.LevelId = id; current.Cells = Array.Empty<int>();
        }
        public void SelectCells(IEnumerable<int> cells)
        {
            int[] selection = cells.Distinct().ToArray();
            if (selection.Any(cell => cell < 0 || cell >= 81)) throw new ArgumentOutOfRangeException(nameof(cells));
            current.Cells = selection;
        }
        public void EditLevel(string label, Action<JObject> edit)
        {
            if (edit == null) throw new ArgumentNullException(nameof(edit));
            ApplyToDocuments(label, new[] { SelectedLevelId }, docs =>
            {
                JObject level = docs[SelectedLevelId].Data;
                if ((int)level["schemaVersion"] != 5) throw new InvalidOperationException("먼저 ‘편집 형식으로 전환’을 눌러 주세요. 이전 형식의 원본은 저장 전까지 바뀌지 않습니다.");
                edit(level);
            });
        }

        public void Apply(string label, Action<IDictionary<string, ContentDocument>> edit)
        {
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("변경 이름이 필요합니다.", nameof(label));
            if (edit == null) throw new ArgumentNullException(nameof(edit));
            lastEditCopyCount = 0;
            Dictionary<string, ContentDocument> draft = current.Documents.ToDictionary(pair => pair.Key, pair => CopyForEdit(pair.Value), StringComparer.Ordinal);
            edit(draft);
            CommitDraft(draft);
        }

        // 문서 ID 목록에는 추가할 ID도 포함한다. 콜백에는 대상의 사본만 주며 삭제는 사전에서 제거한다.
        public void ApplyToDocuments(string label, IEnumerable<string> documentIds, Action<IDictionary<string, ContentDocument>> edit)
        {
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("변경 이름이 필요합니다.", nameof(label));
            if (documentIds == null) throw new ArgumentNullException(nameof(documentIds));
            if (edit == null) throw new ArgumentNullException(nameof(edit));
            var targets = new HashSet<string>(documentIds, StringComparer.Ordinal);
            if (targets.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("대상 문서 ID가 필요합니다.", nameof(documentIds));
            lastEditCopyCount = 0;
            var draft = targets.Where(current.Documents.ContainsKey).ToDictionary(id => id, id => CopyForEdit(current.Documents[id]), StringComparer.Ordinal);
            edit(draft);
            if (draft.Keys.Any(id => !targets.Contains(id))) throw new ContentFormatException("지정하지 않은 문서를 변경에 추가할 수 없습니다.");
            // 무관한 불변 문서는 공유한다. 예외 시 현재 상태를 바꾸지 않도록 게시 전 별도 사전을 구성한다.
            var candidate = new Dictionary<string, ContentDocument>(current.Documents, StringComparer.Ordinal);
            foreach (string id in targets)
            {
                if (draft.TryGetValue(id, out ContentDocument document)) candidate[id] = document;
                else candidate.Remove(id);
            }
            CommitDraft(candidate);
        }

        private void CommitDraft(Dictionary<string, ContentDocument> draft)
        {
            if (draft.Any(pair => pair.Value == null || pair.Key != pair.Value.Id)) throw new ContentFormatException("문서 ID와 저장 키가 다릅니다.");
            if (!draft.TryGetValue(current.LevelId, out ContentDocument level) || level.Kind != "level") throw new ContentFormatException("선택한 레벨을 변경에서 제거할 수 없습니다.");
            if (Equal(draft, current.Documents)) return;
            // 게시한 내부 문서는 다시 수정하지 않는다. 외부 콜백의 변경 문서만 새로 복사한다.
            Dictionary<string, ContentDocument> next = draft.ToDictionary(pair => pair.Key, pair =>
                current.Documents.TryGetValue(pair.Key, out ContentDocument previous) &&
                (ReferenceEquals(previous, pair.Value) || previous.Kind == pair.Value.Kind &&
                JToken.DeepEquals(previous.Data, pair.Value.Data)) ? previous : CopyForEdit(pair.Value), StringComparer.Ordinal);
            undo.Push(Capture()); redo.Clear();
            current.Documents = next;
            current.BeforeSave = false;
        }
        public void Undo() { if (undo.Count == 0) return; redo.Push(Capture()); current = undo.Pop(); }
        public void Redo() { if (redo.Count == 0) return; undo.Push(Capture()); current = redo.Pop(); }
        public void Discard()
        {
            string levelId = saved.ContainsKey(current.LevelId) ? current.LevelId :
                savedLevelId;
            current = new State { Documents = saved, LevelId = levelId, Cells = Array.Empty<int>() };
            undo.Clear(); redo.Clear();
        }
        public ContentSnapshot CreateSnapshot() => ContentSnapshot.FromOwnedDocuments(current.Documents.Values);

        // 시험 대상과 스키마에 명시된 참조만 모은다. 다른 레벨의 본문이나 오류는 시험 입력에 포함하지 않는다.
        public ContentSnapshot CreateTrialSnapshot(IEnumerable<string> levelIds, ContentDocument replacement = null)
        {
            var requested = new HashSet<string>(levelIds ?? throw new ArgumentNullException(nameof(levelIds)), StringComparer.Ordinal);
            if (requested.Count == 0) throw new ContentFormatException("시험할 레벨을 선택하세요.");
            ContentDocument project = Get(DocumentIdsOfKind("project").Single());
            if (requested.Contains(project.Id)) throw new ContentFormatException("시험 입력은 레벨이어야 합니다: " + project.Id);
            var documents = new Dictionary<string, ContentDocument>(StringComparer.Ordinal) { { project.Id, project } };
            var pending = new Queue<string>(requested.Concat(ContentDocumentValidator.ReferencedDocumentIds(project)));
            while (pending.Count > 0)
            {
                string id = pending.Dequeue();
                if (documents.ContainsKey(id)) continue;
                ContentDocument document = replacement?.Id == id ? replacement.Copy() : Get(id);
                if (requested.Contains(id) && document.Kind != "level") throw new ContentFormatException("시험 입력은 레벨이어야 합니다: " + id);
                documents.Add(id, document);
                foreach (string reference in ContentDocumentValidator.ReferencedDocumentIds(document)) pending.Enqueue(reference);
            }
            project.Data["documents"] = new JArray(documents.Values.Where(doc => doc.Kind != "project").OrderBy(doc => doc.Id, StringComparer.Ordinal)
                .Select((doc, index) => new JObject { ["id"] = doc.Id, ["path"] = "trial/" + index + ".json" }));
            return ContentSnapshot.FromOwnedDocuments(documents.Values);
        }
        public void Save(ContentSnapshotStore repository)
        {
            if (DirectoryBaseline == null)
            {
                ContentProjectDirectory candidate = repository.ReadDirectory();
                // v1에 저장된 manifest revision으로 지연 세션의 저장 경계를 다시 연결한다.
                if (candidate.ManifestHash == Revision) DirectoryBaseline = candidate;
                else candidate.Dispose();
            }
            Publish(repository, Revision);
        }
        public void SaveAs(ContentSnapshotStore repository) => Publish(repository, null);
        private void Publish(ContentSnapshotStore repository, string expectedHash)
        {
            if (DirectoryBaseline != null && expectedHash != null)
            {
                ContentDocument project = current.Documents.Values.Single(document => document.Kind == "project");
                ContentDocument[] changes = current.Documents.Values.Where(document => document.Id != project.Id &&
                    (!saved.TryGetValue(document.Id, out ContentDocument previous) || !ReferenceEquals(document, previous))).ToArray();
                string[] removed = saved.Keys.Except(current.Documents.Keys).ToArray();
                var sourceHashes = changes.Select(document => document.Id).Concat(removed).Where(saved.ContainsKey).ToDictionary(id => id,
                    id => saved[id].SourceHash == null
                        ? DirectoryBaseline.VerifySavedContent(saved[id])
                        : DirectoryBaseline.FindEntry(id)?.ContentHash ?? saved[id].SourceHash, StringComparer.Ordinal);
                ContentProjectDirectory published = repository.PublishChanges(DirectoryBaseline, project, changes, removed, sourceHashes, current.Documents);
                var next = new Dictionary<string, ContentDocument>(current.Documents, StringComparer.Ordinal);
                next[project.Id] = published.Project;
                current.Documents = next; DirectoryBaseline = published; Revision = published.ManifestHash;
            }
            else
            {
                StoredContentSnapshot published = repository.Publish(CreateSnapshot(), expectedHash);
                Revision = published.Hash;
                current.Documents = published.Snapshot.ShareOwnedDocuments();
                DirectoryBaseline = null;
            }
            saved = current.Documents; savedLevelId = current.LevelId;
            // 저장 이전의 원자적 작업은 부분 취소 이후에도 원래 모습으로 Undo할 수 있어야 한다.
            foreach (State state in undo) state.BeforeSave = true;
            foreach (State state in redo) state.BeforeSave = true;
            current.BeforeSave = false;
        }
        // 사전과 문서는 게시 이후 불변이며, 변경 가능한 선택 상태만 별도 소유한다.
        private State Capture() => new State { Documents = current.Documents, LevelId = current.LevelId, Cells = (int[])current.Cells.Clone(), BeforeSave = current.BeforeSave };
        private static ContentDocument Clone(ContentDocument doc) => doc.Copy();
        private ContentDocument CopyForEdit(ContentDocument doc) { lastEditCopyCount++; return Clone(doc); }
        private static bool Equal(Dictionary<string, ContentDocument> left, Dictionary<string, ContentDocument> right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left.Count != right.Count) return false;
            foreach (var pair in left)
            {
                if (!right.TryGetValue(pair.Key, out ContentDocument other) || pair.Value.Kind != other.Kind) return false;
                if (ReferenceEquals(pair.Value, other)) continue;
                JObject a = pair.Value.Data, b = other.Data;
                // 세대별 파일 경로는 저장소가 정하므로 사용자 편집의 dirty 비교에서 제외한다.
                if (pair.Value.Kind == "project")
                {
                    if (a.Properties().Count(p => p.Name != "documents") != b.Properties().Count(p => p.Name != "documents") ||
                        a.Properties().Any(p => p.Name != "documents" && !JToken.DeepEquals(p.Value, b[p.Name]))) return false;
                    continue;
                }
                if (!JToken.DeepEquals(a, b)) return false;
            }
            return true;
        }
    }
}
#endif
