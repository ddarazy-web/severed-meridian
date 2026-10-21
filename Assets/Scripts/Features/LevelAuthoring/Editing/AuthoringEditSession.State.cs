#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
            current = Capture(), saved = saved, savedLevelId = savedLevelId, Revision = Revision, DirectoryBaseline = DirectoryBaseline
        };

        public string ExportState()
        {
            using (StringWriter text = new StringWriter(CultureInfo.InvariantCulture))
            { WriteState(text); return text.ToString(); }
        }

        public void WriteState(TextWriter output)
        {
            using var writer = new JsonTextWriter(output) { Formatting = Formatting.None, CloseOutput = false };
            WriteState(writer);
        }

        // v1 필드와 이력 순서를 유지하며 전체 이력을 중간 JObject로 복제하지 않는다.
        internal void WriteState(JsonWriter writer)
        {
            if (DirectoryBaseline != null) { WriteReferencedState(writer); return; }
            WriteLegacyState(writer);
        }

        internal void WriteLegacyState(JsonWriter writer)
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
            using var input = new StringReader(text);
            return Restore(input);
        }

        // 기존 JObject 호출자는 같은 순차 복원 경로를 사용한다.
        internal static AuthoringEditSession RestoreObject(JObject root)
        {
            using var reader = root.CreateReader();
            return ReadState(reader);
        }
        private static void WriteHistoryState(JsonWriter writer, State state)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("documents"); WriteDocuments(writer, state.Documents);
            writer.WritePropertyName("levelId"); writer.WriteValue(state.LevelId);
            writer.WritePropertyName("beforeSave"); writer.WriteValue(state.BeforeSave);
            writer.WritePropertyName("cells"); writer.WriteStartArray();
            foreach (int cell in state.Cells) writer.WriteValue(cell);
            writer.WriteEndArray(); writer.WriteEndObject();
        }

        private static void WriteDocuments(JsonWriter writer, Dictionary<string, ContentDocument> documents)
        {
            writer.WriteStartArray();
            foreach (ContentDocument document in documents.Values)
            {
                // v1은 본문을 포함하므로, 아직 열지 않은 파일의 실제 종류를 먼저 확인한다.
                document.EnsureLoaded();
                writer.WriteStartObject();
                writer.WritePropertyName("id"); writer.WriteValue(document.Id);
                writer.WritePropertyName("kind"); writer.WriteValue(document.Kind);
                writer.WritePropertyName("data"); document.WriteData(writer);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

    }
}
#endif
