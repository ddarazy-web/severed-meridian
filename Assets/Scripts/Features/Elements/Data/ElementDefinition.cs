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
        public ElementDamageSourcePolicy DamageSourcePolicy { get; }
        public ElementColorMatchPolicy ColorMatchPolicy { get; }
        public ElementDamageAggregationPolicy DamageAggregationPolicy { get; }
        public ElementRemovalMissionProfile RemovalMissionProfile { get; }
        public ElementReactionBehavior? ReactionBehavior { get; }
        public ElementLayerProfile Layer { get; }
        public ElementTurnProfile Turn { get; }
        public ElementSupplyProfile Supply { get; }

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
            : this(id, displayName, placement, chargePlacement, null) { }

        /// <param name="id">유효한 영구 정의 ID.</param><param name="displayName">조회 키와 독립된 표시명 원문.</param>
        /// <param name="placement">내구도형 배치 수치. 해당하지 않는 정의는 null.</param>
        /// <param name="chargePlacement">충전형 배치 수치. 해당하지 않는 정의는 null.</param>
        /// <param name="damageSourcePolicy">피해 원인 허용 값. 해당하지 않는 정의는 null.</param>
        public ElementDefinition(ElementId id, string displayName, ElementPlacementProfile placement, ElementChargePlacementProfile chargePlacement, ElementDamageSourcePolicy damageSourcePolicy)
            : this(id, displayName, placement, chargePlacement, damageSourcePolicy, null) { }

        /// <param name="id">유효한 영구 정의 ID.</param><param name="displayName">조회 키와 독립된 표시명 원문.</param>
        /// <param name="placement">내구도형 배치 수치. 해당하지 않는 정의는 null.</param>
        /// <param name="chargePlacement">충전형 배치 수치. 해당하지 않는 정의는 null.</param>
        /// <param name="damageSourcePolicy">피해 원인 허용 값. 해당하지 않는 정의는 null.</param>
        /// <param name="colorMatchPolicy">색 일치 조건. 해당하지 않는 정의는 null.</param>
        public ElementDefinition(ElementId id, string displayName, ElementPlacementProfile placement, ElementChargePlacementProfile chargePlacement, ElementDamageSourcePolicy damageSourcePolicy, ElementColorMatchPolicy colorMatchPolicy)
            : this(id, displayName, placement, chargePlacement, damageSourcePolicy, colorMatchPolicy, null) { }

        /// <param name="id">유효한 영구 정의 ID.</param><param name="displayName">조회 키와 독립된 표시명 원문.</param>
        /// <param name="placement">내구도형 배치 수치. 해당하지 않는 정의는 null.</param>
        /// <param name="chargePlacement">충전형 배치 수치. 해당하지 않는 정의는 null.</param>
        /// <param name="damageSourcePolicy">피해 원인 허용 값. 해당하지 않는 정의는 null.</param>
        /// <param name="colorMatchPolicy">색 일치 조건. 해당하지 않는 정의는 null.</param>
        /// <param name="damageAggregationPolicy">피해 집계 조회 조건. 해당하지 않는 정의는 null.</param>
        public ElementDefinition(ElementId id, string displayName, ElementPlacementProfile placement, ElementChargePlacementProfile chargePlacement, ElementDamageSourcePolicy damageSourcePolicy, ElementColorMatchPolicy colorMatchPolicy, ElementDamageAggregationPolicy damageAggregationPolicy)
            : this(id, displayName, placement, chargePlacement, damageSourcePolicy, colorMatchPolicy, damageAggregationPolicy, null) { }

        /// <param name="removalMissionProfile">제거 미션 값. 해당하지 않는 정의는 null.</param>
        public ElementDefinition(ElementId id, string displayName, ElementPlacementProfile placement, ElementChargePlacementProfile chargePlacement, ElementDamageSourcePolicy damageSourcePolicy, ElementColorMatchPolicy colorMatchPolicy, ElementDamageAggregationPolicy damageAggregationPolicy, ElementRemovalMissionProfile removalMissionProfile)
            : this(id, displayName, placement, chargePlacement, damageSourcePolicy, colorMatchPolicy, damageAggregationPolicy, removalMissionProfile, null) { }

        /// <param name="reactionBehavior">명시적 반응 행동. 메타데이터 전용 정의는 null을 허용한다.</param>
        public ElementDefinition(ElementId id, string displayName, ElementPlacementProfile placement, ElementChargePlacementProfile chargePlacement, ElementDamageSourcePolicy damageSourcePolicy, ElementColorMatchPolicy colorMatchPolicy, ElementDamageAggregationPolicy damageAggregationPolicy, ElementRemovalMissionProfile removalMissionProfile, ElementReactionBehavior? reactionBehavior)
            : this(id, displayName, placement, chargePlacement, damageSourcePolicy, colorMatchPolicy, damageAggregationPolicy, removalMissionProfile, reactionBehavior, null) { }

        public ElementDefinition(ElementId id, string displayName, ElementPlacementProfile placement, ElementChargePlacementProfile chargePlacement, ElementDamageSourcePolicy damageSourcePolicy, ElementColorMatchPolicy colorMatchPolicy, ElementDamageAggregationPolicy damageAggregationPolicy, ElementRemovalMissionProfile removalMissionProfile, ElementReactionBehavior? reactionBehavior, ElementLayerProfile layer)
            : this(id, displayName, placement, chargePlacement, damageSourcePolicy, colorMatchPolicy, damageAggregationPolicy, removalMissionProfile, reactionBehavior, layer, null) { }

        public ElementDefinition(ElementId id, string displayName, ElementPlacementProfile placement, ElementChargePlacementProfile chargePlacement, ElementDamageSourcePolicy damageSourcePolicy, ElementColorMatchPolicy colorMatchPolicy, ElementDamageAggregationPolicy damageAggregationPolicy, ElementRemovalMissionProfile removalMissionProfile, ElementReactionBehavior? reactionBehavior, ElementLayerProfile layer, ElementTurnProfile turn)
            : this(id, displayName, placement, chargePlacement, damageSourcePolicy, colorMatchPolicy, damageAggregationPolicy, removalMissionProfile, reactionBehavior, layer, turn, null) { }

        public ElementDefinition(ElementId id, string displayName, ElementPlacementProfile placement, ElementChargePlacementProfile chargePlacement, ElementDamageSourcePolicy damageSourcePolicy, ElementColorMatchPolicy colorMatchPolicy, ElementDamageAggregationPolicy damageAggregationPolicy, ElementRemovalMissionProfile removalMissionProfile, ElementReactionBehavior? reactionBehavior, ElementLayerProfile layer, ElementTurnProfile turn, ElementSupplyProfile supply)
        {
            if (!id.IsValid) throw new ArgumentException("정의 ID가 초기화되지 않았습니다.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException($"요소 '{id.Value}'의 표시명은 빈 값일 수 없습니다.", nameof(displayName));
            Id = id;
            DisplayName = displayName;
            Placement = placement;
            ChargePlacement = chargePlacement;
            DamageSourcePolicy = damageSourcePolicy;
            ColorMatchPolicy = colorMatchPolicy;
            DamageAggregationPolicy = damageAggregationPolicy;
            RemovalMissionProfile = removalMissionProfile;
            ReactionBehavior = reactionBehavior;
            Layer = layer;
            Turn = turn;
            Supply = supply;
        }

        public static ElementDefinition CreateSupply(ElementId id, string displayName, ElementSupplyProfile supply)
        {
            if (supply == null) throw new ArgumentNullException(nameof(supply));
            return new ElementDefinition(id, displayName, null, null, null, null, null, null, null, null, null, supply);
        }

        /// <returns>배치 수치. 누락은 정의 ID를 포함한 오류로 거절한다.</returns>
        public ElementPlacementProfile RequirePlacement() => Placement ??
            throw new InvalidOperationException($"요소 '{Id.Value}'의 배치 프로필이 없습니다.");

        /// <returns>충전형 배치 수치. 누락은 정의 ID를 포함한 오류로 거절한다.</returns>
        public ElementChargePlacementProfile RequireChargePlacement() => ChargePlacement ??
            throw new InvalidOperationException($"요소 '{Id.Value}'의 충전 배치 프로필이 없습니다.");

        /// <returns>피해 원인 정책. 누락은 정의 ID를 포함한 오류로 거절한다.</returns>
        public ElementDamageSourcePolicy RequireDamageSourcePolicy() => DamageSourcePolicy ??
            throw new InvalidOperationException($"요소 '{Id.Value}'의 피해 원인 정책이 없습니다.");

        /// <returns>색 일치 정책. 누락은 정의 ID를 포함한 오류로 거절한다.</returns>
        public ElementColorMatchPolicy RequireColorMatchPolicy() => ColorMatchPolicy ??
            throw new InvalidOperationException($"요소 '{Id.Value}'의 색 일치 정책이 없습니다.");

        /// <returns>피해 집계 정책. 누락은 정의 ID를 포함한 오류로 거절한다.</returns>
        public ElementDamageAggregationPolicy RequireDamageAggregationPolicy() => DamageAggregationPolicy ??
            throw new InvalidOperationException($"요소 '{Id.Value}'의 피해 집계 정책이 없습니다.");

        /// <returns>제거 미션 값. 누락은 정의 ID를 포함한 오류로 거절한다.</returns>
        public ElementRemovalMissionProfile RequireRemovalMissionProfile() => RemovalMissionProfile ??
            throw new InvalidOperationException($"요소 '{Id.Value}'의 제거 미션 프로필이 없습니다.");

        /// <returns>명시적 행동 키. 종류나 다른 프로필로 행동을 추론하지 않는다.</returns>
        public ElementReactionBehavior RequireReactionBehavior() => ReactionBehavior ??
            throw new InvalidOperationException($"요소 '{Id.Value}'의 반응 행동 키가 없습니다.");

        public ElementLayerProfile RequireLayer() => Layer ??
            throw new InvalidOperationException($"요소 '{Id.Value}'의 층 행동 프로필이 없습니다.");
        public ElementTurnProfile RequireTurn() => Turn ??
            throw new InvalidOperationException($"요소 '{Id.Value}'의 턴 종료 프로필이 없습니다.");
        public ElementSupplyProfile RequireSupply() => Supply ??
            throw new InvalidOperationException($"요소 '{Id.Value}'의 공급 생성 프로필이 없습니다.");
    }
}
