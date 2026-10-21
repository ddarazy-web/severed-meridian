#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using LevelAuthoring.Documents;

namespace LevelAuthoring.Validation
{
    public static class ContentDocumentValidator
    {
        private static readonly JObject Schema = JObject.Parse(AuthoringSchema.Json);
        public static void ValidateElementPlacement(JObject placement)
        {
            if (placement == null) throw new ContentFormatException("붙여넣을 요소가 없습니다.");
            ValidateValue(placement, new JObject { ["type"] = "Levels.ElementPlacementDefinition", ["nullable"] = false }, "elements");
        }
        public static void Validate(ContentDocument document)
        {
            try { ValidateDocument(document); }
            catch (ContentFormatException error) { throw new ContentFormatException(Context(document) + " · " + error.Message, error); }
        }

        // 부분 복원 대상의 참조만 확인한다. 다른 작성 중 문서의 오류 때문에 복원을 막지 않는다.
        internal static void ValidateDocumentReferences(ContentDocument document, Dictionary<string, ContentDocument> documents)
        {
            if (document.Kind == "project")
            {
                CheckReference(document.Data["defaultCatalogId"], "catalog", documents, "project.defaultCatalogId");
                return;
            }
            string type = (string)Schema["roots"][document.Kind];
            References(document.Data, new JObject { ["type"] = type }, documents, Context(document));
        }

        internal static string Context(ContentDocument document) => document == null ? "빈 문서" :
            document.Id + (document.Kind == "level" ? " · 레벨 " + document.Data["levelNumber"] : " · " + document.Kind);

        private static void ValidateDocument(ContentDocument document)
        {
            if (document == null || string.IsNullOrWhiteSpace(document.Id)) throw new ContentFormatException("문서 ID가 없습니다.");
            if (document.Kind == "project") { ValidateProject(document.Data); return; }
            string type = (string)Schema["roots"][document.Kind];
            if (type == null) throw new ContentFormatException("지원하지 않는 문서 종류: " + document.Kind);
            ValidateValue(document.Data, new JObject { ["type"] = type, ["nullable"] = false }, document.Kind + ".data");
            if (document.Kind == "shape" && (document.Data["cells"] as JArray)?.Count != 81)
                throw new ContentFormatException("shape.cells는 9×9의 81칸이어야 합니다.");
            if (document.Kind == "level")
            {
                if ((int)document.Data["schemaVersion"] < 1 || (int)document.Data["schemaVersion"] > 5)
                    throw new ContentFormatException("지원하지 않는 레벨 저장 형식 버전입니다.");
                var board = document.Data["board"] as JObject;
                if (board == null || (int)board["rows"] != 9 || (int)board["columns"] != 9 || (board["cells"] as JArray)?.Count != 81)
                    throw new ContentFormatException("level.board는 9×9의 81칸이어야 합니다.");
                if ((int)document.Data["levelNumber"] < 1 || (int)document.Data["moveCount"] < 0)
                    throw new ContentFormatException("레벨 번호/이동 수가 잘못됐습니다.");
            }
            if (document.Kind == "catalog" && (!(document.Data["definitionIds"] is JArray memberIds) || memberIds.Any(id => id.Type != JTokenType.String)))
                throw new ContentFormatException("catalog.definitionIds에는 null 목록이나 null 항목을 허용하지 않습니다.");
            if (document.Kind == "element" && string.IsNullOrWhiteSpace((string)document.Data["definition"]?["id"]))
                throw new ContentFormatException("element.definition.id가 없습니다.");
        }

        private static void ValidateProject(JObject data)
        {
            string[] fields = { "name", "contentVersion", "defaultCatalogId", "resources", "documents", "sourceIds" };
            RequireFields(data, fields, "project.data");
            if (data["name"].Type != JTokenType.String || data["contentVersion"].Type != JTokenType.Integer ||
                data["contentVersion"].Value<long>() < 1 || data["contentVersion"].Value<long>() > int.MaxValue ||
                !(data["resources"] is JArray resources) || (data["defaultCatalogId"].Type != JTokenType.String && data["defaultCatalogId"].Type != JTokenType.Null))
                throw new ContentFormatException("project.data 값 형식이 잘못됐습니다.");
            ValidatePairs(data["documents"], "id", "path", "documents", true);
            ValidatePairs(data["sourceIds"], "sourceGuid", "documentId", "sourceIds", false);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken item in resources)
            {
                if (!(item is JObject resource)) throw new ContentFormatException("리소스 매핑은 객체여야 합니다.");
                RequireFields(resource, new[] { "id", "path" }, "project.resources");
                if (resource["id"].Type != JTokenType.String || resource["path"].Type != JTokenType.String ||
                    string.IsNullOrWhiteSpace((string)resource["id"]) || string.IsNullOrWhiteSpace((string)resource["path"]) || !ids.Add((string)resource["id"]))
                    throw new ContentFormatException("리소스 ID/경로가 없거나 중복됐습니다.");
            }
        }

