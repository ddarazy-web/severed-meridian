using System;
using Levels;

namespace Elements
{
    /// <summary>기존 장애물 종류만 정의 ID로 연결한다. 배치 인스턴스 ID는 변환하지 않는다.</summary>
    public static class LegacyElementMap
    {
        private static readonly ElementId Crate = new ElementId("obstacle.crate.wood");
        private static readonly ElementId Scrap = new ElementId("obstacle.scrap");
        private static readonly ElementId Safe = new ElementId("obstacle.recovery-capsule");
        private static readonly ElementId ColorLock = new ElementId("obstacle.color-lock");
        private static readonly ElementId Appliance = new ElementId("obstacle.metal-rod-box");
        private static readonly ElementId Generator = new ElementId("obstacle.generator");
        private static readonly ElementId Web = new ElementId("cover.web");
        private static readonly ElementId Mold = new ElementId("cover.mold");
        internal static readonly ElementId Dust = new ElementId("floor.dust");

        public static ElementId Get(SupplyKind kind) => new ElementId(kind switch
        {
            SupplyKind.RandomNormal => "supply.normal.random",
            SupplyKind.FixedNormal => "supply.normal.fixed",
            SupplyKind.Rocket => "power.rocket",
            SupplyKind.Bomb => "power.bomb",
            SupplyKind.Drone => "power.drone",
            SupplyKind.Magnet => "power.magnet",
            SupplyKind.Scrap => "supply.scrap",
            SupplyKind.Recovery => "supply.recovery",
            SupplyKind.RandomPower => "supply.power.random",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "지원하지 않는 공급 종류입니다.")
        });

        public static ElementId Get(CoverKind kind) => kind switch
        {
            CoverKind.Web => Web,
            CoverKind.Mold => Mold,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "지원하지 않는 덮개 종류입니다.")
        };

        /// <param name="kind">기존 장애물 종류.</param><returns>같은 콘텐츠의 정의 ID. 미지원 값은 입력을 포함한 오류로 거절한다.</returns>
        public static ElementId Get(ObstacleKind kind) => kind switch
        {
            ObstacleKind.Crate => Crate,
            ObstacleKind.Scrap => Scrap,
            ObstacleKind.Safe => Safe,
            ObstacleKind.ColorLock => ColorLock,
            ObstacleKind.Appliance => Appliance,
            ObstacleKind.Generator => Generator,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "지원하지 않는 장애물 종류입니다.")
        };
    }
}
