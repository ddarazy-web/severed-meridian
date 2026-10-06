using Levels;

namespace Elements
{
    /// <summary>전환한 구형 종류의 정의를 한 번 준비한다. 미전환 정의는 조회 오류로 남긴다.</summary>
    public static class LegacyElementDefinitions
    {
        private static readonly ElementDamageAggregationPolicy PerBody = new ElementDamageAggregationPolicy(false);
        private static readonly ElementDamageAggregationPolicy PerCell = new ElementDamageAggregationPolicy(true);
        private static readonly ElementCatalog Catalog = new ElementCatalog(new[]
        {
            new ElementDefinition(LegacyElementMap.Get(ObstacleKind.Crate), "나무상자", new ElementPlacementProfile(1, 6), null, new ElementDamageSourcePolicy(true, true, false, true), null, PerBody, new ElementRemovalMissionProfile(MissionKind.Crate), ElementReactionBehavior.Durability),
            new ElementDefinition(LegacyElementMap.Get(ObstacleKind.Scrap), "고철 뭉치", new ElementPlacementProfile(1, 5), null, new ElementDamageSourcePolicy(true, true, false, true), null, PerBody, new ElementRemovalMissionProfile(MissionKind.Scrap), ElementReactionBehavior.Durability),
            new ElementDefinition(LegacyElementMap.Get(ObstacleKind.Safe), "고물 회수 캡슐", new ElementPlacementProfile(1, 5), null, new ElementDamageSourcePolicy(false, true, false, true), null, PerBody, new ElementRemovalMissionProfile(MissionKind.Safe), ElementReactionBehavior.Durability),
            new ElementDefinition(LegacyElementMap.Get(ObstacleKind.ColorLock), "색깔 자물쇠", new ElementPlacementProfile(1, 3), null, new ElementDamageSourcePolicy(true, true, true, true), new ElementColorMatchPolicy(true), PerBody, new ElementRemovalMissionProfile(MissionKind.ColorLock), ElementReactionBehavior.Durability),
            new ElementDefinition(LegacyElementMap.Get(ObstacleKind.Appliance), "금속기둥 상자", new ElementPlacementProfile(2, 9), null, new ElementDamageSourcePolicy(true, true, false, true), null, PerCell, new ElementRemovalMissionProfile(MissionKind.Appliance), ElementReactionBehavior.Durability),
            new ElementDefinition(LegacyElementMap.Get(ObstacleKind.Generator), "고장 난 발전기", null, new ElementChargePlacementProfile(2, 3, 5), new ElementDamageSourcePolicy(true, true, true, true), null, null, null, ElementReactionBehavior.GeneratorCharge),
            new ElementDefinition(LegacyElementMap.Get(CoverKind.Web), "거미줄", new ElementPlacementProfile(1, 3), null, null, null, null, null, null, new ElementLayerProfile(ElementLayerBehavior.CoverDurability, MissionKind.Web)),
            new ElementDefinition(LegacyElementMap.Get(CoverKind.Mold), "우주 곰팡이", new ElementPlacementProfile(1, 1), null, null, null, null, null, null, new ElementLayerProfile(ElementLayerBehavior.CoverRemoval, MissionKind.Mold), new ElementTurnProfile(ElementTurnBehavior.AdjacentCoverSpread, 1)),
            new ElementDefinition(LegacyElementMap.Dust, "먼지", new ElementPlacementProfile(1, 3), null, null, null, null, null, null, new ElementLayerProfile(ElementLayerBehavior.NormalConsumption, MissionKind.Dust)),
            ElementDefinition.CreateSupply(LegacyElementMap.Get(SupplyKind.RandomNormal), "무작위 달토끼 공급", new ElementSupplyProfile(ElementSupplyBehavior.RandomNormal, Simulation.RuntimeContent.Normal)),
            ElementDefinition.CreateSupply(LegacyElementMap.Get(SupplyKind.FixedNormal), "지정 달토끼 공급", new ElementSupplyProfile(ElementSupplyBehavior.FixedNormal, Simulation.RuntimeContent.Normal)),
            ElementDefinition.CreateSupply(LegacyElementMap.Get(SupplyKind.Rocket), "청소 로켓", new ElementSupplyProfile(ElementSupplyBehavior.Power, Simulation.RuntimeContent.Rocket)),
            ElementDefinition.CreateSupply(LegacyElementMap.Get(SupplyKind.Bomb), "폭탄", new ElementSupplyProfile(ElementSupplyBehavior.Power, Simulation.RuntimeContent.Bomb)),
            ElementDefinition.CreateSupply(LegacyElementMap.Get(SupplyKind.Drone), "드론", new ElementSupplyProfile(ElementSupplyBehavior.Power, Simulation.RuntimeContent.Drone)),
            ElementDefinition.CreateSupply(LegacyElementMap.Get(SupplyKind.Magnet), "자석", new ElementSupplyProfile(ElementSupplyBehavior.Power, Simulation.RuntimeContent.Magnet)),
            ElementDefinition.CreateSupply(LegacyElementMap.Get(SupplyKind.Scrap), "고철 공급", new ElementSupplyProfile(ElementSupplyBehavior.Obstacle, Simulation.RuntimeContent.Obstacle, ObstacleKind.Scrap, "supply-scrap-")),
            ElementDefinition.CreateSupply(LegacyElementMap.Get(SupplyKind.Recovery), "부품 공급", new ElementSupplyProfile(ElementSupplyBehavior.Recovery, Simulation.RuntimeContent.Recovery)),
            ElementDefinition.CreateSupply(LegacyElementMap.Get(SupplyKind.RandomPower), "무작위 파워 공급", new ElementSupplyProfile(ElementSupplyBehavior.RandomPower, Simulation.RuntimeContent.Empty, choices: new[] { SupplyKind.Rocket, SupplyKind.Bomb, SupplyKind.Drone }))
        });

        public static ElementCatalog DefaultCatalog => Catalog;

        internal static ElementDefinition GetContent(Simulation.RuntimeContent content, ElementCatalog catalog = null)
        {
            ElementDefinition builtin = ContentDefault(content);
            return builtin != null && catalog != null && catalog.TryGet(builtin.Id, out ElementDefinition selected) ? selected : builtin;
        }

        private static ElementDefinition ContentDefault(Simulation.RuntimeContent content) => content switch
        {
            Simulation.RuntimeContent.Normal => GetSupply(SupplyKind.RandomNormal),
            Simulation.RuntimeContent.Rocket => GetSupply(SupplyKind.Rocket),
            Simulation.RuntimeContent.Bomb => GetSupply(SupplyKind.Bomb),
            Simulation.RuntimeContent.Drone => GetSupply(SupplyKind.Drone),
            Simulation.RuntimeContent.Magnet => GetSupply(SupplyKind.Magnet),
            Simulation.RuntimeContent.Recovery => GetSupply(SupplyKind.Recovery),
            _ => null
        };

        /// <param name="kind">구형 장애물 종류.</param><returns>해당 종류의 불변 정의.</returns>
        public static ElementDefinition Get(ObstacleKind kind) => Catalog.Get(LegacyElementMap.Get(kind));
        public static ElementDefinition Get(CoverKind kind) => Catalog.Get(LegacyElementMap.Get(kind));
        public static ElementDefinition GetDust() => Catalog.Get(LegacyElementMap.Dust);
        public static ElementDefinition GetSupply(SupplyKind kind) => Catalog.Get(LegacyElementMap.Get(kind));
    }
}
