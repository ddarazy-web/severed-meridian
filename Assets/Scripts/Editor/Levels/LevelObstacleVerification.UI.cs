using System;
using System.Collections;
using System.IO;
using System.Linq;
using Board;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class LevelObstacleVerification
    {
        private static LevelEditorWindow window;
        private static IEnumerator sequence;
        private static double nextTick;
        private sealed class FocusWindow : EditorWindow { }

        public static void Start()
        {
            try
            {
                Setup();
                VerifyData();
                window = ScriptableObject.CreateInstance<LevelEditorWindow>();
                window.position = new Rect(60, 60, 1160, 780);
                window.titleContent = new GUIContent("3단계 레벨 에디터 검증");
                window.ShowUtility();
                window.SetLevel(level);
                window.Focus();
                sequence = RunUI();
                EditorApplication.update += Tick;
            }
            catch (Exception exception)
            {
                Results.Add("FAIL " + exception);
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.15;
            try
            {
                if (sequence.MoveNext()) return;
                SaveState();
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
            File.WriteAllLines(Evidence + "/ui-results.txt", Results);
            if (window != null) window.Close();
            if (code != 0 && assetFolder != null) Cleanup(assetFolder);
            EditorApplication.Exit(code);
        }

        private static IEnumerator RunUI()
        {
            yield return null; yield return null;
            LevelBoardView board = window.rootVisualElement.Q<LevelBoardView>();
            Check(board.panel != null && board.Children().OfType<Label>().Count() == 100, "실제 UI 패널·100칸 표시");
            Check(EditorApplication.ExecuteMenuItem("Match/레벨 에디터"), "Match 메뉴 실행");
            window.Focus(); yield return null;
            Layer("장애물/장치"); yield return null;
            Check(window.rootVisualElement.Q<PopupField<string>>("placement-layer").Q(className: "unity-base-field__input").worldBound.width > 70,
                "편집 층 선택 입력이 좁은 도구 패널에서도 보임");
            Click("place-Obstacle-0");
            Pointer(EventType.MouseDown, 0, 0); Pointer(EventType.MouseDrag, 0, 3); Pointer(EventType.MouseDrag, 0, 1);
            Check(level.Obstacles.Count == 0 && board.IsDragging, "실제 드래그는 미리보기이며 원본 무변경");
            Pointer(EventType.MouseUp, 0, 3); yield return null;
            Check(level.Obstacles.Count == 4, "실제 빠른 드래그·반복 방문 장애물 네 칸");
            Undo.PerformUndo(); yield return null;
            Check(level.Obstacles.Count == 0, "실제 장애물 드래그 한 번 Undo");
            Undo.PerformRedo(); yield return null;
            Check(level.Obstacles.Count == 4, "실제 장애물 드래그 Redo");
            SetBool(level, "board.cells.Array.data[12].isActive", false); yield return null;
            Click("place-Obstacle-1");
            Pointer(EventType.MouseDown, 1, 0); Pointer(EventType.MouseUp, 1, 3); yield return null;
            Check(level.Obstacles.Count == 7 && window.rootVisualElement.Q<Label>("operation-status").text.Contains("1개 제외"),
                "부분 적용 드래그: 비활성 제외 수와 이유 표시");
            Click("place-Obstacle-2"); CellClick(2, 0); yield return null;
            Click("place-Obstacle-3"); CellClick(2, 1); yield return null;
            Check(level.Obstacles.Any(item => item.Kind == ObstacleKind.Safe) && level.Obstacles.Any(item => item.Kind == ObstacleKind.ColorLock),
                "금고·자물쇠 실제 도구 배치");
            window.rootVisualElement.Q<IntegerField>("selected-durability").value = 3; yield return null;
            window.rootVisualElement.Q<PopupField<string>>("selected-color").value = "달토끼 4"; yield return null;
            Check(level.Obstacles.Last().Durability == 3 && level.Obstacles.Last().Color == RabbitColor.Type4, "자물쇠 단일 속성·색 UI 편집");
            Undo.PerformUndo(); yield return null;
            Check(level.Obstacles.Last().Color == RabbitColor.Type1 && level.Obstacles.Last().Durability == 3, "색 속성만 한 번 Undo");
            Undo.PerformRedo(); yield return null;
            window.rootVisualElement.Q<IntegerField>("selected-durability").value = 8; yield return null;
            Check(level.Obstacles.Last().Durability == 3, "속성 범위 밖 입력 거절");

            Layer("블록"); yield return null;
            Click("tool-Fixed-2"); CellClick(3, 0); yield return null;
            Click("place-Block-2"); CellClick(3, 1); yield return null;
            window.rootVisualElement.Q<PopupField<string>>("selected-rocketDirection").value = "세로 한 줄 ↕"; yield return null;
            Check(level.InitialBlocks.Last().RocketDirection == RocketDirection.Vertical, "로켓 방향 실제 속성 UI");
            foreach (int kind in new[] { 3, 4, 5 })
            {
                Click("place-Block-" + kind); CellClick(3, kind - 1); yield return null;
            }
            Check(level.InitialBlocks.Count == 5, "파워 네 종류 실제 배치");
            Layer("덮개"); yield return null;
            Click("place-Cover-0"); CellClick(3, 0); yield return null;
            window.rootVisualElement.Q<IntegerField>("selected-durability").value = 3; yield return null;
            Click("place-Cover-1"); CellClick(3, 1); yield return null;
            Check(level.Covers.Count == 2 && level.Covers[0].Durability == 3, "거미줄·곰팡이 실제 배치·내구도");
            Layer("먼지"); yield return null;
            Click("place-Dust-0"); Pointer(EventType.MouseDown, 3, 0); Pointer(EventType.MouseUp, 3, 1); yield return null;
            Check(level.Dust.Count == 2 && level.Covers.Count == 2 && level.InitialBlocks.Count == 5, "먼지·덮개·블록 3층 중첩");
            Layer("덮개"); yield return null;
            Click("tool-Select"); CellClick(3, 1); yield return null;
            Click("delete-placement"); yield return null;
            Check(level.Covers.Count == 1 && level.InitialBlocks[1].RocketDirection == RocketDirection.Vertical && level.Dust.Count == 2,
                "실제 곰팡이 삭제 내부 로켓 방향·먼지 보존");
            Undo.PerformUndo(); yield return null;
            Check(level.Covers.Count == 2, "덮개 삭제 Undo");

            Layer("장애물/장치"); yield return null;
            Click("place-Obstacle-4");
            Pointer(EventType.MouseMove, 5, 2); yield return null;
            Check(board.CellAt(new BoardCoordinate(6, 3)).resolvedStyle.borderTopWidth == 3, "2×2 네 칸 미리보기");
            int beforeCount = level.Obstacles.Count;
            Pointer(EventType.MouseDown, 5, 2); Pointer(EventType.MouseDrag, 6, 3); Pointer(EventType.MouseUp, 6, 3); yield return null;
            Check(level.Obstacles.Count == beforeCount, "2×2 드래그 설치 금지");
            CellClick(5, 2); yield return null;
            Check(level.Obstacles.Count == beforeCount + 1 && level.Obstacles.Last().Kind == ObstacleKind.Appliance, "2×2 클릭 설치");
            window.rootVisualElement.Q<IntegerField>("selected-durability").value = 9; yield return null;
            Click("tool-Select"); CellClick(6, 3); yield return null;
            Check(window.rootVisualElement.Q<IntegerField>("selected-durability").value == 9, "다른 점유 칸 선택도 같은 공유 속성");
            Click("move-obstacle"); CellClick(6, 2); yield return null;
            Check(level.Obstacles.Last().Coordinate.Equals(new BoardCoordinate(6, 2)) && level.Obstacles.Last().Durability == 9, "UI 2×2 자기 영역 중첩 이동");
            Undo.PerformUndo(); yield return null;
            Check(level.Obstacles.Last().Coordinate.Equals(new BoardCoordinate(5, 2)), "UI 이동 한 번 Undo");
            Undo.PerformRedo(); yield return null;
            Click("tool-Select"); CellClick(6, 2); yield return null;
            Click("move-obstacle"); CellClick(0, 0); yield return null;
            Check(level.Obstacles.Last().Coordinate.Equals(new BoardCoordinate(6, 2)) && board.MoveIndex == -1, "UI 충돌 이동 취소·원위치 유지");
            Click("tool-Select"); CellClick(6, 2); yield return null;
            Click("move-obstacle");
            using (KeyDownEvent escape = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
            { escape.target = board; board.SendEvent(escape); }
            Check(board.MoveIndex == -1, "Esc 이동 취소");
            Click("move-obstacle");
            FocusWindow focus = ScriptableObject.CreateInstance<FocusWindow>();
            focus.position = new Rect(1230, 60, 180, 160); focus.ShowUtility(); focus.Focus();
            yield return null; yield return null;
            Check(EditorWindow.focusedWindow == focus && board.MoveIndex == -1, "실제 창 포커스 상실 이동 취소");
            focus.Close(); window.Focus(); yield return null;

            Click("place-Obstacle-5"); CellClick(8, 7); yield return null;
            Check(level.Obstacles.Last().Kind == ObstacleKind.Generator && level.Obstacles.Last().RequiredCharge == 3,
                "발전기 2×2·기본 충전 3");
            window.rootVisualElement.Q<IntegerField>("selected-requiredCharge").value = 5; yield return null;
            Check(level.Obstacles.Last().RequiredCharge == 5 && window.rootVisualElement.Query<HelpBox>().ToList().Any(box => box.text.Contains("실제 충전")),
                "발전기 필요 충전량·게임 실행 미지원 안내");
            Click("tool-Select"); CellClick(9, 8); yield return null;
            Click("delete-placement"); yield return null;
            Check(!level.Obstacles.Any(item => item.Kind == ObstacleKind.Generator), "발전기 다른 점유 칸 전체 삭제");
            Undo.PerformUndo(); yield return null;
            Check(level.Obstacles.Last().Kind == ObstacleKind.Generator, "발전기 삭제 Undo");

            Click("tool-Select"); CellClick(8, 7); yield return null;
            Click("move-obstacle"); Click("save-level");
            Check(board.MoveIndex == -1, "저장으로 대기 중인 이동 취소");
            Button oldMove = window.rootVisualElement.Q<Button>("move-obstacle");
            Undo.IncrementCurrentGroup();
            using (SerializedObject edit = new SerializedObject(level))
            {
                edit.FindProperty("obstacles").MoveArrayElement(0, level.Obstacles.Count - 1);
                edit.ApplyModifiedProperties();
            }
            string reordered = JsonUtility.ToJson(level);
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled()) { submit.target = oldMove; oldMove.SendEvent(submit); }
            Check(board.MoveIndex == -1 && JsonUtility.ToJson(level) == reordered, "목록 순서 변경 직후 오래된 이동 버튼이 다른 본체를 선택하지 않음");
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null;
            Check(level.Obstacles.Last().Kind == ObstacleKind.Generator, "목록 순서 복원");
            SetBool(level, "board.cells.Array.data[87].isActive", false); yield return null;
            Layer("블록"); yield return null;
            Click("validate-level"); yield return null;
            Button obstacleError = window.rootVisualElement.Q<ScrollView>("validation-issues").Query<Button>().ToList()
                .First(button => button.text.Contains("obstacles.Array"));
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled()) { submit.target = obstacleError; obstacleError.SendEvent(submit); }
            yield return null;
            Check(window.rootVisualElement.Q<PopupField<string>>("placement-layer").value == "장애물/장치" &&
                window.rootVisualElement.Q<IntegerField>("selected-requiredCharge")?.value == 5,
                "오류 클릭으로 올바른 층·2×2 본체 속성·도구 동기화");
            SetBool(level, "board.cells.Array.data[87].isActive", true); yield return null;
            using (SerializedObject edit = new SerializedObject(level))
            { edit.FindProperty("colors").arraySize = 3; edit.ApplyModifiedProperties(); }
            yield return null;
            Check(level.Obstacles.First(item => item.Kind == ObstacleKind.ColorLock).Color == RabbitColor.Type4 &&
                LevelDefinitionValidator.Validate(level).Any(issue => issue.Message.Contains("자물쇠 색")),
                "사용 색 변경 시 자물쇠 색 보존·오류 표시");
            using (SerializedObject edit = new SerializedObject(level))
            {
                SerializedProperty colors = edit.FindProperty("colors"); colors.arraySize = 5;
                for (int i = 0; i < 5; i++) colors.GetArrayElementAtIndex(i).intValue = i;
                edit.ApplyModifiedProperties();
            }
            yield return null;

            string snapshot = JsonUtility.ToJson(level);
            Click("place-Obstacle-0"); Pointer(EventType.MouseDown, 4, 7); Click("save-level"); Pointer(EventType.MouseUp, 4, 7); yield return null;
            Check(JsonUtility.ToJson(level) == snapshot && !board.IsDragging, "장애물 미완료 드래그 저장 시 취소");
            Click("place-Obstacle-0"); Pointer(EventType.MouseDown, 4, 7); Layer("먼지"); Pointer(EventType.MouseUp, 4, 7); yield return null;
            Check(JsonUtility.ToJson(level) == snapshot, "드래그 중 층 전환 취소");
            Layer("장애물/장치"); yield return null;
            Click("place-Obstacle-0"); Pointer(EventType.MouseDown, 4, 7);
            SetInt(level, "moveCount", 39); yield return null; Pointer(EventType.MouseUp, 4, 7);
            Check(level.Obstacles.Count == beforeCount + 2 && level.MoveCount == 39, "외부 수정은 미완료 드래그 취소·기존 본체 유지");

            // 새 버전 코드를 거치지 않고 저장된 원본을 다시 가져와 실제 전환 버튼을 검증한다.
            string oldPath = assetFolder + "/UILegacy.asset";
            File.Copy(Fixtures + "/Version1Normal.txt", oldPath);
            AssetDatabase.ImportAsset(oldPath, ImportAssetOptions.ForceSynchronousImport);
            LevelDefinition old = AssetDatabase.LoadAssetAtPath<LevelDefinition>(oldPath);
            window.SetLevel(old); yield return null;
            Check(old.SchemaVersion == 1 && !EditorUtility.IsDirty(old) && !window.rootVisualElement.Q<PopupField<string>>("menu-normal").enabledInHierarchy,
                "실제 버전 1 창: 읽기만으로 변경 없음·칠하기 잠금");
            Click("upgrade-level"); yield return null;
            Check(old.SchemaVersion == LevelDefinition.CurrentSchemaVersion && window.rootVisualElement.Q<PopupField<string>>("menu-normal").enabledInHierarchy, "실제 명시적 전환 버튼 편집 해제");
            Undo.PerformUndo(); yield return null;
            Check(old.SchemaVersion == 1 && window.rootVisualElement.Q<Button>("upgrade-level") != null, "UI 전환 Undo 후 안내 복원");
            Undo.PerformRedo(); yield return null;
            Click("save-level");
            window.SetLevel(level); yield return null;
            IEnumerator menuChecks = VerifyToolMenus();
            while (menuChecks.MoveNext()) yield return menuChecks.Current;
            Layer("장애물/장치"); yield return null;
            Click("tool-Select"); CellClick(8, 7); yield return null;
            Click("validate-level"); yield return null;
            Check(window.rootVisualElement.Q<Label>("validation-status").text.Contains("미지원"), "검사 미지원 범위 표시");
            window.position = new Rect(10, 10, 1000, 780);
            window.rootVisualElement.Q<Foldout>("validation-drawer").value = false;
            Click("tool-tab-0"); yield return null; yield return null;
            Capture("editor-styled.png");
            window.position = new Rect(60, 60, 1160, 780); yield return null; yield return null;
            Capture("obstacles-wide.png");
            window.position = new Rect(60, 60, 680, 480); yield return null; yield return null;
            ScrollView scroll = window.rootVisualElement.Q<ScrollView>("board-scroll");
            scroll.ScrollTo(board.CellAt(new BoardCoordinate(9, 9))); yield return null;
            Check(scroll.horizontalScroller.highValue > 0 && scroll.verticalScroller.highValue > 0, "좁은 창 2축 스크롤");
            Capture("obstacles-narrow.png");
            Click("save-level");
            Check(!EditorUtility.IsDirty(level), "UI 최종 저장");
        }

        private static IEnumerator VerifyToolMenus()
        {
            LevelBoardView board = window.rootVisualElement.Q<LevelBoardView>();
            string original = JsonUtility.ToJson(level);
            foreach (string name in new[] { "menu-board", "menu-normal", "menu-power", "menu-obstacle", "menu-cover", "menu-floor", "menu-device" })
                Check(window.rootVisualElement.Q<PopupField<string>>(name) != null, "카테고리 드롭다운: " + name);
            Check(window.rootVisualElement.Q<Button>("place-Obstacle-0") == null &&
                window.rootVisualElement.Q<PopupField<string>>("menu-power").choices.Count == 5 &&
                !window.rootVisualElement.Q<PopupField<string>>("menu-obstacle").choices.Contains("고장 난 발전기") &&
                window.rootVisualElement.Q<PopupField<string>>("menu-device").choices.Contains("고장 난 발전기"),
                "개별 배치 버튼 제거·파워/장애물/장치 카테고리 구분");
            int total = level.Obstacles.Count + level.Covers.Count + level.Dust.Count;
            Check(window.rootVisualElement.Q<Label>("used-count").text.StartsWith(total + "개 배치") &&
                window.rootVisualElement.Q<Foldout>("used-Obstacle-4").text.EndsWith("1개") &&
                window.rootVisualElement.Q<Foldout>("used-Obstacle-5").text.EndsWith("1개"),
                "사용 목록은 현재 배치 수이며 2×2는 본체 한 개로 집계");
            Click("tool-tab-2"); Click("inspector-tab-1"); yield return null;
            window.rootVisualElement.Q<Foldout>("used-Obstacle-4").value = true; yield return null;
            Click("used-item-Obstacle-4-0"); yield return null;
            Check(window.rootVisualElement.Q("selection-page").resolvedStyle.display == DisplayStyle.Flex &&
                window.rootVisualElement.Q("level-settings-page").resolvedStyle.display == DisplayStyle.None, "목록 선택 시 레벨 설정에서 선택 속성으로 전환");
            Check(board.Brush == LevelBrush.Select && board.Layer == PlacementLayer.Obstacle &&
                window.rootVisualElement.Q<IntegerField>("selected-durability").value == 9 && JsonUtility.ToJson(level) == original,
                "사용 목록 클릭으로 2×2 본체 선택·원본 무변경");
            Check(window.rootVisualElement.Q<Foldout>("used-Obstacle-4").value, "목록 탐색·Refresh 후 펼침 상태 유지");
            window.rootVisualElement.Q<Foldout>("used-Cover-0").value = true; yield return null;
            Click("used-item-Cover-0-0"); yield return null;
            Check(board.Layer == PlacementLayer.Cover && window.rootVisualElement.Q<PopupField<string>>("placement-layer").value == "덮개" &&
                window.rootVisualElement.Q<IntegerField>("selected-durability").value == 3 && JsonUtility.ToJson(level) == original,
                "덮개 목록 선택으로 편집 층·속성 자동 전환");
            Click("delete-placement"); yield return null;
            Check(window.rootVisualElement.Q<Foldout>("used-Cover-0") == null &&
                window.rootVisualElement.Q<Label>("used-count").text.StartsWith((total - 1) + "개 배치"), "마지막 종류 삭제 시 목록과 개수 자동 갱신");
            Undo.PerformUndo(); yield return null;
            Check(JsonUtility.ToJson(level) == original && window.rootVisualElement.Q<Foldout>("used-Cover-0")?.value == true,
                "Undo로 사용 종류·개수·펼침 상태 복원");
            Undo.PerformRedo(); yield return null;
            Check(window.rootVisualElement.Q<Foldout>("used-Cover-0") == null, "Redo로 사용 목록 재갱신");
            Undo.PerformUndo(); yield return null;
            Undo.IncrementCurrentGroup();
            SetInt(level, "covers.Array.data[0].durability", 2); yield return null;
            Check(window.rootVisualElement.Q<Button>("used-item-Cover-0-0").text.Contains("내구 2"), "외부 Inspector 경로 변경을 사용 목록에 반영");
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo(); yield return null;

            LevelDefinition invalid = AssetDatabase.LoadAssetAtPath<LevelDefinition>(assetFolder + "/InvalidLayers.asset");
            string invalidBefore = JsonUtility.ToJson(invalid);
            window.SetLevel(invalid); yield return null;
            window.rootVisualElement.Q<Foldout>("used-Obstacle-3").value = true; yield return null;
            Click("used-item-Obstacle-3-2"); yield return null;
            Check(window.rootVisualElement.Q<Label>("operation-status").text.Contains("보드 밖") && JsonUtility.ToJson(invalid) == invalidBefore,
                "범위 밖 사용 기록도 목록에 보존·임의 좌표 보정 없음");
            LevelDefinition empty = LevelAssetOperations.CreateAtPath(assetFolder + "/EmptyMenu.asset");
            SetInt(empty, "levelNumber", 53003);
            window.SetLevel(empty); yield return null;
            Check(window.rootVisualElement.Q<Label>("used-count").text.Contains("아직") &&
                window.rootVisualElement.Q<VisualElement>("used-obstacles").Query<Foldout>().ToList().Count == 0,
                "레벨 전환 시 이전 목록 제거·빈 목록 안내");
            window.SetLevel(level); yield return null;
            Check(JsonUtility.ToJson(level) == original && window.rootVisualElement.Q<Label>("used-count").text.StartsWith(total + "개 배치"),
                "레벨 복귀 시 실제 배치에서 목록 재구성");
            window.rootVisualElement.Q<PopupField<string>>("menu-power").value = "달폭탄"; yield return null;
            Check(board.Layer == PlacementLayer.Block && board.Brush == LevelBrush.Placement && board.Placement.Kind == (int)InitialBlockKind.Bomb &&
                window.rootVisualElement.Q<PopupField<string>>("menu-obstacle").value == "선택…", "드롭다운 선택 시 해당 도구만 활성·층 자동 변경");
            Click("tool-Select"); yield return null;
            Check(window.rootVisualElement.Q<PopupField<string>>("menu-power").value == "선택…", "선택 도구 복귀 시 배치 메뉴 활성 표시 해제");
            window.position = new Rect(10, 10, 1000, 780); yield return null;
            Click("tool-tab-2"); yield return null;
            window.rootVisualElement.Q<Foldout>("used-Obstacle-0").value = true;
            window.rootVisualElement.Q<Foldout>("used-Obstacle-4").value = true;
            yield return null;
            ScrollView tools = window.rootVisualElement.Q<ScrollView>("tool-scroll");
            tools.ScrollTo(window.rootVisualElement.Q<Button>("used-item-Obstacle-4-0")); yield return null;
            Capture("menu-used-obstacles.png");
            tools.scrollOffset = Vector2.zero;
            window.rootVisualElement.Q<Foldout>("used-Obstacle-0").value = false;
            window.rootVisualElement.Q<Foldout>("used-Obstacle-4").value = false;
            yield return null;
        }

        private static void Layer(string name) => window.rootVisualElement.Q<PopupField<string>>("placement-layer").value = name;
        private static void Click(string name)
        {
            if (LevelEditorVerification.ChooseMenuTool(window, name)) return;
            Button button = window.rootVisualElement.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("버튼 사용 불가: " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled();
            evt.target = button; button.SendEvent(evt);
        }
        private static void CellClick(int row, int column)
        {
            Pointer(EventType.MouseDown, row, column); Pointer(EventType.MouseUp, row, column);
        }
        private static void Pointer(EventType type, int row, int column)
        {
            LevelBoardView board = window.rootVisualElement.Q<LevelBoardView>();
            Event input = new Event { type = type, button = 0, mousePosition = board.LocalToWorld(new Vector2(column * 40 + 20, row * 40 + 20)) };
            if (type == EventType.MouseDown)
            { using PointerDownEvent evt = PointerDownEvent.GetPooled(input); evt.target = board; board.SendEvent(evt); }
            else if (type == EventType.MouseUp)
            { using PointerUpEvent evt = PointerUpEvent.GetPooled(input); evt.target = board; board.SendEvent(evt); }
            else
            { using PointerMoveEvent evt = PointerMoveEvent.GetPooled(input); evt.target = board; board.SendEvent(evt); }
        }
        private static void Capture(string name)
        {
            Rect position = window.position;
            int width = Mathf.RoundToInt(position.width), height = Mathf.RoundToInt(position.height);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(position.position, width, height));
            texture.Apply(); File.WriteAllBytes(Evidence + "/" + name, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            Check(File.Exists(Evidence + "/" + name), "실제 창 캡처: " + name);
        }
    }
}
