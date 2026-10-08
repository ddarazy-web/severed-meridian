using System;
using System.Collections.Generic;
using System.Linq;
using Elements;
using Levels;
using Simulation;

namespace Tutorial
{
    /// <summary>파워 종류는 정의 데이터로 필터하고 생성·직접 발동·조합 사건을 분리한다.</summary>
    public sealed class TutorialPowerConditionEvaluator : ITutorialConditionEvaluator
    {
        private readonly TutorialConditionKind kind;
        public TutorialPowerConditionEvaluator(TutorialConditionKind kind) { this.kind = kind; }
        public int MinimumActions(TutorialConditionDefinition condition) => kind == TutorialConditionKind.Generated ? 1 : Math.Max(1, condition.requiredCount);
        public void Validate(TutorialConditionDefinition condition, TutorialValidationContext context)
        {
            TutorialConditionTargetValidation.Validate(condition, context);
            if (condition.requiredCount <= 0) context.Error(".requiredCount", "필요 횟수는 1 이상입니다.");
            if (!string.IsNullOrEmpty(condition.bindGeneratedAs) && (kind != TutorialConditionKind.Generated || condition.requiredCount != 1 || string.IsNullOrWhiteSpace(condition.bindGeneratedAs)))
                context.Error(".bindGeneratedAs", "생성 결과 연결은 생성1개 조건에 공백이 아닌 이름으로 지정하세요.");
            if (condition.target?.layer != TutorialTargetLayer.Content) context.Error(".target.layer", "파워는 블록 층에서 선택하세요.");
            if (kind == TutorialConditionKind.Combined)
            {
                if (condition.allowedOrigins == null || condition.allowedOrigins.Count == 0 || condition.allowedOrigins.Any(value => value < EffectOrigin.RocketRocket))
                    context.Error(".allowedOrigins", "두 파워의 조합 원인을 하나 이상 선택하세요.");
                return;
            }
            ElementDefinition definition = context.Resolve(condition.powerDefinitionId, ".powerDefinitionId", true);
            if (!condition.anyDirection && (!Enum.IsDefined(typeof(RocketDirection), condition.rocketDirection) ||
                definition != null && definition.Supply?.Content != RuntimeContent.Rocket))
                context.Error(".rocketDirection", "방향 선택은 로켓 생성·발동 조건에서만 사용할 수 있습니다.");
            if (kind == TutorialConditionKind.Generated && condition.target?.kind != TutorialTargetKind.Board && condition.target?.kind != TutorialTargetKind.Area)
                context.Error(".target.kind", "생성은 보드 전체 또는 생성 위치 영역을 대상으로 지정하세요.");
        }
        public ITutorialConditionState Create(TutorialConditionDefinition condition, TutorialConditionContext context)
        {
            TutorialTargetSelection selection = TutorialTargetSelection.Bind(condition.target, context.Entities, context.Bindings);
            var counter = new TutorialCountConditionState(condition.requiredCount, record => record.Kind == kind && record.Entity != null &&
                selection.Matches(record.Entity, record.Position) && (kind == TutorialConditionKind.Combined
                    ? condition.allowedOrigins.Contains(record.Cause)
                    : record.Entity.Definition.Id.Value == condition.powerDefinitionId && (condition.anyDirection || record.Entity.RocketDirection == condition.rocketDirection) &&
                        (kind != TutorialConditionKind.Activated || record.DirectSwap)));
            return string.IsNullOrEmpty(condition.bindGeneratedAs) ? counter : new TutorialGeneratedBindingState(counter, condition.bindGeneratedAs, context);
        }
    }

    /// <summary>생성 사건을 끝까지 모은 뒤 단일 생존 개체만 이름에 연결한다.</summary>
    internal sealed class TutorialGeneratedBindingState : ITutorialConditionState
    {
        private ITutorialConditionState counter;
        private readonly string name;
        private readonly TutorialConditionContext context;
        private HashSet<long> generated = new HashSet<long>();
        private bool preview;
        internal TutorialGeneratedBindingState(ITutorialConditionState counter, string name, TutorialConditionContext context)
        { this.counter = counter; this.name = name; this.context = context; }
        public int Count => counter.Count;
        public long ProgressValue => counter.ProgressValue;
        public ITutorialConditionState Fork()
        {
            var copy = (TutorialGeneratedBindingState)MemberwiseClone();
            copy.counter = counter.Fork(); copy.generated = new HashSet<long>(generated); copy.preview = true; return copy;
        }
        public bool IsSatisfied => counter.IsSatisfied;
        public bool Accept(TutorialConditionEvent record)
        {
            if (!counter.Accept(record)) return false;
            generated.Add(record.Entity.Occurrence); return true;
        }
        public void Refresh(IReadOnlyList<TutorialTargetEntity> current)
        {
            if (!IsSatisfied) return;
            if (generated.Count != 1) throw new InvalidOperationException("생성 결과가 여러 개입니다. 종류·생성 위치로 대상을 구분하세요: " + name);
            long id = generated.Single();
            if (!current.Any(entity => entity.Occurrence == id)) throw new InvalidOperationException("생성 결과 개체가 이미 소실되었습니다: " + name);
            if (!preview) context.BindGenerated(name, id);
        }
    }
}
