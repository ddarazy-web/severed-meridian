#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LevelAuthoring.Documents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Storage
{
    // 원본 경로를 다시 읽지 않고, 열린 작업이 소유한 본문만 조회한다.
    // 게시 후에는 문서의 읽기 콜백이 보관소 수명을 유지한다. Dispose는 실패한 준비 작업의 해제에도 사용한다.
    internal sealed class ImmutableContentBodyStore : IDisposable
    {
        private readonly object gate = new object();
        private readonly FileStream stream;
        private readonly UTF8Encoding encoding = new UTF8Encoding(false, true);
        private bool disposed;

        internal ImmutableContentBodyStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
                throw new ArgumentException("본문 보관소는 절대경로여야 합니다.", nameof(directory));
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, "bodies-" + Guid.NewGuid().ToString("N") + ".tmp");
            stream = new FileStream(file, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536,
                FileOptions.RandomAccess | FileOptions.DeleteOnClose);
        }

        internal ContentDocument Capture(ContentDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            byte[] bytes = encoding.GetBytes(document.Data.ToString(Formatting.None));
            byte[] hash;
            using (SHA256 algorithm = SHA256.Create()) hash = algorithm.ComputeHash(bytes);
            long offset;
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(ImmutableContentBodyStore));
                offset = stream.Length;
                stream.Position = offset;
                stream.Write(bytes, 0, bytes.Length);
            }
            // 큰 bytes 배열을 클로저에 보관하지 않는다.
            int length = bytes.Length;
            return ContentDocument.FromOwnedBody(document.Info, () => Read(offset, length, hash),
                writer => writer.WriteRawValue(ReadText(offset, length, hash)));
        }

        private JObject Read(long offset, int length, byte[] expectedHash)
        {
            using (var text = new StringReader(ReadText(offset, length, expectedHash)))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 96 })
                return JObject.Load(reader);
        }

        // 이미 JSON으로 기록한 내부 본문만 재사용한다. 직접 기록할 때도 무결성 검사는 생략하지 않는다.
        private string ReadText(long offset, int length, byte[] expectedHash)
        {
            byte[] bytes = new byte[length];
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(ImmutableContentBodyStore));
                stream.Position = offset;
                int read = 0;
                while (read < length)
                {
                    int count = stream.Read(bytes, read, length - read);
                    if (count == 0) throw new ContentFormatException("작업용 본문 보관소가 손상됐습니다.");
                    read += count;
                }
            }
            using (SHA256 algorithm = SHA256.Create())
                if (!algorithm.ComputeHash(bytes).SequenceEqual(expectedHash))
                    throw new ContentFormatException("작업용 본문의 내용이 변경됐습니다.");
            return encoding.GetString(bytes);
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                stream.Dispose();
            }
        }
    }
}
#endif
