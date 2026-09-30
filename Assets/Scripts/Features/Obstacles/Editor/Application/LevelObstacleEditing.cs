using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using UnityEditor;

namespace Levels.Editor
{
    public struct PlacementBrush
    {
        public PlacementLayer Layer;
        public int Kind;
        public int Durability;
        public int RequiredCharge;
        public RabbitColor Color;
        public RocketDirection Direction;
        public bool Erase;
        public bool ReplaceExisting;

        public int Size => Layer == PlacementLayer.Obstacle && !Erase ? LevelPlacementRules.Size((ObstacleKind)Kind) : 1;
    }

    public sealed class PlacementEditResult
    {
        public int Changed;
        public int Skipped;
        public readonly HashSet<string> Reasons = new HashSet<string>();
        public override string ToString() => $"{Changed}개 변경 · {Skipped}개 제외" +
            (Reasons.Count > 0 ? " / " + string.Join(" / ", Reasons) : " · Undo 한 번으로 복구");
    }

    public static class LevelObstacleEditing
    {
        public static string ListPath(PlacementLayer layer) => layer switch
        {
            PlacementLayer.Block => "initialBlocks", PlacementLayer.Obstacle => "obstacles",
            PlacementLayer.Cover => "covers", PlacementLayer.Dust => "dust", _ => null
        };

        public static string PlacementError(LevelDefinition level, PlacementBrush brush, BoardCoordinate coordinate)
        {
            if (!LevelBoardEditing.CanEdit(level)) return "저장 형식 또는 보드·배치 목록을 확인하세요.";
            int index = LevelPlacementRules.Find(level, brush.Layer, coordinate);
            if (index == -2) return "중복 배치: 기존 Inspector 목록에서 수정하세요.";
            if (brush.ReplaceExisting && brush.Layer == PlacementLayer.Obstacle && index >= 0)
            {
                coordinate = level.Obstacles[index].Coordinate;
                if (!string.IsNullOrEmpty(level.Obstacles[index].Id) && level.Obstacles.Count(item => item.Id == level.Obstacles[index].Id) > 1)
                    return "중복 ID의 본체는 교체할 수 없습니다.";
            }
            if (brush.Layer == PlacementLayer.Obstacle && index >= 0 &&
                LevelPlacementRules.Footprint(level.Obstacles[index].Coordinate, LevelPlacementRules.Size(level.Obstacles[index].Kind))
                    .Any(cell => LevelPlacementRules.Find(level, PlacementLayer.Obstacle, cell) == -2))
                return "본체 일부가 중복 점유되어 있습니다. 기존 Inspector에서 수정하세요.";
            if (brush.Erase)
            {
                if (brush.Layer == PlacementLayer.Obstacle && index >= 0 && !string.IsNullOrEmpty(level.Obstacles[index].Id) &&
                    level.Obstacles.Count(item => item.Id == level.Obstacles[index].Id) > 1)
                    return "중복 ID의 본체는 연결을 특정할 수 없습니다. 기존 Inspector에서 ID를 수정하세요.";
                return null;
            }
            string cellError = LevelPlacementRules.CellError(level, coordinate);
            if (cellError != null) return cellError;
            switch (brush.Layer)
            {
                case PlacementLayer.Block:
                    InitialBlockKind blockKind = (InitialBlockKind)brush.Kind;
                    if (!Enum.IsDefined(typeof(InitialBlockKind), blockKind)) return "정의되지 않은 블록입니다.";
                    if (blockKind == InitialBlockKind.FixedNormal && (level.Colors == null || !level.Colors.Contains(brush.Color) ||
                        !Enum.IsDefined(typeof(RabbitColor), brush.Color))) return "사용 색에 없는 블록입니다.";
                    if (blockKind == InitialBlockKind.Rocket && !Enum.IsDefined(typeof(RocketDirection), brush.Direction)) return "로켓 방향 오류입니다.";
                    if (!brush.ReplaceExisting && index >= 0 && level.InitialBlocks[index].Kind != blockKind &&
                        !(LevelPlacementRules.IsNormal(level.InitialBlocks[index].Kind) && LevelPlacementRules.IsNormal(blockKind)))
                        return "다른 블록 종류입니다. 더블클릭으로 교체하세요.";
                    return LevelPlacementRules.BlockSpaceError(level, coordinate);
                case PlacementLayer.Obstacle:
                    ObstacleKind kind = (ObstacleKind)brush.Kind;
                    string valueError = LevelPlacementRules.ObstacleValueError(level, kind, brush.Durability, brush.Color, brush.RequiredCharge);
                    if (valueError != null) return valueError;
                    if (!brush.ReplaceExisting && index >= 0 && (level.Obstacles[index].Kind != kind || LevelPlacementRules.Size(kind) > 1))
                        return "더블클릭으로 교체하거나 속성에서 수정하세요.";
                    return LevelPlacementRules.ObstacleSpaceError(level, coordinate, kind, index);
                case PlacementLayer.Cover:
                    string coverError = LevelPlacementRules.CoverValueError((CoverKind)brush.Kind, brush.Durability);
                    if (coverError != null) return coverError;
                    if (!brush.ReplaceExisting && index >= 0 && (int)level.Covers[index].Kind != brush.Kind) return "다른 덮개입니다. 더블클릭으로 교체하세요.";
                    return LevelPlacementRules.CoverSpaceError(level, coordinate);
                case PlacementLayer.Dust:
                    return brush.Durability >= 1 && brush.Durability <= 3 ? null : "먼지 내구도는 1~3입니다.";
                default: return "편집 층 오류입니다.";
            }
        }

