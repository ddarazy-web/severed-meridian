#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.IO;
using System.Linq;

namespace LevelTool
{
    // Windows 도구의 파일 시스템 탐색 경계. UI나 Editor 선택창을 참조하지 않는다.
    public static class LevelToolFolderAccess
    {
        public static string[] List(string directory)
        {
            string path = Path.GetFullPath(directory);
            string[] drives = Directory.GetLogicalDrives();
            string parent = Directory.GetParent(path)?.FullName;
            string[] children = Directory.GetDirectories(path);
            Array.Sort(children, StringComparer.OrdinalIgnoreCase);
            return drives.Concat(parent == null ? Array.Empty<string>() : new[] { parent }).Concat(children)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
}
#endif
