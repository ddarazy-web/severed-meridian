using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static class LevelSupplyEditing
    {
        [Serializable]
        private sealed class Clipboard
        {
            public string type;
            public int version;
            public List<SupplyItem> items;
        }

        public static bool CanEdit(LevelDefinition level) => LevelBoardEditing.CanEdit(level) &&
            level.Supply?.Sources != null && level.RecoveryParts != null && level.Missions != null;

        public static void WriteItem(SerializedProperty property, SupplyItem item)
        {
            property.FindPropertyRelative("kind").intValue = (int)item.Kind;
            property.FindPropertyRelative("count").intValue = item.Count;
            property.FindPropertyRelative("color").intValue = (int)item.Color;
            property.FindPropertyRelative("direction").intValue = (int)item.Direction;
            property.FindPropertyRelative("durability").intValue = item.Durability;
        }

        public static string PlaceSources(LevelDefinition level, IEnumerable<BoardCoordinate> coordinates, bool erase = false)
        {
            if (!CanEdit(level)) return "편집할 수 없는 레벨입니다.";
            BoardCoordinate[] cells = coordinates.Distinct().ToArray();
            foreach (BoardCoordinate cell in cells)
            {
                if (LevelSupplyRules.FindSource(level, cell) == -2) return "중복 생성구는 원본 Inspector에서 수정하세요.";
                if (!erase && LevelSupplyRules.SourceCellError(level, cell) is string error) return cell + " " + error;
            }
            using SerializedObject data = new SerializedObject(level);
            SerializedProperty list = data.FindProperty("supply.sources");
            if (erase)
            {
                for (int i = level.Supply.Sources.Count - 1; i >= 0; i--)
                    if (cells.Contains(level.Supply.Sources[i].Coordinate)) list.DeleteArrayElementAtIndex(i);
            }
            else foreach (BoardCoordinate cell in cells)
            {
                if (LevelSupplyRules.FindSource(level, cell) >= 0) continue;
                SerializedProperty source = list.GetArrayElementAtIndex(list.arraySize++);
                LevelFlowEditing.SetCoordinate(source.FindPropertyRelative("coordinate"), cell);
                source.FindPropertyRelative("mode").intValue = (int)SupplyMode.Random;
                source.FindPropertyRelative("exhaustion").intValue = (int)SupplyExhaustion.Stop;
                source.FindPropertyRelative("items").arraySize = 0;
            }
            LevelObstacleEditing.Commit(data, "생성구 배치 편집");
            return null;
        }

        public static string AddTopSources(LevelDefinition level) => PlaceSources(level,
            Enumerable.Range(0, BoardDefinition.DefaultColumns).Select(column => new BoardCoordinate(0, column)).Where(cell => LevelFlowRules.Active(level, cell)));

        public static string PlaceRecovery(LevelDefinition level, IEnumerable<BoardCoordinate> coordinates, bool erase = false)
        {
            if (!CanEdit(level)) return "편집할 수 없는 레벨입니다.";
            BoardCoordinate[] cells = coordinates.Distinct().ToArray();
            foreach (BoardCoordinate cell in cells)
                if (!erase && LevelSupplyRules.RecoverySpaceError(level, cell) is string error) return cell + " " + error;
            List<BoardCoordinate> result = level.RecoveryParts.ToList();
            if (erase) result.RemoveAll(cell => cells.Contains(cell));
            else foreach (BoardCoordinate cell in cells) if (!result.Contains(cell)) result.Add(cell);
            using SerializedObject data = new SerializedObject(level);
            LevelFlowEditing.SetCoordinates(data.FindProperty("recoveryParts"), result);
            LevelObstacleEditing.Commit(data, "회수 부품 배치 편집");
            return null;
        }

        public static string SetSourceProperty(LevelDefinition level, IReadOnlyCollection<int> indices, string field, int value)
        {
            string error = TargetsError(level, indices);
            if (error != null) return error;
            if (field != "mode" && field != "exhaustion") return "공통 편집 가능한 속성이 아닙니다.";
            if (field == "mode" && !Enum.IsDefined(typeof(SupplyMode), value) ||
                field == "exhaustion" && !Enum.IsDefined(typeof(SupplyExhaustion), value)) return "정의되지 않은 설정입니다.";
            if (field == "exhaustion" && indices.Any(index => level.Supply.Sources[index].Mode != SupplyMode.Fixed)) return "고정 공급 생성구만 선택하세요.";
            return CommitCandidate(level, "생성구 공통 속성", data =>
            {
                foreach (int index in indices.Distinct())
                    data.FindProperty($"supply.sources.Array.data[{index}].{field}").intValue = value;
                if (field == "mode" && value == (int)SupplyMode.MaintainScrap && level.Supply.ScrapTarget == 0)
                    data.FindProperty("supply.scrapTarget").intValue = 1;
                if (field == "mode" && value == (int)SupplyMode.MaintainRecovery && level.Supply.RecoveryTarget == 0)
                    data.FindProperty("supply.recoveryTarget").intValue = 1;
            });
        }

        public static string SetItems(LevelDefinition level, int index, IReadOnlyList<SupplyItem> items)
        {
            string error = TargetsError(level, new[] { index });
            if (error != null) return error;
            if (level.Supply.Sources[index].Mode != SupplyMode.Fixed && items.Count > 0) return "고정 공급 방식에서 목록을 편집하세요.";
            foreach (SupplyItem item in items) if (LevelSupplyRules.ItemError(level, item) is string itemError) return itemError;
            return CommitCandidate(level, "고정 공급 목록 편집", data => WriteItems(data.FindProperty($"supply.sources.Array.data[{index}].items"), items));
        }

        public static string Copy(LevelDefinition level, int index)
        {
            if (TargetsError(level, new[] { index }) != null || level.Supply.Sources[index].Mode != SupplyMode.Fixed || level.Supply.Sources[index].Items == null) return null;
            return JsonUtility.ToJson(new Clipboard { type = "MatchSupplyList", version = 1, items = level.Supply.Sources[index].Items.ToList() });
        }

        public static string Paste(LevelDefinition level, IReadOnlyCollection<int> indices, string text, bool append)
        {
            string error = TargetsError(level, indices);
            if (error != null) return error;
            Clipboard clipboard;
            try { clipboard = JsonUtility.FromJson<Clipboard>(text); }
            catch (ArgumentException) { return "공급 목록 클립보드 형식이 아닙니다."; }
            if (clipboard == null || clipboard.type != "MatchSupplyList" || clipboard.version != 1 || clipboard.items == null)
                return "지원하지 않는 공급 목록 클립보드입니다.";
            foreach (SupplyItem item in clipboard.items) if (LevelSupplyRules.ItemError(level, item) is string itemError) return itemError;
            foreach (int index in indices)
                if (level.Supply.Sources[index].Mode != SupplyMode.Fixed || level.Supply.Sources[index].Items == null)
                    return $"{level.Supply.Sources[index].Coordinate}: 고정 공급 방식의 정상 목록을 선택하세요. 전체 작업을 취소했습니다.";
            return CommitCandidate(level, append ? "공급 목록 뒤에 추가" : "공급 목록 교체", data =>
            {
                foreach (int index in indices.Distinct())
                {
                    List<SupplyItem> items = append ? level.Supply.Sources[index].Items.ToList() : new List<SupplyItem>();
                    items.AddRange(clipboard.items);
                    WriteItems(data.FindProperty($"supply.sources.Array.data[{index}].items"), items);
                }
            });
        }

        private static string TargetsError(LevelDefinition level, IReadOnlyCollection<int> indices)
        {
            if (!CanEdit(level)) return "편집할 수 없는 레벨입니다.";
            if (indices == null || indices.Count == 0 || indices.Any(index => index < 0 || index >= level.Supply.Sources.Count)) return "유효한 생성구를 선택하세요.";
            return null;
        }

        private static void WriteItems(SerializedProperty list, IReadOnlyList<SupplyItem> items)
        {
            list.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) WriteItem(list.GetArrayElementAtIndex(i), items[i]);
        }

        private static string CommitCandidate(LevelDefinition level, string title, Action<SerializedObject> change)
        {
            // 여러 생성구를 함께 수정할 때 일부만 반영되는 상태를 방지한다.
            // 복제본에 변경을 먼저 적용하고, 공급 방식 충돌이 늘어나지 않을 때 원본에 한 번 반영한다.
            // 기존의 미완성 미션 등은 이 변경과 무관하므로 저장 자체를 막지 않는다.
            LevelDefinition candidate = UnityEngine.Object.Instantiate(level);
            try
            {
                using (SerializedObject preview = new SerializedObject(candidate)) { change(preview); preview.ApplyModifiedPropertiesWithoutUndo(); }
                List<LevelValidationIssue> before = new List<LevelValidationIssue>(), after = new List<LevelValidationIssue>();
                LevelSupplyRules.Validate(level, before); LevelSupplyRules.Validate(candidate, after);
                // 작업 중 미션 누락 등은 허용하지만 새 공급 방식 충돌은 원자적으로 거절한다.
                foreach (IGrouping<string, LevelValidationIssue> group in after.Where(issue => issue.Code == LevelValidationCode.SupplyConflict).GroupBy(issue => issue.Message))
                    if (group.Count() > before.Count(issue => issue.Code == LevelValidationCode.SupplyConflict && issue.Message == group.Key)) return group.Key;
                using SerializedObject data = new SerializedObject(level);
                change(data);
                LevelObstacleEditing.Commit(data, title);
                return null;
            }
            finally { UnityEngine.Object.DestroyImmediate(candidate); }
        }
    }
}
