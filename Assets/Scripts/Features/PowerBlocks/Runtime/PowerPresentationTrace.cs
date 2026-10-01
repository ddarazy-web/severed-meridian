using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    // 실제 규칙이 선택한 발사 정보다. 표시를 위해 표적이나 범위를 다시 계산하지 않는다.
    public sealed class PowerAttackRecord
    {
        public int HitGroup { get; }
        public int ParentHitGroup { get; }
        public BoardCoordinate Origin { get; }
        public BoardCoordinate Center { get; }
        public RuntimeContent Power { get; }
        public RocketDirection? Direction { get; }
        public PowerArea Area { get; }
        public bool IsFlight { get; }
        public int WaitForAttacks { get; }
        public bool Retargeted { get; }
        public ReadOnlyCollection<BoardCoordinate> Targets { get; }
        internal PowerAttackRecord(int hit, int parent, BoardCoordinate origin, BoardCoordinate center,
            RuntimeContent power, RocketDirection? direction, PowerArea area, bool flight, int wait, IEnumerable<BoardCoordinate> targets, bool retargeted = false)
        {
            HitGroup = hit; ParentHitGroup = parent; Origin = origin; Center = center;
            Power = power; Direction = direction; Area = area; IsFlight = flight; WaitForAttacks = wait;
            Retargeted = retargeted;
            Targets = targets.ToList().AsReadOnly();
        }
    }

    // 한 번의 효과 계산 결과에만 소속되며 레벨·MemoryPack 저장 데이터에는 포함하지 않는다.
    public sealed class PowerPresentationTrace
    {
        private readonly List<PowerAttackRecord> attacks = new List<PowerAttackRecord>();
        public ReadOnlyCollection<PowerAttackRecord> Attacks => attacks.AsReadOnly();
        public PowerCombination Combination { get; }
        internal PowerPresentationTrace(PowerCombination combination) { Combination = combination; }
        internal void Add(PowerAttackRecord attack) => attacks.Add(attack);
    }
}
