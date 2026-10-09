#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System.Linq;
using LevelAuthoring.Documents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Storage
{
    public sealed class AuthoringTrialSource
    {
        public ContentSnapshot Snapshot { get; }
        public string LevelId { get; }
        private AuthoringTrialSource(ContentSnapshot snapshot, string levelId)
        {
            if (snapshot.Get(levelId).Kind != "level") throw new ContentFormatException("시험 원본은 레벨이어야 합니다.");
            Snapshot = snapshot; LevelId = levelId;
        }
        public static string Encode(ContentSnapshot snapshot, string levelId)
        {
            var source = new AuthoringTrialSource(snapshot, levelId);
            return new JObject { ["version"] = 1, ["levelId"] = source.LevelId,
                ["documents"] = new JArray(snapshot.Documents.OrderBy(doc => doc.Id, System.StringComparer.Ordinal)
                    .Select(doc => JObject.Parse(ContentJson.Write(doc)))) }.ToString(Formatting.None);
        }
        public static AuthoringTrialSource Decode(string text)
        {
            try
            {
                JObject value = JObject.Parse(text, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if ((int?)value["version"] != 1 || value.Properties().Count() != 3 || !(value["documents"] is JArray documents))
                    throw new ContentFormatException("지원하지 않는 시험 원본 형식입니다.");
                return new AuthoringTrialSource(new ContentSnapshot(documents.Select(doc => ContentJson.Read(doc.ToString(Formatting.None)))), (string)value["levelId"]);
            }
            catch (JsonException error) { throw new ContentFormatException("시험 원본이 손상됐습니다.", error); }
        }

        public static string EncodeWithMoves(ContentSnapshot snapshot, string levelId, int moves)
        {
            if (moves < 1 || moves > 100) throw new ContentFormatException("추천 시험 이동 횟수는 1~100입니다.");
            var documents = snapshot.Documents;
            var level = documents.Single(doc => doc.Id == levelId && doc.Kind == "level");
            level.Data["moveCount"] = moves;
            return Encode(new ContentSnapshot(documents), levelId);
        }
    }
}
#endif
