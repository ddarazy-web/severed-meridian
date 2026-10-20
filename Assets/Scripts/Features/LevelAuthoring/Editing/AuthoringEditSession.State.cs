#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Editing
{
    public sealed partial class AuthoringEditSession
    {
        private AuthoringEditSession() { }

        // 공유 사본은 불변 문서를 공유하지만 선택과 이후 편집·Undo는 별도로 소유한다.
        public AuthoringEditSession Fork() => new AuthoringEditSession
        {
            current = Capture(), saved = saved, savedLevelId = savedLevelId, Revision = Revision
        };

        public string ExportState()
        {
            using (StringWriter text = new StringWriter(CultureInfo.InvariantCulture))
            using (JsonTextWriter writer = new JsonTextWriter(text) { Formatting = Formatting.None })
            { WriteState(writer); return text.ToString(); }
        }

        // v1 필드와 이력 순서를 유지하며 전체 이력을 중간 JObject로 복제하지 않는다.
        internal void WriteState(JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("version"); writer.WriteValue(1);
            writer.WritePropertyName("revision"); writer.WriteValue(Revision);
            writer.WritePropertyName("current"); WriteHistoryState(writer, current);
            writer.WritePropertyName("saved"); WriteDocuments(writer, saved);
            writer.WritePropertyName("savedLevelId"); writer.WriteValue(savedLevelId);
            writer.WritePropertyName("undo"); writer.WriteStartArray();
            foreach (State state in undo) WriteHistoryState(writer, state);
            writer.WriteEndArray();
            writer.WritePropertyName("redo"); writer.WriteStartArray();
            foreach (State state in redo) WriteHistoryState(writer, state);
            writer.WriteEndArray(); writer.WriteEndObject();
        }

        public static AuthoringEditSession Restore(string text)
        {
            try { return RestoreObject(JObject.Parse(text)); }
            catch (JsonException error) { throw new ContentFormatException("편집 복구본이 손상됐습니다.", error); }
        }

        // 상위 복구 기록이 이미 파싱한 노드를 읽기만 한다. 내부 문서는 최초 등록 때 복사한다.
        internal static AuthoringEditSession RestoreObject(JObject root)
        {
            try
            {
                if ((int?)root["version"] != 1) throw new ContentFormatException("지원하지 않는 편집 복구 버전입니다.");
                Dictionary<string, List<ContentDocument>> pool = new Dictionary<string, List<ContentDocument>>(StringComparer.Ordinal);
                var session = new AuthoringEditSession
                {
                    current = Decode(root["current"], pool), saved = DecodeDocuments(root["saved"], pool),
                    Revision = (string)root["revision"]
                };
                if (string.IsNullOrEmpty(session.Revision)) throw new ContentFormatException("편집 복구본의 저장 revision이 없습니다.");
                // 저장 기준은 정상 문서여야 하지만 작성 중 문서에는 오류가 있을 수 있다.
                new ContentSnapshot(session.saved.Values);
                session.savedLevelId = (string)root["savedLevelId"] ?? session.saved.Values.First(doc => doc.Kind == "level").Id;
                if (!session.saved.TryGetValue(session.savedLevelId, out ContentDocument selectedSaved) || selectedSaved.Kind != "level") throw new ContentFormatException("복구본의 저장 레벨이 없습니다.");
                foreach (JToken state in ((JArray)root["undo"]).Reverse()) session.undo.Push(Decode(state, pool));
                foreach (JToken state in ((JArray)root["redo"]).Reverse()) session.redo.Push(Decode(state, pool));
                return session;
            }
            catch (Exception error) when (error is JsonException || error is ArgumentException || error is InvalidCastException || error is NullReferenceException || error is FormatException)
            { throw new ContentFormatException("편집 복구본이 손상됐습니다.", error); }
        }

        private static void WriteHistoryState(JsonWriter writer, State state)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("documents"); WriteDocuments(writer, state.Documents);
            writer.WritePropertyName("levelId"); writer.WriteValue(state.LevelId);
            writer.WritePropertyName("cells"); writer.WriteStartArray();
            foreach (int cell in state.Cells) writer.WriteValue(cell);
            writer.WriteEndArray(); writer.WriteEndObject();
        }

        private static void WriteDocuments(JsonWriter writer, Dictionary<string, ContentDocument> documents)
        {
            writer.WriteStartArray();
            foreach (ContentDocument document in documents.Values)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("id"); writer.WriteValue(document.Id);
                writer.WritePropertyName("kind"); writer.WriteValue(document.Kind);
                writer.WritePropertyName("data"); document.Data.WriteTo(writer);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        private static Dictionary<string, ContentDocument> DecodeDocuments(JToken token, Dictionary<string, List<ContentDocument>> pool)
        {
            if (!(token is JArray array)) throw new ContentFormatException("복구 문서 목록이 없습니다.");
            Dictionary<string, ContentDocument> documents = new Dictionary<string, ContentDocument>(StringComparer.Ordinal);
            foreach (JToken item in array)
            {
                string id = (string)item["id"], kind = (string)item["kind"];
                JObject data = (JObject)item["data"];
                if (!pool.TryGetValue(id, out List<ContentDocument> versions))
                    pool.Add(id, versions = new List<ContentDocument>());
                // ID만 같다고 공유하지 않는다. 같은 복원 안의 종류와 내용 전체가 같아야 한다.
                ContentDocument document = versions.FirstOrDefault(value => value.Kind == kind && JToken.DeepEquals(value.Data, data));
                if (document == null) { document = new ContentDocument(kind, id, data); versions.Add(document); }
                documents.Add(id, document);
            }
            return documents;
        }

        private static State Decode(JToken token, Dictionary<string, List<ContentDocument>> pool)
        {
            var state = new State { Documents = DecodeDocuments(token["documents"], pool), LevelId = (string)token["levelId"], Cells = ((JArray)token["cells"]).Values<int>().ToArray() };
            if (state.LevelId == null || !state.Documents.TryGetValue(state.LevelId, out ContentDocument level) || level.Kind != "level" ||
                state.Cells.Any(cell => cell < 0 || cell >= 81) || state.Cells.Distinct().Count() != state.Cells.Length)
                throw new ContentFormatException("복구본의 레벨 또는 선택 칸이 잘못됐습니다.");
            return state;
        }
    }
}
#endif
