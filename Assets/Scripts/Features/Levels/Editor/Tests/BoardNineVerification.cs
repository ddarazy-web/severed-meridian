using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using MemoryPack;
using Simulation;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static class BoardNineVerification
    {
        private const string Evidence = "Logs/BoardNineVerification";
        private static readonly List<string> Results = new List<string>();
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static T Read<T>(string json) => JsonUtility.FromJson<T>(json);
        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);

        public static void Run()
        {
            Directory.CreateDirectory(Evidence);
            Results.Clear();
            try
            {
                Migration();
                EditorGeometry();
                SaveAssetsAndPacks();
                File.WriteAllLines(Evidence + "/data-results.txt", Results);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Results.Add("FAIL " + error);
                File.WriteAllLines(Evidence + "/data-results.txt", Results);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void Migration()
        {
            LevelDefinition fresh = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                Check(fresh.Board.Rows == 9 && fresh.Board.Columns == 9 && fresh.Board.Cells.Count == 81,
                    "신규 보드 9×9 / 81칸");
                string cells = string.Join(",", Enumerable.Range(0, 100).Select(i => "{\"isActive\":" + (i == 18 ? "false" : "true") + "}"));
                PackedLevel old = fresh.ToPacked();
                old.Board = Read<BoardDefinition>("{\"rows\":10,\"columns\":10,\"cells\":[" + cells + "]}");
                old.InitialBlocks.Add(Read<InitialBlockDefinition>("{\"coordinate\":{\"row\":8,\"column\":8},\"kind\":1,\"fixedColor\":2}"));
                old.InitialBlocks.Add(Read<InitialBlockDefinition>("{\"coordinate\":{\"row\":9,\"column\":0},\"kind\":1}"));
                old.InitialBlocks.Add(Read<InitialBlockDefinition>("{\"coordinate\":{\"row\":0,\"column\":9},\"kind\":1}"));
                old.Obstacles.Add(Read<ObstaclePlacementDefinition>("{\"id\":\"keep\",\"coordinate\":{\"row\":6,\"column\":6},\"kind\":5}"));
                old.Obstacles.Add(Read<ObstaclePlacementDefinition>("{\"id\":\"drop\",\"coordinate\":{\"row\":8,\"column\":7},\"kind\":4}"));
                old.Covers.Add(Read<CoverPlacementDefinition>("{\"coordinate\":{\"row\":9,\"column\":3}}"));
                old.Dust.Add(Read<DustPlacementDefinition>("{\"coordinate\":{\"row\":2,\"column\":9}}"));
                old.RecoveryParts.AddRange(new[] { C(8, 1), C(9, 1) });
                old.Flow = Read<LevelFlowDefinition>("{\"gravity\":[{\"coordinate\":{\"row\":9,\"column\":0}}],\"paths\":[{\"coordinate\":{\"row\":8,\"column\":1},\"next\":{\"row\":9,\"column\":1}}],\"walls\":[{\"a\":{\"row\":8,\"column\":0},\"b\":{\"row\":9,\"column\":0}}],\"portals\":[{\"entrance\":{\"row\":1,\"column\":1},\"hasExit\":true,\"exit\":{\"row\":9,\"column\":2}}],\"arrivals\":[{\"row\":8,\"column\":2},{\"row\":9,\"column\":2}],\"merges\":[{\"coordinate\":{\"row\":8,\"column\":3},\"sources\":[{\"row\":8,\"column\":2},{\"row\":9,\"column\":3}]}]}");
                old.Supply = Read<LevelSupplyDefinition>("{\"sources\":[{\"coordinate\":{\"row\":0,\"column\":8}},{\"coordinate\":{\"row\":0,\"column\":9}}]}");
                old.Connections.Add(Read<LevelConnectionDefinition>("{\"generatorId\":\"keep\",\"targetId\":\"drop\",\"vertices\":[{\"row\":8,\"column\":8}]}"));
                byte[] bytes = MemoryPackSerializer.Serialize(new LevelPack { FormatVersion = 1, FirstLevel = 1, Levels = new[] { old } });
                LevelDefinition converted = LevelPackCodec.ReadLevel(bytes, 1);
                try
                {
                    Check(converted.Board.Cells.Count == 81 && !converted.Board.Cells[17].IsActive && converted.Board.Cells[18].IsActive,
                        "구형 MemoryPack 행 간격 10→9 매핑 / 마지막 행·열 제외");
                    Check(converted.InitialBlocks.Count == 1 && converted.InitialBlocks[0].Coordinate.Equals(C(8, 8)), "8,8 유지 / 행9·열9 배치 제거");
                    Check(converted.Obstacles.Count == 1 && converted.Connections.Count == 0, "경계에 걸친 2×2 본체와 종속 전선 제거");
                    Check(converted.Covers.Count == 0 && converted.Dust.Count == 0 && converted.RecoveryParts.SequenceEqual(new[] { C(8, 1) }), "덮개·바닥·회수 부품 경계 정리");
                    Check(converted.Flow.Paths.Count == 2 && converted.Flow.Paths.All(path => path.IsEnd) && converted.Flow.Gravity.Count == 0 && converted.Flow.Walls.Count == 0 &&
                        converted.Flow.Portals.Count == 0 && converted.Flow.Merges.Count == 0 && converted.Flow.Arrivals.Single().Equals(C(8, 2)), "흐름 경계·통로·합류 정리 / 잘린 경로 끝 처리");
                    Check(!LevelFlowRules.Next(converted, C(1, 1), out _), "출구가 잘린 통로 입구에 새 중력 경로를 만들지 않음");
                    Check(converted.Supply.Sources.Count == 1 && converted.Supply.Sources[0].Coordinate.Equals(C(0, 8)), "생성구 마지막 열 제거");
                    string snapshot = JsonUtility.ToJson(converted);
                    converted.OnAfterDeserialize();
                    Check(JsonUtility.ToJson(converted) == snapshot, "9×9 재로드 반복 변환 없음");
                    LevelDefinition roundtrip = LevelPackCodec.ReadLevel(LevelPackCodec.Encode(new[] { converted }), 1);
                    Check(JsonUtility.ToJson(roundtrip) == snapshot, "9×9 MemoryPack 왕복 / 스키마4·50레벨 단위 유지");
                    UnityEngine.Object.DestroyImmediate(roundtrip);
                }
                finally { UnityEngine.Object.DestroyImmediate(converted); }
                Check(old.Board.Rows == 10 && old.InitialBlocks.Count == 3, "구형 팩 디코드 원본 객체 무변경");
                string legacyJson = "{\"board\":{\"rows\":10,\"columns\":10,\"cells\":[" + cells + "]}}";
                JsonUtility.FromJsonOverwrite(legacyJson, fresh);
                Check(fresh.Board.Cells.Count == 81 && !fresh.Board.Cells[17].IsActive, "Unity 에셋 역직렬화 같은 자르기 적용");
                JsonUtility.FromJsonOverwrite("{\"board\":{\"rows\":10,\"columns\":10,\"cells\":[{\"isActive\":true}]}}", fresh);
                Check(fresh.Board.Rows == 10 && fresh.Board.Cells.Count == 1, "불완전한 구형 배열 자동 복구하지 않음");
            }
            finally { UnityEngine.Object.DestroyImmediate(fresh); }
        }

        private static void SaveAssetsAndPacks()
        {
            foreach (string shapeGuid in AssetDatabase.FindAssets("t:LevelShapePreset", new[] { "Assets/Editor/LevelShapes" }))
            {
                LevelShapePreset shape = AssetDatabase.LoadAssetAtPath<LevelShapePreset>(AssetDatabase.GUIDToAssetPath(shapeGuid));
                Check(shape.Cells.Count == 81, "등록 모양 81칸 " + shape.name);
                EditorUtility.SetDirty(shape);
                AssetDatabase.SaveAssetIfDirty(shape);
            }
            LevelDefinition[] levels = AssetDatabase.FindAssets("t:LevelDefinition", new[] { LevelAssetOperations.DefaultFolder })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<LevelDefinition>).ToArray();
            foreach (LevelDefinition level in levels)
            {
                Check(level.Board.Rows == 9 && level.Board.Columns == 9 && level.Board.Cells.Count == 81, "저장 에셋 9×9 " + level.name);
                LevelStateBuildResult built = LevelStateBuilder.Build(level, 12345);
                Check(built.IsBuilt, "실제 에셋 플레이 상태 생성 " + level.name + " " + string.Join(" / ", built.Issues));
                string path = AssetDatabase.GetAssetPath(level);
                string guid = AssetDatabase.AssetPathToGUID(path);
                EditorUtility.SetDirty(level);
                AssetDatabase.SaveAssetIfDirty(level);
                Check(AssetDatabase.AssetPathToGUID(path) == guid, "레벨 GUID 유지 " + level.name);
            }
            // 파일 직렬화만 수행한다. Addressables 콘텐츠/플레이어 빌드는 호출하지 않는다.
            foreach (var group in levels.GroupBy(level => LevelPackCodec.FirstLevel(level.LevelNumber)))
            {
                string path = LevelPackBuild.FilePath(group.Key);
                File.WriteAllBytes(path, LevelPackCodec.Encode(group));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Check(LevelPackCodec.Decode(File.ReadAllBytes(path)).Levels.All(level => level.Board.Cells.Count == 81), "50레벨 구간 팩 갱신 " + path);
            }
        }

        private static void EditorGeometry()
        {
            LevelBoardView view = new LevelBoardView();
            Check(view.style.width.value.value == 396 && view.style.height.value.value == 396 &&
                view.CellAt(C(8, 8)).name == "cell-80" && view.CellAt(C(8, 8)).style.width.value.value == 44,
                "편집 보드 396px / 44px 셀 / 마지막 셀80");
            MethodInfo cell = typeof(LevelFlowOverlay).GetMethod("Cell", BindingFlags.NonPublic | BindingFlags.Static);
            object[] inside = { new Vector2(395, 395), default(BoardCoordinate) };
            Check((bool)cell.Invoke(null, inside) && ((BoardCoordinate)inside[1]).Equals(C(8, 8)), "편집 흐름 오른쪽 아래 칸 입력");
            Check(!(bool)cell.Invoke(null, new object[] { new Vector2(396, 395), default(BoardCoordinate) }) &&
                !(bool)cell.Invoke(null, new object[] { new Vector2(395, 396), default(BoardCoordinate) }), "편집 보드 바깥 입력 제외");
            Vector2 portal = (Vector2)typeof(LevelConnectionGraph).GetMethod("CellPoint", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { C(8, 8) });
            Check(portal == new Vector2(374, 374), "통로 연결점 44px 셀 중앙 일치");
        }
    }
}
