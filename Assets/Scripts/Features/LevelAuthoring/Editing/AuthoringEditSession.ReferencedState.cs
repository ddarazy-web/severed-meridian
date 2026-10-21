#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using LevelAuthoring.Validation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Editing
{
    public sealed partial class AuthoringEditSession
    {
        // 본문은 버전별 한 번만 기록한다. 이력에는 불변 문서의 풀 번호만 남긴다.
        private void WriteReferencedState(JsonWriter writer)
        {
            var pool = new Dictionary<ContentDocument, int>();
            void Add(Dictionary<string, ContentDocument> documents)
            {
                foreach (ContentDocument document in documents.Values)
                    if (!pool.ContainsKey(document)) pool.Add(document, pool.Count);
            }
            Add(current.Documents); Add(saved);
            foreach (State state in undo) Add(state.Documents);
            foreach (State state in redo) Add(state.Documents);
            void WriteIds(Dictionary<string, ContentDocument> documents)
            {
                writer.WriteStartArray();
                foreach (ContentDocument document in documents.Values) writer.WriteValue(pool[document]);
                writer.WriteEndArray();
            }
            void WriteStateIds(State state)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("documents"); WriteIds(state.Documents);
                writer.WritePropertyName("levelId"); writer.WriteValue(state.LevelId);
                writer.WritePropertyName("beforeSave"); writer.WriteValue(state.BeforeSave);
                writer.WritePropertyName("cells"); new JArray(state.Cells).WriteTo(writer);
                writer.WriteEndObject();
            }
            writer.WriteStartObject();
            writer.WritePropertyName("version"); writer.WriteValue(2);
            writer.WritePropertyName("root"); writer.WriteValue(DirectoryBaseline.Root);
            writer.WritePropertyName("backup"); writer.WriteValue(DirectoryBaseline.IsBackup);
            writer.WritePropertyName("revision"); writer.WriteValue(Revision);
            writer.WritePropertyName("versions"); writer.WriteStartArray();
            foreach (ContentDocument document in pool.Keys)
            {
                ContentDocumentInfo info = document.Info;
                writer.WriteStartObject();
                writer.WritePropertyName("id"); writer.WriteValue(document.Id);
                writer.WritePropertyName("kind"); writer.WriteValue(info.Kind);
                if (document.RecoveryReference != null)
                {
                    writer.WritePropertyName("number"); writer.WriteValue(info.LevelNumber);
                    writer.WritePropertyName("schema"); writer.WriteValue(info.LevelSchemaVersion);
                    writer.WritePropertyName("name"); writer.WriteValue(info.DisplayName);
                    writer.WritePropertyName("source"); document.RecoveryReference().WriteTo(writer);
                }
                else { writer.WritePropertyName("data"); document.WriteData(writer); }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WritePropertyName("current"); WriteStateIds(current);
            writer.WritePropertyName("saved"); WriteIds(saved);
            writer.WritePropertyName("savedLevelId"); writer.WriteValue(savedLevelId);
            writer.WritePropertyName("undo"); writer.WriteStartArray();
            foreach (State state in undo) WriteStateIds(state);
            writer.WriteEndArray();
            writer.WritePropertyName("redo"); writer.WriteStartArray();
            foreach (State state in redo) WriteStateIds(state);
            writer.WriteEndArray(); writer.WriteEndObject();
        }

        private static AuthoringEditSession ReadReferencedState(JsonReader reader)
        {
            var bodies = new ImmutableContentBodyStore(Path.Combine(Path.GetTempPath(), "MoonRabbitJunkyard", "AuthoringBodies"));
            ContentProjectDirectory directory = null;
            try
            {
                var fields = new JObject();
                var pool = new List<ContentDocument>();
                var keys = new HashSet<string>(StringComparer.Ordinal);
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    Expect(reader, JsonToken.PropertyName);
                    string key = (string)reader.Value;
                    if (!keys.Add(key) || !reader.Read()) throw new ContentFormatException("복구 v2 필드가 중복되거나 중간에 끝났습니다.");
                    if (key != "versions") { fields[key] = JToken.ReadFrom(reader); continue; }
                    string root = (string)fields["root"];
                    if (string.IsNullOrEmpty(root)) throw new ContentFormatException("복구 원본 폴더가 없습니다.");
                    var repository = new ContentSnapshotStore(root);
                    directory = (bool?)fields["backup"] == true ? repository.ReadBackupDirectory() : repository.ReadDirectory();
                    var sourceStore = new JsonContentStore(root);
                    if (directory.ManifestHash != (string)fields["revision"])
                        throw new ContentFormatException("복구 이후 프로젝트 목록이 변경됐습니다. 현재 작업과 복구 기록을 유지합니다.");
                    Expect(reader, JsonToken.StartArray);
                    while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                    {
                        JObject item = JObject.Load(reader);
                        string id = (string)item["id"], kind = (string)item["kind"];
                        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(kind)) throw new ContentFormatException("복구 문서 ID 또는 종류가 없습니다.");
                        ContentDocument document;
                        if (item["source"] is JObject reference)
                        {
                            if (item["data"] != null || kind == "project") throw new ContentFormatException("복구 문서 참조가 잘못됐습니다.");
                            ProjectDocumentEntry entry = directory.FindEntry(id);
                            if (entry != null && entry.RelativePath == (string)reference["path"] && entry.ContentHash != null &&
                                reference["hash"]?.Type == JTokenType.String && entry.ContentHash != (string)reference["hash"])
                                throw new ContentFormatException("복구 원본의 목록 해시가 변경됐습니다: " + entry.RelativePath);
                            var info = new ContentDocumentInfo(id, kind, (int?)item["number"] ?? 0, (int?)item["schema"] ?? 0, (string)item["name"] ?? "");
                            document = RecoveryDocumentReference.Restore(reference, info, sourceStore, bodies);
                        }
                        else document = bodies.Capture(ContentDocument.FromParsedBody(kind, id, (JObject)item["data"]));
                        pool.Add(document);
                    }
                    Expect(reader, JsonToken.EndArray);
                }
                Expect(reader, JsonToken.EndObject);
                if (directory == null) throw new ContentFormatException("복구 문서 버전 목록이 없습니다.");
                Dictionary<string, ContentDocument> Documents(JToken token)
                {
                    if (!(token is JArray ids)) throw new ContentFormatException("복구 이력의 문서 목록이 없습니다.");
                    var result = new Dictionary<string, ContentDocument>(StringComparer.Ordinal);
                    foreach (JToken value in ids)
                    {
                        int index = (int)value;
                        if (index < 0 || index >= pool.Count) throw new ContentFormatException("복구 문서 번호가 잘못됐습니다.");
                        ContentDocument document = pool[index]; result.Add(document.Id, document);
                    }
                    return result;
                }
                State History(JToken token)
                {
                    if (!(token is JObject item)) throw new ContentFormatException("복구 이력이 없습니다.");
                    var state = new State { Documents = Documents(item["documents"]), LevelId = (string)item["levelId"],
                        Cells = ((JArray)item["cells"]).Values<int>().ToArray(), BeforeSave = (bool?)item["beforeSave"] ?? false };
                    if (state.LevelId == null || !state.Documents.TryGetValue(state.LevelId, out ContentDocument selected) ||
                        selected.Kind != "level" || state.Cells.Any(cell => cell < 0 || cell >= 81) || state.Cells.Distinct().Count() != state.Cells.Length)
                        throw new ContentFormatException("복구 레벨 또는 선택 칸이 잘못됐습니다.");
                    return state;
                }
                var session = new AuthoringEditSession { DirectoryBaseline = directory, Revision = (string)fields["revision"],
                    current = History(fields["current"]), saved = Documents(fields["saved"]), savedLevelId = (string)fields["savedLevelId"] };
                foreach (ContentDocument document in session.saved.Values.Where(document => document.RecoveryReference == null))
                    ContentDocumentValidator.Validate(document);
                if (session.savedLevelId == null || !session.saved.TryGetValue(session.savedLevelId, out ContentDocument savedLevel) || savedLevel.Kind != "level")
                    throw new ContentFormatException("복구 저장 레벨이 없습니다.");
                session.current.Documents[session.current.LevelId].EnsureLoaded();
                foreach (JToken item in ((JArray)fields["undo"]).Reverse()) session.undo.Push(History(item));
                foreach (JToken item in ((JArray)fields["redo"]).Reverse()) session.redo.Push(History(item));
                return session;
            }
            catch { bodies.Dispose(); directory?.Dispose(); throw; }
        }
    }
}
#endif
