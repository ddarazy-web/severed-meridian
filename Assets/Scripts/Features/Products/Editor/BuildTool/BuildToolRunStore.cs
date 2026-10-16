using System;
using System.IO;
using Newtonsoft.Json;

namespace Products.Editor
{
    public sealed class BuildToolRunStore
    {
        public const string DefaultRoot = "Library/MatchBuildTool";
        public readonly string Root;
        public string CurrentPath => Path.Combine(Root, "current.json");
        public BuildToolRunStore(string root) { Root = Path.GetFullPath(root); }

        public BuildToolRequest Load()
        {
            if (!File.Exists(CurrentPath)) return null;
            var request = JsonConvert.DeserializeObject<BuildToolRequest>(File.ReadAllText(CurrentPath));
            if (request == null || request.version != 1 || !Guid.TryParseExact(request.id, "N", out _) ||
                !Enum.IsDefined(typeof(BuildToolRunPhase), request.phase) || string.IsNullOrEmpty(request.session) ||
                string.IsNullOrEmpty(request.profileGuid) || !DateTime.TryParse(request.startedUtc, out _))
                throw new InvalidDataException("빌드 요청 기록이 손상됐거나 지원하지 않는 형식입니다: " + CurrentPath);
            request.logPath = Path.Combine(Root, request.id + ".log");
            return request;
        }

        public void Save(BuildToolRequest request)
        {
            if (!Guid.TryParseExact(request.id, "N", out _)) throw new InvalidDataException("빌드 요청 ID가 잘못됐습니다.");
            Directory.CreateDirectory(Root);
            string text = JsonConvert.SerializeObject(request, Formatting.Indented);
            string temporary = CurrentPath + ".tmp";
            File.WriteAllText(temporary, text);
            if (File.Exists(CurrentPath))
            {
                // Windows 파일 검사기의 짧은 공유 잠금만 제한적으로 재시도한다. 영구 오류는 숨기지 않는다.
                for (int attempt = 0; ; attempt++)
                {
                    try { File.Replace(temporary, CurrentPath, null); break; }
                    catch (IOException error) when (attempt < 3 && File.Exists(temporary) &&
                        ((error.HResult & 0xffff) == 32 || (error.HResult & 0xffff) == 33 || (error.HResult & 0xffff) == 1175))
                    { System.Threading.Thread.Sleep(25 * (attempt + 1)); }
                }
            }
            else File.Move(temporary, CurrentPath);
            File.WriteAllText(Path.Combine(Root, request.id + ".json"), text);
        }

        public void Log(BuildToolRequest request, string message)
        {
            Directory.CreateDirectory(Root);
            File.AppendAllText(Path.Combine(Root, request.id + ".log"), DateTime.UtcNow.ToString("O") + " | " + (request.isMock ? "모의" : "실제") + " | " + message + Environment.NewLine);
        }
    }
}
