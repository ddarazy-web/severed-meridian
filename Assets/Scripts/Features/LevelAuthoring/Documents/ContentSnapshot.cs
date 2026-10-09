#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Validation;

namespace LevelAuthoring.Documents
{
    // 검증한 문서는 외부 수정과 분리한다. 모든 조회는 편집 가능한 사본을 반환한다.
    public sealed class ContentSnapshot
    {
        private readonly Dictionary<string, ContentDocument> documents;
        private readonly string projectId;
        public ContentSnapshot(IEnumerable<ContentDocument> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copies = source.Select(Copy).ToArray();
            ContentDocumentValidator.ValidateSnapshot(copies);
            documents = copies.ToDictionary(document => document.Id, StringComparer.Ordinal);
            projectId = copies.Single(document => document.Kind == "project").Id;
        }
        public ContentDocument Project => Get(projectId);
        public ContentDocument[] Documents => documents.Values.Select(Copy).ToArray();
        public ContentDocument Get(string id)
        {
            if (id == null || !documents.TryGetValue(id, out var document)) throw new ContentFormatException("없는 문서 ID: " + id);
            return Copy(document);
        }
        private static ContentDocument Copy(ContentDocument value)
        {
            if (value == null) throw new ContentFormatException("빈 문서가 있습니다.");
            return new ContentDocument(value.Kind, value.Id, value.Data);
        }
    }
}
#endif
