#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Documents
{
    public sealed class ContentFormatException : Exception
    {
        public ContentFormatException(string message) : base(message) { }
        public ContentFormatException(string message, Exception inner) : base(message, inner) { }
    }

    // 제작 문서의 본문은 고정된 v1 스키마로 검사한다. Unity 객체나 실행 팩을 포함하지 않는다.
    public sealed class ContentDocument
    {
        public string Kind { get; }
        public string Id { get; }
        public JObject Data { get; }
        public ContentDocument(string kind, string id, JObject data)
        {
            Kind = kind;
            Id = id;
            Data = data == null ? throw new ArgumentNullException(nameof(data)) : (JObject)data.DeepClone();
        }
    }
}
#endif
