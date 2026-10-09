using Levels;
using System;
using System.IO;

namespace AutoPlay
{
    /// <summary>시험 기록 조회·삭제가 지정 저장소 밖이나 정션을 따라가지 않도록 경계를 검사한다.</summary>
    public static class TestRecordPaths
    {
        /// <param name="root">호출자가 소유하는 고정 저장 루트. 화면 입력으로 받지 않는다.</param>
        /// <param name="path">검사할 파일 또는 폴더. 아직 존재하지 않아도 경로와 기존 조상은 검사한다.</param>
        public static void Check(string root, string path)
        {
            string allowed = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string target = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!target.Equals(allowed, StringComparison.OrdinalIgnoreCase) &&
                !target.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("시험 저장 위치 밖의 경로입니다.");
            // 루트 자체나 그 상위가 정션이면 내부 이름이 안전해도 외부 데이터를 가리킬 수 있다.
            for (string current = target; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("연결된 파일이나 폴더는 처리하지 않습니다: " + current);
        }
    }
}
