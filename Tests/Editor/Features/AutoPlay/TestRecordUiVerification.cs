using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AutoPlay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>실제 Editor 창에 제품 패널을 표시하되 삭제 저장소는 임시 경로만 주입한다.</summary>
    public sealed class TestRecordUiVerification : EditorWindow
    {
        private static readonly List<string> results = new List<string>();
        private static TestRecordUiVerification window;
        private static IEnumerator sequence;
        private static double deadline;
        private MultiLevelTestPanel multi;
        private LevelAnalysisPanel analysis;
        private TestRecordManagement management;
        private bool answer, busy;
        private string confirmation;
        private LevelDefinition level;
        private MultiLevelTestStore store;
        private string[] roots;

        private static LevelEditorWindow layoutWindow;
        private static int layoutFrames;
        /// <summary>실제 통합창의 기존 최소 크기에서 새 조작과 결과 영역이 잘리지 않는지 따로 확인한다.</summary>
        public static void StartLayout()
        {
            results.Clear(); layoutFrames = 0; layoutWindow = CreateInstance<LevelEditorWindow>();
            layoutWindow.titleContent = new GUIContent("Match"); layoutWindow.ShowUtility(); layoutWindow.CreateGUI(); layoutWindow.SelectWorkspaceTab(4);
            layoutWindow.position = new Rect(25, 25, 1000, 760);
            EditorApplication.update += LayoutTick;
        }
        private static void LayoutTick()
        {
            if (++layoutFrames < 12) return;
            try
            {
                if (layoutFrames == 12)
                {
                    foreach (string name in new[] { "multi-history", "multi-history-refresh", "multi-detail", "multi-read" })
                    {
                        VisualElement element = layoutWindow.rootVisualElement.Q(name);
                        Rect rootRect = layoutWindow.rootVisualElement.worldBound;
                        Check(element.worldBound.height > 0 && element.worldBound.yMax <= rootRect.yMax + 1 && element.worldBound.xMax <= rootRect.xMax + 1,
                            "통합창 최소 크기 전체 영역 " + name);
                    }
                    Check(layoutWindow.rootVisualElement.Q<Label>("multi-execution-title").text == "새 시험 실행", "통합창 조회·새 시험 제목 구분");
                    Capture("test-history-integrated.png"); layoutWindow.SelectWorkspaceTab(3);
                    layoutWindow.rootVisualElement.Q("analysis-panel").Q<Foldout>("record-management").value = true; return;
                }
                if (layoutFrames < 24) return;
                VisualElement controls = layoutWindow.rootVisualElement.Q("analysis-panel").Q("record-management");
                Check(controls.worldBound.yMax <= layoutWindow.rootVisualElement.worldBound.yMax + 1, "결과·이력 탭 기록 관리 영역");
                Capture("test-record-management-integrated.png");
            }
            catch (Exception error) { results.Add("FAIL " + error); }
            EditorApplication.update -= LayoutTick;
            File.WriteAllLines("Logs/Stage32Verification/layout-results.txt", results);
            layoutWindow.Close(); EditorApplication.Exit(results.Any(r => r.StartsWith("FAIL")) ? 1 : 0);
        }

        public static void Start()
        { results.Clear(); File.WriteAllText("Logs/Stage32Verification/ui-results.txt", "RUNNING " + DateTime.UtcNow.ToString("O")); sequence = Run(); deadline = EditorApplication.timeSinceStartup + 120; EditorApplication.update += Step; }

        private static IEnumerator Run()
        {
            // 통합창의 실제 연결은 읽기 전용으로 확인한다. 이 창의 삭제 버튼은 누르지 않는다.
            LevelEditorWindow integrated = LevelEditorWindow.OpenWorkspace(4);
            yield return null; yield return null;
            Check(integrated.rootVisualElement.Query<Button>("records-delete-all").ToList().Count == 2, "통합창 두 화면의 기록 삭제 진입점");
            Check(integrated.rootVisualElement.Q<PopupField<string>>("multi-history") != null, "통합창 지난 시험 선택 목록");
            integrated.Close();

            window = CreateInstance<TestRecordUiVerification>(); window.titleContent = new GUIContent("Match 기록 관리 검사");
            window.minSize = new Vector2(1000, 760); window.ShowUtility(); window.position = new Rect(25, 25, 1000, 760);
            string root = Path.GetFullPath("Logs/Stage32Verification/ui-owned-" + Guid.NewGuid().ToString("N"));
            window.roots = new[] { root + "/repeat", root + "/balance", root + "/multi" };
            window.store = new MultiLevelTestStore(window.roots[2]);
            string originalRoot = File.ReadAllText("Logs/Stage31Workflow/full-run-path.txt").Trim() + "/records";
            string originalId = File.ReadAllText(originalRoot + "/latest.txt").Trim();
            string rawRoot = originalRoot + "/" + originalId;
            BotMoveBalanceRecord original = JsonUtility.FromJson<BotMoveBalanceRecord>(File.ReadAllText(rawRoot + "/balance.json"));
            window.level = CreateInstance<LevelDefinition>(); JsonUtility.FromJsonOverwrite(original.definitionJson, window.level);
            window.level.name = "임시 읽기 전용 레벨";
            string originalLevel = JsonUtility.ToJson(window.level);
            string[] ids = { Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N") };
            string repeatFolder = null;
            for (int index = 0; index < 2; index++)
            {
                MultiLevelTestRecord record = new MultiLevelTestRecord { id = ids[index], startedUtc = "2026-09-29T0" + index + ":00:00Z", mode = MultiLevelTestMode.Balance };
                BotMoveBalanceRecord value = JsonUtility.FromJson<BotMoveBalanceRecord>(JsonUtility.ToJson(original));
                value.id = Guid.NewGuid().ToString("N"); value.trials = value.trials.Take(index + 1).ToList(); value.status = BotBatchStatus.Interrupted;
                record.entries.Add(new MultiLevelTestEntry { name = index == 0 ? "이전 추천 시험" : "최근 추천 시험", resultId = value.id, status = MultiLevelTestStatus.Interrupted });
                string destination = window.store.LevelRoot(record, 0); new BotMoveBalanceStore(destination).Save(value);
                foreach (BotMoveTrial trial in value.trials)
                {
                    string target = Path.Combine(destination, value.id, "trials", trial.batchId); Directory.CreateDirectory(target);
                    foreach (string path in Directory.GetFiles(Path.Combine(rawRoot, "trials", trial.batchId))) File.Copy(path, Path.Combine(target, Path.GetFileName(path)));
                    repeatFolder ??= target;
                }
                window.store.Save(record);
            }
            string badId = Guid.NewGuid().ToString("N"); Directory.CreateDirectory(window.roots[2] + "/" + badId);
            File.WriteAllText(window.roots[2] + "/" + badId + "/queue.json", "{}");
            window.Build();
            while (!window.multi.Root.Q<PopupField<string>>("multi-history").enabledInHierarchy) yield return null;
            PopupField<string> history = window.multi.Root.Q<PopupField<string>>("multi-history");
            Check(history.choices.Count == 3 && history.choices[0].Contains(ids[1].Substring(0, 8)), "최근순 목록과 손상 항목 분리");
            ListView resultsView = window.multi.Root.Q<ListView>("multi-results");
            Label detail = window.multi.Root.Q<Label>("multi-detail");
            history.index = 1; resultsView.SetSelection(0);
            while (detail.text.Contains("확인 중")) yield return null;
            Check(detail.text.Contains("이전 추천 시험") && detail.text.Contains("어려움 : 1회"), "이전 실행의 검증된 추천 표시");
            history.index = 0; resultsView.SetSelection(0);
            while (detail.text.Contains("확인 중")) yield return null;
            Check(detail.text.Contains("최근 추천 시험") && detail.text.Contains("쉬움 : 2회"), "다른 실행 선택 시 결과 교체");
            Check(File.ReadAllText(window.roots[2] + "/latest.txt") == ids[1], "화면 이력 선택도 최근 포인터 보존");
            history.index = 2;
            Check(detail.text.Contains("기록 확인 실패") && !window.multi.Root.Q<Button>("multi-open-repeat").enabledInHierarchy, "손상 이력 오류와 상세 버튼 해제");
            history.index = 0; resultsView.SetSelection(0);
            while (detail.text.Contains("확인 중")) yield return null;
            for (int i = 0; i < 4; i++) yield return null;
            foreach (string name in new[] { "multi-history", "multi-history-refresh", "records-delete-all", "multi-start", "multi-results", "multi-detail" })
            {
                VisualElement element = window.multi.Root.Q(name);
                Check(element.worldBound.width > 0 && element.worldBound.height > 0 && element.worldBound.xMax <= window.rootVisualElement.worldBound.xMax + 1,
                    "최소 창 표시 " + name);
            }
            Capture("test-history.png");

            MultiLevelTestSession paused = new MultiLevelTestSession(new[] { window.level }, MultiLevelTestMode.Repeat, 1, window.store);
            paused.SetPaused(true);
            typeof(MultiLevelTestPanel).GetField("session", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window.multi, paused);
            // 직접 주입은 Start의 Refresh를 지나지 않는다. 실제 탭 표시 경로로 동일한 갱신을 수행한다.
            window.multi.SetVisible(true);
            window.management.Tick(); yield return null;
            Check(!window.multi.Root.Q<Button>("records-delete-all").enabledInHierarchy, "실제 일시정지 세션 삭제 잠금");
            window.management.Request(); Check(!window.management.IsBusy, "일시정지 중 직접 삭제 요청도 거절");
            Check(!history.enabledInHierarchy, "일시정지 중 지난 실행 전환 잠금");
            paused.Stop();
            window.multi.ClearStoredResults(); window.multi.ReloadHistory();
            while (!history.enabledInHierarchy) yield return null;

            window.ShowPanel(false); window.analysis.OpenRecord(repeatFolder);
            while (window.analysis.Analysis?.IsDone != true) yield return null;
            Check(window.analysis.Analysis.Error == null, "삭제 전 원시 기록 검증 완료");
            window.analysis.BeginExport(root + "/separate-export");
            window.management.Tick(); Check(!window.analysis.Root.Q<Button>("records-delete-all").enabledInHierarchy, "실제 내보내기 중 삭제 잠금");
            while (window.analysis.IsExporting) yield return null;
            // 내보내기는 UI 스케줄러, 공유 버튼은 Editor 갱신에서 처리하므로 다음 표시 갱신을 기다린다.
            while (!window.analysis.Root.Q<Button>("records-delete-all").enabledInHierarchy) yield return null;
            int exportCount = Directory.GetFiles(root + "/separate-export").Length;
            typeof(LevelAnalysisPanel).GetField("reference", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window.analysis, window.analysis.Analysis);
            window.answer = false; Click(window.analysis.Root, "records-delete-all");
            while (window.management.IsBusy) yield return null;
            Check(window.analysis.Analysis != null && Directory.Exists(window.roots[2] + "/" + ids[0]), "실제 버튼 취소 후 선택과 파일 보존");
            Check(window.confirmation.Contains("반복 시험") && window.confirmation.Contains("이동 횟수 추천") && window.confirmation.Contains("여러 레벨 시험 4개"), "확인 창 세 종류별 실행 수");
            window.answer = true; Click(window.analysis.Root, "records-delete-all");
            yield return null;
            if (window.management.IsBusy)
            {
                Check(!window.multi.Root.enabledSelf && !window.analysis.Root.enabledSelf, "삭제 중 화면 입력 잠금");
                window.ShowPanel(true); window.ShowPanel(false);
                Check(typeof(LevelAnalysisPanel).GetField("scan", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window.analysis) == null,
                    "삭제 중 탭 재진입으로 기록 열거를 재시작하지 않음");
            }
            while (window.management.IsBusy) yield return null;
            Check(window.analysis.Analysis == null && typeof(LevelAnalysisPanel).GetField("reference", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window.analysis) == null, "삭제 후 분석·비교 사본 해제");
            Check(!window.analysis.Root.Q<Label>("records-management-status").text.Contains("보존/오류"), "아직 없는 저장소도 불필요한 오류 없이 정리");
            Check(Directory.GetFiles(root + "/separate-export").Length == exportCount && JsonUtility.ToJson(window.level) == originalLevel, "별도 보관본과 레벨 원본 보존");
            window.ShowPanel(true);
            while (!history.enabledInHierarchy) yield return null;
            Check(history.choices.Single() == "저장된 시험 없음" && resultsView.itemsSource == null, "삭제 후 지난 시험과 개별 결과 비움");
            Check(!window.multi.Root.Q<Button>("records-delete-all").enabledInHierarchy, "빈 목록 삭제 버튼 비활성화");
            for (int i = 0; i < 4; i++) yield return null;
            Capture("test-records-deleted.png");
            window.multi.Dispose(); window.multi.Root.RemoveFromHierarchy();
            window.multi = new MultiLevelTestPanel(() => false, _ => {}, window.store); window.rootVisualElement.Add(window.multi.Root); window.multi.SetVisible(true);
            yield return null;
            Check(window.multi.Root.Q<ListView>("multi-results").itemsSource == null, "패널 재생성 후 삭제 기록 복원 없음");
        }

        private void Build()
        {
            rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Features/LevelEditor/Editor/Styles/LevelEditor.uss"));
            VisualElement tabs = new VisualElement(); tabs.style.flexDirection = FlexDirection.Row; rootVisualElement.Add(tabs);
            tabs.Add(new Button(() => ShowPanel(true)) { text = "여러 레벨 시험" }); tabs.Add(new Button(() => ShowPanel(false)) { text = "결과·이력" });
            analysis = new LevelAnalysisPanel(() => level, () => busy || multi?.CanContinue == true);
            multi = new MultiLevelTestPanel(() => busy || analysis.IsExporting, path => { ShowPanel(false); analysis.OpenRecord(path); }, store);
            management = new TestRecordManagement(() => multi.CanContinue || analysis.IsExporting,
                () => { multi.ClearStoredResults(); analysis.ClearStoredResults(); },
                () => { multi.ReloadHistory(); analysis.ReloadHistory(); },
                value => { busy = value; multi.Root.SetEnabled(!value); analysis.Root.SetEnabled(!value); }, roots,
                text => { confirmation = text; return answer; });
            management.AddTo(multi.Root); management.AddTo(analysis.Root);
            multi.Root.Q<Foldout>("record-management").value = true;
            analysis.Root.Q<Foldout>("record-management").value = true;
            rootVisualElement.Add(multi.Root); rootVisualElement.Add(analysis.Root); ShowPanel(true);
        }
        private void ShowPanel(bool showMulti) { multi.SetVisible(showMulti); analysis.SetVisible(!showMulti); }
        private static void Click(VisualElement root, string name)
        {
            Button button = root.Q<Button>(name); Check(button.enabledInHierarchy, "버튼 활성 " + name);
            using (NavigationSubmitEvent evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
        private static void Capture(string name)
        {
            typeof(BotAnalysisVerification).GetMethod("CaptureAnalysis", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { name });
            File.Copy("Logs/BotAnalysisVerification/" + name, "Logs/Stage32Verification/" + name, true);
        }
        private static void Check(bool valid, string text)
        { results.Add((valid ? "PASS " : "FAIL ") + text); File.WriteAllLines("Logs/Stage32Verification/ui-results.txt", results); if (!valid) throw new InvalidOperationException(text); }
        private static void Step()
        {
            try { if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("UI 검사 시간 초과"); if (sequence.MoveNext()) return; }
            catch (Exception error) { results.Add("FAIL " + error); }
            EditorApplication.update -= Step;
            File.WriteAllLines("Logs/Stage32Verification/ui-results.txt", results);
            if (window != null) { window.management?.Dispose(); window.multi?.Dispose(); window.analysis?.Dispose(); if (window.level != null) DestroyImmediate(window.level); window.Close(); }
            File.WriteAllLines("Logs/Stage32Verification/ui-results.txt", results);
            EditorApplication.Exit(results.Any(r => r.StartsWith("FAIL")) ? 1 : 0);
        }
    }
}