        public static PlacementEditResult Apply(LevelDefinition level, PlacementBrush brush, IEnumerable<BoardCoordinate> coordinates)
        {
            PlacementEditResult result = new PlacementEditResult();
            BoardCoordinate[] targets = coordinates.Distinct().ToArray();
            if (!LevelBoardEditing.CanEdit(level) || ((brush.Size == 2 || brush.ReplaceExisting) && targets.Length != 1))
            {
                result.Skipped = targets.Length;
                result.Reasons.Add("편집할 수 없는 형식이거나 2×2 연속 배치입니다.");
                return result;
            }
            using SerializedObject data = new SerializedObject(level);
            SerializedProperty list = data.FindProperty(ListPath(brush.Layer));
            HashSet<int> deletions = new HashSet<int>();
            HashSet<string> replacedIds = new HashSet<string>();
            foreach (BoardCoordinate coordinate in targets)
            {
                string error = PlacementError(level, brush, coordinate);
                if (error != null)
                {
                    result.Skipped++;
                    result.Reasons.Add(error);
                    continue;
                }
                int index = LevelPlacementRules.Find(level, brush.Layer, coordinate);
                if (brush.Erase)
                {
                    if (index >= 0) deletions.Add(index);
                    continue;
                }
                bool added = index < 0;
                if (added) index = list.arraySize++;
                SerializedProperty item = list.GetArrayElementAtIndex(index);
                // 종류가 바뀐 장애물은 새 본체다. 이전 발전기 연결을 넘겨주지 않는다.
                if (!added && brush.ReplaceExisting && brush.Layer == PlacementLayer.Obstacle && level.Obstacles[index].Kind != (ObstacleKind)brush.Kind)
                {
                    if (!string.IsNullOrEmpty(level.Obstacles[index].Id)) replacedIds.Add(level.Obstacles[index].Id);
                    item.FindPropertyRelative("id").stringValue = Guid.NewGuid().ToString("N");
                    item.FindPropertyRelative("durability").intValue = brush.Durability;
                    item.FindPropertyRelative("color").intValue = (int)brush.Color;
                    item.FindPropertyRelative("requiredCharge").intValue = brush.RequiredCharge;
                }
                // 배열 확장 시 Unity가 마지막 값을 복제하므로 새 항목의 모든 필드를 지정한다.
                if (added)
                {
                    item.FindPropertyRelative("coordinate.row").intValue = coordinate.Row;
                    item.FindPropertyRelative("coordinate.column").intValue = coordinate.Column;
                    if (brush.Layer == PlacementLayer.Block)
                    {
                        item.FindPropertyRelative("fixedColor").intValue = (int)RabbitColor.Type1;
                        item.FindPropertyRelative("rocketDirection").intValue = (int)RocketDirection.Horizontal;
                    }
                    if (brush.Layer == PlacementLayer.Obstacle)
                    {
                        item.FindPropertyRelative("id").stringValue = Guid.NewGuid().ToString("N");
                        item.FindPropertyRelative("durability").intValue = 1;
                        item.FindPropertyRelative("color").intValue = (int)RabbitColor.Type1;
                        item.FindPropertyRelative("requiredCharge").intValue = 3;
                    }
                }
                bool changed = added;
                if (brush.Layer != PlacementLayer.Dust) changed |= SetInt(item, "kind", brush.Kind);
                switch (brush.Layer)
                {
                    case PlacementLayer.Block:
                        if (brush.Kind == (int)InitialBlockKind.FixedNormal) changed |= SetInt(item, "fixedColor", (int)brush.Color);
                        if (brush.Kind == (int)InitialBlockKind.Rocket) changed |= SetInt(item, "rocketDirection", (int)brush.Direction);
                        break;
                    case PlacementLayer.Obstacle:
                        if (brush.Kind == (int)ObstacleKind.Generator) changed |= SetInt(item, "requiredCharge", brush.RequiredCharge);
                        else changed |= SetInt(item, "durability", brush.Durability);
                        if (brush.Kind == (int)ObstacleKind.ColorLock) changed |= SetInt(item, "color", (int)brush.Color);
                        break;
                    default: changed |= SetInt(item, "durability", brush.Durability); break;
                }
                if (changed) result.Changed++;
            }
            if (brush.Layer == PlacementLayer.Obstacle)
            {
                HashSet<string> removedIds = new HashSet<string>(deletions.Select(index => level.Obstacles[index].Id).Where(id => !string.IsNullOrEmpty(id)));
                removedIds.UnionWith(replacedIds);
                SerializedProperty connections = data.FindProperty("connections");
                for (int i = connections.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty connection = connections.GetArrayElementAtIndex(i);
                    if (removedIds.Contains(connection.FindPropertyRelative("generatorId").stringValue) ||
                        removedIds.Contains(connection.FindPropertyRelative("targetId").stringValue)) connections.DeleteArrayElementAtIndex(i);
                }
            }
            foreach (int index in deletions.OrderByDescending(value => value)) list.DeleteArrayElementAtIndex(index);
            result.Changed += deletions.Count;
            if (result.Changed > 0) Commit(data, "레벨 층 배치 편집");
            return result;
        }

