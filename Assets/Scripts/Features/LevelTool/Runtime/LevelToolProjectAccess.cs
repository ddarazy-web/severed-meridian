#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Documents;
using LevelAuthoring.Editing;

namespace LevelTool
{
    // Android 작업 폴더는 앱 내부의 직접 하위 프로젝트로만 제한한다.
    public static class LevelToolProjectAccess
    {
        public static string InternalRoot(string persistentDataPath) =>
            Path.GetFullPath(Path.Combine(persistentDataPath, "LevelTool", "Projects"));

        public static string[] List(string root)
        {
            if (!Directory.Exists(root)) return Array.Empty<string>();
            return Directory.GetDirectories(root).Where(path => (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0)
                .Select(Path.GetFileName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static string Resolve(string root, string name, bool requireNew = false)
        {
            if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name == "." || name == ".." ||
                name.EndsWith(".", StringComparison.Ordinal) || name.IndexOfAny("/\\:*?\"<>|".ToCharArray()) >= 0 ||
                name.Any(char.IsControl) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ContentFormatException("프로젝트 이름에는 경로나 파일 이름에 사용할 수 없는 문자를 넣을 수 없습니다.");
            string stem = name.Split('.')[0];
            if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }
                .Contains(stem, StringComparer.OrdinalIgnoreCase))
                throw new ContentFormatException("다른 플랫폼에서 사용할 수 없는 프로젝트 이름입니다.");
            string path = Path.GetFullPath(Path.Combine(Path.GetFullPath(root), name));
            if (File.Exists(path) || Directory.Exists(path))
            {
                if (requireNew) throw new ContentFormatException("같은 이름의 폴더나 파일이 이미 있습니다. 다른 프로젝트 이름을 선택하세요.");
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new ContentFormatException("앱 내부 프로젝트에는 외부 폴더 연결을 사용할 수 없습니다.");
            }
            return path;
        }

        // 복구·인계·재활성화로 이미 세션이 있으면 기본 프로젝트로 교체하지 않는다.
        public static bool OpenDefault(AuthoringToolWorkspace workspace, string root, ContentSnapshot template)
        {
            if (workspace.Session != null) return false;
            string path = Resolve(root, "Default");
            if (File.Exists(Path.Combine(path, "project.json"))) return workspace.Open(path, "Cancel");
            if (File.Exists(path) || Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
                throw new ContentFormatException("기본 프로젝트 폴더에 부분 생성 파일이 있습니다. 자동으로 덮어쓰지 않았습니다.");
            return workspace.Create(path, template);
        }
    }
}
#endif
