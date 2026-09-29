using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AutoPlay;
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>이력과 재생 전용 패널. 편집 원본이나 봇 세션을 소유하지 않으며 기록의 사본만 다룬다.</summary>
    internal sealed partial class LevelAnalysisPanel : IDisposable
    {
        internal VisualElement Root { get; } = new VisualElement { name = "analysis-panel" };
        private readonly Func<LevelDefinition> currentLevel;
        private readonly Func<bool> trialAdvancing;
        private readonly List<BotHistoryEntry> entries = new List<BotHistoryEntry>();
        private readonly List<BotGameSummary> filtered = new List<BotGameSummary>();
        private IEnumerator<BotHistoryEntry> scan;
        private BotAnalysisReader analysis, reference;
        private BotBatchGame selectedGame;
        private BotRecordReplay replay;
        private BotAnalysisExport export;
        private readonly IVisualElementScheduledItem schedule;
        private readonly ListView history, cases;
        private readonly Label notice, identity, replayStatus, caseInfo, comparisonInfo;
        private readonly VisualElement statistics, comparison, casePage, replayGrid;
        private readonly PopupField<string> strategyFilter, outcomeFilter;
        private readonly Button markReference, exportButton, prepareReplay, replayNext, replayRun, replayPause, replayClose;
        private bool visible, disposed;
        private double nextDisplay;
        internal BotAnalysisReader Analysis => analysis;
        internal BotRecordReplay ReplaySession => replay;
        internal bool IsExporting => export != null;

        /// <summary>파일 삭제 전에 읽기와 재생을 해제하고 이전 비교·추천이 남지 않게 한다.</summary>
        internal void ClearStoredResults()
        {
            scan?.Dispose(); scan = null; CloseReplay(); analysis = reference = null; selectedGame = null;
            entries.Clear(); filtered.Clear(); history.ClearSelection(); cases.ClearSelection(); history.Rebuild(); cases.Rebuild();
            statistics.Clear(); replayGrid.Clear(); identity.text = caseInfo.text = "";
            comparisonInfo.text = "비교 기준을 먼저 지정하세요."; notice.text = "기록 목록을 다시 확인하세요."; UpdateControls();
        }
        internal void ReloadHistory() => RefreshHistory();

        /// <param name="currentLevel">현재 편집 원본을 읽는 접근자.</param><param name="trialAdvancing">다른 패널의 실행 중 여부.</param>
        internal LevelAnalysisPanel(Func<LevelDefinition> currentLevel, Func<bool> trialAdvancing)
        {
            this.currentLevel = currentLevel; this.trialAdvancing = trialAdvancing;
            Root.AddToClassList("initial-state"); Root.style.flexGrow = 1;
            Root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Features/PlayTesting/Editor/Styles/LevelInitialState.uss"));
            Root.AddToClassList("trial-workspace");
            Root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Features/AutoPlay/Editor/Styles/TrialWorkspace.uss"));
            Label title = Text("시험 결과 · 이력", "analysis-heading");
            LevelEditorHelp.Link(title, "저장한 판 기록의 통계·예상 난이도와 판단 근거를 확인하고 실제 행동을 재생합니다.", "analysis.html#summary");
            Root.Add(title);
            VisualElement toolbar = new VisualElement(); toolbar.style.flexDirection = FlexDirection.Row; toolbar.style.flexWrap = Wrap.Wrap; Root.Add(toolbar);
            toolbar.AddToClassList("trial-analysis-tools");
            AddButton(toolbar, "이력 새로고침", "analysis-refresh", RefreshHistory, "프로젝트에 남아 있는 시험 요약을 다시 읽습니다.");
            AddButton(toolbar, "보관 폴더 열기", "analysis-open", () => {
                string folder = EditorUtility.OpenFolderPanel("보관한 시험 폴더 선택", "", "");
                if (!string.IsNullOrEmpty(folder)) OpenRecord(folder);
            }, "batch.json과 판별 JSON 파일이 있는 보관 폴더를 선택하세요.");
            exportButton = AddButton(toolbar, "선택 기록 보관", "analysis-export", () => {
                string parent = EditorUtility.OpenFolderPanel("새 보관 폴더를 만들 위치", "", "");
                if (!string.IsNullOrEmpty(parent)) BeginExport(Path.Combine(parent, "MatchAnalysis-" + analysis.Record.id));
            }, "선택한 폴더 안에 새 JSON 묶음을 만듭니다. 기존 보관본은 덮어쓰지 않습니다.");
            markReference = AddButton(toolbar, "비교 기준으로 지정", "analysis-reference", () => {
                reference = analysis; ShowComparison(); notice.text = "비교 기준을 지정했습니다. 왼쪽에서 다른 시험을 선택하세요.";
            }, "현재 결과를 기준으로 두고 다른 시험과 나란히 비교합니다.");
            notice = Text("이력을 고르거나 보관 폴더를 여세요. Library의 로컬 기록은 해당 폴더 삭제 시 사라집니다.", "analysis-notice"); Root.Add(notice);
            identity = Text("", "analysis-identity"); Root.Add(identity);
            VisualElement body = new VisualElement(); body.style.flexDirection = FlexDirection.Row; body.style.flexGrow = 1; body.style.minHeight = 0; Root.Add(body);
            history = new ListView { name = "analysis-history", itemsSource = entries, fixedItemHeight = 58, selectionType = SelectionType.Single,
                makeItem = () => Text("", null), bindItem = (element, index) => {
                    BotHistoryEntry entry = entries[index];
                    ((Label)element).text = entry.Error != null ? "읽기 오류 · " + Path.GetFileName(entry.DirectoryPath) :
                        (string.IsNullOrWhiteSpace(entry.Record.sourceName) ? "과거 시험" : entry.Record.sourceName) + "\n" + entry.Record.startedUtc + "\n" + entry.Record.id.Substring(0, 8);
                    element.tooltip = entry.Error ?? entry.DirectoryPath;
                } };
            history.style.width = 235; history.style.minWidth = 190; history.style.flexShrink = 0; body.Add(history);
            history.selectionChanged += _ => { if (history.selectedIndex >= 0 && history.selectedIndex < entries.Count) OpenRecord(entries[history.selectedIndex].DirectoryPath); };
            VisualElement detail = new VisualElement(); detail.style.flexGrow = 1; detail.style.minWidth = 0; body.Add(detail);
            VisualElement tabs = new VisualElement(); tabs.style.flexDirection = FlexDirection.Row; detail.Add(tabs);
            statistics = new ScrollView { name = "analysis-statistics" }; comparison = new ScrollView { name = "analysis-comparison" };
            casePage = new VisualElement { name = "analysis-cases-page" };
            foreach (VisualElement page in new[] { statistics, casePage, comparison }) { page.style.flexGrow = 1; page.style.minHeight = 0; detail.Add(page); }
            AddButton(tabs, "통계", "analysis-tab-summary", () => SelectPage(statistics), "두 전략의 성공률과 실패 잔여 목표를 확인합니다.");
            AddButton(tabs, "사례·재생", "analysis-tab-cases", () => SelectPage(casePage), "실제 판을 고르고 저장한 행동을 재생합니다.");
            AddButton(tabs, "이력 비교", "analysis-tab-compare", () => SelectPage(comparison), "기준 시험과 선택한 시험의 조건 차이 및 결과를 비교합니다.");
            comparisonInfo = Text("비교 기준을 먼저 지정하세요.", "analysis-comparison-info"); comparison.Add(comparisonInfo);
            VisualElement filters = new VisualElement(); filters.style.flexDirection = FlexDirection.Row; casePage.Add(filters);
            strategyFilter = new PopupField<string>("전략", new List<string> { "전체", "기본", "계획" }, 0) { name = "analysis-strategy-filter" };
            outcomeFilter = new PopupField<string>("결과", new List<string> { "전체", "성공", "실패", "오류", "중단" }, 0) { name = "analysis-outcome-filter" };
            foreach (PopupField<string> filter in new[] { strategyFilter, outcomeFilter })
            { filter.style.flexGrow = 1; filter.labelElement.style.minWidth = 30; filter.labelElement.style.width = 30; filters.Add(filter); filter.RegisterValueChangedCallback(_ => FilterCases()); }
            cases = new ListView { name = "analysis-cases", itemsSource = filtered, fixedItemHeight = 26, selectionType = SelectionType.Single,
                makeItem = () => Text("", null), bindItem = (element, index) => {
                    BotGameSummary game = filtered[index];
                    string used = game.UsedMoves < 0 ? "기록 없음" : game.UsedMoves.ToString();
                    string remaining = game.RemainingMoves < 0 ? "기록 없음" : game.RemainingMoves.ToString();
                    ((Label)element).text = $"시드 순번 {game.Ordinal / 2 + 1} · {(game.Strategy == BotStrategyKind.Basic ? "기본" : "계획")} · {OutcomeName(game.Outcome)} · 사용 {used} / 남음 {remaining}";
                } };
            cases.style.height = 118; cases.style.flexShrink = 0; casePage.Add(cases);
            cases.selectionChanged += _ => SelectCase();
            VisualElement replayToolbar = new VisualElement(); replayToolbar.style.flexDirection = FlexDirection.Row; replayToolbar.style.flexWrap = Wrap.Wrap; casePage.Add(replayToolbar);
            prepareReplay = AddButton(replayToolbar, "처음부터", "analysis-replay-start", PrepareReplay, "선택한 사례의 저장 사본으로 시작합니다. 현재 편집 레벨은 바뀌지 않습니다.");
            replayNext = AddButton(replayToolbar, "다음 행동", "analysis-replay-next", () => { replay?.Begin(false); UpdateControls(); }, "현재 또는 다음 행동의 후속 처리까지 진행합니다.");
            replayRun = AddButton(replayToolbar, "자동 재생", "analysis-replay-run", () => { replay?.Begin(true); UpdateControls(); }, "기록된 행동을 순서대로 재생합니다.");
            replayPause = AddButton(replayToolbar, "일시정지", "analysis-replay-pause", () => { replay?.Pause(); UpdateControls(); }, "처리 중인 상태를 유지하고 멈춥니다.");
            replayClose = AddButton(replayToolbar, "재생 닫기", "analysis-replay-close", CloseReplay, "재생 사본만 정리합니다. 저장 기록은 유지합니다.");
            replayStatus = Text("사례를 선택한 뒤 처음부터를 누르세요.", "analysis-replay-status"); casePage.Add(replayStatus);
            ScrollView replayScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal); replayScroll.style.flexGrow = 1; replayScroll.style.minHeight = 100; casePage.Add(replayScroll);
            replayGrid = new VisualElement { name = "analysis-replay-grid" }; replayScroll.Add(replayGrid);
            Foldout raw = new Foldout { text = "선택 판의 원시 기록", value = false, name = "analysis-raw" }; casePage.Add(raw);
            ScrollView rawScroll = new ScrollView(); rawScroll.style.maxHeight = 140; raw.Add(rawScroll);
            caseInfo = Text("", "analysis-case-info"); rawScroll.Add(caseInfo);
            schedule = Root.schedule.Execute(Tick).Every(16);
            SelectPage(statistics); UpdateControls();
        }

        private static Label Text(string value, string name)
        {
            Label label = new Label(value) { name = name }; label.style.whiteSpace = WhiteSpace.Normal; label.style.marginBottom = 4;
            return label;
        }

        private static Button AddButton(VisualElement parent, string text, string name, Action action, string tooltip)
        {
            Button button = new Button(action) { text = text, name = name, tooltip = tooltip }; parent.Add(button); return button;
        }

        private void SelectPage(VisualElement chosen)
        {
            foreach (VisualElement page in new[] { statistics, casePage, comparison }) page.style.display = page == chosen ? DisplayStyle.Flex : DisplayStyle.None;
            if (chosen != casePage) replay?.Pause();
        }

        internal void SetVisible(bool value)
        {
            visible = value; Root.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            if (!value) replay?.Pause();
            else { RefreshIdentity(); if (entries.Count == 0 && scan == null) RefreshHistory(); }
            UpdateControls();
        }

        internal void SourceChanged() { CloseReplay(); RefreshIdentity(); }

        private void RefreshHistory()
        {
            // 삭제 도중 탭을 다시 열어도 SetVisible이 파일 열거를 재시작하지 않게 한다.
            if (!Root.enabledSelf) return;
            scan?.Dispose(); scan = BotAnalysisCatalog.Scan().GetEnumerator(); entries.Clear(); history.ClearSelection(); history.Rebuild();
            notice.text = "이력 목록을 읽는 중입니다.";
        }

        internal void OpenRecord(string folder)
        {
            if (export != null) { notice.text = "보관 작업이 끝난 뒤 다른 기록을 선택하세요."; return; }
            CloseReplay(); selectedGame = null; filtered.Clear(); cases.ClearSelection(); cases.Rebuild();
            try { analysis = new BotAnalysisReader(folder); notice.text = "판별 기록을 확인하는 중입니다."; }
            catch (Exception error) { analysis = null; notice.text = "이력을 열지 못했습니다: " + error.Message; }
            // 큰 기록을 읽는 동안 이전 사례/비교가 새 이력의 결과처럼 남지 않게 비운다.
            statistics.Clear(); caseInfo.text = ""; ShowComparison(); RefreshIdentity(); UpdateControls();
            if (analysis == null) statistics.Add(Text("난이도 판단 보류 · 기록을 정상적으로 열지 못했습니다.", "analysis-difficulty-title"));
        }

        internal void BeginExport(string destination)
        {
            if (trialAdvancing()) { notice.text = "시험을 일시정지하거나 중지한 뒤 보관하세요."; return; }
            try { export?.Dispose(); export = new BotAnalysisExport(analysis, destination); notice.text = "기록 보관 중"; }
            catch (Exception error) { notice.text = "보관 시작 불가: " + error.Message; export = null; }
            UpdateControls();
        }

        /// <summary>파일·검색·연쇄를 갱신 사이에 나눈다. 시간 예산은 양보 시점에만 사용한다.</summary>
        internal void Tick()
        {
            if (!visible || disposed) return;
            Stopwatch budget = Stopwatch.StartNew();
            try
            {
                do
                {
                    if (scan != null)
                    {
                        if (scan.MoveNext()) entries.Add(scan.Current);
                        else
                        {
                            scan.Dispose(); scan = null; entries.Sort((a, b) => string.Compare(b.Record?.startedUtc, a.Record?.startedUtc, StringComparison.Ordinal));
                            history.Rebuild(); notice.text = $"저장 이력 {entries.Count}개 · 로컬 캐시는 Library 삭제 시 사라집니다.";
                        }
                    }
                    else if (analysis != null && !analysis.IsDone)
                    {
                        analysis.Advance();
                        if (analysis.IsDone)
                        {
                            notice.text = analysis.Error ?? analysis.Warning ?? "판별 기록 확인 완료";
                            if (analysis.Error == null) { ShowStatistics(); FilterCases(); ShowComparison(); }
                            else statistics.Add(Text("난이도 판단 보류 · " + analysis.Error, "analysis-difficulty-title"));
                            UpdateControls();
                        }
                    }
                    else if (export != null)
                    {
                        export.Advance();
                        if (export.IsDone)
                        {
                            string destination = export.Destination; export.Dispose();
                            notice.text = export.Error == null ? "보관 완료: " + destination : "보관 실패: " + export.Error; export = null; UpdateControls();
                        }
                    }
                    else if (replay?.NeedsAdvance == true) { replay.Advance(); break; }
                    else break;
                } while (budget.Elapsed.TotalMilliseconds < 8);
            }
            catch (Exception error) { scan?.Dispose(); scan = null; replay?.Pause(); notice.text = "작업 오류: " + error.Message; }
            if (EditorApplication.timeSinceStartup >= nextDisplay || replay != null && !replay.NeedsAdvance)
            { nextDisplay = EditorApplication.timeSinceStartup + .1; RefreshIdentity(); DrawReplay(); UpdateControls(); }
        }

        private void UpdateControls()
        {
            bool loaded = analysis?.IsDone == true && analysis.Error == null, busy = export != null;
            markReference.SetEnabled(loaded && !busy); exportButton.SetEnabled(loaded && !busy && !trialAdvancing());
            prepareReplay.SetEnabled(loaded && selectedGame != null && !busy && !trialAdvancing() && BotRecordReplay.CompatibilityError(analysis.Record) == null);
            bool canReplay = replay != null && !replay.IsTerminal;
            replayNext.SetEnabled(canReplay && !replay.NeedsAdvance); replayRun.SetEnabled(canReplay && !replay.NeedsAdvance);
            replayPause.SetEnabled(replay?.NeedsAdvance == true); replayClose.SetEnabled(replay != null);
            if (replay != null) replayStatus.text = replay.Message;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; schedule.Pause(); scan?.Dispose(); scan = null; replay?.Dispose(); replay = null;
            export?.Dispose(); export = null;
        }
    }
}
