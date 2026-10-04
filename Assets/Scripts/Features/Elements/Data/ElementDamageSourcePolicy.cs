using System;
using Simulation;

namespace Elements
{
    /// <summary>피해 실행과 분리한 네 원인의 불변 허용 값.</summary>
    public sealed class ElementDamageSourcePolicy
    {
        public bool AdjacentMatch { get; }
        public bool Power { get; }
        public bool MagnetAdjacent { get; }
        public bool Hammer { get; }

        /// <param name="adjacentMatch">일반 인접 매칭 허용 여부.</param><param name="power">파워 피해 허용 여부.</param>
        /// <param name="magnetAdjacent">자석 인접 피해 허용 여부.</param><param name="hammer">망치 피해 허용 여부.</param>
        public ElementDamageSourcePolicy(bool adjacentMatch, bool power, bool magnetAdjacent, bool hammer)
        { AdjacentMatch = adjacentMatch; Power = power; MagnetAdjacent = magnetAdjacent; Hammer = hammer; }

        /// <param name="cause">정의된 피해 원인.</param><returns>원인의 허용 여부. 미정의 값은 오류로 거절한다.</returns>
        public bool Allows(DamageCause cause) => cause switch
        {
            DamageCause.AdjacentMatch => AdjacentMatch,
            DamageCause.Power => Power,
            DamageCause.MagnetAdjacent => MagnetAdjacent,
            DamageCause.Hammer => Hammer,
            _ => throw new ArgumentOutOfRangeException(nameof(cause), cause, "정의되지 않은 피해 원인입니다.")
        };
    }
}
