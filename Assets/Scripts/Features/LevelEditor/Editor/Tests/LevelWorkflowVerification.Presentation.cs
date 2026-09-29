using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class LevelWorkflowVerification
    {
        /// <summary>기존 결과를 재사용하여 실제 도킹·결과 최소 창·10×10 조작을 검사한다.</summary>
        public static void Presentation()
        {
            Results.Clear(); resultOverride = "presentation";
            level = (LevelDefinition)typeof(ItemBoosterVerification).GetMethod("PlayFixture", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            // Unity의 실제 Scene 뷰 옆 도킹 API를 사용한다. 좁은 ScrollView 흉내와 구분한다.
            window = EditorWindow.GetWindow<LevelEditorWindow>("Match", false, typeof(SceneView));
            window.SetLevel(level);
            sequence = PresentationSequence(); EditorApplication.update += Tick;
        }

        /// <summary>화면 갱신을 기다린 뒤 실제 치수와 입력 결과를 기록한다.</summary>
        /// <returns>Unity 갱신 사이에 실행할 검사 단계.</returns>
        private static IEnumerator PresentationSequence()
        {
            yield return null;
            Check(window.docked, "실제 Unity DockArea에 작업창 도킹");
            string original = JsonUtility.ToJson(level);
            for (int tab = 0; tab < 4; tab++)
            {
                window.SelectWorkspaceTab(tab);
                // EditorWindow.position은 도킹을 해제한다. 검사 소유 프로세스의 실제
                // DockArea View를 줄여 Unity 도킹 상태와 UI Toolkit 배치를 함께 검사한다.
                object host = typeof(EditorWindow).GetField("m_Parent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                host.GetType().GetProperty("position", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(host, new Rect(0, 0, 600, 400));
                for (int frame = 0; frame < 5; frame++) yield return null;
                ScrollView viewport = window.rootVisualElement.Q<ScrollView>("workspace-viewport");
                Results.Add($"GEOMETRY tab={tab} docked={window.docked} position={window.position} root={window.rootVisualElement.layout} scroll={viewport.horizontalScroller.highValue},{viewport.verticalScroller.highValue}");
                Check(window.docked && (viewport.horizontalScroller.highValue > 0 || viewport.verticalScroller.highValue > 0), "실제 좁은 도킹에서 숨은 작업 영역 스크롤 탭 " + tab);
                viewport.scrollOffset = new Vector2(viewport.horizontalScroller.highValue, viewport.verticalScroller.highValue);
                yield return null;
                Check(viewport.scrollOffset.x > 0 || viewport.scrollOffset.y > 0, "도킹 스크롤로 끝 영역 이동 탭 " + tab);
                viewport.scrollOffset = Vector2.zero;
            }
            // 독립 창은 탭별 최소 크기로 자동 확장한다. 사용자 레벨은 만들거나 저장하지 않는다.
            window.Close(); window = LevelEditorWindow.OpenWorkspace(1, level, true);
            window.ShowUtility(); window.position = new Rect(20, 20, 760, 860);
            yield return null; play = window.ActiveSimulationPanel;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Click("manual-start");
            while (play.IsSearching) yield return null;
            if (play.Execution == null) { Click("manual-start"); yield return null; }
            Check(play.CurrentState.Rows == 10 && play.CurrentState.Columns == 10 && play.Execution != null, "실제 10×10 수동 판 준비");
            Results.Add("TIME 10x10 준비 UI 포함 ms=" + watch.Elapsed.TotalMilliseconds.ToString("F3"));
            var target = play.CurrentState.Cells.First(c => c.Content == Simulation.RuntimeContent.Normal).Coordinate;
            watch.Restart(); Click("item-Hammer"); Click($"initial-cell-{target.Row}-{target.Column}");
            while (play.CascadeRunning) yield return null;
            Check(play.Execution.ItemUses.Count == 1, "10×10 아이템 조작과 후속 연쇄 완료");
            Results.Add("TIME 10x10 아이템·연쇄 UI 포함 ms=" + watch.Elapsed.TotalMilliseconds.ToString("F3"));
            string path = File.ReadAllText(Evidence + "/connections-record.txt");
            window.SelectWorkspaceTab(3); window.position = new Rect(20, 20, 680, 480);
            var panel = (LevelAnalysisPanel)typeof(LevelEditorWindow).GetField("analysisPanel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
            panel.OpenRecord(path);
            while (panel.Analysis?.IsDone != true) yield return null;
            for (int frame = 0; frame < 8; frame++) { window.Repaint(); yield return null; }
            Check(window.position.width >= 1000 && window.position.height >= 760, "결과 탭 최소 크기 자동 확대");
            Check(window.rootVisualElement.Q<Label>("analysis-difficulty-title") != null, "결과 추천 표시");
            window.rootVisualElement.Q<Foldout>("analysis-difficulty-evidence").value = true;
            for (int frame = 0; frame < 5; frame++) yield return null;
            Button help = window.rootVisualElement.Query<Button>(className: "manual-help-icon").ToList()
                .First(button => button.name.Contains("difficulty.html") && button.worldBound.width > 0);
            Check(help.worldBound.width >= 19 && help.tooltip.Contains("클릭"), "결과 도움말 클릭 영역·툴팁 설명");
            Check(File.Exists("Docs/MoonRabbitJunkyard/Manual/difficulty.html"), "결과 도움말 연결 파일");
            typeof(BotAnalysisVerification).GetField("window", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, window);
            typeof(BotAnalysisVerification).GetMethod("CaptureAnalysis", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { "workflow-result-final.png" });
            File.Copy("Logs/BotAnalysisVerification/workflow-result-final.png", Evidence + "/result-final.png", true);
            Check(JsonUtility.ToJson(level) == original, "도킹·수동·분석 검사 후 원본 보존");
        }
    }
}
