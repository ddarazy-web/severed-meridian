using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>33단계 연속 사용 검사. 실제 판은 두 레벨·두 전략 각 한 판이며 기록은 소유 루트에만 쓴다.</summary>
    public static class WorkspaceWorkflowVerification
    {
        private const string Evidence = "Logs/Stage33Verification";
        private static readonly List<string> results = new List<string>();
        private static readonly List<LevelDefinition> owned = new List<LevelDefinition>();
        private static LevelEditorWindow window;
        private static IEnumerator steps;
        private static double deadline;
        private static MultiLevelTestStore store;
        private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string ReloadKey = "Match.Stage33Reload";
        private static int reloadMarker;
        private static bool presentation;
        private static bool menuCapture;

        /// <summary>최종 배치를 캡처하고 중단 검사에서 남긴 소유 임시 에셋을 정리한다.</summary>
        public static void FinalAudit()
        { menuCapture = true; results.Clear(); steps = CaptureWorkspace(); deadline = EditorApplication.timeSinceStartup + 60; EditorApplication.update += Tick; }
        private static IEnumerator CaptureWorkspace()
        {
            window = LevelEditorWindow.OpenWorkspace(4, null, true); window.ShowUtility();
            window.position = new Rect(20, 20, 1000, 788);
            for (int i = 0; i < 10; i++) yield return null;
            typeof(BotAnalysisVerification).GetMethod("CaptureAnalysis", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { "stage33-workspace.png" });
            File.Copy("Logs/BotAnalysisVerification/stage33-workspace.png", Evidence + "/workspace.png", true);
            Check(window.rootVisualElement.Q("multi-setup-card").worldBound.width > 0 && window.rootVisualElement.Q("multi-review-card").worldBound.width > 0, "최종 준비·결과 카드 캡처");
            // 이전 강제 종료가 남긴 폴더도 이 검사의 고유 이름 범위만 정리한다.
            foreach (string folder in Directory.GetDirectories("Assets", "__Stage33Presentation_*", SearchOption.TopDirectoryOnly))
                Check(AssetDatabase.DeleteAsset(folder.Replace(Path.DirectorySeparatorChar, '/')), "검사 소유 재로드 임시 폴더 정리");
        }
        /// <summary>추가 플레이 없이 입력·실제 도킹·창 수명주기를 검사한다.</summary>
        public static void Presentation()
        { presentation = true; results.Clear(); steps = Present(); deadline = EditorApplication.timeSinceStartup + 120; EditorApplication.update += Tick; }

        private static IEnumerator Present()
        {
            string folder = "Assets/__Stage33Presentation_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            SessionState.SetString(ReloadKey + ".folder", folder);
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            AssetDatabase.CreateAsset(level, folder + "/First.asset");
            LevelDefinition duplicate = ScriptableObject.CreateInstance<LevelDefinition>();
            AssetDatabase.CreateAsset(duplicate, folder + "/Other.asset");
            window = EditorWindow.GetWindow<LevelEditorWindow>("Match", false, typeof(SceneView)); window.SetLevel(level);
            yield return null;
            Check(window.docked, "실제 Unity DockArea 도킹");
            foreach (int tab in new[] { 0, 1, 2, 4, 3 })
            {
                window.SelectWorkspaceTab(tab);
                object host = typeof(EditorWindow).GetField("m_Parent", Hidden).GetValue(window);
                host.GetType().GetProperty("position", BindingFlags.Public | Hidden).SetValue(host, new Rect(0, 0, 600, 400));
                for (int i = 0; i < 6; i++) yield return null;
                ScrollView viewport = window.rootVisualElement.Q<ScrollView>("workspace-viewport");
                Check(window.docked && (viewport.horizontalScroller.highValue > 0 || viewport.verticalScroller.highValue > 0), "좁은 실제 도킹 스크롤 " + tab);
                viewport.scrollOffset = new Vector2(viewport.horizontalScroller.highValue, viewport.verticalScroller.highValue);
                yield return null;
                Check(viewport.scrollOffset.x > 0 || viewport.scrollOffset.y > 0, "도킹 끝 영역 접근 " + tab);
                viewport.scrollOffset = Vector2.zero;
            }
            window.Close(); window = LevelEditorWindow.OpenWorkspace(0, level, true); window.ShowUtility();
            yield return null;
            Menu("workspace-menu-level", "이름 변경…").Execute();
            window.rootVisualElement.Q<TextField>("level-file-name").value = "Other"; Click("confirm-level-name");
            Check(AssetDatabase.GetAssetPath(level) == folder + "/First.asset" && window.rootVisualElement.Q("level-name-panel").style.display == DisplayStyle.Flex, "중복 이름 거절 및 원본 유지");
            Click("cancel-level-name"); Click("inspector-tab-1"); yield return null;
            IntegerField moves = window.rootVisualElement.Query<IntegerField>().ToList().First(f => f.bindingPath == "moveCount");
            moves.Focus();
            typeof(TextInputBaseField<int>).GetProperty("text", BindingFlags.Instance | BindingFlags.Public).SetValue(moves, "17");
            using (KeyDownEvent key = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.S, modifiers = EventModifiers.Control }))
            { key.target = moves; moves.SendEvent(key); }
            for (int i = 0; i < 5; i++) yield return null;
            Check(level.MoveCount == 17 && !EditorUtility.IsDirty(level) && File.ReadAllText(folder + "/First.asset").Contains("moveCount: 17"), "입력 포커스 상태 Ctrl+S 확정 저장");
            foreach (int tab in new[] { 0, 1, 2, 4, 3 })
            {
                window.SelectWorkspaceTab(tab); window.position = new Rect(20, 20, window.minSize.x, window.minSize.y);
                for (int i = 0; i < 6; i++) yield return null;
                Rect area = window.rootVisualElement.worldBound;
                Check(window.rootVisualElement.Q("workspace-menubar").worldBound.xMax <= area.xMax + 1, "최소 창 메뉴 접근 " + tab);
                Check(window.rootVisualElement.Q("workspace-tabs").worldBound.yMax <= area.yMax, "최소 창 탭 접근 " + tab);
            }
            window.SelectWorkspaceTab(4);
            store = new MultiLevelTestStore(Path.GetFullPath(Evidence + "/reload-" + Guid.NewGuid().ToString("N")));
            MultiLevelTestPanel panel = (MultiLevelTestPanel)typeof(LevelEditorWindow).GetField("multiPanel", Hidden).GetValue(window);
            typeof(MultiLevelTestPanel).GetField("store", Hidden).SetValue(panel, store);
            MultiLevelTestSession paused = new MultiLevelTestSession(new[] { level }, MultiLevelTestMode.Repeat, 1, store); paused.SetPaused(true);
            typeof(MultiLevelTestPanel).GetField("session", Hidden).SetValue(panel, paused);
            panel.SetVisible(true); Check(paused.Paused, "재로드 전 실제 세션 일시정지·게임 0판");
            SessionState.SetString(ReloadKey + ".root", store.Root);
            SessionState.SetInt(ReloadKey + ".pid", System.Diagnostics.Process.GetCurrentProcess().Id);
            File.WriteAllLines(Evidence + "/presentation-results.txt", results);
            SessionState.SetBool(ReloadKey, true); reloadMarker = 1;
            EditorApplication.update -= Tick; EditorUtility.RequestScriptReload();
            yield return null;
        }

        [InitializeOnLoadMethod]
        private static void ResumeReload()
        {
            if (!SessionState.GetBool(ReloadKey, false)) return;
            File.AppendAllText(Evidence + "/reload-trace.txt", "InitializeOnLoad reached\n");
            presentation = true; results.AddRange(File.ReadAllLines(Evidence + "/presentation-results.txt"));
            steps = AfterReload(); deadline = EditorApplication.timeSinceStartup + 60; EditorApplication.update += Tick;
        }
        private static IEnumerator AfterReload()
        {
            Check(reloadMarker == 0 && SessionState.GetInt(ReloadKey + ".pid", 0) == System.Diagnostics.Process.GetCurrentProcess().Id, "실제 동일 프로세스 도메인 재로드");
            store = new MultiLevelTestStore(SessionState.GetString(ReloadKey + ".root", ""));
            MultiLevelTestRecord saved = store.Load();
            Check(saved.entries.All(e => e.status == MultiLevelTestStatus.Interrupted), "재로드 중 이전 세션 중단 기록");
            window = LevelEditorWindow.OpenWorkspace(4);
            MultiLevelTestPanel panel = (MultiLevelTestPanel)typeof(LevelEditorWindow).GetField("multiPanel", Hidden).GetValue(window);
            Check(!panel.CanContinue, "재로드 후 자동 실행 없음");
            typeof(MultiLevelTestPanel).GetField("store", Hidden).SetValue(panel, store); panel.ClearStoredResults(); panel.ReloadHistory();
            for (int i = 0; i < 20; i++) yield return null;
            Check(window.rootVisualElement.Q<ListView>("multi-results").itemsSource?.Count == 1, "재로드 후 중단 기록 조회");
            window.Close(); window = LevelEditorWindow.OpenWorkspace(4);
            yield return null;
            panel = (MultiLevelTestPanel)typeof(LevelEditorWindow).GetField("multiPanel", Hidden).GetValue(window);
            Check(!panel.CanContinue, "창 재열기 자동 실행 없음");
        }
        public static void Start()
        { steps = Run(); deadline = EditorApplication.timeSinceStartup + 180; EditorApplication.update += Tick; }

        /// <returns>실제 화면이 갱신될 때까지 프레임을 넘기는 검증 순서.</returns>
        private static IEnumerator Run()
        {
            window = LevelEditorWindow.OpenWorkspace(0, null, true); window.ShowUtility();
            yield return null;
            foreach (string name in new[] { "저장    Ctrl+S", "복제…", "이름 변경…", "레벨 데이터 검사" })
                Check(Menu("workspace-menu-level", name).status == DropdownMenuAction.Status.Disabled, "미선택 명령 잠금 " + name);
            Menu("workspace-menu-level", "새 레벨…").Execute();
            window.rootVisualElement.Q<TextField>("level-file-name").value = "__Stage33_" + Guid.NewGuid().ToString("N");
            Click("confirm-level-name"); yield return null;
            LevelDefinition first = window.CurrentLevel; owned.Add(first);
            Check(first != null && File.Exists(AssetDatabase.GetAssetPath(first)), "메뉴에서 임시 레벨 생성");
            string run = File.ReadAllText("Logs/Stage31Workflow/full-run-path.txt").Trim() + "/records";
            string id = File.ReadAllText(run + "/latest.txt").Trim();
            BotMoveBalanceRecord fixture = JsonUtility.FromJson<BotMoveBalanceRecord>(File.ReadAllText(run + "/" + id + "/balance.json"));
            JsonUtility.FromJsonOverwrite(fixture.definitionJson, first);
            JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", first); EditorUtility.SetDirty(first);
            window.CreateGUI(); yield return null;
            Menu("workspace-menu-level", "저장    Ctrl+S").Execute(); yield return null;
            Check(!EditorUtility.IsDirty(first) && File.ReadAllText(AssetDatabase.GetAssetPath(first)).Contains("moveCount: 1"), "메뉴 저장과 디스크 일치");
            Click("inspector-tab-1"); yield return null;
            IntegerField moves = window.rootVisualElement.Query<IntegerField>().ToList().First(f => f.bindingPath == "moveCount");
            moves.value = 2; for (int i = 0; i < 5; i++) yield return null;
            Check(first.MoveCount == 2 && EditorUtility.IsDirty(first), "인스펙터 수정과 dirty");
            using (KeyDownEvent key = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.S, modifiers = EventModifiers.Control }))
            { key.target = window.rootVisualElement; window.rootVisualElement.SendEvent(key); }
            yield return null;
            Check(!EditorUtility.IsDirty(first) && File.ReadAllText(AssetDatabase.GetAssetPath(first)).Contains("moveCount: 2"), "Ctrl+S와 디스크 일치");
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(first));
            Menu("workspace-menu-level", "이름 변경…").Execute(); window.rootVisualElement.Q<TextField>("level-file-name").value = "bad/name"; Click("confirm-level-name");
            Check(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(first)) == guid && window.rootVisualElement.Q("level-name-panel").style.display == DisplayStyle.Flex, "잘못된 이름 거절");
            Click("cancel-level-name");
            Menu("workspace-menu-level", "이름 변경…").Execute(); window.rootVisualElement.Q<TextField>("level-file-name").value = "__Stage33Renamed_" + Guid.NewGuid().ToString("N"); Click("confirm-level-name");
            Check(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(first)) == guid, "이름 변경 GUID 보존");
            Menu("workspace-menu-level", "복제…").Execute();
            string copyPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(first)).Replace('\\', '/') + "/__Stage33Copy_" + Guid.NewGuid().ToString("N") + ".asset";
            window.DuplicateCurrentTo(copyPath); LevelDefinition second = window.CurrentLevel; owned.Add(second);
            Check(second != first && File.Exists(copyPath), "메뉴 복제의 기존 처리 경로");
            Menu("workspace-menu-level", "복제…").Execute(); Click("cancel-duplicate");
            Menu("workspace-menu-level", "등록한 맵 모양…").Execute(); yield return null; Click("close-shape-recommendations");
            Check(window.CurrentLevel == second, "복제 취소와 맵 목록 닫기는 선택 유지");
            Menu("workspace-menu-test", "여러 레벨 시험").Execute(); yield return null;
            MultiLevelTestPanel panel = (MultiLevelTestPanel)typeof(LevelEditorWindow).GetField("multiPanel", Hidden).GetValue(window);
            store = new MultiLevelTestStore(Path.GetFullPath(Evidence + "/run-" + Guid.NewGuid().ToString("N")));
            typeof(MultiLevelTestPanel).GetField("store", Hidden).SetValue(panel, store);
            // 창을 연 뒤 생성한 레벨은 제품의 명시적 목록 갱신 명령으로 다시 읽는다.
            Menu("multi-selection-menu", "레벨 목록 새로고침").Execute();
            ListView choices = window.rootVisualElement.Q<ListView>("multi-level-choices");
            choices.SetSelection(Enumerable.Range(0, choices.itemsSource.Count).Where(i => owned.Contains((LevelDefinition)choices.itemsSource[i])));
            Check(choices.selectedIndices.Count() == 2, "갱신 후 임시 두 레벨만 선택");
            window.rootVisualElement.Q<PopupField<string>>("multi-mode").index = 0;
            window.rootVisualElement.Q<IntegerField>("multi-samples").value = 1;
            Click("multi-start");
            Menu("workspace-menu-test", "플레이 테스트").Execute();
            MultiLevelTestSession session = (MultiLevelTestSession)typeof(MultiLevelTestPanel).GetField("session", Hidden).GetValue(panel);
            Check(session != null && session.Paused, "메뉴 이동 즉시 시험 일시정지");
            Check(!window.ActiveSimulationPanel.rootVisualElement.enabledInHierarchy, "다른 시험 실행 잠금");
            Menu("workspace-menu-test", "여러 레벨 시험").Execute();
            Check(session.Paused, "메뉴 복귀 자동 재개 없음"); Click("multi-pause");
            while (session.CanContinue) yield return null;
            Check(session.Record.entries.Count == 2 && session.Record.entries.All(e => e.status == MultiLevelTestStatus.Completed), "두 레벨 실제 소규모 시험 완료");
            int games = 0;
            for (int i = 0; i < 2; i++)
            {
                MultiLevelTestEntry entry = session.Record.entries[i];
                BotAnalysisReader reader = new BotAnalysisReader(Path.Combine(store.LevelRoot(session.Record, i), entry.resultId));
                while (!reader.IsDone) { reader.Advance(); yield return null; }
                Check(reader.Error == null && reader.Games.Count == 2, "레벨별 두 전략 원시 결과 " + i); games += reader.Games.Count;
                window.rootVisualElement.Q<ListView>("multi-results").SetSelection(i);
                for (int j = 0; j < 8; j++) yield return null;
                while (window.rootVisualElement.Q<Label>("multi-detail").text.Contains("확인 중")) yield return null;
                Check(window.rootVisualElement.Q<Label>("multi-detail").text.Contains("판단 보류"), "소표본 판단 보류 " + i);
            }
            results.Add("DATA 실제 신규 플레이 " + games + "판");
            Click("multi-open-repeat"); Check(window.WorkspaceTab == 3, "개별 결과에서 통계 화면 연결");
            Menu("workspace-menu-records", "지난 여러 레벨 시험").Execute(); Check(window.WorkspaceTab == 4, "지난 시험 메뉴 이동");
            Menu("workspace-menu-records", "기록 관리").Execute(); Check(window.WorkspaceTab == 3, "기록 관리 메뉴 이동");
            File.WriteAllText(Evidence + "/workflow-run.txt", store.Root);
        }
        private static DropdownMenuAction Menu(string menuName, string name)
        {
            ToolbarMenu menu = window.rootVisualElement.Q<ToolbarMenu>(menuName);
            using MouseUpEvent evt = MouseUpEvent.GetPooled(new Event { type = EventType.MouseUp }); menu.menu.PrepareForDisplay(evt);
            return menu.menu.MenuItems().OfType<DropdownMenuAction>().First(a => a.name == name);
        }
        private static void Click(string name)
        {
            Button button = window.rootVisualElement.Q<Button>(name); Check(button != null && button.enabledInHierarchy, "버튼 접근 " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }
        private static void Check(bool pass, string message)
        { results.Add((pass ? "PASS " : "FAIL ") + message); if (!pass) throw new InvalidOperationException(message); }
        private static void Tick()
        {
            try { if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("33단계 검수 제한 시간"); if (steps.MoveNext()) return; }
            catch (Exception error) { results.Add("FAIL " + error); }
            EditorApplication.update -= Tick; if (window != null) window.Close();
            foreach (LevelDefinition level in owned.Where(l => l != null))
            {
                string path = AssetDatabase.GetAssetPath(level);
                if (Path.GetFileName(path).StartsWith("__Stage33", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(path);
            }
            if (presentation)
            {
                string folder = SessionState.GetString(ReloadKey + ".folder", "");
                if (folder.StartsWith("Assets/__Stage33Presentation_", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(folder);
                SessionState.EraseBool(ReloadKey); SessionState.EraseString(ReloadKey + ".folder");
            }
            File.WriteAllLines(Evidence + (menuCapture ? "/final-ui-results.txt" : presentation ? "/presentation-results.txt" : "/workflow-results.txt"), results);
            EditorApplication.Exit(results.Any(r => r.StartsWith("FAIL")) ? 1 : 0);
        }
    }
}
