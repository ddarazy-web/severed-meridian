#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AutoPlay;
using LevelAuthoring.Runtime;
using Levels;
using Simulation;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private string historyStrategy = "전체", historyOutcome = "전체";
        private BotAnalysisReader historyReference;
        private Label historyComparison, historyCaseDetail, historyIdentity;
        private string historyCaseText = "판을 선택하면 행동과 잔여 미션을 확인할 수 있습니다.";

        private void DrawHistoryFilters(VisualElement panel)
        {
            var strategy = new DropdownField("전략", new List<string> { "전체", "기본", "계획" }, historyStrategy) { name = "history-strategy" };
            strategy.RegisterValueChangedCallback(e => { historyStrategy = e.newValue; historyPage = 0; DrawHistoryGames(); }); panel.Add(strategy);
            var outcome = new DropdownField("종료 결과", new List<string> { "전체", "성공", "실패", "오류", "중단" }, historyOutcome) { name = "history-outcome" };
            outcome.RegisterValueChangedCallback(e => { historyOutcome = e.newValue; historyPage = 0; DrawHistoryGames(); }); panel.Add(outcome);
            historyIdentity = new Label { name = "history-identity", style = { whiteSpace = WhiteSpace.Normal } }; panel.Add(historyIdentity);
            Button(panel, "history-reference", "선택 기록을 비교 기준으로 지정", () => {
                if (historyReader?.IsDone != true || historyReader.Error != null) return;
                historyReference = historyReader; ShowHistoryComparison();
            });
            historyComparison = new Label { name = "history-comparison", style = { whiteSpace = WhiteSpace.Normal } }; panel.Add(historyComparison);
            historyCaseDetail = new Label(historyCaseText) { name = "history-case-detail", style = { whiteSpace = WhiteSpace.Normal } }; panel.Add(historyCaseDetail);
            ShowHistoryComparison();
        }

        private IEnumerable<BotGameSummary> FilterHistoryGames() => (historyReader?.Games ?? Array.Empty<BotGameSummary>()).Where(g =>
            (historyStrategy == "전체" || g.Strategy == (historyStrategy == "기본" ? BotStrategyKind.Basic : BotStrategyKind.Planning)) &&
            (historyOutcome == "전체" || historyOutcome == "성공" && g.Outcome == BotSessionStatus.Won ||
             historyOutcome == "실패" && (g.Outcome == BotSessionStatus.MovesExhausted || g.Outcome == BotSessionStatus.Blocked) ||
             historyOutcome == "오류" && g.Outcome == BotSessionStatus.Error || historyOutcome == "중단" && g.Outcome == BotSessionStatus.Stopped));

        private static string HistoryStrategyText(string name, BotStrategyStatistics stats)
        {
            var text = new StringBuilder(name + " · 정상 종료 " + stats.Normal + " · 성공 " + stats.Won +
                " · 성공률 " + (stats.SuccessPercent?.ToString("0.0") ?? "—") + "% · 미실행 " + stats.Unrun + " · 오류 " + stats.Errors + " · 중지 " + stats.Stopped);
            text.Append("\n이동 수 소진 " + stats.Exhausted + " · 막힘 " + stats.Blocked);
            foreach (var metric in new[] { (name: "성공 판 사용 이동", value: stats.Used), (name: "성공 판 남은 이동", value: stats.Remaining) })
                text.Append("\n" + metric.name + (metric.value.Count == 0 ? " · 표본 없음" :
                    $" · 평균 {metric.value.Mean:0.00} · 중앙 {metric.value.Median:0.0} · 최소 {metric.value.Minimum} · 최대 {metric.value.Maximum} · 표본 {metric.value.Count}"));
            foreach (var mission in stats.Missions)
                text.Append($"\n미션 {mission.Index + 1} · {LevelMissionRules.Name(mission.Kind)} · {mission.Color} · 패배 잔여 " +
                    (mission.Samples == 0 ? "표본 없음" : $"평균 {mission.MeanRemaining:0.00} · 최대 {mission.MaximumRemaining} · 남은 판 {mission.Unfinished}/{mission.Samples}") + $" · 누락 {mission.Missing}");
            return text.ToString();
        }

        private void ShowHistoryComparison()
        {
            if (historyComparison == null) return;
            if (historyReference == null || historyReader?.IsDone != true || historyReader.Error != null)
            { historyComparison.text = "정상적으로 읽은 기록을 비교 기준으로 지정한 후 다른 기록을 선택하세요."; return; }
            var a = historyReference.Record; var b = historyReader.Record;
            bool sameVersions = a.engineVersion == b.engineVersion && a.sessionVersion == b.sessionVersion && a.basicVersion == b.basicVersion &&
                a.planningVersion == b.planningVersion && a.observationVersion == b.observationVersion && a.assumptionVersion == b.assumptionVersion &&
                (a.startingVersion ?? "starting-board-dfs-v1") == (b.startingVersion ?? "starting-board-dfs-v1");
            var text = new StringBuilder("기준 " + a.startedUtc + " → 선택 " + b.startedUtc + "\n" +
                (a.fingerprint == b.fingerprint ? "같은 정의" : "서로 다른 정의") + " · " +
                (a.seeds.SequenceEqual(b.seeds) ? "같은 시드 묶음" : "다른 시드 묶음") + " · " + (sameVersions ? "같은 규칙/전략" : "다른 규칙/전략 · 쌍 비교 제외"));
            foreach (BotStrategyKind strategy in Enum.GetValues(typeof(BotStrategyKind)))
            {
                text.Append("\n기준 " + HistoryStrategyText(strategy.ToString(), BotBatchStatistics.Calculate(a, historyReference.Games, historyReference.Missions, strategy)));
                text.Append("\n선택 " + HistoryStrategyText(strategy.ToString(), BotBatchStatistics.Calculate(b, historyReader.Games, historyReader.Missions, strategy)));
                if (!sameVersions) continue;
                var pair = BotBatchStatistics.Pair(a.seeds.Union(b.seeds), historyReference.Games.Where(g => g.Strategy == strategy), historyReader.Games.Where(g => g.Strategy == strategy));
                text.Append($"\n공통 시드 {pair.Included}쌍 · 제외 {pair.Excluded} · 둘 다 성공 {pair.BothWon} · 기준만 성공 {pair.LeftOnlyWon} · 선택만 성공 {pair.RightOnlyWon} · 둘 다 실패 {pair.NeitherWon}");
            }
            historyComparison.text = text.ToString();
        }

        private void ShowHistoryIdentity()
        {
            if (historyIdentity == null) return;
            if (historyReader == null) { historyIdentity.text = ""; return; }
            string relation;
            try
            {
                var snapshot = sharedTutorialDraft?.Preview(Session) ?? Session.CreateSnapshot();
                using var graph = new AuthoringObjectGraph(snapshot.Documents);
                relation = LevelStateBuilder.Fingerprint((LevelDefinition)graph.Resolve(Session.SelectedLevelId)) == historyReader.Record.fingerprint ?
                    "현재 편집 내용과 같은 정의" : "현재 편집 내용과 다른 과거 사본";
            }
            catch (Exception error) { relation = "현재 편집 내용과 비교할 수 없음: " + error.Message; }
            historyIdentity.text = relation + "\n기록 ID " + historyReader.Record.id + " · " + historyReader.Record.status + " · 레벨 " + historyReader.LevelNumber;
        }

        private void ShowHistoryCase(BotBatchGame game)
        {
            var text = new StringBuilder($"시드 {game.seed} · {game.outcome} · {game.message}\n사용/남은 이동 {game.usedMoves}/{game.remainingMoves}");
            for (int i = 0; i < game.missions.Length; i++)
                text.Append($"\n미션 {i + 1} · {LevelMissionRules.Name(game.missions[i].kind)} · 남음 {game.missions[i].remaining}");
            foreach (var action in game.actions)
                text.Append($"\n턴 {action.turn} · {action.first}" + (action.hasSecond ? " ↔ " + action.second : " 제자리 발동") +
                    $" · 이동 {action.movesBefore}→{action.movesAfter} · 난수 소비 {action.randomBefore}→{action.randomAfter}\n{action.reason}");
            historyCaseText = text.ToString(); if (historyCaseDetail != null) historyCaseDetail.text = historyCaseText;
        }
    }
}
#endif
