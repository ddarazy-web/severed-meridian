using System;
using System.Collections.Generic;
using System.Linq;

namespace Elements
{
    /// <summary>시각 키와 ID 별칭을 한번 검증해 구성한다. 원본 DTO와 분리된 읽기 전용 값이다.</summary>
    public sealed class ElementVisualCatalog
    {
        private readonly Dictionary<string, ElementVisualDefinition> definitions;
        private readonly Dictionary<ElementId, ElementVisualDefinition> bindings;
        private ElementVisualCatalog(ElementVisualCatalogDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            definitions = new Dictionary<string, ElementVisualDefinition>(StringComparer.Ordinal);
            bindings = new Dictionary<ElementId, ElementVisualDefinition>();
            foreach (ElementVisualDefinitionDto source in dto.definitions ?? Array.Empty<ElementVisualDefinitionDto>())
            {
                try
                {
                    ElementVisualDefinition definition = new ElementVisualDefinition(source);
                    if (!definitions.TryAdd(definition.Key, definition)) throw ElementVisualFrame.Error(definition.Key, "중복 시각 키");
                }
                catch (ArgumentException error)
                {
                    string ids = string.Join(", ", (dto.bindings ?? Array.Empty<ElementVisualBindingDto>())
                        .Where(value => value != null && value.visualKey == source?.key).Select(value => value.id));
                    throw new ArgumentException($"요소 ID [{ids}]: {error.Message}", nameof(dto), error);
                }
            }
            foreach (ElementVisualBindingDto binding in dto.bindings ?? Array.Empty<ElementVisualBindingDto>())
            {
                if (binding == null) throw ElementVisualFrame.Error("<null>", "빈 별칭");
                ElementId id = new ElementId(binding.id);
                if (binding.visualKey == null || !definitions.TryGetValue(binding.visualKey, out ElementVisualDefinition definition))
                    throw ElementVisualFrame.Error(id.Value, "없는 시각 키 '" + binding.visualKey + "'");
                if (!bindings.TryAdd(id, definition)) throw ElementVisualFrame.Error(id.Value, "중복 ID 별칭");
            }
            foreach (KeyValuePair<ElementId, ElementVisualDefinition> binding in bindings)
                foreach (ElementId generated in binding.Value.Generates)
                    if (!bindings.ContainsKey(generated)) throw ElementVisualFrame.Error(binding.Key.Value, "없는 생성 참조 '" + generated.Value + "'");
        }
        public static ElementVisualCatalog FromDto(ElementVisualCatalogDto dto) => new ElementVisualCatalog(dto);
        public ElementVisualDefinition Get(ElementId id) => bindings.TryGetValue(id, out ElementVisualDefinition definition)
            ? definition : throw ElementVisualFrame.Error(id.Value, "시각 별칭이 등록되지 않았습니다.");
        public ElementVisualCatalogDto ToDto() => new ElementVisualCatalogDto
        {
            definitions = definitions.Values.OrderBy(value => value.Key, StringComparer.Ordinal).Select(value => value.ToDto()).ToArray(),
            bindings = bindings.OrderBy(value => value.Key.Value, StringComparer.Ordinal)
                .Select(value => new ElementVisualBindingDto { id = value.Key.Value, visualKey = value.Value.Key }).ToArray()
        };
    }
}
