#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace LevelAuthoring.Documents
{
    public static class ContentSnapshotFingerprint
    {
        public static string Compute(ContentSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var documents = snapshot.Documents;
            // 저장 세대의 경로는 제작 내용이 아니다. 조회 사본만 정규화한다.
            foreach (var document in documents)
                if (document.Kind == "project") document.Data["documents"] = new Newtonsoft.Json.Linq.JArray();
            byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\n", documents.OrderBy(document => document.Id, StringComparer.Ordinal).Select(ContentJson.Write)));
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
#endif
