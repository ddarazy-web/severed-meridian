using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>등록 모양의 저장·중복 검사·재사용을 담당한다. 자동 추천은 하지 않는다.</summary>
    internal static class LevelShapeRecommendations
    {
        internal const string Folder = "Assets/Editor/LevelShapes";

        /// <summary>등록 모양의 파일 이름만 변경하고 모양·장애물 이력·원본 기록은 보존한다.</summary>
        /// <param name="shape">목록에서 선택한 저장된 모양.</param>
        /// <param name="fileName">확장자를 제외한 새 이름.</param>
        /// <returns>성공은 null, 실패는 화면에 표시할 안내.</returns>
        internal static string Rename(LevelShapePreset shape, string fileName)
        {
            if (shape == null) return "이름을 바꿀 등록 모양을 선택하세요.";
            string error = LevelAssetOperations.FileNameError(fileName);
            if (error != null) return error;
            string path = AssetDatabase.GetAssetPath(shape);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                return "프로젝트에 저장된 등록 모양만 이름을 바꿀 수 있습니다.";
            string name = fileName.Trim();
            if (Path.GetFileNameWithoutExtension(path) == name) return null;
            string destination = Path.GetDirectoryName(path).Replace('\\', '/') + "/" + name + ".asset";
            if (!string.Equals(path, destination, StringComparison.OrdinalIgnoreCase) &&
                (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(destination)) || File.Exists(destination)))
                return "같은 폴더에 같은 이름의 파일이 있습니다. 다른 이름을 입력하세요.";
            // RenameAsset으로 GUID를 유지하므로 이미 선택한 모양의 참조도 유지된다.
            error = AssetDatabase.RenameAsset(path, name);
            if (string.IsNullOrEmpty(error)) AssetDatabase.SaveAssetIfDirty(shape);
            return string.IsNullOrEmpty(error) ? null : "이름을 바꾸지 못했습니다. " + error;
        }

        /// <summary>사용 가능한 등록 모양을 이름순으로 읽는다. 파일을 자동 생성하지 않는다.</summary>
        /// <returns>등록한 10×10 모양 목록. 첫 사용 시에는 빈 목록이다.</returns>
        internal static List<LevelShapePreset> LoadAll() => AssetDatabase.FindAssets("t:LevelShapePreset", new[] { "Assets" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<LevelShapePreset>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(item => item != null && item.IsValid).OrderBy(item => item.name, StringComparer.Ordinal)
            .ThenBy(item => AssetDatabase.GetAssetPath(item), StringComparer.Ordinal).ToList();

        /// <summary>저장한 레벨을 등록한다. 같은 모양은 기존 기록을 덮어쓰지 않는다.</summary>
        /// <param name="source">선택한 레벨. 미저장 수정은 먼저 저장해야 한다.</param>
        /// <param name="registered">성공 시 새 항목, 중복 시 기존 항목, 그 외 실패 시 null.</param>
        /// <returns>성공은 null, 그 외에는 화면에 표시할 안내.</returns>
        internal static string Register(LevelDefinition source, out LevelShapePreset registered)
        {
            registered = null;
            if (source == null || !AssetDatabase.GetAssetPath(source).StartsWith("Assets/", StringComparison.Ordinal))
                return "저장한 레벨 파일을 먼저 선택하세요.";
            if (EditorUtility.IsDirty(source)) return "레벨에 저장하지 않은 변경이 있습니다. 저장한 뒤 등록하세요.";
            if (source.Board?.Rows != 10 || source.Board.Columns != 10 || source.Board.Cells?.Count != 100 || !source.Board.Cells.Any(cell => cell.IsActive))
                return "사용할 칸이 있는 10×10 보드가 필요합니다.";
            bool[] mask = source.Board.Cells.Select(cell => cell.IsActive).ToArray();
            // 이름·색·장애물·미션은 제외하고 활성 칸 좌표만 비교한다.
            // 회전·이동은 별개의 모양이다. 중복 등록으로 장애물 사용 기록을 바꾸지 않는다.
            registered = LoadAll().FirstOrDefault(item => item.Cells.SequenceEqual(mask));
            if (registered != null) return "같은 모양이 이미 등록되어 있습니다: " + registered.name + " · 기존 기록은 유지합니다.";

            List<string> history = DescribeObstacles(source);
            if (!AssetDatabase.IsValidFolder("Assets/Editor")) AssetDatabase.CreateFolder("Assets", "Editor");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Editor", "LevelShapes");
            LevelShapePreset preset = ScriptableObject.CreateInstance<LevelShapePreset>();
            try
            {
                preset.Capture(source, history);
                string path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + source.name + ".asset");
                AssetDatabase.CreateAsset(preset, path); AssetDatabase.SaveAssetIfDirty(preset);
                registered = preset; return null;
            }
            catch
            {
                if (!EditorUtility.IsPersistent(preset)) UnityEngine.Object.DestroyImmediate(preset);
                throw;
            }
        }

        /// <summary>배치와 공급에서 사용하는 장애물을 사람이 읽는 종류·수량 목록으로 정리한다.</summary>
        /// <param name="source">조회할 레벨 사본 또는 등록 원본.</param>
        /// <returns>장애물·덮개·먼지·벽·고철 공급의 요약.</returns>
        internal static List<string> DescribeObstacles(LevelDefinition source)
        {
            List<string> history = new List<string>();
            if (source.Obstacles != null)
                foreach (var group in source.Obstacles.GroupBy(item => item.Kind).OrderBy(item => item.Key))
                    history.Add(LevelPlacementRules.Name(group.Key) + " · " + group.Count() + "개");
            if (source.Covers != null)
                foreach (var group in source.Covers.GroupBy(item => item.Kind).OrderBy(item => item.Key))
                    history.Add((group.Key == CoverKind.Web ? "거미줄" : "우주 곰팡이") + " · " + group.Count() + "칸");
            if (source.Dust?.Count > 0) history.Add("먼지 · " + source.Dust.Count + "칸");
            if (source.Flow?.Walls?.Count > 0) history.Add("고철 벽 · " + source.Flow.Walls.Count + "구간");
            // 처음 배치되지 않고 생성구에서 나오는 고철도 공급 기록으로 따로 표시한다.
            long fixedScrap = source.Supply?.Sources?.Where(s => s.Mode == SupplyMode.Fixed)
                .Sum(s => s.Items?.Where(i => i.Kind == SupplyKind.Scrap).Sum(i => (long)i.Count) ?? 0) ?? 0;
            if (fixedScrap > 0) history.Add("고철 뭉치 공급 · 고정 목록 " + fixedScrap + "개");
            if (source.Supply?.Sources?.Any(s => s.Mode == SupplyMode.MaintainScrap) == true)
                history.Add("고철 뭉치 공급 · 유지 목표 " + source.Supply.ScrapTarget + "개");
            return history;
        }

        /// <summary>이번에 만든 빈 레벨에 등록 모양과 기본 생성구를 적용한다.</summary>
        /// <param name="level">새로 생성한 빈 레벨. 기존 작업 레벨에 호출하지 않는다.</param>
        /// <param name="shape">사용할 등록 모양.</param>
        /// <param name="number">새 레벨 번호.</param>
        internal static void Initialize(LevelDefinition level, LevelShapePreset shape, int number)
        {
            if (shape == null || !shape.IsValid) throw new ArgumentException("등록 모양을 다시 선택하세요.", nameof(shape));
            using (SerializedObject edit = new SerializedObject(level))
            {
                edit.FindProperty("levelNumber").intValue = number;
                for (int i = 0; i < 100; i++)
                    edit.FindProperty("board.cells").GetArrayElementAtIndex(i).FindPropertyRelative("isActive").boolValue = shape.Cells[i];
                edit.FindProperty("supply.sources").arraySize = 0;
                edit.ApplyModifiedPropertiesWithoutUndo();
            }
            // 장애물 이력은 복제하지 않는다. 기본 아래 중력으로 각 세로 구간을 채울
            // 생성구만 추가한다. 원본의 특수 중력·경로·미션도 함께 복사하지 않는다.
            BoardCoordinate[] sources = Enumerable.Range(0, 100)
                .Where(i => shape.Cells[i] && (i < 10 || !shape.Cells[i-10]))
                .Select(i => new BoardCoordinate(i/10, i%10)).ToArray();
            string error = LevelSupplyEditing.PlaceSources(level, sources);
            if (error != null) throw new InvalidOperationException(error);
            AssetDatabase.SaveAssetIfDirty(level);
        }
    }
}
