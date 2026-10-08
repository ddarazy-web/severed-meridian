using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
namespace Tutorial.Editor
{
    public static class TutorialComposerReviewVerification
    {
        public static void Run()
        {
            var output = new List<string>();
            void Verify(string name, Action check)
            { try { check(); output.Add("PASS " + name); } catch (Exception error) { output.Add("FAIL " + name + ": " + error); } }
            Verify("잘못된 흐름 연결의 원자성", () =>
            {
                LevelDefinition level = TutorialSampleBoards.All.First(value => value.Id == "swap").CreateBoard();
                TutorialFlowDefinition flow = ScriptableObject.CreateInstance<TutorialFlowDefinition>();
                try
                {
                    string before = JsonUtility.ToJson(level);
                    flow.parameters.Add(new TutorialFlowParameter { key = "missing", stepId = "deleted", field = TutorialFlowField.First });
                    try { TutorialFlowAuthoring.Connect(level, flow); } catch (Exception) { }
                    if (JsonUtility.ToJson(level) != before) throw new Exception("연결 실패가 원본을 바꿈");
                }
                finally { UnityEngine.Object.DestroyImmediate(level); UnityEngine.Object.DestroyImmediate(flow); }
            });
            Verify("공통 편집 사본은 다른 레벨로 교체할 수 없음", () =>
            {
                LevelDefinition source = TutorialSampleBoards.All.First(value => value.Id == "swap").CreateBoard();
                LevelDefinition other = ScriptableObject.CreateInstance<LevelDefinition>();
                TutorialFlowDefinition flow = ScriptableObject.CreateInstance<TutorialFlowDefinition>();
                LevelEditorWindow window = null;
                try
                {
                    flow.steps = TutorialFlowAuthoring.CopySteps(source.Tutorial.steps); TutorialFlowAuthoring.EnsureIds(flow.steps);
                    window = LevelEditorWindow.OpenTutorialFlow(source, flow); LevelDefinition draft = window.CurrentLevel;
                    if (window.rootVisualElement.Q<Button>("new-level").enabledSelf || window.rootVisualElement.Q<Button>("duplicate-level").enabledSelf) throw new Exception("공통 편집에서 레벨 생성/복제 버튼 활성");
                    window.SetLevel(other);
                    if (window.CurrentLevel != draft || draft == null) throw new Exception("공통 적용 대상이 다른 레벨로 바뀜");
                }
                finally { if (window != null) { window.Close(); UnityEngine.Object.DestroyImmediate(window); } UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(other); UnityEngine.Object.DestroyImmediate(flow); }
            });
            Verify("생성 연결이 있는 샘플 재적용", () =>
            {
                LevelDefinition level = TutorialSampleBoards.All.First(value => value.Id == "follow").CreateBoard();
                TutorialUserSampleDefinition sample = ScriptableObject.CreateInstance<TutorialUserSampleDefinition>();
                try
                {
                    sample.steps = TutorialFlowAuthoring.CopySteps(level.Tutorial.steps);
                    TutorialUserSampleStore.Apply(level, sample);
                    string[] names = level.Tutorial.steps.SelectMany(step => step.conditions).Select(value => value.bindGeneratedAs).Where(value => !string.IsNullOrEmpty(value)).ToArray();
                    if (names.Distinct().Count() != names.Length) throw new Exception("샘플 생성 연결 이름 중복");
                    if (level.Tutorial.steps.Last().firstBinding != names.Last()) throw new Exception("재적용한 샘플의 내부 참조 유실");
                }
                finally { UnityEngine.Object.DestroyImmediate(level); UnityEngine.Object.DestroyImmediate(sample); }
            });
            Verify("공유 설정 일반 오류에서 해당 항목으로 이동", () =>
            {
                LevelEditorWindow window = LevelEditorWindow.OpenTutorialSample(TutorialSampleBoards.All.First(value => value.Id == "swap"));
                TutorialFlowDefinition flow = ScriptableObject.CreateInstance<TutorialFlowDefinition>();
                try
                {
                    flow.steps = TutorialFlowAuthoring.CopySteps(window.CurrentLevel.Tutorial.steps); TutorialFlowAuthoring.EnsureIds(flow.steps);
                    flow.steps[0].conditions[0].requiredCount = 0;
                    flow.parameters.Add(new TutorialFlowParameter { key = "count", stepId = flow.steps[0].authoringId, conditionId = flow.steps[0].conditions[0].authoringId, field = TutorialFlowField.RequiredCount });
                    window.rootVisualElement.Q<ObjectField>("tutorial-flow-choice").value = flow;
                    if (!window.rootVisualElement.Query<Button>().ToList().Any(button => button.text == "이 설정으로 이동")) throw new Exception("잘못된 횟수의 오류 이동 없음");
                }
                finally { window.Close(); UnityEngine.Object.DestroyImmediate(window); UnityEngine.Object.DestroyImmediate(flow); }
            });
            Directory.CreateDirectory("Logs/Tutorial/Composer03"); File.WriteAllLines("Logs/Tutorial/Composer03/review.txt", output);
            EditorApplication.Exit(output.Any(line => line.StartsWith("FAIL")) ? 1 : 0);
        }
    }
}
