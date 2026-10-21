#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Editing
{
    public sealed partial class AuthoringEditSession
    {
        private sealed class RecoveryVersion
        {
            internal ContentDocument Document;
            internal string Hash;
        }
        public static AuthoringEditSession Restore(TextReader input)
        {
            using var reader = new JsonTextReader(input) { CloseInput = false, DateParseHandling = DateParseHandling.None, MaxDepth = 96 };
            try
            {
                AuthoringEditSession session = ReadState(reader);
                if (reader.Read()) throw new ContentFormatException("복구 문서 뒤에 추가 내용이 있습니다.");
                return session;
            }
            catch (JsonException error) { throw new ContentFormatException("편집 복구본이 손상됐습니다.", error); }
        }

        // v1 형식의 순서를 강제하지 않는다. 레벨 본문 하나만 펼치며 이력은 불변 버전을 공유한다.
        internal static AuthoringEditSession ReadState(JsonReader reader)
        {
            var bodies = new ImmutableContentBodyStore(Path.Combine(Path.GetTempPath(), "MoonRabbitJunkyard", "AuthoringBodies"));
            try
            {
                if (reader.TokenType == JsonToken.None) reader.Read();
                Expect(reader, JsonToken.StartObject);
                var pool = new Dictionary<string, List<RecoveryVersion>>(StringComparer.Ordinal);
                var session = new AuthoringEditSession();
                List<State> previous = null, following = null;
                int? version = null;
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    Expect(reader, JsonToken.PropertyName);
                    string key = (string)reader.Value;
                    if (!reader.Read()) throw new ContentFormatException("복구 문서가 중간에 끝났습니다.");
                    switch (key)
                    {
                        case "version":
                            version = (int?)JToken.ReadFrom(reader);
                            if (version == 2)
                            {
                                if (session.current != null || session.saved != null || previous != null || following != null)
                                    throw new ContentFormatException("복구 v2는 버전 필드부터 기록해야 합니다.");
                                bodies.Dispose();
                                return ReadReferencedState(reader);
                            }
                            break;
                        case "revision": session.Revision = (string)JToken.ReadFrom(reader); break;
                        case "current": session.current = ReadHistoryState(reader, pool, bodies); break;
                        case "saved": session.saved = ReadDocuments(reader, pool, bodies); break;
                        case "savedLevelId": session.savedLevelId = (string)JToken.ReadFrom(reader); break;
                        case "undo": previous = ReadHistory(reader, pool, bodies); break;
                        case "redo": following = ReadHistory(reader, pool, bodies); break;
                        default: reader.Skip(); break;
                    }
                }
                Expect(reader, JsonToken.EndObject);
                if (version != 1) throw new ContentFormatException("지원하지 않는 편집 복구 버전입니다.");
                if (session.current == null || session.saved == null || previous == null || following == null || string.IsNullOrEmpty(session.Revision))
                    throw new ContentFormatException("편집 복구본의 필수 상태 또는 revision이 없습니다.");
                ContentSnapshot.FromOwnedDocuments(session.saved.Values);
                session.savedLevelId ??= session.saved.Values.First(doc => doc.Kind == "level").Id;
                if (!session.saved.TryGetValue(session.savedLevelId, out ContentDocument selected) || selected.Kind != "level")
                    throw new ContentFormatException("복구본의 저장 레벨이 없습니다.");
                foreach (State state in previous.AsEnumerable().Reverse()) session.undo.Push(state);
                foreach (State state in following.AsEnumerable().Reverse()) session.redo.Push(state);
                return session;
            }
            catch (Exception error) when (error is JsonException || error is ArgumentException || error is InvalidCastException ||
                error is NullReferenceException || error is FormatException || error is OverflowException || error is InvalidOperationException)
            {
                bodies.Dispose();
                throw new ContentFormatException("편집 복구본이 손상됐습니다.", error);
            }
            catch { bodies.Dispose(); throw; }
        }

        private static Dictionary<string, ContentDocument> ReadDocuments(JsonReader reader,
            Dictionary<string, List<RecoveryVersion>> pool, ImmutableContentBodyStore bodies)
        {
            Expect(reader, JsonToken.StartArray);
            var documents = new Dictionary<string, ContentDocument>(StringComparer.Ordinal);
            while (reader.Read() && reader.TokenType != JsonToken.EndArray)
            {
                Expect(reader, JsonToken.StartObject);
                JObject item = JObject.Load(reader);
                string id = (string)item["id"], kind = (string)item["kind"];
                JObject data = (JObject)item["data"];
                if (!pool.TryGetValue(id, out List<RecoveryVersion> versions)) pool.Add(id, versions = new List<RecoveryVersion>());
                string hash;
                using (SHA256 algorithm = SHA256.Create())
                    hash = Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(data.ToString(Formatting.None))));
                // 동일하게 기록된 이력은 이전 본문을 펼치지 않는다. 필드 순서만 다른 기존 기록은 의미 비교로 호환한다.
                ContentDocument document = versions.FirstOrDefault(value => value.Document.Kind == kind && value.Hash == hash)?.Document;
                document ??= versions.FirstOrDefault(value => value.Document.Kind == kind && JToken.DeepEquals(value.Document.Data, data))?.Document;
                if (document == null)
                {
                    document = ContentDocument.FromParsedBody(kind, id, data);
                    if (kind == "level") document = bodies.Capture(document);
                }
                if (!versions.Any(value => value.Document.Kind == kind && value.Hash == hash))
                    versions.Add(new RecoveryVersion { Document = document, Hash = hash });
                documents.Add(id, document);
            }
            Expect(reader, JsonToken.EndArray);
            return documents;
        }

        private static State ReadHistoryState(JsonReader reader, Dictionary<string, List<RecoveryVersion>> pool, ImmutableContentBodyStore bodies)
        {
            Expect(reader, JsonToken.StartObject);
            var state = new State();
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                Expect(reader, JsonToken.PropertyName);
                string key = (string)reader.Value;
                if (!reader.Read()) throw new ContentFormatException("복구 이력이 중간에 끝났습니다.");
                switch (key)
                {
                    case "documents": state.Documents = ReadDocuments(reader, pool, bodies); break;
                    case "levelId": state.LevelId = (string)JToken.ReadFrom(reader); break;
                    case "cells": state.Cells = JArray.Load(reader).Values<int>().ToArray(); break;
                    case "beforeSave": state.BeforeSave = (bool?)JToken.ReadFrom(reader) ?? false; break;
                    default: reader.Skip(); break;
                }
            }
            Expect(reader, JsonToken.EndObject);
            if (state.Documents == null || state.Cells == null || state.LevelId == null ||
                !state.Documents.TryGetValue(state.LevelId, out ContentDocument level) || level.Kind != "level" ||
                state.Cells.Any(cell => cell < 0 || cell >= 81) || state.Cells.Distinct().Count() != state.Cells.Length)
                throw new ContentFormatException("복구본의 레벨 또는 선택 칸이 잘못됐습니다.");
            return state;
        }

        private static List<State> ReadHistory(JsonReader reader, Dictionary<string, List<RecoveryVersion>> pool, ImmutableContentBodyStore bodies)
        {
            Expect(reader, JsonToken.StartArray);
            var states = new List<State>();
            while (reader.Read() && reader.TokenType != JsonToken.EndArray) states.Add(ReadHistoryState(reader, pool, bodies));
            Expect(reader, JsonToken.EndArray);
            return states;
        }

        private static void Expect(JsonReader reader, JsonToken token)
        {
            if (reader.TokenType != token) throw new ContentFormatException("복구 형식이 잘못됐습니다: " + reader.Path);
        }
    }
}
#endif
