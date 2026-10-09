using System.Collections.Generic;
using Levels;
using UnityEditor;

namespace Tutorial.Editor
{
    // 기존 창의 Unity Undo만 담당하며 제작 규칙은 실행용 도구와 공유한다.
    public static class TutorialFlowAuthoring
    {
        public static List<TutorialStepDefinition> CopySteps(List<TutorialStepDefinition> steps) => TutorialAuthoringRules.CopySteps(steps);
        public static void EnsureIds(List<TutorialStepDefinition> steps) => TutorialAuthoringRules.EnsureIds(steps);
        public static string Label(TutorialFlowField field) => TutorialAuthoringRules.Label(field);
        public static TutorialFlowBinding DefaultValue(TutorialFlowDefinition flow, TutorialFlowParameter parameter)
            => TutorialAuthoringRules.DefaultValue(flow, parameter);
        public static void Connect(LevelDefinition level, TutorialFlowDefinition flow)
        {
            Undo.RecordObject(level, "공통 진행 구성 연결");
            TutorialAuthoringRules.Connect(level, flow); EditorUtility.SetDirty(level);
        }
        public static void Synchronize(LevelDefinition level)
        {
            Undo.RecordObject(level, "레벨별 설정 목록 동기화");
            TutorialAuthoringRules.Synchronize(level); EditorUtility.SetDirty(level);
        }
        public static void Detach(LevelDefinition level)
        {
            Undo.RecordObject(level, "공통 진행 구성 독립 복사");
            TutorialAuthoringRules.Detach(level); EditorUtility.SetDirty(level);
        }
    }
}
