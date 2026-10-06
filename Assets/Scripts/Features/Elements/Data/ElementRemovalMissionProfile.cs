using System;
using Levels;

namespace Elements
{
    /// <summary>내구도형 본체가 제거될 때 집계할 미션의 불변 값.</summary>
    public sealed class ElementRemovalMissionProfile
    {
        public MissionKind Kind { get; }

        public ElementRemovalMissionProfile(MissionKind kind)
        {
            if (kind != MissionKind.Crate && kind != MissionKind.Scrap && kind != MissionKind.Safe &&
                kind != MissionKind.ColorLock && kind != MissionKind.Appliance)
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "내구도형 제거 미션이 아닙니다.");
            Kind = kind;
        }
    }
}
