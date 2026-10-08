namespace Tutorial
{
    /// <summary>구형 결과 DTO를 공통 조건 상태로 변환한다. 별도의 진행기나 집계를 유지하지 않는다.</summary>
    internal static class TutorialLegacyConditionAdapter
    {
        internal static ITutorialConditionState Create(TutorialResultDefinition definition, TutorialHandlerRegistry registry)
        {
            registry.TryGetResult(definition.kind, out ITutorialResultEvaluator evaluator);
            return new TutorialCountConditionState(definition.count, record => record.LegacyResult != null && evaluator.Matches(definition, record.LegacyResult));
        }
    }
}
