using System;

namespace Elements
{
    /// <summary>종류 분기 없이 ID와 실제 표시 상태로 등록 값을 조회한다.</summary>
    public sealed class ElementVisualResolver
    {
        public ElementVisualCatalog Catalog { get; }
        public ElementVisualResolver(ElementVisualCatalog catalog)
        { Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); }
        public ElementVisualFrame Resolve(ElementId id, ElementVisualState state) => Catalog.Get(id).Resolve(id, state);
        public System.Collections.Generic.IReadOnlyList<ElementVisualFrame> ResolveEffect(ElementId id, ElementVisualState state, string key)
        {
            ElementVisualFrame frame = Resolve(id, state);
            return key != null && frame.EffectAnimations.TryGetValue(key, out var animation) ? animation
                : throw ElementVisualFrame.Error(id.Value, "등록되지 않은 효과 키 '" + key + "'");
        }
    }
}
