using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tutorial.Editor
{
    public static class TutorialComposerWorkflowVerification
    {
        private static LevelEditorWindow window;
        private static LevelDefinition level;
        private static string folder;
        private static IEnumerator sequence;
        private static double next;
        private static readonly List<string> results = new List<string>();
        private static BoardCoordinate first, second;
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); results.Add("PASS " + message); }
        public static void Run()
        {
            results.Clear();
            folder = "Assets/__ComposerWorkflow_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            level = LevelPackCodec.ReadLevel(File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-v3.bytes"), 1);
            TutorialStepDefinition swap = level.Tutorial.steps.First(value => value.kind == TutorialStepKind.Swap);
            first = swap.first; second = swap.second; level.Tutorial.steps.Clear(); level.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(level, folder + "/Sample.asset");
            window = ScriptableObject.CreateInstance<LevelEditorWindow>();
            window.position = new Rect(40, 40, 1400, 1000); window.ShowUtility(); window.CreateGUI(); window.SetLevel(level);
            sequence = Verify(); EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 0.25;
            try { if (sequence.MoveNext()) return; Finish(0); }
            catch (Exception error) { results.Add("FAIL " + error); Debug.LogException(error); Finish(1); }
        }
        private static void Finish(int exit)
        {
            EditorApplication.update -= Tick;
            if (window != null) window.Close();
            AssetDatabase.DeleteAsset(folder);
            Directory.CreateDirectory("Logs/Tutorial/Composer01");
            File.WriteAllLines("Logs/Tutorial/Composer01/workflow-results.txt", results);
            if (exit == 0 && SessionState.GetBool("Tutorial.Composer01.FullRun", false)) TutorialComposerPlayVerification.Run();
            else { SessionState.SetBool("Tutorial.Composer01.FullRun", false); EditorApplication.Exit(exit); }
        }
        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            Check(button != null && button.enabledInHierarchy, "조작 가능한 버튼 " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }
        private static void Pointer(BoardCoordinate coordinate)
        {
            LevelBoardView board = window.rootVisualElement.Q<LevelBoardView>();
            Vector2 point = board.LocalToWorld(new Vector2(coordinate.Column * LevelBoardView.CellSize + 20, coordinate.Row * LevelBoardView.CellSize + 20));
            Event input = new Event { type = EventType.MouseDown, button = 0, mousePosition = point, clickCount = 1 };
            using (PointerDownEvent evt = PointerDownEvent.GetPooled(input)) { evt.target = board; board.SendEvent(evt); }
            input.type = EventType.MouseUp;
            using (PointerUpEvent evt = PointerUpEvent.GetPooled(input)) { evt.target = board; board.SendEvent(evt); }
        }
        private static IEnumerator Verify()
        {
            yield return null;
            Click("tutorial-composer-mode"); yield return null;
            Rect stages = window.rootVisualElement.Q("tutorial-stage-list").worldBound;
            Rect boardBounds = window.rootVisualElement.Q<LevelBoardView>().worldBound;
            Rect settings = window.rootVisualElement.Q<LevelTutorialEditorPanel>().worldBound;
            Rect testing = window.rootVisualElement.Q("tutorial-test-area").worldBound;
            Check(stages.xMax <= boardBounds.xMin && settings.xMin >= boardBounds.xMax && testing.yMin >= boardBounds.yMax,
                "실제 레이아웃은 왼쪽 단계·중앙 보드·오른쪽 설정·하단 검사 영역으로 분리");
            string before = JsonUtility.ToJson(level);
            Click("tutorial-sample-pick"); Pointer(first); yield return null;
            Check(JsonUtility.ToJson(level) == before, "첫 포인터 선택은 배치/제작 데이터를 변경하지 않음");
            Pointer(second); yield return null;
            Check(JsonUtility.ToJson(level) == before, "두 칸 선택 미리보기에서도 원본 보존");
            window.rootVisualElement.Q<PopupField<string>>("tutorial-sample-choice").value = "직접 3매칭";
            Click("tutorial-sample-apply"); yield return null; yield return null;
            Check(level.Tutorial.steps.Count == 1 && level.Tutorial.steps[0].conditions[0].kind == TutorialConditionKind.Match, "포인터 선택→매칭 샘플 적용");
            Check(level.Tutorial.steps[0].first.Equals(first) && level.Tutorial.steps[0].second.Equals(second), "선택한 실제 좌표 저장");
            Click("tutorial-duplicate"); yield return null; yield return null;
            Check(level.Tutorial.steps.Count == 2, "단계 복제");
            VisualElement card = window.rootVisualElement.Q("tutorial-condition-0");
            IntegerField count = card.Query<IntegerField>().First();
            count.value = 2; yield return null; yield return null;
            Check(level.Tutorial.steps[1].conditions[0].requiredCount == 2 && level.Tutorial.steps[0].conditions[0].requiredCount == 1, "복제 조건 수정은 원래 단계와 독립");
            Click("tutorial-up"); yield return null; yield return null;
            Check(level.Tutorial.steps[0].conditions[0].requiredCount == 2, "단계 위로 정렬은 조건까지 함께 이동");
            Click("tutorial-down"); yield return null; yield return null;
            Check(level.Tutorial.steps[1].conditions[0].requiredCount == 2, "단계 아래로 정렬 복귀");
            Click("tutorial-pick-pair"); Pointer(first);
            Click("tutorial-select-step-0"); yield return null;
            Check(window.rootVisualElement.Q<LevelBoardView>().TutorialTargetPicked == null, "단계 변경 시 진행 중이던 셀 선택 취소");
            Click("tutorial-select-step-1"); yield return null;
            count = window.rootVisualElement.Q("tutorial-condition-0").Query<IntegerField>().First();
            count.value = 0; yield return null; yield return null;
            Click("tutorial-error-jump"); yield return null; yield return null;
            count = window.rootVisualElement.Q("tutorial-condition-0").Query<IntegerField>().First();
            VisualElement focused = window.rootVisualElement.panel.focusController.focusedElement as VisualElement;
            Check(focused == count || (focused != null && count.Contains(focused)), "오류 이동은 실제 잘못된 횟수 필드에 포커스");
            count.value = 2; yield return null; yield return null;
            Click("tutorial-pick-pair"); Pointer(first); Pointer(new BoardCoordinate(first.Row, 8)); yield return null; yield return null;
            Click("tutorial-error-jump"); yield return null; yield return null;
            LevelBoardView errorBoard = window.rootVisualElement.Q<LevelBoardView>();
            Label errorMark = errorBoard.Q<Label>("tutorial-mark");
            Check(errorMark != null && Mathf.Abs(errorMark.resolvedStyle.left - (8 * LevelBoardView.CellSize + 2)) < 1, "오류 이동은 잘못된 대상 셀을 표시");
            Click("tutorial-pick-pair"); Pointer(first); Pointer(second); yield return null; yield return null;
            Click("tutorial-delete"); yield return null; yield return null;
            Undo.PerformUndo(); yield return null; yield return null;
            Check(level.Tutorial.steps.Count == 2, "실제 창 단계 삭제 Undo");
            Undo.PerformRedo(); yield return null; yield return null;
            Check(level.Tutorial.steps.Count == 1, "실제 창 단계 삭제 Redo");
            Click("save-level"); yield return null;
            string expected = JsonUtility.ToJson(level.Tutorial);
            window.SetLevel(null); Resources.UnloadAsset(level);
            level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(folder + "/Sample.asset"); window.SetLevel(level);
            yield return null; yield return null;
            Check(JsonUtility.ToJson(level.Tutorial) == expected, "실제 에셋 저장·언로드·재로드 조건 보존");
            byte[] bytes = LevelPackCodec.Snapshot(level);
            LevelDefinition packed = LevelPackCodec.ReadLevel(bytes, level.LevelNumber);
            try { Check(JsonUtility.ToJson(packed.Tutorial) == expected, "Asset·MemoryPack 조건/좌표/공급 동등"); }
            finally { UnityEngine.Object.DestroyImmediate(packed); }
            File.WriteAllBytes("Logs/Tutorial/Composer01/workflow.bytes", bytes);
            Check(LevelTutorialReplayValidator.Validate(level).Count == 0, "UI 작성 샘플의 실제 논리 재생 통과");
        }
    }
}
