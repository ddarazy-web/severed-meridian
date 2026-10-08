using System;
using System.Collections.Generic;
using System.Linq;

namespace Tutorial
{
    public interface ITutorialConditionState
    {
        int Count { get; }
        long ProgressValue { get; }
        ITutorialConditionState Fork();
        bool IsSatisfied { get; }
        bool Accept(TutorialConditionEvent record);
        void Refresh(IReadOnlyList<TutorialTargetEntity> current);
    }

    public sealed class TutorialConditionContext
    {
        public IReadOnlyList<TutorialTargetEntity> Entities { get; }
        public IReadOnlyDictionary<string, long> Bindings { get; }
        public IReadOnlyList<int> MissionRemaining { get; }
        private readonly Action<string, long> bindGenerated;
        public TutorialConditionContext(IReadOnlyList<TutorialTargetEntity> entities, IReadOnlyDictionary<string, long> bindings, Action<string, long> bindGenerated = null, IReadOnlyList<int> missionRemaining = null)
        { Entities = entities; Bindings = bindings; this.bindGenerated = bindGenerated; MissionRemaining = missionRemaining; }
        internal void BindGenerated(string name, long occurrence)
        {
            if (bindGenerated == null) throw new InvalidOperationException("생성 결과 연결을 보관할 실행 문맥이 없습니다.");
            bindGenerated(name, occurrence);
        }
    }

    internal sealed class TutorialCountConditionState : ITutorialConditionState
    {
        private readonly int required;
        private readonly Func<TutorialConditionEvent, bool> matches;
        public int Count { get; private set; }
        public long ProgressValue => Count;
        public ITutorialConditionState Fork() => (TutorialCountConditionState)MemberwiseClone();
        public bool IsSatisfied => Count >= required;
        internal TutorialCountConditionState(int required, Func<TutorialConditionEvent, bool> matches)
        { this.required = required; this.matches = matches; }
        public bool Accept(TutorialConditionEvent record) { if (!matches(record)) return false; Count++; return true; }
        public void Refresh(IReadOnlyList<TutorialTargetEntity> current) { }
    }

    internal sealed class TutorialMissionConditionState : ITutorialConditionState
    {
        private readonly int missionIndex;
        private readonly int required;
        public int Count { get; private set; }
        public long ProgressValue => Count;
        public ITutorialConditionState Fork() => (TutorialMissionConditionState)MemberwiseClone();
        public bool IsSatisfied => Count >= required;
        internal TutorialMissionConditionState(int missionIndex, int required)
        { this.missionIndex = missionIndex; this.required = required; }
        public bool Accept(TutorialConditionEvent record)
        {
            if (record.Kind != TutorialConditionKind.MissionProgress || record.MissionIndex != missionIndex || record.Amount <= 0) return false;
            Count = (int)Math.Min(int.MaxValue, (long)Count + record.Amount);
            return true;
        }
        public void Refresh(IReadOnlyList<TutorialTargetEntity> current) { }
    }

    internal sealed class TutorialDurabilityConditionState : ITutorialConditionState
    {
        private readonly TutorialConditionDefinition condition;
        private readonly TutorialTargetSelection selection;
        private Dictionary<long, int> values;
        private HashSet<long> removed = new HashSet<long>();
        private readonly bool remaining;
        private int total;
        public long ProgressValue => remaining ? values.Values.Count(value => value == condition.requiredCount) :
            condition.aggregation == TutorialDamageAggregation.Total ? total : values.Values.Sum(value => (long)Math.Min(value, condition.requiredCount));
        public ITutorialConditionState Fork()
        {
            var copy = (TutorialDurabilityConditionState)MemberwiseClone();
            copy.values = new Dictionary<long, int>(values); copy.removed = new HashSet<long>(removed); return copy;
        }
        public int Count => remaining ? (values.Count == 1 ? values.Values.Single() : values.Values.Count(value => value == condition.requiredCount)) :
            condition.aggregation == TutorialDamageAggregation.Total ? total : values.Values.Min();
        public bool IsSatisfied => remaining ? values.Values.All(value => value == condition.requiredCount) :
            condition.aggregation == TutorialDamageAggregation.Total ? total >= condition.requiredCount : values.Values.All(value => value >= condition.requiredCount);

        internal TutorialDurabilityConditionState(TutorialConditionDefinition condition, TutorialConditionContext context, bool remaining)
        {
            this.condition = condition; this.remaining = remaining;
            selection = TutorialTargetSelection.Bind(condition.target, context.Entities, context.Bindings);
            var selected = context.Entities.Where(entity => selection.InitialOccurrences.Contains(entity.Occurrence) && entity.Durability.HasValue).ToArray();
            if (selected.Length == 0) throw new InvalidOperationException("내구도를 가진 대상이 없습니다.");
            if (remaining || condition.aggregation == TutorialDamageAggregation.Each)
            {
                if (selected.Any(entity => entity.Durability.Value < condition.requiredCount))
                    throw new InvalidOperationException("대상의 남은 내구도보다 큰 값을 요구합니다.");
            }
            else if (selected.Sum(entity => (long)entity.Durability.Value) < condition.requiredCount)
                throw new InvalidOperationException("전체 남은 내구도보다 큰 감소량을 요구합니다.");
            values = selected.ToDictionary(entity => entity.Occurrence, entity => remaining ? entity.Durability.Value : 0);
        }

        public bool Accept(TutorialConditionEvent record)
        {
            if (record.Entity == null || !selection.Matches(record.Entity, record.Position)) return false;
            long id = record.Entity.Occurrence;
            if (remaining)
            {
                if (!values.ContainsKey(id)) return false;
                if (record.Kind == TutorialConditionKind.Removed) { values[id] = 0; removed.Add(id); }
                else if (record.Kind == TutorialConditionKind.DurabilityDecrease && record.Entity.Durability.HasValue) values[id] = record.Entity.Durability.Value;
                return record.Kind == TutorialConditionKind.Removed || record.Kind == TutorialConditionKind.DurabilityDecrease;
            }
            if (record.Kind != TutorialConditionKind.DurabilityDecrease || record.Amount <= 0 ||
                condition.allowedOrigins.Count > 0 && !condition.allowedOrigins.Contains(record.Cause)) return false;
            if (condition.aggregation == TutorialDamageAggregation.Each)
            { if (values.ContainsKey(id)) values[id] += record.Amount; }
            else total += record.Amount;
            return true;
        }

        public void Refresh(IReadOnlyList<TutorialTargetEntity> current)
        {
            if (!remaining) return;
            foreach (long id in values.Keys.ToArray())
            {
                TutorialTargetEntity entity = current.FirstOrDefault(value => value.Occurrence == id);
                if (entity?.Durability != null) values[id] = entity.Durability.Value;
                else if (!removed.Contains(id)) throw new InvalidOperationException("남은 내구도를 확인할 대상이 소실되었습니다.");
            }
        }
    }
}
