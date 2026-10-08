using System;
using System.Linq;
using Simulation;

namespace Tutorial
{
    internal static class TutorialConditionTargetValidation
    {
        internal static void Validate(TutorialConditionDefinition condition, TutorialValidationContext context)
        {
            if (condition.target == null) { context.Error(".target", "조건 대상을 지정하세요."); return; }
            TutorialTargetDefinition target = condition.target;
            if (!Enum.IsDefined(typeof(TutorialTargetKind), target.kind)) context.Error(".target.kind", "지원하지 않는 대상 종류입니다.");
            if (!Enum.IsDefined(typeof(TutorialTargetLayer), target.layer)) context.Error(".target.layer", "지원하지 않는 대상 층입니다.");
            if (target.kind == TutorialTargetKind.Entity) context.Cell(target.coordinate, ".target.coordinate");
            if (target.kind == TutorialTargetKind.Definition) context.Resolve(target.definitionId, ".target.definitionId");
            if (target.kind == TutorialTargetKind.Area)
            {
                if (target.cells == null || target.cells.Count == 0) context.Error(".target.cells", "영역 셀을 지정하세요.");
                else for (int i = 0; i < target.cells.Count; i++) context.Cell(target.cells[i], $".target.cells.Array.data[{i}]");
            }
            if (target.kind == TutorialTargetKind.Generated && string.IsNullOrWhiteSpace(target.binding)) context.Error(".target.binding", "생성 결과 연결 이름을 지정하세요.");
            if (condition.allowedOrigins == null || condition.allowedOrigins.Any(value => value == EffectOrigin.Unknown || !Enum.IsDefined(typeof(EffectOrigin), value)))
                context.Error(".allowedOrigins", "지원하는 발생 원인을 지정하세요.");
            context.ValidateCapabilities(condition);
        }
    }

    public sealed class TutorialDurabilityConditionEvaluator : ITutorialConditionEvaluator
    {
        private readonly bool remaining;
        public TutorialDurabilityConditionEvaluator(bool remaining = false) { this.remaining = remaining; }
        public void Validate(TutorialConditionDefinition condition, TutorialValidationContext context)
        {
            TutorialConditionTargetValidation.Validate(condition, context);
            if (condition.requiredCount < (remaining ? 0 : 1)) context.Error(".requiredCount", remaining ? "남은 내구도는 0 이상입니다." : "감소량은 1 이상입니다.");
            if (!Enum.IsDefined(typeof(TutorialDamageAggregation), condition.aggregation)) context.Error(".aggregation", "지원하지 않는 집계 방식입니다.");
        }
        public ITutorialConditionState Create(TutorialConditionDefinition condition, TutorialConditionContext context)
            => new TutorialDurabilityConditionState(condition, context, remaining);
        public int MinimumActions(TutorialConditionDefinition condition) => remaining ? 0 : 1;
    }

    public sealed class TutorialRemovalConditionEvaluator : ITutorialConditionEvaluator
    {
        public int MinimumActions(TutorialConditionDefinition condition) => 1;
        public void Validate(TutorialConditionDefinition condition, TutorialValidationContext context)
        {
            TutorialConditionTargetValidation.Validate(condition, context);
            if (condition.requiredCount <= 0) context.Error(".requiredCount", "제거 개수는 1 이상입니다.");
            if (condition.allowedOrigins?.Count == 0) context.Error(".allowedOrigins", "제거를 인정할 원인을 하나 이상 지정하세요.");
        }
        public ITutorialConditionState Create(TutorialConditionDefinition condition, TutorialConditionContext context)
        {
            TutorialTargetSelection selection = TutorialTargetSelection.Bind(condition.target, context.Entities, context.Bindings);
            return new TutorialCountConditionState(condition.requiredCount, record => record.Kind == TutorialConditionKind.Removed &&
                record.Entity != null && selection.Matches(record.Entity, record.Position) && condition.allowedOrigins.Contains(record.Cause));
        }
    }
}
