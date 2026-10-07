using System.Collections.Generic;

namespace Tutorial
{
    /// <summary>결과 종류는 등록 시 고정하고 본체 기능은 정의 ID로 구분한다. 집계 상태는 진행부에 둔다.</summary>
    public sealed class TutorialResultEvaluator : ITutorialResultEvaluator
    {
        private readonly TutorialResultKind kind;
        public TutorialResultEvaluator(TutorialResultKind kind) { this.kind = kind; }
        public void Validate(TutorialResultDefinition result, TutorialValidationContext context)
        {
            if (result.count <= 0) context.Error("", "조건 종류·수량이 잘못됐습니다.");
            context.Resolve(result.definitionId, ".definitionId", kind != TutorialResultKind.Removed);
            if (result.hasCoordinate) context.Cell(result.coordinate, ".coordinate");
        }
        public IEnumerable<string> References(TutorialResultDefinition result) { yield return result.definitionId; }
        public bool Matches(TutorialResultDefinition result, TutorialResultRecord record)
            => record.Kind == kind && record.DefinitionId == result.definitionId
                && (!result.hasCoordinate || record.Coordinate.HasValue && record.Coordinate.Value.Equals(result.coordinate));
    }
}
