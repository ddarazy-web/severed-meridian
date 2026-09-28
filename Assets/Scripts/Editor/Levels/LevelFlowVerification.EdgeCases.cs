using System;
using System.Linq;
using Board;
using UnityEditor;
using UnityEngine;

namespace Levels.Editor
{
    public static partial class LevelFlowVerification
    {
        private static void VerifyMoreData()
        {
            LevelDefinition original = level;
            level = CreateFlowLevel(folder + "/SavedFlow.asset");
            SetInt(level, "levelNumber", 54004);
            Ok(LevelFlowEditing.SetGravity(level, new[] { C(0, 0) }, GravityDirection.Right), "구역 연결 오른쪽");
            Ok(LevelFlowEditing.SetGravity(level, new[] { C(0, 1) }, GravityDirection.Down), "구역 연결 아래");
            Ok(LevelFlowEditing.SetGravity(level, new[] { C(1, 1) }, GravityDirection.Left), "구역 연결 왼쪽");
            Ok(LevelFlowEditing.SetGravity(level, new[] { C(1, 0) }, GravityDirection.Up), "구역 연결 위");
            Check(Has(LevelValidationCode.FlowCycle), "여러 방향 구역을 합친 순환");
            ResetFlow();
            Ok(LevelFlowEditing.SetPath(level, new[] { C(4, 1), C(4, 2), C(4, 3) }), "저장 검증 직접 경로");
            Ok(LevelFlowEditing.SetPortal(level, C(8, 8), C(4, 3)), "저장 검증 통로");
            foreach (BoardCoordinate destination in LevelFlowRules.Graph(level).Values.Distinct().ToArray())
            {
                System.Collections.Generic.List<BoardCoordinate> sources = LevelFlowRules.Sources(level, destination);
                if (sources.Count > 1) Ok(LevelFlowEditing.SetMerge(level, destination, sources.AsEnumerable().Reverse().ToArray()), "저장 검증 합류 " + destination);
            }
            Ok(LevelFlowEditing.SetArrival(level, C(9, 3), false), "저장 검증 도착");
            Ok(LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(7, 0), C(7, 1)) }, false), "저장 검증 벽");
            Check(!LevelDefinitionValidator.Validate(level).Any(), "완성 흐름 설정 구조 검사 통과");
            string snapshot = JsonUtility.ToJson(level);
            AssetDatabase.SaveAssetIfDirty(level);
            int undo = Undo.GetCurrentGroup();
            Ok(LevelFlowEditing.SetArrival(level, C(9, 3), false), "같은 도착 설정");
            Check(!EditorUtility.IsDirty(level) && Undo.GetCurrentGroup() == undo && JsonUtility.ToJson(level) == snapshot, "무변경 조작 dirty·Undo 없음");
            int count = level.Flow.Merges[0].Sources.Count;
            Check(LevelFlowEditing.SetMerge(level, level.Flow.Merges[0].Coordinate, Enumerable.Repeat(C(4, 1), count).ToArray()) != null, "합류 공급원 중복 거절");
            Check(LevelFlowEditing.SetMerge(level, level.Flow.Merges[0].Coordinate, Array.Empty<BoardCoordinate>()) != null, "합류 공급원 누락 거절");
            Edit(data => data.FindProperty("board.cells.Array.data[43].isActive").boolValue = false);
            Check(Has(LevelValidationCode.InvalidFlow) && Has(LevelValidationCode.InvalidPortal), "칸 비활성화 시 참조 오류");
            Undo.PerformUndo();
            Check(JsonUtility.ToJson(level) == snapshot, "칸 재활성/Undo 시 흐름 참조 보존");
            LevelDefinition savedFlow = level;
            level = CreateFlowLevel(folder + "/InvalidFlow.asset"); SetInt(level, "levelNumber", 54005);
            Place(ObstacleKind.Generator, C(2, 2)); Place(ObstacleKind.Crate, C(2, 7)); Place(ObstacleKind.Appliance, C(6, 1));
            string generator = level.Obstacles[0].Id, target = level.Obstacles[1].Id;
            Ok(LevelConnectionEditing.Add(level, generator, target), "검증 전선 대상");
            BoardCoordinate[] wire = Enumerable.Range(4, 4).Select(column => C(2, column)).ToArray();
            Ok(LevelConnectionEditing.SetWire(level, 0, wire), "검증 수평 전선");
            Check(LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(1, 4), C(2, 4)) }, false) != null, "기존 전선 위 벽 배치 거절");
            Ok(LevelFlowEditing.SetWalls(level, new[] { new BoardEdge(C(2, 4), C(3, 4)) }, false), "다른 경계 벽 배치");
            BoardCoordinate[] wallWire = new[] { C(3, 4), C(3, 5), C(3, 6), C(3, 7) };
            Check(LevelConnectionEditing.SetWire(level, 0, wallWire) != null, "벽과 겹치는 전선 거절");
            Check(LevelConnectionEditing.SetWire(level, 0, new[] { C(2, 4), C(2, 7) }) != null, "전선 비인접 꼭짓점 거절");
            Check(LevelConnectionEditing.SetWire(level, 0, new[] { C(3, 2), C(3, 3), C(3, 4), C(3, 5), C(3, 6), C(3, 7) }) != null, "전선 본체 내부 침범 거절");
            Check(LevelConnectionEditing.SetWire(level, 0, new[] { C(2, 4), C(1, 4), C(2, 4), C(2, 5), C(2, 6), C(2, 7) }) != null, "전선 자기 교차/반복 거절");
            Place(ObstacleKind.Generator, C(6, 6));
            Check(LevelConnectionEditing.Add(level, level.Obstacles[3].Id, target) != null, "다른 발전기의 동일 대상 연결 거절");
            string applianceId = level.Obstacles[2].Id;
            Ok(LevelConnectionEditing.Add(level, generator, applianceId), "대형 폐가전 연결");
            Ok(LevelConnectionEditing.SetWire(level, 1, new[] { C(4, 2), C(5, 2), C(6, 2) }), "대형 폐가전 전선");
            string beforeMove = JsonUtility.ToJson(level);
            Check(LevelObstacleEditing.Move(level, 2, C(7, 1), out _) && level.Connections[1].TargetId == applianceId &&
                LevelConnectionRules.WireError(level, generator, applianceId, level.Connections[1].Vertices, 1) != null, "대상 본체 이동 후 ID 유지·전선 재지정");
            Undo.PerformUndo();
            Check(JsonUtility.ToJson(level) == beforeMove, "연결 대상 이동 Undo 전체 복구");
            Ok(LevelConnectionEditing.Remove(level, 1), "대상 연결 삭제");
            BoardEdge moveWall = new BoardEdge(C(8, 1), C(8, 2));
            Ok(LevelFlowEditing.SetWalls(level, new[] { moveWall }, false), "이동 목적지 벽");
            Check(!LevelObstacleEditing.Move(level, 2, C(8, 1), out _), "2×2 이동 목적 내부 벽 거절");
            Ok(LevelFlowEditing.SetWalls(level, new[] { moveWall }, true), "검증 이동 벽 삭제");
            Edit(data => data.FindProperty("obstacles.Array.data[1].id").stringValue = generator);
            Check(Has(LevelValidationCode.InvalidObstacleId) && Has(LevelValidationCode.InvalidConnection), "외부 ID 중복과 끊긴 참조 검사");
            Undo.PerformUndo();
            Edit(data => data.FindProperty("obstacles.Array.data[1].id").stringValue = "");
            Check(Has(LevelValidationCode.InvalidObstacleId), "외부 ID 누락 검사");
            Undo.PerformUndo();
            string before = JsonUtility.ToJson(level);
            SetInt(level, "schemaVersion", 2);
            string legacyUnexpected = JsonUtility.ToJson(level);
            Check(!LevelSchemaUpgrade.Upgrade(level, out _) && JsonUtility.ToJson(level) == legacyUnexpected, "구버전의 예상 밖 신규 ID/흐름 전환 거절");
            Check(Has(LevelValidationCode.UnexpectedLegacyData), "구버전 신규 데이터 검사");
            Undo.PerformUndo();
            Check(JsonUtility.ToJson(level) == before, "버전 원복 시 기존 연결 보존");
            SetInt(level, "schemaVersion", 99);
            string future = JsonUtility.ToJson(level);
            Check(!LevelSchemaUpgrade.Upgrade(level, out _) && JsonUtility.ToJson(level) == future && Has(LevelValidationCode.UnsupportedSchemaVersion), "미지원 미래 버전 전환 거절");
            Undo.PerformUndo();
            Edit(data =>
            {
                SerializedProperty paths = data.FindProperty("flow.paths"); paths.arraySize = 2;
                foreach (int index in new[] { 0, 1 })
                {
                    SerializedProperty path = paths.GetArrayElementAtIndex(index);
                    LevelFlowEditing.SetCoordinate(path.FindPropertyRelative("coordinate"), C(20, 2));
                    LevelFlowEditing.SetCoordinate(path.FindPropertyRelative("next"), C(8, 8));
                }
                SerializedProperty portals = data.FindProperty("flow.portals"); portals.arraySize = 2;
                foreach (int index in new[] { 0, 1 })
                {
                    SerializedProperty portal = portals.GetArrayElementAtIndex(index);
                    LevelFlowEditing.SetCoordinate(portal.FindPropertyRelative("entrance"), C(1, 1));
                    portal.FindPropertyRelative("hasExit").boolValue = index == 0;
                    LevelFlowEditing.SetCoordinate(portal.FindPropertyRelative("exit"), C(1, 1));
                }
                SerializedProperty walls = data.FindProperty("flow.walls"); walls.arraySize++;
                SerializedProperty wall = walls.GetArrayElementAtIndex(walls.arraySize - 1);
                LevelFlowEditing.SetCoordinate(wall.FindPropertyRelative("a"), C(6, 1));
                LevelFlowEditing.SetCoordinate(wall.FindPropertyRelative("b"), C(6, 2));
                LevelFlowEditing.SetCoordinates(data.FindProperty("flow.arrivals"), new[] { C(1, 1), C(1, 1), C(30, 30) });
                data.FindProperty("connections.Array.data[0].vertices").arraySize = 1;
                data.FindProperty("obstacles.Array.data[1].id").stringValue = "";
            });
            snapshot = JsonUtility.ToJson(level);
            Check(Has(LevelValidationCode.InvalidFlow) && Has(LevelValidationCode.InvalidPortal) && Has(LevelValidationCode.InvalidArrival) && Has(LevelValidationCode.InvalidWall) && Has(LevelValidationCode.InvalidWire), "원본 Inspector 복합 오류 분류");
            Check(JsonUtility.ToJson(level) == snapshot, "검사에서 오류 좌표·중복·미완성 원본 보존");
            Ok(LevelFlowEditing.SetArrival(level, C(9, 9), false), "다른 정상 칸 편집");
            Check(level.Flow.Paths.Count == 2 && level.Flow.Paths[0].Coordinate.Row == 20 && level.Flow.Arrivals.Contains(C(30, 30)), "정상 칸 편집 후 다른 오류 기록 보존");
            level = savedFlow;
            AssetDatabase.SaveAssetIfDirty(level);
            level = original;
        }

        private static void Edit(Action<SerializedObject> action)
        {
            using SerializedObject data = new SerializedObject(level);
            action(data);
            LevelObstacleEditing.Commit(data, "검증 원본 값 변경");
        }
    }
}
