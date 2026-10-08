using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using LevelAuthoring.Runtime;

namespace LevelAuthoring.Editor
{
    // 계약을 갱신할 때만 명시 실행한다. 실행 중 원본 타입에서 스키마를 자동 확장하지 않는다.
    public static class JsonSchemaCapture
    {
        private static readonly Dictionary<string, Type> Roots = new Dictionary<string, Type>
        {
            { "level", typeof(Levels.LevelDefinition) }, { "element", typeof(Elements.ElementDefinitionAsset) },
            { "catalog", typeof(Elements.ElementCatalogAsset) }, { "visual", typeof(Elements.ElementVisualCatalogAsset) },
            { "tutorialFlow", typeof(Tutorial.TutorialFlowDefinition) }, { "tutorialSample", typeof(Tutorial.TutorialUserSampleDefinition) },
            { "shape", typeof(Levels.Editor.LevelShapePreset) }
        };
        private static JObject types;
        public static void Run()
        {
            try
            {
                types = new JObject();
                var roots = new JObject();
                foreach (var pair in Roots) { roots[pair.Key] = pair.Value.FullName; Capture(pair.Value, true); }
                var schema = new JObject { ["roots"] = roots, ["types"] = types };
                string json = schema.ToString(Formatting.None).Replace("\"", "\"\"");
                string source = "// v1 계약. 변경 시 버전 호환성 및 외부 도구 영향을 검토한다.\nnamespace LevelAuthoring.Documents\n{\n    public static class AuthoringSchema\n    {\n        public const string Json = @\"" + json + "\";\n    }\n}\n";
                File.WriteAllText("Assets/Scripts/Features/LevelAuthoring/Documents/AuthoringSchema.cs", source, new System.Text.UTF8Encoding(false));
                Directory.CreateDirectory("Logs/GameAuthoringStage02");
                File.WriteAllText("Logs/GameAuthoringStage02/schema-v1.json", schema.ToString(Formatting.Indented));
                Debug.Log("JSON schema types: " + types.Count);
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static JObject Spec(Type type, FieldInfo field = null)
        {
            if (field != null && field.DeclaringType == typeof(Elements.PackedElementDefinition) && field.Name == "reaction")
                type = typeof(Elements.ElementReactionBehavior);
            var spec = new JObject { ["nullable"] = !type.IsValueType };
            if (type == typeof(string)) spec["type"] = "string";
            else if (type == typeof(bool)) spec["type"] = "boolean";
            else if (type == typeof(int)) spec["type"] = "integer";
            else if (type == typeof(float)) spec["type"] = "number";
            else if (type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
            {
                spec["type"] = "array"; spec["items"] = Spec(type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0]);
            }
            else if (typeof(ScriptableObject).IsAssignableFrom(type))
            { spec["type"] = "reference"; spec["kind"] = Roots.Single(pair => pair.Value == type).Key; }
            else { spec["type"] = type.FullName; Capture(type, false); }
            return spec;
        }
        private static void Capture(Type type, bool root)
        {
            if (types.Property(type.FullName) != null) return;
            var definition = new JObject(); types[type.FullName] = definition;
            if (type.IsEnum)
            {
                var keys = new JObject();
                foreach (string name in Enum.GetNames(type)) keys[name] = Convert.ToInt32(Enum.Parse(type, name));
                if (!Enum.IsDefined(type, 0)) keys["Unspecified"] = 0;
                if (type == typeof(Levels.MissionKind)) keys["Unselected"] = -1;
                definition["enum"] = keys; return;
            }
            var fields = new JObject(); definition["fields"] = fields;
            if (root) fields["displayName"] = new JObject { ["type"] = "string", ["nullable"] = false };
            foreach (var field in SerializedAuthoringFields.For(type)) fields[SerializedAuthoringFields.Name(field)] = Spec(field.FieldType, field);
        }
    }
}
