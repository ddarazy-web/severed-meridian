#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Elements;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using Levels;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LevelTool
{
    public static class LevelToolDocuments
    {
        public static JObject UpgradePreview(JObject original)
        {
            if ((int)original["schemaVersion"] == 5) return original;
            if ((int)original["schemaVersion"] != 4) throw new ContentFormatException("현재 도구는 레벨 형식 4·5를 지원합니다.");
            List<ScriptableObject> owned = new List<ScriptableObject>();
            try
            {
                // 구형 배치/공급 변환에는 공유 문서 객체가 필요하지 않다. 이 임시 입력의 참조만 비우고
                // 결과는 원문에 세 필드만 합쳐 공유 카탈로그·튜토리얼 참조를 그대로 보존한다.
                JObject input = (JObject)original.DeepClone();
                input["catalogId"] = null;
                if (input["tutorial"] is JObject tutorial) tutorial["flowId"] = null;
                LevelDefinition source = (LevelDefinition)UnityAuthoringCodec.ReadDraft(new ContentDocument("level", "preview", input),
                    typeof(LevelDefinition), _ => null, _ => null, owned.Add);
                ElementPlacementDefinition[] items = LegacyElementLevelAdapter.Preview(source);
                foreach (ElementPlacementDefinition item in items)
                    if (string.IsNullOrEmpty(item.instanceId)) item.instanceId = Guid.NewGuid().ToString("N");
                JObject patch = JObject.Parse(JsonUtility.ToJson(new PlacementList { elements = items }));
                patch["schemaVersion"] = 5;
                patch["elementSupply"] = JObject.Parse(JsonUtility.ToJson(ElementLevelSupplyDefinition.FromLegacy(source.Supply)));
                JsonUtility.FromJsonOverwrite(patch.ToString(), source);
                JObject converted = UnityAuthoringCodec.WriteDraft(source, "level", "preview", _ => null, _ => null).Data;
                JObject result = (JObject)original.DeepClone();
                foreach (string field in new[] { "schemaVersion", "elements", "elementSupply" }) result[field] = converted[field].DeepClone();
                return result;
            }
            finally { foreach (ScriptableObject value in owned) Release(value); }
        }
        [Serializable] private sealed class PlacementList { public ElementPlacementDefinition[] elements; }

        public static JObject NewLevel()
        {
            LevelDefinition source = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                JObject data = UnityAuthoringCodec.WriteDraft(source, "level", "template", _ => null, _ => null).Data;
                data["schemaVersion"] = 5;
                SupplyDocumentEditing.PlaceSources(data, Enumerable.Range(0, 9), false);
                return data;
            }
            finally { Release(source); }
        }

        public static ContentSnapshot NewProject()
        {
            ContentDocument project = new ContentDocument("project", "project-" + Guid.NewGuid().ToString("N"), new JObject
            {
                ["name"] = "레벨 작업", ["contentVersion"] = 1, ["defaultCatalogId"] = null,
                ["resources"] = new JArray(), ["documents"] = new JArray(), ["sourceIds"] = new JArray()
            });
            JObject level = NewLevel();
            level["displayName"] = "새 레벨";
            return new ContentSnapshot(new[] { project, new ContentDocument("level", "level-" + Guid.NewGuid().ToString("N"), level) });
        }

        public static Dictionary<string, JObject> Definitions(AuthoringEditSession session)
        {
            JObject level = session.Get(session.SelectedLevelId).Data;
            string catalogId = (string)level["catalogId"];
            if (catalogId == null && level["embeddedDefinitions"] is JArray embedded && embedded.Count > 0)
                return embedded.Cast<JObject>().ToDictionary(value => (string)value["id"], value => value, StringComparer.Ordinal);
            // 실행 파일과 같은 기본 정의를 보충하고, 같은 ID의 제작 정의가 우선한다.
            // Editor 정적 공급자는 사용하지 않아 SO가 없는 도구에서도 같은 결과를 얻는다.
            Dictionary<string, JObject> result = new Dictionary<string, JObject>(StringComparer.Ordinal);
            foreach (ElementDefinition definition in LegacyElementDefinitions.DefaultCatalog.Definitions)
            {
                ElementDefinitionAsset asset = ScriptableObject.CreateInstance<ElementDefinitionAsset>();
                try
                {
                    JsonUtility.FromJsonOverwrite("{\"definition\":" + JsonUtility.ToJson(PackedElementDefinition.FromDefinition(definition)) + "}", asset);
                    JObject value = (JObject)UnityAuthoringCodec.WriteDraft(asset, "element", "template", _ => null, _ => null).Data["definition"];
                    result.Add(definition.Id.Value, value);
                }
                finally { Release(asset); }
            }
            if (catalogId != null)
                foreach (string id in session.Get(catalogId).Data["definitionIds"].Values<string>())
                {
                    JObject definition = (JObject)session.Get(id).Data["definition"];
                    result[(string)definition["id"]] = definition;
                }
            return result;
        }

        public static string Layer(JObject definition)
        {
            if ((string)definition["reaction"] != "Unspecified") return "Obstacle";
            if ((bool?)definition["layer"]?["enabled"] == true)
                return (string)definition["layer"]["behavior"] == "NormalConsumption" ? "Dust" : "Cover";
            if ((bool?)definition["supply"]?["enabled"] == true &&
                (string)definition["supply"]["behavior"] != "Obstacle" && (string)definition["supply"]["behavior"] != "RandomPower") return "Block";
            return null;
        }

        public static int Size(JObject definition) => (bool?)definition["charge"]?["enabled"] == true ?
            (int)definition["charge"]["size"] : (bool?)definition["placement"]?["enabled"] == true ? (int)definition["placement"]["size"] : 1;

        private static void Release(UnityEngine.Object value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
#endif
