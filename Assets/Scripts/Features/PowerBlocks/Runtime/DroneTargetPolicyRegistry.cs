using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Board;
using Elements;
using Levels;

namespace Simulation
{
    internal enum DroneTargetPolicy { Color, Durability, Layer, Recovery, GeneratorTargets }

    /// <summary>남은 미션과 대상 정의의 특성 키만 조회한다. 후보·예약·선택 난수는 목표 관리자가 소유한다.</summary>
    internal sealed class DroneTargetPolicyRegistry
    {
        private delegate MissionContribution PolicyQuery(ElementDefinition definition, LevelRuntimeState state, RuntimeCell cell,
            DamageReaction reaction, TurnEffectContext context, int mission, int? linked);
        private static readonly Dictionary<DroneTargetPolicy, PolicyQuery> Policies = new Dictionary<DroneTargetPolicy, PolicyQuery>
        {
            { DroneTargetPolicy.Color, (definition, state, cell, reaction, context, mission, linked) =>
                NormalMissionRule.Query(state, cell.Coordinate, reaction, mission) },
            { DroneTargetPolicy.Durability, (definition, state, cell, reaction, context, mission, linked) =>
                ObstacleMissionRule.QueryDefinition(definition, state, cell.Coordinate, reaction, mission) },
            { DroneTargetPolicy.Layer, (definition, state, cell, reaction, context, mission, linked) =>
                LayerMissionRule.QueryDefinition(definition, cell.Cover.HasValue ? cell.CoverDurability : cell.DustDurability, mission) },
            { DroneTargetPolicy.Recovery, (definition, state, cell, reaction, context, mission, linked) =>
                RecoveryRules.Query(state, cell.Coordinate, reaction, mission) },
            { DroneTargetPolicy.GeneratorTargets, (definition, state, cell, reaction, context, mission, linked) =>
                new MissionContribution(mission, state.Obstacles[cell.ObstacleIndex.Value].Charge + reaction.Amount >=
                    state.Obstacles[cell.ObstacleIndex.Value].Definition.RequiredCharge ? 1 : 0, 0, bodyIndex: linked, charge: reaction.Amount) }
        };
        private readonly LevelRuntimeState state;
        private readonly TurnEffectContext context;
        private readonly Dictionary<MissionKind, int[]> missions;
        private readonly Dictionary<BoardCoordinate, ReadOnlyCollection<MissionContribution>> cache = new Dictionary<BoardCoordinate, ReadOnlyCollection<MissionContribution>>();
        // 실행 상태에 포함되지 않는 검사·진단용 호출 수량이다.
        private readonly Dictionary<DroneTargetPolicy, int> invocations = new Dictionary<DroneTargetPolicy, int>();

        internal DroneTargetPolicyRegistry(LevelRuntimeState state, TurnEffectContext context)
        {
            this.state = state; this.context = context;
            missions = Enumerable.Range(0, state.Missions.Count).GroupBy(index => state.Missions[index].Definition.Kind)
                .ToDictionary(group => group.Key, group => group.ToArray());
        }
        internal void Invalidate() => cache.Clear();

        internal ReadOnlyCollection<MissionContribution> Query(BoardCoordinate coordinate, ElementDefinition selected = null)
        {
            if (selected == null && cache.TryGetValue(coordinate, out ReadOnlyCollection<MissionContribution> cached))
                return cached.Where(item => state.Missions[item.MissionIndex].Remaining > 0).ToList().AsReadOnly();
            RuntimeCell cell = state.CellAt(coordinate);
            DamageReaction reaction = DamageReaction.Evaluate(state, coordinate, DamageCause.Power, coordinate, context);
            List<MissionContribution> result = new List<MissionContribution>();
            if (reaction.Response == DamageResponse.Charge)
            {
                foreach (int linked in GeneratorRules.Targets(state, cell.ObstacleIndex.Value))
                {
                    ElementDefinition definition = state.Obstacles[linked].Element;
                    Dispatch(DroneTargetPolicy.GeneratorTargets, definition.RequireRemovalMissionProfile().Kind, definition, linked);
                }
                Dispatch(DroneTargetPolicy.Recovery, MissionKind.Recovery, null);
            }
            else
            {
                if (cell.Content == RuntimeContent.Normal && cell.Cover != CoverKind.Mold &&
                    (reaction.Response == DamageResponse.Remove || reaction.Response == DamageResponse.CoverDamage))
                    Dispatch(DroneTargetPolicy.Color, MissionKind.Color, null);
                if (reaction.Response == DamageResponse.Damage)
                {
                    ElementDefinition definition = selected ?? state.Obstacles[cell.ObstacleIndex.Value].Element;
                    Dispatch(DroneTargetPolicy.Durability, definition.RequireRemovalMissionProfile().Kind, definition);
                }
                if (cell.Cover.HasValue && reaction.Response == DamageResponse.CoverDamage)
                {
                    ElementDefinition definition = selected ?? cell.CoverElement ?? LegacyElementDefinitions.Get(cell.Cover.Value);
                    Dispatch(DroneTargetPolicy.Layer, definition.RequireLayer().Mission, definition);
                }
                else if (reaction.Response == DamageResponse.Remove && DustRules.CanDamage(cell, context))
                {
                    ElementDefinition definition = selected ?? cell.DustElement ?? LegacyElementDefinitions.GetDust();
                    Dispatch(DroneTargetPolicy.Layer, definition.RequireLayer().Mission, definition);
                }
                Dispatch(DroneTargetPolicy.Recovery, MissionKind.Recovery, null);
                // 기존 미션 배열의 기여 순서를 유지한다.
                result.Sort((left, right) => left.MissionIndex.CompareTo(right.MissionIndex));
            }
            ReadOnlyCollection<MissionContribution> contributions = result.AsReadOnly();
            if (selected == null) cache[coordinate] = contributions;
            return contributions;

            void Dispatch(DroneTargetPolicy key, MissionKind kind, ElementDefinition definition, int? linked = null)
            {
                if (!missions.TryGetValue(kind, out int[] indices)) return;
                foreach (int index in indices)
                {
                    if (state.Missions[index].Remaining == 0) continue;
                    invocations.TryGetValue(key, out int calls); invocations[key] = calls + 1;
                    MissionContribution contribution = Policies[key](definition, state, cell, reaction, context, index, linked);
                    if (contribution != null) result.Add(contribution);
                }
            }
        }
    }
}
