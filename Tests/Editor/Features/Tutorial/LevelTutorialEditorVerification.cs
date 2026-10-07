using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Board;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Tutorial.Editor
{
    public static class LevelTutorialEditorVerification
    {
        private static readonly List<string> Results = new List<string>();
        private static IEnumerator sequence;
        private static LevelEditorWindow window;
        private static LevelDefinition level;
        private static LevelDefinition other;
        private static string folder;
        private static double next;
        private sealed class FocusProbe : EditorWindow { }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        public static void Run()
        {
            folder = "Assets/__TutorialVerification_" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            level = LevelTutorialDataVerification.Fixture(100051); level.hideFlags = HideFlags.None; AssetDatabase.CreateAsset(level, folder + "/Tutorial.asset");
            other = LevelTutorialDataVerification.Fixture(100052); other.hideFlags = HideFlags.None; AssetDatabase.CreateAsset(other, folder + "/Other.asset");
            window = ScriptableObject.CreateInstance<LevelEditorWindow>(); window.position = new Rect(40, 40, 1200, 1000); window.ShowUtility(); window.SetLevel(level);
            sequence = Verify(); EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return; next = EditorApplication.timeSinceStartup + 0.2;
            try { if (sequence.MoveNext()) return; Finish(0); }
            catch (Exception error) { Debug.LogException(error); Results.Add("FAIL " + error); Finish(1); }
        }
        private static void Finish(int exit)
        {
            EditorApplication.update -= Tick; if (window != null) window.Close();
            AssetDatabase.DeleteAsset(folder); File.WriteAllLines("Logs/Tutorial/Stage01/editor-results.txt", Results); EditorApplication.Exit(exit);
        }
        private static void Click(string name)
        { Button button = window.rootVisualElement.Q<Button>(name); if (button == null) throw new InvalidOperationException("버튼 없음 " + name); using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt); }
        private static void Pointer(int row, int column, int clickCount = 1)
        {
            LevelBoardView board = window.rootVisualElement.Q<LevelBoardView>(); Vector2 position = board.LocalToWorld(new Vector2(column * LevelBoardView.CellSize + 22, row * LevelBoardView.CellSize + 22));
            Event input = new Event { type = EventType.MouseDown, button = 0, mousePosition = position, clickCount = clickCount };
            using (PointerDownEvent evt = PointerDownEvent.GetPooled(input)) { evt.target = board; board.SendEvent(evt); }
            input.type = EventType.MouseUp; using (PointerUpEvent evt = PointerUpEvent.GetPooled(input)) { evt.target = board; board.SendEvent(evt); }
        }
        private static string Layout() { string json = JsonUtility.ToJson(level); return json.Substring(0, json.LastIndexOf(",\"tutorial\":", StringComparison.Ordinal)); }
        private static IEnumerator Verify()
        {
            yield return null; yield return null; Click("inspector-tab-1"); yield return null;
            Check(window.rootVisualElement.Q<LevelTutorialEditorPanel>() != null, "레벨 설정에 실제 제작 패널 표시");
            Click("tutorial-add"); yield return null; yield return null;
            Check(level.Tutorial.steps.Count == 1 && level.Tutorial.steps[0].instructions == "새 안내", "UI 단계 추가");
            TextField text = window.rootVisualElement.Q("tutorial-field-instructions").Q<TextField>(); text.value = "실제 필드 편집";
            yield return null; yield return null;
            Check(level.Tutorial.steps[0].instructions == "실제 필드 편집", "바인딩 문구 필드 실제 편집");
            Click("tutorial-add"); yield return null;
            Check(level.Tutorial.steps.Count == 2, "UI 두 번째 단계 추가");
            Click("tutorial-pick-pair"); Pointer(1, 1);
            window.rootVisualElement.Q<PopupField<string>>("tutorial-step").value = "1단계";
            Check(window.rootVisualElement.Q<LevelBoardView>().TutorialTargetPicked == null && !level.Tutorial.steps[1].hasFirst, "단계 전환 임시 대상 선택 해제");
            window.rootVisualElement.Q<PopupField<string>>("tutorial-step").value = "2단계";
            using (SerializedObject data = new SerializedObject(level)) { data.FindProperty("tutorial").FindPropertyRelative("steps").GetArrayElementAtIndex(1).FindPropertyRelative("instructions").stringValue = "두 번째"; data.ApplyModifiedProperties(); }
            yield return null; yield return null;
            Click("tutorial-up"); yield return null;
            Check(level.Tutorial.steps[0].instructions == "두 번째", "단계 위로 정렬");
            Click("tutorial-down"); yield return null;
            Check(level.Tutorial.steps[1].instructions == "두 번째", "단계 아래로 정렬");
            Click("tutorial-delete"); yield return null;
            Check(level.Tutorial.steps.Count == 1, "UI 단계 삭제");
            Undo.PerformUndo(); yield return null; yield return null;
            Check(level.Tutorial.steps.Count == 2, "단계 삭제 Undo 복원");
            Undo.PerformRedo(); yield return null; yield return null;
            Check(level.Tutorial.steps.Count == 1, "단계 삭제 Redo");
            using (SerializedObject data = new SerializedObject(level)) { data.FindProperty("tutorial").FindPropertyRelative("steps").GetArrayElementAtIndex(0).FindPropertyRelative("kind").enumValueIndex = 1; data.ApplyModifiedProperties(); }
            yield return null; yield return null;
            LevelBoardView board = window.rootVisualElement.Q<LevelBoardView>(); board.Brush = LevelBrush.Erase;
            string placements = Layout();
            Click("tutorial-pick-pair"); Pointer(2, 2, 2);
            Check(!level.Tutorial.steps[0].hasFirst && Layout() == placements, "첫 대상 더블클릭 임시 선택 · 기존 배치 보존");
            Pointer(2, 3, 2); yield return null; yield return null;
            Check(level.Tutorial.steps[0].hasFirst && level.Tutorial.steps[0].hasSecond && level.Tutorial.steps[0].first.Equals(new BoardCoordinate(2, 2)) && level.Tutorial.steps[0].second.Equals(new BoardCoordinate(2, 3)), "교환 두 칸 원자적 확정");
            Check(Layout() == placements && !board.IsDragging, "지우기 도구에서도 대상 선택은 배치·드래그 불가");
            Check(board.Q("tutorial-swap-arrow") != null, "교환 방향 보드 표시");
            Undo.PerformUndo(); yield return null; yield return null;
            Check(!level.Tutorial.steps[0].hasFirst && !level.Tutorial.steps[0].hasSecond, "교환 대상 한 번 Undo");
            Undo.PerformRedo(); yield return null; yield return null;
            Click("tutorial-pick-pair"); Pointer(1, 1); board.CancelStroke(); board.Brush = LevelBrush.Select; Pointer(1, 2);
            Check(level.Tutorial.steps[0].first.Equals(new BoardCoordinate(2, 2)) && board.TutorialTargetPicked == null, "미완료 대상 선택 취소");
            Click("tutorial-pick-pair"); Pointer(1, 1);
            using (KeyDownEvent escape = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape })) { escape.target = board; board.SendEvent(escape); }
            Check(board.TutorialTargetPicked == null && level.Tutorial.steps[0].first.Equals(new BoardCoordinate(2, 2)), "Esc 임시 선택 취소");
            Click("tutorial-pick-pair"); Pointer(1, 1);
            FocusProbe probe = ScriptableObject.CreateInstance<FocusProbe>(); probe.ShowUtility(); probe.Focus(); yield return null; yield return null;
            Check(board.TutorialTargetPicked == null, "창 포커스 상실 선택 취소"); probe.Close(); window.Focus(); yield return null;
            Click("tutorial-pick-highlight"); Pointer(3, 3); yield return null; yield return null;
            Check(level.Tutorial.steps[0].highlights.Count == 1, "보드 강조 칸 추가");
            Click("tutorial-pick-highlight"); Pointer(3, 3); yield return null; yield return null;
            Check(level.Tutorial.steps[0].highlights.Count == 0, "보드 강조 칸 토글 제거");
            using (SerializedObject data = new SerializedObject(level)) { data.FindProperty("tutorial").FindPropertyRelative("steps").GetArrayElementAtIndex(0).FindPropertyRelative("kind").enumValueIndex = 3; data.ApplyModifiedProperties(); }
            yield return null; yield return null;
            Click("tutorial-pick-item"); Pointer(3, 3); yield return null; yield return null;
            Check(level.Tutorial.steps[0].hasFirst && !level.Tutorial.steps[0].hasSecond && level.Tutorial.steps[0].first.Equals(new BoardCoordinate(3, 3)), "아이템 한 칸 선택");
            Check(window.rootVisualElement.Q("tutorial-field-results") != null && window.rootVisualElement.Q("tutorial-field-actionDefinitionId") != null, "결과 조건·정의 ID 편집 필드");
            string prior = JsonUtility.ToJson(level.Tutorial); AssetDatabase.SaveAssetIfDirty(level); string guid = AssetDatabase.AssetPathToGUID(folder + "/Tutorial.asset");
            AssetDatabase.ImportAsset(folder + "/Tutorial.asset", ImportAssetOptions.ForceUpdate); yield return null;
            Check(JsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<LevelDefinition>(folder + "/Tutorial.asset").Tutorial) == prior && AssetDatabase.AssetPathToGUID(folder + "/Tutorial.asset") == guid, "실제 저장·재로드·GUID 보존");
            Click("tutorial-pick-pair"); Pointer(1, 1); window.SetLevel(other); yield return null; yield return null;
            Check(board.TutorialTargetPicked == null && other.Tutorial.steps.Count == 0 && JsonUtility.ToJson(level.Tutorial) == prior, "레벨 전환 선택·구독 해제");
            window.SetLevel(level); yield return null; yield return null; Click("inspector-tab-1");
            Click("tutorial-pick-pair"); Pointer(1, 1); LevelBoardView before = window.rootVisualElement.Q<LevelBoardView>(); window.CreateGUI(); yield return null; yield return null;
            Check(before.TutorialTargetPicked == null && window.rootVisualElement.Query<LevelTutorialEditorPanel>().ToList().Count == 1, "창 UI 재생성 이전 선택 해제·패널 중복 없음");
            using (SerializedObject data = new SerializedObject(level)) { data.FindProperty("tutorial").FindPropertyRelative("steps").GetArrayElementAtIndex(0).FindPropertyRelative("first").FindPropertyRelative("row").intValue = 12; data.ApplyModifiedProperties(); }
            yield return null; yield return null;
            Check(window.rootVisualElement.Q("tutorial-error") != null, "단계별 정적 오류 실제 표시");
            LevelTutorialEditorPanel panel = window.rootVisualElement.Q<LevelTutorialEditorPanel>(); panel.Dispose(); panel.Dispose();
            Check(window.rootVisualElement.Q<LevelBoardView>().TutorialTargetPicked == null, "패널 Dispose 반복 안전·구독 정리");
        }
    }
}


