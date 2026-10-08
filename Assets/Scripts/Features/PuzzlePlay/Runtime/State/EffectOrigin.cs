using System;

namespace Simulation
{
    // 피해 허용 정책(DamageCause)과 분리한 실제 공격 출처. 조합은 단독 원인에 포함하지 않는다.
    public enum EffectOrigin
    {
        Unknown, AdjacentMatch, Rocket, Bomb, Drone, Magnet, Hammer,
        RocketRocket, RocketBomb, RocketDrone, BombBomb, BombDrone, DroneDrone,
        MagnetRocket, MagnetBomb, MagnetDrone, MagnetMagnet
    }

    internal static class EffectOrigins
    {
        internal static EffectOrigin Power(RuntimeContent content) => content switch
        {
            RuntimeContent.Rocket => EffectOrigin.Rocket,
            RuntimeContent.Bomb => EffectOrigin.Bomb,
            RuntimeContent.Drone => EffectOrigin.Drone,
            RuntimeContent.Magnet => EffectOrigin.Magnet,
            _ => EffectOrigin.Unknown
        };
        internal static EffectOrigin Combination(PowerCombinationKind kind) => kind switch
        {
            PowerCombinationKind.RocketRocket => EffectOrigin.RocketRocket,
            PowerCombinationKind.RocketBomb => EffectOrigin.RocketBomb,
            PowerCombinationKind.RocketDrone => EffectOrigin.RocketDrone,
            PowerCombinationKind.BombBomb => EffectOrigin.BombBomb,
            PowerCombinationKind.BombDrone => EffectOrigin.BombDrone,
            PowerCombinationKind.DroneDrone => EffectOrigin.DroneDrone,
            PowerCombinationKind.MagnetRocket => EffectOrigin.MagnetRocket,
            PowerCombinationKind.MagnetBomb => EffectOrigin.MagnetBomb,
            PowerCombinationKind.MagnetDrone => EffectOrigin.MagnetDrone,
            PowerCombinationKind.MagnetMagnet => EffectOrigin.MagnetMagnet,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
