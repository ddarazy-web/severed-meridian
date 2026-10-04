using System;
using System.Collections.Generic;

namespace Elements
{
    /// <summary>생성 시 입력을 독립 색인으로 복사하고 이후에는 정의 조회만 제공한다.</summary>
    public sealed class ElementCatalog
    {
        private readonly Dictionary<ElementId, ElementDefinition> definitions;
        public int Count => definitions.Count;

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
        }

        /// <param name="id">조회할 유효 정의 ID.</param><returns>불변 정의. 미등록 ID는 해당 ID를 포함한 오류로 거절한다.</returns>
        public ElementDefinition Get(ElementId id)
        {
            if (!id.IsValid) throw new ArgumentException("조회할 정의 ID가 초기화되지 않았습니다.", nameof(id));
            if (!definitions.TryGetValue(id, out ElementDefinition definition)) throw new KeyNotFoundException($"등록하지 않은 요소 정의 ID: {id.Value}");
            return definition;
        }
    }
}
