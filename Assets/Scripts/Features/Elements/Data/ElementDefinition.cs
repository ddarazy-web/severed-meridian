using System;

namespace Elements
{
    /// <summary>제작 에셋·실행 상태를 참조하지 않는 불변 정의 조회 값.</summary>
    public sealed class ElementDefinition
    {
        public ElementId Id { get; }
        public string DisplayName { get; }
        public ElementPlacementProfile Placement { get; }
        public ElementChargePlacementProfile ChargePlacement { get; }

        /// <param name="id">유효한 영구 정의 ID.</param><param name="displayName">조회 키와 독립된 표시명 원문.</param>
        public ElementDefinition(ElementId id, string displayName)
            : this(id, displayName, null) { }

        /// <param name="id">유효한 영구 정의 ID.</param><param name="displayName">조회 키와 독립된 표시명 원문.</param>
        /// <param name="placement">불변 배치 수치. 메타데이터 전용 정의는 null을 허용한다.</param>
        public ElementDefinition(ElementId id, string displayName, ElementPlacementProfile placement)
            : this(id, displayName, placement, null) { }

        /// <param name="id">유효한 영구 정의 ID.</param><param name="displayName">조회 키와 독립된 표시명 원문.</param>
        /// <param name="placement">내구도형 배치 수치. 해당하지 않는 정의는 null.</param>
        /// <param name="chargePlacement">충전형 배치 수치. 해당하지 않는 정의는 null.</param>
        public ElementDefinition(ElementId id, string displayName, ElementPlacementProfile placement, ElementChargePlacementProfile chargePlacement)
        {
            if (!id.IsValid) throw new ArgumentException("정의 ID가 초기화되지 않았습니다.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException($"요소 '{id.Value}'의 표시명은 빈 값일 수 없습니다.", nameof(displayName));
            Id = id;
            DisplayName = displayName;
            Placement = placement;
            ChargePlacement = chargePlacement;
        }

        /// <returns>배치 수치. 누락은 정의 ID를 포함한 오류로 거절한다.</returns>
        public ElementPlacementProfile RequirePlacement() => Placement ??
            throw new InvalidOperationException($"요소 '{Id.Value}'의 배치 프로필이 없습니다.");

        /// <returns>충전형 배치 수치. 누락은 정의 ID를 포함한 오류로 거절한다.</returns>
        public ElementChargePlacementProfile RequireChargePlacement() => ChargePlacement ??
            throw new InvalidOperationException($"요소 '{Id.Value}'의 충전 배치 프로필이 없습니다.");
    }
}
