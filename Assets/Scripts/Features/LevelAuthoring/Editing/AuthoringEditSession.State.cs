#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Documents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Editing
{
    public sealed partial class AuthoringEditSession
    {
        private AuthoringEditSession() { }

        // 공유 원본 사본의 Undo는 부모 이력을 이어받지 않는다. 유효한 저장 기준은 복구 검증을 위해 유지한다.
        public AuthoringEditSession Fork() => new AuthoringEditSession
        {
            current = Capture(), saved = Copy(saved), savedLevelId = savedLevelId, Revision = Revision
        };

        // Editor 재로드 보관용이다. 제작 문서 내보내기와 달리 미완성 값과 이력도 포함한다.
        public string ExportState() => new JObject
        {
            ["version"] = 1, ["revision"] = Revision, ["current"] = Encode(current),
            ["saved"] = EncodeDocuments(saved), ["savedLevelId"] = savedLevelId,
            ["undo"] = new JArray(undo.Select(Encode)), ["redo"] = new JArray(redo.Select(Encode))
        }.ToString(Formatting.None);

        public static AuthoringEditSession Restore(string text)
        {
            try
            {
                JObject root = JObject.Parse(text);
                if ((int?)root["version"] != 1) throw new ContentFormatException("지원하지 않는 편집 복구 버전입니다.");
                var session = new AuthoringEditSession
                {
                    current = Decode(root["current"]), saved = DecodeDocuments(root["saved"]),
                    Revision = (string)root["revision"]
                };
                if (string.IsNullOrEmpty(session.Revision)) throw new ContentFormatException("편집 복구본의 저장 revision이 없습니다.");
                // 저장 기준은 정상 문서여야 하지만 작성 중 문서에는 오류가 있을 수 있다.
                new ContentSnapshot(session.saved.Values);
                session.savedLevelId = (string)root["savedLevelId"] ?? session.saved.Values.First(doc => doc.Kind == "level").Id;
                if (!session.saved.TryGetValue(session.savedLevelId, out ContentDocument selectedSaved) || selectedSaved.Kind != "level") throw new ContentFormatException("복구본의 저장 레벨이 없습니다.");
                foreach (JToken state in ((JArray)root["undo"]).Reverse()) session.undo.Push(Decode(state));
                foreach (JToken state in ((JArray)root["redo"]).Reverse()) session.redo.Push(Decode(state));
                return session;
            }
            catch (Exception error) when (error is JsonException || error is ArgumentException || error is InvalidCastException || error is NullReferenceException || error is FormatException)
            { throw new ContentFormatException("편집 복구본이 손상됐습니다.", error); }
        }
        private static JObject Encode(State state) => new JObject
        {
            ["documents"] = EncodeDocuments(state.Documents), ["levelId"] = state.LevelId,
            ["cells"] = new JArray(state.Cells)
        };
        private static JArray EncodeDocuments(Dictionary<string, ContentDocument> documents) => new JArray(documents.Values.Select(doc =>
            new JObject { ["id"] = doc.Id, ["kind"] = doc.Kind, ["data"] = doc.Data.DeepClone() }));
        private static Dictionary<string, ContentDocument> DecodeDocuments(JToken token)
        {
            if (!(token is JArray array)) throw new ContentFormatException("복구 문서 목록이 없습니다.");
            return array.Select(item => new ContentDocument((string)item["kind"], (string)item["id"], (JObject)item["data"]))
                .ToDictionary(doc => doc.Id, StringComparer.Ordinal);
        }
        private static State Decode(JToken token)
        {
            var state = new State { Documents = DecodeDocuments(token["documents"]), LevelId = (string)token["levelId"], Cells = ((JArray)token["cells"]).Values<int>().ToArray() };
            if (state.LevelId == null || !state.Documents.TryGetValue(state.LevelId, out ContentDocument level) || level.Kind != "level" ||
                state.Cells.Any(cell => cell < 0 || cell >= 81) || state.Cells.Distinct().Count() != state.Cells.Length)
                throw new ContentFormatException("복구본의 레벨 또는 선택 칸이 잘못됐습니다.");
            return state;
        }
    }
}
#endif
