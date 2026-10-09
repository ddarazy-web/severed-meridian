#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoPlay;
using LevelAuthoring.Runtime;
using LevelAuthoring.Storage;
using Levels;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private IEnumerator<BotHistoryEntry> historyScan;
        private readonly List<BotHistoryEntry> historyEntries = new List<BotHistoryEntry>();
        private BotAnalysisReader historyReader;
        private AuthoringObjectGraph historyGraph;
        private AuthoringTrialSource historySource;
        private TrialBoardView historyBoard;
        private BotRecordReplay historyReplay;
        private BotAnalysisExport historyExport;
        private VisualElement historyList, historyGames;
        private Label historySummary, historyReplayStatus;
        private int historyPage;
        private string historyDestination = "";

        private void DrawToolHistory()
        {
            var panel = new Foldout { text = "저장된 시험 결과", value = true }; inspector.Add(panel);
            panel.Add(new Label("저장 당시의 레벨·카탈로그로 조회하고 재생합니다. 현재 편집 내용과 정식 게임 진행에는 영향을 주지 않습니다.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            Button(panel, "history-refresh", "저장 이력 새로고침", StartHistoryScan);
            historyList = new VisualElement(); panel.Add(historyList); DrawHistoryEntries();
            historySummary = new Label { name = "history-summary", style = { whiteSpace = WhiteSpace.Normal } }; panel.Add(historySummary);
            DrawHistoryFilters(panel);
            historyGames = new VisualElement(); panel.Add(historyGames);
            Button(panel, "history-previous", "이전 20판", () => { historyPage = Math.Max(0, historyPage - 1); DrawHistoryGames(); });
            Button(panel, "history-next", "다음 20판", () => { if ((historyPage + 1) * 20 < FilterHistoryGames().Count()) historyPage++; DrawHistoryGames(); });
            Button(panel, "history-replay-step", "기록 한 수 재생", () => historyReplay?.Begin(false));
            Button(panel, "history-replay-all", "기록 끝까지 재생", () => historyReplay?.Begin(true));
            Button(panel, "history-replay-pause", "기록 재생 일시정지", () => historyReplay?.Pause());
            Button(panel, "history-replay-close", "재생 닫기", () => { historyReplay?.Dispose(); historyReplay = null; UpdateHistoryReplay(); });
            historyReplayStatus = new Label { name = "history-replay-status", style = { whiteSpace = WhiteSpace.Normal } }; panel.Add(historyReplayStatus);
            var destination = new TextField("새 보관 폴더의 전체 경로") { name = "history-destination", value = historyDestination };
            destination.RegisterValueChangedCallback(e => historyDestination = e.newValue); panel.Add(destination);
            Button(panel, "history-export", "선택 기록을 새 폴더에 보관", () => {
                try { if (historyExport != null) return; historyExport = new BotAnalysisExport(historyReader, historyDestination); }
                catch (Exception error) { Show("기록 보관 실패: " + error.Message); }
            });
            historyBoard?.Dispose(); historyBoard = new TrialBoardView("history-trial-board", "기록 재생 보드"); boardScroll.Add(historyBoard.Root);
            ShowHistoryStatistics(); DrawHistoryGames(); UpdateHistoryReplay();
        }

        private void StartHistoryScan()
        {
            historyScan?.Dispose(); historyEntries.Clear(); DrawHistoryEntries();
            historyScan = BotAnalysisCatalog.Scan(BatchDirectory).GetEnumerator();
        }

        private void DrawHistoryEntries()
        {
            if (historyList == null) return;
            historyList.Clear();
            foreach (var entry in historyEntries.OrderByDescending(e => e.Record?.startedUtc, StringComparer.Ordinal))
            {
                if (entry.Error != null) { historyList.Add(new Label("읽기 실패: " + entry.DirectoryPath + " · " + entry.Error)); continue; }
                var selected = entry;
                Button(historyList, "history-open-" + entry.Record.id, entry.Record.startedUtc + " · " + entry.Record.Recorded + "/" + entry.Record.Total + "판",
                    () => OpenHistory(selected.DirectoryPath));
            }
        }

        private void OpenHistory(string directory)
        {
            if (historyExport != null) { Show("보관 작업 완료 후 다른 기록을 선택하세요."); return; }
            AuthoringObjectGraph graph = null;
            try
            {
                // 저장된 독립 원본으로 임시 카탈로그 참조를 새로 구성한다.
                string context = Path.Combine(directory, "source.context");
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 || (File.GetAttributes(context) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("연결된 경로 대신 실제 기록 폴더를 선택하세요.");
                string text = File.ReadAllText(context);
                var source = AuthoringTrialSource.Decode(text);
                graph = new AuthoringObjectGraph(source.Snapshot.Documents);
                var reader = new BotAnalysisReader(directory, (LevelDefinition)graph.Resolve(source.LevelId));
                if (reader.SourceContext != text) throw new InvalidDataException("조회 중 원본이 변경되었습니다. 다시 여세요.");
                historyReplay?.Dispose(); historyReplay = null; historyGraph?.Dispose();
                historyGraph = graph; graph = null; historySource = source; historyReader = reader; historyPage = 0;
                historyCaseText = "판을 선택하면 행동과 잔여 미션을 확인할 수 있습니다.";
                if (historyCaseDetail != null) historyCaseDetail.text = historyCaseText;
                ShowHistoryStatistics(); DrawHistoryGames(); UpdateHistoryReplay();
            }
            catch (Exception error) { graph?.Dispose(); Show("이력 열기 실패: " + error.Message); }
        }

        private void AdvanceToolHistory()
        {
            if (busy) return;
            try
            {
                if (historyScan != null)
                {
                    if (historyScan.MoveNext()) { historyEntries.Add(historyScan.Current); DrawHistoryEntries(); }
                    else { historyScan.Dispose(); historyScan = null; }
                }
                if (historyReader != null && !historyReader.IsDone)
                {
                    historyReader.Advance();
                    if (historyReader.IsDone) { ShowHistoryStatistics(); DrawHistoryGames(); }
                }
                if (historyExport != null)
                {
                    historyExport.Advance();
                    if (historyExport.IsDone)
                    {
                        historyExport.Dispose();
                        Show(historyExport.Error == null ? "기록 보관 완료: " + historyExport.Destination : "보관 실패: " + historyExport.Error);
                        historyExport = null;
                    }
                }
                if (historyReplay?.NeedsAdvance == true) { historyReplay.Advance(); UpdateHistoryReplay(); }
            }
            catch (Exception error) { historyScan?.Dispose(); historyScan = null; historyReplay?.Pause(); Show("이력 처리 실패: " + error.Message); }
        }

        private void ShowHistoryStatistics()
        {
            if (historySummary == null) return;
            ShowHistoryIdentity(); ShowHistoryComparison();
            if (historyReader == null) { historySummary.text = "조회할 기록을 선택하세요."; return; }
            if (!historyReader.IsDone) { historySummary.text = "판 기록 검증 중…"; return; }
            if (historyReader.Error != null) { historySummary.text = "기록 오류: " + historyReader.Error; return; }
            var record = historyReader.Record;
            var basic = BotBatchStatistics.Calculate(record, historyReader.Games, historyReader.Missions, BotStrategyKind.Basic);
            var planning = BotBatchStatistics.Calculate(record, historyReader.Games, historyReader.Missions, BotStrategyKind.Planning);
            var pairs = BotBatchStatistics.Pair(record.seeds, historyReader.Games.Where(g => g.Strategy == BotStrategyKind.Basic), historyReader.Games.Where(g => g.Strategy == BotStrategyKind.Planning));
            var difficulty = BotDifficultyRules.Evaluate(record, basic, planning, pairs, historyReader.InitialMoves, historyReader.DefinitionIssue,
                AutoPlay.BotBatchStore.SameVersions(record) ? null : "기록 당시 실행 규칙 버전이 다릅니다.");
            historySummary.text = HistoryStrategyText("기본", basic) + "\n" + HistoryStrategyText("계획", planning) +
                "\n동일 시드 비교 " + pairs.Included + "쌍 · 기본만 성공 " + pairs.LeftOnlyWon + " · 계획만 성공 " + pairs.RightOnlyWon + " · 제외 " + pairs.Excluded +
                "\n" + difficulty.Title + "\n" + string.Join("\n", difficulty.HoldReasons.Concat(difficulty.Tags).Concat(difficulty.Notes)) +
                (historyReader.Warning == null ? "" : "\n" + historyReader.Warning);
        }

        private void DrawHistoryGames()
        {
            if (historyGames == null) return;
            historyGames.Clear();
            if (historyReader?.IsDone != true || historyReader.Error != null) return;
            foreach (var game in FilterHistoryGames().Skip(historyPage * 20).Take(20))
            {
                int ordinal = game.Ordinal;
                Button(historyGames, "history-game-" + ordinal, (ordinal + 1) + ". 시드 " + game.Seed + " · " + game.Strategy + " · " + game.Outcome +
                    " · 사용/남은 이동 " + game.UsedMoves + "/" + game.RemainingMoves, () => {
                    try
                    {
                        var saved = historyReader.ReadGame(ordinal); ShowHistoryCase(saved);
                        var next = new BotRecordReplay((LevelDefinition)historyGraph.Resolve(historySource.LevelId), historyReader.Record, saved);
                        historyReplay?.Dispose(); historyReplay = next; UpdateHistoryReplay();
                    }
                    catch (Exception error) { Show("기록 재생 실패: " + error.Message); }
                });
            }
        }

        private void UpdateHistoryReplay()
        {
            historyBoard?.Show(historyReplay?.State, historyGraph == null ? null : (LevelDefinition)historyGraph.Resolve(historySource.LevelId));
            if (historyReplayStatus != null) historyReplayStatus.text = historyReplay == null ? "판을 선택하면 기록된 행동을 재생할 수 있습니다." :
                historyReplay.ActionIndex + "/" + historyReplay.ActionCount + "행동 · " + historyReplay.Message;
        }

        private void DisposeToolHistory()
        {
            historyBoard?.Dispose(); historyBoard = null;
            historyScan?.Dispose(); historyScan = null;
            historyReplay?.Dispose(); historyReplay = null;
            historyExport?.Dispose(); historyExport = null;
            historyGraph?.Dispose(); historyGraph = null; historySource = null; historyReader = null; historyReference = null;
        }
    }
}
#endif
