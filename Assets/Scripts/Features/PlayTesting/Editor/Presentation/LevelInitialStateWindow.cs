using UnityEditor;

namespace Levels.Editor
{
    // 기존 메뉴와 호출부도 통합 창의 해당 탭으로 진입한다.
    public static class LevelInitialStateWindow
    {
        [MenuItem("Match/초기 보드 확인")]
        public static void Open() => LevelTool.Editor.LevelToolLauncher.Launch();

        [MenuItem("Match/플레이 테스트")]
        public static void OpenManual() => LevelTool.Editor.LevelToolLauncher.Launch();

        public static void OpenManualLevel(LevelDefinition target)
        {
            LevelTool.Editor.LevelToolLegacyImport.Open(target);
        }
    }
}
