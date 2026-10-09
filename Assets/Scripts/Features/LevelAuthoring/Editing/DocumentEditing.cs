#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using LevelAuthoring.Documents;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Editing
{
    public static class DocumentEditing
    {
        public static string DuplicateLevel(AuthoringEditSession session, string sourceId, int number, string name)
        {
            ContentDocument source = session.Get(sourceId);
            if (source.Kind != "level") throw new ContentFormatException("복제할 레벨을 선택하세요.");
            return AddLevel(session, source.Data, number, name);
        }

        public static string AddLevel(AuthoringEditSession session, JObject template, int number, string name)
        {
            if (number < 1 || session.Documents.Any(doc => doc.Kind == "level" && (int)doc.Data["levelNumber"] == number))
                throw new ContentFormatException("레벨 번호가 잘못됐거나 이미 사용 중입니다.");
            if (string.IsNullOrWhiteSpace(name)) throw new ContentFormatException("레벨 이름을 입력하세요.");
            if (template == null) throw new ArgumentNullException(nameof(template));
            string id = "level-" + Guid.NewGuid().ToString("N");
            var data = (JObject)template.DeepClone();
            data["displayName"] = name.Trim(); data["levelNumber"] = number;
            // 배치/단계 ID는 레벨 내부 참조다. 기존 SO 복제처럼 내부 참조와 명시 학습 ID는 보존한다.
            session.Apply("레벨 추가", docs => docs.Add(id, new ContentDocument("level", id, data)));
            session.SelectLevel(id);
            return id;
        }
        public static void SetField(AuthoringEditSession session, string documentId, string path, JToken value)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("속성 경로를 지정하세요.", nameof(path));
            session.Apply("속성 변경: " + path, docs =>
            {
                if (!docs.TryGetValue(documentId, out ContentDocument document)) throw new ContentFormatException("없는 문서입니다.");
                JToken target = document.Data.SelectToken(path, false);
                if (target == null) throw new ContentFormatException("없는 속성: " + path);
                target.Replace(value?.DeepClone() ?? JValue.CreateNull());
            });
        }
    }
}
#endif
