#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using LevelAuthoring.Documents;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LevelAuthoring.Storage
{
    // 외부 경로나 URI 대신 앱 내부 프로젝트의 이름만 기록한다.
    public sealed class RecentProjectStore
    {
        private readonly string path;
        private readonly object gate = new object();
        public string ReadWarning { get; private set; }
        public RecentProjectStore(string stateDirectory) { path = Path.Combine(Path.GetFullPath(stateDirectory), "recent-projects.json"); }

        public string[] Read()
        {
            lock (gate)
            {
                ReadWarning = null;
                if (!File.Exists(path) && !File.Exists(path + ".previous")) return Array.Empty<string>();
                try { return Validate(JObject.Parse(File.ReadAllText(path))); }
                catch (Exception error) when (error is IOException || error is JsonException || error is ContentFormatException)
                {
                    try
                    {
                        string[] previous = Validate(JObject.Parse(File.ReadAllText(path + ".previous")));
                        ReadWarning = "최근 프로젝트 기록을 읽을 수 없어 이전 기록을 사용했습니다.";
                        return previous;
                    }
                    catch (Exception backupError) when (backupError is IOException || backupError is JsonException || backupError is ContentFormatException)
                    { throw new ContentFormatException("최근 프로젝트 기록과 이전 기록을 읽을 수 없습니다.", backupError); }
                }
            }
        }

        public void RecordOpened(string internalProjectName)
        {
            lock (gate)
            {
                Validate(new JObject { ["version"] = 1, ["projects"] = new JArray(new JValue(internalProjectName)) });
                string[] previous = Read();
                JObject record = new JObject
                {
                    ["version"] = 1,
                    ["projects"] = new JArray(new[] { internalProjectName }.Concat(previous.Where(name =>
                        !string.Equals(name, internalProjectName, StringComparison.OrdinalIgnoreCase))).Take(10))
                };
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".pending-" + Guid.NewGuid().ToString("N");
                try
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(record.ToString(Formatting.None));
                    using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                    Validate(JObject.Parse(File.ReadAllText(temporary)));
                    // 손상된 현재 파일로 마지막 정상 이전본을 교체하지 않는다.
                    if (File.Exists(path)) File.Replace(temporary, path, ReadWarning == null ? path + ".previous" : null);
                    else File.Move(temporary, path);
                    ReadWarning = null;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
        }

        private static string[] Validate(JObject record)
        {
            if (record["version"]?.Type != JTokenType.Integer || record["version"].ToString() != "1" ||
                !(record["projects"] is JArray projects) || projects.Count > 10 || projects.Any(item => item.Type != JTokenType.String))
                throw new ContentFormatException("최근 프로젝트 기록 형식이 잘못됐습니다.");
            string[] names = projects.Values<string>().ToArray();
            if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
                throw new ContentFormatException("최근 프로젝트 기록에 중복 이름이 있습니다.");
            foreach (string name in names)
            {
                if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name == "." || name == ".." ||
                    name.EndsWith(".", StringComparison.Ordinal) || name.IndexOfAny("/\\:*?\"<>|".ToCharArray()) >= 0 ||
                    name.Any(char.IsControl) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new ContentFormatException("최근 프로젝트에는 내부 프로젝트 이름만 사용할 수 있습니다.");
                string stem = name.Split('.')[0];
                if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }
                    .Contains(stem, StringComparer.OrdinalIgnoreCase))
                    throw new ContentFormatException("다른 플랫폼에서 사용할 수 없는 프로젝트 이름입니다.");
            }
            return names;
        }
    }
}
#endif
