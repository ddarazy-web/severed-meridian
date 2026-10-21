#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
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
            current = new State { Documents = stored.Snapshot.Documents.ToDictionary(doc => doc.Id, StringComparer.Ordinal), Cells = Array.Empty<int>() };
            SelectLevel(levelId); saved = current.Documents; savedLevelId = levelId; Revision = stored.Hash;
        }
        public ContentDocument Get(string id)
        {
            if (id == null || !current.Documents.TryGetValue(id, out ContentDocument document)) throw new ContentFormatException("없는 문서 ID: " + id);
            return Clone(document);
        }
        public ContentDocument[] Documents => current.Documents.Values.Select(Clone).ToArray();
        private sealed class DocumentStamp
        {
            internal readonly long Value = System.Threading.Interlocked.Increment(ref nextDocumentStamp);
        }
        private static long nextDocumentStamp;
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ContentDocument, DocumentStamp> documentStamps =
            new System.Runtime.CompilerServices.ConditionalWeakTable<ContentDocument, DocumentStamp>();
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
            if (Get(id).Kind != "level") throw new ContentFormatException("레벨 문서를 선택하세요.");
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
        public ContentSnapshot CreateSnapshot() => new ContentSnapshot(current.Documents.Values);
        public void Save(ContentSnapshotStore repository) => Publish(repository, Revision);
        public void SaveAs(ContentSnapshotStore repository) => Publish(repository, null);
        private void Publish(ContentSnapshotStore repository, string expectedHash)
        {
            StoredContentSnapshot published = repository.Publish(CreateSnapshot(), expectedHash);
            Revision = published.Hash;
            current.Documents = published.Snapshot.Documents.ToDictionary(doc => doc.Id, StringComparer.Ordinal);
            saved = current.Documents; savedLevelId = current.LevelId;
            // 저장 이전의 원자적 작업은 부분 취소 이후에도 원래 모습으로 Undo할 수 있어야 한다.
            foreach (State state in undo) state.BeforeSave = true;
            foreach (State state in redo) state.BeforeSave = true;
            current.BeforeSave = false;
        }
        // 사전과 문서는 게시 이후 불변이며, 변경 가능한 선택 상태만 별도 소유한다.
        private State Capture() => new State { Documents = current.Documents, LevelId = current.LevelId, Cells = (int[])current.Cells.Clone(), BeforeSave = current.BeforeSave };
        private static ContentDocument Clone(ContentDocument doc) => new ContentDocument(doc.Kind, doc.Id, doc.Data);
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
