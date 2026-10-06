using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Levels;

namespace Simulation
{
    // 실제 진행이 증가한 결정만 보존한다. 예측 기여량이나 저장 데이터가 아니다.
    public sealed class MissionProgressRecord
    {
        public int MissionIndex { get; }
        public int Amount { get; }
        public BoardCoordinate? Source { get; }
        public int? BodyIndex { get; }
        internal MissionProgressRecord(int missionIndex, int amount, BoardCoordinate? source, int? bodyIndex)
        { MissionIndex = missionIndex; Amount = amount; Source = source; BodyIndex = bodyIndex; }
    }

    public sealed class MissionContribution
    {
        public int MissionIndex { get; }
        public int ExpectedComplete { get; }
        public int Damage { get; }
        public int Charge { get; }
        public bool IsFallback { get; }
        public int? BodyIndex { get; }
        internal MissionContribution(int index, int complete, int damage, bool fallback = false, int? bodyIndex = null, int charge = 0)
        { MissionIndex = index; ExpectedComplete = complete; Damage = damage; IsFallback = fallback; BodyIndex = bodyIndex; Charge = charge; }
    }

    // 대상 반응은 실제 타격과 공유하고, 미션의 의미만 대상별 규칙에서 결정한다.
    public static class MissionProgressRules
    {
        internal static IEnumerable<int> PendingBodyRemovals(LevelRuntimeState state, IEnumerable<DroneImpact> impacts) =>
            Project(state, impacts).contributions.Where(c => c.BodyIndex.HasValue && c.ExpectedComplete > 0).Select(c => c.BodyIndex.Value).Distinct();
        // 실제 상태를 변경하지 않고, 예약된 직접 범위만 본체/칸별로 합산한다.
        internal static (ReadOnlyCollection<MissionContribution> contributions, int damage, int charge) Project(LevelRuntimeState state, IEnumerable<DroneImpact> impacts)
        {
            DroneImpact[] unique = impacts.GroupBy(i => i.Coordinate).Select(g => g.First()).ToArray();
            List<MissionContribution> result = new List<MissionContribution>();
            Dictionary<(int body, int mission), MissionContribution> bodies = new Dictionary<(int, int), MissionContribution>();
            int damage = 0, charge = 0;
            foreach (IGrouping<int, DroneImpact> body in unique.Where(i => i.ObstacleIndex.HasValue).GroupBy(i => i.ObstacleIndex.Value))
            {
                RuntimeObstacle obstacle = state.Obstacles[body.Key];
                if (obstacle.Definition.Kind == ObstacleKind.Generator)
                {
                    charge += obstacle.Element.RequireChargePlacement().ChargePerHit;
                    foreach (MissionContribution contribution in body.SelectMany(i => i.Contributions))
                    {
                        if (!contribution.BodyIndex.HasValue) continue;
                        (int body, int mission) key = (contribution.BodyIndex.Value, contribution.MissionIndex);
                        if (!bodies.TryGetValue(key, out MissionContribution previous) || contribution.ExpectedComplete > previous.ExpectedComplete)
                            bodies[key] = contribution;
                    }
                    continue;
                }
                int amount = ObstacleDamageRules.ReservedDamage(obstacle, body.Count());
                damage += amount;
                foreach (int index in body.SelectMany(i => i.Contributions).Select(c => c.MissionIndex).Distinct())
                {
                    (int body, int mission) key = (body.Key, index);
                    int complete = state.Missions[index].Definition.Kind != MissionKind.Recovery && amount >= obstacle.Durability ? 1 : 0;
                    if (!bodies.TryGetValue(key, out MissionContribution previous) || complete >= previous.ExpectedComplete)
                        bodies[key] = new MissionContribution(index, complete, amount, bodyIndex: body.Key);
                }
            }
            foreach (DroneImpact cell in unique.Where(i => !i.ObstacleIndex.HasValue))
            {
                result.AddRange(cell.Contributions);
                damage += cell.Contributions.Count == 0 ? 0 : cell.Contributions.Max(c => c.Damage);
            }
            result.AddRange(bodies.Values);
            return (result.AsReadOnly(), damage, charge);
        }

