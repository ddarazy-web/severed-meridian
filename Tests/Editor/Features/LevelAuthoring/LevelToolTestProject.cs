using LevelAuthoring.Documents;
using LevelTool;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolTestProject
    {
        // 기존 편집 검사는 시작 UX가 아닌 레벨 편집을 대상으로 하므로 레벨을 명시적으로 준비한다.
        internal static ContentSnapshot Create()
        {
            ContentDocument project = LevelToolDocuments.NewProject().Project;
            return new ContentSnapshot(new[] { project, new ContentDocument("level", "level-test", LevelToolDocuments.NewLevel()) });
        }
    }
}
