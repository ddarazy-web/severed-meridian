using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Board;
using Simulation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public static partial class ManualPlayIntegrationVerification
    {
        private const string Evidence = "Logs/ManualPlayIntegrationVerification";
        private static readonly List<string> Results = new List<string>();
        private static LevelInitialStatePanel window;
        private static LevelEditorWindow editor;
        private static LevelDefinition level;
        private static string folder;
        private static IEnumerator sequence;
        private static double nextTick;
        [Serializable] private sealed class Saved { public string path, folder, json, guid, replay; public int process, tab, seed; }
        private static object Invoke(Type type, string method, params object[] args) => type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        private static string Snapshot(object value) => (string)Invoke(typeof(LevelInitialStateVerification), "Snapshot", value);
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); Results.Add("PASS " + message); }
        private static void Click(VisualElement root, string name)
        {
            Button button = root.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("버튼 사용 불가 " + name);
            using NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled(); evt.target = button; button.SendEvent(evt);
        }
        private static void Finish(BoardActionExecutor executor)
        {
            int remaining = 1000;
            while (executor.HasPendingCascade && remaining-- > 0) executor.AdvanceCascade();
            Check(!executor.HasPendingCascade, "공통 실행기 후속 처리 종료");
        }
        private static string State(BoardActionExecutor executor) => Snapshot(executor.State) + Snapshot(executor.TurnEffects) + Snapshot(executor.Outcome) +
            Snapshot(executor.CascadeHistory) + Snapshot(executor.ItemUses) + Snapshot(executor.BoosterPlacements) + Snapshot(executor.PendingBoosters);
        private static string Replay(LevelDefinition source)
        {
            StartingBoardSearch search = StartingBoardBuilder.Build(source, 12345);
            BoardActionExecutor executor = new BoardActionExecutor(search.State, new[] { StartBooster.Rocket });
            BoardCoordinate target = executor.State.Cells.First(c => c.Content == RuntimeContent.Normal).Coordinate;
            Check(executor.UseItem(BoardItem.Hammer, target).IsApplied, "재현 아이템 적용"); Finish(executor);
            return State(executor);
        }
        public static void Start()
        {
            Directory.CreateDirectory(Evidence); Results.Clear();
            if (File.Exists(Evidence + "/state.json")) throw new InvalidOperationException("이전 독립 재현 필요");
            folder = "Assets/__ManualPlayIntegrationVerification_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            level = (LevelDefinition)Invoke(typeof(ItemBoosterVerification), "PlayFixture");
            AssetDatabase.CreateAsset(level, folder + "/Manual.asset"); AssetDatabase.SaveAssetIfDirty(level);
            LevelEditorWindow.OpenLevel(level); editor = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().Single();
            sequence = UI(); EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + 0.3;
            try { if (!sequence.MoveNext()) Complete(null); }
            catch (Exception error) { Complete(error); }
        }
        private static void Complete(Exception error)
        {
            EditorApplication.update -= Tick;
            if (error != null) { Results.Add("FAIL " + error); Debug.LogException(error); AssetDatabase.DeleteAsset(folder); }
            File.WriteAllLines(Evidence + "/ui-results.txt", Results);
            if (window != null) window.Owner.Close(); if (editor != null) editor.Close();
            EditorApplication.Exit(error == null ? 0 : 1);
        }
        private static IEnumerator UI()
        {
            yield return null;
            string disk = File.ReadAllText(AssetDatabase.GetAssetPath(level));
            JsonUtility.FromJsonOverwrite("{\"levelNumber\":24}", level); EditorUtility.SetDirty(level);
            string json = JsonUtility.ToJson(level);
            Click(editor.rootVisualElement, "play-level"); yield return null;
            window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().Single().ActiveSimulationPanel; window.Owner.position = new Rect(10, 10, 1000, 780); window.Owner.Focus();

            typeof(SettlementVerification).GetField("window", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, window);
            Invoke(typeof(SettlementVerification), "RaiseVerificationWindow");
            for (int i = 0; window.IsSearching && i < 500; i++) yield return null;
            Check(window.CurrentState?.LevelNumber == 24 && window.Execution == null, "미저장 편집 전달·시작 전 사본 구성");
            VisualElement root = window.rootVisualElement;
            root.Q<IntegerField>("initial-seed").value = 12345; Click(root, "manual-start");
            for (int i = 0; window.IsSearching && i < 500; i++) yield return null;
            Check(window.CurrentState != null, "지정 시드 재구성");
            root.Q<Toggle>("booster-Rocket").value = true; Click(root, "manual-start"); yield return null;
            Check(window.Execution != null && window.Execution.BoosterPlacements.Count == 1, "기본 시작·선택 부스터 배치");
            Check(root.Q<Foldout>("manual-diagnostics").value == false && root.Q<VisualElement>("initial-inspector").style.display == DisplayStyle.None, "기본 화면 상세 진단 접힘");
            BoardCoordinate target = window.Execution.State.Cells.First(c => c.Content == RuntimeContent.Normal).Coordinate;
            Click(root, "item-Hammer"); Click(root, $"initial-cell-{target.Row}-{target.Column}");
            Check(window.CascadeRunning && !root.Q<Button>("item-Hammer").enabledInHierarchy, "아이템 후 자동 연쇄 예약·중복 입력 차단");
            BoardActionExecutor tabSession = window.Execution;
            string pausedState = State(tabSession);
            Click(editor.rootVisualElement, "workspace-tab-0"); yield return null;
            Check(editor.WorkspaceTab == 0 && !window.CascadeRunning && State(tabSession) == pausedState, "편집 탭 이동 시 실행 상태·난수 보존 및 연쇄 일시정지");
            Check(editor.rootVisualElement.Q<LevelBoardView>() != null && editor.CurrentLevel == level, "통합 편집 화면·공통 레벨 유지");
            Click(editor.rootVisualElement, "workspace-tab-2"); yield return null;
            LevelInitialStatePanel diagnostic = editor.ActiveSimulationPanel;
            Click(diagnostic.rootVisualElement, "initial-build"); yield return null;
            Check(diagnostic != window && diagnostic.CurrentState != null && State(tabSession) == pausedState, "진단 후보 구성은 보관한 플레이 세션과 독립");
            LevelRuntimeState diagnosticState = diagnostic.CurrentState;
            Click(editor.rootVisualElement, "workspace-tab-1");
            Check(editor.ActiveSimulationPanel == window && window.Execution == tabSession && window.CascadeRunning, "플레이 탭 복귀 시 동일 세션·연쇄 재개");
            for (int i = 0; window.CascadeRunning && i < 1000; i++) yield return null;
            string replay = Replay(level);
            Check(State(window.Execution) == replay, "실제 기본 자동 진행과 공통 단계 실행 상태·난수·기록 일치");
            Click(root, "item-Hammer"); Click(editor.rootVisualElement, "workspace-tab-2");
            Check(diagnostic.CurrentState == diagnosticState, "진단 탭 왕복 시 기존 후보 보존");
            Click(editor.rootVisualElement, "workspace-tab-1");
            Check(root.Q<Button>("item-cancel").style.display == DisplayStyle.None && window.Execution == tabSession, "탭 이동 시 아이템 선택만 해제·실행 세션 보존");
            Check(EditorApplication.ExecuteMenuItem("Match/플레이 테스트") && Resources.FindObjectsOfTypeAll<LevelEditorWindow>().Length == 1 && editor.ActiveSimulationPanel.Execution == tabSession,
                "기존 메뉴도 같은 통합 창·플레이 세션 재사용");
            Check(root.Q<Label>("manual-summary").text.Contains("남은 이동 20"), "기본 화면 이동·미션 표시 유지");
            yield return null; Capture("manual-wide.png");
            window.Owner.position = new Rect(10, 10, 680, 480); yield return null; Capture("manual-narrow.png");
            Check(root.Q<Button>("manual-return").worldBound.xMax <= window.Owner.position.width, "680폭 복귀 버튼 접근");
            Check(root.Q<ScrollView>("initial-board-scroll").worldBound.height >= 100 && root.Q<Button>("item-Shuffle").worldBound.yMax < 480, "680×480 보드·아이템 접근 공간");
            // 탭별 최소 창 크기에서는 보드 전체가 보여 스크롤이 필요 없을 수 있다.
            // 검증 소유 뷰의 높이만 제한하여 실제 넘침을 만든 뒤, 탭 왕복의 위치 보존을 검사한다.
            root.Q<ScrollView>("initial-board-scroll").style.maxHeight = 200;
            yield return null;
            root.Q<ScrollView>("initial-board-scroll").scrollOffset = new Vector2(0, 60); yield return null; Capture("manual-scrolled.png");
            Vector2 scrollBefore = root.Q<ScrollView>("initial-board-scroll").scrollOffset;
            Click(editor.rootVisualElement, "workspace-tab-0"); yield return null;
            ScrollView editScroll = editor.rootVisualElement.Q<ScrollView>("board-scroll");
            editScroll.scrollOffset = new Vector2(0, 50); yield return null;
            Vector2 editScrollBefore = editScroll.scrollOffset;
            typeof(LevelEditorWindow).GetMethod("SelectCell", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, new object[] { new BoardCoordinate(3, 3) });
            object selectedBefore = typeof(LevelEditorWindow).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
            Click(editor.rootVisualElement, "workspace-tab-1"); yield return null;
            Check(scrollBefore.y > 0 && (root.Q<ScrollView>("initial-board-scroll").scrollOffset - scrollBefore).sqrMagnitude < 1,
                $"플레이 탭 복귀 시 보드 스크롤 보존 (전 {scrollBefore}, 후 {root.Q<ScrollView>("initial-board-scroll").scrollOffset}, 창 {window.Owner.position.size})");
            Click(editor.rootVisualElement, "workspace-tab-0"); yield return null;
            Check((editScroll.scrollOffset - editScrollBefore).sqrMagnitude < 1 && Equals(selectedBefore, typeof(LevelEditorWindow).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor)),
                "편집 탭 복귀 시 스크롤·선택 칸 보존");
            Click(editor.rootVisualElement, "workspace-tab-1"); yield return null;
            root.Q<ScrollView>("initial-board-scroll").style.maxHeight = StyleKeyword.Null;
            Check(JsonUtility.ToJson(level) == json && EditorUtility.IsDirty(level) && File.ReadAllText(AssetDatabase.GetAssetPath(level)) == disk, "시험 후 원본 JSON·미저장 dirty·디스크 보존");
            Click(root, "manual-restart");
            for (int i = 0; window.IsSearching && i < 500; i++) yield return null;
            Check(window.Execution == null && root.Q<IntegerField>("initial-seed").value == 12345 && !root.Q<Toggle>("booster-Rocket").value, "같은 시드 재시작·실행/부스터 선택 초기화");
            root.Q<Toggle>("booster-Rocket").value = true; Click(root, "manual-start");
            root.Q<Foldout>("manual-diagnostics").value = true;
            target = window.Execution.State.Cells.First(c => c.Content == RuntimeContent.Normal).Coordinate;
            Click(root, "item-Hammer"); Click(root, $"initial-cell-{target.Row}-{target.Column}");
            Check(!window.CascadeRunning && window.Execution.HasPendingCascade, "상세 진단에서는 수동 단계 대기");
            Click(root, "execution-settle"); root.Q<Foldout>("manual-diagnostics").value = false;
            for (int i = 0; window.CascadeRunning && i < 1000; i++) yield return null;
            Check(State(window.Execution) == replay, "상세 단계 뒤 기본 자동 진행 전환 결과 일치");
            Click(root, "item-Hammer"); target = window.Execution.State.Cells.First(c => c.Content == RuntimeContent.Normal).Coordinate;
            Click(root, $"initial-cell-{target.Row}-{target.Column}"); Click(root, "manual-new-seed");
            for (int i = 0; window.IsSearching && i < 500; i++) yield return null;
            Check(window.Execution == null && !window.CascadeRunning && root.Q<IntegerField>("initial-seed").value != 12345, "연쇄 중 새 시드 재시작·이전 예약 해제");
            Click(root, "manual-start");
            JsonUtility.FromJsonOverwrite("{\"levelNumber\":25}", level); EditorUtility.SetDirty(level);
            typeof(LevelInitialStatePanel).GetMethod("CheckInput", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
            Check(window.Execution == null && window.CurrentState == null && !window.CascadeRunning, "원본 변경은 이전 실행·후보·예약 무효화");
            Click(editor.rootVisualElement, "workspace-tab-2");
            Check(diagnostic.CurrentState == null && diagnostic.rootVisualElement.Q<Label>("initial-status").text.Contains("원본 내용"), "원본 변경 시 숨겨진 진단 탭도 후보 무효화·안내");
            Click(editor.rootVisualElement, "workspace-tab-1");
            JsonUtility.FromJsonOverwrite("{\"levelNumber\":0}", level); EditorUtility.SetDirty(level); Click(root, "manual-restart");
            for (int i = 0; window.IsSearching && i < 500; i++) yield return null;
            Check(window.CurrentState == null && window.Execution == null && window.LastSearch.Status == StartingBoardStatus.DefinitionError, "잘못된 정의 시작 차단·원본 자동 수정 없음");
            JsonUtility.FromJsonOverwrite(json, level);
            JsonUtility.FromJsonOverwrite("{\"schemaVersion\":99}", level); EditorUtility.SetDirty(level); Click(root, "manual-restart");
            for (int i = 0; window.IsSearching && i < 500; i++) yield return null;
            Check(window.CurrentState == null && window.Execution == null && window.LastSearch.Issues.Any(issue => issue.Code == LevelValidationCode.UnsupportedSchemaVersion), "미지원 저장 형식은 사본 플레이 시작 전 거절");
            JsonUtility.FromJsonOverwrite(json, level); EditorUtility.SetDirty(level); Click(root, "manual-restart");
            for (int i = 0; window.IsSearching && i < 500; i++) yield return null;
            Click(root, "manual-start");
            BoardActionExecutor swap = new BoardActionExecutor(window.CurrentState);
            Check(swap.Swap(new BoardCoordinate(9, 8), new BoardCoordinate(9, 9)).IsApplied, "비교용 일반·파워 교환"); Finish(swap);
            Click(root, "initial-cell-9-8"); Click(root, "initial-cell-9-9"); Click(root, "execution-swap");
            for (int i = 0; window.CascadeRunning && i < 1000; i++) yield return null;
            Check(State(window.Execution) == State(swap) && window.Execution.State.MovesRemaining == 19, "실제 교환·파워 발동·자동 연쇄·이동 차감 일치");
            Invoke(typeof(PowerEffectVerification), "Place", level, new BoardCoordinate(9, 8), InitialBlockKind.Bomb, RocketDirection.Horizontal, RabbitColor.Type1);
            EditorUtility.SetDirty(level); Click(root, "manual-restart");
            for (int i = 0; window.IsSearching && i < 500; i++) yield return null;
            Click(root, "manual-start");
            BoardActionExecutor combination = new BoardActionExecutor(window.CurrentState);
            Check(combination.Swap(new BoardCoordinate(9, 8), new BoardCoordinate(9, 9)).IsApplied, "비교용 폭탄·로켓 조합"); Finish(combination);
            Click(root, "initial-cell-9-8"); Click(root, "initial-cell-9-9"); Click(root, "execution-swap");
            for (int i = 0; window.CascadeRunning && i < 1000; i++) yield return null;
            Check(State(window.Execution) == State(combination), "실제 파워 조합·후속 연쇄 공통 실행 일치");
            JsonUtility.FromJsonOverwrite(json, level);
            JsonUtility.FromJsonOverwrite("{\"moveCount\":1}", level); EditorUtility.SetDirty(level); Click(root, "manual-restart");
            for (int i = 0; window.IsSearching && i < 500; i++) yield return null;
            Click(root, "manual-start"); Click(root, "initial-cell-9-9"); Click(root, "execution-activate");
            for (int i = 0; window.CascadeRunning && i < 1000; i++) yield return null;
            Check(window.Execution.Outcome?.Kind == BoardOutcomeKind.MovesExhausted && root.Q<Label>("manual-summary").text.Contains("이동 수 소진"), "실제 마지막 수·자동 연쇄·이동 소진 결과 표시");
            JsonUtility.FromJsonOverwrite("{\"missions\":[{\"kind\":0,\"color\":0,\"count\":1}]}", level); EditorUtility.SetDirty(level); Click(root, "manual-restart");
            for (int i = 0; window.IsSearching && i < 500; i++) yield return null;
            Click(root, "manual-start"); Click(root, "initial-cell-9-9"); Click(root, "execution-activate");
            for (int i = 0; window.CascadeRunning && i < 1000; i++) yield return null;
            Check(window.Execution.Outcome?.Kind == BoardOutcomeKind.Won && window.Execution.Phase == BoardActionPhase.Stopped && root.Q<Label>("manual-summary").text.Contains("성공"), "실제 마지막 수 성공 우선·라스트팡 완료·결과 유지");
            yield return null; Capture("manual-result.png");
            JsonUtility.FromJsonOverwrite(json, level); EditorUtility.SetDirty(level); Click(root, "manual-restart");
            for (int i = 0; window.IsSearching && i < 500; i++) yield return null;
            Click(root, "manual-start"); Click(root, "manual-return"); yield return null;
            editor = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().Single();
            Check(editor.WorkspaceTab == 0 && window.Execution != null && editor.CurrentLevel == level && JsonUtility.ToJson(level) == json && EditorUtility.IsDirty(level), "편집 복귀·선택 레벨·미저장 원본 보존");
            LevelInitialStateWindow.OpenManualLevel(level); window = Resources.FindObjectsOfTypeAll<LevelEditorWindow>().Single().ActiveSimulationPanel;
            for (int i = 0; window.IsSearching && i < 500; i++) yield return null;
            root = window.rootVisualElement;
            window.Owner.CreateGUI(); window = editor.ActiveSimulationPanel; yield return null;
            Check(window.Execution == null && window.CurrentState == null && !window.CascadeRunning &&
                window.rootVisualElement.Q<Label>("initial-status").text.Contains("다시 시작"), "창 UI 재생성 후 이전 세션 폐기·재시작 안내");
            window.rootVisualElement.Q<ObjectField>("initial-level").value = null;
            Check(window.Execution == null && window.CurrentState == null && !window.rootVisualElement.Q<Button>("manual-start").enabledInHierarchy, "레벨 선택 해제 후 이전 상태 폐기·시작 차단");
            LevelInitialStateWindow.Open(); window = editor.ActiveSimulationPanel; root = window.rootVisualElement;
            Check(root.Q<VisualElement>("manual-toolbar") == null && root.Q<VisualElement>("execution-toolbar") != null, "기존 Match 초기 보드 확인 진입점 보존");
            root.Q<ObjectField>("initial-level").value = level;
            root.Q<PopupField<string>>("initial-mode").value = "시작 조건 구성"; Click(root, "initial-build");
            for (int i = 0; window.IsSearching && i < 500; i++) yield return null;
            root.Q<Toggle>("execution-mode").value = true;
            Check(window.Execution != null && !window.CascadeRunning, "기존 진단 창 시작·단계 실행 모드 유지");
            window.Owner.Close(); yield return null;
            // 재현용 검증 에셋만 저장한다. 사용자의 에셋은 저장하지 않는다.
            AssetDatabase.SaveAssetIfDirty(level);
            File.WriteAllText(Evidence + "/state.json", JsonUtility.ToJson(new Saved { path = AssetDatabase.GetAssetPath(level), folder = folder, json = json,
                guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level)), replay = replay, process = System.Diagnostics.Process.GetCurrentProcess().Id }, true));
        }
        private static void Capture(string name)
        {
            Rect rect = window.Owner.position; Texture2D texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
            texture.SetPixels(UnityEditorInternal.InternalEditorUtility.ReadScreenPixel(rect.position, (int)rect.width, (int)rect.height)); texture.Apply();
            File.WriteAllBytes(Evidence + "/" + name, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
        public static void Restart()
        {
            Results.Clear();
            try
            {
                Saved saved = JsonUtility.FromJson<Saved>(File.ReadAllText(Evidence + "/state.json"));
                Check(saved.process != System.Diagnostics.Process.GetCurrentProcess().Id, "독립 Unity 프로세스");
                LevelDefinition source = AssetDatabase.LoadAssetAtPath<LevelDefinition>(saved.path);
                Check(JsonUtility.ToJson(source) == saved.json && AssetDatabase.AssetPathToGUID(saved.path) == saved.guid, "정의·GUID 재로드");
                Check(Replay(source) == saved.replay, "수동 시험 전체 상태 독립 재현");
                if (!saved.folder.StartsWith("Assets/__ManualPlayIntegrationVerification_", StringComparison.Ordinal) || saved.folder.Contains("..")) throw new InvalidOperationException("임시 경로 오류");
                Check(AssetDatabase.DeleteAsset(saved.folder), "소유 임시 에셋 정리");
                File.Move(Evidence + "/state.json", Evidence + "/completed-" + DateTime.UtcNow.Ticks + ".json");
                File.WriteAllLines(Evidence + "/restart-results.txt", Results); EditorApplication.Exit(0);
            }
            catch (Exception error) { Results.Add("FAIL " + error); File.WriteAllLines(Evidence + "/restart-results.txt", Results); Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
