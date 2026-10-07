using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static class PlacementReplacementVerification
    {
        private static LevelEditorWindow window;
        private static LevelDefinition level;
        private static int ticks;
        private static readonly List<string> results = new List<string>();

        public static void Run()
        {
            results.Clear();
            level = ScriptableObject.CreateInstance<LevelDefinition>();
            LevelBoardEditing.Apply(level, LevelBrush.Fixed, RabbitColor.Type1, new[] { new BoardCoordinate(0, 0) });
            window = ScriptableObject.CreateInstance<LevelEditorWindow>();
            window.ShowUtility();
            window.SetLevel(level);
            ticks = 0;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (++ticks < 15) return;
            EditorApplication.update -= Tick;
            try
            {
                LevelBoardView board = window.rootVisualElement.Q<LevelBoardView>();
                window.rootVisualElement.Q<PopupField<string>>("menu-power").value = "청소로켓";
                Click(board, 0, 0, 1);
                Check(level.InitialBlocks[0].Kind == InitialBlockKind.FixedNormal, "단일 클릭은 다른 종류를 보존");
                Click(board, 0, 0, 2);
                Check(level.InitialBlocks[0].Kind == InitialBlockKind.Rocket, "더블클릭 일반→파워 교체");
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Check(level.InitialBlocks[0].Kind == InitialBlockKind.FixedNormal, "교체 Undo 한 번 복원");
                Undo.PerformRedo();
                Check(level.InitialBlocks[0].Kind == InitialBlockKind.Rocket, "교체 Redo");
                window.rootVisualElement.Q<PopupField<string>>("menu-normal").value = "고정 1";
                Click(board, 0, 0, 2);
                Check(level.InitialBlocks[0].Kind == InitialBlockKind.FixedNormal, "파워→일반 교체");
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 2 }, new[] { new BoardCoordinate(0, 0) });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 2 }, new[] { new BoardCoordinate(0, 0) });
                window.rootVisualElement.Q<PopupField<string>>("menu-cover").value = "우주 곰팡이";
                Click(board, 0, 0, 2);
                Check(level.Covers[0].Kind == CoverKind.Mold && level.InitialBlocks.Count == 1 && level.Dust[0].Durability == 2, "덮개만 교체하고 다른 층 보존");
                window.rootVisualElement.Q<PopupField<string>>("menu-obstacle").value = "나무상자";
                string before = JsonUtility.ToJson(level);
                Click(board, 0, 0, 1);
                Check(JsonUtility.ToJson(level) == before, "단일 클릭은 블록과 덮개 보존");
                Click(board, 0, 0, 2);
                Check(level.InitialBlocks.Count == 0 && level.Covers.Count == 0 && level.Obstacles.Count == 1 && level.Dust[0].Durability == 2, "더블클릭 블록→장애물 교체와 먼지 보존");
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Check(JsonUtility.ToJson(level) == before, "장애물 교체 Undo가 블록과 덮개를 함께 복원");
                Click(board, 2, 2, 1);
                window.rootVisualElement.Q<PopupField<string>>("menu-obstacle").value = "고철 뭉치";
                Click(board, 2, 2, 2);
                Check(level.Obstacles.Count == 1 && level.Obstacles[0].Kind == ObstacleKind.Scrap, "장애물 종류 교체");
                window.rootVisualElement.Q<PopupField<string>>("menu-obstacle").value = "대형 폐가전";
                Click(board, 2, 2, 2);
                Check(level.Obstacles[0].Kind == ObstacleKind.Appliance, "빈 주변으로 1×1→2×2 확장");
                before = JsonUtility.ToJson(level);
                window.rootVisualElement.Q<PopupField<string>>("menu-normal").value = "고정 1";
                Click(board, 3, 3, 1);
                Check(JsonUtility.ToJson(level) == before, "단일 클릭은 장애물 보존");
                Click(board, 3, 3, 2);
                Check(level.Obstacles.Count == 0 && LevelPlacementRules.Find(level, PlacementLayer.Block, new BoardCoordinate(3, 3)) >= 0, "2×2 장애물 전체를 클릭한 칸의 일반 블록으로 교체");
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Check(JsonUtility.ToJson(level) == before, "일반 블록 교체 Undo가 2×2 본체 복원");
                window.rootVisualElement.Q<PopupField<string>>("menu-obstacle").value = "나무상자";
                Click(board, 3, 3, 2);
                Check(level.Obstacles.Count == 1 && level.Obstacles[0].Kind == ObstacleKind.Crate && level.Obstacles[0].Coordinate.Equals(new BoardCoordinate(2, 2)), "2×2 내부 클릭은 기준 칸에서 교체");
                LevelBoardEditing.Apply(level, LevelBrush.Fixed, RabbitColor.Type1, new[] { new BoardCoordinate(3, 3) });
                window.rootVisualElement.Q<PopupField<string>>("menu-obstacle").value = "대형 폐가전";
                before = JsonUtility.ToJson(level);
                Click(board, 2, 2, 2);
                Check(level.Obstacles[0].Kind == ObstacleKind.Appliance && LevelPlacementRules.Find(level, PlacementLayer.Block, new BoardCoordinate(3, 3)) == -1, "2×2 확장 영역의 블록도 교체");
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Check(JsonUtility.ToJson(level) == before, "2×2 교체 Undo가 전체 영역 복원");
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Generator, RequiredCharge = 3 }, new[] { new BoardCoordinate(6, 6) });
                LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = 1 }, new[] { new BoardCoordinate(6, 3) });
                using (SerializedObject data = new SerializedObject(level))
                {
                    SerializedProperty connections = data.FindProperty("connections");
                    connections.arraySize = 2;
                    for (int i = 0; i < 2; i++)
                    {
                        SerializedProperty connection = connections.GetArrayElementAtIndex(i);
                        connection.FindPropertyRelative("generatorId").stringValue = level.Obstacles[1].Id;
                        connection.FindPropertyRelative("targetId").stringValue = level.Obstacles[i == 0 ? 0 : 2].Id;
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                before = JsonUtility.ToJson(level);
                window.rootVisualElement.Q<PopupField<string>>("menu-power").value = "청소로켓";
                Click(board, 6, 6, 2);
                Check(level.Obstacles.Count == 2 && level.Connections.Count == 0 &&
                    level.InitialBlocks.Any(item => item.Coordinate.Equals(new BoardCoordinate(6, 6)) && item.Kind == InitialBlockKind.Rocket), "발전기→파워 블록 교체와 연결 정리");
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Check(JsonUtility.ToJson(level) == before, "블록 교체 Undo가 발전기와 연결 복원");
                window.rootVisualElement.Q<PopupField<string>>("menu-obstacle").value = "고철 뭉치";
                Click(board, 2, 2, 2);
                Check(level.Connections.Count == 1 && level.Connections[0].TargetId == level.Obstacles[2].Id, "교체 대상의 연결만 제거");
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Check(JsonUtility.ToJson(level) == before, "교체 Undo가 ID와 연결도 복원");
                Undo.PerformRedo();
                Click(board, 6, 6, 2);
                Check(level.Connections.Count == 0 && level.Obstacles[1].Kind == ObstacleKind.Scrap, "발전기 교체 시 발신 연결 제거");
            }
            catch (Exception exception) { results.Add("FAIL " + exception); }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(level);
                Directory.CreateDirectory("Logs/PlacementReplacementVerification");
                File.WriteAllLines("Logs/PlacementReplacementVerification/results.txt", results);
                EditorApplication.Exit(results.Any(value => value.StartsWith("FAIL")) ? 1 : 0);
            }
        }

        private static void Click(LevelBoardView board, int row, int column, int count)
        {
            Vector2 point = board.LocalToWorld(new Vector2(column * LevelBoardView.CellSize + 20, row * LevelBoardView.CellSize + 20));
            using (PointerDownEvent down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, clickCount = count, mousePosition = point }))
            { down.target = board; board.SendEvent(down); }
            using (PointerUpEvent up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, clickCount = count, mousePosition = point }))
            { up.target = board; board.SendEvent(up); }
        }

        private static void Check(bool condition, string message) => results.Add((condition ? "PASS " : "FAIL ") + message);
    }
}
