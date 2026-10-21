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
        public ContentSnapshot(IEnumerable<ContentDocument> source) : this(source, false) { }
        private ContentSnapshot(IEnumerable<ContentDocument> source, bool owned, bool documentsValidated = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copies = owned ? source.ToArray() : source.Select(Copy).ToArray();
            ContentDocumentValidator.ValidateSnapshot(copies, documentsValidated);
            documents = copies.ToDictionary(document => document.Id, StringComparer.Ordinal);
            projectId = copies.Single(document => document.Kind == "project").Id;
        }
        // 세션이 게시한 불변 버전만 받는다. 공개 생성자는 외부 입력을 계속 복사한다.
        internal static ContentSnapshot FromOwnedDocuments(IEnumerable<ContentDocument> source) =>
            new ContentSnapshot(source, true);
        // 저장소가 각 파일의 스키마 검증을 마친 직후에만 호출한다. 전체 참조 검사는 그대로 수행한다.
        internal static ContentSnapshot FromValidatedDocuments(IEnumerable<ContentDocument> source) =>
            new ContentSnapshot(source, true, true);
        public ContentDocument Project => Get(projectId);
        public ContentDocument[] Documents => documents.Values.Select(Copy).ToArray();
        public string[] DocumentIds => documents.Keys.ToArray();
        public ContentDocumentInfo GetDocumentInfo(string id) => documents[id].Info;
        // 편집 세션만 사용하는 소유권 경계다. 사전은 따로 소유하고 본문은 게시 후 수정하지 않는다.
        internal Dictionary<string, ContentDocument> ShareOwnedDocuments() =>
            new Dictionary<string, ContentDocument>(documents, StringComparer.Ordinal);
        public ContentDocument Get(string id)
        {
            if (id == null || !documents.TryGetValue(id, out var document)) throw new ContentFormatException("없는 문서 ID: " + id);
            return Copy(document);
        }
        private static ContentDocument Copy(ContentDocument value)
        {
            if (value == null) throw new ContentFormatException("빈 문서가 있습니다.");
            return value.Copy();
        }
    }
}
#endif
