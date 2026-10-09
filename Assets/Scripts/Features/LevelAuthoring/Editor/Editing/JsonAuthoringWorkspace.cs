using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using Levels;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Object = UnityEngine.Object;

namespace LevelAuthoring.Editor
{
    // 기존 Editor UI를 위한 표시 객체만 소유한다. 저장 원본과 이력은 Session이다.
    public sealed class JsonAuthoringWorkspace : IDisposable
    {
        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>
        {
            { "level", typeof(LevelDefinition) }, { "element", typeof(Elements.ElementDefinitionAsset) },
            { "catalog", typeof(Elements.ElementCatalogAsset) }, { "visual", typeof(Elements.ElementVisualCatalogAsset) },
            { "tutorialFlow", typeof(Tutorial.TutorialFlowDefinition) },
            { "tutorialSample", typeof(Tutorial.TutorialUserSampleDefinition) }, { "shape", typeof(Levels.Editor.LevelShapePreset) }
        };
        private readonly Dictionary<string, ScriptableObject> objects = new Dictionary<string, ScriptableObject>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> kinds = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, JObject> displayed = new Dictionary<string, JObject>(StringComparer.Ordinal);
        private int editDepth;
        private Dictionary<string, string> resourcePaths;
        private Dictionary<string, string> resourceIds;
        private readonly string projectId;
        private bool restoring;
        private bool disposed;
        public AuthoringEditSession Session { get; }
        public LevelDefinition Level => (LevelDefinition)Resolve(Session.SelectedLevelId);
        public JsonAuthoringWorkspace(AuthoringEditSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            projectId = session.Documents.Single(doc => doc.Kind == "project").Id;
            try { RefreshResources(); Resolve(session.SelectedLevelId); }
            catch { Dispose(); throw; }
        }
        public bool Owns(Object value) => value != null && objects.Values.Any(item => item == value);
        public ScriptableObject Resolve(string id)
        {
            if (disposed) throw new ObjectDisposedException(nameof(JsonAuthoringWorkspace));
            if (objects.TryGetValue(id, out ScriptableObject result)) return result;
            ContentDocument document = Session.Get(id);
            if (!Types.TryGetValue(document.Kind, out Type type)) throw new ContentFormatException("표시할 수 없는 문서 종류: " + document.Kind);
            kinds[id] = document.Kind;
            var value = UnityAuthoringCodec.ReadDraft(document, type, Resolve, ResourcePath, item => objects.Add(id, item));
            displayed[id] = UnityAuthoringCodec.WriteDraft(value, document.Kind, id, DocumentId, ResourceId).Data;
            return value;
        }
        private void RefreshResources()
        {
            var resources = Session.Get(projectId).Data["resources"];
            resourcePaths = resources.ToDictionary(item => (string)item["id"], item => (string)item["path"], StringComparer.Ordinal);
            // 복수 ID가 같은 리소스 경로를 가리킬 때도 최초 등록 ID로 안정적으로 기록한다.
            resourceIds = resources.GroupBy(item => (string)item["path"], StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => (string)group.First()["id"], StringComparer.Ordinal);
        }
        private string ResourcePath(string id) => resourcePaths.TryGetValue(id, out string path)
            ? path : throw new ContentFormatException("없는 리소스 ID: " + id);
        private string ResourceId(string path) => resourceIds.TryGetValue(path, out string id)
            ? id : throw new ContentFormatException("작업 폴더에 등록되지 않은 리소스: " + path);
        public string DocumentId(Object value)
        {
            foreach (var pair in objects) if (pair.Value == value) return pair.Key;
            throw new ContentFormatException("JSON 작업 폴더에 없는 에셋 참조입니다.");
        }
        public string Register(ScriptableObject value, string kind)
        {
            if (editDepth == 0) throw new InvalidOperationException("새 문서는 Execute 트랜잭션 안에서 등록하세요.");
            if (value == null || EditorUtility.IsPersistent(value) || Owns(value) || !Types.TryGetValue(kind, out Type type) || value.GetType() != type)
                throw new ContentFormatException("등록할 표시 사본의 문서 종류가 잘못됐습니다.");
            string id = kind + "-" + Guid.NewGuid().ToString("N");
            objects.Add(id, value); kinds.Add(id, kind); value.hideFlags = HideFlags.HideAndDontSave;
            return id;
        }
        public void Execute(string label, Action action)
        {
            if (disposed) throw new ObjectDisposedException(nameof(JsonAuthoringWorkspace));
            editDepth++;
            try { action(); Capture(label); }
            catch { RestoreDisplay(); throw; }
            finally { editDepth--; }
        }
        public void Capture(string label)
        {
            if (restoring) return;
            if (disposed) throw new ObjectDisposedException(nameof(JsonAuthoringWorkspace));
            try
            {
                RefreshResources();
                var changes = objects.Select(pair => UnityAuthoringCodec.WriteDraft(pair.Value,
                    kinds[pair.Key], pair.Key, DocumentId, ResourceId)).ToArray();
                Session.Apply(label, docs =>
                {
                    foreach (ContentDocument change in changes)
                    {
                        JObject data = docs.TryGetValue(change.Id, out ContentDocument original) && displayed.TryGetValue(change.Id, out JObject previous)
                            ? (JObject)MergeDisplayChanges(original.Data, previous, change.Data) : change.Data;
                        docs[change.Id] = new ContentDocument(change.Kind, change.Id, data);
                    }
                });
                foreach (ContentDocument change in changes) displayed[change.Id] = change.Data;
            }
            catch { RestoreDisplay(); throw; }
            ClearOwnedUndo();
        }
        public void Undo() { Session.Undo(); RestoreDisplay(); }
        public void Redo() { Session.Redo(); RestoreDisplay(); }
        public void RestoreDisplay()
        {
            if (disposed) throw new ObjectDisposedException(nameof(JsonAuthoringWorkspace));
            restoring = true;
            try
            {
                RefreshResources();
                var documents = Session.Documents.ToDictionary(doc => doc.Id, StringComparer.Ordinal);
                foreach (var pair in objects.ToArray())
                {
                    if (!documents.TryGetValue(pair.Key, out ContentDocument document))
                    {
                        UndoClear(pair.Value); Object.DestroyImmediate(pair.Value); objects.Remove(pair.Key); kinds.Remove(pair.Key); displayed.Remove(pair.Key); continue;
                    }
                    ScriptableObject temporary = null;
                    try
                    {
                        UnityAuthoringCodec.ReadDraft(document, pair.Value.GetType(), Resolve, ResourcePath, value => temporary = value);
                        // UI 콜백이 보관한 표시 객체의 식별자는 유지한다.
                        EditorUtility.CopySerialized(temporary, pair.Value);
                    }
                    finally { if (temporary != null) Object.DestroyImmediate(temporary); }
                }
                Resolve(Session.SelectedLevelId);
                foreach (var pair in objects)
                    displayed[pair.Key] = UnityAuthoringCodec.WriteDraft(pair.Value, kinds[pair.Key], pair.Key, DocumentId, ResourceId).Data;
                ClearOwnedUndo();
            }
            finally { restoring = false; }
        }
        // Unity CopySerialized는 null 배열을 빈 배열로 표시할 수 있다.
        // 표시 갱신만으로 원본/Undo가 바뀌지 않게 실제 편집한 값만 원문에 합친다.
        private static JToken MergeDisplayChanges(JToken original, JToken before, JToken after)
        {
            if (JToken.DeepEquals(before, after)) return original.DeepClone();
            if (original is JObject source && before is JObject oldObject && after is JObject newObject)
            {
                var result = (JObject)source.DeepClone();
                foreach (var field in newObject.Properties())
                    result[field.Name] = source[field.Name] != null && oldObject[field.Name] != null
                        ? MergeDisplayChanges(source[field.Name], oldObject[field.Name], field.Value) : field.Value.DeepClone();
                return result;
            }
            if (original is JArray sourceArray && before is JArray oldArray && after is JArray newArray &&
                sourceArray.Count == oldArray.Count && oldArray.Count == newArray.Count)
                return new JArray(newArray.Select((value, index) => MergeDisplayChanges(sourceArray[index], oldArray[index], value)));
            return after.DeepClone();
        }
        private static void UndoClear(Object value)
        {
            UnityEditor.Undo.ClearUndo(value);
            EditorUtility.ClearDirty(value);
        }
        private void ClearOwnedUndo()
        {
            // RecordObject의 지연 기록을 먼저 확정하고 소유 표시 객체의 기록만 지운다.
            UnityEditor.Undo.FlushUndoRecordObjects();
            foreach (ScriptableObject value in objects.Values) UndoClear(value);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ClearOwnedUndo();
            foreach (ScriptableObject value in objects.Values.Reverse()) if (value != null) Object.DestroyImmediate(value);
            objects.Clear(); kinds.Clear(); displayed.Clear();
        }
    }
}
