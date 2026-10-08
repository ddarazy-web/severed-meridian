using System;
using Board;
using Levels;
using Simulation;

namespace Tutorial
{
    /// <summary>게임에서 이미 판정한 실제 사건. 같은 사건 ID는 같은 단계에서 한 번만 센다.</summary>
    public readonly struct TutorialConditionEvent
    {
        public TutorialConditionKind Kind { get; }
        public string Id { get; }
        public int MatchSize { get; }
        public RabbitColor Color { get; }
        public bool DirectSwap { get; }
        public TutorialTargetEntity Entity { get; }
        public BoardCoordinate Position { get; }
        public int Amount { get; }
        public EffectOrigin Cause { get; }
        public BoardItem? Item { get; }
        public int? MissionIndex { get; }
        public TutorialConditionEvent(TutorialConditionKind kind, string id, int matchSize = 0, RabbitColor color = default, bool directSwap = false) : this()
        { Kind = kind; Id = id; MatchSize = matchSize; Color = color; DirectSwap = directSwap; Entity = null; Position = default; Amount = 1; Cause = EffectOrigin.Unknown; Item = null; }
        public TutorialConditionEvent(TutorialConditionKind kind, string id, TutorialTargetEntity entity, BoardCoordinate position, int amount, EffectOrigin cause, bool directSwap = false) : this()
        { Kind = kind; Id = id; Entity = entity; Position = position; Amount = amount; Cause = cause; MatchSize = 0; Color = default; DirectSwap = directSwap; Item = null; }
        public TutorialConditionEvent(string id, BoardItem item) : this()
        { Kind = TutorialConditionKind.ItemUsed; Id = id; Entity = null; Position = default; Amount = 1; Cause = EffectOrigin.Unknown; MatchSize = 0; Color = default; DirectSwap = false; Item = item; }
        public TutorialConditionEvent(string id, int missionIndex, int amount)
        { Kind = TutorialConditionKind.MissionProgress; Id = id; Entity = null; Position = default; Amount = amount; Cause = EffectOrigin.Unknown; MatchSize = 0; Color = default; DirectSwap = false; Item = null; MissionIndex = missionIndex; }
    }

    public interface ITutorialConditionEvaluator
    {
        void Validate(TutorialConditionDefinition condition, TutorialValidationContext context);
        ITutorialConditionState Create(TutorialConditionDefinition condition, TutorialConditionContext context);
        int MinimumActions(TutorialConditionDefinition condition);
    }

    public sealed class TutorialSwapConditionEvaluator : ITutorialConditionEvaluator
    {
        public void Validate(TutorialConditionDefinition condition, TutorialValidationContext context)
        { if (condition.requiredCount <= 0) context.Error(".requiredCount", "교환 횟수는 1 이상이어야 합니다."); }
        public bool Matches(TutorialConditionDefinition condition, TutorialConditionEvent record)
            => record.Kind == TutorialConditionKind.SuccessfulSwap;
        public ITutorialConditionState Create(TutorialConditionDefinition condition, TutorialConditionContext context)
            => new TutorialCountConditionState(condition.requiredCount, record => Matches(condition, record));
        public int MinimumActions(TutorialConditionDefinition condition) => Math.Max(1, condition.requiredCount);
    }

    public sealed class TutorialItemConditionEvaluator : ITutorialConditionEvaluator
    {
        public void Validate(TutorialConditionDefinition condition, TutorialValidationContext context)
        {
            if (condition.requiredCount <= 0) context.Error(".requiredCount", "아이템 사용 성공 횟수는 1 이상입니다.");
            if (!Enum.IsDefined(typeof(BoardItem), condition.item)) context.Error(".item", "지원하지 않는 아이템입니다.");
        }
        public ITutorialConditionState Create(TutorialConditionDefinition condition, TutorialConditionContext context)
            => new TutorialCountConditionState(condition.requiredCount, record => record.Kind == TutorialConditionKind.ItemUsed && record.Item == condition.item);
        public int MinimumActions(TutorialConditionDefinition condition) => Math.Max(1, condition.requiredCount);
    }

    public sealed class TutorialMissionConditionEvaluator : ITutorialConditionEvaluator
    {
        public void Validate(TutorialConditionDefinition condition, TutorialValidationContext context)
        {
            if (condition.requiredCount <= 0) context.Error(".requiredCount", "미션 진행 증가량은 1 이상입니다.");
            context.ValidateMission(condition);
        }
        public ITutorialConditionState Create(TutorialConditionDefinition condition, TutorialConditionContext context)
        {
            if (context.MissionRemaining != null && (condition.missionIndex < 0 || condition.missionIndex >= context.MissionRemaining.Count ||
                context.MissionRemaining[condition.missionIndex] < condition.requiredCount))
                throw new InvalidOperationException("미션의 남은 목표량보다 큰 진행 증가량을 요구합니다.");
            return new TutorialMissionConditionState(condition.missionIndex, condition.requiredCount);
        }
        public int MinimumActions(TutorialConditionDefinition condition) => 1;
    }

    public sealed class TutorialMatchConditionEvaluator : ITutorialConditionEvaluator
    {
        public void Validate(TutorialConditionDefinition condition, TutorialValidationContext context)
        {
            if (condition.requiredCount <= 0) context.Error(".requiredCount", "매칭 횟수는 1 이상이어야 합니다.");
            if (condition.matchSize < 3 || condition.matchSize > 81) context.Error(".matchSize", "매칭 크기는 3~81개여야 합니다.");
            if (!Enum.IsDefined(typeof(TutorialMatchSizeComparison), condition.sizeComparison)) context.Error(".sizeComparison", "지원하지 않는 크기 비교입니다.");
            if (!Enum.IsDefined(typeof(TutorialMatchOrigin), condition.origin)) context.Error(".origin", "지원하지 않는 매칭 원인입니다.");
            if (!condition.anyColor && !Enum.IsDefined(typeof(RabbitColor), condition.color)) context.Error(".color", "지원하지 않는 블록 색상입니다.");
        }
        public bool Matches(TutorialConditionDefinition condition, TutorialConditionEvent record)
            => record.Kind == TutorialConditionKind.Match && (condition.anyColor || record.Color == condition.color)
                && (condition.origin == TutorialMatchOrigin.IncludingCascade || record.DirectSwap)
                && (condition.sizeComparison == TutorialMatchSizeComparison.Exactly ? record.MatchSize == condition.matchSize : record.MatchSize >= condition.matchSize);
        public ITutorialConditionState Create(TutorialConditionDefinition condition, TutorialConditionContext context)
            => new TutorialCountConditionState(condition.requiredCount, record => Matches(condition, record));
        public int MinimumActions(TutorialConditionDefinition condition) => 1;
    }
}
