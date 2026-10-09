using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LevelAuthoring.Documents;
using LevelAuthoring.Storage;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Editor
{
    // 생성 팩과 명시한 등록 파일을 같은 복구 기록으로 관리한다.
    public static class JsonPackPublication
    {
        private const string JournalPath = "Library/AuthoringPacks/publication.json";

        public static void Publish(string sourceFolder, string outputRoot, Action<int> checkpoint)
            => PublishWithRegistration(sourceFolder, outputRoot, Array.Empty<string>(), null, checkpoint);

        public static void PublishWithRegistration(string sourceFolder, string outputRoot,
            IReadOnlyCollection<string> registrationFiles, Action<IReadOnlyCollection<string>> register, Action<int> checkpoint)
        {
            outputRoot = Path.GetFullPath(outputRoot);
            _ = new ContentSnapshotStore(outputRoot);
            string journal = Path.Combine(outputRoot, JournalPath);
            Directory.CreateDirectory(Path.GetDirectoryName(journal));
            using var gate = new FileStream(journal + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            RestorePending(outputRoot);
            var store = new ContentSnapshotStore(Path.GetFullPath(sourceFolder));
            StoredContentSnapshot source = store.Read();
            Dictionary<string, byte[]> outputs = JsonContentPackBuild.CreateBytes(source.Snapshot);
            string[] paths = outputs.Keys.OrderBy(path => path == JsonContentPackBuild.GenerationPath ? 1 : 0)
                .ThenBy(path => path, StringComparer.Ordinal).ToArray();
            var originals = new JArray();
            foreach (string relative in paths.SelectMany(path => new[] { path, path + ".meta" }).Concat(registrationFiles).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string path = OutputPath(outputRoot, relative);
                originals.Add(new JObject { ["path"] = relative, ["bytes"] = File.Exists(path) ? Convert.ToBase64String(File.ReadAllBytes(path)) : null });
            }
            checkpoint?.Invoke(-1);
            if (store.Read().Hash != source.Hash) throw new ContentFormatException("변환 중 JSON 원본이 변경됐습니다. 팩을 교체하지 않았습니다.");
            AtomicWrite(journal, Encoding.UTF8.GetBytes(new JObject { ["version"] = 1, ["root"] = outputRoot, ["files"] = originals }.ToString()));
            try
            {
                for (int index = 0; index < paths.Length; index++)
                {
                    string path = OutputPath(outputRoot, paths[index]);
                    AtomicWrite(path, outputs[paths[index]]);
                    if (!File.ReadAllBytes(path).SequenceEqual(outputs[paths[index]])) throw new IOException("팩 기록 검증 실패: " + path);
                    checkpoint?.Invoke(index);
                }
                register?.Invoke(paths);
                if (store.Read().Hash != source.Hash) throw new ContentFormatException("교체 중 JSON 원본이 변경됐습니다. 이전 팩으로 복원합니다.");
                File.Delete(journal);
            }
            catch (Exception failure)
            {
                try { RestorePending(outputRoot); }
                catch (Exception recovery) { throw new AggregateException("팩 교체와 복구가 실패했습니다. 복구 기록을 유지합니다.", failure, recovery); }
                throw;
            }
        }

        public static void Recover(string outputRoot)
        {
            outputRoot = Path.GetFullPath(outputRoot);
            _ = new ContentSnapshotStore(outputRoot);
            string journal = Path.Combine(outputRoot, JournalPath);
            if (!File.Exists(journal)) return;
            using var gate = new FileStream(journal + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            RestorePending(outputRoot);
        }

        private static void RestorePending(string root)
        {
            string path = Path.Combine(root, JournalPath);
            if (!File.Exists(path)) return;
            var journal = JObject.Parse(File.ReadAllText(path));
            if ((int?)journal["version"] != 1 || !string.Equals((string)journal["root"], root, StringComparison.OrdinalIgnoreCase) || !(journal["files"] is JArray files))
                throw new ContentFormatException("다른 출력 위치이거나 잘못된 팩 복구 기록입니다.");
            // 모든 경로와 복구 바이트를 먼저 검증한 뒤에만 파일을 변경한다.
            var originals = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in files)
                originals.Add(OutputPath(root, (string)entry["path"]), entry["bytes"]?.Type == JTokenType.Null ? null : Convert.FromBase64String((string)entry["bytes"]));
            foreach (var original in originals)
            {
                if (original.Value == null) { if (File.Exists(original.Key)) File.Delete(original.Key); }
                else AtomicWrite(original.Key, original.Value);
            }
            File.Delete(path);
        }

        private static string OutputPath(string root, string relative)
        {
            if (relative == null) throw new ContentFormatException("팩 출력 경로가 없습니다.");
            string asset = relative.EndsWith(".meta", StringComparison.Ordinal) ? relative.Substring(0, relative.Length - 5) : relative;
            if (asset != Elements.Editor.ElementContentPackBuild.OutputPath && asset != JsonContentPackBuild.GenerationPath &&
                !Regex.IsMatch(asset, @"\AAssets/Data/LevelPacks/levels-[0-9]{6,}\.bytes\z") &&
                !Regex.IsMatch(asset, @"\AAssets/AddressableAssetsData/(?:[A-Za-z0-9_ -]+/)*[A-Za-z0-9_ -]+\.asset\z"))
                throw new ContentFormatException("소유하지 않는 팩 출력 경로입니다: " + relative);
            string path = Path.GetFullPath(Path.Combine(root, relative));
            _ = new ContentSnapshotStore(Path.GetDirectoryName(path));
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new ContentFormatException("링크 팩 파일은 교체하지 않습니다: " + path);
            return path;
        }

        private static void AtomicWrite(string path, byte[] bytes)
        {
            if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(bytes)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string pending = path + ".pending-" + Guid.NewGuid().ToString("N");
            string backup = path + ".replace-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(pending, path, backup); else File.Move(pending, path);
            }
            catch (IOException error) { throw new IOException("팩 파일 교체 실패: " + path, error); }
            finally
            {
                if (File.Exists(pending)) File.Delete(pending);
                if (File.Exists(backup)) File.Delete(backup);
            }
        }
    }
}

