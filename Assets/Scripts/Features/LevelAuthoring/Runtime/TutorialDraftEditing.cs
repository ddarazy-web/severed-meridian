#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LevelAuthoring.Editing;
using Levels;
using Newtonsoft.Json.Linq;
using Tutorial;
using UnityEngine;

namespace LevelAuthoring.Runtime
{
    public static class TutorialDraftEditing
    {
        public static void EditLevel(AuthoringEditSession session, string label, Action<LevelDefinition> edit)
        {
            using var graph = new AuthoringObjectGraph(session.Documents);
            string id = session.SelectedLevelId;
            var level = (LevelDefinition)graph.Resolve(id);
            JToken before = graph.Encode(id).Data["tutorial"];
            edit(level);
            JToken after = graph.Encode(id).Data["tutorial"];
            // 다른 레벨 필드와 변환 시 정규화된 값은 원본 그대로 보존한다.
            if (!JToken.DeepEquals(before, after)) session.Apply(label, docs => docs[id].Data["tutorial"] = after.DeepClone());
        }
        public static void EditFlow(AuthoringEditSession session, string id, string label, Action<TutorialFlowDefinition> edit)
        {
            using var graph = new AuthoringObjectGraph(session.Documents);
            var flow = (TutorialFlowDefinition)graph.Resolve(id);
            JObject before = graph.Encode(id).Data;
            edit(flow);
            var changed = graph.Encode(id);
            if (!JToken.DeepEquals(before, changed.Data)) session.Apply(label, docs => docs[id] = changed);
        }
        public static void EditFlowSteps(AuthoringEditSession session, string flowId, string label, Action<LevelDefinition> edit)
        {
            using var graph = new AuthoringObjectGraph(session.Documents);
            var level = (LevelDefinition)graph.Resolve(session.SelectedLevelId);
            var flow = (TutorialFlowDefinition)graph.Resolve(flowId);
            JObject before = graph.Encode(flowId).Data;
            level.Tutorial.flow = null; level.Tutorial.steps = flow.steps;
            edit(level); flow.steps = level.Tutorial.steps;
            var changed = graph.Encode(flowId);
            if (!JToken.DeepEquals(before, changed.Data)) session.Apply(label, docs => docs[flowId] = changed);
        }
        public static void Connect(AuthoringEditSession session, string flowId)
        {
            using var graph = new AuthoringObjectGraph(session.Documents);
            var level = (LevelDefinition)graph.Resolve(session.SelectedLevelId);
            TutorialAuthoringRules.Connect(level, (TutorialFlowDefinition)graph.Resolve(flowId));
            ApplyLevel(session, graph, "공통 구성 연결");
        }
        public static void Synchronize(AuthoringEditSession session) => EditLevel(session, "레벨 설정 동기화", TutorialAuthoringRules.Synchronize);
        public static void Detach(AuthoringEditSession session) => EditLevel(session, "공통 구성 독립 복사", TutorialAuthoringRules.Detach);

        public static string CreateFlow(AuthoringEditSession session, string name)
        {
            RequireName(name);
            using var graph = new AuthoringObjectGraph(session.Documents);
            var level = (LevelDefinition)graph.Resolve(session.SelectedLevelId);
            if (level.Tutorial.flow != null) throw new InvalidOperationException("이미 공유 중입니다. 독립 복사 후 새 구성을 만드세요.");
            var flow = ScriptableObject.CreateInstance<TutorialFlowDefinition>();
            flow.name = name.Trim();
            string id = graph.Register(flow, "tutorialFlow");
            flow.steps = TutorialAuthoringRules.CopySteps(level.Tutorial.steps);
            TutorialAuthoringRules.EnsureIds(flow.steps);
            TutorialAuthoringRules.Connect(level, flow);
            var flowDocument = graph.Encode(id); JToken tutorial = graph.Encode(session.SelectedLevelId).Data["tutorial"];
            session.Apply("공통 구성 생성 및 연결", docs => { docs.Add(id, flowDocument); docs[session.SelectedLevelId].Data["tutorial"] = tutorial.DeepClone(); });
            return id;
        }
        public static string CreateSample(AuthoringEditSession session, string name, string description, int stepIndex)
        {
            RequireName(name);
            using var graph = new AuthoringObjectGraph(session.Documents);
            var level = (LevelDefinition)graph.Resolve(session.SelectedLevelId);
            var steps = TutorialFlowResolver.Resolve(level).steps;
            var sample = ScriptableObject.CreateInstance<TutorialUserSampleDefinition>();
            sample.name = name.Trim();
            string id = graph.Register(sample, "tutorialSample");
            sample.description = description;
            sample.steps = TutorialAuthoringRules.CopySteps(stepIndex < 0 ? steps : new List<TutorialStepDefinition> { steps[stepIndex] });
            TutorialAuthoringRules.EnsureIds(sample.steps);
            var document = graph.Encode(id);
            session.Apply("내 샘플 등록", docs => docs.Add(id, document)); return id;
        }
        public static void ApplySample(AuthoringEditSession session, string id)
        {
            using var graph = new AuthoringObjectGraph(session.Documents);
            var level = (LevelDefinition)graph.Resolve(session.SelectedLevelId);
            if (level.Tutorial.flow != null) throw new InvalidOperationException("공유 중에는 단계를 추가할 수 없습니다. 독립 복사를 먼저 선택하세요.");
            var sample = (TutorialUserSampleDefinition)graph.Resolve(id);
            level.Tutorial.steps.AddRange(TutorialAuthoringRules.CopySample(level.Tutorial.steps, sample.steps));
            ApplyLevel(session, graph, "내 샘플 적용");
        }
        public static string AddSampleBoard(AuthoringEditSession session, string sampleId)
        {
            var sample = TutorialSampleBoards.All.Single(value => value.Id == sampleId);
            using var graph = new AuthoringObjectGraph(session.Documents);
            var board = sample.CreateBoard();
            string temporary = graph.Register(board, "level");
            TutorialAuthoringRules.EnsureIds(board.Tutorial.steps);
            JObject template = graph.Encode(temporary).Data;
            template["catalogId"] = session.Get(session.SelectedLevelId).Data["catalogId"].DeepClone();
            int number = session.Documents.Where(doc => doc.Kind == "level").Max(doc => (int)doc.Data["levelNumber"]) + 1;
            return DocumentEditing.AddLevel(session, template, number, "시험 · " + sample.Title);
        }
        private static void ApplyLevel(AuthoringEditSession session, AuthoringObjectGraph graph, string label)
        {
            string id = session.SelectedLevelId;
            JToken tutorial = graph.Encode(id).Data["tutorial"];
            session.Apply(label, docs => docs[id].Data["tutorial"] = tutorial.DeepClone());
        }
        private static void RequireName(string name)
        { if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("이름을 입력하세요."); }
    }
}
#endif
