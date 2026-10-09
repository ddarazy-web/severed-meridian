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
        }
        private State current;
        private Dictionary<string, ContentDocument> saved;
        private string savedLevelId;
        private readonly Stack<State> undo = new Stack<State>();
        private readonly Stack<State> redo = new Stack<State>();
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
            SelectLevel(levelId); saved = Copy(current.Documents); savedLevelId = levelId; Revision = stored.Hash;
        }
        public ContentDocument Get(string id)
        {
            if (id == null || !current.Documents.TryGetValue(id, out ContentDocument document)) throw new ContentFormatException("없는 문서 ID: " + id);
            return Clone(document);
        }
        public ContentDocument[] Documents => current.Documents.Values.Select(Clone).ToArray();
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
        public void Apply(string label, Action<IDictionary<string, ContentDocument>> edit)
        {
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("변경 이름이 필요합니다.", nameof(label));
            if (edit == null) throw new ArgumentNullException(nameof(edit));
            Dictionary<string, ContentDocument> draft = Copy(current.Documents);
            edit(draft);
            if (draft.Any(pair => pair.Value == null || pair.Key != pair.Value.Id)) throw new ContentFormatException("문서 ID와 저장 키가 다릅니다.");
            if (!draft.TryGetValue(current.LevelId, out ContentDocument level) || level.Kind != "level") throw new ContentFormatException("선택한 레벨을 변경에서 제거할 수 없습니다.");
            if (Equal(draft, current.Documents)) return;
            undo.Push(Capture()); redo.Clear();
            // 콜백이 보관한 사본으로 나중에 세션을 변경하지 못하게 한다.
            current.Documents = Copy(draft);
        }
        public void Undo() { if (undo.Count == 0) return; redo.Push(Capture()); current = undo.Pop(); }
        public void Redo() { if (redo.Count == 0) return; undo.Push(Capture()); current = redo.Pop(); }
        public void Discard()
        {
            string levelId = saved.ContainsKey(current.LevelId) ? current.LevelId :
                savedLevelId;
            current = new State { Documents = Copy(saved), LevelId = levelId, Cells = Array.Empty<int>() };
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
            saved = Copy(current.Documents); savedLevelId = current.LevelId;
        }
        private State Capture() => new State { Documents = Copy(current.Documents), LevelId = current.LevelId, Cells = (int[])current.Cells.Clone() };
        private static ContentDocument Clone(ContentDocument doc) => new ContentDocument(doc.Kind, doc.Id, doc.Data);
        private static Dictionary<string, ContentDocument> Copy(Dictionary<string, ContentDocument> docs) => docs.ToDictionary(pair => pair.Key, pair => Clone(pair.Value), StringComparer.Ordinal);
        private static bool Equal(Dictionary<string, ContentDocument> left, Dictionary<string, ContentDocument> right)
        {
            if (left.Count != right.Count) return false;
            foreach (var pair in left)
            {
                if (!right.TryGetValue(pair.Key, out ContentDocument other) || pair.Value.Kind != other.Kind) return false;
                JObject a = pair.Value.Data, b = other.Data;
                // 세대별 파일 경로는 저장소가 정하므로 사용자 편집의 dirty 비교에서 제외한다.
                if (pair.Value.Kind == "project")
                {
                    a = (JObject)a.DeepClone(); b = (JObject)b.DeepClone();
                    a.Remove("documents"); b.Remove("documents");
                }
                if (!JToken.DeepEquals(a, b)) return false;
            }
            return true;
        }
    }
}
#endif
