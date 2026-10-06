using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEngine;

namespace Elements.Editor
{
    public static class ElementSupplyEditing
    {
        [Serializable] private sealed class Clipboard
        {
            public string type;
            public int version;
            public List<ElementSupplyItemDefinition> items;
        }
        private static string TargetsError(LevelDefinition level, IEnumerable<int> indices)
        {
            if (!LevelSupplyEditing.CanEdit(level)) return "편집할 수 없는 레벨입니다.";
            if (indices == null || !indices.Any() || indices.Any(index => index < 0 || index >= level.ElementSupply.sources.Count || level.ElementSupply.sources[index]?.items == null))
                return "유효한 신형 생성구를 선택하세요.";
            return null;
        }
        public static string PlaceSources(LevelDefinition level, IEnumerable<BoardCoordinate> coordinates, bool erase)
        {
            if (!LevelSupplyEditing.CanEdit(level)) return "편집할 수 없는 레벨입니다.";
            BoardCoordinate[] cells = coordinates.Distinct().ToArray();
            foreach (BoardCoordinate cell in cells)
            {
                if (LevelSupplyRules.FindSource(level, cell) == -2) return "중복 생성구는 원본 Inspector에서 수정하세요.";
                if (!erase && LevelSupplyRules.SourceCellError(level, cell) is string error) return cell + " " + error;
            }
            using SerializedObject data = new SerializedObject(level);
            SerializedProperty sources = data.FindProperty("elementSupply.sources");
            if (erase)
            {
                for (int i = sources.arraySize - 1; i >= 0; i--)
                    if (cells.Contains(level.ElementSupply.sources[i].coordinate)) sources.DeleteArrayElementAtIndex(i);
            }
            else foreach (BoardCoordinate cell in cells)
            {
                if (LevelSupplyRules.FindSource(level, cell) >= 0) continue;
                SerializedProperty source = sources.GetArrayElementAtIndex(sources.arraySize++);
                LevelFlowEditing.SetCoordinate(source.FindPropertyRelative("coordinate"), cell);
                source.FindPropertyRelative("mode").intValue = (int)SupplyMode.Random;
                source.FindPropertyRelative("exhaustion").intValue = (int)SupplyExhaustion.Stop;
                source.FindPropertyRelative("randomDefinitionId").stringValue = LegacyElementMap.Get(SupplyKind.RandomNormal).Value;
                source.FindPropertyRelative("items").arraySize = 0;
            }
            LevelObstacleEditing.Commit(data, "신형 생성구 배치"); return null;
        }
        public static int MaximumDurability(LevelDefinition level, string supplyId)
        {
            ElementCatalog catalog = level.CreateElementCatalog(); ElementSupplyProfile supply = catalog.Get(new ElementId(supplyId)).RequireSupply();
            if (supply.Behavior != ElementSupplyBehavior.Obstacle) throw new ArgumentException("내구도 공급 정의가 아닙니다: " + supplyId);
            ElementDefinition body = catalog.Get(supply.ObstacleDefinitionId ?? LegacyElementMap.Get(supply.Obstacle.Value));
            if (body.ReactionBehavior != ElementReactionBehavior.Durability || body.RequirePlacement().Size != 1)
                throw new ArgumentException("한 칸 내구도 본체만 공급할 수 있습니다: " + body.Id.Value);
            return body.RequirePlacement().MaxDurability;
        }
        public static string ItemError(LevelDefinition level, ElementSupplyItemDefinition item)
        {
            try
            {
                if (item == null || item.count < 1) return "공급 항목과 양수 수량이 필요합니다.";
                ElementDefinition definition = level.CreateElementCatalog().Get(new ElementId(item.definitionId));
                PackedElementDefinition.FromDefinition(definition).ToDefinition();
                ElementSupplyProfile supply = definition.RequireSupply();
                if (supply.Behavior == ElementSupplyBehavior.FixedNormal && (!Enum.IsDefined(typeof(RabbitColor), item.color) || !level.Colors.Contains(item.color)))
                    return "레벨 사용 색을 선택하세요.";
                if (supply.Content == Simulation.RuntimeContent.Rocket && !Enum.IsDefined(typeof(RocketDirection), item.direction)) return "로켓 방향이 잘못됐습니다.";
                if (supply.Behavior == ElementSupplyBehavior.Obstacle && (item.durability < 1 || item.durability > MaximumDurability(level, item.definitionId)))
                    return "공급 본체의 실제 내구도 범위를 벗어났습니다: " + item.definitionId;
                return null;
            }
            catch (ArgumentException error) { return error.Message; }
            catch (InvalidOperationException error) { return error.Message; }
            catch (KeyNotFoundException error) { return error.Message; }
        }
        public static string SetSourceProperty(LevelDefinition level, IReadOnlyCollection<int> indices, string field, int value)
        {
            string error = TargetsError(level, indices); if (error != null) return error;
            if (field != "mode" && field != "exhaustion") return "편집 가능한 생성구 설정이 아닙니다.";
            if (field == "mode" && !Enum.IsDefined(typeof(SupplyMode), value) || field == "exhaustion" && !Enum.IsDefined(typeof(SupplyExhaustion), value)) return "정의되지 않은 설정입니다.";
            if (field == "exhaustion" && indices.Any(index => level.ElementSupply.sources[index].mode != SupplyMode.Fixed)) return "고정 생성구만 선택하세요.";
            return CommitCandidate(level, "신형 생성구 공통 설정", data =>
            { foreach (int index in indices.Distinct()) data.FindProperty($"elementSupply.sources.Array.data[{index}].{field}").intValue = value; });
        }
        public static string SetRandomDefinition(LevelDefinition level, IReadOnlyCollection<int> indices, ElementId id)
        {
            string error = TargetsError(level, indices); if (error != null) return error;
            try { if (level.CreateElementCatalog().Get(id).RequireSupply().Behavior != ElementSupplyBehavior.RandomNormal) return "무작위 일반 공급 정의를 선택하세요."; }
            catch (Exception invalid) when (invalid is ArgumentException || invalid is InvalidOperationException || invalid is KeyNotFoundException) { return invalid.Message; }
            return CommitCandidate(level, "무작위 공급 정의 선택", data =>
            { foreach (int index in indices.Distinct()) data.FindProperty($"elementSupply.sources.Array.data[{index}].randomDefinitionId").stringValue = id.Value; });
        }
        public static string SetItems(LevelDefinition level, int index, IReadOnlyList<ElementSupplyItemDefinition> items)
        {
            string error = TargetsError(level, new[] { index }); if (error != null) return error;
            if (items == null) return "공급 목록이 없습니다.";
            if (items.Count > 0 && level.ElementSupply.sources[index].mode != SupplyMode.Fixed) return "고정 방식에서 목록을 편집하세요.";
            foreach (ElementSupplyItemDefinition item in items) if (ItemError(level, item) is string invalid) return invalid;
            return CommitCandidate(level, "정의 ID 고정 공급 목록", data => WriteItems(data.FindProperty($"elementSupply.sources.Array.data[{index}].items"), items));
        }
        public static string SetMaintenanceProperty(LevelDefinition level, string field, int value)
        {
            if (!LevelSupplyEditing.CanEdit(level) || level.SchemaVersion != LevelDefinition.CurrentSchemaVersion) return "신형 레벨을 선택하세요.";
            if (field != "scrapTarget" && field != "scrapLimit" && field != "scrapDurability" && field != "recoveryTarget") return "편집 가능한 유지 설정이 아닙니다.";
            if (value < 0) return "유지 수량과 한도는 0 이상이어야 합니다.";
            if (field == "scrapDurability")
            {
                try { if (value < 1 || value > MaximumDurability(level, level.ElementSupply.scrapDefinitionId)) return "선택 공급 본체의 내구도 범위를 벗어났습니다."; }
                catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is KeyNotFoundException) { return error.Message; }
            }
            return CommitCandidate(level, "요소 유지 공급 설정", data => data.FindProperty("elementSupply." + field).intValue = value);
        }
        public static string SetMaintenanceDefinition(LevelDefinition level, string field, ElementId id)
        {
            if (!LevelSupplyEditing.CanEdit(level) || level.SchemaVersion != LevelDefinition.CurrentSchemaVersion) return "신형 레벨을 선택하세요.";
            if (field != "scrapDefinitionId" && field != "recoveryDefinitionId") return "유지 공급 정의 필드가 아닙니다.";
            try
            {
                ElementSupplyProfile profile = level.CreateElementCatalog().Get(id).RequireSupply();
                if (field == "recoveryDefinitionId" && profile.Behavior != ElementSupplyBehavior.Recovery) return "회수 공급 정의를 선택하세요.";
                if (field == "scrapDefinitionId")
                {
                    MaximumDurability(level, id.Value);
                    ElementDefinition body = level.CreateElementCatalog().Get(profile.ObstacleDefinitionId ?? LegacyElementMap.Get(profile.Obstacle.Value));
                    if (body.RemovalMissionProfile.Kind != MissionKind.Scrap) return "고철 미션 공급 정의를 선택하세요.";
                }
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is KeyNotFoundException) { return error.Message; }
            return CommitCandidate(level, "유지 공급 정의 선택", data => data.FindProperty("elementSupply." + field).stringValue = id.Value);
        }
        public static string Copy(LevelDefinition level, int index) => TargetsError(level, new[] { index }) != null ||
            level.ElementSupply.sources[index].mode != SupplyMode.Fixed ? null : JsonUtility.ToJson(new Clipboard
            { type = "MatchElementSupplyList", version = 2, items = level.ElementSupply.sources[index].items });
        public static string Paste(LevelDefinition level, IReadOnlyCollection<int> indices, string text, bool append)
        {
            string error = TargetsError(level, indices); if (error != null) return error;
            Clipboard value;
            try { value = JsonUtility.FromJson<Clipboard>(text); }
            catch (ArgumentException) { return "요소 공급 클립보드 형식이 아닙니다."; }
            if (value == null || value.type != "MatchElementSupplyList" || value.version != 2 || value.items == null) return "지원하지 않는 요소 공급 클립보드입니다.";
            foreach (ElementSupplyItemDefinition item in value.items) if (ItemError(level, item) is string invalid) return invalid;
            if (indices.Any(index => level.ElementSupply.sources[index].mode != SupplyMode.Fixed)) return "고정 생성구만 선택하세요. 전체 작업을 취소했습니다.";
            return CommitCandidate(level, append ? "요소 공급 목록 추가" : "요소 공급 목록 교체", data =>
            {
                foreach (int index in indices.Distinct()) WriteItems(data.FindProperty($"elementSupply.sources.Array.data[{index}].items"),
                    append ? level.ElementSupply.sources[index].items.Concat(value.items).ToArray() : value.items);
            });
        }
        private static void WriteItems(SerializedProperty list, IReadOnlyList<ElementSupplyItemDefinition> items)
        {
            list.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                SerializedProperty value = list.GetArrayElementAtIndex(i); ElementSupplyItemDefinition item = items[i];
                value.FindPropertyRelative("definitionId").stringValue = item.definitionId;
                value.FindPropertyRelative("count").intValue = item.count;
                value.FindPropertyRelative("color").intValue = (int)item.color;
                value.FindPropertyRelative("direction").intValue = (int)item.direction;
                value.FindPropertyRelative("durability").intValue = item.durability;
            }
        }
        private static string CommitCandidate(LevelDefinition level, string title, Action<SerializedObject> mutation)
        {
            LevelDefinition candidate = UnityEngine.Object.Instantiate(level);
            try
            {
                using (SerializedObject preview = new SerializedObject(candidate)) { mutation(preview); preview.ApplyModifiedPropertiesWithoutUndo(); }
                List<LevelValidationIssue> before = LevelDefinitionValidator.Validate(level), after = LevelDefinitionValidator.Validate(candidate);
                foreach (IGrouping<string, LevelValidationIssue> issue in after.Where(issue => issue.Code == LevelValidationCode.SupplyConflict).GroupBy(issue => issue.Message))
                    if (issue.Count() > before.Count(old => old.Code == LevelValidationCode.SupplyConflict && old.Message == issue.Key)) return issue.Key;
                using SerializedObject data = new SerializedObject(level); mutation(data); LevelObstacleEditing.Commit(data, title); return null;
            }
            finally { UnityEngine.Object.DestroyImmediate(candidate); }
        }
    }
}