        internal static bool Supports(MissionKind kind) => kind == MissionKind.Color || kind == MissionKind.Crate || kind == MissionKind.Scrap || kind == MissionKind.Web || kind == MissionKind.Dust || kind == MissionKind.Mold || kind == MissionKind.Safe || kind == MissionKind.ColorLock || kind == MissionKind.Appliance || kind == MissionKind.Recovery;
        public static ReadOnlyCollection<MissionContribution> Query(LevelRuntimeState state, BoardCoordinate target, TurnEffectContext context)
        {
            List<MissionContribution> result = new List<MissionContribution>();
            DamageReaction reaction = DamageReaction.Evaluate(state, target, DamageCause.Power, target, context);
            if (reaction.Response == DamageResponse.Charge)
            {
                int generator = state.CellAt(target).ObstacleIndex.Value;
                RuntimeObstacle body = state.Obstacles[generator];
                foreach (int linked in GeneratorRules.Targets(state, generator))
                    for (int i = 0; i < state.Missions.Count; i++)
                        if (state.Missions[i].Remaining > 0 && state.Missions[i].Definition.Kind == ObstacleDamageRules.MissionForBody(state.Obstacles[linked]))
                            result.Add(new MissionContribution(i, body.Charge + reaction.Amount >= body.Definition.RequiredCharge ? 1 : 0, 0, bodyIndex: linked, charge: reaction.Amount));
                for (int i = 0; i < state.Missions.Count; i++)
                    if (state.Missions[i].Remaining > 0 && state.Missions[i].Definition.Kind == MissionKind.Recovery)
                    {
                        MissionContribution recovery = RecoveryRules.Query(state, target, reaction, i);
                        if (recovery != null) result.Add(recovery);
                    }
                return result.AsReadOnly();
            }
            for (int i = 0; i < state.Missions.Count; i++)
            {
                RuntimeMission mission = state.Missions[i];
                if (mission.Remaining == 0) continue;
                MissionContribution contribution = mission.Definition.Kind == MissionKind.Recovery ?
                    RecoveryRules.Query(state, target, reaction, i) : mission.Definition.Kind == MissionKind.Color ?
                    NormalMissionRule.Query(state, target, reaction, i) : mission.Definition.Kind == MissionKind.Crate || mission.Definition.Kind == MissionKind.Scrap || mission.Definition.Kind == MissionKind.Safe || mission.Definition.Kind == MissionKind.ColorLock || mission.Definition.Kind == MissionKind.Appliance ?
                    ObstacleMissionRule.Query(state, target, reaction, i) :
                    LayerMissionRule.Query(state, target, reaction, context, i);
                if (contribution != null) result.Add(contribution);
            }
            return result.AsReadOnly();
        }

        internal static void ConsumeColor(LevelRuntimeState state, RabbitColor? color, BoardCoordinate? source = null)
        {
            if (!color.HasValue) return;
            for (int index = 0; index < state.Missions.Count; index++)
            {
                RuntimeMission mission = state.Missions[index];
                if (mission.Definition.Kind == MissionKind.Color && mission.Definition.Color == color)
                {
                    int before = mission.Progress;
                    mission.Progress = Math.Min(mission.Target, mission.Progress + 1);
                    if (mission.Progress > before)
                        state.RecordMissionProgress(new MissionProgressRecord(index, mission.Progress - before, source, null));
                }
            }
        }

