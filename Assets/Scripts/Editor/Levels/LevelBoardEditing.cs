using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using UnityEditor;

namespace Levels.Editor
{
    public enum LevelBrush { Select, Activate, Deactivate, Fixed, Random, Erase, Placement, Move, Flow, SourceSelect, Source, SourceErase, Recovery, RecoveryErase }

    public static class LevelBoardEditing
    {
        public static bool CanEdit(LevelDefinition level)
        {
            return level != null && level.SchemaVersion == LevelDefinition.CurrentSchemaVersion &&
                level.Board != null && level.Board.Rows == 10 && level.Board.Columns == 10 &&
                level.Board.Cells != null && level.Board.Cells.Count == 100 && level.InitialBlocks != null &&
                level.Obstacles != null && level.Covers != null && level.Dust != null;
        }

        // -1은 빈칸, -2는 중복 배치다. 중복을 임의로 하나의 블록으로 해석하지 않는다.
        public static int FindBlock(LevelDefinition level, BoardCoordinate coordinate)
        {
            int found = -1;
            if (level == null || level.InitialBlocks == null)
                return found;
            for (int i = 0; i < level.InitialBlocks.Count; i++)
            {
                if (!level.InitialBlocks[i].Coordinate.Equals(coordinate))
                    continue;
                if (found >= 0)
                    return -2;
                found = i;
            }
            return found;
        }

        // 방문 좌표들을 한 직렬화 작업으로 적용한다. 반환값은 실제 변경한 칸 수다.
        public static int Apply(LevelDefinition level, LevelBrush brush, RabbitColor color,
            IEnumerable<BoardCoordinate> coordinates)
        {
            if (!CanEdit(level) || brush == LevelBrush.Select)
                return 0;
            if (brush == LevelBrush.Fixed && (level.Colors == null || !level.Colors.Contains(color) ||
                !Enum.IsDefined(typeof(RabbitColor), color)))
                return 0;

            using SerializedObject data = new SerializedObject(level);
            SerializedProperty blocks = data.FindProperty("initialBlocks");
            int changed = 0;
            foreach (BoardCoordinate coordinate in coordinates.Distinct())
            {
                if (!level.Board.Contains(coordinate))
                    continue;
                SerializedProperty active = data.FindProperty("board.cells")
                    .GetArrayElementAtIndex(coordinate.Row * 10 + coordinate.Column).FindPropertyRelative("isActive");
                if (brush == LevelBrush.Activate || brush == LevelBrush.Deactivate)
                {
                    bool desired = brush == LevelBrush.Activate;
                    if (active.boolValue == desired)
                        continue;
                    active.boolValue = desired;
                    changed++;
                    continue;
                }

                int index = -1;
                for (int i = 0; i < blocks.arraySize; i++)
                {
                    SerializedProperty position = blocks.GetArrayElementAtIndex(i).FindPropertyRelative("coordinate");
                    if (position.FindPropertyRelative("row").intValue != coordinate.Row ||
                        position.FindPropertyRelative("column").intValue != coordinate.Column)
                        continue;
                    if (index >= 0) { index = -2; break; }
                    index = i;
                }
                if (index == -2)
                    continue;
                if (brush == LevelBrush.Erase)
                {
                    if (index < 0)
                        continue;
                    blocks.DeleteArrayElementAtIndex(index);
                    changed++;
                    continue;
                }
                if (!active.boolValue || LevelPlacementRules.BlockSpaceError(level, coordinate) != null)
                    continue;
                int kind = brush == LevelBrush.Fixed ? (int)InitialBlockKind.FixedNormal : (int)InitialBlockKind.RandomNormal;
                if (index >= 0)
                {
                    SerializedProperty existing = blocks.GetArrayElementAtIndex(index);
                    int oldKind = existing.FindPropertyRelative("kind").intValue;
                    if (!LevelPlacementRules.IsNormal((InitialBlockKind)oldKind))
                        continue;
                    if (oldKind == kind && (brush != LevelBrush.Fixed ||
                        existing.FindPropertyRelative("fixedColor").intValue == (int)color))
                        continue;
                }
                else
                {
                    index = blocks.arraySize++;
                    SerializedProperty added = blocks.GetArrayElementAtIndex(index);
                    added.FindPropertyRelative("coordinate.row").intValue = coordinate.Row;
                    added.FindPropertyRelative("coordinate.column").intValue = coordinate.Column;
                    added.FindPropertyRelative("fixedColor").intValue = (int)RabbitColor.Type1;
                }
                SerializedProperty block = blocks.GetArrayElementAtIndex(index);
                block.FindPropertyRelative("kind").intValue = kind;
                if (brush == LevelBrush.Fixed)
                    block.FindPropertyRelative("fixedColor").intValue = (int)color;
                changed++;
            }
            if (changed == 0)
                return 0;
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("레벨 보드 편집");
            data.ApplyModifiedProperties();
            Undo.IncrementCurrentGroup();
            return changed;
        }
    }
}
