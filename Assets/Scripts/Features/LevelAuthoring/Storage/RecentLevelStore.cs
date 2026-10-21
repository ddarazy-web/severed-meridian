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
    public sealed class RecentLevelStore
    {
        private readonly string directory;
        private readonly object gate = new object();
        public string ReadWarning { get; private set; }
        public RecentLevelStore(string stateDirectory) { directory = Path.GetFullPath(stateDirectory); }

        // 같은 프로젝트 ID를 가진 작업 사본도 폴더가 다르면 별도 최근 목록을 갖는다.
        private string RecordPath(string folder, string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId)) throw new ArgumentException("프로젝트 ID가 필요합니다.", nameof(projectId));
            string normalized = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (Path.DirectorySeparatorChar == '\\') normalized = normalized.ToUpperInvariant();
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(normalized + "\n" + projectId));
                return Path.Combine(directory, "recent-levels-" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant() + ".json");
            }
        }

        public string[] Read(string folder, string projectId)
        {
            lock (gate)
            {
                string path = RecordPath(folder, projectId);
                ReadWarning = null;
                if (!File.Exists(path) && !File.Exists(path + ".previous")) return Array.Empty<string>();
                try { return Decode(File.ReadAllText(path)); }
                catch (Exception error) when (error is IOException || error is JsonException || error is ContentFormatException)
                {
                    ReadWarning = "최근 편집 기록을 읽을 수 없어 이전 기록을 사용합니다.";
                    try { return Decode(File.ReadAllText(path + ".previous")); }
                    catch (Exception previous) when (previous is IOException || previous is JsonException || previous is ContentFormatException)
                    {
                        ReadWarning = "최근 편집 기록이 손상되어 목록을 비웠습니다. 기존 기록 파일은 보존합니다.";
                        return Array.Empty<string>();
                    }
                }
            }
        }

        public void RecordOpened(string folder, string projectId, string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId)) throw new ArgumentException("레벨 ID가 필요합니다.", nameof(levelId));
            lock (gate)
            {
                string path = RecordPath(folder, projectId);
                string[] previous = Read(folder, projectId);
                string[] ids = new[] { levelId }.Concat(previous.Where(id => id != levelId)).Take(20).ToArray();
                if (ReadWarning == null && ids.SequenceEqual(previous)) return;
                JObject record = new JObject { ["version"] = 1, ["levels"] = new JArray(ids) };
                Directory.CreateDirectory(directory);
                // 해시 파일명 뒤에 접미사를 붙이면 Windows 경로 한도를 넘을 수 있다.
                string temporary = Path.Combine(directory, "pending-" + Guid.NewGuid().ToString("N"));
                try
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(record.ToString(Formatting.None));
                    using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                    Decode(File.ReadAllText(temporary));
                    // 손상 기록은 별도 보존하여 새 기록 저장이 원본을 지우지 않게 한다.
                    if (File.Exists(path)) File.Replace(temporary, path, ReadWarning == null ? path + ".previous" :
                        Path.Combine(directory, "recent.json.corrupt-" + Guid.NewGuid().ToString("N")));
                    else File.Move(temporary, path);
                    ReadWarning = null;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        private static string[] Decode(string text)
        {
            JObject record = JObject.Parse(text);
            if (record["version"]?.Type != JTokenType.Integer || record["version"].ToString() != "1" ||
                !(record["levels"] is JArray levels) || levels.Count > 20 || levels.Any(id => id.Type != JTokenType.String))
                throw new ContentFormatException("최근 레벨 기록 형식이 잘못됐습니다.");
            string[] ids = levels.Values<string>().ToArray();
            if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new ContentFormatException("최근 레벨 기록에 빈 ID 또는 중복이 있습니다.");
            return ids;
        }
    }
}
#endif
