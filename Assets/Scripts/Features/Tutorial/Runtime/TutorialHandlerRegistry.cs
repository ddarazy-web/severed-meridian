using System;
using System.Collections.Generic;
using Simulation;

namespace Tutorial
{
    /// <summary>실행 전에 조립하는 종류별 등록. 상태는 실행별 TutorialProgress가 소유한다.</summary>
    public sealed class TutorialHandlerRegistry
    {
        private readonly Dictionary<TutorialStepKind, ITutorialStepHandler> steps = new Dictionary<TutorialStepKind, ITutorialStepHandler>();
        private readonly Dictionary<BoardItem, ITutorialStepHandler> items = new Dictionary<BoardItem, ITutorialStepHandler>();
        private readonly Dictionary<TutorialResultKind, ITutorialResultEvaluator> results = new Dictionary<TutorialResultKind, ITutorialResultEvaluator>();
        private bool frozen;
        private readonly Dictionary<TutorialConditionKind, ITutorialConditionEvaluator> conditions = new Dictionary<TutorialConditionKind, ITutorialConditionEvaluator>();

        public void RegisterCondition(TutorialConditionKind kind, ITutorialConditionEvaluator evaluator)
        {
            if (frozen) throw new InvalidOperationException("실행 중 등록 목록을 변경할 수 없습니다.");
            conditions.Add(kind, evaluator ?? throw new ArgumentNullException(nameof(evaluator)));
        }
        public bool TryGetCondition(TutorialConditionKind kind, out ITutorialConditionEvaluator evaluator) => conditions.TryGetValue(kind, out evaluator);

        public void RegisterStep(TutorialStepKind kind, ITutorialStepHandler handler)
        {
            if (frozen) throw new InvalidOperationException("실행 중 등록 목록을 변경할 수 없습니다.");
            if (handler == null || kind == TutorialStepKind.Item) throw new ArgumentException("아이템은 RegisterItem으로 등록하고 처리기를 지정하세요.");
            steps.Add(kind, handler);
        }
        public void RegisterItem(BoardItem item, ITutorialStepHandler handler)
        {
            if (frozen) throw new InvalidOperationException("실행 중 등록 목록을 변경할 수 없습니다.");
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            items.Add(item, handler);
        }
        public void RegisterResult(TutorialResultKind kind, ITutorialResultEvaluator evaluator)
        {
            if (frozen) throw new InvalidOperationException("실행 중 등록 목록을 변경할 수 없습니다.");
            if (evaluator == null) throw new ArgumentNullException(nameof(evaluator));
            results.Add(kind, evaluator);
        }
        public void Freeze() => frozen = true;
        public bool TryGetStep(TutorialStepDefinition step, out ITutorialStepHandler handler)
        {
            handler = null;
            return step != null && (step.kind == TutorialStepKind.Item ? items.TryGetValue(step.item, out handler) : steps.TryGetValue(step.kind, out handler));
        }
        public bool TryGetResult(TutorialResultKind kind, out ITutorialResultEvaluator evaluator) => results.TryGetValue(kind, out evaluator);
        public static TutorialHandlerRegistry CreateDefault()
        {
            TutorialHandlerRegistry registry = new TutorialHandlerRegistry();
            registry.RegisterCondition(TutorialConditionKind.SuccessfulSwap, new TutorialSwapConditionEvaluator());
            registry.RegisterCondition(TutorialConditionKind.Match, new TutorialMatchConditionEvaluator());
            registry.RegisterCondition(TutorialConditionKind.DurabilityDecrease, new TutorialDurabilityConditionEvaluator());
            registry.RegisterCondition(TutorialConditionKind.RemainingDurability, new TutorialDurabilityConditionEvaluator(true));
            registry.RegisterCondition(TutorialConditionKind.Removed, new TutorialRemovalConditionEvaluator());
            registry.RegisterCondition(TutorialConditionKind.Generated, new TutorialPowerConditionEvaluator(TutorialConditionKind.Generated));
            registry.RegisterCondition(TutorialConditionKind.Activated, new TutorialPowerConditionEvaluator(TutorialConditionKind.Activated));
            registry.RegisterCondition(TutorialConditionKind.Combined, new TutorialPowerConditionEvaluator(TutorialConditionKind.Combined));
            registry.RegisterCondition(TutorialConditionKind.ItemUsed, new TutorialItemConditionEvaluator());
            registry.RegisterCondition(TutorialConditionKind.MissionProgress, new TutorialMissionConditionEvaluator());
            registry.RegisterStep(TutorialStepKind.Description, new TutorialDescriptionHandler());
            registry.RegisterStep(TutorialStepKind.Swap, new TutorialSwapHandler());
            registry.RegisterStep(TutorialStepKind.PowerSwap, new TutorialSwapHandler(true));
            registry.RegisterItem(BoardItem.Hammer, new TutorialItemHandler(BoardItem.Hammer, 1));
            registry.RegisterItem(BoardItem.Swap, new TutorialItemHandler(BoardItem.Swap, 2));
            registry.RegisterItem(BoardItem.Shuffle, new TutorialItemHandler(BoardItem.Shuffle, 0));
            registry.RegisterResult(TutorialResultKind.Generated, new TutorialResultEvaluator(TutorialResultKind.Generated));
            registry.RegisterResult(TutorialResultKind.Activated, new TutorialResultEvaluator(TutorialResultKind.Activated));
            registry.RegisterResult(TutorialResultKind.Removed, new TutorialResultEvaluator(TutorialResultKind.Removed));
            registry.Freeze(); return registry;
        }
    }
}
