using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class LevelAssetOperations
    {
        public static int? SuggestDuplicateNumber(LevelDefinition source)
        {
            if (source == null || source.LevelNumber == int.MaxValue) return null;
            HashSet<int> used = new HashSet<int>();
            foreach (string guid in AssetDatabase.FindAssets("t:LevelDefinition", new[] { "Assets" }))
            {
                LevelDefinition item = AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null) used.Add(item.LevelNumber);
            }
            int number = Math.Max(1, source.LevelNumber + 1);
            while (used.Contains(number))
            {
                if (number == int.MaxValue) return null;
                number++;
            }
            return number;
        }

        public static LevelDefinition DuplicateAtPath(LevelDefinition source, string path, int number)
        {
            if (source == null) throw new ArgumentException("복제할 레벨을 선택하세요.");
            if (source.SchemaVersion < 1 || source.SchemaVersion > LevelDefinition.CurrentSchemaVersion)
                throw new ArgumentException("현재 코드로 전체 필드를 보존할 수 없는 저장 버전입니다.");
            if (number <= 0) throw new ArgumentException("레벨 번호는 양수여야 합니다.");
            foreach (string guid in AssetDatabase.FindAssets("t:LevelDefinition", new[] { "Assets" }))
            {
                LevelDefinition item = AssetDatabase.LoadAssetAtPath<LevelDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && item.LevelNumber == number) throw new ArgumentException($"레벨 번호 {number}가 이미 사용 중입니다.");
            }
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || path.Contains("\\") ||
                path.Contains("/../") || path.Contains("/./") || path.Contains("//") || path.Contains(":"))
                throw new ArgumentException("Assets 내부의 새 .asset 경로를 지정하세요.");
            string fullPath = Path.GetFullPath(path);
            string assetRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(assetRoot, StringComparison.OrdinalIgnoreCase) ||
                !AssetDatabase.IsValidFolder(Path.GetDirectoryName(path).Replace('\\', '/')))
                throw new ArgumentException("존재하는 Assets 폴더를 지정하세요.");
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)) || File.Exists(fullPath) || File.Exists(fullPath + ".meta"))
                throw new ArgumentException("이미 존재하는 에셋이나 meta를 덮어쓸 수 없습니다.");

            // 디스크 복사가 아니라 현재 편집 상태를 깊게 복제한다. 내부 ID는 레벨 안에서만 참조한다.
            LevelDefinition copy = UnityEngine.Object.Instantiate(source);
            bool created = false;
            try
            {
                copy.name = Path.GetFileNameWithoutExtension(path);
                using (SerializedObject data = new SerializedObject(copy))
                {
                    data.FindProperty("levelNumber").intValue = number;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                AssetDatabase.CreateAsset(copy, path);
                created = EditorUtility.IsPersistent(copy) && string.Equals(AssetDatabase.GetAssetPath(copy), path, StringComparison.OrdinalIgnoreCase);
                if (!created) throw new IOException("복제 에셋을 생성하지 못했습니다.");
                AssetDatabase.SaveAssetIfDirty(copy);
                return copy;
            }
            catch
            {
                // 이 호출에서 만든 사본만 정리한다. 기존 대상이나 원본은 삭제하지 않는다.
                if (EditorUtility.IsPersistent(copy) && string.Equals(AssetDatabase.GetAssetPath(copy), path, StringComparison.OrdinalIgnoreCase))
                    AssetDatabase.DeleteAsset(path);
                else if (!EditorUtility.IsPersistent(copy)) UnityEngine.Object.DestroyImmediate(copy);
                throw;
            }
        }
    }
}
