using System;
using System.IO;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tutorial.Editor
{
    public static class TutorialComposerEditorVerification
    {
        public static void Run()
        {
            int exit = 0;
            LevelDefinition level = LevelPackCodec.ReadLevel(File.ReadAllBytes("Tests/Fixtures/Tutorial/legacy-v3.bytes"), 1);
            level.Tutorial.steps.Clear();
            LevelBoardView board = new LevelBoardView();
            board.Display(level, null);
            EditorWindow host = ScriptableObject.CreateInstance<EditorWindow>(); host.ShowUtility();
            host.rootVisualElement.Add(board);
            try
            {
                using LevelTutorialEditorPanel panel = new LevelTutorialEditorPanel(level, board, 0, _ => { });
                host.rootVisualElement.Add(panel);
                Button sample = panel.Q<Button>("tutorial-sample-pick");
                if (sample == null) throw new InvalidOperationException("셀 선택부터 샘플을 만드는 진입 버튼 없음");
                using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = sample; sample.SendEvent(evt); }
                if (board.TutorialTargetPicked == null) throw new InvalidOperationException("샘플 셀 선택 시작 실패");
                board.TutorialTargetPicked(new Board.BoardCoordinate(2, 2));
                board.TutorialTargetPicked(new Board.BoardCoordinate(2, 3));
                if (level.Tutorial.steps.Count != 0) throw new InvalidOperationException("미리보기 전에 단계가 저장됨");
                board.CancelStroke();
                if (panel.Q<Button>("tutorial-sample-apply") != null || level.Tutorial.steps.Count != 0)
                    throw new InvalidOperationException("선택 취소 후 미리보기 적용 버튼이 남음");
                using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = sample; sample.SendEvent(evt); }
                board.TutorialTargetPicked(new Board.BoardCoordinate(2, 2));
                board.TutorialTargetPicked(new Board.BoardCoordinate(2, 3));
                Button apply = panel.Q<Button>("tutorial-sample-apply");
                if (apply == null) throw new InvalidOperationException("샘플 적용 버튼 없음");
                using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = apply; apply.SendEvent(evt); }
                if (level.Tutorial.steps.Count != 1 || level.Tutorial.steps[0].conditions.Count != 1)
                    throw new InvalidOperationException("샘플 단계와 조건 적용 실패");
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                if (level.Tutorial.steps.Count != 0) throw new InvalidOperationException("샘플 적용 Undo 실패");
                Undo.PerformRedo();
                if (level.Tutorial.steps.Count != 1) throw new InvalidOperationException("샘플 적용 Redo 실패");
                LevelEditorWindow window = ScriptableObject.CreateInstance<LevelEditorWindow>();
                try
                {
                    window.ShowUtility(); window.SetLevel(level);
                    Button mode = window.rootVisualElement.Q<Button>("tutorial-composer-mode");
                    if (mode == null) throw new InvalidOperationException("전용 튜토리얼 편집 모드 없음");
                    using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = mode; mode.SendEvent(evt); }
                    if (window.rootVisualElement.Q("tutorial-stage-list") == null || window.rootVisualElement.Q("tutorial-test-area") == null)
                        throw new InvalidOperationException("단계 목록/하단 테스트 영역 없음");
                }
                finally { window.Close(); }
                level.Tutorial.steps[0].conditions[0].requiredCount = 0;
                using (LevelTutorialEditorPanel invalidPanel = new LevelTutorialEditorPanel(level, board, 0, _ => { }))
                {
                    host.rootVisualElement.Add(invalidPanel);
                    if (invalidPanel.Q<Button>("tutorial-error-jump") == null)
                        throw new InvalidOperationException("설정 오류에서 해당 조건으로 이동하는 버튼 없음");
                }
                File.WriteAllText("Logs/Tutorial/Composer01/editor-results.txt", "PASS 셀 선택·미리보기·샘플 적용");
            }
            catch (Exception error) { exit = 1; File.WriteAllText("Logs/Tutorial/Composer01/editor-results.txt", "FAIL " + error); Debug.LogException(error); }
            finally { host.Close(); UnityEngine.Object.DestroyImmediate(level); }
            EditorApplication.Exit(exit);
        }
    }
}
