using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class BotAnalysisVerification
    {
        private static LevelEditorWindow window;
        private static LevelDefinition uiLevel;
        private static IEnumerator uiSequence;
        private static double uiNext;
        private delegate bool AnalysisWindowVisitor(IntPtr handle, IntPtr value);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool EnumWindows(AnalysisWindowVisitor visitor, IntPtr value);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr handle, int command);

        public static void UI()
        {
            Results.Clear(); Directory.CreateDirectory(Evidence);
            uiSequence = UISequence(); EditorApplication.update += UITick;
        }

        private static void UITick()
        {
            if (EditorApplication.timeSinceStartup < uiNext) return;
            uiNext = EditorApplication.timeSinceStartup + .1;
            Exception failure = null;
            try { if (uiSequence.MoveNext()) return; }
            catch (Exception error) { failure = error; Results.Add("FAIL " + error); UnityEngine.Debug.LogException(error); }
            EditorApplication.update -= UITick;
            if (window != null) window.Close();
            if (uiLevel != null) UnityEngine.Object.DestroyImmediate(uiLevel);
            File.WriteAllLines(Evidence + "/ui-results.txt", Results);
            EditorApplication.Exit(failure == null ? 0 : 1);
        }

        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name);
            Check(button != null && button.enabledInHierarchy, "실제 버튼 접근 " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }

        private static IEnumerator UISequence()
        {
            string source = File.ReadAllText(Evidence + "/source-path.txt");
            BotAnalysisReader reader = Load(source);
            uiLevel = ScriptableObject.CreateInstance<LevelDefinition>();
            JsonUtility.FromJsonOverwrite(reader.Record.definitionJson, uiLevel);
            string original = JsonUtility.ToJson(uiLevel); bool dirty = EditorUtility.IsDirty(uiLevel);
            window = LevelEditorWindow.OpenWorkspace(3, uiLevel, true);
            window.ShowUtility(); window.position = new Rect(10, 10, 1050, 850);
            yield return null;
#if UNITY_EDITOR_WIN
            // 숨겨서 시작한 검증 Editor의 창만 복원한다. 사용자가 실행한 다른 Unity는 건드리지 않는다.
            uint owner = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumWindows((handle, value) => { GetWindowThreadProcessId(handle, out uint process); if (process == owner) ShowWindow(handle, 9); return true; }, IntPtr.Zero);
#endif
            typeof(ConnectionGraphVerification).GetField("window", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, window);
            typeof(ConnectionGraphVerification).GetMethod("RaiseWindow", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            yield return null; yield return null;
            LevelAnalysisPanel panel = (LevelAnalysisPanel)typeof(LevelEditorWindow).GetField("analysisPanel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
            Check(window.WorkspaceTab == 3 && panel.Root.panel != null && panel.Root.resolvedStyle.display == DisplayStyle.Flex, "통합 결과·이력 탭 실제 패널");
            panel.OpenRecord(source);
            for (int wait = 0; panel.Analysis?.IsDone != true && wait < 400; wait++) yield return null;
            Check(panel.Analysis?.IsDone == true && panel.Analysis.Error == null, "갱신 분할 200판 읽기 완료");
            Check(window.rootVisualElement.Q<Label>("analysis-basic").text.Contains("100") && window.rootVisualElement.Q<Label>("analysis-planning").text.Contains("100"),
                "기본/계획 전략별 표본 화면");
            Check(window.rootVisualElement.Q<Label>("analysis-identity").text.Contains("같은 정의"), "현재 편집 내용과 같은 조건 표시");
            Click("analysis-reference"); Click("analysis-tab-compare"); yield return null;
            Check(window.rootVisualElement.Q<Label>("analysis-comparison-info").text.Contains("같은 시드 묶음"), "비교 조건과 시드 표시");
            Click("analysis-tab-cases"); yield return null;
            window.rootVisualElement.Q<PopupField<string>>("analysis-strategy-filter").index = 1; yield return null;
            ListView cases = window.rootVisualElement.Q<ListView>("analysis-cases");
            Check(cases.itemsSource.Count == 100, "전략 필터 기본 100판");
            cases.SetSelection(0); yield return null;
            Check(window.rootVisualElement.Q<Label>("analysis-case-info").text.Contains("난수 소비"), "선택 사례 원시 행동 표시");
            Click("analysis-replay-start");
            for (int wait = 0; panel.ReplaySession.Status == BotReplayStatus.Preparing && wait < 400; wait++) yield return null;
            Check(panel.ReplaySession.Status == BotReplayStatus.Ready && panel.ReplaySession.State != null, "저장 사본의 시작 보드 준비");
            Check(window.rootVisualElement.Q("analysis-replay-grid").childCount > 0, "공유 블록 표시로 재생 보드 렌더링");
            Click("analysis-replay-next");
            for (int wait = 0; panel.ReplaySession.NeedsAdvance && wait < 400; wait++) yield return null;
            Check(panel.ReplaySession.ActionIndex == 1 && panel.ReplaySession.Status == BotReplayStatus.Ready, "다음 행동은 후속 처리까지 한 번");
            Click("analysis-replay-run"); Click("analysis-replay-pause"); yield return null;
            int index = panel.ReplaySession.ActionIndex, random = panel.ReplaySession.State.Random.DrawCount;
            yield return null; yield return null;
            Check(panel.ReplaySession.Status == BotReplayStatus.Paused && panel.ReplaySession.ActionIndex == index && panel.ReplaySession.State.Random.DrawCount == random, "일시정지 중 진행 없음");
            Click("workspace-tab-0"); yield return null; Click("workspace-tab-3"); yield return null;
            Check(panel.ReplaySession.Status == BotReplayStatus.Paused, "탭 복귀 시 자동 재생하지 않음");
            Click("analysis-replay-run");
            for (int wait = 0; panel.ReplaySession.NeedsAdvance && wait < 400; wait++) yield return null;
            Check(panel.ReplaySession.Status == BotReplayStatus.Completed, "자동 재생 완료와 기록 대조");
            window.position = new Rect(10, 10, 1000, 760); yield return null; yield return null;
            CaptureAnalysis("analysis-replay.png");
            Click("analysis-replay-start"); yield return null;
            Click("analysis-replay-close"); yield return null;
            Check(panel.ReplaySession == null && window.rootVisualElement.Q("analysis-replay-grid").childCount == 0, "닫기는 재생 사본만 해제");
            Check(JsonUtility.ToJson(uiLevel) == original && EditorUtility.IsDirty(uiLevel) == dirty, "UI 분석/재생 후 편집 정의와 dirty 보존");
            window.position = new Rect(10, 10, 1000, 760); yield return null; yield return null;
            Button end = window.rootVisualElement.Q<Button>("analysis-replay-close");
            Check(end.worldBound.yMax < window.position.height && end.worldBound.xMax <= window.position.width, "최소 크기에서 재생 제어 접근");
            CaptureAnalysis("analysis-cases.png");
            Click("analysis-tab-summary"); yield return null; CaptureAnalysis("analysis-summary.png");
            string exportPath = Path.GetFullPath(Evidence + "/ui-export-" + Guid.NewGuid().ToString("N"));
            panel.BeginExport(exportPath);
            for (int wait = 0; !File.Exists(Path.Combine(exportPath, "batch.json")) && wait < 400; wait++) yield return null;
            Check(File.Exists(Path.Combine(exportPath, "batch.json")) && Load(exportPath).Games.Count == 200, "UI 내보내기 처리 완료");
            // 비교 표시 검사용 합성 이력이다. 새 시드로 실제 플레이한 기록이라고 보고하지 않는다.
            BotBatchRecord different = JsonUtility.FromJson<BotBatchRecord>(File.ReadAllText(Path.Combine(exportPath, "batch.json")));
            LevelDefinition variant = ScriptableObject.CreateInstance<LevelDefinition>();
            try
            {
                JsonUtility.FromJsonOverwrite(different.definitionJson, variant);
                JsonUtility.FromJsonOverwrite("{\"levelNumber\":281}", variant);
                different.definitionJson = JsonUtility.ToJson(variant); different.fingerprint = LevelStateBuilder.Fingerprint(variant);
            }
            finally { UnityEngine.Object.DestroyImmediate(variant); }
            different.seeds = different.seeds.Select(seed => unchecked(seed + 100000)).ToArray();
            for (int i = 0; i < 200; i++)
            {
                string path = Path.Combine(exportPath, i.ToString("D6") + ".json");
                BotBatchGame game = JsonUtility.FromJson<BotBatchGame>(File.ReadAllText(path));
                game.seed = different.seeds[i / 2]; File.WriteAllText(path, JsonUtility.ToJson(game));
            }
            File.WriteAllText(Path.Combine(exportPath, "batch.json"), JsonUtility.ToJson(different));
            panel.OpenRecord(exportPath);
            Check(window.rootVisualElement.Q<Label>("analysis-case-info").text == "" &&
                !window.rootVisualElement.Q<Label>("analysis-comparison-info").text.Contains("같은 시드 묶음"), "다른 이력을 읽는 동안 이전 사례·비교 결과 제거");
            for (int wait = 0; panel.Analysis?.IsDone != true && wait < 400; wait++) yield return null;
            Click("analysis-tab-compare"); yield return null;
            string comparison = window.rootVisualElement.Q<Label>("analysis-comparison-info").text;
            Check(comparison.Contains("서로 다른 정의") && comparison.Contains("다른 시드 묶음") && comparison.Contains("같은 규칙"), "수정 정의·다른 시드 이력 비교 표시");
            Check(window.rootVisualElement.Q<Label>("analysis-identity").text.Contains("다른 과거 사본"), "과거 결과를 현재 편집 결과로 표시하지 않음");
            different.engineVersion = "unsupported-test-version";
            File.WriteAllText(Path.Combine(exportPath, "batch.json"), JsonUtility.ToJson(different));
            panel.OpenRecord(exportPath);
            for (int wait = 0; panel.Analysis?.IsDone != true && wait < 400; wait++) yield return null;
            Check(window.rootVisualElement.Q<Label>("analysis-comparison-info").text.Contains("쌍 비교 제외"), "버전 차이 시 통계는 유지하고 쌍 비교 제외");
            Click("analysis-tab-cases"); yield return null; cases.SetSelection(0); yield return null;
            Check(!window.rootVisualElement.Q<Button>("analysis-replay-start").enabledInHierarchy, "비호환 사례 재생 버튼 차단");
            panel.OpenRecord(source);
            for (int wait = 0; panel.Analysis?.IsDone != true && wait < 400; wait++) yield return null;
            Click("analysis-tab-cases"); yield return null; cases.SetSelection(0); yield return null;
            Click("workspace-tab-1"); yield return null;
            LevelInitialStatePanel trial = window.ActiveSimulationPanel;
            typeof(LevelInitialStatePanel).GetField("batchStore", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(trial, new BotBatchStore(Path.GetFullPath(Evidence + "/ui-owned-batch-" + Guid.NewGuid().ToString("N"))));
            trial.rootVisualElement.Q<Foldout>("bot-trial").value = true;
            trial.rootVisualElement.Q<IntegerField>("batch-count").value = 1;
            // 같은 이름의 진단 패널 버튼 대신 현재 플레이 패널의 버튼에 보낸다.
            Button batchStart = trial.rootVisualElement.Q<Button>("batch-new");
            using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = batchStart; batchStart.SendEvent(evt); }
            BotBatchSession batch = (BotBatchSession)typeof(LevelInitialStatePanel).GetField("batchSession", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trial);
            Check(batch?.NeedsAdvance == true, "실제 플레이 패널 반복 시험 시작");
            panel.BeginExport(Path.GetFullPath(Evidence + "/blocked-export-" + Guid.NewGuid().ToString("N")));
            Check(window.rootVisualElement.Q<Label>("analysis-notice").text.Contains("일시정지"), "실행 중 시험의 보관 차단");
            Click("workspace-tab-3"); yield return null;
            Check(batch.Record.status == BotBatchStatus.Paused, "분석 탭 이동 시 반복 시험 일시정지");
            int draws = batch.Current?.State?.Random.DrawCount ?? -1, recorded = batch.Record.Recorded;
            Click("analysis-replay-start");
            for (int wait = 0; panel.ReplaySession?.Status == BotReplayStatus.Preparing && wait < 400; wait++) yield return null;
            Click("analysis-replay-run");
            for (int wait = 0; panel.ReplaySession?.NeedsAdvance == true && wait < 400; wait++) yield return null;
            Check(panel.ReplaySession?.Status == BotReplayStatus.Completed && batch.Record.status == BotBatchStatus.Paused &&
                (batch.Current?.State?.Random.DrawCount ?? -1) == draws && batch.Record.Recorded == recorded, "재생과 일시정지된 봇 시험의 상태·난수 독립");
            Click("analysis-replay-close"); batch.Stop();
            Button help = window.rootVisualElement.Q<Button>("manual-help-analysis.html-summary");
            Check(help != null && help.worldBound.width >= 18 && help.tooltip.Contains("사용 설명서") &&
                File.ReadAllText("Docs/MoonRabbitJunkyard/Manual/analysis.html").Contains("id=\"summary\""), "도움말 아이콘 영역·툴팁·연결 문단 존재");
            window.CreateGUI(); window.SelectWorkspaceTab(3); yield return null;
            panel = (LevelAnalysisPanel)typeof(LevelEditorWindow).GetField("analysisPanel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
            panel.OpenRecord(exportPath);
            for (int wait = 0; panel.Analysis?.IsDone != true && wait < 400; wait++) yield return null;
            Check(panel.Analysis?.Games.Count == 200 && panel.Analysis.Error == null, "창 UI 재생성 후 보관본 열기");
        }

    }
}
