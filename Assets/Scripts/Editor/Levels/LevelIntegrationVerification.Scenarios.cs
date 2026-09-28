using System;
using System.Collections;
using System.Linq;
using Board;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class LevelIntegrationVerification
    {
        private static IEnumerator RunRemaining()
        {
            Menu("menu-flow", "통로 입구"); FlowClick(3, 6); yield return null;
            Click("validate-level"); yield return null;
            NavigateIssue("InvalidPortal:"); yield return null;
            Click("select-portal-exit"); FlowClick(1, 8); yield return null;
            Check(level.Flow.Portals.Single().HasExit, "통로 오류에서 출구 지정 동선");
            Tool("place-Obstacle-" + (int)ObstacleKind.Generator); Paint(5, 5); yield return null;
            Tool("place-Obstacle-" + (int)ObstacleKind.Crate); Paint(5, 8); yield return null;
            Menu("menu-flow", "흐름 선택"); FlowClick(5, 5); yield return null;
            Click("add-generator-target"); FlowClick(5, 8); yield return null;
            Check(level.Connections.Count == 1, "제작 레벨 발전기와 대상 연결");
            Click("validate-level"); yield return null;
            NavigateIssue("InvalidWire:"); yield return null;
            Check(window.rootVisualElement.Q<LevelFlowOverlay>().HighlightConnection == 0, "전선 오류 대상 강조");
            Click("edit-wire-0"); yield return null;
            WirePoint(5, 7); WirePoint(5, 8); Click("complete-flow"); yield return null;
            Check(level.Connections[0].Vertices.Count == 2 && !LevelDefinitionValidator.Validate(level).Any(issue => issue.Code == LevelValidationCode.InvalidWire), "오류 이동 후 전선 완성");
            Menu("menu-supply", "회수 부품"); Paint(8, 8); yield return null;
            Click("inspector-tab-1"); Click("add-mission"); yield return null;
            window.rootVisualElement.Q<PopupField<string>>("mission-kind-1").value = "부품 회수"; yield return null;
            window.rootVisualElement.Q<IntegerField>("mission-count-1").value = 1; yield return null;
            Click("validate-level"); yield return null;
            NavigateIssue("InvalidMission:"); yield return null;
            Check(window.rootVisualElement.Q("level-settings-page").resolvedStyle.display == DisplayStyle.Flex, "도착 바닥 누락 오류에서 레벨 설정 이동");
            Menu("menu-flow", "도착 바닥"); FlowClick(9, 8); yield return null;
            Check(level.RecoveryParts.Count == 1 && level.Flow.Arrivals.Count == 1, "회수 대상·도착 바닥·미션 구성");
            for (int attempt = 0; attempt < 10 && LevelDefinitionValidator.Validate(level).Any(issue => issue.Code == LevelValidationCode.InvalidMerge); attempt++)
            {
                Click("validate-level"); NavigateIssue("InvalidMerge:"); yield return null;
                Click("confirm-merge"); yield return null;
            }
            Check(LevelDefinitionValidator.Validate(level).Count == 0, "통로·연결·회수 포함 구조 검사: " + string.Join(" | ", LevelDefinitionValidator.Validate(level)));
            // 공급 항목 오류를 만든 후 실제 오류 이동과 편집으로 복구한다.
            SetValue(level, "supply.sources.Array.data[0].items.Array.data[0].count", 0); yield return null;
            Click("validate-level"); NavigateIssue("InvalidSupply:"); yield return null;
            Check(window.rootVisualElement.Q<IntegerField>("supply-item-count").value == 0, "오류에서 정확한 공급 항목 선택");
            window.rootVisualElement.Q<IntegerField>("supply-item-count").value = 3; yield return null;
            SetValue(level, "obstacles.Array.data[0].durability", 99); yield return null;
            Click("validate-level"); NavigateIssue("InvalidPlacementValue:"); yield return null;
            Check(window.rootVisualElement.Q<LevelBoardView>().Layer == PlacementLayer.Obstacle, "내구도 오류에서 장애물 층 선택");
            window.rootVisualElement.Q<IntegerField>("selected-durability").value = 2; yield return null;
            Click("validate-level"); yield return null;
            Check(LevelDefinitionValidator.Validate(level).Count == 0, "공급·배치 오류 수정 후 재검사");
            Click("save-level"); yield return null;
            SetValue(level, "moveCount", 27); yield return null;
            string original = JsonUtility.ToJson(level);
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level));
            Check(EditorUtility.IsDirty(level), "복제할 원본 미저장 상태");
            Click("duplicate-level"); yield return null;
            window.rootVisualElement.Q<IntegerField>("duplicate-number").value = 71012;
            window.DuplicateCurrentTo(folder + "/Copy.asset"); yield return null;
            LevelDefinition copy = window.CurrentLevel;
            Check(copy != level && copy.MoveCount == 27 && copy.Connections.Count == 1 && JsonUtility.ToJson(level) == original && EditorUtility.IsDirty(level), "통합 레벨 메모리 복제와 원본 dirty 보존");
            Check(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(copy)) != guid, "통합 사본 새 GUID");
            SetValue(copy, "moveCount", 28); yield return null;
            Check(JsonUtility.ToJson(level) == original, "사본 수정 원본 독립");
            SetValue(copy, "levelNumber", level.LevelNumber); yield return null;
            Click("validate-level"); NavigateIssue("DuplicateLevelNumber:"); yield return null;
            Check(window.rootVisualElement.Q<Label>("operation-status").text.Contains("중복"), "중복 번호 오류의 상대 에셋 안내");
            SetValue(copy, "levelNumber", 71012); yield return null;
            Click("validate-level"); yield return null;
            Check(window.rootVisualElement.Q<Label>("validation-status").text.Contains("오류 없음"), "사본 번호 수정 후 재검사");
            window.SetLevel(level); yield return null;
            Menu("menu-flow", "직접 경로"); FlowClick(8, 0); FlowClick(8, 1);
            Check(window.rootVisualElement.Q<LevelFlowOverlay>().DraftCount == 2, "전환 취소용 경로 초안");
            window.SetLevel(copy); yield return null;
            Check(window.rootVisualElement.Q<LevelFlowOverlay>().DraftCount == 0 && JsonUtility.ToJson(level) == original, "다른 레벨 이동 시 경로 초안 취소와 원본 보존");
            Check(window.rootVisualElement.Q<Label>("operation-status").text.StartsWith("선택 도구"), "레벨 전환 뒤 이전 경로 안내 초기화");
            window.SetLevel(null); yield return null;
            Check(window.rootVisualElement.Q<Label>("operation-status").text.Contains("레벨을 선택"), "레벨 미선택 안내 초기화");
            window.SetLevel(level); yield return null;
            window.rootVisualElement.Q<PopupField<string>>("placement-layer").index = (int)PlacementLayer.Obstacle;
            Paint(4, 2); yield return null;
            string beforeDelete = JsonUtility.ToJson(level);
            Click("delete-placement"); yield return null;
            string buttonDeleted = JsonUtility.ToJson(level);
            Undo.PerformUndo(); yield return null;
            Check(JsonUtility.ToJson(level) == beforeDelete, "버튼 삭제 단일 Undo 보존");
            Paint(4, 2); yield return null;
            LevelContextMenuVerification.Action(window, "선택한 층의 요소 삭제").Execute(); yield return null;
            Check(JsonUtility.ToJson(level) == buttonDeleted, "버튼과 컨텍스트 삭제의 전체 데이터 결과 일치");
            Undo.PerformUndo(); yield return null;
            Check(JsonUtility.ToJson(level) == beforeDelete, "컨텍스트 삭제 단일 Undo 보존");
            Click("save-level"); yield return null;
            Check(!EditorUtility.IsDirty(level), "통합 제작 레벨 최종 저장");
            CaptureIntegration("integrated-wide.png");
        }

        private static void SetValue(LevelDefinition target, string path, int value)
        { using SerializedObject edit = new SerializedObject(target); edit.FindProperty(path).intValue = value; edit.ApplyModifiedProperties(); }
        private static void NavigateIssue(string prefix)
        {
            Button button = window.rootVisualElement.Q<ScrollView>("validation-issues").Query<Button>().ToList().First(item => item.text.StartsWith(prefix, StringComparison.Ordinal));
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }
        private static void WirePoint(int row, int column)
        {
            VisualElement overlay = window.rootVisualElement.Q<LevelFlowOverlay>();
            Vector2 position = overlay.LocalToWorld(new Vector2(column * 40, row * 40));
            Event input = new Event { type = EventType.MouseDown, button = 0, mousePosition = position };
            using (PointerDownEvent down = PointerDownEvent.GetPooled(input)) { down.target = overlay; overlay.SendEvent(down); }
            input.type = EventType.MouseUp;
            using PointerUpEvent up = PointerUpEvent.GetPooled(input); up.target = overlay; overlay.SendEvent(up);
        }
    }
}

