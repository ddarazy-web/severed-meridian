using System;
using System.Collections.Generic;

namespace Elements
{
    /// <summary>생성 시 입력을 독립 색인으로 복사하고 이후에는 정의 조회만 제공한다.</summary>
    public sealed class ElementCatalog
    {
        private readonly Dictionary<ElementId, ElementDefinition> definitions;
        public int Count => definitions.Count;
        public IReadOnlyCollection<ElementDefinition> Definitions => definitions.Values;
        internal bool TryGet(ElementId id, out ElementDefinition definition) => definitions.TryGetValue(id, out definition);

        /// <param name="definitions">불변 정의 목록. 호출자의 컬렉션 자체는 보유하지 않는다.</param>
        public ElementCatalog(IEnumerable<ElementDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            this.definitions = new Dictionary<ElementId, ElementDefinition>();
            foreach (ElementDefinition definition in definitions)
            {
                if (definition == null) throw new ArgumentException("카탈로그에 null 정의가 있습니다.", nameof(definitions));
                if (this.definitions.ContainsKey(definition.Id)) throw new ArgumentException($"중복 요소 정의 ID: {definition.Id.Value}", nameof(definitions));
                this.definitions.Add(definition.Id, definition);
            }
            foreach (ElementDefinition definition in this.definitions.Values)
            {
                ElementSupplyProfile supply = definition.Supply;
                if (supply?.ObstacleDefinitionId.HasValue == true)
                {
                    ElementDefinition body = Reference(supply.ObstacleDefinitionId.Value);
                    if (body.ReactionBehavior != ElementReactionBehavior.Durability || body.RequirePlacement().Size != 1)
                        throw new ArgumentException($"공급 본체 정의 ID '{body.Id.Value}'는 한 칸 내구도 본체여야 합니다.");
                }
                if (supply != null) foreach (ElementId choice in supply.ChoiceDefinitionIds)
                {
                    ElementDefinition target = Reference(choice);
                    if (target.Supply?.Behavior != ElementSupplyBehavior.Power)
                        throw new ArgumentException($"공급 선택 정의 ID '{choice.Value}'는 파워 생성이어야 합니다.");
                }
            }
        }

        private ElementDefinition Reference(ElementId id) => definitions.TryGetValue(id, out ElementDefinition definition)
            ? definition : throw new ArgumentException($"등록하지 않은 공급 참조 정의 ID: {id.Value}");

        /// <param name="id">조회할 유효 정의 ID.</param><returns>불변 정의. 미등록 ID는 해당 ID를 포함한 오류로 거절한다.</returns>
        public ElementDefinition Get(ElementId id)
        {
            if (!id.IsValid) throw new ArgumentException("조회할 정의 ID가 초기화되지 않았습니다.", nameof(id));
            if (!definitions.TryGetValue(id, out ElementDefinition definition)) throw new KeyNotFoundException($"등록하지 않은 요소 정의 ID: {id.Value}");
            return definition;
        }
    }
}
