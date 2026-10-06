using System;
using Levels;

namespace Elements
{
    public enum ElementLayerBehavior { CoverDurability = 1, CoverRemoval = 2, NormalConsumption = 3 }

    /// <summary>층 상태에 적용할 행동과 제거 미션. 내용물 실행과 층 피해를 분리한다.</summary>
    public sealed class ElementLayerProfile
    {
        public ElementLayerBehavior Behavior { get; }
        public MissionKind Mission { get; }
        public int Damage { get; }

        public ElementLayerProfile(ElementLayerBehavior behavior, MissionKind mission, int damage = 1)
        {
            if (!Enum.IsDefined(typeof(ElementLayerBehavior), behavior)) throw new ArgumentOutOfRangeException(nameof(behavior));
            if (mission != MissionKind.Web && mission != MissionKind.Mold && mission != MissionKind.Dust)
                throw new ArgumentOutOfRangeException(nameof(mission));
            if (damage < 1) throw new ArgumentOutOfRangeException(nameof(damage));
            Behavior = behavior; Mission = mission; Damage = damage;
        }
    }
}
