#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Documents;
using Levels;
using Tutorial;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LevelAuthoring.Runtime
{
    // JSON 초안을 기존 제작/실행 규칙에 전달하는 임시 객체만 소유한다. 저장과 이력은 세션이 소유한다.
    public sealed class AuthoringObjectGraph : IDisposable
    {
        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>
        {
            { "level", typeof(LevelDefinition) }, { "element", typeof(Elements.ElementDefinitionAsset) },
            { "catalog", typeof(Elements.ElementCatalogAsset) }, { "visual", typeof(Elements.ElementVisualCatalogAsset) },
            { "tutorialFlow", typeof(TutorialFlowDefinition) }, { "tutorialSample", typeof(TutorialUserSampleDefinition) }
        };
        private readonly Dictionary<string, ContentDocument> documents;
        private readonly Dictionary<string, ScriptableObject> objects = new Dictionary<string, ScriptableObject>(StringComparer.Ordinal);
        private readonly HashSet<string> resolving = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<ScriptableObject> owned = new List<ScriptableObject>();
        private readonly Dictionary<string, string> resources;
        private readonly Dictionary<string, string> resourceIds;

        public AuthoringObjectGraph(IEnumerable<ContentDocument> source)
        {
            documents = source.ToDictionary(doc => doc.Id, StringComparer.Ordinal);
            var entries = documents.Values.Single(doc => doc.Kind == "project").Data["resources"];
            resources = entries.ToDictionary(item => (string)item["id"], item => (string)item["path"], StringComparer.Ordinal);
            resourceIds = resources.GroupBy(pair => pair.Value, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);
        }

        public ScriptableObject Resolve(string id)
        {
            if (objects.TryGetValue(id, out var value)) return value;
            if (!documents.TryGetValue(id, out var document)) throw new ContentFormatException("없는 제작 문서: " + id);
            if (!Types.TryGetValue(document.Kind, out Type type)) throw new ContentFormatException("실행용 객체로 변환할 수 없는 문서 종류: " + document.Kind);
            if (!resolving.Add(id)) throw new ContentFormatException("제작 문서 참조가 순환합니다: " + id);
            try
            {
                value = UnityAuthoringCodec.ReadDraft(document, type, Resolve, key => resources[key], owned.Add);
                objects.Add(id, value); return value;
            }
            finally { resolving.Remove(id); }
        }
        public ContentDocument Encode(string id) => UnityAuthoringCodec.WriteDraft(Resolve(id), documents[id].Kind, id, DocumentId, path => resourceIds[path]);
        private string DocumentId(Object value) => objects.FirstOrDefault(pair => pair.Value == value).Key
            ?? throw new ContentFormatException("현재 작업 폴더에 등록되지 않은 문서 참조입니다.");

        public string Register(ScriptableObject value, string kind)
        {
            if (!Types.TryGetValue(kind, out Type type) || value.GetType() != type) throw new ContentFormatException("등록할 문서 종류가 잘못됐습니다.");
            string id = kind + "-" + Guid.NewGuid().ToString("N");
            owned.Add(value); value.hideFlags = HideFlags.HideAndDontSave; objects.Add(id, value);
            documents.Add(id, UnityAuthoringCodec.WriteDraft(value, kind, id, DocumentId, path => resourceIds[path]));
            return id;
        }

        public void Dispose()
        {
            for (int index = owned.Count - 1; index >= 0; index--)
                if (owned[index] != null)
                {
                    if (Application.isPlaying) Object.Destroy(owned[index]);
                    else Object.DestroyImmediate(owned[index]);
                }
            owned.Clear(); objects.Clear();
        }
    }
}
#endif
