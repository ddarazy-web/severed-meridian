using System;
using System.Linq;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class StartingBoardVerification
    {
        private static void Edges()
        {
            foreach (string shape in new[] { "00 01 11 12 22", "00 02 11 20 22" })
            {
                LevelRuntimeState state = Empty();
                foreach (string point in shape.Split(' ')) Normal(state, C(point[0] - '0', point[1] - '0'));
                Check(MatchQuery.Find(state).Count == 0, "유사 모양 오판 거절 " + shape);
            }
            foreach (int axis in new[] { 0, 1 })
            {
                LevelDefinition level = Definition();
                BoardCoordinate cut = C(axis, 1 - axis);
                using (SerializedObject edit = new SerializedObject(level))
                {
                    edit.FindProperty("board.cells").GetArrayElementAtIndex(cut.Row * 10 + cut.Column).FindPropertyRelative("isActive").boolValue = false;
                    edit.ApplyModifiedPropertiesWithoutUndo();
                }
                LevelRuntimeState state = Build(level);
                foreach (RuntimeCell cell in state.Cells) { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
                foreach (int i in new[] { 0, 2, 3 }) Normal(state, C(axis * i, (1 - axis) * i));
                Check(MatchQuery.Find(state).Count == 0 && !ActionQuery.Swap(state, C(0, 0), cut).IsAllowed, "비활성 칸 연결/교환 차단 " + axis);
                LevelDefinition wallLevel = Definition();
                LevelFlowEditing.SetWalls(wallLevel, new[] { new BoardEdge(C(0, 0), cut) }, false);
                state = Build(wallLevel);
                foreach (RuntimeCell cell in state.Cells) { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
                foreach (int i in new[] { 0, 1, 2 }) Normal(state, C(axis * i, (1 - axis) * i));
                Check(MatchQuery.Find(state).Count == 0, "직선 내부 벽 연결 차단 " + axis);
            }
            LevelRuntimeState both = Empty();
            foreach (BoardCoordinate cell in new[] { C(0, 0), C(2, 0), C(1, 1) }) Normal(both, cell);
            foreach (BoardCoordinate cell in new[] { C(0, 1), C(2, 1), C(1, 0) }) Normal(both, cell, 1);
            Check(ActionQuery.Swap(both, C(1, 0), C(1, 1)).Matches.Count == 2, "양쪽 신규 매칭 교환");
            Check(!ActionQuery.Swap(both, C(0, 0), C(0, 0)).IsAllowed && !ActionQuery.Swap(both, C(-1, 0), C(0, 0)).IsAllowed &&
                !ActionQuery.Swap(both, C(0, 0), C(1, 1)).IsAllowed, "같은 칸/범위 밖/대각선 거절");

            LevelDefinition obstacleLevel = Definition(@"{""obstacles"":[{""id"":""scrap"",""coordinate"":{""row"":0,""column"":1},""kind"":1,""durability"":5},{""id"":""crate"",""coordinate"":{""row"":5,""column"":5},""kind"":0,""durability"":6}],""recoveryParts"":[{""row"":3,""column"":1}]}");
            LevelRuntimeState objects = Build(obstacleLevel);
            foreach (RuntimeCell cell in objects.Cells.Where(c => c.Content == RuntimeContent.Normal)) { Set(cell, "Content", RuntimeContent.Empty); Set(cell, "Color", null); }
            foreach (int row in new[] { 0, 3 })
            {
                Normal(objects, C(row, 0)); Normal(objects, C(row, 2)); Normal(objects, C(row + 1, 1));
                Check(ActionQuery.Swap(objects, C(row, 1), C(row + 1, 1)).IsAllowed, "고철/회수 일반 매칭 허용 " + row);
                Normal(objects, C(row + 1, 1), 1);
                Check(!ActionQuery.Swap(objects, C(row, 1), C(row + 1, 1)).IsAllowed, "고철/회수 미매칭 거절 " + row);
                foreach (RuntimeContent power in new[] { RuntimeContent.Rocket, RuntimeContent.Bomb, RuntimeContent.Drone, RuntimeContent.Magnet })
                {
                    Power(objects, C(row + 1, 1), power);
                    Check(ActionQuery.Swap(objects, C(row, 1), C(row + 1, 1)).IsAllowed == (power != RuntimeContent.Magnet), "고철/회수 파워 표 " + row + "/" + power);
                }
            }
            Power(objects, C(5, 4), RuntimeContent.Rocket);
            Check(!ActionQuery.Swap(objects, C(5, 4), C(5, 5)).IsAllowed, "고정 장애물 파워 교환 거절");
            string snapshot = Snapshot(objects);
            ActionQuery.Find(objects); MatchQuery.Find(objects);
            Check(Snapshot(objects) == snapshot, "장애물/내구도/회수/미션/공급 전체 조회 불변");

            LevelDefinition fixedLevel = Definition();
            using (SerializedObject edit = new SerializedObject(fixedLevel))
            {
                SerializedProperty cells = edit.FindProperty("board.cells"), blocks = edit.FindProperty("initialBlocks"); blocks.arraySize = 6;
                for (int i = 0; i < cells.arraySize; i++) cells.GetArrayElementAtIndex(i).FindPropertyRelative("isActive").boolValue = i / 10 < 2 && i % 10 < 3;
                int[] colors = { 0, 1, 0, 0, 0, 2 };
                for (int i = 0; i < 6; i++)
                {
                    SerializedProperty block = blocks.GetArrayElementAtIndex(i);
                    LevelFlowEditing.SetCoordinate(block.FindPropertyRelative("coordinate"), C(i / 3, i % 3));
                    block.FindPropertyRelative("kind").intValue = 1; block.FindPropertyRelative("fixedColor").intValue = colors[i];
                }
                edit.ApplyModifiedPropertiesWithoutUndo();
            }
            StartingBoardSearch fixedSuccess = StartingBoardBuilder.Build(fixedLevel, 12345);
            Check(fixedSuccess.Status == StartingBoardStatus.Success && fixedSuccess.Attempts == 0 && fixedSuccess.RandomDrawCount == 0, "전체 고정 시작 성공/난수 미소비");
            Check(ActionQuery.Swap(fixedSuccess.State, C(0, 1), C(0, 2)).IsAllowed, "초기 고정은 교환 잠금 아님");

            // 서로 인접하지 않은 변수 9칸: 완성 매칭은 없지만 교환도 없다. 5^9 할당 전에 기본 상한에 도달한다.
            LevelDefinition hard = Definition();
            using (SerializedObject edit = new SerializedObject(hard))
            {
                SerializedProperty cells = edit.FindProperty("board.cells");
                for (int i = 0; i < cells.arraySize; i++) cells.GetArrayElementAtIndex(i).FindPropertyRelative("isActive").boolValue = i / 10 <= 4 && i % 10 <= 4 && i / 10 % 2 == 0 && i % 10 % 2 == 0;
                edit.ApplyModifiedPropertiesWithoutUndo();
            }
            System.Diagnostics.Stopwatch total = System.Diagnostics.Stopwatch.StartNew();
            StartingBoardSearch capped = new StartingBoardSearch(hard, 9876);
            double maxChunk = 0;
            while (!capped.IsDone)
            {
                System.Diagnostics.Stopwatch chunk = System.Diagnostics.Stopwatch.StartNew(); capped.Advance(64); chunk.Stop();
                maxChunk = Math.Max(maxChunk, chunk.Elapsed.TotalMilliseconds);
            }
            total.Stop();
            Check(capped.Status == StartingBoardStatus.LimitReached && capped.Attempts == 100000 && capped.State == null,
                "기본 상한 100000 유한 종료 / 총 " + total.ElapsedMilliseconds + "ms / 최대 64할당 " + maxChunk.ToString("F2") + "ms");
        }
    }
}
