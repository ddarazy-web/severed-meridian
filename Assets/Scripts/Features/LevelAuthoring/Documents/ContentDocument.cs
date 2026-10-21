#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Documents
{
    public sealed class ContentFormatException : Exception
    {
        public ContentFormatException(string message) : base(message) { }
        public ContentFormatException(string message, Exception inner) : base(message, inner) { }
    }

    public sealed class ContentDocumentInfo
    {
        public string Id { get; }
        public string Kind { get; }
        public int LevelNumber { get; }
        public int LevelSchemaVersion { get; }
        public string DisplayName { get; }
        internal ContentDocumentInfo(string id, string kind, int levelNumber, int levelSchemaVersion, string displayName)
        {
            Id = id; Kind = kind; LevelNumber = levelNumber; LevelSchemaVersion = levelSchemaVersion; DisplayName = displayName;
        }
        internal ContentDocumentInfo(string id, string kind, JObject data)
        {
            Id = id; Kind = kind;
            LevelNumber = kind == "level" ? (int?)data["levelNumber"] ?? 0 : 0;
            LevelSchemaVersion = kind == "level" ? (int?)data["schemaVersion"] ?? 0 : 0;
            DisplayName = kind == "level" ? (string)data["displayName"] ?? "" : "";
        }
    }

    // 제작 문서의 본문은 고정된 v1 스키마로 검사한다. Unity 객체나 실행 팩을 포함하지 않는다.
    public sealed class ContentDocument
    {
        private readonly string kind;
        public string Kind => referenceInfo?.Invoke()?.Kind ?? kind;
        public string Id { get; }
        private readonly JObject data;
        private readonly Func<JObject> readBody;
        private readonly Action<JsonWriter> writeBody;
        private readonly ContentDocumentInfo metadata;
        private readonly Func<ContentDocument> readReference;
        private readonly Func<ContentDocumentInfo> referenceInfo;
        private readonly Func<string> referenceHash;
        private readonly Action<ContentDocumentInfo, string> updateReferenceInfo;
        internal Func<JObject> RecoveryReference { get; set; }
        internal void SupplyReferenceInfo(ContentDocumentInfo info, string hash)
        {
            if (Kind == "unknown") updateReferenceInfo?.Invoke(info, hash);
        }
        internal string SourceHash => referenceHash?.Invoke();
        public JObject Data => readReference != null ? readReference().Data : data ?? readBody();
        internal ContentDocumentInfo Info => referenceInfo?.Invoke() ?? metadata ?? new ContentDocumentInfo(Id, Kind, data);
        public ContentDocument(string kind, string id, JObject data) : this(kind, id, data, false) { }
        private ContentDocument(string kind, string id, JObject data, bool owned)
        {
            this.kind = kind;
            Id = id;
            if (data == null) throw new ArgumentNullException(nameof(data));
            this.data = owned ? data : (JObject)data.DeepClone();
        }
        // 파서가 새로 만든 본문에만 사용한다. 외부 입력에는 공개 생성자의 복사 경계를 유지한다.
        internal static ContentDocument FromParsedBody(string kind, string id, JObject data) =>
            new ContentDocument(kind, id, data, true);
        // 내부 불변 문서만 지연 조회한다. 외부 편집 사본은 기존 생성자를 사용한다.
        private ContentDocument(ContentDocumentInfo metadata, Func<JObject> readBody, Action<JsonWriter> writeBody)
        {
            kind = metadata.Kind; Id = metadata.Id; this.metadata = metadata;
            this.readBody = readBody ?? throw new ArgumentNullException(nameof(readBody));
            this.writeBody = writeBody;
        }
        internal static ContentDocument FromOwnedBody(ContentDocumentInfo metadata, Func<JObject> readBody, Action<JsonWriter> writeBody) =>
            new ContentDocument(metadata, readBody, writeBody);
        private ContentDocument(ContentDocumentInfo metadata, Func<ContentDocument> readReference, Func<ContentDocumentInfo> referenceInfo, Func<string> referenceHash, Action<ContentDocumentInfo, string> updateReferenceInfo)
        {
            kind = metadata.Kind; Id = metadata.Id; this.metadata = metadata;
            this.readReference = readReference; this.referenceInfo = referenceInfo; this.referenceHash = referenceHash;
            this.updateReferenceInfo = updateReferenceInfo;
        }
        internal static ContentDocument FromReference(ContentDocumentInfo metadata, Func<ContentDocument> readReference, Func<ContentDocumentInfo> referenceInfo, Func<string> referenceHash, Action<ContentDocumentInfo, string> updateReferenceInfo) =>
            new ContentDocument(metadata, readReference, referenceInfo, referenceHash, updateReferenceInfo);
        internal void EnsureLoaded() { if (readReference != null) _ = readReference(); }
        internal void WriteData(JsonWriter writer)
        {
            if (readReference != null) readReference().WriteData(writer);
            else if (writeBody != null) writeBody(writer);
            else Data.WriteTo(writer);
        }
        // 한 문서 검사 동안만 본문을 유지하고, 전체 문서 사전에는 지연 핸들을 남긴다.
        internal ContentDocument Materialize() => readReference != null ? readReference().Materialize() : readBody == null ? this : FromParsedBody(Kind, Id, readBody());
        internal ContentDocument Copy() => readReference != null ? readReference().Copy() : readBody == null ? new ContentDocument(Kind, Id, data) : Materialize();
    }
}
#endif
