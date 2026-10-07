using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class LevelObstacleVerification
    {
        private const string Evidence = "Logs/LevelObstacleVerification";
        private const string StateFile = Evidence + "/state.json";
        private const string Fixtures = "Tests/Editor/Features/Levels/Fixtures";
        private static readonly List<string> Results = new List<string>();
        private static string assetFolder;
        private static LevelDefinition level;
        private static LevelDefinition legacyNormal;
        private static LevelDefinition legacyInvalid;

        [Serializable]
        private sealed class SavedState
        {
            public string folder;
            public string[] paths;
            public string[] json;
            public string[] guids;
            public int processId;
        }

        [Serializable]
        private sealed class LegacyBlock
        {
            public BoardCoordinate coordinate = default;
            public int kind = 0;
            public int fixedColor = 0;
        }

        [Serializable]
        private sealed class LegacyData
        {
            public int schemaVersion = 0;
            public int levelNumber = 0;
            public int moveCount = 0;
            public int[] colors = null;
            public BoardDefinition board = null;
            public LegacyBlock[] initialBlocks = null;
        }

        public static void Prepare()
        {
            try
            {
                Setup();
                VerifyData();
                SaveState();
                File.WriteAllLines(Evidence + "/data-results.txt", Results);
            }
            catch (Exception exception)
            {
                Results.Add("FAIL " + exception);
                File.WriteAllLines(Evidence + "/data-results.txt", Results);
                Cleanup(assetFolder);
                throw;
            }
        }

        private static void Setup()
        {
            Results.Clear();
            Directory.CreateDirectory(Evidence);
            if (File.Exists(StateFile)) throw new InvalidOperationException("이전 상태가 남아 있습니다. Restart부터 실행하세요.");
            string name = "__LevelObstacleVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", name);
            assetFolder = "Assets/" + name;
            File.WriteAllText(Evidence + "/owned-folder.txt", assetFolder);
            level = LevelAssetOperations.CreateAtPath(assetFolder + "/Board.asset");
            LevelMissionEditing.Add(level);
            SetInt(level, "levelNumber", 53001);
            foreach (string suffix in new[] { "Normal", "Invalid" })
            {
                string path = assetFolder + "/Legacy" + suffix + ".asset";
                File.Copy(Fixtures + "/Version1" + suffix + ".txt", path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
            legacyNormal = AssetDatabase.LoadAssetAtPath<LevelDefinition>(assetFolder + "/LegacyNormal.asset");
            legacyInvalid = AssetDatabase.LoadAssetAtPath<LevelDefinition>(assetFolder + "/LegacyInvalid.asset");
        }

        private static void VerifyData()
        {
            foreach (LevelDefinition legacy in new[] { legacyNormal, legacyInvalid })
            {
                string suffix = legacy == legacyNormal ? "Normal" : "Invalid";
                string expected = File.ReadAllText(Fixtures + "/Version1" + suffix + ".json");
                string loaded = JsonUtility.ToJson(legacy);
                string disk = File.ReadAllText(AssetDatabase.GetAssetPath(legacy));
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(legacy));
                List<LevelValidationIssue> beforeIssues = LevelDefinitionValidator.Validate(legacy);
                Check(legacy.SchemaVersion == 1 && JsonUtility.ToJson(JsonUtility.FromJson<LegacyData>(loaded)) == expected,
                    "실제 버전 1 직렬화 에셋의 기존 필드 전체 읽기: " + suffix);
                Check(!EditorUtility.IsDirty(legacy) && JsonUtility.ToJson(legacy) == loaded &&
                    File.ReadAllText(AssetDatabase.GetAssetPath(legacy)) == disk, "버전 1 읽기·검사 무변경: " + suffix);
                Check(!LevelBoardEditing.CanEdit(legacy), "버전 1 보드 편집 잠금: " + suffix);
                Check(LevelSchemaUpgrade.Upgrade(legacy, out _) && legacy.SchemaVersion == LevelDefinition.CurrentSchemaVersion, "명시적 전환: " + suffix);
                LegacyData converted = JsonUtility.FromJson<LegacyData>(JsonUtility.ToJson(legacy));
                converted.schemaVersion = 1;
                Check(JsonUtility.ToJson(converted) == expected && legacy.Obstacles.Count == 0 && legacy.Covers.Count == 0 && legacy.Dust.Count == 0,
                    "전환 후 기존 정상·오류 필드 및 비어 있는 신규 목록: " + suffix);
                Check(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(legacy)) == guid && File.ReadAllText(AssetDatabase.GetAssetPath(legacy)) == disk,
                    "전환은 GUID 보존·자동 저장 없음: " + suffix);
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                Check(JsonUtility.ToJson(legacy) == loaded, "형식 전환 Undo: " + suffix);
                Undo.PerformRedo();
                List<LevelValidationIssue> convertedIssues = LevelDefinitionValidator.Validate(legacy);
                Check(convertedIssues.Count(issue => issue.Code == LevelValidationCode.InvalidMission) == 1 && legacy.Missions.Count == 0,
                    "구버전 미션 자동 추측 없음·미설정 오류: " + suffix);
                Check(legacy.SchemaVersion == LevelDefinition.CurrentSchemaVersion && convertedIssues.Where(issue => issue.Code != LevelValidationCode.InvalidMission).Select(issue => issue.Code).SequenceEqual(beforeIssues.Select(issue => issue.Code)),
                    "형식 전환 Redo·기존 오류 분류 유지: " + suffix);
            }
            Check(level.InitialBlocks.Count == 0 && level.SchemaVersion == LevelDefinition.CurrentSchemaVersion, "전환은 다른 에셋에 전파되지 않음");
            SetInt(level, "schemaVersion", 99);
            string unsupported = JsonUtility.ToJson(level);
            Check(!LevelSchemaUpgrade.Upgrade(level, out _) && JsonUtility.ToJson(level) == unsupported, "미지원 버전 자동 전환 거절");
            SetInt(level, "schemaVersion", LevelDefinition.CurrentSchemaVersion);

            foreach (ObstacleKind kind in Enum.GetValues(typeof(ObstacleKind)))
            {
                ResetBoard();
                PlacementBrush brush = Obstacle(kind, kind == ObstacleKind.Generator ? 1 : LevelPlacementRules.MaxDurability(kind));
                bool placed = Place(brush, 0, 0).Changed == 1;
                List<LevelValidationIssue> placementIssues = LevelDefinitionValidator.Validate(level);
                Check(placed && (kind == ObstacleKind.Generator
                    ? placementIssues.Count == 1 && placementIssues[0].Code == LevelValidationCode.InvalidConnection
                    : placementIssues.Count == 0), "최대값 장애물 배치·검사 (발전기는 미연결 오류만): " + kind);
                Check(level.Obstacles.Count == 1 && LevelPlacementRules.Find(level, PlacementLayer.Obstacle,
                    new BoardCoordinate(brush.Size - 1, brush.Size - 1)) == 0, "본체 한 기록·영역 조회: " + kind);
                string original = JsonUtility.ToJson(level);
                brush.Kind = ((int)kind + 1) % 6;
                Check(Place(brush, 0, 0).Changed == 0 && JsonUtility.ToJson(level) == original, "다른 종류 묵시적 교체 거절: " + kind);
                PlacementBrush invalid = Obstacle(kind, 0);
                invalid.RequiredCharge = 2;
                Check(Place(invalid, 5, 5).Changed == 0, "범위 밖 속성 배치 거절: " + kind);
                if (kind == ObstacleKind.Generator) SetInt(level, "obstacles.Array.data[0].requiredCharge", 6);
                else SetInt(level, "obstacles.Array.data[0].durability", LevelPlacementRules.MaxDurability(kind) + 1);
                Check(LevelDefinitionValidator.Validate(level).Any(issue => issue.Code == LevelValidationCode.InvalidPlacementValue), "Inspector 잘못된 속성 검사: " + kind);
            }
            ResetBoard();
            foreach (InitialBlockKind kind in Enum.GetValues(typeof(InitialBlockKind)))
            {
                PlacementBrush brush = new PlacementBrush { Layer = PlacementLayer.Block, Kind = (int)kind, Color = RabbitColor.Type3, Direction = RocketDirection.Vertical };
                Check(Place(brush, 0, (int)kind).Changed == 1, "일반/파워 초기 배치: " + kind);
            }
            Check(level.InitialBlocks.All(block => LevelPlacementRules.IsNormal(block.Kind) || block.FixedColor == null) &&
                level.InitialBlocks[2].RocketDirection == RocketDirection.Vertical, "파워에 달토끼 색 의미 없음·로켓 방향 보존");
            string powerBefore = JsonUtility.ToJson(level);
            Check(Place(new PlacementBrush { Layer = PlacementLayer.Block, Kind = (int)InitialBlockKind.FixedNormal }, 0, 2).Changed == 0 &&
                JsonUtility.ToJson(level) == powerBefore, "일반 블록으로 파워 묵시적 교체 금지");
            Check(LevelBoardEditing.Apply(level, LevelBrush.Fixed, RabbitColor.Type1, new[] { new BoardCoordinate(0, 2) }) == 0,
                "기존 일반 블록 편집 API도 파워 교체 금지");
            SetInt(level, "initialBlocks.Array.data[2].rocketDirection", 99);
            Check(LevelDefinitionValidator.Validate(level).Any(issue => issue.PropertyPath.EndsWith("rocketDirection")), "잘못된 로켓 방향 검사");
            SetInt(level, "initialBlocks.Array.data[2].rocketDirection", 1);
            Place(new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 3 }, 0, 2);
            Place(new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Mold, Durability = 1 }, 0, 2);
            string under = JsonUtility.ToJson(level.InitialBlocks[2]);
            Check(Place(new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 2 }, 0, 2).Changed == 0,
                "곰팡이 위 거미줄 거절");
            Check(Erase(PlacementLayer.Cover, 0, 2).Changed == 1 && JsonUtility.ToJson(level.InitialBlocks[2]) == under && level.Dust.Count == 1,
                "곰팡이 삭제는 내부 파워·방향·먼지 보존");
            Place(new PlacementBrush { Layer = PlacementLayer.Cover, Kind = (int)CoverKind.Web, Durability = 3 }, 0, 1);
            Check(Erase(PlacementLayer.Block, 0, 1).Changed == 1 && level.Covers.Count == 1 &&
                LevelDefinitionValidator.Validate(level).Any(issue => issue.Code == LevelValidationCode.InvalidCover), "내부 블록만 지우면 덮개 보존·오류");
            Place(new PlacementBrush { Layer = PlacementLayer.Block, Kind = (int)InitialBlockKind.RandomNormal }, 0, 1);
            Check(LevelDefinitionValidator.Validate(level).Count == 0, "내부 무작위 블록 복원으로 덮개 오류 해소");
            Check(Place(Obstacle(ObstacleKind.Crate, 1), 0, 1).Changed == 0, "블록·덮개 자리 장애물 설치 금지");
            Place(Obstacle(ObstacleKind.Scrap, 1), 3, 3);
            Check(Place(new PlacementBrush { Layer = PlacementLayer.Cover, Kind = 0, Durability = 1 }, 3, 3).Changed == 0 &&
                LevelBoardEditing.Apply(level, LevelBrush.Random, RabbitColor.Type1, new[] { new BoardCoordinate(3, 3) }) == 0,
                "고철 위 덮개 및 기존 일반 칠하기 API 충돌 거절");

            VerifyLargeAndPreservation();
            ResetBoard();
            Place(Obstacle(ObstacleKind.Crate, 1), 0, 0);
            SetInt(level, "schemaVersion", 1);
            string unexpected = JsonUtility.ToJson(level);
            Check(!LevelSchemaUpgrade.Upgrade(level, out _) && JsonUtility.ToJson(level) == unexpected, "버전 1 신규 데이터 전환 거절·원본 보존");
            ResetBoard();
            VerifyCorruptLayers();
            ResetBoard();
        }

        private static void VerifyCorruptLayers()
        {
            Place(Obstacle(ObstacleKind.Appliance, 5), 2, 2);
            using (SerializedObject data = new SerializedObject(level))
            {
                SerializedProperty list = data.FindProperty("obstacles");
                list.arraySize = 2;
                SerializedProperty other = list.GetArrayElementAtIndex(1);
                other.FindPropertyRelative("kind").intValue = (int)ObstacleKind.Crate;
                other.FindPropertyRelative("coordinate.row").intValue = 3;
                other.FindPropertyRelative("coordinate.column").intValue = 3;
                data.ApplyModifiedProperties();
            }
            string before = JsonUtility.ToJson(level);
            Check(Erase(PlacementLayer.Obstacle, 2, 2).Changed == 0 && JsonUtility.ToJson(level) == before,
                "2×2 일부만 중복되어도 다른 점유 칸에서 임의 삭제 금지");
            Check(!LevelObstacleEditing.Move(level, 0, new BoardCoordinate(5, 5), out _) && JsonUtility.ToJson(level) == before,
                "일부 중복 본체 이동 거절·원본 보존");
            ResetBoard();
            Place(new PlacementBrush { Layer = PlacementLayer.Block, Kind = 0 }, 1, 1);
            Place(new PlacementBrush { Layer = PlacementLayer.Cover, Kind = 0, Durability = 1 }, 1, 1);
            Place(new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 1 }, 1, 1);
            using (SerializedObject data = new SerializedObject(level))
            {
                data.FindProperty("covers").arraySize = 2;
                data.FindProperty("dust").arraySize = 2;
                data.ApplyModifiedProperties();
            }
            before = JsonUtility.ToJson(level);
            Check(Erase(PlacementLayer.Cover, 1, 1).Changed == 0 && Erase(PlacementLayer.Dust, 1, 1).Changed == 0 &&
                JsonUtility.ToJson(level) == before, "중복 덮개·먼지 보드 삭제 거절·원본 보존");
            Check(LevelDefinitionValidator.Validate(level).Count(issue => issue.Code == LevelValidationCode.DuplicatePlacement) >= 4,
                "중복 덮개·먼지 각 원본 필드 오류");
        }

        private static void VerifyLargeAndPreservation()
        {
            ResetBoard();
            PlacementBrush big = Obstacle(ObstacleKind.Appliance, 9);
            Check(Place(big, 7, 7).Changed == 1 && level.Obstacles.Count == 1, "8행8열 2×2 설치 성공");
            AssetDatabase.SaveAssetIfDirty(level);
            int noMoveGroup = Undo.GetCurrentGroup();
            Check(!LevelObstacleEditing.Move(level, 0, new BoardCoordinate(7, 7), out _) && !EditorUtility.IsDirty(level) &&
                Undo.GetCurrentGroup() == noMoveGroup, "자기 자리 이동은 dirty·Undo를 만들지 않음");
            Check(Place(big, 8, 0).Changed == 0 && Place(big, 0, 8).Changed == 0, "끝 행/열 2×2 설치 실패");
            foreach (BoardCoordinate cell in LevelPlacementRules.Footprint(new BoardCoordinate(7, 7), 2))
                Place(new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 2 }, cell.Row, cell.Column);
            string before = JsonUtility.ToJson(level);
            Check(LevelObstacleEditing.Move(level, 0, new BoardCoordinate(6, 7), out _) && level.Dust.Count == 4 && level.Obstacles[0].Durability == 9,
                "자기 점유와 겹치는 2×2 이동·먼지와 공유 내구도 보존");
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Check(JsonUtility.ToJson(level) == before, "2×2 이동 한 번 Undo");
            Undo.PerformRedo();
            Check(level.Obstacles[0].Coordinate.Equals(new BoardCoordinate(6, 7)), "2×2 이동 Redo");
            Place(Obstacle(ObstacleKind.Crate, 1), 5, 7);
            before = JsonUtility.ToJson(level);
            Check(!LevelObstacleEditing.Move(level, 0, new BoardCoordinate(5, 7), out _) && JsonUtility.ToJson(level) == before,
                "다른 점유와 겹치는 이동 원자적 취소");
            SetBool(level, "board.cells.Array.data[52].isActive", false);
            Check(!LevelObstacleEditing.Move(level, 0, new BoardCoordinate(5, 7), out _), "비활성 목적 칸 이동 거절");
            Check(!LevelObstacleEditing.Move(level, 0, new BoardCoordinate(8, 8), out _), "보드 밖 이동 거절");
            foreach (BoardCoordinate cell in LevelPlacementRules.Footprint(new BoardCoordinate(6, 7), 2))
            {
                before = JsonUtility.ToJson(level);
                Check(Erase(PlacementLayer.Obstacle, cell.Row, cell.Column).Changed == 1 && level.Obstacles.Count == 1 && level.Dust.Count == 4,
                    "점유 칸에서 본체 전체 삭제·먼지 보존: " + cell);
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Check(JsonUtility.ToJson(level) == before, "본체 삭제 한 번 Undo: " + cell);
            }
            ResetBoard();
            PlacementBrush crate = Obstacle(ObstacleKind.Crate, 2);
            BoardCoordinate[] cells = { new BoardCoordinate(1, 1), new BoardCoordinate(1, 2), new BoardCoordinate(1, 1) };
            before = JsonUtility.ToJson(level);
            Check(LevelObstacleEditing.Apply(level, crate, cells).Changed == 2, "반복 방문 제외·한 드래그 배치");
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Check(JsonUtility.ToJson(level) == before, "여러 칸 배치 한 번 Undo");
            Undo.PerformRedo();
            Check(level.Obstacles.Count == 2, "여러 칸 배치 Redo");
            AssetDatabase.SaveAssetIfDirty(level);
            int group = Undo.GetCurrentGroup();
            Check(LevelObstacleEditing.Apply(level, crate, cells).Changed == 0 && !EditorUtility.IsDirty(level) && Undo.GetCurrentGroup() == group,
                "동일값 배치 dirty·Undo 없음");
            Check(LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Erase = true }, cells).Changed == 2 && level.Obstacles.Count == 0,
                "다중 삭제 인덱스 변화 처리");
            Place(Obstacle(ObstacleKind.ColorLock, 3), 2, 2);
            SetInt(level, "obstacles.Array.data[0].color", 99);
            Check(LevelDefinitionValidator.Validate(level).Any(issue => issue.Code == LevelValidationCode.InvalidPlacementValue), "잘못된 자물쇠 색 검사");
            using (SerializedObject data = new SerializedObject(level))
            {
                SerializedProperty obstacles = data.FindProperty("obstacles");
                obstacles.arraySize = 3;
                obstacles.GetArrayElementAtIndex(2).FindPropertyRelative("coordinate.row").intValue = 20;
                data.ApplyModifiedProperties();
            }
            string[] broken = level.Obstacles.Select(item => JsonUtility.ToJson(item)).ToArray();
            Check(Erase(PlacementLayer.Obstacle, 2, 2).Changed == 0, "중복 본체 보드 삭제 거절");
            Place(new PlacementBrush { Layer = PlacementLayer.Dust, Durability = 1 }, 2, 2);
            Place(new PlacementBrush { Layer = PlacementLayer.Block, Kind = 0 }, 5, 5);
            Check(level.Obstacles.Select(item => JsonUtility.ToJson(item)).SequenceEqual(broken) && level.Obstacles.Count == 3 &&
                LevelDefinitionValidator.Validate(level).Any(issue => issue.Code == LevelValidationCode.CoordinateOutOfRange), "정상 층·칸 편집은 중복·범위 밖 오류 기록 보존");
            // 잘못된 레벨도 재시작 왕복 검사에 남긴다.
            AssetDatabase.SaveAssetIfDirty(level);
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(level), assetFolder + "/InvalidLayers.asset");
            SetInt(AssetDatabase.LoadAssetAtPath<LevelDefinition>(assetFolder + "/InvalidLayers.asset"), "levelNumber", 53002);
        }

        private static PlacementBrush Obstacle(ObstacleKind kind, int durability) => new PlacementBrush
        { Layer = PlacementLayer.Obstacle, Kind = (int)kind, Durability = durability, RequiredCharge = 5, Color = RabbitColor.Type2 };
        private static PlacementEditResult Place(PlacementBrush brush, int row, int column) =>
            LevelObstacleEditing.Apply(level, brush, new[] { new BoardCoordinate(row, column) });
        private static PlacementEditResult Erase(PlacementLayer layer, int row, int column) => Place(new PlacementBrush { Layer = layer, Erase = true }, row, column);

        private static void ResetBoard()
        {
            using SerializedObject data = new SerializedObject(level);
            data.FindProperty("schemaVersion").intValue = LevelDefinition.CurrentSchemaVersion;
            foreach (string field in new[] { "initialBlocks", "obstacles", "covers", "dust" }) data.FindProperty(field).arraySize = 0;
            SerializedProperty cells = data.FindProperty("board.cells");
            for (int i = 0; i < cells.arraySize; i++) cells.GetArrayElementAtIndex(i).FindPropertyRelative("isActive").boolValue = true;
            data.ApplyModifiedProperties();
            Undo.ClearAll();
        }

        private static void SaveState()
        {
            string[] paths = AssetDatabase.FindAssets("t:LevelDefinition", new[] { assetFolder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
            foreach (string path in paths) AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path));
            SavedState state = new SavedState
            {
                folder = assetFolder, paths = paths,
                json = paths.Select(path => JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path))).ToArray(),
                guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray(), processId = System.Diagnostics.Process.GetCurrentProcess().Id
            };
            File.WriteAllText(StateFile, JsonUtility.ToJson(state, true));
        }

        public static void Restart()
        {
            Results.Clear();
            SavedState saved = JsonUtility.FromJson<SavedState>(File.ReadAllText(StateFile));
            Check(saved.processId != System.Diagnostics.Process.GetCurrentProcess().Id, "별도 Editor 프로세스 재시작");
            for (int i = 0; i < saved.paths.Length; i++)
            {
                LevelDefinition loaded = AssetDatabase.LoadAssetAtPath<LevelDefinition>(saved.paths[i]);
                Check(loaded != null && JsonUtility.ToJson(loaded) == saved.json[i] && AssetDatabase.AssetPathToGUID(saved.paths[i]) == saved.guids[i],
                    "재시작 전체 데이터·GUID 보존: " + Path.GetFileName(saved.paths[i]));
                if (saved.paths[i].EndsWith("InvalidLayers.asset"))
                    Check(LevelDefinitionValidator.Validate(loaded).Any(issue => issue.Code == LevelValidationCode.PlacementConflict), "재시작 신규 층 충돌 오류 유지");
            }
            Cleanup(saved.folder);
            Check(!AssetDatabase.IsValidFolder(saved.folder), "임시 검증 에셋 정리");
            File.Move(StateFile, Evidence + "/completed-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".json");
            File.WriteAllLines(Evidence + "/restart-results.txt", Results);
        }

        private static void Cleanup(string folder)
        {
            const string prefix = "Assets/__LevelObstacleVerification_";
            if (folder == null || !folder.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(folder.Substring(prefix.Length), "N", out _))
                throw new InvalidOperationException("검증 폴더 소유권 확인 실패");
            AssetDatabase.DeleteAsset(folder);
        }

        private static void SetInt(LevelDefinition target, string path, int value)
        {
            using SerializedObject data = new SerializedObject(target);
            data.FindProperty(path).intValue = value;
            data.ApplyModifiedProperties();
        }
        private static void SetBool(LevelDefinition target, string path, bool value)
        {
            using SerializedObject data = new SerializedObject(target);
            data.FindProperty(path).boolValue = value;
            data.ApplyModifiedProperties();
        }
        private static void Check(bool condition, string description)
        {
            Results.Add((condition ? "PASS " : "FAIL ") + description);
            File.WriteAllLines(Evidence + "/progress.txt", Results);
            if (!condition) throw new InvalidOperationException(description);
        }
    }
}
