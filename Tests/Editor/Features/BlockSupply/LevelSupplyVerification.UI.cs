using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class LevelSupplyVerification
    {
        private static LevelEditorWindow window;
        private static IEnumerator sequence;
        private static double nextTick;
        private static string originalClipboard;
        private static bool powerSupplyOnly;
        private sealed class FocusWindow : EditorWindow { }

        public static void StartPowerSupply()
        {
            powerSupplyOnly = true;
            Start();
        }

        public static void Start()
        {
            try
            {
                Prepare();
                originalClipboard = EditorGUIUtility.systemCopyBuffer;
                level = LevelAssetOperations.CreateAtPath(folder + "/UI.asset"); SetInt(level, "levelNumber", 55002);
                window = ScriptableObject.CreateInstance<LevelEditorWindow>(); window.titleContent = new GUIContent("5단계 공급·미션 검증");
                window.position = new Rect(10, 10, 1000, 780); window.ShowUtility(); window.SetLevel(level); window.Focus();
                sequence = RunUI(); EditorApplication.update += Tick;
            }
            catch (Exception exception) { Results.Add("FAIL " + exception); Debug.LogException(exception); Finish(1); }
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.2;
            try { if (sequence.MoveNext()) return; SaveState(); Finish(0); }
            catch (Exception exception) { Results.Add("FAIL " + exception); Debug.LogException(exception); Finish(1); }
        }

        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            if (originalClipboard != null) EditorGUIUtility.systemCopyBuffer = originalClipboard;
            File.WriteAllLines(Evidence + "/ui-results.txt", Results);
            if (window != null) window.Close();
            if (code != 0 && folder != null)
            {
                AssetDatabase.DeleteAsset(folder);
                if (File.Exists(StatePath)) File.Delete(StatePath);
            }
            EditorApplication.Exit(code);
        }

        private static IEnumerator RunUI()
        {
            yield return null;
            Check(window.rootVisualElement.Q<LevelBoardView>().panel != null, "실제 Editor 패널 연결");
            Click("inspector-tab-1"); yield return null;
            Click("add-mission"); yield return null;
            Check(level.Missions.Count == 1 && level.Missions[0].Kind == MissionKind.Color, "UI 첫 미션 추가");
            window.rootVisualElement.Q<IntegerField>("mission-count-0").value = 12; yield return null;
            Check(level.Missions[0].Count == 12, "UI 미션 목표 수정");
            window.rootVisualElement.Q<Foldout>("supply-maintenance").value = true;
            window.rootVisualElement.Q<IntegerField>("supply-scrapTarget").value = 2; yield return null;
            window.rootVisualElement.Q<IntegerField>("supply-scrapLimit").value = 6; yield return null;
            window.rootVisualElement.Q<IntegerField>("supply-scrapDurability").value = 5; yield return null;
            window.rootVisualElement.Q<IntegerField>("supply-recoveryTarget").value = 2; yield return null;
            Check(level.Supply.ScrapTarget == 2 && level.Supply.ScrapLimit == 6 && level.Supply.ScrapDurability == 5 && level.Supply.RecoveryTarget == 2,
                "UI 레벨 공통 유지 목표·한도·내구도 편집");
            window.rootVisualElement.Q<IntegerField>("supply-scrapTarget").value = 0; yield return null;
            window.rootVisualElement.Q<IntegerField>("supply-scrapLimit").value = 0; yield return null;
            window.rootVisualElement.Q<IntegerField>("supply-scrapDurability").value = 1; yield return null;
            window.rootVisualElement.Q<IntegerField>("supply-recoveryTarget").value = 0; yield return null;
            window.rootVisualElement.Q<Foldout>("supply-maintenance").value = false;
            Menu("생성구 선택"); yield return null;
            Cell(0, 0); yield return null;
            Check(window.rootVisualElement.Q<PopupField<string>>("source-mode") != null, "보드 생성구 선택 속성");
            Mode("고정 목록"); yield return null;
            Button addPower = window.rootVisualElement.Q<Button>("add-power-supply-item");
            using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled())
            { evt.target = addPower; addPower.SendEvent(evt); }
            yield return null;
            Check(level.Supply.Sources[0].Items.Count == 1 && level.Supply.Sources[0].Items[0].Kind == SupplyKind.RandomPower, "파워블록 추가 버튼 기본 랜덤");
            foreach (SupplyKind power in new[] { SupplyKind.Rocket, SupplyKind.Bomb, SupplyKind.Drone, SupplyKind.RandomPower })
            {
                window.rootVisualElement.Q<PopupField<string>>("supply-item-kind").value = LevelSupplyRules.Name(power); yield return null;
                Check(level.Supply.Sources[0].Items[0].Kind == power, "파워 공급 선택 " + power);
            }
            if (powerSupplyOnly)
            {
                window.rootVisualElement.Q<IntegerField>("supply-item-count").value = 7; yield return null;
                Check(level.Supply.Sources[0].Items[0].Count == 7, "랜덤 파워 수량 UI 저장");
                // OS 클립보드 사용 여부와 분리하여 실제 복사 데이터의 새 종류 직렬화를 검증한다.
                string powerCopy = LevelSupplyEditing.Copy(level, 0);
                Check(LevelSupplyEditing.Paste(level, new[] { 0 }, powerCopy, true) == null &&
                    level.Supply.Sources[0].Items.Count == 2 && level.Supply.Sources[0].Items[1].Kind == SupplyKind.RandomPower &&
                    level.Supply.Sources[0].Items[1].Count == 7, "랜덤 파워 복사 데이터/뒤에 추가");
                yield return null;
                Undo.PerformUndo(); yield return null;
                Check(level.Supply.Sources[0].Items.Count == 1, "랜덤 파워 붙여넣기 Undo");
                Undo.PerformRedo(); yield return null;
                Check(level.Supply.Sources[0].Items.Count == 2, "랜덤 파워 붙여넣기 Redo");
                yield break;
            }
            LevelContextMenuVerification.Action(window, "공급 항목/선택 항목 삭제").Execute(); yield return null;
            LevelContextMenuVerification.Action(window, "공급 항목/추가").Execute(); yield return null;
            Check(level.Supply.Sources[0].Items.Count == 1, "UI 공급 항목 추가");
            window.rootVisualElement.Q<PopupField<string>>("supply-item-kind").value = "청소로켓"; yield return null;
            window.rootVisualElement.Q<IntegerField>("supply-item-count").value = 3; yield return null;
            window.rootVisualElement.Q<PopupField<string>>("supply-item-direction").value = "세로"; yield return null;
            Check(level.Supply.Sources[0].Items[0].Kind == SupplyKind.Rocket && level.Supply.Sources[0].Items[0].Count == 3 &&
                level.Supply.Sources[0].Items[0].Direction == RocketDirection.Vertical, "UI 로켓 종류·수량·방향");
            LevelContextMenuVerification.Action(window, "공급 항목/선택 항목 복제").Execute(); yield return null;
            window.rootVisualElement.Q<PopupField<string>>("supply-item-kind").value = "달폭탄"; yield return null;
            Check(level.Supply.Sources[0].Items[0].Kind == SupplyKind.Rocket && level.Supply.Sources[0].Items[1].Kind == SupplyKind.Bomb, "UI 항목 복제 독립 값");
            LevelContextMenuVerification.Action(window, "공급 항목/먼저 공급 ↓").Execute(); yield return null;
            Check(level.Supply.Sources[0].Items[0].Kind == SupplyKind.Bomb, "아래쪽 먼저 공급 버튼과 저장 순서 일치");
            Undo.PerformUndo(); yield return null;
            Check(level.Supply.Sources[0].Items[0].Kind == SupplyKind.Rocket, "목록 순서 Undo");
            ListView contextList = window.rootVisualElement.Q<ListView>("supply-items");
            Label firstRow = contextList.Query<Label>().ToList().First(label => label.userData is int index && index == 0);
            contextList.SetSelection(1);
            Event rightClick = new Event { type = EventType.MouseDown, button = 1, mousePosition = firstRow.worldBound.center };
            using (PointerDownEvent pointer = PointerDownEvent.GetPooled(rightClick)) { pointer.target = firstRow; firstRow.SendEvent(pointer); }
            DropdownMenuAction rowAction = LevelContextMenuVerification.Action(window, "공급 항목/선택 항목 복제");
            Check(contextList.selectedIndex == 0, "공급 행 우클릭으로 대상 선택");
            window.rootVisualElement.Q<LevelBoardView>().CancelStroke(); yield return null;
            Check(contextList.panel != null, "우클릭 뒤 포커스 취소가 목록을 재생성하지 않음");
            contextList.SetSelection(1);
            string beforeStaleItem = JsonUtility.ToJson(level);
            rowAction.Execute(); yield return null;
            Check(JsonUtility.ToJson(level) == beforeStaleItem, "선택 행 변경 후 오래된 메뉴 거절");
            contextList.SetSelection(0);
            ListView reorder = window.rootVisualElement.Q<ListView>("supply-items");
            Vector2 start = new Vector2(reorder.worldBound.xMin + 9, reorder.worldBound.yMin + 14);
            VisualElement handle = window.rootVisualElement.panel.Pick(start);
            SendListPointer(handle, EventType.MouseDown, start); yield return null;
            SendListPointer(handle, EventType.MouseDrag, start + new Vector2(0, 10)); yield return null;
            SendListPointer(handle, EventType.MouseDrag, start + new Vector2(0, 38)); yield return null; yield return null;
            SendListPointer(handle, EventType.MouseUp, start + new Vector2(0, 38)); yield return null; yield return null;
            Check(level.Supply.Sources[0].Items[0].Kind == SupplyKind.Bomb, "실제 목록 핸들 드래그 재정렬");
            Undo.PerformUndo(); yield return null;
            Check(level.Supply.Sources[0].Items[0].Kind == SupplyKind.Rocket, "드래그 재정렬 한 번 Undo");
            string beforeCancel = JsonUtility.ToJson(level);
            reorder = window.rootVisualElement.Q<ListView>("supply-items");
            start = new Vector2(reorder.worldBound.xMin + 9, reorder.worldBound.yMin + 14);
            handle = window.rootVisualElement.panel.Pick(start);
            SendListPointer(handle, EventType.MouseDown, start); yield return null;
            SendListPointer(handle, EventType.MouseDrag, start + new Vector2(0, 38)); yield return null;
            using (KeyDownEvent cancel = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None)) { cancel.target = reorder; reorder.SendEvent(cancel); }
            yield return null;
            SendListPointer(handle, EventType.MouseUp, start + new Vector2(0, 38)); yield return null; yield return null;
            Check(JsonUtility.ToJson(level) == beforeCancel, "공급 목록 드래그 Esc 취소");
            window.Focus(); yield return null;
            Check(EditorWindow.focusedWindow == window, "포커스 취소 검사 시작 창 확인");
            reorder = window.rootVisualElement.Q<ListView>("supply-items");
            start = new Vector2(reorder.worldBound.xMin + 9, reorder.worldBound.yMin + 14);
            handle = window.rootVisualElement.panel.Pick(start);
            SendListPointer(handle, EventType.MouseDown, start); yield return null;
            SendListPointer(handle, EventType.MouseDrag, start + new Vector2(0, 38)); yield return null;
            Check(JsonUtility.ToJson(level) == beforeCancel, "포커스 이동 전 미완료 드래그 원본 보존");
            FocusWindow focus = ScriptableObject.CreateInstance<FocusWindow>();
            focus.position = new Rect(1030, 20, 180, 160); focus.ShowUtility(); focus.Focus(); yield return null; yield return null;
            Check(EditorWindow.focusedWindow == focus, "드래그 검사 포커스 실제 이동");
            Check(JsonUtility.ToJson(level) == beforeCancel, "포커스 이동 직후 원본 보존");
            focus.Close(); window.Focus(); yield return null;
            SendListPointer(handle, EventType.MouseUp, start + new Vector2(0, 38)); yield return null; yield return null;
            Check(JsonUtility.ToJson(level) == beforeCancel, "공급 목록 포커스 상실 취소");
            window.rootVisualElement.Q<PopupField<string>>("source-exhaustion").index = 1; yield return null;
            Check(level.Supply.Sources[0].Exhaustion == SupplyExhaustion.Random && level.Supply.Sources[0].Items.Count == 2, "소진 정책 변경 시 목록 보존");
            LevelContextMenuVerification.Action(window, "공급 항목/선택 항목 삭제").Execute(); yield return null;
            Check(level.Supply.Sources[0].Items.Count == 1, "UI 선택 공급 항목 삭제");
            Undo.PerformUndo(); yield return null;
            LevelContextMenuVerification.Action(window, "공급 목록 비우기").Execute(); yield return null;
            Check(level.Supply.Sources[0].Items.Count == 0, "UI 공급 목록 비우기");
            Undo.PerformUndo(); yield return null;
            LevelContextMenuVerification.Action(window, "공급 목록 복사").Execute(); yield return null;
            string copied = EditorGUIUtility.systemCopyBuffer;
            Cell(0, 1); yield return null; Cell(0, 2, true); yield return null;
            Check(window.rootVisualElement.Q<LevelBoardView>().SourceSelection.Count == 2, "Ctrl 클릭 생성구 다중 선택");
            Mode("고정 목록"); yield return null;
            LevelContextMenuVerification.Action(window, "공급 목록 교체 붙여넣기").Execute(); yield return null;
            Check(level.Supply.Sources[1].Items.Count == 2 && level.Supply.Sources[2].Items.Count == 2 &&
                window.rootVisualElement.Q<ListView>("supply-items") == null, "다중 붙여넣기·다중 순서 편집 제외");
            Undo.PerformUndo(); yield return null;
            Check(level.Supply.Sources[1].Items.Count == 0 && level.Supply.Sources[2].Items.Count == 0, "다중 붙여넣기 한 번 Undo");
            Undo.PerformRedo(); yield return null;
            window.rootVisualElement.Q<PopupField<string>>("source-exhaustion").index = 1; yield return null;
            Check(level.Supply.Sources[1].Exhaustion == SupplyExhaustion.Random && level.Supply.Sources[2].Exhaustion == SupplyExhaustion.Random &&
                level.Supply.Sources[1].Items.Count == 2 && level.Supply.Sources[2].Items.Count == 2, "공통 소진 정책만 수정·목록 보존");
            Undo.PerformUndo(); yield return null;
            Check(level.Supply.Sources[1].Exhaustion == SupplyExhaustion.Stop && level.Supply.Sources[2].Exhaustion == SupplyExhaustion.Stop,
                "공통 속성 한 번 Undo");
            Undo.PerformRedo(); yield return null;
            Cell(0, 3, true); yield return null;
            Check(window.rootVisualElement.Q<PopupField<string>>("source-mode").value.Contains("혼합"), "다른 방식의 혼합 값 표시");
            string before = JsonUtility.ToJson(level);
            Menu("회수 부품"); yield return null;
            Cell(5, 5); yield return null;
            Check(level.RecoveryParts.Contains(C(5, 5)), "UI 회수 부품 배치");
            Click("tool-Select"); Cell(5, 5); yield return null;
            LevelContextMenuVerification.Action(window, "회수 부품 삭제").Execute(); yield return null;
            Check(!level.RecoveryParts.Contains(C(5, 5)), "회수 부품 선택 속성에서 삭제");
            Undo.PerformUndo(); yield return null;
            Check(level.RecoveryParts.Contains(C(5, 5)), "회수 부품 삭제 Undo");
            LevelFlowEditing.SetGravity(level, new[] { C(5, 5) }, GravityDirection.Left); yield return null;
            LevelContextMenuVerification.Action(window, "바닥·연결/중력 기본값으로").Execute(); yield return null;
            Check(LevelFlowRules.GravityAt(level, C(5, 5)) == GravityDirection.Down, "컨텍스트 중력 초기화");
            Undo.PerformUndo(); yield return null;
            Check(LevelFlowRules.GravityAt(level, C(5, 5)) == GravityDirection.Left, "컨텍스트 중력 초기화 Undo");
            Undo.PerformUndo(); yield return null;
            Menu("생성구 배치"); yield return null;
            Cell(5, 5); yield return null;
            Check(LevelSupplyRules.FindSource(level, C(5, 5)) >= 0 && level.RecoveryParts.Contains(C(5, 5)), "회수 부품 점유 중 생성구 설정 보존");
            Menu("생성구 선택"); yield return null;
            Cell(0, 0); yield return null;
            LevelSupplyEditing.PlaceSources(level, new[] { C(6, 6) });
            LevelObstacleEditing.Apply(level, new PlacementBrush { Layer = PlacementLayer.Obstacle, Kind = (int)ObstacleKind.Appliance, Durability = 9 }, new[] { C(6, 6) });
            yield return null;
            Check(window.rootVisualElement.Q<Label>("supply-mark-66").resolvedStyle.display != DisplayStyle.None, "2×2 본체 위치의 생성구 기호 표시");
            window.position = new Rect(10, 10, 1000, 780); yield return null; yield return null;
            Capture("supply-wide.png");
            Click("inspector-tab-1"); yield return null;
            Capture("mission-wide.png");
            window.position = new Rect(10, 10, 680, 480); yield return null; yield return null;
            // 통합 창의 현재 계약은 최소 크기 자동 확대다. 내부 스크롤의 존재가 아니라
            // 확대 후 보드 접근과 좁은 컨테이너에서의 전체 영역 접근을 각각 확인한다.
            Check(window.position.width >= window.minSize.x && window.position.height >= window.minSize.y &&
                window.rootVisualElement.Q<ScrollView>("board-scroll").contentViewport.worldBound.width >= 400,
                "작은 독립 창의 최소 크기·공급 보드 영역 확보");
            ScrollView workspace = window.rootVisualElement.Q<ScrollView>("workspace-viewport");
            workspace.style.width = 600; workspace.style.height = 400; workspace.style.flexGrow = 0;
            yield return null; yield return null;
            Check(workspace.horizontalScroller.highValue > 0 && workspace.verticalScroller.highValue > 0,
                "좁은 컨테이너에서 공급 편집 양방향 스크롤");
            Capture("supply-narrow.png");
            workspace.style.width = StyleKeyword.Null; workspace.style.height = StyleKeyword.Null; workspace.style.flexGrow = 1;
            window.position = new Rect(10, 10, 1000, 780); yield return null;
            Click("tool-tab-2"); yield return null;
            Click("source-list-0-1"); yield return null;
            Check(window.rootVisualElement.Q<PopupField<string>>("source-mode") != null && window.rootVisualElement.Q("selection-page").resolvedStyle.display == DisplayStyle.Flex,
                "생성구 목록 선택과 보드·속성 연동");
            before = JsonUtility.ToJson(level);
            EditorGUIUtility.systemCopyBuffer = "invalid"; LevelContextMenuVerification.Action(window, "공급 목록 교체 붙여넣기").Execute(); yield return null;
            Check(JsonUtility.ToJson(level) == before && window.rootVisualElement.Q<Label>("operation-status").text.Contains("클립보드"), "UI 잘못된 붙여넣기 원본 유지·이유 표시");
            EditorGUIUtility.systemCopyBuffer = copied;
            DropdownMenuAction stalePaste = LevelContextMenuVerification.Action(window, "공급 목록 교체 붙여넣기");
            string sourcesBefore = JsonUtility.ToJson(level.Supply);
            SetInt(level, "moveCount", 24);
            stalePaste.Execute(); yield return null;
            Check(JsonUtility.ToJson(level.Supply) == sourcesBefore && window.rootVisualElement.Q<Label>("operation-status").text.Contains("원본이 변경"),
                "외부 변경 직후 오래된 붙여넣기 대상 거절");
            LevelContextMenuVerification.Action(window, "공급 목록 뒤에 추가").Execute(); yield return null;
            Check(level.Supply.Sources[1].Items.Count == 4, "UI 공급 목록 뒤에 추가");
            Undo.PerformUndo(); yield return null;
            SetInt(level, "supply.sources.Array.data[0].items.Array.data[0].count", 0);
            yield return null;
            Click("validate-level"); yield return null;
            Submit(window.rootVisualElement.Q<ScrollView>("validation-issues").Query<Button>().ToList()
                .First(button => button.text.Contains("InvalidSupply") && button.text.Contains("items")));
            yield return null;
            Check(window.rootVisualElement.Q<IntegerField>("supply-item-count")?.value == 0 &&
                window.rootVisualElement.Q<LevelBoardView>().SourceSelection.Contains(C(0, 0)), "공급 항목 오류 클릭 시 생성구·항목 선택");
            window.rootVisualElement.Q<IntegerField>("supply-item-count").value = 3; yield return null;
            SetInt(level, "missions.Array.data[0].count", 0);
            yield return null;
            Click("validate-level"); yield return null;
            Submit(window.rootVisualElement.Q<ScrollView>("validation-issues").Query<Button>().ToList()
                .First(button => button.text.Contains("InvalidMission")));
            yield return null;
            Check(window.rootVisualElement.Q<Foldout>("mission-settings").value &&
                window.rootVisualElement.Q<IntegerField>("mission-count-0").resolvedStyle.display != DisplayStyle.None, "미션 오류 클릭 시 설정 펼침");
            window.rootVisualElement.Q<IntegerField>("mission-count-0").value = 12; yield return null;
            Click("validate-level"); yield return null;
            Check(window.rootVisualElement.Q<Label>("validation-status").text.Contains("도달 가능성"), "검사 범위와 한계 표시");
            Click("save-level"); yield return null;
            Check(!EditorUtility.IsDirty(level), "UI 저장");
            Menu("생성구 선택"); Cell(0, 0); window.Focus(); yield return null;
            before = JsonUtility.ToJson(level);
            reorder = window.rootVisualElement.Q<ListView>("supply-items");
            start = new Vector2(reorder.worldBound.xMin + 9, reorder.worldBound.yMin + 14);
            handle = window.rootVisualElement.panel.Pick(start);
            SendListPointer(handle, EventType.MouseDown, start); yield return null;
            SendListPointer(handle, EventType.MouseDrag, start + new Vector2(0, 38)); yield return null;
            window.Close(); yield return null;
            Check(JsonUtility.ToJson(level) == before && !EditorUtility.IsDirty(level), "창 닫기 시 미완료 공급 드래그 취소");
        }

        private static void Menu(string value)
        {
            Button tab = window.rootVisualElement.Q<Button>("tool-tab-0");
            if (!tab.ClassListContains("active")) Submit(tab);
            window.rootVisualElement.Q<PopupField<string>>("menu-supply").value = value;
        }
        private static void Mode(string value) => window.rootVisualElement.Q<PopupField<string>>("source-mode").value = value;
        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("버튼 사용 불가: " + name);
            Submit(button);
        }
        private static void Submit(Button button)
        { using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt); }
        private static void Cell(int row, int column, bool additive = false)
        {
            LevelBoardView board = window.rootVisualElement.Q<LevelBoardView>();
            Vector2 position = board.LocalToWorld(new Vector2(column * 40 + 20, row * 40 + 20));
            Event input = new Event { type = EventType.MouseDown, button = 0, mousePosition = position, modifiers = additive ? EventModifiers.Control : EventModifiers.None };
            using (PointerDownEvent evt = PointerDownEvent.GetPooled(input)) { evt.target = board; board.SendEvent(evt); }
            input.type = EventType.MouseUp;
            using (PointerUpEvent evt = PointerUpEvent.GetPooled(input)) { evt.target = board; board.SendEvent(evt); }
        }
        private static void SendListPointer(VisualElement target, EventType type, Vector2 point)
        {
            Event input = new Event { type = type, button = 0, mousePosition = point };
            if (type == EventType.MouseDown) { using PointerDownEvent evt = PointerDownEvent.GetPooled(input); evt.target = target; target.SendEvent(evt); }
            else if (type == EventType.MouseUp) { using PointerUpEvent evt = PointerUpEvent.GetPooled(input); evt.target = target; target.SendEvent(evt); }
            else { using PointerMoveEvent evt = PointerMoveEvent.GetPooled(input); evt.target = target; target.SendEvent(evt); }
        }
        private static void Capture(string name)
        {
            Rect position = window.position; int width = Mathf.RoundToInt(position.width), height = Mathf.RoundToInt(position.height);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(position.position, width, height));
            texture.Apply(); File.WriteAllBytes(Evidence + "/" + name, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            Check(File.Exists(Evidence + "/" + name), "실제 화면 " + name);
        }
    }
}





