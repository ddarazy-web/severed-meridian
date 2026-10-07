using UnityEngine;

namespace Elements
{
    /// <summary>제작 입력만 보유한다. 게임 진입에는 검증된 DTO/불변 카탈로그를 전달한다.</summary>
    [CreateAssetMenu(menuName = "MATCH/요소 시각 카탈로그")]
    public sealed class ElementVisualCatalogAsset : ScriptableObject
    {
        [SerializeField] private ElementVisualCatalogDto catalog = new ElementVisualCatalogDto();
        public ElementVisualCatalog CreateCatalog() => LegacyElementVisuals.WithOverrides(catalog);
        public ElementVisualCatalogDto ToDto() => CreateCatalog().ToDto();
    }
}
