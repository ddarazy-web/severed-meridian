using System;
using System.Collections.Generic;
using System.Linq;
using Board;
using Levels;
using MemoryPack;

namespace Tutorial
{
    /// <summary>공유 원본과 레벨별 값을 독립 실행 데이터로 합친다. 제작 데이터는 변경하지 않는다.</summary>
    public static class TutorialFlowResolver
    {
        public static LevelTutorialDefinition Resolve(LevelDefinition level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            LevelTutorialDefinition source = level.Tutorial;
            if (source == null) return null;
            LevelTutorialDefinition result = new LevelTutorialDefinition
            {
                seed = source.seed, steps = source.steps?.Select(step => step?.Copy()).ToList(),
                supply = MemoryPackSerializer.Deserialize<ElementLevelSupplyDefinition>(MemoryPackSerializer.Serialize(source.supply)),
                completionId = source.completionId,
                previousLevelNumbers = source.previousLevelNumbers == null ? null : new List<int>(source.previousLevelNumbers)
            };
            result.flow = null; result.bindings.Clear();
            if (source.flow == null) return result;
            string prefix = $"레벨 {level.LevelNumber} · tutorial.bindings";
            void Error(string message) => throw new ArgumentException(prefix + ": " + message);
            TutorialFlowDefinition flow = source.flow;
            if (flow.steps == null || flow.parameters == null || source.bindings == null) Error("공통 진행 구성 또는 레벨별 설정 목록이 없습니다.");
            result.steps = flow.steps.Select(step => step?.Copy()).ToList();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (TutorialStepDefinition step in result.steps)
            {
                if (step == null || string.IsNullOrWhiteSpace(step.authoringId) || !ids.Add(step.authoringId)) Error("공통 단계 ID가 없거나 중복입니다.");
                var conditionIds = new HashSet<string>(StringComparer.Ordinal);
                if (step.conditions == null) Error("조건 목록이 없습니다: " + step.authoringId);
                foreach (TutorialConditionDefinition condition in step.conditions)
                    if (condition == null || string.IsNullOrWhiteSpace(condition.authoringId) || !conditionIds.Add(condition.authoringId)) Error("조건 ID가 없거나 중복입니다: " + step.authoringId);
            }
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var destinations = new HashSet<string>(StringComparer.Ordinal);
            foreach (TutorialFlowParameter parameter in flow.parameters)
            {
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.key) || !keys.Add(parameter.key)) Error("레벨별 설정 이름이 없거나 중복입니다.");
                string location = parameter.stepId + "/" + parameter.conditionId + "/" + parameter.field;
                if (!destinations.Add(location)) Error(parameter.key + ": 같은 필드에 연결을 두 번 선언했습니다.");
                TutorialStepDefinition step = result.steps.SingleOrDefault(value => value.authoringId == parameter.stepId);
                if (step == null) Error(parameter.key + ": 연결한 단계가 삭제되었습니다.");
                TutorialFlowBinding[] values = source.bindings.Where(value => value != null && value.key == parameter.key).ToArray();
                if (values.Length != 1) Error(parameter.key + ": 이 레벨의 설정을 한 번 입력하세요.");
                TutorialFlowBinding value = values[0];
                if (value.field != parameter.field) Error(parameter.key + ": 설정 종류가 공통 선언과 다릅니다.");
                bool conditionField = parameter.field >= TutorialFlowField.RequiredCount && parameter.field <= TutorialFlowField.MissionIndex;
                TutorialConditionDefinition condition = conditionField ? step.conditions.SingleOrDefault(item => item.authoringId == parameter.conditionId) : null;
                if ((parameter.field == TutorialFlowField.First && !string.IsNullOrEmpty(step.firstBinding)) || (parameter.field == TutorialFlowField.Second && !string.IsNullOrEmpty(step.secondBinding))) Error(parameter.key + ": 생성 개체 연결을 좌표 설정으로 덮어쓸 수 없습니다.");
                if (conditionField && condition == null) Error(parameter.key + ": 연결한 조건이 삭제되었습니다.");
                switch (parameter.field)
                {
                    case TutorialFlowField.First: step.first = value.coordinate; step.hasFirst = true; step.firstBinding = ""; break;
                    case TutorialFlowField.Second: step.second = value.coordinate; step.hasSecond = true; step.secondBinding = ""; break;
                    case TutorialFlowField.ActionArea: step.actionArea = value.cells == null ? null : new List<BoardCoordinate>(value.cells); break;
                    case TutorialFlowField.Highlights: step.highlights = value.cells == null ? null : new List<BoardCoordinate>(value.cells); break;
                    case TutorialFlowField.FreeItemCount: step.freeItemCount = value.number; break;
                    case TutorialFlowField.RequiredCount: condition.requiredCount = value.number; break;
                    case TutorialFlowField.MatchSize: condition.matchSize = value.number; break;
                    case TutorialFlowField.Target: condition.target = value.target == null ? null : new TutorialConditionDefinition { target = value.target }.Copy().target; break;
                    case TutorialFlowField.PowerDefinitionId: condition.powerDefinitionId = value.definitionId; break;
                    case TutorialFlowField.Color: condition.anyColor = false; condition.color = value.color; break;
                    case TutorialFlowField.MissionIndex:
                        if (value.number < 0 || value.number >= level.Missions.Count) Error(parameter.key + ": 미션 번호가 유효하지 않습니다.");
                        condition.SelectMission(value.number, level.Missions[value.number]); break;
                    case TutorialFlowField.ActionDefinitionId: step.actionDefinitionId = value.definitionId; break;
                    default: Error(parameter.key + ": 지원하지 않는 설정 종류입니다."); break;
                }
            }
            if (source.bindings.Any(value => value == null || !keys.Contains(value.key))) Error("공통 구성에서 삭제된 연결 값이 있습니다. 설정 목록을 동기화하세요.");
            LevelTutorialValidator.ValidateBindings(result.steps, (path, message) => Error(path + ": " + message));
            return result;
        }
    }
}
