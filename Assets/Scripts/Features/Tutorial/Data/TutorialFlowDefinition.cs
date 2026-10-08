using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tutorial
{
    public enum TutorialFlowField
    {
        [InspectorName("첫 조작 칸")] First,
        [InspectorName("둘째 조작 칸")] Second,
        [InspectorName("조작 영역")] ActionArea,
        [InspectorName("강조 영역")] Highlights,
        [InspectorName("무료 체험 횟수")] FreeItemCount,
        [InspectorName("조건 횟수·감소량")] RequiredCount,
        [InspectorName("매칭 크기")] MatchSize,
        [InspectorName("조건 대상")] Target,
        [InspectorName("조건 파워 종류")] PowerDefinitionId,
        [InspectorName("매칭 색상")] Color,
        [InspectorName("미션 번호")] MissionIndex,
        [InspectorName("조작 파워 종류")] ActionDefinitionId
    }

    [Serializable]
    public sealed class TutorialFlowParameter
    {
        public string key = "";
        public string label = "";
        [TextArea] public string help = "";
        public string stepId = "";
        public string conditionId = "";
        public TutorialFlowField field;
    }

    [CreateAssetMenu(menuName = "달 토끼/튜토리얼/공통 진행 구성", fileName = "TutorialFlow")]
    public sealed class TutorialFlowDefinition : ScriptableObject
    {
        public List<TutorialStepDefinition> steps = new List<TutorialStepDefinition>();
        public List<TutorialFlowParameter> parameters = new List<TutorialFlowParameter>();
    }
}
