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
            using var hash = SHA256.Create();
            byte[] separator = { 10 };
            bool first = true;
            foreach (string id in snapshot.DocumentIds.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!first) hash.TransformBlock(separator, 0, separator.Length, separator, 0);
                first = false;
                ContentDocument document = snapshot.Get(id);
                // 기존 정규화와 문서 사이 개행을 그대로 유지하며 한 문서씩 해시한다.
                if (document.Kind == "project") document.Data["documents"] = new Newtonsoft.Json.Linq.JArray();
                byte[] bytes = Encoding.UTF8.GetBytes(ContentJson.Write(document));
                hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
            }
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return BitConverter.ToString(hash.Hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
#endif