        private static void ValidatePairs(JToken value, string first, string second, string path, bool paths)
        {
            if (!(value is JArray array)) throw new ContentFormatException(path + ": 목록이 필요합니다.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var values = new HashSet<string>(paths ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (var item in array)
            {
                if (!(item is JObject pair)) throw new ContentFormatException(path + ": 객체가 필요합니다.");
                RequireFields(pair, new[] { first, second }, path);
                if (pair[first].Type != JTokenType.String || pair[second].Type != JTokenType.String ||
                    string.IsNullOrWhiteSpace((string)pair[first]) || string.IsNullOrWhiteSpace((string)pair[second]) ||
                    !keys.Add((string)pair[first]) || !values.Add((string)pair[second]))
                    throw new ContentFormatException(path + ": 빈 값 또는 중복된 대응입니다.");
                if (paths) Storage.JsonContentStore.ValidateRelativePath((string)pair[second]);
                else if (!System.Text.RegularExpressions.Regex.IsMatch((string)pair[first], @"\A[a-f0-9]{32}\z"))
                    throw new ContentFormatException("sourceIds.sourceGuid 형식이 잘못됐습니다.");
            }
        }

        public static void ValidateSnapshot(IEnumerable<ContentDocument> documents) => ValidateSnapshot(documents, false);
        internal static void ValidateSnapshot(IEnumerable<ContentDocument> documents, bool documentsValidated)
        {
            var byId = new Dictionary<string, ContentDocument>(StringComparer.Ordinal);
            var numbers = new HashSet<int>();
            var elements = new HashSet<string>(StringComparer.Ordinal);
            foreach (var storedDocument in documents)
            {
                ContentDocument document = documentsValidated ? storedDocument : storedDocument?.Materialize();
                if (!documentsValidated || document == null) Validate(document);
                if (byId.ContainsKey(document.Id)) throw new ContentFormatException("중복 문서 ID: " + document.Id);
                byId.Add(document.Id, storedDocument);
                if (document.Kind == "level" && !numbers.Add(document.Info.LevelNumber)) throw new ContentFormatException(Context(document) + " · levelNumber: 중복 레벨 번호입니다.");
                if (document.Kind == "element" && !elements.Add((string)document.Data["definition"]["id"])) throw new ContentFormatException("중복 게임 요소 ID입니다.");
            }
            var projects = byId.Values.Where(document => document.Kind == "project").ToArray();
            if (projects.Length != 1) throw new ContentFormatException("스냅샷에는 project 문서가 하나 필요합니다.");
            CheckReference(projects[0].Data["defaultCatalogId"], "catalog", byId, "project.defaultCatalogId");
            var resources = new HashSet<string>(((JArray)projects[0].Data["resources"]).Select(item => (string)item["id"]), StringComparer.Ordinal);
            // 레벨이 참조하는 공유 문서의 참조를 먼저 검사해야 의미 검사에서 누락된 키를 조회하지 않는다.
            foreach (var storedDocument in byId.Values.Where(document => document.Kind != "project").OrderBy(document => document.Kind == "level"))
            {
                ContentDocument document = storedDocument.Materialize();
                References(document.Data, new JObject { ["type"] = (string)Schema["roots"][document.Kind] }, byId, Context(document) + ".data");
                if (document.Kind == "visual")
                    foreach (var property in document.Data.Descendants().OfType<JProperty>().Where(property => property.Name == "resourceId" || property.Name == "effectResourceIds"))
                        foreach (var reference in property.Value is JArray list ? list.ToArray() : new[] { property.Value })
                            if (reference.Type != JTokenType.Null && !string.IsNullOrEmpty((string)reference) && !resources.Contains((string)reference))
                                throw new ContentFormatException(Context(document) + " · " + property.Path + ": 없는 리소스 ID " + reference);
                ContentReferenceValidator.Validate(document, byId);
            }
        }

        internal static IEnumerable<string> ReferencedDocumentIds(ContentDocument document)
        {
            Validate(document);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (document.Kind == "project")
            {
                string catalog = (string)document.Data["defaultCatalogId"];
                if (catalog != null) ids.Add(catalog);
            }
            else VisitReferences(document.Data, new JObject { ["type"] = (string)Schema["roots"][document.Kind] }, Context(document),
                (value, kind, path) => ids.Add((string)value));
            return ids;
        }

        private static void References(JToken value, JObject spec, Dictionary<string, ContentDocument> documents, string path) =>
            VisitReferences(value, spec, path, (reference, kind, location) => CheckReference(reference, kind, documents, location));

        private static void VisitReferences(JToken value, JObject spec, string path, Action<JToken, string, string> visit)
        {
            if (value.Type == JTokenType.Null) return;
            string type = (string)spec["type"];
            if (type == "reference") { visit(value, (string)spec["kind"], path); return; }
            if (type == "array")
            {
                int index = 0;
                foreach (var item in (JArray)value) VisitReferences(item, (JObject)spec["items"], path + "[" + index++ + "]", visit);
                return;
            }
            if (Schema["types"][type]?["fields"] is JObject fields)
                foreach (var field in fields.Properties()) VisitReferences(value[field.Name], (JObject)field.Value, path + "." + field.Name, visit);
        }

        private static void CheckReference(JToken value, string kind, Dictionary<string, ContentDocument> documents, string path)
        {
            if (value.Type == JTokenType.Null) return;
            if (!documents.TryGetValue((string)value, out var target))
                throw new ContentFormatException(path + ": 참조가 없습니다. 기대 종류: " + kind);
            if (target.Kind == "unknown") target.EnsureLoaded();
            if (target.Kind != kind)
                throw new ContentFormatException(path + ": 참조가 없거나 종류가 다릅니다. 기대 종류: " + kind);
        }

        private static void ValidateValue(JToken value, JObject spec, string path)
        {
            if (value.Type == JTokenType.Null)
            {
                if ((bool?)spec["nullable"] == true) return;
                throw new ContentFormatException(path + ": null을 허용하지 않습니다.");
            }
            string type = (string)spec["type"];
            switch (type)
            {
                case "string": if (value.Type == JTokenType.String) return; break;
                case "reference": if (value.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)value)) return; break;
                case "boolean": if (value.Type == JTokenType.Boolean) return; break;
                case "integer": if (value.Type == JTokenType.Integer && value.Value<double>() >= int.MinValue && value.Value<double>() <= int.MaxValue) return; break;
                case "number":
                    if ((value.Type == JTokenType.Integer || value.Type == JTokenType.Float) && !double.IsNaN(value.Value<double>()) && Math.Abs(value.Value<double>()) <= float.MaxValue) return;
                    break;
                case "array":
                    if (!(value is JArray array)) break;
                    for (int index = 0; index < array.Count; index++) ValidateValue(array[index], (JObject)spec["items"], path + "[" + index + "]");
                    return;
                default:
                    var definition = (JObject)Schema["types"][type];
                    if (definition == null) throw new ContentFormatException(path + ": 스키마 타입이 없습니다: " + type);
                    if (definition["enum"] is JObject keys)
                    {
                        if (value.Type == JTokenType.String && keys.Property((string)value) != null) return;
                        throw new ContentFormatException(path + ": 지원하지 않는 enum 키: " + value);
                    }
                    if (!(value is JObject obj)) break;
                    var fields = (JObject)definition["fields"];
                    RequireFields(obj, fields.Properties().Select(field => field.Name).ToArray(), path);
                    foreach (var field in fields.Properties()) ValidateValue(obj[field.Name], (JObject)field.Value, path + "." + field.Name);
                    return;
            }
            throw new ContentFormatException(path + ": 값 형식이 잘못됐습니다. 기대 형식: " + type);
        }

        private static void RequireFields(JObject value, string[] fields, string path)
        {
            string unknown = value.Properties().Select(property => property.Name).FirstOrDefault(name => !fields.Contains(name));
            string missing = fields.FirstOrDefault(name => value.Property(name) == null);
            if (unknown != null || missing != null) throw new ContentFormatException(path + ": " + (unknown != null ? "미지원 필드 " + unknown : "필수 필드 누락 " + missing));
        }
    }
}
#endif
