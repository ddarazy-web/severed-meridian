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

    public sealed class DroneRetargetRecord
    {
        public BoardCoordinate LostTarget { get; }
        public BoardCoordinate? NextTarget { get; }
        public int EffectIndex { get; }
        internal DroneRetargetRecord(BoardCoordinate lost, BoardCoordinate? next, int effect)
        { LostTarget = lost; NextTarget = next; EffectIndex = effect; }
    }
    /// <summary>예약 때의 목표와 실제 소실 효과를 보존한다. 최종 보드를 역조회해 비행을 추측하지 않는다.</summary>
    public sealed class DroneFlightRecord
    {
        public int Request { get; }
        public int LandingHitGroup { get; }
        public BoardCoordinate Origin { get; }
        public BoardCoordinate InitialTarget { get; }
        public BoardCoordinate? LandingTarget { get; }
        public ReadOnlyCollection<DroneRetargetRecord> Retargets { get; }
        public int ReservedAfterEffects { get; }
        public int ReservedAfterAttacks { get; }
        public int InterruptedByEffect { get; }
        public bool Retargeted { get; }
        internal DroneFlightRecord(int request, int hit, BoardCoordinate origin, BoardCoordinate initial, BoardCoordinate? landing,
            int effects, int attacks, int interrupted, bool retargeted, IEnumerable<DroneRetargetRecord> retargets = null)
        {
            Request = request; LandingHitGroup = hit; Origin = origin; InitialTarget = initial; LandingTarget = landing;
            Retargets = (retargets ?? Enumerable.Empty<DroneRetargetRecord>()).ToList().AsReadOnly();
            ReservedAfterEffects = effects; ReservedAfterAttacks = attacks; InterruptedByEffect = interrupted; Retargeted = retargeted;
        }
    }

    // 한 번의 효과 계산 결과에만 소속되며 레벨·MemoryPack 저장 데이터에는 포함하지 않는다.
    public sealed class PowerPresentationTrace
    {
        private readonly List<PowerAttackRecord> attacks = new List<PowerAttackRecord>();
        private readonly List<DroneFlightRecord> flights = new List<DroneFlightRecord>();
        public ReadOnlyCollection<PowerAttackRecord> Attacks => attacks.AsReadOnly();
        public ReadOnlyCollection<DroneFlightRecord> Flights => flights.AsReadOnly();
        public PowerCombination Combination { get; }
        internal PowerPresentationTrace(PowerCombination combination) { Combination = combination; }
        internal void Add(PowerAttackRecord attack) => attacks.Add(attack);
        internal void AddFlight(DroneFlightRecord flight) => flights.Add(flight);
    }
}
