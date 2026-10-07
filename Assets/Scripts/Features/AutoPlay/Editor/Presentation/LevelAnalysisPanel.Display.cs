using System;
using System.Linq;
using System.Text;
using AutoPlay;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    internal sealed partial class LevelAnalysisPanel
    {
        private LevelRuntimeState lastDrawn;

        private static string OutcomeName(BotSessionStatus outcome) => outcome switch {
            BotSessionStatus.Won => "성공", BotSessionStatus.MovesExhausted => "이동 수 소진",
            BotSessionStatus.Blocked => "정상 막힘", BotSessionStatus.Error => "실행 오류",
            BotSessionStatus.Stopped => "중단", _ => "확정되지 않음" };

        private void RefreshIdentity()
        {
            if (analysis == null) { identity.text = ""; return; }
            LevelDefinition current = currentLevel();
            BotBatchRecord record = analysis.Record;
            string relation = current == null ? "현재 편집 레벨 없음" : LevelEditorInputIdentity.Matches(current, record.fingerprint) ?
                "현재 편집 내용과 같은 정의" : "현재 편집 내용과 다른 과거 사본";
            string source = string.IsNullOrEmpty(record.sourceGuid) ? "원본 연결 정보 없음 · 이름/번호만으로 이력을 합치지 않습니다." :
                AssetDatabase.GUIDToAssetPath(record.sourceGuid);
            // Unity는 삭제 직후에도 GUID 조회에 이전 경로를 돌려줄 수 있다.
            // 경로 문자열만으로 연결 성공을 표시하지 않고 실제 파일 존재 여부까지 확인한다.
            if (!string.IsNullOrEmpty(record.sourceGuid) && (string.IsNullOrEmpty(source) || !System.IO.File.Exists(source)))
                source = "원본 파일이 없거나 현재 프로젝트에서 찾을 수 없습니다.";
            string state = record.status == BotBatchStatus.Completed ? "전체 완료" : "일부 실행 · " + (record.status switch {
                BotBatchStatus.Running => "실행 중 기록", BotBatchStatus.Paused => "일시정지 시점 기록",
                BotBatchStatus.Stopped => "사용자 중지", BotBatchStatus.Interrupted => "종료로 중단", _ => "오류" });
            identity.text = $"레벨 {analysis.LevelNumber} · {record.sourceName ?? "원본 이름 기록 없음"} · {record.startedUtc}\n{state} · {relation}\n{source}\n" +
                $"지문 {record.fingerprint.Substring(0, Math.Min(12, record.fingerprint.Length))} · 규칙 {record.engineVersion} · 기본 {record.basicVersion} / 계획 {record.planningVersion}";
            string compatibility = BotRecordReplay.CompatibilityError(record);
            if (compatibility != null) identity.text += "\n" + compatibility;
        }

        private void ShowStatistics()
        {
            statistics.Clear();
            AddDifficulty(statistics, analysis, "analysis-difficulty");
            AddStrategySummary(statistics, analysis, BotStrategyKind.Basic, "analysis-basic");
            AddStrategySummary(statistics, analysis, BotStrategyKind.Planning, "analysis-planning");
            BotPairedStatistics pair = BotBatchStatistics.Pair(analysis.Record.seeds,
                analysis.Games.Where(g => g.Strategy == BotStrategyKind.Basic), analysis.Games.Where(g => g.Strategy == BotStrategyKind.Planning));
            statistics.Add(Text($"같은 시드 비교 · 정상 쌍 {pair.Included} / 제외 {pair.Excluded}\n둘 다 성공 {pair.BothWon} · 기본만 성공 {pair.LeftOnlyWon} · 계획만 성공 {pair.RightOnlyWon} · 둘 다 실패 {pair.NeitherWon}", "analysis-pairs"));
            statistics.Add(Text("종료 이유와 남은 목표는 관측값입니다. 실패 원인이나 사람의 체감 난이도를 단정하지 않습니다.", null));
        }

        private static void AddStrategySummary(VisualElement parent, BotAnalysisReader reader, BotStrategyKind strategy, string name)
        {
            BotStrategyStatistics stats = BotBatchStatistics.Calculate(reader.Record, reader.Games, reader.Missions, strategy);
            string rate = stats.SuccessPercent.HasValue ? stats.SuccessPercent.Value.ToString("F1") + "%" : "계산할 기록 없음";
            StringBuilder text = new StringBuilder();
            text.AppendLine((strategy == BotStrategyKind.Basic ? "기본 전략" : "계획 전략") + " · 성공률 " + rate + $" ({stats.Won}/{stats.Normal} 정상 종료)");
            text.AppendLine($"예정 {stats.Planned} · 성공 {stats.Won} · 이동 소진 {stats.Exhausted} · 막힘 {stats.Blocked}");
            text.AppendLine($"오류 {stats.Errors} · 중단 {stats.Stopped} · 미실행 {stats.Unrun}");
            foreach (var metric in new[] { (title: "성공 판 사용 이동", value: stats.Used), (title: "성공 판 남은 이동", value: stats.Remaining) })
                text.AppendLine(metric.value.Count == 0 ? metric.title + ": 기록 없음" :
                    $"{metric.title}: 평균 {metric.value.Mean:F2} · 중앙 {metric.value.Median:F1} · 최소 {metric.value.Minimum} · 최대 {metric.value.Maximum} · 표본 {metric.value.Count}");
            foreach (BotMissionStatistics mission in stats.Missions)
            {
                string title = $"미션 {mission.Index + 1} · {LevelMissionRules.Name(mission.Kind)}" + (mission.Kind == MissionKind.Color ? $" 토{(int)mission.Color + 1}" : "");
                text.AppendLine(mission.Samples == 0 ? title + $" · 패배 잔여 기록 없음 (누락 {mission.Missing})" :
                    $"{title} · 패배 잔여 평균 {mission.MeanRemaining:F2} / 최대 {mission.MaximumRemaining} · 남은 판 {mission.Unfinished}/{mission.Samples} · 누락 {mission.Missing}");
            }
            Label label = Text(text.ToString(), name);
            label.style.paddingLeft = 8; label.style.paddingTop = 6; label.style.paddingBottom = 6;
            label.style.backgroundColor = new Color(.15f, .18f, .22f); parent.Add(label);
        }

        private void FilterCases()
        {
            CloseReplay(); selectedGame = null; filtered.Clear();
            if (analysis?.IsDone == true && analysis.Error == null)
                filtered.AddRange(analysis.Games.Where(g =>
                    (strategyFilter.index == 0 || (int)g.Strategy == strategyFilter.index - 1) &&
                    (outcomeFilter.index == 0 || outcomeFilter.index == 1 && g.Outcome == BotSessionStatus.Won ||
                     outcomeFilter.index == 2 && (g.Outcome == BotSessionStatus.MovesExhausted || g.Outcome == BotSessionStatus.Blocked) ||
                     outcomeFilter.index == 3 && g.Outcome == BotSessionStatus.Error || outcomeFilter.index == 4 && g.Outcome == BotSessionStatus.Stopped)));
            cases.ClearSelection(); cases.Rebuild(); caseInfo.text = $"조건에 맞는 사례 {filtered.Count}판"; UpdateControls();
        }

        private void SelectCase()
        {
            CloseReplay(); selectedGame = null;
            if (cases.selectedIndex < 0 || cases.selectedIndex >= filtered.Count) { UpdateControls(); return; }
            try
            {
                selectedGame = analysis.ReadGame(filtered[cases.selectedIndex].Ordinal);
                StringBuilder text = new StringBuilder();
                text.AppendLine($"시드 {selectedGame.seed} · {OutcomeName(selectedGame.outcome)} · {selectedGame.message}");
                text.AppendLine("이동 수 · 사용 " + (selectedGame.usedMoves < 0 ? "기록 없음" : selectedGame.usedMoves.ToString()) +
                    " / 남음 " + (selectedGame.remainingMoves < 0 ? "기록 없음" : selectedGame.remainingMoves.ToString()));
                for (int i = 0; i < selectedGame.missions.Length; i++)
                    text.AppendLine($"미션 {i + 1} · {LevelMissionRules.Name(selectedGame.missions[i].kind)} · 남음 {selectedGame.missions[i].remaining}");
                foreach (BotBatchAction action in selectedGame.actions)
                    text.AppendLine($"턴 {action.turn} · {action.first}" + (action.hasSecond ? " ↔ " + action.second : " 제자리 발동") +
                        $" · 이동 {action.movesBefore}→{action.movesAfter} · 난수 소비 {action.randomBefore}→{action.randomAfter}\n{action.reason}");
                caseInfo.text = text.ToString();
            }
            catch (Exception error) { notice.text = "사례 읽기 실패: " + error.Message; }
            UpdateControls();
        }

        private void ShowComparison()
        {
            comparison.Clear(); comparison.Add(comparisonInfo);
            if (reference == null || analysis?.IsDone != true || analysis.Error != null)
            { comparisonInfo.text = "비교 기준을 지정한 다음 다른 시험을 선택하세요."; return; }
            BotBatchRecord a = reference.Record, b = analysis.Record;
            bool sameVersions = a.engineVersion == b.engineVersion && a.sessionVersion == b.sessionVersion &&
                a.basicVersion == b.basicVersion && a.planningVersion == b.planningVersion && a.observationVersion == b.observationVersion &&
                a.assumptionVersion == b.assumptionVersion &&
                (string.IsNullOrEmpty(a.startingVersion) ? "starting-board-dfs-v1" : a.startingVersion) ==
                (string.IsNullOrEmpty(b.startingVersion) ? "starting-board-dfs-v1" : b.startingVersion);
            comparisonInfo.text = $"기준 {a.startedUtc} ({a.id.Substring(0, 8)}) → 선택 {b.startedUtc} ({b.id.Substring(0, 8)})\n" +
                (a.fingerprint == b.fingerprint ? "같은 정의" : "수정 전/후 · 서로 다른 정의") + " / " +
                (a.seeds.SequenceEqual(b.seeds) ? "같은 시드 묶음" : "다른 시드 묶음") + " / " +
                (sameVersions ? "같은 규칙·전략 버전" : "다른 규칙/전략 · 쌍 비교 제외");
            AddDifficulty(comparison, reference, "reference-difficulty");
            AddDifficulty(comparison, analysis, "selected-difficulty");
            foreach (BotStrategyKind strategy in Enum.GetValues(typeof(BotStrategyKind)))
            {
                comparison.Add(Text("기준 결과", null)); AddStrategySummary(comparison, reference, strategy, "reference-" + strategy);
                comparison.Add(Text("선택 결과", null)); AddStrategySummary(comparison, analysis, strategy, "selected-" + strategy);
                if (!sameVersions) continue;
                BotPairedStatistics paired = BotBatchStatistics.Pair(a.seeds.Union(b.seeds),
                    reference.Games.Where(g => g.Strategy == strategy), analysis.Games.Where(g => g.Strategy == strategy));
                comparison.Add(Text($"정상 공통 시드 {paired.Included}쌍 · 제외 {paired.Excluded}\n둘 다 성공 {paired.BothWon} · 기준만 성공 {paired.LeftOnlyWon} · 선택만 성공 {paired.RightOnlyWon} · 둘 다 실패 {paired.NeitherWon}", null));
            }
        }

        private void PrepareReplay()
        {
            if (selectedGame == null || trialAdvancing()) { notice.text = "실행 중 시험을 먼저 일시정지하거나 중지하세요."; return; }
            CloseReplay();
            LevelDefinition definition = ScriptableObject.CreateInstance<LevelDefinition>(); definition.hideFlags = HideFlags.HideAndDontSave;
            try { JsonUtility.FromJsonOverwrite(analysis.Record.definitionJson, definition); replay = new BotRecordReplay(definition, analysis.Record, selectedGame); }
            catch (Exception error) { replayStatus.text = "재생 시작 불가: " + error.Message; }
            finally { UnityEngine.Object.DestroyImmediate(definition); }
            DrawReplay(); UpdateControls();
        }

        private void DrawReplay()
        {
            LevelRuntimeState state = replay?.State;
            if (state == lastDrawn) return;
            lastDrawn = state; replayGrid.Clear();
            if (state == null) return;
            Elements.ElementVisualCatalog visuals = Elements.ElementVisualLookup.ForLevel(currentLevel());
            for (int row = 0; row < state.Rows; row++)
            {
                VisualElement line = new VisualElement(); line.AddToClassList("initial-row"); replayGrid.Add(line);
                for (int column = 0; column < state.Columns; column++)
                {
                    RuntimeCell cell = state.CellAt(new BoardCoordinate(row, column));
                    Label label = new Label(LevelInitialStatePanel.CellText(cell, state)) { tooltip = cell.Coordinate.ToString() };
                    label.AddToClassList("initial-cell"); label.style.unityTextAlign = TextAnchor.MiddleCenter;
                    if (!cell.IsActive) label.AddToClassList("inactive");
                    if (cell.Color.HasValue && cell.Cover != CoverKind.Mold) label.AddToClassList("rabbit-" + (int)cell.Color.Value);
                    if (cell.Cover.HasValue) label.AddToClassList("covered");
                    RuntimeBoardArtwork.Bind(label, cell, state, visuals);
                    line.Add(label);
                }
            }
        }

        private void CloseReplay()
        {
            replay?.Dispose(); replay = null; lastDrawn = null; replayGrid.Clear();
            replayStatus.text = "사례를 선택한 뒤 처음부터를 누르세요."; UpdateControls();
        }
    }
}
