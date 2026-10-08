using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;
using UnityEditor;
using UnityEngine;

namespace Tutorial.Editor
{
    public static class TutorialFlowAuthoring
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
            Undo.RecordObject(level, "공통 진행 구성 연결");
            level.Tutorial.flow = flow;
            level.Tutorial.bindings = values;
            EditorUtility.SetDirty(level);
        }

        public static void Synchronize(LevelDefinition level)
        {
            List<TutorialFlowBinding> updated = level.Tutorial.flow.parameters.Select(parameter =>
                level.Tutorial.bindings.FirstOrDefault(value => value.key == parameter.key && value.field == parameter.field) ?? DefaultValue(level.Tutorial.flow, parameter)).ToList();
            Undo.RecordObject(level, "레벨별 설정 목록 동기화"); level.Tutorial.bindings = updated; EditorUtility.SetDirty(level);
        }

        public static void Detach(LevelDefinition level)
        {
            LevelTutorialDefinition resolved = TutorialFlowResolver.Resolve(level);
            Undo.RecordObject(level, "공통 진행 구성 독립 복사");
            level.Tutorial.steps = resolved.steps; level.Tutorial.bindings.Clear();
            level.Tutorial.flow = null; EditorUtility.SetDirty(level);
        }
    }
}
