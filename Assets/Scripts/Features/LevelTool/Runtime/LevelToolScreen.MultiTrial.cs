#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoPlay;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using Levels;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private readonly HashSet<string> multiSelection = new HashSet<string>();
        private PersistedMultiLevelTest toolMulti;
        private AuthoringObjectGraph multiGraph;
        private string[] multiIds;
        private int multiSamples = 100;
        private MultiLevelTestMode multiMode;
        private TrialBoardView multiBoard;
        private VisualElement multiRows, multiLevels, multiHistoryList, balanceRows;
        private Label multiStatus, balanceStatus;
        private IEnumerator<MultiLevelHistoryEntry> multiScan;
        private readonly List<MultiLevelHistoryEntry> multiHistory = new List<MultiLevelHistoryEntry>();
        private BotMoveBalanceReader toolBalanceReader;
        private AuthoringObjectGraph balanceGraph;
        private string balanceResultDirectory;
        private string multiDisplayedState;
        private bool MultiActive => toolMulti != null && (toolMulti.HasWork || toolMulti.CanControl);
        private string MultiDirectory => Path.Combine(Path.GetDirectoryName(BatchDirectory), "MultiLevelTrials");

        private void DrawToolMulti()
        {
            var panel = new Foldout { text = "여러 레벨 · 이동 횟수 추천 시험", value = false }; inspector.Add(panel);
            panel.Add(new Label("체크한 레벨의 현재 초안을 순서대로 시험합니다. 오류가 있는 레벨은 표시하고 다음 레벨로 넘어갑니다. 추천 시험은 레벨마다 이동 1~100회 × 두 전략 × 100판(총 20,000판)이므로 오래 걸립니다. 편집 값은 자동 변경하지 않습니다.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            Choice(panel, "multi-mode", "시험 방식", new[] { "Repeat", "Balance" }, new[] { "현재 이동 횟수 반복", "이동 횟수 추천" }, multiMode.ToString(),
                value => { multiMode = (MultiLevelTestMode)Enum.Parse(typeof(MultiLevelTestMode), value); checkpointPending = true; UpdateToolMulti(); });
            Number(panel, "multi-samples", "반복 전략별 판 수", multiSamples, value => { multiSamples = value; checkpointPending = true; });
            multiLevels = new VisualElement(); panel.Add(multiLevels);
            foreach (var doc in Session.Documents.Where(d => d.Kind == "level").OrderBy(d => (int)d.Data["levelNumber"]))
            {
                string id = doc.Id;
                var toggle = new Toggle(doc.Data["levelNumber"] + " · " + doc.Data["displayName"]) { name = "multi-level-" + id, value = multiSelection.Contains(id) };
                toggle.RegisterValueChangedCallback(e => { if (e.newValue) multiSelection.Add(id); else multiSelection.Remove(id); checkpointPending = true; }); multiLevels.Add(toggle);
            }
            Button(panel, "multi-start", "선택 레벨 시험 시작", StartToolMulti);
            Button(panel, "multi-pause", "여러 레벨 시험 일시정지", () => { toolMulti?.Pause(); UpdateToolMulti(); });
            Button(panel, "multi-resume", "여러 레벨 시험 재개", () => { toolMulti?.Resume(); UpdateToolMulti(); });
            Button(panel, "multi-stop", "여러 레벨 시험 중지", () => { toolMulti?.RequestStop(); UpdateToolMulti(); });
            multiStatus = new Label { name = "multi-status", style = { whiteSpace = WhiteSpace.Normal } }; panel.Add(multiStatus);
            multiRows = new VisualElement(); panel.Add(multiRows); multiDisplayedState = null;
            Button(panel, "multi-history-refresh", "지난 여러 레벨 시험 조회", () => {
                multiScan?.Dispose(); multiHistory.Clear(); DrawMultiHistory(); multiScan = new MultiLevelTestStore(MultiDirectory).Scan().GetEnumerator();
            });
            multiHistoryList = new VisualElement(); panel.Add(multiHistoryList); DrawMultiHistory();
            balanceStatus = new Label { name = "balance-status", style = { whiteSpace = WhiteSpace.Normal } }; panel.Add(balanceStatus);
            balanceRows = new VisualElement(); panel.Add(balanceRows); ShowBalanceResult();
            multiBoard?.Dispose(); multiBoard = new TrialBoardView("multi-trial-board", "여러 레벨 시험 현재 판"); boardScroll.Add(multiBoard.Root);
            UpdateToolMulti();
        }

        private void StartToolMulti()
        {
            if (busy || RecordsBusy || MultiActive || BatchActive || toolBot?.NeedsAdvance == true) return;
            AuthoringObjectGraph graph = null;
            try
            {
                var snapshot = sharedTutorialDraft?.Preview(Session) ?? Session.CreateSnapshot();
                string[] ids = snapshot.Documents.Where(d => d.Kind == "level" && multiSelection.Contains(d.Id))
                    .OrderBy(d => (int)d.Data["levelNumber"]).Select(d => d.Id).ToArray();
                if (ids.Length == 0) throw new InvalidOperationException("시험할 레벨을 하나 이상 체크하세요.");
                graph = new AuthoringObjectGraph(snapshot.Documents);
                var levels = ids.Select(id => (LevelDefinition)graph.Resolve(id)).ToArray();
                var contexts = levels.Select((level, index) => new { level, id = ids[index] }).ToDictionary(pair => pair.level,
                    pair => new BotTrialSourceContext(pair.id, AuthoringTrialSource.Encode(snapshot, pair.id), moves => AuthoringTrialSource.EncodeWithMoves(snapshot, pair.id, moves)));
                var next = new PersistedMultiLevelTest(levels, multiMode, multiMode == MultiLevelTestMode.Balance ? 100 : multiSamples, MultiDirectory, level => contexts[level]);
                toolMulti?.Dispose(); multiGraph?.Dispose(); DisposeToolBot();
                toolMulti = next; multiGraph = graph; graph = null; multiIds = ids;
                multiDisplayedState = null; UpdateToolMulti(); UpdateToolBotControls();
            }
            catch (Exception error) { graph?.Dispose(); Show("여러 레벨 시험 시작 실패: " + error.Message); }
        }

        private void AdvanceToolMulti()
        {
            if (busy) return;
            if (toolMulti?.HasWork == true) { toolMulti.Advance(); UpdateToolMulti(); UpdateToolBotControls(); }
            try
            {
                if (multiScan != null)
                {
                    if (multiScan.MoveNext()) { multiHistory.Add(multiScan.Current); DrawMultiHistory(); }
                    else { multiScan.Dispose(); multiScan = null; }
                }
                if (toolBalanceReader != null && !toolBalanceReader.IsDone) { toolBalanceReader.Advance(); ShowBalanceResult(); }
            }
            catch (Exception error) { multiScan?.Dispose(); multiScan = null; Show("여러 레벨 이력 조회 실패: " + error.Message); }
        }

        private void UpdateToolMulti()
        {
            if (inspectorPage != "자동 시험" || multiStatus == null) return;
            bool active = MultiActive;
            multiLevels?.SetEnabled(!active);
            root.Q("multi-mode")?.SetEnabled(!active); root.Q("multi-samples")?.SetEnabled(!active && multiMode == MultiLevelTestMode.Repeat);
            root.Q<Button>("multi-start")?.SetEnabled(!active && !BatchActive && toolBot?.NeedsAdvance != true);
            root.Q<Button>("multi-pause")?.SetEnabled(toolMulti?.CanControl == true && !toolMulti.Paused);
            root.Q<Button>("multi-resume")?.SetEnabled(toolMulti?.CanControl == true && toolMulti.Paused);
            root.Q<Button>("multi-stop")?.SetEnabled(active);
            multiStatus.text = toolMulti == null ? "시험할 레벨을 체크하세요." : toolMulti.PersistenceError ??
                (toolMulti.IsSaving ? "기록 저장 중" : toolMulti.Paused ? "일시정지" : active ? "실행 중" : "시험 종료") + " · " + toolMulti.Index + "/" + toolMulti.Record.entries.Count + "레벨 처리";
            multiBoard?.Show(toolMulti?.Current?.State, toolMulti?.Current == null || multiGraph == null ? null : (LevelDefinition)multiGraph.Resolve(multiIds[toolMulti.Index]));
            string state = toolMulti == null ? "" : string.Join("|", toolMulti.Record.entries.Select(e => e.status + ":" + e.message)) + ":" + toolMulti.IsSaving;
            if (state == multiDisplayedState) return;
            multiDisplayedState = state; multiRows.Clear();
            if (toolMulti != null) DrawMultiEntries(multiRows, toolMulti.Record, !active && !toolMulti.IsSaving && toolMulti.PersistenceError == null);
        }

        private void DrawMultiEntries(VisualElement parent, MultiLevelTestRecord record, bool allowOpen)
        {
            for (int i = 0; i < record.entries.Count; i++)
            {
                var entry = record.entries[i]; int index = i;
                parent.Add(new Label(entry.name + " · " + entry.status + "\n" + entry.message) { style = { whiteSpace = WhiteSpace.Normal } });
                if (string.IsNullOrEmpty(entry.resultId)) continue;
                Button(parent, "multi-result-" + record.id + "-" + i, "이 레벨 결과 열기", () => {
                    string levelRoot = new MultiLevelTestStore(MultiDirectory).LevelRoot(record, index);
                    if (record.mode == MultiLevelTestMode.Repeat) OpenHistory(Path.Combine(levelRoot, entry.resultId));
                    else OpenBalanceResult(levelRoot, entry.resultId);
                }).SetEnabled(allowOpen);
            }
        }

        private void DrawMultiHistory()
        {
            if (multiHistoryList == null) return;
            multiHistoryList.Clear();
            foreach (var entry in multiHistory.OrderByDescending(e => e.Record?.startedUtc, StringComparer.Ordinal))
            {
                if (entry.Error != null) { multiHistoryList.Add(new Label("기록 오류: " + entry.Error)); continue; }
                var group = new Foldout { text = entry.Record.startedUtc + " · " + entry.Record.mode, value = false }; multiHistoryList.Add(group);
                DrawMultiEntries(group, entry.Record, !MultiActive);
            }
        }

        private void OpenBalanceResult(string directory, string id)
        {
            AuthoringObjectGraph graph = null;
            try
            {
                string folder = Path.Combine(directory, id);
                TestRecordPaths.Check(MultiDirectory, Path.Combine(folder, "source.context"));
                var source = AuthoringTrialSource.Decode(File.ReadAllText(Path.Combine(folder, "source.context")));
                graph = new AuthoringObjectGraph(source.Snapshot.Documents);
                // 각 레벨 폴더에는 추천 실행이 하나만 있으며, 포인터도 선택 결과와 일치해야 한다.
                var reader = new BotMoveBalanceStore(directory).OpenReader((LevelDefinition)graph.Resolve(source.LevelId));
                if (reader.Record?.id != id) throw new InvalidDataException("선택한 추천 기록이 변경되었습니다. 다시 조회하세요.");
                toolBalanceReader = reader; balanceGraph?.Dispose(); balanceGraph = graph; graph = null; balanceResultDirectory = folder;
                ShowBalanceResult();
            }
            catch (Exception error) { graph?.Dispose(); Show("추천 결과 열기 실패: " + error.Message); }
        }

        private void ShowBalanceResult()
        {
            if (balanceStatus == null) return;
            balanceRows.Clear();
            var reader = toolBalanceReader;
            if (reader == null) { balanceStatus.text = "추천 결과를 선택하면 검증한 이동 횟수 구간을 표시합니다."; return; }
            if (!reader.IsDone) { balanceStatus.text = "추천 원시 판 검증 중 · " + reader.CheckedTrials + "구간"; return; }
            if (reader.Error != null) { balanceStatus.text = "추천 기록 오류: " + reader.Error; return; }
            var record = reader.Record;
            bool sameVersion = record.engineVersion == BotMoveRecommendations.ExecutionVersion && record.rulesVersion == BotMoveRecommendations.Version;
            balanceStatus.text = record.sourceName + " · " + record.status + " · 완료 구간 " + record.trials.Count + "/100\n" +
                (sameVersion ? "실제 완료한 구간만 추천합니다. 원본 이동 횟수는 바뀌지 않습니다." : "실행 또는 평가 버전이 달라 추천을 보류합니다.");
            if (sameVersion)
                for (int grade = 0; grade < 4; grade++) balanceRows.Add(new Label(BotMoveRecommendations.Titles[grade] + ": " + BotMoveRecommendations.Ranges(record.trials, grade)));
            foreach (var trial in record.trials)
            {
                var selected = trial;
                Button(balanceRows, "balance-trial-" + trial.moves, "이동 " + trial.moves + "회 · 기본 " + trial.basicWon + "/100 · 계획 " + trial.planningWon + "/100 · 원시 판 조회",
                    () => OpenHistory(Path.Combine(balanceResultDirectory, "trials", selected.batchId)));
                if (!string.IsNullOrEmpty(trial.reason)) balanceRows.Add(new Label(trial.reason) { style = { whiteSpace = WhiteSpace.Normal } });
            }
        }

        private async UniTask CloseToolMulti()
        {
            var closing = toolMulti; var graph = multiGraph; toolMulti = null; multiGraph = null;
            multiScan?.Dispose(); multiScan = null; toolBalanceReader = null; balanceGraph?.Dispose(); balanceGraph = null;
            multiBoard?.Dispose(); multiBoard = null;
            try { if (closing != null) await closing.StopAndDisposeAsync(); }
            finally { graph?.Dispose(); }
        }
    }
}
#endif
