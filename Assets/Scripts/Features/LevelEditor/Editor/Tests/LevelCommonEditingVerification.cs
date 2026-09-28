using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class LevelCommonEditingVerification
    {
        private const string Evidence = "Logs/LevelCommonEditingVerification";
        private static readonly List<string> Results = new List<string>();
        [Serializable] private sealed class State { public string folder; public int process; public string[] paths, values, guids; }

        public static void Prepare()
        {
            Directory.CreateDirectory(Evidence);
            if (File.Exists(Evidence + "/state.json")) throw new InvalidOperationException("기존 검증 Restart를 먼저 실행하세요.");
            string folder = "Assets/__LevelCommonVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            try
            {
                LevelDefinition source = LevelAssetOperations.CreateAtPath(folder + "/Source.asset");
                Set(source, "levelNumber", 61001);
                foreach ((int column, RabbitColor color) in new[] { (0, RabbitColor.Type1), (1, RabbitColor.Type2) })
                    LevelObstacleEditing.Apply(source, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.ColorLock, Durability = column + 1, Color = color }, new[] { C(4, column) });
                LevelObstacleEditing.Apply(source, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Crate, Durability = 2 }, new[] { C(5, 0) });
                LevelObstacleEditing.Apply(source, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Appliance, Durability = 9 }, new[] { C(6, 6) });
                Check(LevelCommonEditing.TrySelect(source, PlacementLayer.Obstacle, C(4, 0), out PlacementSelection first) &&
                    LevelCommonEditing.TrySelect(source, PlacementLayer.Obstacle, C(4, 1), out _), "동일 종류 선택");
                LevelCommonEditing.TrySelect(source, PlacementLayer.Obstacle, C(4, 1), out PlacementSelection second);
                LevelCommonEditing.TrySelect(source, PlacementLayer.Obstacle, C(5, 0), out PlacementSelection crate);
                LevelCommonEditing.TrySelect(source, PlacementLayer.Obstacle, C(6, 6), out PlacementSelection body);
                LevelCommonEditing.TrySelect(source, PlacementLayer.Obstacle, C(7, 7), out PlacementSelection sameBody);
                Check(body.Equals(sameBody), "2×2 다른 점유 칸은 같은 본체");
                string original = JsonUtility.ToJson(source);
                Check(LevelCommonEditing.Set(source, new[] { first, second }, "durability", 3, out int changed) == null && changed == 2, "공통 내구도 2개 수정");
                Check(source.Obstacles[0].Color == RabbitColor.Type1 && source.Obstacles[1].Color == RabbitColor.Type2 &&
                    source.Obstacles[0].Id == first.Id && source.Obstacles[1].Id == second.Id, "무관한 색·ID 보존");
                Undo.PerformUndo(); Check(JsonUtility.ToJson(source) == original, "공통 편집 한 번 Undo"); Undo.PerformRedo();
                Check(LevelCommonEditing.Set(source, new[] { first, second }, "durability", 3, out changed) == null && changed == 0, "무변경 공통 편집");
                Undo.PerformUndo(); Check(JsonUtility.ToJson(source) == original, "무변경 입력은 Undo 추가 없음"); Undo.PerformRedo();
                string clipboard = LevelCommonEditing.Copy(source, first);
                Check(!clipboard.Contains(first.Id) && !clipboard.Contains("coordinate"), "클립보드에 ID·좌표 제외");
                string beforePaste = JsonUtility.ToJson(source);
                Check(LevelCommonEditing.Paste(source, new[] { second, crate }, clipboard, out changed, out int excluded) == null && changed == 1 && excluded == 1,
                    "같은 종류 적용·다른 종류 제외");
                string afterPaste = JsonUtility.ToJson(source);
                Undo.PerformUndo(); Check(JsonUtility.ToJson(source) == beforePaste, "설정 붙여넣기 한 번 Undo");
                Undo.PerformRedo(); Check(JsonUtility.ToJson(source) == afterPaste, "설정 붙여넣기 한 번 Redo");
                original = JsonUtility.ToJson(source);
                Check(LevelCommonEditing.Set(source, new[] { first, second }, "durability", 99, out _) != null && JsonUtility.ToJson(source) == original, "잘못된 값 전체 거절");
                Check(LevelCommonEditing.Paste(source, new[] { first }, "{}", out _, out _) != null && JsonUtility.ToJson(source) == original, "잘못된 설정 클립보드 보존");
                LevelObstacleEditing.Apply(source, new PlacementBrush { Layer = PlacementLayer.Obstacle, Erase = true }, new[] { second.Coordinate });
                original = JsonUtility.ToJson(source);
                Check(LevelCommonEditing.Set(source, new[] { first, second }, "durability", 1, out _) != null && JsonUtility.ToJson(source) == original, "삭제된 대상 포함 시 전체 거절");
                Undo.PerformUndo();
                AssetDatabase.SaveAssetIfDirty(source);
                Set(source, "moveCount", 37);
                original = JsonUtility.ToJson(source); string sourceGuid = AssetDatabase.AssetPathToGUID(folder + "/Source.asset");
                Check(EditorUtility.IsDirty(source), "미저장 원본 구성");
                LevelDefinition copy = LevelAssetOperations.DuplicateAtPath(source, folder + "/Copy.asset", 61002);
                Check(copy.MoveCount == 37 && copy.LevelNumber == 61002 && EditorUtility.IsDirty(source) && JsonUtility.ToJson(source) == original, "메모리 복제와 원본 dirty 보존");
                Check(AssetDatabase.AssetPathToGUID(folder + "/Copy.asset") != sourceGuid && sourceGuid == AssetDatabase.AssetPathToGUID(folder + "/Source.asset"), "새 GUID·원본 GUID 보존");
                Set(copy, "levelNumber", 61001);
                Check(JsonUtility.ToJson(copy) == original, "번호 외 전체 복제 값 동일");
                Set(copy, "levelNumber", 61002);
                Set(copy, "obstacles.Array.data[0].durability", 1);
                Check(JsonUtility.ToJson(source) == original, "사본 목록 수정은 원본과 독립");
                foreach ((string path, int number) in new[] { (folder + "/Copy.asset", 61003), (folder + "/DuplicateNumber.asset", 61001), ("../Outside.asset", 61003) })
                {
                    bool rejected = false;
                    try { LevelAssetOperations.DuplicateAtPath(source, path, number); } catch (ArgumentException) { rejected = true; }
                    Check(rejected && JsonUtility.ToJson(source) == original && EditorUtility.IsDirty(source), "복제 충돌/경로 거절·원본 보존 " + path);
                }
                Set(source, "schemaVersion", 99);
                bool futureRejected = false;
                try { LevelAssetOperations.DuplicateAtPath(source, folder + "/Future.asset", 61003); } catch (ArgumentException) { futureRejected = true; }
                Check(futureRejected && !File.Exists(folder + "/Future.asset"), "미래 버전 손실 가능 복제 거절");
                Set(source, "schemaVersion", 4);
                int legacyNumber = 62000;
                foreach (string fixture in new[] { "Version1Invalid", "Version2Normal", "Version3Flow", "Version3Invalid" })
                {
                    string legacyPath = folder + "/" + fixture + ".asset";
                    File.Copy("Assets/Scripts/Features/Levels/Editor/Tests/Fixtures/" + fixture + ".txt", legacyPath);
                    AssetDatabase.ImportAsset(legacyPath, ImportAssetOptions.ForceSynchronousImport);
                    LevelDefinition legacy = AssetDatabase.LoadAssetAtPath<LevelDefinition>(legacyPath);
                    string legacyJson = JsonUtility.ToJson(legacy);
                    LevelDefinition legacyCopy = LevelAssetOperations.DuplicateAtPath(legacy, folder + "/" + fixture + "Copy.asset", ++legacyNumber);
                    Set(legacyCopy, "levelNumber", legacy.LevelNumber);
                    Check(JsonUtility.ToJson(legacyCopy) == legacyJson && JsonUtility.ToJson(legacy) == legacyJson && !EditorUtility.IsDirty(legacy), fixture + " 미전환 전체 복제·원본 보존");
                    Check(legacyCopy.Connections.All(connection => LevelConnectionRules.Find(legacyCopy, connection.GeneratorId) == LevelConnectionRules.Find(legacy, connection.GeneratorId)),
                        fixture + " 연결은 사본 내부 조회");
                    Set(legacyCopy, "levelNumber", legacyNumber);
                }
                VerifyFields(folder);
                string[] paths = AssetDatabase.FindAssets("t:LevelDefinition", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
                foreach (string path in paths) AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path));
                State state = new State { folder = folder, process = System.Diagnostics.Process.GetCurrentProcess().Id, paths = paths,
                    values = paths.Select(path => JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path))).ToArray(), guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray() };
                File.WriteAllText(Evidence + "/state.json", JsonUtility.ToJson(state, true));
            }
            catch (Exception exception)
            {
                Results.Add("FAIL " + exception); AssetDatabase.DeleteAsset(folder); throw;
            }
            finally { File.WriteAllLines(Evidence + "/data-results.txt", Results); }
        }

        public static void Restart()
        {
            State state = JsonUtility.FromJson<State>(File.ReadAllText(Evidence + "/state.json"));
            Check(state.process != System.Diagnostics.Process.GetCurrentProcess().Id, "별도 Unity 프로세스");
            for (int i = 0; i < state.paths.Length; i++)
                Check(JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<LevelDefinition>(state.paths[i])) == state.values[i] && AssetDatabase.AssetPathToGUID(state.paths[i]) == state.guids[i], "전체 값·GUID 재시작 보존 " + state.paths[i]);
            Check(state.folder.StartsWith("Assets/__LevelCommonVerification_", StringComparison.Ordinal) && AssetDatabase.DeleteAsset(state.folder), "검증 에셋 정리");
            File.WriteAllLines(Evidence + "/restart-results.txt", Results);
            File.Move(Evidence + "/state.json", Evidence + "/completed-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".json");
        }

        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static void VerifyFields(string folder)
        {
            LevelDefinition fields = LevelAssetOperations.CreateAtPath(folder + "/Fields.asset");
            Set(fields, "levelNumber", 61020);
            LevelBoardEditing.Apply(fields, LevelBrush.Fixed, RabbitColor.Type1, new[] { C(2, 0), C(2, 1) });
            LevelObstacleEditing.Apply(fields, new PlacementBrush { Layer = PlacementLayer.Block, Kind = (int)InitialBlockKind.Rocket }, new[] { C(2, 2) });
            LevelObstacleEditing.Apply(fields, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 1 }, new[] { C(2, 0) });
            LevelObstacleEditing.Apply(fields, new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Mold, Durability = 1 }, new[] { C(2, 1) });
            LevelObstacleEditing.Apply(fields, new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 1 }, new[] { C(2, 2) });
            foreach ((ObstacleKind kind, int column) in new[] { (ObstacleKind.Crate, 0), (ObstacleKind.Scrap, 1), (ObstacleKind.Safe, 2), (ObstacleKind.ColorLock, 3) })
                LevelObstacleEditing.Apply(fields, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)kind, Durability = 1, Color = RabbitColor.Type1 }, new[] { C(4, column) });
            LevelObstacleEditing.Apply(fields, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Generator, RequiredCharge = 3 }, new[] { C(6, 0) });
            LevelObstacleEditing.Apply(fields, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Appliance, Durability = 1 }, new[] { C(6, 4) });
            foreach ((PlacementLayer layer, int row, int column, string field, int value) in new[]
            {
                (PlacementLayer.Block, 2, 0, "fixedColor", 4), (PlacementLayer.Block, 2, 2, "rocketDirection", 1),
                (PlacementLayer.Cover, 2, 0, "durability", 3), (PlacementLayer.Dust, 2, 2, "durability", 3),
                (PlacementLayer.Obstacle, 4, 0, "durability", 6), (PlacementLayer.Obstacle, 4, 1, "durability", 5),
                (PlacementLayer.Obstacle, 4, 2, "durability", 5), (PlacementLayer.Obstacle, 4, 3, "color", 4),
                (PlacementLayer.Obstacle, 6, 0, "requiredCharge", 5), (PlacementLayer.Obstacle, 6, 4, "durability", 9)
            })
            {
                Check(LevelCommonEditing.TrySelect(fields, layer, C(row, column), out PlacementSelection target) &&
                    LevelCommonEditing.Set(fields, new[] { target }, field, value, out _) == null && LevelCommonEditing.Read(fields, target, field) == value,
                    "종류별 설정 " + layer + "/" + field + "/" + value);
            }
            LevelCommonEditing.TrySelect(fields, PlacementLayer.Cover, C(2, 1), out PlacementSelection mold);
            string before = JsonUtility.ToJson(fields);
            Check(LevelCommonEditing.Set(fields, new[] { mold }, "durability", 2, out _) != null && JsonUtility.ToJson(fields) == before, "곰팡이 고정 내구도 유지");
            foreach (string value in new[] { "null", "\"1.5\"", "\"\"", "\"999999999999\"" })
            {
                string bad = "{\"type\":\"MatchPlacementSettings\",\"version\":1,\"layer\":\"Cover\",\"kind\":\"1\",\"values\":[{\"field\":\"durability\",\"value\":" + value + "}]}";
                Check(LevelCommonEditing.Paste(fields, new[] { mold }, bad, out _, out _) != null && JsonUtility.ToJson(fields) == before, "누락/소수/범위 초과 클립보드 수치 거절 " + value);
            }
            Check(LevelCommonEditing.Paste(fields, new[] { mold }, "{\"type\":\"MatchSupplyList\",\"version\":1,\"items\":[]}", out _, out _) != null &&
                LevelSupplyEditing.Paste(fields, new[] { 0 }, LevelCommonEditing.Copy(fields, mold), false)?.Contains("클립보드") == true && JsonUtility.ToJson(fields) == before,
                "일반 설정·공급 목록 클립보드 구분");
            LevelCommonEditing.TrySelect(fields, PlacementLayer.Block, C(2, 0), out PlacementSelection colorTarget);
            string copied = LevelCommonEditing.Copy(fields, colorTarget);
            using (SerializedObject data = new SerializedObject(fields)) { data.FindProperty("colors").arraySize = 3; data.ApplyModifiedProperties(); }
            before = JsonUtility.ToJson(fields);
            Check(LevelCommonEditing.Paste(fields, new[] { colorTarget }, copied, out _, out _) != null && JsonUtility.ToJson(fields) == before, "붙여넣기 대상 레벨 미사용 색 거절");
            Check(LevelCommonEditing.Set(fields, new[] { colorTarget }, "coordinate.row", 0, out _) != null && JsonUtility.ToJson(fields) == before, "허용하지 않은 속성 변경 거절");
        }
        private static void Set(LevelDefinition level, string path, int value)
        { using SerializedObject data = new SerializedObject(level); data.FindProperty(path).intValue = value; data.ApplyModifiedProperties(); }
        private static void Check(bool passed, string message)
        {
            Results.Add((passed ? "PASS " : "FAIL ") + message);
            if (!passed) throw new InvalidOperationException(message);
        }
    }
}
