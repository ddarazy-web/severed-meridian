#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using LevelAuthoring.Documents;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Storage
{
    // 플랫폼 선택기는 스트림만 전달하며, 검증된 공개 JSON만 새 내부 프로젝트로 등록한다.
    public static class ProjectPackageStore
    {
        private const long MaxArchiveBytes = 128L * 1024 * 1024;
        private const long MaxExpandedBytes = 512L * 1024 * 1024;
        private const long MaxJsonBytes = 16L * 1024 * 1024;
        private const int MaxEntries = 10000;

        // savedProjectFolder의 정상 저장본을 destination에 쓴다. 스트림 닫기·외부 전송 완료는 호출자 책임이다.
        public static void Export(string savedProjectFolder, Stream destination, CancellationToken cancellationToken, string expectedHash = null)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (!destination.CanWrite) throw new ArgumentException("쓸 수 없는 스트림입니다.", nameof(destination));
            cancellationToken.ThrowIfCancellationRequested();
            new JsonContentStore(savedProjectFolder);
            string temporary = Path.Combine(Path.GetTempPath(), "level-project-package-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try
            {
                long total = 0;
                byte[] buffer = new byte[64 * 1024];
                CopyDocument(savedProjectFolder, temporary, "project.json", buffer, ref total, cancellationToken);
                string[] paths = PublicPaths(temporary);
                foreach (string path in paths.Where(path => path != "project.json"))
                    CopyDocument(savedProjectFolder, temporary, path, buffer, ref total, cancellationToken);
                StoredContentSnapshot staged = new ContentSnapshotStore(temporary).Read();
                if (expectedHash != null && staged.Hash != expectedHash)
                    throw new ContentFormatException("ZIP 저장 위치를 선택하는 동안 제작 데이터가 변경됐습니다. 다시 저장한 뒤 내보내세요.");
                cancellationToken.ThrowIfCancellationRequested();
                using (var limited = new PackageWriteStream(destination, cancellationToken))
                using (var archive = new ZipArchive(limited, ZipArchiveMode.Create, true))
                {
                    foreach (string path in paths)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        using (Stream input = File.OpenRead(Path.Combine(temporary, path)))
                        using (Stream output = archive.CreateEntry(path, CompressionLevel.Optimal).Open())
                        {
                            int read;
                            while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                output.Write(buffer, 0, read);
                            }
                        }
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (expectedHash != null && new ContentSnapshotStore(savedProjectFolder).Read().Hash != expectedHash)
                    throw new ContentFormatException("ZIP 내보내기 중 제작 데이터가 변경됐습니다. 다시 저장한 뒤 내보내세요.");
            }
            finally { Directory.Delete(temporary, true); }
        }

        // source를 검증한 뒤 internalProjectsRoot/projectName에 새로 등록하고 폴더 경로를 반환한다. source는 닫지 않는다.
        public static string Import(Stream source, string internalProjectsRoot, string projectName, CancellationToken cancellationToken)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.CanRead) throw new ArgumentException("읽을 수 없는 스트림입니다.", nameof(source));
            cancellationToken.ThrowIfCancellationRequested();
            new JsonContentStore(internalProjectsRoot);
            ValidateName(projectName);
            string root = Path.GetFullPath(internalProjectsRoot);
            string target = Path.Combine(root, projectName);
            if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root)
                .Any(path => string.Equals(Path.GetFileName(path), projectName, StringComparison.OrdinalIgnoreCase)))
                throw new ContentFormatException("같은 이름의 프로젝트나 파일이 이미 있습니다.");
            Directory.CreateDirectory(root);
            string temporary = Path.Combine(root, ".package-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try
            {
                string zipPath = Path.Combine(temporary, "input.zip");
                byte[] buffer = new byte[64 * 1024];
                // seek나 Length가 없는 SAF 스트림도 실제 읽은 바이트로 제한한다.
                using (Stream output = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    long inputBytes = 0;
                    int read;
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        read = source.Read(buffer, 0, buffer.Length);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (read == 0) break;
                        inputBytes += read;
                        if (inputBytes > MaxArchiveBytes) throw new ContentFormatException("ZIP 입력은 128 MiB를 넘을 수 없습니다.");
                        output.Write(buffer, 0, read);
                    }
                }
                string project = Path.Combine(temporary, "project");
                using (Stream input = File.OpenRead(zipPath))
                using (var archive = new ZipArchive(input, ZipArchiveMode.Read))
                {
                    if (archive.Entries.Count == 0 || archive.Entries.Count > MaxEntries)
                        throw new ContentFormatException("ZIP 엔트리는 1개 이상 10,000개 이하여야 합니다.");
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    long declared = 0;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ValidatePath(entry.FullName);
                        int fileType = (entry.ExternalAttributes >> 16) & 0xF000;
                        if ((fileType != 0 && fileType != 0x8000) || (entry.ExternalAttributes & 0x410) != 0)
                            throw new ContentFormatException("ZIP 링크나 일반 파일이 아닌 엔트리는 허용하지 않습니다.");
                        if (!names.Add(entry.FullName)) throw new ContentFormatException("ZIP 경로가 중복되거나 대소문자가 충돌합니다.");
                        declared += entry.Length;
                        if (entry.Length > MaxJsonBytes || declared > MaxExpandedBytes)
                            throw new ContentFormatException("ZIP JSON은 개별 16 MiB, 전체 512 MiB를 넘을 수 없습니다.");
                    }
                    if (!archive.Entries.Any(entry => entry.FullName == "project.json"))
                        throw new ContentFormatException("ZIP 루트에 project.json이 필요합니다.");
                    Directory.CreateDirectory(project);
                    long total = 0;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string path = Resolve(project, entry.FullName);
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        using (Stream member = entry.Open())
                        using (Stream output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                            CopyJson(member, output, buffer, ref total, cancellationToken);
                    }
                    string[] expected = PublicPaths(project);
                    if (expected.Length != names.Count || !expected.All(path => archive.GetEntry(path) != null))
                        throw new ContentFormatException("ZIP은 공개 목록의 JSON만 포함해야 합니다. 누락 또는 추가 파일이 있습니다.");
                }
                new ContentSnapshotStore(project).Read();
                cancellationToken.ThrowIfCancellationRequested();
                CheckLinks(root);
                // 검증된 프로젝트만 같은 파일시스템 안에서 한 번에 공개한다. 기존 대상은 덮어쓰지 않는다.
                Directory.Move(project, target);
                return target;
            }
            catch (InvalidDataException error)
            {
                throw new ContentFormatException("ZIP 형식이 손상됐거나 지원되지 않습니다.", error);
            }
            finally { Directory.Delete(temporary, true); }
        }

        private static string[] PublicPaths(string root)
        {
            ContentDocument project = new JsonContentStore(root).Read("project.json").Document;
            if (project.Kind != "project") throw new ContentFormatException("project.json 종류가 project가 아닙니다.");
            var paths = new List<string> { "project.json" };
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "project.json" };
            foreach (JToken entry in (JArray)project.Data["documents"])
            {
                string path = (string)entry["path"];
                ValidatePath(path);
                if (!unique.Add(path)) throw new ContentFormatException("공개 문서 경로가 중복되거나 대소문자가 충돌합니다.");
                paths.Add(path);
            }
            if (paths.Count > MaxEntries) throw new ContentFormatException("공개 JSON은 10,000개를 넘을 수 없습니다.");
            return paths.ToArray();
        }

        private static void CopyDocument(string source, string destination, string relativePath, byte[] buffer, ref long total, CancellationToken token)
        {
            string sourcePath = Resolve(source, relativePath);
            CheckLinks(sourcePath);
            string target = Resolve(destination, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            using (Stream input = File.OpenRead(sourcePath))
            using (Stream output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                CopyJson(input, output, buffer, ref total, token);
        }

        private static void CopyJson(Stream input, Stream output, byte[] buffer, ref long total, CancellationToken token)
        {
            long member = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int read = input.Read(buffer, 0, buffer.Length);
                token.ThrowIfCancellationRequested();
                if (read == 0) break;
                member += read; total += read;
                if (member > MaxJsonBytes || total > MaxExpandedBytes)
                    throw new ContentFormatException("실제 JSON은 개별 16 MiB, 전체 512 MiB를 넘을 수 없습니다.");
                output.Write(buffer, 0, read);
            }
        }

        private static string Resolve(string root, string relativePath)
        {
            ValidatePath(relativePath);
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string path = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ContentFormatException("ZIP 문서 경로가 임시 루트를 벗어났습니다.");
            return path;
        }

        private static void ValidatePath(string path)
        {
            JsonContentStore.ValidateRelativePath(path);
            if (path.IndexOf('\\') >= 0 || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw new ContentFormatException("ZIP에는 / 상대 경로의 JSON 파일만 허용합니다.");
            foreach (string part in path.Split('/')) ValidateName(part);
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name == "." || name == ".." ||
                name.EndsWith(".", StringComparison.Ordinal) || name.IndexOfAny("/\\:*?\"<>|".ToCharArray()) >= 0 ||
                name.Any(char.IsControl) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ContentFormatException("프로젝트 또는 문서 이름에 사용할 수 없는 문자가 있습니다.");
            string stem = name.Split('.')[0];
            if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem, StringComparer.OrdinalIgnoreCase))
                throw new ContentFormatException("다른 플랫폼에서 사용할 수 없는 이름입니다.");
        }

        private static void CheckLinks(string path)
        {
            for (string current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new ContentFormatException("링크를 통과하는 프로젝트 경로는 허용하지 않습니다.");
        }

        private sealed class PackageWriteStream : Stream
        {
            private readonly Stream destination;
            private readonly CancellationToken token;
            private long written;
            public PackageWriteStream(Stream destination, CancellationToken token) { this.destination = destination; this.token = token; }
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => written; set => throw new NotSupportedException(); }
            public override void Flush() { token.ThrowIfCancellationRequested(); destination.Flush(); }
            public override void Write(byte[] buffer, int offset, int count)
            {
                token.ThrowIfCancellationRequested();
                if (written + count > MaxArchiveBytes) throw new ContentFormatException("ZIP 출력은 128 MiB를 넘을 수 없습니다.");
                destination.Write(buffer, offset, count); written += count;
            }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
#endif