        internal static void Complete(LevelRuntimeState state, MissionKind kind, BoardCoordinate? source = null, int? bodyIndex = null)
        {
            for (int index = 0; index < state.Missions.Count; index++)
            {
                RuntimeMission mission = state.Missions[index];
                if (mission.Definition.Kind == kind)
                {
                    int before = mission.Progress;
                    mission.Progress = Math.Min(mission.Target, mission.Progress + 1);
                    if (mission.Progress > before)
                        state.RecordMissionProgress(new MissionProgressRecord(index, mission.Progress - before, source, bodyIndex));
                }
            }
        }
    }

    internal static class NormalMissionRule
    {
        internal static MissionContribution Query(LevelRuntimeState state, BoardCoordinate target, DamageReaction reaction, int index)
            => state.CellAt(target).Cover == CoverKind.Mold || state.CellAt(target).Content != RuntimeContent.Normal || state.CellAt(target).Color != state.Missions[index].Definition.Color ? null :
                reaction.Response == DamageResponse.Remove ? new MissionContribution(index, 1, 0) :
                reaction.Response == DamageResponse.CoverDamage ? new MissionContribution(index, 0, 1, true) : null;
    }

    internal static class ObstacleMissionRule
    {
        internal static MissionContribution Query(LevelRuntimeState state, BoardCoordinate target, DamageReaction reaction, int index)
        {
            if (reaction.Response != DamageResponse.Damage) return null;
            RuntimeObstacle obstacle = state.Obstacles[state.CellAt(target).ObstacleIndex.Value];
            if (ObstacleDamageRules.MissionForBody(obstacle) != state.Missions[index].Definition.Kind) return null;
            return new MissionContribution(index, obstacle.Durability == reaction.Amount ? 1 : 0, reaction.Amount);
        }
        internal static MissionContribution QueryDefinition(Elements.ElementDefinition definition, LevelRuntimeState state, BoardCoordinate target, DamageReaction reaction, int index)
        {
            RuntimeObstacle obstacle = state.Obstacles[state.CellAt(target).ObstacleIndex.Value];
            if (definition.RequireRemovalMissionProfile().Kind != state.Missions[index].Definition.Kind) return null;
            return new MissionContribution(index, obstacle.Durability == reaction.Amount ? 1 : 0, reaction.Amount);
        }
    }

    internal static class WebMissionRule
    {
        internal static MissionContribution Query(LevelRuntimeState state, BoardCoordinate target, DamageReaction reaction, int index)
            => LayerMissionRule.Query(state, target, reaction, null, index);
    }

    internal static class DustMissionRule
    {
        internal static MissionContribution Query(LevelRuntimeState state, BoardCoordinate target, DamageReaction reaction, TurnEffectContext context, int index)
            => LayerMissionRule.Query(state, target, reaction, context, index);
    }

    internal static class LayerMissionRule
    {
        internal static MissionContribution Query(LevelRuntimeState state, BoardCoordinate target, DamageReaction reaction, TurnEffectContext context, int index)
        {
            RuntimeCell cell = state.CellAt(target);
            Elements.ElementDefinition definition;
            int durability;
            if (cell.Cover.HasValue && reaction.Response == DamageResponse.CoverDamage)
            {
                definition = cell.CoverElement ?? Elements.LegacyElementDefinitions.Get(cell.Cover.Value);
                durability = cell.CoverDurability;
            }
            else if (reaction.Response == DamageResponse.Remove && DustRules.CanDamage(cell, context))
            {
                definition = cell.DustElement ?? Elements.LegacyElementDefinitions.GetDust();
                durability = cell.DustDurability;
            }
            else return null;
            if (state.Missions[index].Definition.Kind != definition.RequireLayer().Mission) return null;
            return QueryDefinition(definition, durability, index);
        }
        internal static MissionContribution QueryDefinition(Elements.ElementDefinition definition, int durability, int index)
        {
            Elements.ElementLayerProfile layer = definition.RequireLayer();
            bool removal = layer.Behavior == Elements.ElementLayerBehavior.CoverRemoval;
            return new MissionContribution(index, removal || durability <= layer.Damage ? 1 : 0, layer.Damage);
        }
    }
}
