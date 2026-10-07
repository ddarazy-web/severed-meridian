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
