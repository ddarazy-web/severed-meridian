using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Levels;

namespace Simulation
{
    public enum GeneratorEvent { Charged, Activated, Disconnected, Retired }

    public sealed class GeneratorRecord
    {
        public GeneratorEvent Event { get; }
        public int GeneratorIndex { get; }
        public int? TargetIndex { get; }
        public int ChargeBefore { get; }
        public int ChargeAfter { get; }
        internal GeneratorRecord(GeneratorEvent kind, int generator, int? target, int before, int after)
        { Event = kind; GeneratorIndex = generator; TargetIndex = target; ChargeBefore = before; ChargeAfter = after; }
    }

    // 연결 정의는 불변이다. 실제 점유에서 활성 연결을 파생해 사본과 원본의 수명을 분리한다.
    public static class GeneratorRules
    {
        internal static bool Alive(LevelRuntimeState state, int index) => state.Cells.Any(c => c.ObstacleIndex == index);
        internal static int Find(LevelRuntimeState state, string id)
        {
            for (int i = 0; i < state.Obstacles.Count; i++)
                if (state.Obstacles[i].Definition.Id == id) return i;
            return -1;
        }
        public static ReadOnlyCollection<RuntimeConnection> ActiveConnections(LevelRuntimeState state) => state.Connections
            .Where(c => Alive(state, Find(state, c.GeneratorId)) && Alive(state, Find(state, c.TargetId))).ToList().AsReadOnly();
        internal static IEnumerable<int> Targets(LevelRuntimeState state, int generator) => ActiveConnections(state)
            .Where(c => c.GeneratorId == state.Obstacles[generator].Definition.Id).Select(c => Find(state, c.TargetId));

        internal static DamageReaction Query(LevelRuntimeState state, int index, TurnEffectContext context)
            => QueryDefinition(state.Obstacles[index].Element, state, index, context);

        internal static DamageReaction QueryDefinition(Elements.ElementDefinition definition, LevelRuntimeState state, int index, TurnEffectContext context)
            => context?.HasCharged(index) == true ? new DamageReaction(DamageResponse.AlreadyDamaged, "발전기 본체별 수당 최대 1 충전") :
                new DamageReaction(DamageResponse.Charge, "발전기 충전", definition.RequireChargePlacement().ChargePerHit);

        internal static void Apply(LevelRuntimeState state, int index, TurnEffectContext context)
            => ApplyDefinition(state.Obstacles[index].Element, state, index, context);

        internal static void ApplyDefinition(Elements.ElementDefinition definition, LevelRuntimeState state, int index, TurnEffectContext context)
        {
            RuntimeObstacle generator = state.Obstacles[index];
            int before = generator.Charge;
            context.RegisterCharge(index);
            generator.Charge += definition.RequireChargePlacement().ChargePerHit;
            context.RecordGenerator(new GeneratorRecord(GeneratorEvent.Charged, index, null, before, generator.Charge));
            if (generator.Charge < generator.Definition.RequiredCharge) return;
            int[] targets = Targets(state, index).ToArray();
            context.RecordGenerator(new GeneratorRecord(GeneratorEvent.Activated, index, null, before, generator.Charge));
            foreach (int target in targets) ObstacleDamageRules.Remove(state, target);
            foreach (int target in targets)
                context.RecordGenerator(new GeneratorRecord(GeneratorEvent.Disconnected, index, target, generator.Charge, generator.Charge));
            ObstacleDamageRules.Remove(state, index);
        }

        internal static void TargetRemoved(LevelRuntimeState state, int target, TurnEffectContext context)
        {
            foreach (RuntimeConnection connection in state.Connections.Where(c => c.TargetId == state.Obstacles[target].Definition.Id))
            {
                int index = Find(state, connection.GeneratorId);
                if (!Alive(state, index)) continue;
                int charge = state.Obstacles[index].Charge;
                context.RecordGenerator(new GeneratorRecord(GeneratorEvent.Disconnected, index, target, charge, charge));
                if (Targets(state, index).Any()) continue;
                ObstacleDamageRules.Remove(state, index);
                context.RecordGenerator(new GeneratorRecord(GeneratorEvent.Retired, index, null, charge, charge));
            }
        }
    }
}
