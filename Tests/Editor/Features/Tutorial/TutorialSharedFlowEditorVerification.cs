using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Board;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
namespace Tutorial.Editor
{
    public static class TutorialSharedFlowEditorVerification
    {
        public static void Run()
        {
            LevelEditorWindow window = null, draft = null;
            TutorialFlowDefinition flow = null; TutorialUserSampleDefinition sample = null;
            string path = TutorialUserSampleStore.Folder + "/__Composer03Verification.asset";
            bool owned = false, hadTutorial = AssetDatabase.IsValidFolder("Assets/Data/Tutorial"), hadSamples = AssetDatabase.IsValidFolder(TutorialUserSampleStore.Folder);
            var results = new List<string>();
            void Check(bool value, string message) { if (!value) throw new Exception(message); results.Add("PASS " + message); }
            void Click(VisualElement root, string name)
            {
                Button button = root.Q<Button>(name); if (button == null) throw new Exception("버튼 없음: " + name);
                using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
            }
            try
            {
                window = LevelEditorWindow.OpenTutorialSample(TutorialSampleBoards.All.First(value => value.Id == "swap"));
                Check(window.rootVisualElement.Q("tutorial-flow-choice") != null && window.rootVisualElement.Q("tutorial-manual") != null, "공유 제작 메뉴와 도움말");
                LevelDefinition level = window.CurrentLevel;
                flow = ScriptableObject.CreateInstance<TutorialFlowDefinition>();
                flow.steps = TutorialFlowAuthoring.CopySteps(level.Tutorial.steps); TutorialFlowAuthoring.EnsureIds(flow.steps);
                flow.parameters.Add(new TutorialFlowParameter { key = "first", label = "교환할 첫 칸", stepId = flow.steps[0].authoringId, field = TutorialFlowField.First });
                window.rootVisualElement.Q<ObjectField>("tutorial-flow-choice").value = flow;
                Check(level.Tutorial.flow == flow && level.Tutorial.bindings.Count == 1 && window.rootVisualElement.Q("tutorial-level-settings") != null, "선택 UI로 공통 구성 연결 및 레벨 설정 표시");
                string original = JsonUtility.ToJson(flow);
                LevelBoardView board = window.rootVisualElement.Q<LevelBoardView>();
                Button pick = window.rootVisualElement.Q("tutorial-binding-first").Query<Button>().ToList().First();
                using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = pick; pick.SendEvent(evt); }
                BoardCoordinate chosen = new BoardCoordinate(2, 2); board.TutorialTargetPicked(chosen);
                Check(level.Tutorial.bindings[0].coordinate.Equals(chosen) && JsonUtility.ToJson(flow) == original, "보드 셀 선택은 레벨 값만 변경");
                Undo.PerformUndo();
                Check(!level.Tutorial.bindings[0].coordinate.Equals(chosen), "셀 연결 Undo");
                Undo.PerformRedo(); Check(level.Tutorial.bindings[0].coordinate.Equals(chosen), "셀 연결 Redo");
                draft = LevelEditorWindow.OpenTutorialFlow(level, flow);
                draft.CurrentLevel.Tutorial.steps[0].instructions = "공통 변경";
                Check(JsonUtility.ToJson(flow) == original, "공통 편집 사본은 적용 전 원본 보존");
                Click(draft.rootVisualElement, "tutorial-flow-apply");
                Check(flow.steps[0].instructions == "공통 변경" && level.Tutorial.bindings[0].coordinate.Equals(chosen), "공통 수정 적용 후 레벨 값 보존");
                draft.Close(); UnityEngine.Object.DestroyImmediate(draft); draft = null;
                TutorialFlowAuthoring.Detach(level);
                Check(level.Tutorial.flow == null && level.Tutorial.steps[0].first.Equals(chosen), "독립 복사는 연결 값을 반영");
                flow.steps[0].instructions = "다른 변경";
                Check(level.Tutorial.steps[0].instructions == "공통 변경", "독립 복사 후 원본 변경 격리");
                Check(TutorialUserSampleStore.Save(null, level.Tutorial.steps, "") == null, "저장 취소는 에셋 미생성");
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new Exception("검사 경로가 이미 존재함");
                TutorialUserSampleStore.EnsureFolder(); sample = TutorialUserSampleStore.Save(path, level.Tutorial.steps, "검사용"); owned = true;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                sample = AssetDatabase.LoadAssetAtPath<TutorialUserSampleDefinition>(path);
                int before = level.Tutorial.steps.Count; TutorialUserSampleStore.Apply(level, sample);
                sample.steps[0].instructions = "샘플 변경";
                Check(level.Tutorial.steps.Count == before + 1 && level.Tutorial.steps.Last().instructions == "공통 변경", "저장·재로드·복사 적용 후 샘플 변경 격리");
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Check(level.Tutorial.steps.Count == before, "샘플 적용 Undo");
                Undo.PerformRedo(); Check(level.Tutorial.steps.Count == before + 1, "샘플 적용 Redo");
                bool duplicate = false; try { TutorialUserSampleStore.Save(path, level.Tutorial.steps, ""); } catch (ArgumentException) { duplicate = true; }
                Check(duplicate, "동일 이름 저장 거절");
            }
            catch (Exception error) { results.Add("FAIL " + error); }
            finally
            {
                if (draft != null) { draft.Close(); UnityEngine.Object.DestroyImmediate(draft); }
                if (window != null) { window.Close(); UnityEngine.Object.DestroyImmediate(window); }
                if (flow != null) UnityEngine.Object.DestroyImmediate(flow);
                if (owned) AssetDatabase.DeleteAsset(path);
                if (!hadSamples && AssetDatabase.IsValidFolder(TutorialUserSampleStore.Folder) && AssetDatabase.FindAssets("", new[] { TutorialUserSampleStore.Folder }).Length == 0) AssetDatabase.DeleteAsset(TutorialUserSampleStore.Folder);
                if (!hadTutorial && AssetDatabase.IsValidFolder("Assets/Data/Tutorial") && AssetDatabase.FindAssets("", new[] { "Assets/Data/Tutorial" }).Length == 0) AssetDatabase.DeleteAsset("Assets/Data/Tutorial");
            }
            Directory.CreateDirectory("Logs/Tutorial/Composer03"); File.WriteAllLines("Logs/Tutorial/Composer03/editor.txt", results);
            EditorApplication.Exit(results.Any(value => value.StartsWith("FAIL")) ? 1 : 0);
        }
    }
}
