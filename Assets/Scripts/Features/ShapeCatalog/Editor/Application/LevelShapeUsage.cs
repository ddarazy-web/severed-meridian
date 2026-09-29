using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Levels.Editor
{
    /// <summary>등록 모양과 일치하는 저장된 레벨을 조회한다. 사용 횟수를 별도로 저장하지 않아 중복 집계와 오래된 참조를 피한다.</summary>
    internal static class LevelShapeUsage
    {
        /// <summary>저장된 레벨 한 개의 표시 정보. 원본 Unity 객체를 보유하지 않는다.</summary>
        internal sealed class Entry
        {
            internal string Path;
            internal int Number;
            internal List<string> Obstacles;
        }

        /// <summary>각 레벨 파일을 한 번만 읽고 등록된 활성 칸 키에 일치할 때만 장애물을 집계한다.</summary>
        /// <param name="registered">목록에 실제 등록된 유효한 모양.</param>
        /// <returns>등록 모양별 사용 레벨 목록. 등록이 없으면 레벨 검색 자체를 생략한다.</returns>
        internal static Dictionary<LevelShapePreset, List<Entry>> Read(IReadOnlyList<LevelShapePreset> registered)
        {
            Dictionary<LevelShapePreset, List<Entry>> result = registered.Where(shape => shape != null && shape.IsValid)
                .ToDictionary(shape => shape, _ => new List<Entry>());
            if (result.Count == 0) return result;
            var byMask = result.Keys.GroupBy(shape => new string(shape.Cells.Select(active => active ? '1' : '0').ToArray()))
                .ToDictionary(group => group.Key, group => group.ToArray());
            foreach (string guid in AssetDatabase.FindAssets("t:LevelDefinition", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // LoadAssetAtPath는 편집 중인 미저장 객체를 돌려준다. 디스크의 독립 사본을
                // 읽어 마지막 저장 기준으로 계산하고, 사본만 파괴하여 원본 dirty/Undo를 보존한다.
                UnityEngine.Object[] copies = InternalEditorUtility.LoadSerializedFileAndForget(path);
                try
                {
                    LevelDefinition saved = copies.OfType<LevelDefinition>().SingleOrDefault();
                    if (saved == null || EditorUtility.IsPersistent(saved))
                        throw new IOException("저장된 레벨을 읽지 못했습니다: " + path);
                    if (saved.Board?.Rows != 10 || saved.Board.Columns != 10 || saved.Board.Cells?.Count != 100) continue;
                    string key = new string(saved.Board.Cells.Select(cell => cell.IsActive ? '1' : '0').ToArray());
                    if (!byMask.TryGetValue(key, out LevelShapePreset[] matches)) continue;
                    Entry entry = new Entry { Path = path, Number = saved.LevelNumber, Obstacles = LevelShapeRecommendations.DescribeObstacles(saved) };
                    foreach (LevelShapePreset match in matches) result[match].Add(entry);
                }
                finally
                {
                    foreach (UnityEngine.Object copy in copies)
                        if (copy != null && !EditorUtility.IsPersistent(copy)) UnityEngine.Object.DestroyImmediate(copy);
                }
            }
            foreach (List<Entry> entries in result.Values)
                entries.Sort((left, right) => { int order = left.Number.CompareTo(right.Number); return order != 0 ? order : string.CompareOrdinal(left.Path, right.Path); });
            return result;
        }

        /// <summary>삭제 직전에 저장 파일을 다시 조회하고 미사용 등록 모양만 휴지통으로 이동한다.</summary>
        /// <param name="shape">사용자가 삭제를 확정한 등록 모양.</param>
        /// <returns>성공은 null, 실패는 사용자 안내. 실제 레벨 파일은 변경하지 않는다.</returns>
        internal static string DeleteUnused(LevelShapePreset shape)
        {
            if (shape == null || !shape.IsValid) return "삭제할 등록 맵을 다시 선택하세요.";
            string path = AssetDatabase.GetAssetPath(shape);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                return "프로젝트에 저장된 등록 맵만 삭제할 수 있습니다.";
            try
            {
                if (Read(new[] { shape })[shape].Count > 0) return "사용 중인 레벨이 있어 삭제할 수 없습니다. 사용 현황을 다시 확인하세요.";
                return AssetDatabase.MoveAssetToTrash(path) ? null : "등록 맵을 휴지통으로 옮기지 못했습니다.";
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is UnityException)
            { return "사용 현황을 확인하지 못해 삭제하지 않았습니다. " + error.Message; }
        }
    }
}
