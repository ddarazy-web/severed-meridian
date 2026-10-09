using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tutorial.Editor
{
    public sealed partial class LevelTutorialEditorPanel
    {
        private void AddJsonFlowChoice()
        {
            var documents = jsonWorkspace.Session.Documents.Where(doc => doc.Kind == "tutorialFlow").OrderBy(doc => (string)doc.Data["displayName"]).ToArray();
            var labels = new List<string> { "독립 구성" };
            labels.AddRange(documents.Select(doc => (string)doc.Data["displayName"] + " · " + doc.Id.Substring(Math.Max(0, doc.Id.Length - 6))));
            string current = (string)jsonWorkspace.Session.Get(jsonWorkspace.Session.SelectedLevelId).Data["tutorial"]["flowId"];
            int index = Array.FindIndex(documents, doc => doc.Id == current) + 1;
            var choice = new PopupField<string>("JSON 공통 진행 구성", labels, index) { name = "tutorial-json-flow-choice" };
            choice.RegisterValueChangedCallback(_ =>
            {
                Mutate("공통 진행 구성 연결", () =>
                {
                    if (choice.index == 0) TutorialFlowAuthoring.Detach(owner);
                    else TutorialFlowAuthoring.Connect(owner, (TutorialFlowDefinition)jsonWorkspace.Resolve(documents[choice.index - 1].Id));
                });
                Rebuild();
            });
            fields.Add(choice);
        }
        private void CreateJsonFlow()
        {
            if (owner.Tutorial.steps.Count == 0) { fields.Add(new HelpBox("단계를 먼저 추가하세요.", HelpBoxMessageType.Warning)); return; }
            Mutate("JSON 공통 흐름 생성", () =>
            {
                var flow = ScriptableObject.CreateInstance<TutorialFlowDefinition>();
                jsonWorkspace.Register(flow, "tutorialFlow");
                flow.name = owner.name + " 공통 흐름";
                flow.steps = TutorialFlowAuthoring.CopySteps(owner.Tutorial.steps);
                TutorialFlowAuthoring.EnsureIds(flow.steps);
                TutorialFlowAuthoring.Connect(owner, flow);
            });
            Rebuild();
        }
        private void SaveJsonSample(string name, bool whole)
        {
            if (owner.Tutorial.steps.Count == 0) return;
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("샘플 이름을 입력하세요.");
            Mutate("JSON 샘플 저장", () =>
            {
                var sample = ScriptableObject.CreateInstance<TutorialUserSampleDefinition>();
                jsonWorkspace.Register(sample, "tutorialSample");
                sample.name = name.Trim(); sample.description = whole ? "전체 흐름" : "한 단계";
                sample.steps = TutorialFlowAuthoring.CopySteps(whole ? owner.Tutorial.steps : new List<TutorialStepDefinition> { owner.Tutorial.steps[selected] });
                TutorialFlowAuthoring.EnsureIds(sample.steps);
            });
            Rebuild();
        }
        private void AddJsonSamples()
        {
            var section = new Foldout { text = "JSON 내 샘플 · 복사해서 적용", value = false }; fields.Add(section);
            section.Add(new HelpBox("샘플 생성·적용은 실행 취소할 수 있습니다. 작업 폴더 저장을 눌러야 파일에 반영됩니다. 이후 샘플 수정은 이미 적용한 단계에 전파되지 않습니다.", HelpBoxMessageType.Info));
            var name = new TextField("샘플 이름") { value = "내 튜토리얼 샘플", name = "tutorial-json-sample-name" }; section.Add(name);
            section.Add(new Button(() => SaveJsonSample(name.value, false)) { text = "현재 단계를 샘플로 저장" });
            section.Add(new Button(() => SaveJsonSample(name.value, true)) { text = "전체 흐름을 샘플로 저장" });
            var documents = jsonWorkspace.Session.Documents.Where(doc => doc.Kind == "tutorialSample").ToArray();
            if (documents.Length == 0) { section.Add(new Label("이 폴더에 저장한 샘플이 없습니다.")); return; }
            var choice = new PopupField<string>("적용할 샘플", documents.Select(doc => (string)doc.Data["displayName"] + " · " + doc.Id.Substring(Math.Max(0, doc.Id.Length - 6))).ToList(), 0);
            var preview = new Label { style = { whiteSpace = WhiteSpace.Normal } };
            void Preview()
            {
                var sample = (TutorialUserSampleDefinition)jsonWorkspace.Resolve(documents[choice.index].Id);
                preview.text = string.Join("\n", sample.steps.Select(step => step.instructions + " · " + step.kind + " · 조건 " + step.conditions.Count + "개"));
            }
            choice.RegisterValueChangedCallback(_ => Preview()); section.Add(choice); section.Add(preview); Preview();
            section.Add(new Button(() =>
            {
                Mutate("JSON 샘플 적용", () => TutorialUserSampleStore.Apply(owner, (TutorialUserSampleDefinition)jsonWorkspace.Resolve(documents[choice.index].Id)));
                Rebuild();
            }) { text = "미리 본 샘플을 끝에 추가", name = "tutorial-json-sample-apply" });
        }
    }
}
