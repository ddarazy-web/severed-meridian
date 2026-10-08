using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LevelAuthoring.Documents;

namespace LevelAuthoring.Storage
{
    internal enum SavePhase { BeforeWrite, AfterFlush, BeforeValidation, BeforeReplace }

    public sealed class ContentFile
    {
        public ContentDocument Document { get; }
        public string Hash { get; }
        internal ContentFile(ContentDocument document, string hash) { Document = document; Hash = hash; }
    }

    public sealed class JsonContentStore
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly string root;
        private readonly Action<SavePhase> checkpoint;
        public JsonContentStore(string root) : this(root, null) { }
        internal JsonContentStore(string root, Action<SavePhase> checkpoint)
        {
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root)) throw new ContentFormatException("작업 루트는 절대경로여야 합니다.");
            this.root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            this.checkpoint = checkpoint;
            CheckLinks(this.root);
        }

        public ContentFile Read(string relativePath) => ReadFile(Resolve(relativePath));
        public ContentFile ReadBackup(string relativePath) => ReadFile(Resolve(relativePath + ".previous"));

        public ContentFile Save(ContentDocument document, string relativePath, string expectedHash)
        {
            byte[] bytes = Utf8.GetBytes(ContentJson.Write(document));
            string path = Resolve(relativePath);
            string backup = Resolve(relativePath + ".previous");
            CheckExpected(path, expectedHash);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".pending-" + Guid.NewGuid().ToString("N");
            try
            {
                checkpoint?.Invoke(SavePhase.BeforeWrite);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                checkpoint?.Invoke(SavePhase.AfterFlush);
                checkpoint?.Invoke(SavePhase.BeforeValidation);
                ContentFile verified = ReadFile(temporary);
                if (verified.Hash != Hash(bytes)) throw new ContentFormatException("임시 문서가 기록 중 변경됐습니다.");
                checkpoint?.Invoke(SavePhase.BeforeReplace);
                // 협조하지 않는 외부 프로세스와의 완전한 트랜잭션은 아니다. 교체 직전 다시 검사한다.
                Resolve(relativePath);
                Resolve(relativePath + ".previous");
                CheckExpected(path, expectedHash);
                if (expectedHash == null) File.Move(temporary, path);
                else File.Replace(temporary, path, backup);
                return verified;
            }
            finally
            {
                // 이번 호출이 만든 임시 파일만 정리한다. 중단된 다른 쓰기의 파일은 건드리지 않는다.
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private void CheckExpected(string path, string expectedHash)
        {
            if (File.Exists(path))
            {
                if (expectedHash == null) throw new ContentFormatException("파일이 이미 있습니다. 다시 읽은 뒤 저장하세요.");
                if (ReadFile(path).Hash != expectedHash) throw new ContentFormatException("외부에서 문서가 변경됐습니다. 덮어쓰지 않았습니다.");
            }
            else if (expectedHash != null) throw new ContentFormatException("문서가 삭제됐습니다. 다시 열어 주세요.");
        }

        private static ContentFile ReadFile(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            try { return new ContentFile(ContentJson.Read(Utf8.GetString(bytes)), Hash(bytes)); }
            catch (DecoderFallbackException error) { throw new ContentFormatException("UTF-8 문서가 아닙니다: " + path, error); }
        }

        public static void ValidateRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.IndexOf(':') >= 0)
                throw new ContentFormatException("문서 경로는 작업 루트 아래 상대경로여야 합니다.");
            foreach (string part in relativePath.Replace('\\', '/').Split('/'))
                if (part.Length == 0 || part == "." || part == ".." || part.TrimEnd(' ', '.') != part || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new ContentFormatException("문서 경로에 잘못된 구간이 있습니다: " + relativePath);
        }

        private string Resolve(string relativePath)
        {
            ValidateRelativePath(relativePath);
            string path = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ContentFormatException("작업 루트를 벗어났습니다.");
            CheckLinks(path);
            return path;
        }

        private static void CheckLinks(string path)
        {
            for (string current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new ContentFormatException("링크를 통과하는 문서 경로는 허용하지 않습니다: " + current);
        }

        private static string Hash(byte[] bytes)
        {
            using (var algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