        public static bool Move(LevelDefinition level, int index, BoardCoordinate destination, out string message)
        {
            message = "이동할 2×2 본체가 없습니다.";
            if (!LevelBoardEditing.CanEdit(level) || index < 0 || index >= level.Obstacles.Count ||
                LevelPlacementRules.Size(level.Obstacles[index].Kind) != 2) return false;
            ObstaclePlacementDefinition obstacle = level.Obstacles[index];
            if (!string.IsNullOrEmpty(obstacle.Id) && level.Obstacles.Count(item => item.Id == obstacle.Id) > 1)
            { message = "중복 ID입니다. 기존 Inspector에서 수정하세요."; return false; }
            if (LevelPlacementRules.Footprint(obstacle.Coordinate, 2).Any(cell => LevelPlacementRules.Find(level, PlacementLayer.Obstacle, cell) != index))
            {
                message = "중복 본체입니다. 기존 Inspector에서 수정하세요.";
                return false;
            }
            message = LevelPlacementRules.ObstacleSpaceError(level, destination, obstacle.Kind, index);
            if (message != null) return false;
            if (obstacle.Coordinate.Equals(destination)) { message = "변경 없음: 원래 위치입니다."; return false; }
            using SerializedObject data = new SerializedObject(level);
            SerializedProperty item = data.FindProperty("obstacles").GetArrayElementAtIndex(index);
            item.FindPropertyRelative("coordinate.row").intValue = destination.Row;
            item.FindPropertyRelative("coordinate.column").intValue = destination.Column;
            Commit(data, "2×2 본체 이동");
            message = "2×2 본체를 이동했습니다. 바닥 먼지는 원래 칸에 남습니다.";
            return true;
        }

        public static bool SetInt(SerializedProperty item, string field, int value)
        {
            SerializedProperty property = item.FindPropertyRelative(field);
            if (property.intValue == value) return false;
            property.intValue = value;
            return true;
        }

        public static void Commit(SerializedObject data, string name)
        {
            if (!data.hasModifiedProperties) return;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(name);
            data.ApplyModifiedProperties();
            Undo.IncrementCurrentGroup();
        }
    }
}
