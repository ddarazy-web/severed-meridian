using System;

namespace Elements
{
    /// <summary>Editor만 제작 원본 조회를 연결한다. 플레이어는 레벨/콘텐츠 팩으로 주입한다.</summary>
    public static class ElementAuthoringDefaults
    {
        private static Func<ElementCatalog> definitions;
        private static Func<ElementVisualCatalog> visuals;
        public static ElementCatalog Definitions => definitions?.Invoke() ?? LegacyElementDefinitions.DefaultCatalog;
        public static ElementVisualCatalog Visuals => visuals?.Invoke() ?? LegacyElementVisuals.Catalog;
        public static void Configure(Func<ElementCatalog> definitionSource, Func<ElementVisualCatalog> visualSource)
        { definitions = definitionSource; visuals = visualSource; }
    }
}
