using Levels;

namespace Elements
{
    /// <summary>전환한 구형 종류의 정의를 한 번 준비한다. 미전환 정의는 조회 오류로 남긴다.</summary>
    public static class LegacyElementDefinitions
    {
        private static readonly ElementCatalog Catalog = new ElementCatalog(new[]
        {
            new ElementDefinition(LegacyElementMap.Get(ObstacleKind.Crate), "나무상자", new ElementPlacementProfile(1, 6), null, new ElementDamageSourcePolicy(true, true, false, true)),
            new ElementDefinition(LegacyElementMap.Get(ObstacleKind.Scrap), "고철 뭉치", new ElementPlacementProfile(1, 5), null, new ElementDamageSourcePolicy(true, true, false, true)),
            new ElementDefinition(LegacyElementMap.Get(ObstacleKind.Safe), "고물 회수 캡슐", new ElementPlacementProfile(1, 5), null, new ElementDamageSourcePolicy(false, true, false, true)),
            new ElementDefinition(LegacyElementMap.Get(ObstacleKind.ColorLock), "색깔 자물쇠", new ElementPlacementProfile(1, 3), null, new ElementDamageSourcePolicy(true, true, true, true)),
            new ElementDefinition(LegacyElementMap.Get(ObstacleKind.Appliance), "금속기둥 상자", new ElementPlacementProfile(2, 9), null, new ElementDamageSourcePolicy(true, true, false, true)),
            new ElementDefinition(LegacyElementMap.Get(ObstacleKind.Generator), "고장 난 발전기", null, new ElementChargePlacementProfile(2, 3, 5))
        });

        /// <param name="kind">구형 장애물 종류.</param><returns>해당 종류의 불변 정의.</returns>
        public static ElementDefinition Get(ObstacleKind kind) => Catalog.Get(LegacyElementMap.Get(kind));
    }
}
