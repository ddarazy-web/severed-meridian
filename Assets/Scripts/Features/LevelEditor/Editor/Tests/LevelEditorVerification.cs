using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    // -executeMethod ...Start로 실행한다. UI 프레임을 실제 진행한 뒤 스스로 종료하므로 -quit는 사용하지 않는다.
    public static class LevelEditorVerification
    {
        private const string Folder = "Logs/LevelEditorVerification";
        private const string StateFile = Folder + "/state.json";
        private static readonly List<string> Results = new List<string>();
        private static IEnumerator sequence;
        private static double nextTick;
        private static LevelEditorWindow window;
        private static LevelDefinition level;
        private static LevelDefinition other;
        private static string assetFolder;
        private sealed class FocusWindow : EditorWindow { }

        [Serializable]
        private sealed class SavedState
        {
            public string folder;
            public string normal;
            public string other;
            public string normalGuid;
            public string otherGuid;
            public int processId;
        }

        public static void Start()
        {
            Directory.CreateDirectory(Folder);
            Results.Clear();
            if (File.Exists("Logs/LevelDataVerification/restart-state.json"))
                LevelDefinitionVerification.VerifyAfterRestartAndCleanup();
            if (File.Exists(StateFile))
                throw new InvalidOperationException("이전 검증 상태가 남아 있습니다. Restart부터 실행하세요.");
            string name = "__LevelEditorVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", name);
            assetFolder = "Assets/" + name;
            level = LevelAssetOperations.CreateAtPath(assetFolder + "/Normal.asset");
            other = LevelAssetOperations.CreateAtPath(assetFolder + "/Other.asset");
            LevelMissionEditing.Add(level);
            LevelMissionEditing.Add(other);
            SetInt(level, "levelNumber", 51001);
            SetInt(other, "levelNumber", 51002);
            AssetDatabase.SaveAssetIfDirty(level);
            AssetDatabase.SaveAssetIfDirty(other);
            window = ScriptableObject.CreateInstance<LevelEditorWindow>();
            window.titleContent = new GUIContent("레벨 에디터 검증");
            window.position = new Rect(80, 80, 1120, 720);
            window.ShowUtility();
            window.SetLevel(level);
            window.Focus();
            sequence = Run();
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.15;
            try
            {
                if (sequence.MoveNext()) return;
                Finish(0);
            }
            catch (Exception exception)
            {
                Results.Add("FAIL " + exception);
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            File.WriteAllLines(Folder + "/ui-results.txt", Results);
            if (window != null) window.Close();
            if (code != 0 && !string.IsNullOrEmpty(assetFolder))
                AssetDatabase.DeleteAsset(assetFolder);
            EditorApplication.Exit(code);
        }

        private static IEnumerator Run()
        {
            yield return null;
            yield return null;
            LevelBoardView board = window.rootVisualElement.Q<LevelBoardView>();
            Check(board.panel != null && board.worldBound.width == 400 && board.Children().OfType<Label>().Count() == 100, "실제 창에 100칸과 포인터 패널 배치");
            Check(!EditorUtility.IsDirty(level) && level.InitialBlocks.Count == 0, "창 열기만으로 원본 변경 없음");
            string independent = JsonUtility.ToJson(other);

            Click("tool-Fixed-2");
            Pointer(EventType.MouseDown, 2, 1);
            yield return null;
            Check(board.IsDragging && board.HasPointerCapture(PointerId.mousePointerId) && level.InitialBlocks.Count == 0,
                "포인터 캡처·드래그 미리보기는 원본과 분리");
            Pointer(EventType.MouseDrag, 2, 8);
            Pointer(EventType.MouseDrag, 2, 3);
            Pointer(EventType.MouseUp, 2, 5);
            yield return null;
            Check(level.InitialBlocks.Count == 8 && Enumerable.Range(1, 8).All(c =>
                LevelBoardEditing.FindBlock(level, new BoardCoordinate(2, c)) >= 0), "빠른 드래그·반복 방문: 지나간 8칸에 중복 없는 배치");
            Check(level.InitialBlocks.All(block => block.FixedColor == RabbitColor.Type3), "고정 색 도구 값 적용");
            string painted = JsonUtility.ToJson(level);
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            yield return null;
            Check(level.InitialBlocks.Count == 0, "드래그 1회 → Undo 1회로 전체 복구");
            Undo.PerformRedo();
            yield return null;
            Check(JsonUtility.ToJson(level) == painted, "드래그 Redo 전체 동일");

            Click("save-level");
            int undoGroup = Undo.GetCurrentGroup();
            Pointer(EventType.MouseDown, 2, 1); Pointer(EventType.MouseUp, 2, 1);
            yield return null;
            Check(!EditorUtility.IsDirty(level) && Undo.GetCurrentGroup() == undoGroup, "같은 블록 칠하기: dirty·Undo 변화 없음");
            Click("tool-Erase");
            undoGroup = Undo.GetCurrentGroup();
            Pointer(EventType.MouseDown, 0, 0); Pointer(EventType.MouseUp, 0, 0);
            yield return null;
            Check(!EditorUtility.IsDirty(level) && Undo.GetCurrentGroup() == undoGroup, "빈칸 지우기: dirty·Undo 변화 없음");

            Click("tool-Deactivate");
            Pointer(EventType.MouseDown, 2, 1); Pointer(EventType.MouseUp, 2, 1);
            yield return null;
            Check(!level.Board.Cells[21].IsActive && level.InitialBlocks.Count == 8, "비활성화해도 블록 보존");
            Click("tool-Random");
            Pointer(EventType.MouseDown, 2, 1); Pointer(EventType.MouseUp, 2, 1);
            yield return null;
            Check(level.InitialBlocks[LevelBoardEditing.FindBlock(level, new BoardCoordinate(2, 1))].FixedColor == RabbitColor.Type3,
                "비활성 칸 신규 칠하기 금지");
            Click("tool-Select");
            Pointer(EventType.MouseDown, 2, 1);
            window.rootVisualElement.Q<Toggle>("selected-active").value = true;
            yield return null;
            Check(level.Board.Cells[21].IsActive, "선택 칸 속성에서 재활성화");
            window.rootVisualElement.Q<PopupField<string>>("selected-block").value = "무작위 ?";
            yield return null;
            Check(level.InitialBlocks[LevelBoardEditing.FindBlock(level, new BoardCoordinate(2, 1))].Kind == InitialBlockKind.RandomNormal,
                "선택 칸 속성에서 고정→무작위 교체");
            Check(window.rootVisualElement.Q<Label>("cell-21").text == "?", "무작위 보드 표시");
            window.rootVisualElement.Q<PopupField<string>>("selected-block").value = "빈칸";
            yield return null;
            Check(level.InitialBlocks.Count == 7 && level.Board.Cells[21].IsActive, "지우기는 일반 블록만 제거");

            Click("tool-Random");
            Pointer(EventType.MouseDown, 4, 8); Pointer(EventType.MouseDrag, 4, 12); Pointer(EventType.MouseUp, 4, 12);
            yield return null;
            Check(LevelBoardEditing.FindBlock(level, new BoardCoordinate(4, 8)) >= 0 &&
                LevelBoardEditing.FindBlock(level, new BoardCoordinate(4, 9)) >= 0, "보드 밖에서 놓기: 내부 경계까지 완료");
            Pointer(EventType.MouseDown, 5, 9); Pointer(EventType.MouseDrag, 5, 12);
            Pointer(EventType.MouseDrag, 7, 12); Pointer(EventType.MouseDrag, 7, 0); Pointer(EventType.MouseUp, 7, 0);
            yield return null;
            Check(LevelBoardEditing.FindBlock(level, new BoardCoordinate(6, 5)) < 0 &&
                LevelBoardEditing.FindBlock(level, new BoardCoordinate(7, 5)) < 0, "보드 밖 재진입 시 허구의 연결 경로 없음");

            string beforeCancel = JsonUtility.ToJson(level);
            window.Focus();
            yield return null;
            Check(EditorWindow.focusedWindow == window, "포커스 검사 시작 창 확인");
            Pointer(EventType.MouseDown, 0, 0); Pointer(EventType.MouseDrag, 0, 5);
            FocusWindow focusWindow = ScriptableObject.CreateInstance<FocusWindow>();
            focusWindow.ShowUtility(); focusWindow.Focus();
            yield return null;
            Check(EditorWindow.focusedWindow == focusWindow, "다른 실제 Editor 창으로 포커스 전환");
            focusWindow.Close(); window.Focus();
            yield return null;
            Pointer(EventType.MouseUp, 0, 5);
            Check(!board.IsDragging && JsonUtility.ToJson(level) == beforeCancel, "포커스 상실: 미완료 드래그만 취소");
            Pointer(EventType.MouseDown, 0, 0);
            yield return null;
            Check(board.HasPointerCapture(PointerId.mousePointerId), "캡처 상실 검사 전 소유권 확인");
            board.ReleasePointer(PointerId.mousePointerId);
            yield return null;
            Pointer(EventType.MouseUp, 0, 0);
            Check(!board.IsDragging && JsonUtility.ToJson(level) == beforeCancel, "캡처 상실: 미완료 드래그 취소");
            Pointer(EventType.MouseDown, 0, 0);
            Click("save-level"); Pointer(EventType.MouseUp, 0, 0);
            Check(JsonUtility.ToJson(level) == beforeCancel, "저장 시 미완료 미리보기 취소");
            Pointer(EventType.MouseDown, 0, 0);
            Click("validate-level"); Pointer(EventType.MouseUp, 0, 0);
            Check(JsonUtility.ToJson(level) == beforeCancel, "검사 시 미완료 미리보기 취소");
            Pointer(EventType.MouseDown, 0, 0);
            Click("tool-Fixed-1"); Pointer(EventType.MouseUp, 0, 0);
            Check(JsonUtility.ToJson(level) == beforeCancel, "도구 전환 시 미완료 미리보기 취소");

            Pointer(EventType.MouseDown, 0, 0);
            SetInt(level, "moveCount", 35);
            yield return null;
            Pointer(EventType.MouseUp, 0, 0);
            Check(!board.IsDragging && level.InitialBlocks.Count == 11 && level.MoveCount == 35,
                "외부 Inspector 직렬화 변경 시 미완료 미리보기 취소");
            Check(window.rootVisualElement.Query<IntegerField>().ToList().Any(field => field.value == 35), "외부 속성 변경 화면 반영");
            Pointer(EventType.MouseDown, 0, 0);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            yield return null;
            Pointer(EventType.MouseUp, 0, 0);
            Check(!board.IsDragging && level.MoveCount == 20, "Undo 중 드래그 취소·화면 갱신");
            Undo.PerformRedo(); yield return null;

            beforeCancel = JsonUtility.ToJson(level);
            Pointer(EventType.MouseDown, 0, 0);
            window.SetLevel(other);
            yield return null;
            Pointer(EventType.MouseUp, 0, 0);
            Check(JsonUtility.ToJson(level) == beforeCancel && JsonUtility.ToJson(other) == independent, "대상 전환 중 미완료 입력 혼입 없음");
            window.SetLevel(null); yield return null;
            Check(!window.rootVisualElement.Q<Button>("save-level").enabledSelf, "미선택 상태 편집 안내·저장 비활성");
            window.SetLevel(level); yield return null;
            Click("tool-Random"); Pointer(EventType.MouseDown, 0, 0);
            window.Close(); yield return null;
            // 숨겨진 기본 Editor의 도킹 영역 대신 실제 렌더링되는 독립 창에서 재열기 경로를 확인한다.
            window = ScriptableObject.CreateInstance<LevelEditorWindow>();
            window.titleContent = new GUIContent("레벨 에디터 검증");
            window.position = new Rect(80, 80, 1120, 720);
            window.ShowUtility();
            LevelEditorWindow.OpenLevel(level);
            window.Focus();
            yield return null; yield return null;
            Check(JsonUtility.ToJson(level) == beforeCancel, "창 닫기·다시 열기: 완료 데이터 유지, 미완료 입력 취소");
            Check(EditorApplication.ExecuteMenuItem("Match/레벨 에디터"), "메뉴에서 편집 창 열기");

            FocusWindow inspectorHost = ScriptableObject.CreateInstance<FocusWindow>();
            inspectorHost.position = new Rect(1230, 80, 330, 600);
            inspectorHost.ShowUtility();
            UnityEditor.Editor inspector = UnityEditor.Editor.CreateEditor(level);
            VisualElement inspectorRoot = inspector.CreateInspectorGUI();
            inspectorHost.rootVisualElement.Add(inspectorRoot);
            inspectorRoot.Bind(inspector.serializedObject);
            yield return null;
            Submit(inspectorRoot.Q<Button>("open-level-editor"));
            yield return null;
            Check(window.CurrentLevel == level, "기존 Inspector 열기 버튼: 같은 에셋 연결");
            IntegerField inspectorMoves = inspectorRoot.Query<PropertyField>().ToList()
                .First(field => field.bindingPath == "moveCount").Q<IntegerField>();
            inspectorMoves.value = 36;
            yield return null; yield return null;
            Check(level.MoveCount == 36 && window.rootVisualElement.Query<IntegerField>().ToList().Any(field => field.value == 36),
                "실제 기존 Inspector 입력 → 편집 창 동기화");
            IntegerField windowMoves = window.rootVisualElement.Query<PropertyField>().ToList()
                .First(field => field.bindingPath == "moveCount").Q<IntegerField>();
            Check(windowMoves.isDelayed, "숫자 속성은 입력 확정 시 반영하여 편집 포커스 보존");
            Undo.IncrementCurrentGroup();
            windowMoves.value = 37;
            yield return null; yield return null;
            Check(level.MoveCount == 37 && inspectorMoves.value == 37, "편집 창 입력 → 기존 Inspector 동기화");
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            yield return null; yield return null;
            Check(level.MoveCount == 36 && inspectorMoves.value == 36, "두 편집 화면 간 Undo 동기화");
            Undo.PerformRedo(); yield return null; yield return null;
            Check(level.MoveCount == 37 && inspectorMoves.value == 37, "두 편집 화면 간 Redo 동기화");
            inspectorRoot.Unbind(); inspectorHost.Close(); UnityEngine.Object.DestroyImmediate(inspector);
            window.Focus();
            Click("new-level"); yield return null;
            Click("confirm-level-name"); yield return null;
            LevelDefinition disposable = window.CurrentLevel;
            string disposablePath = AssetDatabase.GetAssetPath(disposable);
            Check(disposable != level && disposable.Board.Cells.Count == 100 && disposablePath.StartsWith(LevelAssetOperations.DefaultFolder),
                "편집 창 새 레벨 버튼: 기본 경로에 독립 에셋 생성");
            AssetDatabase.DeleteAsset(disposablePath);
            yield return null; yield return null; yield return null;
            Check(window.CurrentLevel == null && !window.rootVisualElement.Q<Button>("save-level").enabledSelf,
                "선택 중인 에셋 삭제 시 빈 상태로 복귀");
            window.SetLevel(level); yield return null;

            // 원본 목록에 오류를 만든 뒤 그리기·검사·다른 정상 칸 편집으로 소실되지 않는지 확인한다.
            using (SerializedObject invalid = new SerializedObject(level))
            {
                invalid.FindProperty("colors").arraySize = 3;
                SerializedProperty blocks = invalid.FindProperty("initialBlocks");
                int start = blocks.arraySize;
                blocks.arraySize += 3;
                SetBlock(blocks.GetArrayElementAtIndex(start), 8, 8, 1, 4);
                SetBlock(blocks.GetArrayElementAtIndex(start + 1), 8, 8, 0, 0);
                SetBlock(blocks.GetArrayElementAtIndex(start + 2), 20, 20, 77, 4);
                invalid.ApplyModifiedProperties();
            }
            yield return null;
            yield return null;
            yield return null;
            Check(window.CurrentLevel == level && level.Colors.Count == 3, "외부 변경한 에셋과 창 대상 확인");
            Check(!window.rootVisualElement.Q<PopupField<string>>("menu-normal").choices.Contains("고정 5"), "미사용 색은 일반 블록 메뉴에서 제외");
            string invalidBefore = JsonUtility.ToJson(level);
            Click("save-level"); Click("validate-level");
            Check(!EditorUtility.IsDirty(level) && JsonUtility.ToJson(level) == invalidBefore, "검사·저장 시 오류 데이터 보존");
            ScrollView errorList = window.rootVisualElement.Q<ScrollView>("validation-issues");
            Check(errorList.Query<Button>().ToList().Count >= 3, "중복·범위 밖·미사용 색 오류 표시");
            Button inside = errorList.Query<Button>().ToList().First(button => button.text.Contains("DuplicatePlacement"));
            Submit(inside); yield return null;
            Check(window.rootVisualElement.Q<VisualElement>("selected-cell-properties").Query<Label>().ToList()
                .Any(label => label.text.Contains("9행 9열")), "오류 클릭 시 해당 칸 선택·강조");
            Button outside = errorList.Query<Button>().ToList().First(button => button.text.Contains("CoordinateOutOfRange"));
            Submit(outside);
            Check(window.rootVisualElement.Q<Label>("operation-status").text.Contains("행 21, 열 21"), "범위 밖 오류 좌표를 보정하지 않고 표시");
            Click("tool-Erase"); Pointer(EventType.MouseDown, 8, 8); Pointer(EventType.MouseUp, 8, 8);
            Check(JsonUtility.ToJson(level) == invalidBefore, "중복 배치 지우기 거부·원본 유지");
            Click("tool-Random"); Pointer(EventType.MouseDown, 9, 0); Pointer(EventType.MouseUp, 9, 0);
            yield return null;
            Check(level.InitialBlocks.Count == 15 && level.InitialBlocks.Count(block => block.Coordinate.Equals(new BoardCoordinate(8, 8))) == 2 &&
                level.InitialBlocks.Any(block => block.Coordinate.Row == 20 && (int)block.Kind == 77), "정상 칸 편집 시 오류 항목 보존");
            Check(window.rootVisualElement.Q<Label>("validation-status").text.Contains("다시 검사"), "편집 후 오래된 검사 결과 무효화");

            SetInt(level, "board.rows", 9); yield return null;
            string malformed = JsonUtility.ToJson(level);
            Pointer(EventType.MouseDown, 0, 0); Pointer(EventType.MouseUp, 0, 0);
            Check(JsonUtility.ToJson(level) == malformed && !window.rootVisualElement.Q<PopupField<string>>("menu-normal").enabledSelf,
                "잘못된 보드 구조는 칠하기 차단·자동 보정 없음");
            SetInt(level, "board.rows", 10); yield return null;
            using (SerializedObject missingCell = new SerializedObject(level))
            {
                missingCell.FindProperty("board.cells").arraySize = 99;
                missingCell.ApplyModifiedProperties();
            }
            yield return null;
            malformed = JsonUtility.ToJson(level);
            Pointer(EventType.MouseDown, 9, 9); Pointer(EventType.MouseUp, 9, 9);
            Check(JsonUtility.ToJson(level) == malformed && window.rootVisualElement.Q<Label>("cell-99").text == "—",
                "누락된 칸은 자동 추가 없이 표시하고 칠하기 차단");
            using (SerializedObject restoreCell = new SerializedObject(level))
            {
                restoreCell.FindProperty("board.cells").arraySize = 100;
                restoreCell.ApplyModifiedProperties();
            }
            SetInt(level, "schemaVersion", 99); yield return null;
            Check(!window.rootVisualElement.Q<PopupField<string>>("menu-normal").enabledSelf && level.SchemaVersion == 99,
                "미지원 저장 버전 편집 차단·원본 보존");
            SetInt(level, "schemaVersion", LevelDefinition.CurrentSchemaVersion); yield return null;
            Click("validate-level");
            SetInt(other, "levelNumber", level.LevelNumber); yield return null; yield return null;
            Check(window.rootVisualElement.Q<Label>("validation-status").text.Contains("다시 검사"),
                "다른 에셋 번호 변경 시 검사 결과 무효화");
            Click("validate-level");
            Check(window.rootVisualElement.Q<ScrollView>("validation-issues").Query<Button>().ToList()
                .Any(button => button.text.Contains("DuplicateLevelNumber")), "편집 창에서 에셋 간 번호 중복 표시");
            SetInt(other, "levelNumber", 51002); yield return null;
            Click("validate-level");
            window.Focus();
            yield return null;
            window.position = new Rect(80, 80, 1120, 720);
            window.rootVisualElement.Q<ScrollView>("board-scroll").scrollOffset = Vector2.zero;
            window.Repaint();
            yield return null; yield return null;
            Check(window.position.width == 1120 && window.position.height == 720, "넓은 창 캡처 크기 확인");
            Capture("editor-wide.png");
            window.position = new Rect(80, 80, 680, 480);
            yield return null; yield return null;
            ScrollView scroll = window.rootVisualElement.Q<ScrollView>("board-scroll");
            Check(scroll.contentContainer.worldBound.width > scroll.contentViewport.worldBound.width, "좁은 창에서 보드 가로 스크롤 제공");
            scroll.ScrollTo(window.rootVisualElement.Q<Label>("cell-99"));
            yield return null;
            Capture("editor-narrow.png");

            Click("save-level");
            AssetDatabase.SaveAssetIfDirty(other);
            SavedState state = new SavedState
            {
                folder = assetFolder, normal = JsonUtility.ToJson(level), other = JsonUtility.ToJson(other),
                normalGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level)),
                otherGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(other)),
                processId = System.Diagnostics.Process.GetCurrentProcess().Id
            };
            window.SetLevel(null);
            string levelPath = AssetDatabase.GetAssetPath(level);
            Resources.UnloadAsset(level);
            level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(levelPath);
            Check(JsonUtility.ToJson(level) == state.normal, "저장·언로드·재로딩 전체 필드 동일");
            File.WriteAllText(StateFile, JsonUtility.ToJson(state, true));
        }

        public static void Restart()
        {
            Results.Clear();
            SavedState state = JsonUtility.FromJson<SavedState>(File.ReadAllText(StateFile));
            Check(state.processId != System.Diagnostics.Process.GetCurrentProcess().Id, "별도 Editor 프로세스 재시작");
            LevelDefinition a = AssetDatabase.LoadAssetAtPath<LevelDefinition>(state.folder + "/Normal.asset");
            LevelDefinition b = AssetDatabase.LoadAssetAtPath<LevelDefinition>(state.folder + "/Other.asset");
            Check(JsonUtility.ToJson(a) == state.normal && JsonUtility.ToJson(b) == state.other, "정상·오류 에셋 재시작 후 전체 데이터 동일");
            Check(AssetDatabase.AssetPathToGUID(state.folder + "/Normal.asset") == state.normalGuid &&
                AssetDatabase.AssetPathToGUID(state.folder + "/Other.asset") == state.otherGuid, "에셋 GUID 보존");
            Check(LevelDefinitionValidator.Validate(a).Count >= 3 && LevelDefinitionValidator.Validate(b).Count == 0, "재시작 후 정상·오류 검사 유지");
            const string prefix = "Assets/__LevelEditorVerification_";
            if (!state.folder.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(state.folder.Substring(prefix.Length), "N", out _))
                throw new InvalidOperationException("검증 폴더 경로 오류");
            Check(AssetDatabase.DeleteAsset(state.folder), "검증 에셋 정리");
            File.Move(StateFile, Folder + "/completed-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".json");
            File.WriteAllLines(Folder + "/restart-results.txt", Results);
        }

        private static void Pointer(EventType type, int row, int column)
        {
            LevelBoardView board = window.rootVisualElement.Q<LevelBoardView>();
            Vector2 position = board.LocalToWorld(new Vector2(column * 40 + 20, row * 40 + 20));
            Event input = new Event { type = type, button = 0, mousePosition = position };
            if (type == EventType.MouseDown)
            {
                using PointerDownEvent evt = PointerDownEvent.GetPooled(input);
                evt.target = board; board.SendEvent(evt);
            }
            else if (type == EventType.MouseUp)
            {
                using PointerUpEvent evt = PointerUpEvent.GetPooled(input);
                evt.target = board; board.SendEvent(evt);
            }
            else
            {
                using PointerMoveEvent evt = PointerMoveEvent.GetPooled(input);
                evt.target = board; board.SendEvent(evt);
            }
        }

        private static void Click(string name)
        {
            if (!ChooseMenuTool(window, name)) Submit(window.rootVisualElement.Q<Button>(name));
        }

        // 기존 인수 시나리오의 도구 이름을 새 실제 드롭다운 조작으로 연결한다.
        internal static bool ChooseMenuTool(LevelEditorWindow target, string tool)
        {
            string menuName = null;
            string choice = null;
            if (tool.StartsWith("tool-Fixed-")) { menuName = "menu-normal"; choice = "고정 " + (int.Parse(tool.Substring(11)) + 1); }
            else if (tool == "tool-Random") { menuName = "menu-normal"; choice = "무작위 ?"; }
            else if (tool == "tool-Activate" || tool == "tool-Deactivate") { menuName = "menu-board"; choice = tool == "tool-Activate" ? "칸 활성화" : "칸 비활성화"; }
            else if (tool == "tool-Erase") { Submit(target.rootVisualElement.Q<Button>("erase-layer")); return true; }
            else if (tool.StartsWith("place-"))
            {
                string[] parts = tool.Split('-');
                int kind = int.Parse(parts[2]);
                switch (parts[1])
                {
                    case "Block": menuName = "menu-power"; choice = LevelPlacementRules.Name((InitialBlockKind)kind); break;
                    case "Obstacle": menuName = kind == (int)ObstacleKind.Generator ? "menu-device" : "menu-obstacle"; choice = LevelPlacementRules.Name((ObstacleKind)kind); break;
                    case "Cover": menuName = "menu-cover"; choice = kind == 0 ? "거미줄" : "우주 곰팡이"; break;
                    case "Dust": menuName = "menu-floor"; choice = "먼지"; break;
                }
            }
            if (menuName == null) return false;
            Button placementTab = target.rootVisualElement.Q<Button>("tool-tab-0");
            if (placementTab != null && !placementTab.ClassListContains("active")) Submit(placementTab);
            PopupField<string> menu = target.rootVisualElement.Q<PopupField<string>>(menuName);
            if (menu == null || !menu.enabledInHierarchy || !menu.choices.Contains(choice)) throw new InvalidOperationException("메뉴 도구 사용 불가: " + tool);
            menu.value = choice;
            return true;
        }

        private static void Submit(Button button)
        {
            if (button == null) throw new InvalidOperationException("버튼을 찾을 수 없습니다.");
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled();
            evt.target = button; button.SendEvent(evt);
        }

        private static void SetInt(LevelDefinition target, string path, int value)
        {
            using SerializedObject data = new SerializedObject(target);
            data.FindProperty(path).intValue = value;
            data.ApplyModifiedProperties();
        }

        private static void SetBlock(SerializedProperty block, int row, int column, int kind, int color)
        {
            block.FindPropertyRelative("coordinate.row").intValue = row;
            block.FindPropertyRelative("coordinate.column").intValue = column;
            block.FindPropertyRelative("kind").intValue = kind;
            block.FindPropertyRelative("fixedColor").intValue = color;
        }

        private static void Capture(string name)
        {
            Rect position = window.position;
            int width = Mathf.RoundToInt(position.width);
            int height = Mathf.RoundToInt(position.height);
            Color[] pixels = UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(position.position, width, height);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.SetPixels(pixels); texture.Apply();
            File.WriteAllBytes(Folder + "/" + name, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            Check(File.Exists(Folder + "/" + name), "실제 창 캡처 저장: " + name);
        }

        private static void Check(bool condition, string description)
        {
            Results.Add((condition ? "PASS " : "FAIL ") + description);
            File.WriteAllLines(Folder + "/ui-progress.txt", Results);
            if (!condition) throw new InvalidOperationException(description);
            Debug.Log("[LevelEditorVerification] " + description);
        }
    }
}
