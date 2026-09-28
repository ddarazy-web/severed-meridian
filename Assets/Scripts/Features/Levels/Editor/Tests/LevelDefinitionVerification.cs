using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using Levels;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    // 별도 테스트 어셈블리 없이 배치 Editor에서 실행하는 1단계 인수 검사다.
    // PrepareAndVerify와 VerifyAfterRestartAndCleanup을 서로 다른 Editor 실행에서 호출한다.
    public static class LevelDefinitionVerification
    {
        private const string EvidenceFolder = "Logs/LevelDataVerification";
        private const string StatePath = EvidenceFolder + "/restart-state.json";
        private static readonly List<string> Results = new List<string>();

        private sealed class VerificationWindow : EditorWindow { }

        [Serializable]
        private sealed class RestartState
        {
            public string folder;
            public string[] names;
            public string[] snapshots;
            public string[] guids;
            public int processId;
        }

        public static void PrepareAndVerify()
        {
            Results.Clear();
            Directory.CreateDirectory(EvidenceFolder);
            if (File.Exists(StatePath))
            {
                RestartState previous = JsonUtility.FromJson<RestartState>(File.ReadAllText(StatePath));
                if (previous.snapshots.All(snapshot => !string.IsNullOrEmpty(snapshot)))
                    throw new InvalidOperationException("이전 검증 에셋이 남아 있습니다. 재시작 검증부터 실행하세요.");
                DeleteFixtures(previous);
                File.Move(StatePath, EvidenceFolder + "/failed-state-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".json");
            }

            string name = "__LevelDataVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", name);
            RestartState state = new RestartState
            {
                folder = "Assets/" + name,
                names = new[] { "Normal", "Independent", "Invalid" },
                snapshots = new string[3],
                guids = new string[3],
                processId = System.Diagnostics.Process.GetCurrentProcess().Id
            };
            // 실패 시에도 검증용 폴더의 소유권을 알 수 있도록 먼저 기록한다.
            File.WriteAllText(StatePath, JsonUtility.ToJson(state, true));

            try
            {
                UnityEngine.Object previousSelection = Selection.activeObject;
                Check(EditorApplication.ExecuteMenuItem("Assets/Create/퍼즐/레벨 데이터"), "에셋 생성 메뉴 실행");
                LevelDefinition normal = Selection.activeObject as LevelDefinition;
                Check(normal != null && AssetDatabase.GetAssetPath(normal).StartsWith(LevelAssetOperations.DefaultFolder + "/"),
                    "기본 Assets/Data/Levels 경로에 독립 에셋 생성");
                string createdGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(normal));
                Check(string.IsNullOrEmpty(AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(normal), state.folder + "/Normal.asset")),
                    "검증용 에셋 이동");
                Check(AssetDatabase.AssetPathToGUID(state.folder + "/Normal.asset") == createdGuid,
                    "파일명·경로 변경 시 에셋 식별 보존");
                Selection.activeObject = previousSelection;
                LevelDefinition independent = LevelAssetOperations.CreateAtPath(state.folder + "/Independent.asset");
                LevelDefinition invalid = LevelAssetOperations.CreateAtPath(state.folder + "/Invalid.asset");
                LevelMissionEditing.Add(normal);
                LevelMissionEditing.Add(independent);
                Check(normal.Board.Rows == 10 && normal.Board.Columns == 10 && normal.Board.Cells.Count == 100 &&
                    normal.Board.Cells.All(cell => cell.IsActive), "기본 에셋: 10×10 활성 100칸");
                Check(normal.InitialBlocks.Count == 0 && normal.Colors.Count == 5 &&
                    LevelDefinitionValidator.Validate(normal).Count == 0, "활성 빈칸과 기본 색 정의");
                Check(LevelAssetOperations.FindNumberConflicts(normal).Count >= 2, "레벨 번호 중복 검색");
                Check(normal.LevelNumber == 1 && independent.LevelNumber == 1, "중복 검사 시 번호 보존");

                ConfigureNormal(normal);
                string independentBefore = JsonUtility.ToJson(independent);
                Check(independent.Board.Cells[99].IsActive && independent.InitialBlocks.Count == 0 &&
                    independent.MoveCount == 20, "다른 에셋에 변경 전파 없음");
                Check(LevelDefinitionValidator.Validate(normal).Count == 0, "고정·무작위 혼합 배치 정상 판정");
                Check(normal.InitialBlocks[0].FixedColor == RabbitColor.Type2 &&
                    normal.InitialBlocks[1].Kind == InitialBlockKind.RandomNormal &&
                    normal.InitialBlocks[1].FixedColor == null, "무작위 색 미확정 및 고정 색 의미 분리");

                VerifyUndoAndDrawers(normal);
                Check(JsonUtility.ToJson(independent) == independentBefore, "Undo/Redo 후에도 다른 에셋 독립");
                VerifyInactivePreservation(normal);
                VerifyValidationCases();
                ConfigureInvalid(invalid);
                List<LevelValidationIssue> invalidIssues = LevelDefinitionValidator.Validate(invalid);
                Check(invalidIssues.Any(issue => issue.Code == LevelValidationCode.CoordinateOutOfRange &&
                    issue.Coordinate.Equals(new BoardCoordinate(20, 0)) &&
                    issue.PropertyPath == "initialBlocks.Array.data[2].coordinate"), "오류 좌표와 필드 경로 보고");

                LevelDefinition[] levels = { normal, independent, invalid };
                for (int i = 0; i < levels.Length; i++)
                {
                    LevelDefinition level = levels[i];
                    AssetDatabase.SaveAssetIfDirty(level);
                    string before = JsonUtility.ToJson(level);
                    Check(!EditorUtility.IsDirty(level), state.names[i] + " 저장 후 수정 상태 해제");
                    LevelDefinitionValidator.Validate(level);
                    LevelAssetOperations.FindNumberConflicts(level);
                    Check(JsonUtility.ToJson(level) == before && !EditorUtility.IsDirty(level),
                        state.names[i] + " 검사 원본·수정 상태 보존");

                    string path = AssetDatabase.GetAssetPath(level);
                    state.snapshots[i] = before;
                    state.guids[i] = AssetDatabase.AssetPathToGUID(path);
                    Resources.UnloadAsset(level);
                    LevelDefinition reloaded = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                    Check(JsonUtility.ToJson(reloaded) == before, state.names[i] + " 저장·언로드·재로딩 왕복");
                }
                File.WriteAllText(StatePath, JsonUtility.ToJson(state, true));
                Results.Add("다음 검증: 별도 Editor 프로세스에서 VerifyAfterRestartAndCleanup 실행");
            }
            finally
            {
                File.WriteAllLines(EvidenceFolder + "/prepare-results.txt", Results);
            }
        }

        public static void VerifyAfterRestartAndCleanup()
        {
            Results.Clear();
            RestartState state = JsonUtility.FromJson<RestartState>(File.ReadAllText(StatePath));
            try
            {
                Check(state.processId != System.Diagnostics.Process.GetCurrentProcess().Id,
                    "별도 Unity Editor 프로세스로 재시작 확인");
                for (int i = 0; i < state.names.Length; i++)
                {
                    string path = state.folder + "/" + state.names[i] + ".asset";
                    LevelDefinition level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                    Check(level != null && JsonUtility.ToJson(level) == state.snapshots[i],
                        state.names[i] + " Editor 재시작 후 전체 필드 동일");
                    Check(AssetDatabase.AssetPathToGUID(path) == state.guids[i],
                        state.names[i] + " 에셋 GUID 보존");
                    Check(!EditorUtility.IsDirty(level), state.names[i] + " 로딩 부작용 없음");
                }
                LevelDefinition invalid = AssetDatabase.LoadAssetAtPath<LevelDefinition>(state.folder + "/Invalid.asset");
                Check(LevelDefinitionValidator.Validate(invalid).Count >= 7, "오류 데이터가 재시작 후에도 보존·진단됨");

                // 이 실행에서 만든 검증 폴더만 AssetDatabase로 삭제한다.
                DeleteFixtures(state);
                File.Move(StatePath, EvidenceFolder + "/completed-state-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".json");
            }
            finally
            {
                File.WriteAllLines(EvidenceFolder + "/restart-results.txt", Results);
            }
        }

        private static void ConfigureNormal(LevelDefinition level)
        {
            // 기존 칸 비활성화 회귀는 공급 없는 보드를 사용한다. 기본 생성구는 5단계 검사에서 확인한다.
            LevelSupplyEditing.PlaceSources(level, level.Supply.Sources.Select(source => source.Coordinate).ToArray(), true);
            SerializedObject data = new SerializedObject(level);
            data.FindProperty("levelNumber").intValue = 4321;
            data.FindProperty("moveCount").intValue = 27;
            data.FindProperty("colors").arraySize = 3;
            data.FindProperty("board.cells").GetArrayElementAtIndex(99).FindPropertyRelative("isActive").boolValue = false;
            SerializedProperty blocks = data.FindProperty("initialBlocks");
            blocks.arraySize = 2;
            SetBlock(blocks.GetArrayElementAtIndex(0), 0, 0, InitialBlockKind.FixedNormal, RabbitColor.Type2);
            SetBlock(blocks.GetArrayElementAtIndex(1), 3, 4, InitialBlockKind.RandomNormal, RabbitColor.Type5);
            data.ApplyModifiedProperties();
        }

        private static void ConfigureInvalid(LevelDefinition level)
        {
            SerializedObject data = new SerializedObject(level);
            data.FindProperty("levelNumber").intValue = 0;
            data.FindProperty("moveCount").intValue = -1;
            data.FindProperty("colors").arraySize = 3;
            data.FindProperty("colors").GetArrayElementAtIndex(1).intValue = 0;
            data.FindProperty("board.cells").GetArrayElementAtIndex(0).FindPropertyRelative("isActive").boolValue = false;
            SerializedProperty blocks = data.FindProperty("initialBlocks");
            blocks.arraySize = 3;
            SetBlock(blocks.GetArrayElementAtIndex(0), 0, 0, InitialBlockKind.FixedNormal, RabbitColor.Type5);
            SetBlock(blocks.GetArrayElementAtIndex(1), 0, 0, InitialBlockKind.RandomNormal, RabbitColor.Type1);
            SetBlock(blocks.GetArrayElementAtIndex(2), 20, 0, InitialBlockKind.FixedNormal, RabbitColor.Type1);
            data.ApplyModifiedProperties();
        }

        private static void VerifyInactivePreservation(LevelDefinition level)
        {
            SerializedObject data = new SerializedObject(level);
            string before = JsonUtility.ToJson(level);
            data.FindProperty("board.cells").GetArrayElementAtIndex(0).FindPropertyRelative("isActive").boolValue = false;
            data.ApplyModifiedProperties();
            Check(level.InitialBlocks.Count == 2 && level.InitialBlocks[0].FixedColor == RabbitColor.Type2 &&
                LevelDefinitionValidator.Validate(level).Any(issue => issue.Code == LevelValidationCode.InactiveCellPlacement),
                "비활성 칸 배치 보존 및 오류 보고");
            data.Update();
            data.FindProperty("board.cells").GetArrayElementAtIndex(0).FindPropertyRelative("isActive").boolValue = true;
            data.ApplyModifiedProperties();
            Check(JsonUtility.ToJson(level) == before && LevelDefinitionValidator.Validate(level).Count == 0,
                "칸 재활성화 시 원래 데이터 복원");
        }

        private static void VerifyUndoAndDrawers(LevelDefinition level)
        {
            Undo.IncrementCurrentGroup();
            SerializedObject data = new SerializedObject(level);
            string before = JsonUtility.ToJson(level);
            data.FindProperty("moveCount").intValue = 41;
            data.FindProperty("board.cells").GetArrayElementAtIndex(1).FindPropertyRelative("isActive").boolValue = false;
            data.FindProperty("initialBlocks").GetArrayElementAtIndex(1).FindPropertyRelative("coordinate.row").intValue = 6;
            data.ApplyModifiedProperties();
            Undo.FlushUndoRecordObjects();
            string after = JsonUtility.ToJson(level);
            Undo.PerformUndo();
            Check(JsonUtility.ToJson(level) == before, "Inspector 직렬화 경로: 다중 속성 Undo");
            Undo.PerformRedo();
            Check(JsonUtility.ToJson(level) == after && level.MoveCount == 41, "Inspector 직렬화 경로: Redo");
            Undo.IncrementCurrentGroup();
            data.Update();

            VerificationWindow window = ScriptableObject.CreateInstance<VerificationWindow>();
            UnityEditor.Editor inspector = UnityEditor.Editor.CreateEditor(level);
            try
            {
                window.Show();
                BoardCoordinateDrawer drawer = new BoardCoordinateDrawer();
                VisualElement coordinate = drawer.CreatePropertyGUI(data.FindProperty("initialBlocks").GetArrayElementAtIndex(1)
                    .FindPropertyRelative("coordinate"));
                window.rootVisualElement.Add(coordinate);
                Check(coordinate.panel != null, "좌표 입력을 실제 Editor 패널에 연결");
                LongField row = coordinate.Q<LongField>("row");
                Check(row.value == 7, "좌표 표시: 내부 행 6 → 화면 행 7");
                row.value = 9;
                Undo.FlushUndoRecordObjects();
                Check(level.InitialBlocks[1].Coordinate.Row == 8, "좌표 UI 변경: 화면 행 9 → 내부 행 8");
                Undo.PerformUndo();
                Check(level.InitialBlocks[1].Coordinate.Row == 6, "좌표 Drawer 직접 편집 Undo");
                Undo.PerformRedo();
                Check(level.InitialBlocks[1].Coordinate.Row == 8, "좌표 Drawer 직접 편집 Redo");

                Check(inspector is LevelDefinitionInspector, "사용자 정의 Inspector 연결");
                VisualElement root = inspector.CreateInspectorGUI();
                Check(root.Q<Button>("validate-level") != null && root.Q<Button>("save-level") != null &&
                    new[] { "schemaVersion", "levelNumber", "moveCount", "colors", "board", "initialBlocks", "obstacles", "covers", "dust" }
                        .All(path => root.Query<PropertyField>().ToList().Any(field => field.bindingPath == path)),
                    "Inspector 기존 필드·신규 층·검사·저장 진입점 생성");
                window.rootVisualElement.Add(root);
                root.Bind(inspector.serializedObject);
                IntegerField moves = root.Query<PropertyField>().ToList()
                    .First(field => field.bindingPath == "moveCount").Q<IntegerField>();
                Check(moves != null && moves.value == 41, "Inspector 이동 횟수 바인딩");
                Undo.IncrementCurrentGroup();
                moves.value = 43;
                Undo.FlushUndoRecordObjects();
                Check(level.MoveCount == 43, "Inspector 실제 입력 → 에셋 반영");
                Undo.PerformUndo();
                Check(level.MoveCount == 41, "Inspector 실제 입력 Undo");
                Undo.PerformRedo();
                Check(level.MoveCount == 43, "Inspector 실제 입력 Redo");

                Button save = root.Q<Button>("save-level");
                using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = save;
                    save.SendEvent(submit);
                }
                Check(!EditorUtility.IsDirty(level), "Inspector 저장 버튼 실행");
                string saved = JsonUtility.ToJson(level);
                Button validate = root.Q<Button>("validate-level");
                using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = validate;
                    validate.SendEvent(submit);
                }
                if (!root.Query<Label>().ToList().Any(label => label.text == "구조 오류 없음 (현재 검사 기준)") || EditorUtility.IsDirty(level) || JsonUtility.ToJson(level) != saved)
                    Results.Add("진단: " + string.Join(" | ", LevelDefinitionValidator.Validate(level).Select(issue => issue.ToString())) +
                        " / dirty=" + EditorUtility.IsDirty(level) + " / same=" + (JsonUtility.ToJson(level) == saved) +
                        " / labels=" + string.Join(" | ", root.Query<Label>().ToList().Select(label => label.text).Where(text => text?.Contains("구조 오류") == true)));
                Check(root.Query<Label>().ToList().Any(label => label.text == "구조 오류 없음 (현재 검사 기준)") &&
                    !EditorUtility.IsDirty(level) && JsonUtility.ToJson(level) == saved,
                    "Inspector 검사 버튼: 결과 표시 및 데이터·수정 상태 보존");
                root.Unbind();
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(inspector);
                Undo.ClearUndo(level);
            }
        }

        private static void VerifyValidationCases()
        {
            VerifyInvalidField("schemaVersion", 99, LevelValidationCode.UnsupportedSchemaVersion);
            VerifyInvalidField("levelNumber", 0, LevelValidationCode.InvalidLevelNumber);
            VerifyInvalidField("moveCount", 0, LevelValidationCode.InvalidMoveCount);
            VerifyInvalidField("board.rows", 9, LevelValidationCode.UnsupportedBoardSize);
            VerifyInvalidField("board.columns", 0, LevelValidationCode.CellCountMismatch);

            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                SerializedObject data = new SerializedObject(level);
                data.FindProperty("colors").arraySize = 2;
                data.ApplyModifiedProperties();
                Check(LevelDefinitionValidator.Validate(level).Any(x => x.Code == LevelValidationCode.InvalidColorCount), "사용 색 2종 거부");
                data.FindProperty("colors").arraySize = 6;
                data.ApplyModifiedProperties();
                Check(LevelDefinitionValidator.Validate(level).Any(x => x.Code == LevelValidationCode.InvalidColorCount), "사용 색 6종 거부");
                data.FindProperty("colors").arraySize = 3;
                data.FindProperty("colors").GetArrayElementAtIndex(2).intValue = 999;
                data.FindProperty("board.cells").arraySize = 99;
                data.ApplyModifiedProperties();
                List<LevelValidationIssue> issues = LevelDefinitionValidator.Validate(level);
                Check(issues.Any(x => x.Code == LevelValidationCode.InvalidColor) &&
                    issues.Any(x => x.Code == LevelValidationCode.CellCountMismatch), "미정의 색·칸 수 오류");

                ConfigureInvalid(level);
                string before = JsonUtility.ToJson(level);
                issues = LevelDefinitionValidator.Validate(level);
                foreach (LevelValidationCode expected in new[] { LevelValidationCode.DuplicateColor,
                    LevelValidationCode.DuplicatePlacement, LevelValidationCode.UnusedFixedColor,
                    LevelValidationCode.CoordinateOutOfRange, LevelValidationCode.InactiveCellPlacement })
                    Check(issues.Any(x => x.Code == expected), "오류 진단: " + expected);
                Check(JsonUtility.ToJson(level) == before, "복합 오류 데이터 검사 후 원본 동일");

                data.Update();
                data.FindProperty("initialBlocks").GetArrayElementAtIndex(0).FindPropertyRelative("kind").intValue = 999;
                data.ApplyModifiedProperties();
                Check(LevelDefinitionValidator.Validate(level).Any(x => x.Code == LevelValidationCode.InvalidBlockKind), "미정의 배치 유형 오류");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(level);
            }
        }

        private static void VerifyInvalidField(string path, int value, LevelValidationCode expected)
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                SerializedObject data = new SerializedObject(level);
                data.FindProperty(path).intValue = value;
                data.ApplyModifiedProperties();
                string before = JsonUtility.ToJson(level);
                Check(LevelDefinitionValidator.Validate(level).Any(x => x.Code == expected) &&
                    JsonUtility.ToJson(level) == before, "오류 진단·보존: " + path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(level);
            }
        }

        private static void SetBlock(SerializedProperty block, int row, int column, InitialBlockKind kind, RabbitColor color)
        {
            block.FindPropertyRelative("coordinate.row").intValue = row;
            block.FindPropertyRelative("coordinate.column").intValue = column;
            block.FindPropertyRelative("kind").intValue = (int)kind;
            block.FindPropertyRelative("fixedColor").intValue = (int)color;
        }

        private static void Check(bool condition, string description)
        {
            Results.Add((condition ? "PASS " : "FAIL ") + description);
            if (!condition)
                throw new InvalidOperationException(description);
            Debug.Log("[LevelDataVerification] PASS " + description);
        }

        private static void DeleteFixtures(RestartState state)
        {
            const string prefix = "Assets/__LevelDataVerification_";
            if (state.folder == null || !state.folder.StartsWith(prefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(state.folder.Substring(prefix.Length), "N", out _))
                throw new InvalidOperationException("검증 폴더 경로가 안전하지 않습니다.");
            Check(AssetDatabase.DeleteAsset(state.folder), "임시 검증 에셋과 메타데이터 정리");
        }
    }
}
