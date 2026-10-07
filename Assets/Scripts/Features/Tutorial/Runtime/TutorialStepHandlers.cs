using System.Collections.Generic;
using Simulation;

namespace Tutorial
{
    public sealed class TutorialDescriptionHandler : ITutorialStepHandler
    {
        public bool IsDescription => true;
        public bool UsesFreeItem => false;
        public bool ConsumesMove => false;
        public void Validate(TutorialStepDefinition step, TutorialValidationContext context)
        {
            context.ValidateTargets(step, 0);
            if (step.results?.Count > 0) context.Error(".results", "설명 단계에는 실행 결과 조건을 지정할 수 없습니다.");
        }
        public IEnumerable<string> References(TutorialStepDefinition step) { yield break; }
        public bool Allows(TutorialStepDefinition step, TutorialInput input) => input.Kind == TutorialInputKind.Next;
        public bool IsSuccessful(TutorialStepDefinition step, TutorialInput input, bool succeeded, string definitionId) => succeeded;
    }

    public sealed class TutorialSwapHandler : ITutorialStepHandler
    {
        private readonly bool power;
        public bool IsDescription => false;
        public bool UsesFreeItem => false;
        public bool ConsumesMove => true;
        public TutorialSwapHandler(bool power = false) { this.power = power; }
        public void Validate(TutorialStepDefinition step, TutorialValidationContext context)
        {
            context.ValidateTargets(step, 2);
            if (power) context.Resolve(step.actionDefinitionId, ".actionDefinitionId", true);
            context.ValidateInitialTargets(step, 2, power ? step.actionDefinitionId : null);
        }
        public IEnumerable<string> References(TutorialStepDefinition step) { if (power) yield return step.actionDefinitionId; }
        public bool Allows(TutorialStepDefinition step, TutorialInput input)
            => input.Kind == TutorialInputKind.Swap && TutorialTargetRules.Matches(step, input, 2);
        public bool IsSuccessful(TutorialStepDefinition step, TutorialInput input, bool succeeded, string definitionId)
            => succeeded && (!power || step.actionDefinitionId == definitionId);
    }

    public sealed class TutorialItemHandler : ITutorialStepHandler
    {
        private readonly BoardItem item;
        private readonly int targets;
        public bool IsDescription => false;
        public bool UsesFreeItem => true;
        public bool ConsumesMove => false;
        public TutorialItemHandler(BoardItem item, int targets) { this.item = item; this.targets = targets; }
        public void Validate(TutorialStepDefinition step, TutorialValidationContext context)
        { context.ValidateTargets(step, targets); context.ValidateInitialTargets(step, targets); }
        public IEnumerable<string> References(TutorialStepDefinition step) { yield break; }
        public bool Allows(TutorialStepDefinition step, TutorialInput input)
            => input.Kind == TutorialInputKind.Item && input.Item == item && TutorialTargetRules.Matches(step, input, targets);
        public bool IsSuccessful(TutorialStepDefinition step, TutorialInput input, bool succeeded, string definitionId) => succeeded;
    }

    /// <summary>일반 교환·파워 교환·교환/망치 아이템이 공유하는 대상 규칙.</summary>
    internal static class TutorialTargetRules
    {
        internal static bool Matches(TutorialStepDefinition step, TutorialInput input, int count)
        {
            if (count == 0) return !input.First.HasValue && !input.Second.HasValue;
            if (!input.First.HasValue) return false;
            if (count == 1) return input.First.Value.Equals(step.first) && !input.Second.HasValue;
            if (!input.Second.HasValue) return false;
            // 실제 교환은 두 방향이 동일하다. 안내 화살표의 첫→둘째 방향과 실행 허용을 분리한다.
            return input.First.Value.Equals(step.first) && input.Second.Value.Equals(step.second)
                || input.First.Value.Equals(step.second) && input.Second.Value.Equals(step.first);
        }
    }
}
