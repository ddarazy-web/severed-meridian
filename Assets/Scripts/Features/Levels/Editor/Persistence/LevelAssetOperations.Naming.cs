using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Levels.Editor
{
    public static partial class LevelAssetOperations
    {
        public static string FileNameError(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "파일 이름을 입력하세요.";
            string name = value.Trim();
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name == "." || name == ".." || name.EndsWith(".", StringComparison.Ordinal))
                return "이름에 /, \\, :, *, ?, 따옴표 같은 기호를 넣을 수 없습니다. 끝의 점도 빼 주세요.";
            if (name.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return ".asset은 자동으로 붙습니다. 이름만 입력하세요.";
            if (Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", RegexOptions.IgnoreCase))
                return "Windows에서 사용하지 못하는 이름입니다. 다른 이름을 입력하세요.";
            return null;
        }

        public static string Rename(LevelDefinition target, string fileName)
        {
            if (target == null) return "이름을 바꿀 레벨을 먼저 선택하세요.";
            string error = FileNameError(fileName);
            if (error != null) return error;
            string path = AssetDatabase.GetAssetPath(target);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                return "프로젝트에 저장된 레벨 파일만 이름을 바꿀 수 있습니다.";
            string name = fileName.Trim();
            if (Path.GetFileNameWithoutExtension(path) == name) { AssetDatabase.SaveAssetIfDirty(target); return null; }
            string destination = Path.GetDirectoryName(path).Replace('\\', '/') + "/" + name + ".asset";
            if (!string.Equals(path, destination, StringComparison.OrdinalIgnoreCase) &&
                (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(destination)) || File.Exists(destination)))
                return "같은 폴더에 같은 이름의 파일이 있습니다. 다른 이름을 입력하세요.";
            // Unity의 이름 변경 기능을 사용해 GUID와 이 레벨을 가리키는 연결을 보존한다.
            error = AssetDatabase.RenameAsset(path, name);
            if (string.IsNullOrEmpty(error)) AssetDatabase.SaveAssetIfDirty(target);
            return string.IsNullOrEmpty(error) ? null : "이름을 바꾸지 못했습니다. " + error;
        }
    }
}
