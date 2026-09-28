using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    public enum TargetingEvent { CandidatesBuilt, CandidatesReused, Reserved, Released, Retargeted, Landed, NoTarget, ColorSelected }

    public sealed class TargetingRecord
    {
        public TargetingEvent Event { get; }
        public int Request { get; }
        public BoardCoordinate Origin { get; }
        public BoardCoordinate? Target { get; }
        public RabbitColor? Color { get; }
        public int ReservedCount { get; }
        public int ExpectedComplete { get; }
        public int ExpectedDamage { get; }
        public int ExpectedCharge { get; }
        public string Message { get; }
        internal TargetingRecord(TargetingEvent kind, int request, BoardCoordinate origin, BoardCoordinate? target,
            RabbitColor? color, int reserved, int expected, string message, int damage = 0, int charge = 0)
        { Event = kind; Request = request; Origin = origin; Target = target; Color = color; ReservedCount = reserved; ExpectedComplete = expected; ExpectedDamage = damage; ExpectedCharge = charge; Message = message; }
    }

    public sealed class DroneTarget
    {
        public BoardCoordinate Coordinate { get; }
        public RuntimeContent Content { get; }
        public RabbitColor? Color { get; }
        public int? ObstacleIndex { get; }
        public Levels.CoverKind? Cover { get; }
        public int CoverDurability { get; }
        public int DustDurability { get; }
        public ReadOnlyCollection<MissionContribution> Contributions { get; }
        public PowerArea Area { get; }
        public ReadOnlyCollection<DroneImpact> Impacts { get; }
        public bool IsMission => Contributions.Count > 0;
        internal DroneTarget(LevelRuntimeState state, RuntimeCell cell, PowerArea area, IEnumerable<DroneImpact> impacts)
        {
            Coordinate = cell.Coordinate; Content = cell.Content; Color = cell.Color; ObstacleIndex = cell.ObstacleIndex; Area = area;
            Cover = cell.Cover; CoverDurability = cell.CoverDurability; DustDurability = cell.DustDurability;
            Impacts = impacts.ToList().AsReadOnly(); Contributions = MissionProgressRules.Project(state, Impacts).contributions;
        }
    }

    public sealed class DroneImpact
    {
        public BoardCoordinate Coordinate { get; }
        public RuntimeContent Content { get; }
        public RabbitColor? Color { get; }
        public int? ObstacleIndex { get; }
        public Levels.CoverKind? Cover { get; }
        public int CoverDurability { get; }
        public int DustDurability { get; }
        public ReadOnlyCollection<MissionContribution> Contributions { get; }
        internal DroneImpact(RuntimeCell cell, IEnumerable<MissionContribution> contributions)
        { Coordinate = cell.Coordinate; Content = cell.Content; Color = cell.Color; ObstacleIndex = cell.ObstacleIndex; Contributions = contributions.ToList().AsReadOnly();
            Cover = cell.Cover; CoverDurability = cell.CoverDurability; DustDurability = cell.DustDurability; }
    }

    // 한 효과 작업 사본의 드론들이 공유한다. 실제 상태/미션을 수정하지 않고 선택과 예약만 소유한다.
    public sealed class DroneTargetManager
    {
        private readonly LevelRuntimeState state;
        private readonly TurnEffectContext context;
        private readonly Dictionary<int, DroneTarget> reservations = new Dictionary<int, DroneTarget>();
        private readonly Dictionary<PowerArea, List<DroneTarget>> cache = new Dictionary<PowerArea, List<DroneTarget>>();
        public int CacheBuildCount { get; private set; }
        public int ReservationCount => reservations.Count;
        public int ExpectedComplete => MissionProgressRules.Project(state, LiveReservations(0).SelectMany(t => t.Impacts)).contributions.GroupBy(c => c.MissionIndex)
            .Sum(g => System.Math.Min(state.Missions[g.Key].Remaining, g.Sum(c => c.ExpectedComplete)));
        public int ExpectedDamage => MissionProgressRules.Project(state, LiveReservations(0).SelectMany(t => t.Impacts)).damage;
        public int ExpectedCharge => MissionProgressRules.Project(state, LiveReservations(0).SelectMany(t => t.Impacts)).charge;
        internal DroneTargetManager(LevelRuntimeState state, TurnEffectContext context) { this.state = state; this.context = context; }
        internal void Invalidate() => cache.Clear();

        public ReadOnlyCollection<DroneTarget> Query(int excludingRequest = 0)
            => QueryArea(PowerArea.Point, excludingRequest);

        public ReadOnlyCollection<DroneTarget> QueryArea(PowerArea area, int excludingRequest = 0)
        {
            if (!cache.TryGetValue(area, out List<DroneTarget> candidates))
            {
                candidates = new List<DroneTarget>(); cache.Add(area, candidates); CacheBuildCount++;
                foreach (RuntimeCell cell in state.Cells.OrderBy(c => c.Coordinate.Row).ThenBy(c => c.Coordinate.Column))
                {
                    DamageReaction reaction = DamageReaction.Evaluate(state, cell.Coordinate, DamageCause.Power, cell.Coordinate, context);
                    if (reaction.Response != DamageResponse.Remove && reaction.Response != DamageResponse.Damage && reaction.Response != DamageResponse.CoverDamage && reaction.Response != DamageResponse.Charge) continue;
                    DroneImpact[] impacts = PowerCombinationResolution.Range(state, cell.Coordinate, area)
                        .Select(c => new DroneImpact(state.CellAt(c), MissionProgressRules.Query(state, c, context))).Where(i => i.Contributions.Count > 0).ToArray();
                    candidates.Add(new DroneTarget(state, cell, area, impacts));
                }
            }
            DroneTarget[] others = LiveReservations(excludingRequest).ToArray();
            ReadOnlyCollection<MissionContribution> expected = MissionProgressRules.Project(state, others.SelectMany(t => t.Impacts)).contributions;
            HashSet<BoardCoordinate> blocked = new HashSet<BoardCoordinate>(others.Select(t => t.Coordinate).Concat(others.SelectMany(t => t.Impacts.Select(i => i.Coordinate))));
            // 미션 목표가 여러 개여도 이미 파괴 예정인 같은 본체에 추가 조준하지 않는다.
            HashSet<int> completedBodies = new HashSet<int>(MissionProgressRules.PendingBodyRemovals(state, others.SelectMany(t => t.Impacts)));
            blocked.UnionWith(state.Cells.Where(c => c.ObstacleIndex.HasValue && completedBodies.Contains(c.ObstacleIndex.Value)).Select(c => c.Coordinate));
            HashSet<int> chargedBodies = new HashSet<int>(others.SelectMany(t => t.Impacts).Where(i => i.ObstacleIndex.HasValue &&
                state.Obstacles[i.ObstacleIndex.Value].Definition.Kind == ObstacleKind.Generator).Select(i => i.ObstacleIndex.Value));
            blocked.UnionWith(state.Cells.Where(c => c.ObstacleIndex.HasValue && chargedBodies.Contains(c.ObstacleIndex.Value)).Select(c => c.Coordinate));
            List<DroneTarget> missions = new List<DroneTarget>(), normal = new List<DroneTarget>();
            foreach (DroneTarget target in candidates)
            {
                if (blocked.Contains(target.Coordinate)) continue;
                DroneImpact[] available = target.Impacts.Where(i => !blocked.Contains(i.Coordinate)).Select(i => new DroneImpact(state.CellAt(i.Coordinate),
                    i.Contributions.Where(c => (!c.BodyIndex.HasValue || !completedBodies.Contains(c.BodyIndex.Value)) && state.Missions[c.MissionIndex].Remaining >
                        expected.Where(x => x.MissionIndex == c.MissionIndex).Sum(x => x.ExpectedComplete))))
                    .Where(i => i.Contributions.Count > 0).ToArray();
                if (available.Length > 0) missions.Add(new DroneTarget(state, state.CellAt(target.Coordinate), area, available));
                if (target.Content == RuntimeContent.Normal && target.Cover != CoverKind.Mold) normal.Add(new DroneTarget(state, state.CellAt(target.Coordinate), area, System.Array.Empty<DroneImpact>()));
            }
            // 예약 가능한 노출 색 기여가 있으면 같은 색 미션의 덮개 대체 기여만 제외한다.
            // 거미줄 제거 등 다른 미션 기여에는 새 우선순위를 강제하지 않는다.
            HashSet<int> direct = new HashSet<int>(missions.SelectMany(t => t.Contributions).Where(c => !c.IsFallback).Select(c => c.MissionIndex));
            List<DroneTarget> preferred = missions.Select(t => new DroneTarget(state, state.CellAt(t.Coordinate), area,
                t.Impacts.Select(i => new DroneImpact(state.CellAt(i.Coordinate), i.Contributions.Where(c => !c.IsFallback || !direct.Contains(c.MissionIndex))))
                    .Where(i => i.Contributions.Count > 0))).Where(t => t.IsMission).ToList();
            return (preferred.Count > 0 ? preferred : normal).AsReadOnly();
        }

        // 소실된 다른 예약이 남은 미션을 이미 해결한 것으로 계산되지 않게 한다.
        // 예약 자체는 착탄 시 자기 요청이 해제하며, 이 조회는 상태와 예약을 바꾸지 않는다.
        private IEnumerable<DroneTarget> LiveReservations(int excludingRequest)
        {
            foreach (KeyValuePair<int, DroneTarget> pair in reservations)
            {
                if (pair.Key == excludingRequest) continue;
                DroneTarget reserved = pair.Value; RuntimeCell cell = state.CellAt(reserved.Coordinate);
                if (cell.Content != reserved.Content || cell.Color != reserved.Color || cell.ObstacleIndex != reserved.ObstacleIndex ||
                    cell.Cover != reserved.Cover || cell.CoverDurability != reserved.CoverDurability || cell.DustDurability != reserved.DustDurability) continue;
                DamageReaction reaction = DamageReaction.Evaluate(state, cell.Coordinate, DamageCause.Power, cell.Coordinate, context);
                if (reaction.Response != DamageResponse.Remove && reaction.Response != DamageResponse.Damage && reaction.Response != DamageResponse.CoverDamage && reaction.Response != DamageResponse.Charge) continue;
                List<DroneImpact> live = new List<DroneImpact>();
                foreach (DroneImpact impact in reserved.Impacts)
                {
                    RuntimeCell current = state.CellAt(impact.Coordinate);
                    if (current.Content != impact.Content || current.Color != impact.Color || current.ObstacleIndex != impact.ObstacleIndex ||
                        current.Cover != impact.Cover || current.CoverDurability != impact.CoverDurability || current.DustDurability != impact.DustDurability) continue;
                    ReadOnlyCollection<MissionContribution> contributions = MissionProgressRules.Query(state, current.Coordinate, context);
                    if (contributions.Count > 0) live.Add(new DroneImpact(current, contributions));
                }
                yield return new DroneTarget(state, cell, reserved.Area, live);
            }
        }

        internal int Request(BoardCoordinate origin)
            => RequestArea(origin, PowerArea.Point);

        internal int RequestArea(BoardCoordinate origin, PowerArea area)
        {
            int request = context.NextTargetRequest();
            Reserve(request, origin, area); return request;
        }

        private DroneTarget Reserve(int request, BoardCoordinate origin, PowerArea area)
        {
            int builds = CacheBuildCount;
            ReadOnlyCollection<DroneTarget> candidates = QueryArea(area);
            Record(CacheBuildCount == builds ? TargetingEvent.CandidatesReused : TargetingEvent.CandidatesBuilt, request, origin, null,
                "후보 " + candidates.Count + " · 조회 " + CacheBuildCount + " · 도착 " + area);
            if (candidates.Count == 0) { Record(TargetingEvent.NoTarget, request, origin, null, "미션/일반 후보 없음 · 추가 타격 종료"); return null; }
            DroneTarget selected = candidates[candidates.Count == 1 ? 0 : state.Random.Next(candidates.Count)];
            reservations.Add(request, selected);
            Record(TargetingEvent.Reserved, request, origin, selected.Coordinate, (selected.IsMission ? "남은 미션 우선 예약" : "일반 블록 대체 예약") +
                " · 도착 " + area + " · 직접 미션 예약 " + string.Join(", ", selected.Impacts.Select(i => i.Coordinate)));
            return selected;
        }

        internal DroneTarget Land(int request, BoardCoordinate origin)
        {
            if (!reservations.TryGetValue(request, out DroneTarget target)) return null;
            DroneTarget valid = QueryArea(target.Area, request).FirstOrDefault(c => c.Coordinate.Equals(target.Coordinate) && c.Content == target.Content &&
                c.Color == target.Color && c.ObstacleIndex == target.ObstacleIndex && c.Cover == target.Cover && c.CoverDurability == target.CoverDurability &&
                c.DustDurability == target.DustDurability && c.IsMission == target.IsMission);
            if (valid == null)
            {
                Release(request, origin, "목표/미션 무효 · 자기 예약 반환");
                Record(TargetingEvent.Retargeted, request, origin, target.Coordinate, "미션 후보부터 다시 검색");
                target = Reserve(request, origin, target.Area);
                if (target == null) return null;
            }
            else target = valid;
            Release(request, origin, "착탄 · 예상 기여 반환");
            Record(TargetingEvent.Landed, request, origin, target.Coordinate, "도착 타격 " + target.Area);
            return target;
        }

        private void Release(int request, BoardCoordinate origin, string message)
        {
            BoardCoordinate target = reservations[request].Coordinate;
            reservations.Remove(request); Record(TargetingEvent.Released, request, origin, target, message);
        }

        private void Record(TargetingEvent kind, int request, BoardCoordinate origin, BoardCoordinate? target, string message)
            => context.RecordTargeting(new TargetingRecord(kind, request, origin, target, null, ReservationCount, ExpectedComplete, message, ExpectedDamage, ExpectedCharge));
    }
}
