using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;

namespace Tutorial
{
    public static partial class TutorialAuthoringRules
    {
        public static List<TutorialStepDefinition> CopySteps(List<TutorialStepDefinition> steps)
            => steps?.Select(step => step?.Copy()).ToList();

        public static void EnsureIds(List<TutorialStepDefinition> steps)
        {
            var ids = new HashSet<string>();
            foreach (TutorialStepDefinition step in steps)
            {
                if (string.IsNullOrWhiteSpace(step.authoringId) || !ids.Add(step.authoringId))
                { step.authoringId = Guid.NewGuid().ToString("N"); ids.Add(step.authoringId); }
                var conditions = new HashSet<string>();
                foreach (TutorialConditionDefinition condition in step.conditions)
                    if (string.IsNullOrWhiteSpace(condition.authoringId) || !conditions.Add(condition.authoringId))
                    { condition.authoringId = Guid.NewGuid().ToString("N"); conditions.Add(condition.authoringId); }
            }
        }

        public static string Label(TutorialFlowField field) => field switch
        {
            TutorialFlowField.First => "첫 조작 칸", TutorialFlowField.Second => "둘째 조작 칸",
            TutorialFlowField.ActionArea => "조작 영역", TutorialFlowField.Highlights => "강조 영역",
            TutorialFlowField.FreeItemCount => "무료 체험 횟수", TutorialFlowField.RequiredCount => "조건 횟수·감소량",
            TutorialFlowField.MatchSize => "매칭 블록 수", TutorialFlowField.Target => "조건 대상",
            TutorialFlowField.PowerDefinitionId => "조건 파워 종류", TutorialFlowField.Color => "매칭 색상",
            TutorialFlowField.MissionIndex => "미션 번호", TutorialFlowField.ActionDefinitionId => "조작 파워 종류", _ => field.ToString()
        };

        public static TutorialFlowBinding DefaultValue(TutorialFlowDefinition flow, TutorialFlowParameter parameter)
        {
            TutorialStepDefinition step = flow.steps.Single(value => value.authoringId == parameter.stepId);
            TutorialConditionDefinition condition = step.conditions.SingleOrDefault(value => value.authoringId == parameter.conditionId);
            TutorialFlowBinding value = new TutorialFlowBinding { key = parameter.key, field = parameter.field };
            switch (parameter.field)
            {
                case TutorialFlowField.First: value.coordinate = step.first; break;
                case TutorialFlowField.Second: value.coordinate = step.second; break;
                case TutorialFlowField.ActionArea: value.cells = new List<BoardCoordinate>(step.actionArea); break;
                case TutorialFlowField.Highlights: value.cells = new List<BoardCoordinate>(step.highlights); break;
                case TutorialFlowField.FreeItemCount: value.number = step.freeItemCount; break;
                case TutorialFlowField.RequiredCount: value.number = condition.requiredCount; break;
                case TutorialFlowField.MatchSize: value.number = condition.matchSize; break;
                case TutorialFlowField.Target: value.target = condition.Copy().target ?? new TutorialTargetDefinition(); break;
                case TutorialFlowField.PowerDefinitionId: value.definitionId = condition.powerDefinitionId; break;
                case TutorialFlowField.Color: value.color = condition.color; break;
                case TutorialFlowField.MissionIndex: value.number = condition.missionIndex; break;
                case TutorialFlowField.ActionDefinitionId: value.definitionId = step.actionDefinitionId; break;
            }
            return value;
        }

        public static void Connect(LevelDefinition level, TutorialFlowDefinition flow)
        {
            List<TutorialFlowBinding> values = flow.parameters.Select(parameter => DefaultValue(flow, parameter)).ToList();
            level.Tutorial.flow = flow;
            level.Tutorial.bindings = values;

        }

        public static void Synchronize(LevelDefinition level)
        {
            List<TutorialFlowBinding> updated = level.Tutorial.flow.parameters.Select(parameter =>
                level.Tutorial.bindings.FirstOrDefault(value => value.key == parameter.key && value.field == parameter.field) ?? DefaultValue(level.Tutorial.flow, parameter)).ToList();
            level.Tutorial.bindings = updated;
        }

        public static void Detach(LevelDefinition level)
        {
            LevelTutorialDefinition resolved = TutorialFlowResolver.Resolve(level);
            level.Tutorial.steps = resolved.steps; level.Tutorial.bindings.Clear();
            level.Tutorial.flow = null;
        }
        public static List<TutorialStepDefinition> CopySample(List<TutorialStepDefinition> existing, List<TutorialStepDefinition> sample)
        {
            List<TutorialStepDefinition> copy = CopySteps(sample);
            HashSet<string> used = new HashSet<string>(existing.SelectMany(step => step.conditions)
                .Select(condition => condition.bindGeneratedAs).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            Dictionary<string, string> renamed = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (TutorialConditionDefinition condition in copy.SelectMany(step => step.conditions).Where(value => !string.IsNullOrWhiteSpace(value.bindGeneratedAs)))
            {
                string original = condition.bindGeneratedAs;
                if (renamed.ContainsKey(original)) throw new ArgumentException("샘플 내부의 생성 연결 이름이 중복입니다: " + original);
                string name = original; int suffix = 2;
                while (!used.Add(name)) name = original + "_" + suffix++;
                renamed.Add(original, name); condition.bindGeneratedAs = name;
            }
            foreach (TutorialStepDefinition step in copy)
            {
                if (renamed.TryGetValue(step.firstBinding, out string first)) step.firstBinding = first;
                if (renamed.TryGetValue(step.secondBinding, out string second)) step.secondBinding = second;
                foreach (TutorialConditionDefinition condition in step.conditions)
                    if (condition.target?.kind == TutorialTargetKind.Generated && renamed.TryGetValue(condition.target.binding, out string target)) condition.target.binding = target;
            }
            foreach (TutorialStepDefinition step in copy)
            { step.authoringId = Guid.NewGuid().ToString("N"); foreach (TutorialConditionDefinition condition in step.conditions) condition.authoringId = Guid.NewGuid().ToString("N"); }
            return copy;
        }
    }
}
