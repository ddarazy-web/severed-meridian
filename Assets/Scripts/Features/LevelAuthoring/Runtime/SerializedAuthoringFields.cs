using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace LevelAuthoring.Runtime
{
    // 제작 SO의 저장 필드만 직접 읽는다. 프로퍼티 및 실행 변환의 기본값 보충을 피한다.
    public static class SerializedAuthoringFields
    {
        public static FieldInfo[] For(Type type)
        {
            var result = new List<FieldInfo>();
            for (Type current = type; current != null && current != typeof(ScriptableObject) && current != typeof(UnityEngine.Object); current = current.BaseType)
                result.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(field => !field.IsStatic && !field.IsDefined(typeof(NonSerializedAttribute)) &&
                        (field.IsPublic || field.IsDefined(typeof(SerializeField)))));
            return result.OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();
        }

        public static string Name(FieldInfo field)
        {
            if (field.Name == "flow" && field.FieldType == typeof(Tutorial.TutorialFlowDefinition)) return "flowId";
            if (field.Name == "elementCatalog") return "catalogId";
            if (field.DeclaringType == typeof(Elements.ElementCatalogAsset)) return field.Name == "definitions" ? "definitionIds" : "visualId";
            if (field.Name == "path" && field.DeclaringType.FullName.StartsWith("Elements.ElementVisualCatalogAsset+", StringComparison.Ordinal)) return "resourceId";
            if (field.Name == "effects" && field.DeclaringType.FullName.StartsWith("Elements.ElementVisualCatalogAsset+", StringComparison.Ordinal)) return "effectResourceIds";
            return field.Name;
        }
    }
}
