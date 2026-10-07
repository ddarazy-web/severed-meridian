using System;
using System.Collections.Generic;
using UnityEngine;

namespace Elements
{
    /// <summary>제작 목록에서 원본과 독립된 카탈로그를 구성한다. 자동 저장하지 않는다.</summary>
    [CreateAssetMenu(menuName = "MATCH/요소 카탈로그")]
    public sealed class ElementCatalogAsset : ScriptableObject
    {
        [SerializeField] private List<ElementDefinitionAsset> definitions = new List<ElementDefinitionAsset>();
        [SerializeField] private ElementVisualCatalogAsset visuals;

        public ElementVisualCatalog CreateVisualCatalog() => visuals != null ? visuals.CreateCatalog() : LegacyElementVisuals.Catalog;

        public ElementCatalog CreateCatalog()
        {
            if (definitions == null) throw new ArgumentException($"카탈로그 '{name}'의 정의 목록이 없습니다.");
            List<ElementDefinition> values = new List<ElementDefinition>(definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] == null) throw new ArgumentException($"카탈로그 '{name}'의 {i}번째 정의 원본이 없습니다.");
                values.Add(definitions[i].ToDefinition());
            }
            return new ElementCatalog(values);
        }
    }
}
