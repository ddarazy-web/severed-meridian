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
    public static partial class LevelFlowVerification
    {
        private static LevelEditorWindow window;
        private static IEnumerator sequence;
        private static double nextTick;
        private sealed class FocusWindow : EditorWindow { }

        public static void Start()
        {
            try
            {
                Setup(); VerifyData();
                level = CreateFlowLevel(folder + "/UI.asset");
                SetInt(level, "levelNumber", 54002);
                window = ScriptableObject.CreateInstance<LevelEditorWindow>();
                window.position = new Rect(60, 60, 1160, 780);
                window.titleContent = new GUIContent("4단계 흐름·연결 검증");
                window.ShowUtility(); window.SetLevel(level); window.Focus();
                sequence = RunUI();
                EditorApplication.update += Tick;
            }
            catch (Exception exception) { Results.Add("FAIL " + exception); Debug.LogException(exception); Finish(1); }
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.2;
            try
            {
                if (sequence.MoveNext()) return;
                SaveState(); Finish(0);
            }
            catch (Exception exception) { Results.Add("FAIL " + exception); Debug.LogException(exception); Finish(1); }
        }
        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            File.WriteAllLines(Evidence + "/ui-results.txt", Results);
            if (window != null) window.Close();
            if (code != 0 && folder != null) Cleanup(folder);
            EditorApplication.Exit(code);
        }

        private static IEnumerator RunUI()
        {
            yield return null;
            Check(window.rootVisualElement.Q<LevelBoardView>().panel != null, "실제 Editor 패널 연결");
            string beforeTabs = JsonUtility.ToJson(level);
            Check(window.rootVisualElement.styleSheets.count > 0, "에디터 USS 스타일 연결");
            Check(window.rootVisualElement.Q("placement-page").resolvedStyle.display == DisplayStyle.Flex &&
                window.rootVisualElement.Q("flow-page").resolvedStyle.display == DisplayStyle.None, "배치 탭만 기본 표시");
            Check(!window.rootVisualElement.Q<Foldout>("validation-drawer").value, "검사 결과 기본 접힘");
            Click("tool-tab-2"); yield return null;
            Check(window.rootVisualElement.Q("used-page").resolvedStyle.display == DisplayStyle.Flex &&
                window.rootVisualElement.Q("placement-page").resolvedStyle.display == DisplayStyle.None, "목록 탭 분리");
            Click("inspector-tab-1"); yield return null;
            Check(window.rootVisualElement.Q("level-settings-page").resolvedStyle.display == DisplayStyle.Flex &&
                window.rootVisualElement.Q("selection-page").resolvedStyle.display == DisplayStyle.None, "레벨 설정 탭 분리");
            Click("inspector-tab-0"); Click("tool-tab-1"); yield return null;
            Check(window.rootVisualElement.Q("flow-page").resolvedStyle.display == DisplayStyle.Flex &&
                JsonUtility.ToJson(level) == beforeTabs, "탭 이동은 레벨 데이터 변경 없음");
            Check(window.rootVisualElement.Q<PopupField<string>>("menu-flow") != null && window.rootVisualElement.Q<PopupField<string>>("menu-terrain") != null, "카테고리 흐름·지형 메뉴");
            Menu("menu-flow", "중력 →"); yield return null;
            window.rootVisualElement.Q<Toggle>("gravity-rectangle").value = true;
            Pointer(EventType.MouseDown, 20, 20); Pointer(EventType.MouseDrag, 100, 60);
            Check(level.Flow.Gravity.Count == 0 && Overlay.IsDragging, "사각 구역 미리보기는 원본 미변경");
            Pointer(EventType.MouseUp, 100, 60); yield return null;
            Check(level.Flow.Gravity.Count == 6 && level.Flow.Gravity.All(item => item.Direction == GravityDirection.Right), "사각 구역 6칸 중력 적용");
            Undo.PerformUndo(); yield return null;
            Check(level.Flow.Gravity.Count == 0, "구역 한 번 Undo");
            Undo.PerformRedo(); yield return null;
            Check(level.Flow.Gravity.Count == 6, "구역 Redo");
            window.rootVisualElement.Q<Toggle>("gravity-rectangle").value = false;
            Pointer(EventType.MouseDown, 20, 180); Pointer(EventType.MouseUp, 140, 180); yield return null;
            Check(level.Flow.Gravity.Count == 10, "빠른 드래그 중력 칠하기");
            Menu("menu-flow", "직접 경로"); yield return null;
            CellClick(2, 2); CellClick(2, 3); CellClick(3, 3);
            Check(level.Flow.Paths.Count == 0 && Overlay.DraftCount == 3, "직접 경로 클릭 미리보기");
            Click("complete-flow"); yield return null;
            Check(level.Flow.Paths.Count == 3 && level.Flow.Paths.Last().IsEnd, "경로 완료·명시 끝 칸");
            CellClick(6, 1); CellClick(6, 2); Key(KeyCode.Escape);
            Check(Overlay.DraftCount == 0 && level.Flow.Paths.Count == 3, "경로 Esc 취소");
            CellClick(6, 1); CellClick(6, 2); Click("save-level");
            Check(Overlay.DraftCount == 0 && level.Flow.Paths.Count == 3, "저장 시 미완료 경로 취소");
            window.Focus(); yield return null;
            Check(EditorWindow.focusedWindow == window, "경로 입력 창 실제 포커스");
            CellClick(6, 1); CellClick(6, 2);
            FocusWindow focus = ScriptableObject.CreateInstance<FocusWindow>();
            focus.position = new Rect(1230, 60, 180, 160); focus.ShowUtility(); focus.Focus(); yield return null; yield return null;
            Check(EditorWindow.focusedWindow == focus && Overlay.DraftCount == 0, "포커스 상실 경로 취소");
            focus.Close(); window.Focus(); yield return null;
            Menu("menu-terrain", "고철 벽"); yield return null;
            Pointer(EventType.MouseDown, 240, 100); Pointer(EventType.MouseUp, 240, 180); yield return null;
            Check(level.Flow.Walls.Count == 3 && level.InitialBlocks.Count == 0, "경계 드래그 벽·블록 입력 분리");
            Undo.PerformUndo(); yield return null;
            Check(level.Flow.Walls.Count == 0, "벽 드래그 한 번 Undo");
            Undo.PerformRedo(); yield return null;
            Menu("menu-flow", "통로 입구"); yield return null;
            CellClick(5, 1); yield return null;
            Check(level.Flow.Portals.Count == 1 && !level.Flow.Portals[0].HasExit, "UI 입구만 생성");
            Click("select-portal-exit"); yield return null;
            CellClick(1, 8); yield return null;
            Check(level.Flow.Portals[0].HasExit && level.Flow.Portals[0].Exit.Equals(C(1, 8)), "UI 출구 지정");
            Menu("menu-flow", "흐름 선택"); yield return null;
            CellClick(1, 8); Click("remove-portal-exit"); yield return null;
            Check(!level.Flow.Portals[0].HasExit, "UI 출구만 삭제·입구 보존");
            Undo.PerformUndo(); yield return null;
            Menu("menu-flow", "도착 바닥"); yield return null;
            CellClick(8, 8); yield return null;
            Check(level.Flow.Arrivals.Contains(C(8, 8)), "UI 도착 바닥 배치");
            LevelEditorVerification.ChooseMenuTool(window, "place-Obstacle-5"); yield return null;
            BoardClick(6, 1); yield return null;
            LevelEditorVerification.ChooseMenuTool(window, "place-Obstacle-0"); yield return null;
            BoardClick(6, 6); yield return null;
            Check(level.Obstacles.Count == 2 && Overlay.pickingMode == PickingMode.Ignore, "기존 장애물 도구 복귀·오버레이 입력 해제");
            Menu("menu-flow", "흐름 선택"); yield return null;
            CellClick(6, 1); yield return null;
            Click("add-generator-target"); yield return null;
            CellClick(6, 6); yield return null;
            Check(level.Connections.Count == 1, "UI 발전기 대상 선택");
            Click("edit-wire-0"); yield return null;
            for (int column = 3; column <= 6; column++)
            { Pointer(EventType.MouseDown, column * 40, 240); Pointer(EventType.MouseUp, column * 40, 240); }
            Check(Overlay.DraftCount == 4 && level.Connections[0].Vertices.Count == 0, "전선 꼭짓점 미리보기");
            Click("complete-flow"); yield return null;
            Check(level.Connections[0].Vertices.Count == 4 && !Has(LevelValidationCode.InvalidWire), "UI 전선 확정");
            window.rootVisualElement.Q<Foldout>("flow-connections").value = true; yield return null;
            Click("flow-connection-0"); yield return null;
            Check(Overlay.HighlightConnection == 0 && window.rootVisualElement.Q<Button>("edit-wire-0") != null, "연결 목록 선택·오른쪽 편집 연동");
            Click("validate-level"); yield return null;
            Check(window.rootVisualElement.Q<Label>("validation-status").text.Contains("실제 낙하"), "구조 검사 한계 표시");
            window.position = new Rect(60, 60, 1160, 780); yield return null; yield return null;
            Check(window.position.width >= 1100 && window.position.height >= 700, "넓은 창 실제 크기");
            Capture("flow-wide.png");
            int initialPhase = (int)(EditorApplication.timeSinceStartup / 3) % 2;
            double phaseDeadline = EditorApplication.timeSinceStartup + 4;
            while ((int)(EditorApplication.timeSinceStartup / 3) % 2 == initialPhase && EditorApplication.timeSinceStartup < phaseDeadline) yield return null;
            yield return null;
            Check((int)(EditorApplication.timeSinceStartup / 3) % 2 != initialPhase, "흐름·전선 강조 주기 전환");
            Capture("flow-alternate-highlight.png");
            window.position = new Rect(60, 60, 680, 480); yield return null;
            Check(window.rootVisualElement.Q<ScrollView>("board-scroll").horizontalScroller.highValue > 0, "좁은 창 보드 가로 스크롤");
            Capture("flow-narrow.png");
            window.rootVisualElement.Q<Foldout>("validation-drawer").value = false;
            window.rootVisualElement.Q<Toggle>("toggle-inspector").value = false;
            window.rootVisualElement.Q<Toggle>("toggle-tools").value = false;
            yield return null;
            Check(window.rootVisualElement.Q("inspector-scroll").resolvedStyle.display == DisplayStyle.None &&
                window.rootVisualElement.Q("tool-scroll").resolvedStyle.display == DisplayStyle.None &&
                window.rootVisualElement.Q("board-scroll").resolvedStyle.width > 600, "좁은 창 패널 접기 보드 공간 확보");
            Capture("flow-board-focus.png");
            window.rootVisualElement.Q<Toggle>("toggle-inspector").value = true;
            window.rootVisualElement.Q<Toggle>("toggle-tools").value = true;
            window.position = new Rect(60, 60, 1160, 780); yield return null;
            Click("tool-tab-0"); yield return null;
            Capture("placement-styled.png");
            Click("tool-tab-2"); yield return null;
            Capture("used-styled.png");
            Menu("menu-flow", "직접 경로"); yield return null;
            CellClick(9, 0); CellClick(9, 1);
            SetInt(level, "moveCount", 31); yield return null;
            Check(Overlay.DraftCount == 0, "외부 원본 변경 시 경로 미리보기 취소");
            CellClick(9, 0); CellClick(9, 1);
            LevelDefinition other = CreateFlowLevel(folder + "/Other.asset"); SetInt(other, "levelNumber", 54003);
            window.SetLevel(other); yield return null;
            Check(Overlay.DraftCount == 0 && Overlay.Tool == FlowTool.None, "레벨 전환 시 입력·도구 초기화");
            window.SetLevel(level); yield return null;
            IEnumerator additional = VerifyRemainingUI();
            while (additional.MoveNext()) yield return additional.Current;
            Click("save-level");
            Check(!EditorUtility.IsDirty(level), "UI 최종 저장");
        }

        private static IEnumerator VerifyRemainingUI()
        {
            Menu("menu-flow", "중력 ↑"); yield return null;
            string original = JsonUtility.ToJson(level);
            Pointer(EventType.MouseDown, 380, 380);
            Overlay.ReleasePointer(PointerId.mousePointerId); yield return null;
            Pointer(EventType.MouseUp, 380, 380);
            Check(!Overlay.IsDragging && JsonUtility.ToJson(level) == original, "흐름 포인터 캡처 상실 원자적 취소");
            Menu("menu-flow", "직접 경로"); yield return null;
            CellClick(9, 0); CellClick(9, 1);
            LevelEditorVerification.ChooseMenuTool(window, "tool-Random"); yield return null;
            Check(Overlay.DraftCount == 0 && Overlay.Tool == FlowTool.None && JsonUtility.ToJson(level) == original, "일반 배치 도구 전환 시 경로 취소");
            Menu("menu-flow", "직접 경로"); yield return null;
            CellClick(9, 0); CellClick(9, 1); Key(KeyCode.Backspace);
            Check(Overlay.DraftCount == 1, "경로 한 단계 되돌리기");
            CellClick(9, 1); Key(KeyCode.Return); yield return null;
            Check(level.Flow.Paths.Any(item => item.Coordinate.Equals(C(9, 1)) && item.IsEnd), "Enter 경로 확정");
            Undo.PerformUndo(); yield return null;
            Menu("menu-flow", "흐름 선택"); yield return null;
            CellClick(1, 3); yield return null;
            Check(window.rootVisualElement.Q<Button>("confirm-merge") != null, "합류 칸 속성 표시");
            Click("confirm-merge"); yield return null;
            FlowMerge merge = level.Flow.Merges.Single(item => item.Coordinate.Equals(C(1, 3)));
            BoardCoordinate[] before = merge.Sources.ToArray();
            Click("merge-0-1"); yield return null;
            Check(level.Flow.Merges.Single(item => item.Coordinate.Equals(C(1, 3))).Sources[1].Equals(before[0]), "합류 순서 아래로 이동");
            Undo.PerformUndo(); yield return null;
            Check(level.Flow.Merges.Single(item => item.Coordinate.Equals(C(1, 3))).Sources.SequenceEqual(before), "합류 순서 Undo");
            CellClick(6, 1); yield return null;
            Click("edit-wire-0"); yield return null;
            Pointer(EventType.MouseDown, 120, 240); Pointer(EventType.MouseUp, 120, 240);
            SetInt(level, "moveCount", 32); yield return null;
            Check(Overlay.DraftCount == 0 && Overlay.WireIndex == -1, "전선 입력 중 외부 변경 시 오래된 연결 선택 해제");
            original = JsonUtility.ToJson(level);
            Click("complete-flow"); yield return null;
            Check(JsonUtility.ToJson(level) == original, "해제된 전선 대상에 적용 금지");
            Menu("menu-flow", "흐름 선택"); yield return null;
            CellClick(6, 1); Click("edit-wire-0"); yield return null;
            Pointer(EventType.MouseDown, 120, 240); Pointer(EventType.MouseUp, 120, 240);
            Pointer(EventType.MouseDown, 160, 240); Pointer(EventType.MouseUp, 160, 240);
            Click("save-partial-wire"); yield return null;
            Check(level.Connections[0].Vertices.Count == 2 && Has(LevelValidationCode.InvalidWire), "전선 중간 경로 명시적 기록·미완성 오류");
            Click("save-level");
            Check(!EditorUtility.IsDirty(level), "미완성 전선 저장 허용");
            LevelDefinition partialWire = UnityEngine.Object.Instantiate(level);
            AssetDatabase.CreateAsset(partialWire, folder + "/PartialWire.asset");
            using (SerializedObject partialData = new SerializedObject(partialWire))
            { partialData.FindProperty("levelNumber").intValue = 54006; partialData.ApplyModifiedPropertiesWithoutUndo(); }
            AssetDatabase.SaveAssetIfDirty(partialWire);
            Undo.PerformUndo(); yield return null;
            Check(level.Connections[0].Vertices.Count == 4, "전선 중간 기록 한 번 Undo");
            string legacyPath = folder + "/UILegacy2.asset";
            File.Copy(Fixtures + "/Version2Invalid.txt", legacyPath);
            AssetDatabase.ImportAsset(legacyPath, ImportAssetOptions.ForceSynchronousImport);
            LevelDefinition legacy = AssetDatabase.LoadAssetAtPath<LevelDefinition>(legacyPath);
            string legacyBefore = JsonUtility.ToJson(legacy);
            window.SetLevel(legacy); yield return null;
            Check(!window.rootVisualElement.Q<PopupField<string>>("menu-flow").enabledInHierarchy && !window.rootVisualElement.Q<PopupField<string>>("menu-terrain").enabledInHierarchy &&
                JsonUtility.ToJson(legacy) == legacyBefore && !EditorUtility.IsDirty(legacy), "버전 2 UI 읽기 비파괴·신규 도구 잠금");
            Click("upgrade-level"); yield return null;
            Check(legacy.SchemaVersion == LevelDefinition.CurrentSchemaVersion && window.rootVisualElement.Q<PopupField<string>>("menu-flow").enabledInHierarchy, "버전 2 UI 명시 전환 후 흐름 도구 해제");
            Undo.PerformUndo(); yield return null;
            Check(JsonUtility.ToJson(legacy) == legacyBefore && !window.rootVisualElement.Q<PopupField<string>>("menu-flow").enabledInHierarchy, "UI 전환 Undo·편집 잠금 복구");
            Undo.PerformRedo(); yield return null;
            LevelDefinition invalidFlow = AssetDatabase.LoadAssetAtPath<LevelDefinition>(folder + "/InvalidFlow.asset");
            window.SetLevel(invalidFlow); yield return null;
            string invalidBefore = JsonUtility.ToJson(invalidFlow);
            Click("validate-level"); yield return null;
            Button wallIssue = window.rootVisualElement.Q<ScrollView>("validation-issues").Query<Button>().ToList().First(button => button.text.Contains("flow.walls"));
            Submit(wallIssue); yield return null;
            Check(Overlay.HighlightWall.HasValue && Overlay.Tool == FlowTool.Select, "벽 오류 클릭 시 경계 강조·흐름 도구 동기화");
            Button wireIssue = window.rootVisualElement.Q<ScrollView>("validation-issues").Query<Button>().ToList().First(button => button.text.Contains("connections.Array.data[0]"));
            Submit(wireIssue); yield return null;
            Check(Overlay.HighlightConnection == 0, "연결 오류 클릭 시 전선 대상 강조");
            Button outsideIssue = window.rootVisualElement.Q<ScrollView>("validation-issues").Query<Button>().ToList().First(button => button.text.Contains("flow.paths"));
            Submit(outsideIssue); yield return null;
            Check(window.rootVisualElement.Q<Label>("operation-status").text.Contains("flow.paths") && JsonUtility.ToJson(invalidFlow) == invalidBefore, "범위 밖 경로 오류 위치 안내·원본 보존");
            window.SetLevel(level); yield return null;
            Menu("menu-flow", "직접 경로"); yield return null;
            CellClick(9, 0); CellClick(9, 1);
            LevelFlowOverlay detached = Overlay;
            original = JsonUtility.ToJson(level);
            window.Close(); yield return null;
            Check(detached.panel == null && detached.DraftCount == 0 && JsonUtility.ToJson(level) == original, "창 닫기 시 미리보기 취소·오버레이 일정 분리");
            window = ScriptableObject.CreateInstance<LevelEditorWindow>();
            window.ShowUtility(); window.SetLevel(level); window.Focus(); yield return null;
            Check(window.CurrentLevel == level && Overlay.Tool == FlowTool.None && JsonUtility.ToJson(level) == original, "창 재열기 원본 설정 유지");
        }

        private static LevelFlowOverlay Overlay => window.rootVisualElement.Q<LevelFlowOverlay>();
        private static void Menu(string name, string value)
        {
            int page = name == "menu-flow" || name == "menu-terrain" ? 1 : 0;
            if (window.rootVisualElement.Q("tool-tab-" + page) is Button tab && !tab.ClassListContains("active")) Submit(tab);
            window.rootVisualElement.Q<PopupField<string>>(name).value = value;
        }
        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("버튼 사용 불가: " + name);
            Submit(button);
        }
        private static void Submit(Button button) { using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt); }
        private static void CellClick(int row, int column) { Pointer(EventType.MouseDown, column * 40 + 20, row * 40 + 20); Pointer(EventType.MouseUp, column * 40 + 20, row * 40 + 20); }
        private static void BoardClick(int row, int column)
        {
            VisualElement board = window.rootVisualElement.Q<LevelBoardView>();
            SendPointer(board, EventType.MouseDown, new Vector2(column * 40 + 20, row * 40 + 20));
            SendPointer(board, EventType.MouseUp, new Vector2(column * 40 + 20, row * 40 + 20));
        }
        private static void Pointer(EventType type, float x, float y) => SendPointer(Overlay, type, new Vector2(x, y));
        private static void SendPointer(VisualElement target, EventType type, Vector2 point)
        {
            Event input = new Event { type = type, button = 0, mousePosition = target.LocalToWorld(point) };
            if (type == EventType.MouseDown) { using PointerDownEvent evt = PointerDownEvent.GetPooled(input); evt.target = target; target.SendEvent(evt); }
            else if (type == EventType.MouseUp) { using PointerUpEvent evt = PointerUpEvent.GetPooled(input); evt.target = target; target.SendEvent(evt); }
            else { using PointerMoveEvent evt = PointerMoveEvent.GetPooled(input); evt.target = target; target.SendEvent(evt); }
        }
        private static void Key(KeyCode key)
        {
            using KeyDownEvent evt = KeyDownEvent.GetPooled('\0', key, EventModifiers.None); evt.target = Overlay; Overlay.SendEvent(evt);
        }
        private static void Capture(string name)
        {
            Rect position = window.position;
            int width = Mathf.RoundToInt(position.width), height = Mathf.RoundToInt(position.height);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(position.position, width, height));
            texture.Apply(); File.WriteAllBytes(Evidence + "/" + name, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            Check(File.Exists(Evidence + "/" + name), "실제 창 캡처 " + name);
        }
    }
}
