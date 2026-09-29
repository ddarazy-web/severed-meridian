using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AutoPlay;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>레벨 다중 선택, 순차 실행 제어, 선택한 레벨의 검증된 결과를 표시한다.</summary>
    internal sealed partial class MultiLevelTestPanel : IDisposable
    {
        internal VisualElement Root { get; } = new VisualElement { name = "multi-level-panel" };
        internal bool CanContinue => session?.CanContinue == true;
        private readonly Func<bool> otherBusy;
        private readonly Action<string> openRepeat;
        private readonly MultiLevelTestStore store;
        private readonly List<LevelDefinition> levels = new List<LevelDefinition>();
        private readonly ListView choices, results;
        private readonly PopupField<string> mode;
        private readonly IntegerField samples;
        private readonly Button start, pause, stop, read, open;
        private readonly VisualElement selectionTools;
        private readonly Label progress, detail, estimate;
        private MultiLevelTestSession session;
        private MultiLevelTestRecord record;
        private BotAnalysisReader repeatReader;
        private BotMoveBalanceReader balanceReader;
        private bool visible;
        private double nextDisplay;
        private string selectedFolder;

        /// <param name="otherBusy">다른 시험이나 재생이 보드를 사용 중인지 확인한다.</param>
        /// <param name="openRepeat">기존 결과·이력 화면에 반복 시험의 상세 사례를 연다.</param>
        /// <param name="store">검사 시에도 사용자 기록과 분리할 수 있는 저장소.</param>
        internal MultiLevelTestPanel(Func<bool> otherBusy, Action<string> openRepeat, MultiLevelTestStore store = null)
        {
            this.otherBusy = otherBusy; this.openRepeat = openRepeat; this.store = store ?? new MultiLevelTestStore();
            Root.style.flexGrow = 1; Root.style.minHeight = 0;
            Label guide = new Label("저장된 레벨을 Ctrl/Shift로 여러 개 선택하세요. 시작할 때 내용을 복사하며, 한 레벨씩 시험합니다.");
            guide.style.whiteSpace = WhiteSpace.Normal; Root.Add(guide);
            LevelEditorHelp.Link(guide, "반복 시험은 현재 이동 횟수의 난이도, 추천 시험은 네 난이도별 이동 횟수를 확인합니다.", "difficulty.html#multi-level");
            Label executionTitle = new Label("새 시험 실행") { name = "multi-execution-title" };
            executionTitle.style.unityFontStyleAndWeight = FontStyle.Bold; executionTitle.style.marginTop = 8; Root.Add(executionTitle);
            selectionTools = new VisualElement(); selectionTools.style.flexDirection = FlexDirection.Row; selectionTools.style.flexWrap = Wrap.Wrap; Root.Add(selectionTools);
            ToolbarMenu selectionMenu = new ToolbarMenu { text = "선택 도구", name = "multi-selection-menu" };
            selectionMenu.menu.AppendAction("전체 선택", _ => choices.SetSelection(Enumerable.Range(0, levels.Count)));
            selectionMenu.menu.AppendAction("선택 해제", _ => choices.ClearSelection());
            selectionMenu.menu.AppendAction("Project 선택 가져오기", _ => choices.SetSelection(Enumerable.Range(0, levels.Count).Where(i => Selection.objects.Contains(levels[i]))));
            selectionMenu.menu.AppendSeparator();
            selectionMenu.menu.AppendAction("레벨 목록 새로고침", _ => RefreshLevels());
            selectionTools.Add(selectionMenu);
            VisualElement options = new VisualElement(); options.style.flexDirection = FlexDirection.Row; Root.Add(options);
            mode = new PopupField<string>("시험 방식", new List<string> { "현재 횟수 반복 시험", "난이도별 이동 횟수 추천" }, 0) { name = "multi-mode" };
            samples = new IntegerField("전략별 판 수") { value = 100, name = "multi-samples", tooltip = "반복 시험에만 적용합니다. 난이도 판단에는 전략별 정상 100판 이상이 필요합니다." };
            mode.style.flexGrow = 1; samples.style.flexGrow = 1; options.Add(mode); options.Add(samples);
            mode.labelElement.style.minWidth = 65; mode.labelElement.style.width = 65;
            samples.labelElement.style.minWidth = 82; samples.labelElement.style.width = 82;
            mode.RegisterValueChangedCallback(_ => Refresh());
            samples.RegisterValueChangedCallback(_ => Refresh());
            estimate = new Label { name = "multi-estimate" }; estimate.style.whiteSpace = WhiteSpace.Normal; Root.Add(estimate);
            VisualElement controls = new VisualElement(); controls.style.flexDirection = FlexDirection.Row; Root.Add(controls);
            start = new Button(Start) { text = "선택 레벨 시험 시작", name = "multi-start" };
            pause = new Button(() => { session.SetPaused(!session.Paused); Refresh(); }) { text = "일시정지", name = "multi-pause" };
            stop = new Button(() => { session.Stop(); Refresh(); }) { text = "중지", name = "multi-stop" };
            controls.Add(start); controls.Add(pause); controls.Add(stop);
            progress = new Label { name = "multi-progress" }; progress.style.whiteSpace = WhiteSpace.Normal; Root.Add(progress);
            VisualElement captions = new VisualElement(); captions.style.flexDirection = FlexDirection.Row; Root.Add(captions);
            Label selectionTitle = new Label("시험할 레벨 · 다중 선택"); selectionTitle.style.width = Length.Percent(40); captions.Add(selectionTitle);
            captions.Add(new Label("레벨별 결과 · 선택하여 아래에서 확인"));
            VisualElement lists = new VisualElement(); lists.style.flexDirection = FlexDirection.Row; lists.style.height = 240; lists.style.flexShrink = 0; Root.Add(lists);
            choices = new ListView { name = "multi-level-choices", itemsSource = levels, fixedItemHeight = 25, selectionType = SelectionType.Multiple,
                makeItem = () => new Label(), bindItem = (e, i) => { ((Label)e).text = levels[i].name; e.tooltip = AssetDatabase.GetAssetPath(levels[i]); } };
            choices.style.width = Length.Percent(40); lists.Add(choices); choices.selectionChanged += _ => Refresh();
            results = new ListView { name = "multi-results", fixedItemHeight = 60, selectionType = SelectionType.Single,
                makeItem = () => new Label(), bindItem = (e, i) => {
                    MultiLevelTestEntry entry = record.entries[i]; ((Label)e).text = entry.name + " · " + StatusName(entry.status) + "\n" + entry.message;
                    e.tooltip = entry.message;
                } };
            results.style.flexGrow = 1; lists.Add(results); results.selectionChanged += _ => ReadSelected();
            VisualElement resultTools = new VisualElement(); resultTools.style.flexDirection = FlexDirection.Row; Root.Add(resultTools);
            read = new Button(ReadSelected) { text = "결과 다시 읽기", name = "multi-read" }; resultTools.Add(read);
            open = new Button(() => openRepeat(selectedFolder)) { text = "판별 통계·사례 열기", name = "multi-open-repeat" }; resultTools.Add(open);
            resultTools.Add(new Button(() => {
                if (record != null) EditorUtility.RevealInFinder(Path.GetFullPath(Path.Combine(this.store.Root, record.id)));
            }) { text = "결과 폴더" });
            ScrollView details = new ScrollView(); details.style.flexGrow = 1; details.style.minHeight = 140; Root.Add(details);
            detail = new Label("오른쪽에서 레벨 결과를 선택하세요.") { name = "multi-detail" }; detail.style.whiteSpace = WhiteSpace.Normal; details.Add(detail);
            try { record = this.store.Load(); } catch (Exception error) { detail.text = error.Message; }
            results.itemsSource = record?.entries; CreateHistory();
            ArrangeWorkspace(guide, executionTitle, options, controls, resultTools, details);
            RefreshLevels();
            EditorApplication.update += Tick;
        }

        /// <summary>선택 목록 갱신은 실행 전후에만 허용하여 순번이 실행 도중 바뀌지 않게 한다.</summary>
        private void RefreshLevels()
        {
            if (CanContinue) return;
            choices.ClearSelection(); levels.Clear();
            levels.AddRange(AssetDatabase.FindAssets("t:LevelDefinition").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<LevelDefinition>).Where(l => l != null).OrderBy(l => l.LevelNumber).ThenBy(l => l.name));
            choices.Rebuild(); Refresh();
        }
        private void Start()
        {
            if (CanContinue || otherBusy()) return;
            try
            {
                session?.Dispose();
                session = new MultiLevelTestSession(choices.selectedIndices.OrderBy(i => i).Select(i => levels[i]), (MultiLevelTestMode)mode.index, samples.value, store);
                record = session.Record; results.ClearSelection(); results.itemsSource = record.entries; results.Rebuild();
                repeatReader = null; balanceReader = null; selectedFolder = null; detail.text = "진행 결과를 선택하면 해당 레벨의 결과만 확인합니다.";
            }
            catch (Exception error) { detail.text = "시작 불가: " + error.Message; }
            Refresh();
        }
        /// <param name="value">탭 표시 여부. 숨기면 일시정지하고 복귀해도 자동 재개하지 않는다.</param>
        internal void SetVisible(bool value)
        {
            visible = value; Root.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            if (!value && CanContinue && !session.Paused) session.SetPaused(true);
            Refresh();
        }
        private void Tick()
        {
            if (!visible) return;
            Stopwatch budget = Stopwatch.StartNew();
            AdvanceHistory(budget);
            if (CanContinue && !session.Paused)
                do { session.Advance(); } while (CanContinue && !session.Paused && budget.Elapsed.TotalMilliseconds < 8);
            // 결과는 저장된 원시 판까지 검사한 뒤 표시한다. 실행 중인 묶음의 파일을 동시에 읽지 않는다.
            if (repeatReader != null && !repeatReader.IsDone || balanceReader != null && !balanceReader.IsDone)
            {
                do { repeatReader?.Advance(); balanceReader?.Advance(); }
                while ((repeatReader != null && !repeatReader.IsDone || balanceReader != null && !balanceReader.IsDone) && budget.Elapsed.TotalMilliseconds < 8);
                // 내용과 조작 가능 상태를 함께 갱신한다. 완료 화면에서 버튼만 늦게 켜지는 간격을 없앤다.
                if (repeatReader?.IsDone == true || balanceReader?.IsDone == true) { ShowResult(); Refresh(); }
            }
            if (EditorApplication.timeSinceStartup < nextDisplay) return;
            nextDisplay = EditorApplication.timeSinceStartup + .25; Refresh();
        }
        private void Refresh()
        {
            if (choices == null || start == null || progress == null) return;
            bool busy = CanContinue;
            historyChoice?.SetEnabled(!busy && !otherBusy() && historyScan == null);
            historyRefresh?.SetEnabled(!busy && !otherBusy() && historyScan == null);
            int selectedCount = choices.selectedIndices.Count();
            estimate.text = $"선택 {selectedCount}개 · 예정 {(long)selectedCount * (mode.index == 1 ? 20000 : Math.Max(0L, samples.value) * 2):N0}판" +
                (mode.index == 1 ? " · 레벨당 1~100회, 두 전략 각 100판" : " · 현재 이동 횟수로 두 전략 시험");
            choices.SetEnabled(!busy); selectionTools.SetEnabled(!busy); mode.SetEnabled(!busy);
            samples.SetEnabled(!busy && mode.index == 0);
            start.SetEnabled(!busy && !otherBusy() && choices.selectedIndices.Any());
            pause.SetEnabled(busy); stop.SetEnabled(busy); pause.text = session?.Paused == true ? "재개" : "일시정지";
            read.SetEnabled(record != null && results.selectedIndex >= 0);
            open.SetEnabled(repeatReader?.IsDone == true && repeatReader.Error == null && selectedFolder != null);
            progress.text = record == null ? $"선택 {choices.selectedIndices.Count()}개 · 추천 시험은 레벨마다 20,000판입니다." :
                $"{(record.mode == MultiLevelTestMode.Balance ? "이동 횟수 추천" : "반복 시험")} · 완료 {record.entries.Count(e => e.status == MultiLevelTestStatus.Completed)}/{record.entries.Count}개 · " +
                $"오류 {record.entries.Count(e => e.status == MultiLevelTestStatus.Error)}개" + (session?.Error != null ? "\n" + session.Error : "");
            results.RefreshItems();
        }
        private void ReadSelected()
        {
            repeatReader = null; balanceReader = null; selectedFolder = null;
            if (record == null || results.selectedIndex < 0) return;
            int index = results.selectedIndex; MultiLevelTestEntry entry = record.entries[index];
            detail.text = entry.name + " · " + StatusName(entry.status) + "\n" + entry.message;
            if (CanContinue && index == session.Index) { detail.text += "\n실행 중인 레벨은 중지 또는 완료 후 확인하세요."; Refresh(); return; }
            if (string.IsNullOrEmpty(entry.resultId)) { Refresh(); return; }
            try
            {
                selectedFolder = Path.Combine(store.LevelRoot(record, index), entry.resultId);
                if (record.mode == MultiLevelTestMode.Balance) balanceReader = new BotMoveBalanceStore(store.LevelRoot(record, index)).OpenReader();
                else repeatReader = new BotAnalysisReader(selectedFolder);
                detail.text += "\n원시 기록 확인 중 · 확인이 끝난 뒤 추천 표시";
                if (repeatReader?.IsDone == true || balanceReader?.IsDone == true) ShowResult();
            }
            catch (Exception error) { detail.text += "\n조회 실패: " + error.Message; }
            Refresh();
        }
        /// <summary>기존 공통 평가·추천 규칙만 사용한다. 표본 부족이나 버전 차이를 임의 등급으로 바꾸지 않는다.</summary>
        private void ShowResult()
        {
            string error = repeatReader?.Error ?? balanceReader?.Error;
            if (error != null) { detail.text = "기록 확인 실패 · 추천 없음\n" + error; return; }
            string heading = record.entries[results.selectedIndex].name + "\n";
            if (balanceReader != null)
            {
                BotMoveBalanceRecord value = balanceReader.Record;
                if (value == null) { detail.text = heading + "저장 기록 없음"; return; }
                if (value.rulesVersion != BotMoveRecommendations.Version || value.engineVersion != BotMoveRecommendations.ExecutionVersion)
                { detail.text = heading + "실행·평가 버전이 다른 기록 · 추천 표시 안 함"; return; }
                detail.text = heading + string.Join("\n", Enumerable.Range(0, 4).Select(grade => {
                    string range = BotMoveRecommendations.Ranges(value.trials, grade);
                    return BotMoveRecommendations.Titles[grade] + " : " + (range.Length > 0 ? range : value.status == BotBatchStatus.Completed ? "추천 없음" : "추가 시험 필요");
                })) + "\n" + string.Join("\n", value.trials.Select(t => $"{t.moves}회 · 기본 {t.basicWon}/100 · 계획 {t.planningWon}/100"));
            }
            else
            {
                BotStrategyStatistics basic = BotBatchStatistics.Calculate(repeatReader.Record, repeatReader.Games, repeatReader.Missions, BotStrategyKind.Basic);
                BotStrategyStatistics planning = BotBatchStatistics.Calculate(repeatReader.Record, repeatReader.Games, repeatReader.Missions, BotStrategyKind.Planning);
                BotPairedStatistics pairs = BotBatchStatistics.Pair(repeatReader.Record.seeds,
                    repeatReader.Games.Where(g => g.Strategy == BotStrategyKind.Basic), repeatReader.Games.Where(g => g.Strategy == BotStrategyKind.Planning));
                string versionIssue = BotRecordReplay.CompatibilityError(repeatReader.Record);
                if (repeatReader.Record.basicVersion != BasicBotStrategy.Version || repeatReader.Record.planningVersion != PlanningSearch.Version ||
                    repeatReader.Record.observationVersion != BotObservationBuilder.Version || repeatReader.Record.assumptionVersion != BotBatchSession.AssumptionVersion)
                    versionIssue = "전략·관찰·가정 버전이 현재 기준과 다릅니다.";
                BotDifficultyResult value = BotDifficultyRules.Evaluate(repeatReader.Record, basic, planning, pairs, repeatReader.InitialMoves,
                    repeatReader.DefinitionIssue, versionIssue);
                int grade = BotMoveRecommendations.Grade(value.Grade);
                detail.text = heading + $"이동 {repeatReader.InitialMoves}회 · 기본 성공 {basic.Won}/{basic.Normal} · 계획 성공 {planning.Won}/{planning.Normal}\n" +
                    (grade < 0 ? "판단 보류 · " + string.Join(" / ", value.HoldReasons) : "예상 난이도 : " + BotMoveRecommendations.Titles[grade]) +
                    "\n네 난이도별 추천 이동 횟수는 ‘난이도별 이동 횟수 추천’ 방식으로 시험하세요.";
            }
        }
        private static string StatusName(MultiLevelTestStatus status) => status switch {
            MultiLevelTestStatus.Waiting => "대기", MultiLevelTestStatus.Running => "진행", MultiLevelTestStatus.Paused => "일시정지",
            MultiLevelTestStatus.Completed => "완료", MultiLevelTestStatus.Error => "오류", MultiLevelTestStatus.Stopped => "중지", _ => "중단" };
        public void Dispose() { EditorApplication.update -= Tick; historyScan?.Dispose(); historyScan = null; session?.Dispose(); session = null; }
    }
}
