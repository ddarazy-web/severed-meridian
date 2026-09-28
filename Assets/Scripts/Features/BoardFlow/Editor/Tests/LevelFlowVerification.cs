using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class LevelFlowVerification
    {
        private const string Evidence = "Logs/LevelFlowVerification";
        private const string StateFile = Evidence + "/state.json";
        private const string Fixtures = "Assets/Scripts/Features/Levels/Editor/Tests/Fixtures";
        private static readonly List<string> Results = new List<string>();
        private static string folder;
        private static LevelDefinition level;

        [Serializable]
        private sealed class State
        {
            public string folder;
            public string[] paths;
            public string[] json;
            public string[] guids;
            public int processId;
        }

        // 구버전의 저장 필드만 비교한다. 새 ID/흐름 추가가 기존 오류 값을 숨기지 않게 한다.
        [Serializable]
        private sealed class LegacyObstacle
        {
            public BoardCoordinate coordinate = default;
            public int kind = 0;
            public int durability = 0;
            public int color = 0;
            public int requiredCharge = 0;
        }

        [Serializable]
        private sealed class LegacySnapshot
        {
            public int schemaVersion = 0;
            public int levelNumber = 0;
            public int moveCount = 0;
            public int[] colors = null;
            public BoardDefinition board = null;
            public InitialBlockDefinition[] initialBlocks = null;
            public LegacyObstacle[] obstacles = null;
            public CoverPlacementDefinition[] covers = null;
            public DustPlacementDefinition[] dust = null;
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
                Cleanup(folder);
                throw;
            }
        }

        private static void Setup()
        {
            Directory.CreateDirectory(Evidence);
            Results.Clear();
            if (File.Exists(StateFile)) throw new InvalidOperationException("이전 실행의 Restart를 먼저 완료하세요.");
            string name = "__LevelFlowVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", name);
            folder = "Assets/" + name;
            File.WriteAllText(Evidence + "/owned-folder.txt", folder);
            level = CreateFlowLevel(folder + "/Flow.asset");
            SetInt(level, "levelNumber", 54001);
        }

        private static void VerifyData()
        {
            VerifyLegacy();
            Check(level.SchemaVersion == LevelDefinition.CurrentSchemaVersion && !level.Flow.HasRecords && level.Connections.Count == 0, "현재 버전 레벨·기본 흐름 설정");
            Check(LevelFlowRules.Next(level, C(0, 0), out BoardCoordinate next) && next.Equals(C(1, 0)), "미설정 아래 중력");
            foreach (GravityDirection direction in Enum.GetValues(typeof(GravityDirection)))
            {
                Ok(LevelFlowEditing.SetGravity(level, new[] { C(4, 4), C(4, 5) }, direction), "구역 " + direction);
                Check(level.Flow.Gravity.Count == 2 && LevelFlowRules.GravityAt(level, C(4, 5)) == direction, "구역 값 " + direction);
            }
            Ok(LevelFlowEditing.SetGravity(level, new[] { C(4, 4), C(4, 5) }, null), "중력 초기화");
            Check(level.Flow.Gravity.Count == 0, "중력 예외 제거");
            Ok(LevelFlowEditing.SetPath(level, new[] { C(3, 3), C(2, 3), C(2, 4) }), "위쪽으로 꺾이는 경로");
            Check(!LevelFlowRules.Next(level, C(2, 4), out _), "명시 끝 칸에서 멈춤");
            string snapshot = JsonUtility.ToJson(level);
            Check(LevelFlowEditing.SetPath(level, new[] { C(3, 3), C(3, 4) }) != null && JsonUtility.ToJson(level) == snapshot, "기존 경로 분기·덮어쓰기 거절");
            Check(LevelFlowEditing.SetPath(level, new[] { C(0, 0), C(0, 2) }) != null, "비인접 직접 경로 거절");
            Ok(LevelFlowEditing.SetPath(level, new[] { C(1, 4), C(2, 4) }), "기존 경로 합류");
            List<BoardCoordinate> sources = LevelFlowRules.Sources(level, C(2, 4));
            Check(sources.Count == 2 && Has(LevelValidationCode.InvalidMerge), "합류 우선순위 미지정 오류");
            sources.Reverse();
            Ok(LevelFlowEditing.SetMerge(level, C(2, 4), sources), "유입 순서 지정");
            Check(level.Flow.Merges[0].Sources.SequenceEqual(sources), "합류 순서 저장");
            Ok(LevelFlowEditing.RemovePath(level, C(1, 4)), "경로 칸 삭제·중력 복귀");
            Check(LevelFlowRules.Next(level, C(1, 4), out next) && next.Equals(C(2, 4)), "복귀 후 아래 흐름");
            Ok(LevelFlowEditing.SetGravity(level, new[] { C(1, 4) }, GravityDirection.Left), "합류 공급원 변경");
            Check(Has(LevelValidationCode.InvalidMerge), "합류 후보 변경 시 재설정 요구");
            ResetFlow();
            Ok(LevelFlowEditing.SetGravity(level, new[] { C(1, 0) }, GravityDirection.Up), "마주 보는 중력 설정");
            Check(Has(LevelValidationCode.FlowCycle), "두 중력 순환 검출");
            Place(ObstacleKind.Crate, C(1, 0));
            Ok(LevelFlowEditing.SetArrival(level, C(1, 0), false), "장애물 아래 도착 바닥");
            Check(Has(LevelValidationCode.FlowCycle), "장애물·도착 바닥이 순환을 숨기지 않음");
            ClearPlacements(); ResetFlow();
            BoardEdge wall = new BoardEdge(C(0, 0), C(1, 0));
            Ok(LevelFlowEditing.SetWalls(level, new[] { wall, new BoardEdge(wall.B, wall.A) }, false), "벽 양방향 중복 방지");
            Check(level.Flow.Walls.Count == 1 && !LevelFlowRules.Next(level, C(0, 0), out _), "벽에서 기본 흐름 정지");
            Check(LevelFlowEditing.SetPath(level, new[] { wall.A, wall.B }) != null, "벽 통과 경로 거절");
            Check(LevelObstacleEditing.Apply(level, Obstacle(ObstacleKind.Appliance), new[] { C(0, 0) }).Changed == 0, "벽 위 2×2 배치 거절");
            Ok(LevelFlowEditing.SetWalls(level, new[] { wall }, true), "벽 삭제");
            Place(ObstacleKind.Appliance, C(0, 0));
            Check(LevelFlowEditing.SetWalls(level, new[] { wall }, false) != null, "본체 내부 벽 거절");
            Ok(LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(1, 0), C(2, 0)) }, false), "본체 외곽 벽 허용");
            ClearPlacements(); ResetFlow();
            Ok(LevelFlowEditing.SetPortal(level, C(0, 0), null), "미완성 통로 저장");
            Check(Has(LevelValidationCode.InvalidPortal) && !LevelFlowRules.Next(level, C(0, 0), out _), "미완성 입구 대기·오류");
            Check(LevelFlowEditing.SetPortal(level, C(0, 0), C(0, 0)) != null, "통로 자기 연결 거절");
            Ok(LevelFlowEditing.SetPortal(level, C(0, 0), C(5, 5)), "통로 쌍 완성");
            Check(LevelFlowRules.Next(level, C(0, 0), out next) && next.Equals(C(5, 5)), "통로 우선 연결");
            Check(LevelFlowEditing.SetArrival(level, C(5, 5), false) != null && LevelFlowEditing.SetPortal(level, C(5, 5), C(8, 8)) != null, "도착/통로 역할 중첩 거절");
            Ok(LevelFlowEditing.SetPath(level, new[] { C(5, 5), C(5, 6) }), "통로 출구 이후 직접 경로");
            Check(LevelFlowRules.Sources(level, C(5, 5)).Count == 2, "통로·중력 혼합 합류 후보");
            Ok(LevelFlowEditing.SetPortal(level, C(1, 0), C(0, 1)), "두 번째 통로");
            Ok(LevelFlowEditing.SetPath(level, new[] { C(0, 1), C(0, 0) }), "통로 경로 연결");
            Ok(LevelFlowEditing.SetPortal(level, C(5, 6), C(1, 0)), "역할 중복은 거절되어야 함", expectError: true);
            ResetFlow();
            Ok(LevelFlowEditing.SetPortal(level, C(2, 0), C(0, 0)), "통로로 위쪽 복귀");
            Check(Has(LevelValidationCode.FlowCycle), "중력·통로 혼합 순환 검출");
            ResetFlow();
            VerifyConnections();
            VerifyMoreData();
        }

        private static void VerifyConnections()
        {
            Place(ObstacleKind.Generator, C(0, 0));
            Place(ObstacleKind.Crate, C(0, 5));
            string generator = level.Obstacles[0].Id, target = level.Obstacles[1].Id;
            Check(!string.IsNullOrEmpty(generator) && generator != target, "신규 장애물 고유 ID");
            Check(Has(LevelValidationCode.InvalidConnection), "대상 없는 발전기 오류");
            Ok(LevelConnectionEditing.Add(level, generator, target), "발전기 대상 연결");
            Check(Has(LevelValidationCode.InvalidWire), "미완성 전선 오류");
            List<BoardCoordinate> wire = Enumerable.Range(2, 4).Select(column => C(0, column)).ToList();
            Ok(LevelConnectionEditing.SetWire(level, 0, wire), "외곽 전선 지정");
            Check(!Has(LevelValidationCode.InvalidWire), "정상 전선 통과");
            Check(LevelConnectionEditing.Add(level, generator, target) != null, "중복 대상 연결 거절");
            Place(ObstacleKind.Scrap, C(3, 3));
            Check(LevelConnectionEditing.Add(level, generator, level.Obstacles[2].Id) != null, "고철 뭉치 연결 거절");
            Place(ObstacleKind.Safe, C(0, 8));
            Place(ObstacleKind.ColorLock, C(5, 5));
            Place(ObstacleKind.Crate, C(8, 8));
            Ok(LevelConnectionEditing.Add(level, generator, level.Obstacles[3].Id), "두 번째 대상");
            Ok(LevelConnectionEditing.Add(level, generator, level.Obstacles[4].Id), "세 번째 대상");
            Check(LevelConnectionEditing.Add(level, generator, level.Obstacles[5].Id) != null, "네 번째 대상 거절");
            Check(LevelConnectionEditing.SetWire(level, 1, Enumerable.Range(2, 7).Select(column => C(0, column)).ToList()) != null, "전선 공통 단자·선분 중복 거절");
            string before = JsonUtility.ToJson(level);
            Check(LevelObstacleEditing.Move(level, 0, C(1, 0), out _), "발전기 이동");
            Check(level.Connections[0].GeneratorId == generator && level.Connections[0].Vertices.SequenceEqual(wire) && Has(LevelValidationCode.InvalidWire), "이동 후 대상·원래 전선 보존·재지정 오류");
            Undo.PerformUndo();
            Check(JsonUtility.ToJson(level) == before, "본체 이동 Undo 전체 복구");
            using (SerializedObject data = new SerializedObject(level))
            {
                data.FindProperty("obstacles").MoveArrayElement(0, 3);
                LevelObstacleEditing.Commit(data, "검증 목록 재정렬");
            }
            Check(LevelConnectionRules.Find(level, generator) == 3 && LevelConnectionRules.TargetError(level, generator, target, 0) == null, "목록 재정렬 후 ID 대상 유지");
            Undo.PerformUndo();
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Erase = true }, new[] { C(0, 5) });
            Check(level.Connections.Count == 2 && level.Obstacles.Any(item => item.Id == generator), "대상 삭제 시 해당 연결만 제거");
            Undo.PerformUndo();
            Check(JsonUtility.ToJson(level) == before, "대상·전선 한 번 Undo 복구");
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Erase = true }, new[] { C(0, 0) });
            Check(level.Connections.Count == 0 && level.Obstacles.Any(item => item.Id == target), "발전기 삭제 시 전선 제거·대상 보존");
            Undo.PerformUndo();
            Check(JsonUtility.ToJson(level) == before, "발전기·전체 연결 Undo 복구");
            Undo.PerformRedo();
            Check(level.Connections.Count == 0, "발전기 삭제 Redo");
            Undo.PerformUndo();
        }

        private static void VerifyLegacy()
        {
            foreach (int version in new[] { 1, 2 })
                foreach (string suffix in new[] { "Normal", "Invalid" })
                {
                    string path = folder + $"/Version{version}{suffix}.asset";
                    File.Copy(Fixtures + $"/Version{version}{suffix}.txt", path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    LevelDefinition legacy = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                    string before = JsonUtility.ToJson(legacy), disk = File.ReadAllText(path), guid = AssetDatabase.AssetPathToGUID(path);
                    if (version == 2)
                        Check(JsonUtility.ToJson(JsonUtility.FromJson<LegacySnapshot>(before)) ==
                            JsonUtility.ToJson(JsonUtility.FromJson<LegacySnapshot>(File.ReadAllText(Fixtures + $"/Version2{suffix}.json"))),
                            "v2 이전 코드가 기록한 전체 필드 스냅샷 일치 " + suffix);
                    LevelDefinitionValidator.Validate(legacy);
                    Check(before == JsonUtility.ToJson(legacy) && !EditorUtility.IsDirty(legacy) && disk == File.ReadAllText(path), $"v{version} {suffix} 읽기 비파괴");
                    Check(LevelSchemaUpgrade.Upgrade(legacy, out _), $"v{version} {suffix} 명시 전환");
                    LegacySnapshot oldData = JsonUtility.FromJson<LegacySnapshot>(before);
                    LegacySnapshot newData = JsonUtility.FromJson<LegacySnapshot>(JsonUtility.ToJson(legacy));
                    newData.schemaVersion = oldData.schemaVersion;
                    Check(JsonUtility.ToJson(oldData) == JsonUtility.ToJson(newData) && AssetDatabase.AssetPathToGUID(path) == guid && disk == File.ReadAllText(path), $"v{version} {suffix} 기존 필드·오류·GUID·미저장 보존");
                    Check(legacy.SchemaVersion == LevelDefinition.CurrentSchemaVersion && !legacy.Flow.HasRecords && legacy.Obstacles.All(item => !string.IsNullOrEmpty(item.Id)) && legacy.Obstacles.Select(item => item.Id).Distinct().Count() == legacy.Obstacles.Count, $"v{version} {suffix} 기본 흐름·ID 전환");
                    string upgraded = JsonUtility.ToJson(legacy);
                    Undo.PerformUndo();
                    Check(JsonUtility.ToJson(legacy) == before, $"v{version} {suffix} 전환 한 번 Undo");
                    Undo.PerformRedo();
                    Check(JsonUtility.ToJson(legacy) == upgraded, $"v{version} {suffix} Redo ID 유지");
                }
        }

        private static BoardCoordinate C(int row, int column) => new BoardCoordinate(row, column);
        private static LevelDefinition CreateFlowLevel(string path)
        {
            LevelDefinition created = LevelAssetOperations.CreateAtPath(path);
            // 흐름 회귀에서는 공급을 비워 기존 도착 위치를 유지하고 유효한 단일 미션을 명시한다.
            using SerializedObject data = new SerializedObject(created);
            data.FindProperty("supply.sources").arraySize = 0;
            data.ApplyModifiedProperties();
            LevelMissionEditing.Add(created);
            return created;
        }
        private static PlacementBrush Obstacle(ObstacleKind kind) => new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)kind, Durability = 1, RequiredCharge = 3 };
        private static void Place(ObstacleKind kind, BoardCoordinate cell) => Check(LevelObstacleEditing.Apply(level, Obstacle(kind), new[] { cell }).Changed == 1, "검증 본체 배치 " + kind);
        private static bool Has(LevelValidationCode code) => LevelDefinitionValidator.Validate(level).Any(issue => issue.Code == code);
        private static void Ok(string error, string label, bool expectError = false) => Check(expectError ? error != null : error == null, label + (error == null ? "" : " / " + error));
        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
            Results.Add("PASS " + label);
            File.WriteAllLines(Evidence + "/progress.txt", Results);
        }
        private static void SetInt(LevelDefinition target, string path, int value)
        {
            using SerializedObject data = new SerializedObject(target);
            data.FindProperty(path).intValue = value;
            LevelObstacleEditing.Commit(data, "검증 값 변경");
        }
        private static void ResetFlow()
        {
            using SerializedObject data = new SerializedObject(level);
            foreach (string list in new[] { "gravity", "paths", "merges", "walls", "portals", "arrivals" }) data.FindProperty("flow." + list).arraySize = 0;
            LevelObstacleEditing.Commit(data, "검증 흐름 초기화");
        }
        private static void ClearPlacements()
        {
            using SerializedObject data = new SerializedObject(level);
            data.FindProperty("obstacles").arraySize = 0;
            data.FindProperty("connections").arraySize = 0;
            LevelObstacleEditing.Commit(data, "검증 본체 초기화");
        }
        private static void SaveState()
        {
            AssetDatabase.SaveAssets();
            string[] paths = AssetDatabase.FindAssets("t:LevelDefinition", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path).ToArray();
            State state = new State { folder = folder, paths = paths,
                json = paths.Select(path => JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<LevelDefinition>(path))).ToArray(),
                guids = paths.Select(AssetDatabase.AssetPathToGUID).ToArray(), processId = System.Diagnostics.Process.GetCurrentProcess().Id };
            File.WriteAllText(StateFile, JsonUtility.ToJson(state, true));
        }
        public static void Restart()
        {
            Results.Clear();
            State state = JsonUtility.FromJson<State>(File.ReadAllText(StateFile));
            Check(state.processId != System.Diagnostics.Process.GetCurrentProcess().Id, "별도 Editor 프로세스");
            for (int i = 0; i < state.paths.Length; i++)
            {
                LevelDefinition saved = AssetDatabase.LoadAssetAtPath<LevelDefinition>(state.paths[i]);
                Check(saved != null && JsonUtility.ToJson(saved) == state.json[i] && AssetDatabase.AssetPathToGUID(state.paths[i]) == state.guids[i], "재시작 전체 값·GUID " + Path.GetFileName(state.paths[i]));
                if (state.paths[i].EndsWith("SavedFlow.asset"))
                    Check(LevelDefinitionValidator.Validate(saved).Count == 0, "재시작 정상 흐름 구조 검사");
                if (state.paths[i].EndsWith("PartialWire.asset"))
                    Check(LevelDefinitionValidator.Validate(saved).Any(issue => issue.Code == LevelValidationCode.InvalidWire) && saved.Connections[0].Vertices.Count == 2,
                        "재시작 전선 중간 경로·미완성 오류 유지");
                if (state.paths[i].EndsWith("InvalidFlow.asset"))
                    Check(new[] { LevelValidationCode.InvalidFlow, LevelValidationCode.InvalidPortal, LevelValidationCode.InvalidArrival, LevelValidationCode.InvalidWall, LevelValidationCode.InvalidWire }
                        .All(code => LevelDefinitionValidator.Validate(saved).Any(issue => issue.Code == code)), "재시작 복합 오류 분류 유지");
            }
            Cleanup(state.folder);
            Check(!AssetDatabase.IsValidFolder(state.folder), "임시 에셋 정리");
            File.WriteAllLines(Evidence + "/restart-results.txt", Results);
            File.Move(StateFile, Evidence + "/completed-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".json");
        }
        private static void Cleanup(string owned)
        {
            if (string.IsNullOrEmpty(owned) || !owned.StartsWith("Assets/__LevelFlowVerification_", StringComparison.Ordinal) || owned.Contains("..") || owned.Substring("Assets/".Length).Contains("/"))
                throw new InvalidOperationException("검증 폴더 소유권을 확인할 수 없습니다.");
            if (AssetDatabase.IsValidFolder(owned)) AssetDatabase.DeleteAsset(owned);
        }
    }
}
