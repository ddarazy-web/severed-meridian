using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LevelAuthoring.Documents;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LevelAuthoring.Editor
{
    public sealed class LegacyContentSource
    {
        public string Guid { get; }
        public string Kind { get; }
        public ScriptableObject Asset { get; }
        public LegacyContentSource(string guid, string kind, ScriptableObject asset)
        { Guid = guid; Kind = kind; Asset = asset; }
    }

    public static class LegacyContentExporter
    {
        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>
        {
            { "level", typeof(Levels.LevelDefinition) }, { "element", typeof(Elements.ElementDefinitionAsset) },
            { "catalog", typeof(Elements.ElementCatalogAsset) }, { "visual", typeof(Elements.ElementVisualCatalogAsset) },
            { "tutorialFlow", typeof(Tutorial.TutorialFlowDefinition) }, { "tutorialSample", typeof(Tutorial.TutorialUserSampleDefinition) },
            { "shape", typeof(Levels.Editor.LevelShapePreset) }
        };
        public static StoredContentSnapshot Export(string trialRoot) => Export(trialRoot, Discover(), null);
        internal static LegacyContentSource[] Discover() => Types.SelectMany(pair =>
            AssetDatabase.FindAssets("t:" + pair.Value.Name, new[] { "Assets" })
                .Select(guid => new LegacyContentSource(guid, pair.Key,
                    AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), pair.Value) as ScriptableObject)))
            .Where(source => source.Asset != null).OrderBy(source => source.Guid, StringComparer.Ordinal).ToArray();

        internal static StoredContentSnapshot Export(string trialRoot, IReadOnlyList<LegacyContentSource> sources, Action beforePublish)
        {
            string root = Path.GetFullPath(trialRoot);
            string allowed = Path.GetFullPath("ContentData/Trials/game-authoring-stage-02") + Path.DirectorySeparatorChar;
            if (!root.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                throw new ContentFormatException("내보내기는 2단계 시험 폴더의 명시적 하위 폴더만 허용합니다.");
            var repository = new ContentSnapshotStore(root);
            StoredContentSnapshot previous = null;
            try { previous = repository.Read(); }
            catch (FileNotFoundException) when (!File.Exists(Path.Combine(root, "project.json"))) { }
            catch (DirectoryNotFoundException) when (!Directory.Exists(root)) { }

            var old = previous?.Snapshot.Project;
            var mapping = old == null ? new Dictionary<string, string>(StringComparer.Ordinal) :
                ((JArray)old.Data["sourceIds"]).ToDictionary(item => (string)item["sourceGuid"], item => (string)item["documentId"], StringComparer.Ordinal);
            var objectIds = new Dictionary<Object, string>();
            foreach (var source in sources)
            {
                if (source.Asset == null || !Types.TryGetValue(source.Kind, out var type) || source.Asset.GetType() != type)
                    throw new ContentFormatException("지원하지 않는 제작 원본입니다.");
                if (!mapping.TryGetValue(source.Guid, out var id))
                    mapping.Add(source.Guid, id = source.Kind + "-" + Guid.NewGuid().ToString("N"));
                if (objectIds.ContainsKey(source.Asset)) throw new ContentFormatException("중복 제작 원본입니다.");
                objectIds.Add(source.Asset, id);
            }

            var resources = new Dictionary<string, string>(StringComparer.Ordinal);
            string Resource(string path)
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                string id;
                if (!string.IsNullOrEmpty(guid)) id = "resource-" + guid;
                else
                    using (var hash = SHA256.Create()) id = "resource-" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(path))).Replace("-", "").ToLowerInvariant();
                if (resources.TryGetValue(id, out var existing) && existing != path) throw new ContentFormatException("리소스 ID 충돌입니다.");
                resources[id] = path;
                return id;
            }
            string Reference(Object value)
            {
                if (!objectIds.TryGetValue(value, out var id)) throw new ContentFormatException("내보낼 목록에 없는 원본 참조: " + value.name);
                return id;
            }
            var documents = sources.Select(source => UnityAuthoringCodec.Write(source.Asset, source.Kind, objectIds[source.Asset], Reference, Resource)).ToList();
            string defaultCatalog = old == null ? null : (string)old.Data["defaultCatalogId"];
            if (defaultCatalog == null || !documents.Any(document => document.Id == defaultCatalog && document.Kind == "catalog"))
                defaultCatalog = documents.FirstOrDefault(document => document.Kind == "catalog")?.Id;
            var project = new ContentDocument("project", old?.Id ?? "project-" + Guid.NewGuid().ToString("N"), new JObject
            {
                ["name"] = old?.Data["name"]?.DeepClone() ?? new JValue("ServeredMeridian"),
                ["contentVersion"] = old?.Data["contentVersion"]?.DeepClone() ?? new JValue(1),
                ["defaultCatalogId"] = defaultCatalog,
                ["documents"] = new JArray(),
                ["resources"] = new JArray(resources.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new JObject { ["id"] = pair.Key, ["path"] = pair.Value })),
                // 공개본과 함께 기록하므로 실패한 내보내기가 다음 작업의 ID를 바꾸지 않는다.
                ["sourceIds"] = new JArray(mapping.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new JObject { ["sourceGuid"] = pair.Key, ["documentId"] = pair.Value }))
            });
            documents.Add(project);
            var snapshot = new ContentSnapshot(documents);
            beforePublish?.Invoke();
            return repository.Publish(snapshot, previous?.Hash);
        }
    }
}
