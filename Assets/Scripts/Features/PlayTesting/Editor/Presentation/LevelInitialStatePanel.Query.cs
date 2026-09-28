using System.Collections.Generic;
using System.Linq;
using Board;
using Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelInitialStatePanel
    {
        [SerializeField] private bool startingMode;
        private Label querySummary;
        private VisualElement queryResults;
        private StartingBoardSearch search;
        private IVisualElementScheduledItem searchSchedule;
        private System.Diagnostics.Stopwatch searchWatch;
        internal StartingBoardSearch LastSearch { get; private set; }
        internal bool IsSearching => search != null;

        private void CreateQueryUI(VisualElement root)
        {
            PopupField<string> mode = new PopupField<string>("구성 목적", new List<string> { "원시 후보", "시작 조건 구성" }, startingMode ? 1 : 0) { name = "initial-mode" };
            mode.RegisterValueChangedCallback(evt => { startingMode = evt.newValue == "시작 조건 구성"; Invalidate("구성 목적이 바뀌었습니다. 다시 구성하세요."); });
            root.Add(mode);
            querySummary = new Label { name = "query-summary" }; root.Add(querySummary);
            searchSchedule?.Pause();
            searchSchedule = root.schedule.Execute(TickSearch).Every(16);
        }

        private void AttachQueryResults(VisualElement inspector)
        {
            Foldout results = new Foldout { text = "매칭·행동 조회 (실행 없음)", value = true, name = "query-foldout" };
            queryResults = new ScrollView { name = "query-results" }; results.Add(queryResults); inspector.Insert(0, results);
        }

        private void ClearQuery()
        {
            search = null; LastSearch = null; searchWatch?.Stop();
            queryResults?.Clear();
            if (querySummary != null) querySummary.text = "";
            Label boundary = rootVisualElement.Q<Label>("initial-boundary");
            if (boundary != null) boundary.text = "시작 조건 미검사 · 플레이 미지원";
        }

        private void StartSearch()
        {
            searchWatch = System.Diagnostics.Stopwatch.StartNew();
            search = new StartingBoardSearch(level, seed);
            status.text = "시작 조건 구성 중…";
            UpdateManualControls();
        }

        private void TickSearch()
        {
            if (search == null) return;
            CheckInput();
            if (search == null) return;
            search.Advance(64);
            status.text = $"{search.Message} · 색 할당 {search.Attempts}/{search.Limit}";
            if (!search.IsDone) return;
            searchWatch.Stop(); LastBuildMilliseconds = searchWatch.Elapsed.TotalMilliseconds;
            LastSearch = search; search = null;
            if (LastSearch.State == null)
            {
                status.text += "\n" + string.Join("\n", LastSearch.Issues.Select(issue => issue.ToString()));
                rootVisualElement.Q<Label>("initial-boundary").text = LastSearch.Message + " · 플레이 미지원";
                UpdateManualControls();
                return;
            }
            CurrentState = LastSearch.State; DisplayState();
            status.text += $"\n{StartingBoardSearch.AlgorithmVersion} · 색 할당 {LastSearch.Attempts}/{LastSearch.Limit} · 난수 {LastSearch.RandomDrawCount}회";
        }

        private void ShowQueryResults()
        {
            rootVisualElement.Q<Foldout>("query-foldout").style.display = DisplayStyle.Flex;
            StartConditionReport report = new StartConditionReport(CurrentState);
            rootVisualElement.Q<Label>("initial-boundary").text = report.Message + " · 실제 플레이 미지원";
            querySummary.text = $"매칭 {report.Matches.Count} · 유효 교환 {report.SwapCount} · 제자리 발동 {report.ActivationCount} (아래 목록은 조회만 합니다)";
            queryResults.Clear();
            for (int i = 0; i < report.Matches.Count; i++)
            {
                MatchPattern match = report.Matches[i];
                string kind = match.Kind switch { MatchKind.Magnet => "자석", MatchKind.Bomb => "폭탄", MatchKind.Rocket => "로켓", MatchKind.Drone => "드론", _ => "3매칭" };
                queryResults.Add(new Button(() => Highlight(match.Cells)) { name = "query-match-" + i, text = $"{kind} · 토{(int)match.Color + 1} · {match.Cells.Count}칸", tooltip = string.Join(", ", match.Cells) });
            }
            for (int i = 0; i < report.Actions.Count; i++)
            {
                ActionCandidate action = report.Actions[i];
                BoardCoordinate[] cells = action.Second.HasValue ? new[] { action.First, action.Second.Value } : new[] { action.First };
                queryResults.Add(new Button(() => Highlight(cells)) { name = "query-action-" + i, text = action.Message + " " + action.First + (action.Second.HasValue ? " ↔ " + action.Second : "") });
            }
        }

        private void Highlight(IEnumerable<BoardCoordinate> cells)
        {
            foreach (Button button in grid.Query<Button>().ToList()) button.RemoveFromClassList("query-highlight");
            foreach (BoardCoordinate cell in cells) grid.Q<Button>($"initial-cell-{cell.Row}-{cell.Column}")?.AddToClassList("query-highlight");
            details.text = "조회 좌표: " + string.Join(", ", cells) + "\n실제 교환·제거·파워 효과는 실행하지 않습니다.";
        }

        internal void Dispose() { search = null; searchWatch?.Stop(); searchSchedule?.Pause(); inputSchedule?.Pause(); resumeCascade = false; ClearExecution(); }
    }
}
