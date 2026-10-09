#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;
using UnityEngine;
using LevelAuthoring.Documents;
using LevelAuthoring.Validation;

namespace LevelAuthoring.Runtime
{
    public static class UnityAuthoringCodec
    {
        private static readonly JObject Schema = JObject.Parse(AuthoringSchema.Json);
        public static ContentDocument Write(ScriptableObject source, string kind, string id,
            Func<UnityEngine.Object, string> documentId, Func<string, string> resourceId)
        {
            ContentDocument result = WriteDraft(source, kind, id, documentId, resourceId);
            ContentDocumentValidator.Validate(result);
            return result;
        }

        // 편집 사본은 잘못된 수치도 보존한다. 공개 저장과 실행에는 Write/ContentSnapshot을 사용한다.
        public static ContentDocument WriteDraft(ScriptableObject source, string kind, string id,
            Func<UnityEngine.Object, string> documentId, Func<string, string> resourceId)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            string type = (string)Schema["roots"][kind];
            if (type != source.GetType().FullName) throw new ContentFormatException("원본 종류와 JSON 계약이 다릅니다: " + kind);
            var data = WriteObject(source, source.GetType(), type, documentId, resourceId);
            data["displayName"] = source.name;
            return new ContentDocument(kind, id, data);
        }

        public static ScriptableObject Read(ContentDocument document, Type rootType,
            Func<string, ScriptableObject> reference, Func<string, string> resourcePath, Action<ScriptableObject> own)
        {
            ContentDocumentValidator.Validate(document);
            return ReadDraft(document, rootType, reference, resourcePath, own);
        }

        public static ScriptableObject ReadDraft(ContentDocument document, Type rootType,
            Func<string, ScriptableObject> reference, Func<string, string> resourcePath, Action<ScriptableObject> own)
        {
            if ((string)Schema["roots"][document.Kind] != rootType.FullName) throw new ContentFormatException("요청 타입과 문서 종류가 다릅니다.");
            if (own == null) throw new ArgumentNullException(nameof(own));
            var result = ScriptableObject.CreateInstance(rootType);
            own(result);
            result.hideFlags = HideFlags.HideAndDontSave;
            result.name = (string)document.Data["displayName"];
            Populate(result, rootType, document.Data, rootType.FullName, reference, resourcePath);
            return result;
        }

        private static JObject WriteObject(object value, Type type, string schemaType,
            Func<UnityEngine.Object, string> reference, Func<string, string> resource)
        {
            var fields = SerializedAuthoringFields.For(type);
            var specs = (JObject)Schema["types"][schemaType]["fields"];
            if (fields.Any(field => specs.Property(SerializedAuthoringFields.Name(field)) == null))
                throw new ContentFormatException("제작 타입에 v1이 지원하지 않는 필드가 있습니다: " + type.FullName);
            var result = new JObject();
            foreach (var field in fields)
            {
                string name = SerializedAuthoringFields.Name(field);
                object content = field.GetValue(value);
                if (name == "resourceId" && content is string path && path.Length > 0) content = resource(path);
                if (name == "effectResourceIds" && content is string[] paths) content = paths.Select(path => string.IsNullOrEmpty(path) ? path : resource(path)).ToArray();
                result[name] = WriteValue(content, field.FieldType, (JObject)specs[name], reference, resource);
            }
            return result;
        }

        private static JToken WriteValue(object value, Type type, JObject spec,
            Func<UnityEngine.Object, string> reference, Func<string, string> resource)
        {
            if (value == null || value is UnityEngine.Object unity && unity == null) return JValue.CreateNull();
            string key = (string)spec["type"];
            if (key == "reference") return new JValue(reference((UnityEngine.Object)value));
            if (key == "array")
            {
                var array = new JArray();
                Type itemType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                foreach (var item in (IEnumerable)value) array.Add(WriteValue(item, itemType, (JObject)spec["items"], reference, resource));
                return array;
            }
            if (Schema["types"][key] is JObject definition)
            {
                if (definition["enum"] is JObject keys)
                {
                    int number = Convert.ToInt32(value);
                    var entry = keys.Properties().FirstOrDefault(property => (int)property.Value == number);
                    if (entry == null) throw new ContentFormatException("지원하지 않는 enum 값: " + key + "=" + number);
                    return new JValue(entry.Name);
                }
                return WriteObject(value, type, key, reference, resource);
            }
            return new JValue(value);
        }

        private static void Populate(object value, Type type, JObject data, string schemaType,
            Func<string, ScriptableObject> reference, Func<string, string> resource)
        {
            var specs = (JObject)Schema["types"][schemaType]["fields"];
            foreach (var field in SerializedAuthoringFields.For(type))
            {
                string name = SerializedAuthoringFields.Name(field);
                JToken content = data[name];
                if (name == "resourceId" && content.Type == JTokenType.String && ((string)content).Length > 0) content = new JValue(resource((string)content));
                if (name == "effectResourceIds" && content is JArray paths) content = new JArray(paths.Select(path => path.Type == JTokenType.Null || string.IsNullOrEmpty((string)path) ? path.DeepClone() : new JValue(resource((string)path))));
                field.SetValue(value, ReadValue(content, field.FieldType, (JObject)specs[name], reference, resource));
            }
        }

        private static object ReadValue(JToken value, Type type, JObject spec,
            Func<string, ScriptableObject> reference, Func<string, string> resource)
        {
            if (value.Type == JTokenType.Null) return null;
            string key = (string)spec["type"];
            if (key == "reference")
            {
                var target = reference((string)value);
                if (target == null || !type.IsInstanceOfType(target)) throw new ContentFormatException("문서 참조 타입이 다릅니다: " + value);
                return target;
            }
            if (key == "array")
            {
                var values = (JArray)value;
                Type itemType = type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0];
                IList result = type.IsArray ? (IList)Array.CreateInstance(itemType, values.Count) : (IList)Activator.CreateInstance(type);
                for (int index = 0; index < values.Count; index++)
                {
                    object item = ReadValue(values[index], itemType, (JObject)spec["items"], reference, resource);
                    if (type.IsArray) result[index] = item; else result.Add(item);
                }
                return result;
            }
            if (Schema["types"][key] is JObject definition)
            {
                if (definition["enum"] is JObject keys)
                {
                    int number = (int)keys[(string)value];
                    return type.IsEnum ? Enum.ToObject(type, number) : (object)number;
                }
                // 저장 필드 전체를 채우므로 생성자가 없는 불변 제작 값도 복원할 수 있다.
                object result = type.IsValueType ? Activator.CreateInstance(type) : FormatterServices.GetUninitializedObject(type);
                Populate(result, type, (JObject)value, key, reference, resource);
                return result;
            }
            return value.ToObject(type);
        }
    }
}
#endif
