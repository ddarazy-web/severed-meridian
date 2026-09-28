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
        /// <summary>작업 중 미완성 내용은 허용하되 배열을 편집하는 데 필요한 구조를 확인한다.</summary>
        /// <param name="level">편집할 레벨.</param>
        /// <returns>현재 형식의 10×10 배열에 안전하게 접근할 수 있으면 true.</returns>
        public static bool CanEdit(LevelDefinition level)
        {
            return level != null && level.SchemaVersion == LevelDefinition.CurrentSchemaVersion &&
                level.Board != null && level.Board.Rows == 10 && level.Board.Columns == 10 &&
                level.Board.Cells != null && level.Board.Cells.Count == 100 && level.InitialBlocks != null &&
                level.Obstacles != null && level.Covers != null && level.Dust != null;
        }

        /// <summary>해당 칸의 초기 블록을 찾는다. 중복 데이터는 임의로 하나만 선택하지 않는다.</summary>
        /// <param name="level">조회할 레벨.</param>
        /// <param name="coordinate">보드의 0부터 시작하는 행·열 좌표.</param>
        /// <returns>배열 위치. -1은 빈칸, -2는 중복 배치.</returns>
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

        /// <summary>한 번의 드래그로 방문한 칸들을 같은 Undo 작업으로 편집한다.</summary>
        /// <param name="level">편집할 원본 레벨.</param>
        /// <param name="brush">활성·비활성·일반 블록 배치·삭제 도구.</param>
        /// <param name="color">고정 일반 블록을 놓을 때 사용할 색.</param>
        /// <param name="coordinates">중복 방문을 포함할 수 있는 칸 목록.</param>
        /// <returns>실제 값이 달라진 칸 수. 편집 불가 또는 변경 없음이면 0.</returns>
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
                // 드래그 중 같은 칸을 여러 번 지나도 한 번만 처리한다.
                // 아래에서 배열 길이를 바꾸므로, 블록 검색은 원본이 아닌 편집 중인 직렬화 배열에서 한다.
                if (!level.Board.Contains(coordinate))
                    continue;
                SerializedProperty active = data.FindProperty("board.cells")
                    .GetArrayElementAtIndex(coordinate.Row * 10 + coordinate.Column).FindPropertyRelative("isActive");
                if (brush == LevelBrush.Activate || brush == LevelBrush.Deactivate)
                {
                    // 칸을 끄는 행위와 내용 삭제는 별개다. 장애물·연결을 몰래 잃지 않도록
                    // 활성 여부만 바꾼다. 비활성 칸에 남은 데이터 오류는 검사에서 안내한다.
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
            // 변경이 있었을 때만 Undo를 등록해, 같은 값을 칠한 빈 작업이 기록되지 않게 한다.
            data.ApplyModifiedProperties();
            Undo.IncrementCurrentGroup();
            return changed;
        }
    }
}
