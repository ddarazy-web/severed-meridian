using AutoPlay;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    internal sealed partial class MultiLevelTestPanel
    {
        private readonly List<MultiLevelHistoryEntry> pastRuns = new List<MultiLevelHistoryEntry>();
        private IEnumerator<MultiLevelHistoryEntry> historyScan;
        private PopupField<string> historyChoice;
        private Button historyRefresh;
        private Label historyNotice;

        /// <summary>삭제 전에 저장 결과를 참조하는 실행·조회 객체를 정리한다. 진행 중이면 호출하지 않는다.</summary>
        internal void ClearStoredResults()
        {
            historyScan?.Dispose(); historyScan = null; session?.Dispose(); session = null;
            results.ClearSelection(); record = null; repeatReader = null; balanceReader = null; selectedFolder = null;
            ResetErrorDetails();
            results.itemsSource = null; results.Rebuild(); pastRuns.Clear();
            historyChoice.choices = new List<string> { "저장된 시험 없음" }; historyChoice.SetValueWithoutNotify(historyChoice.choices[0]);
            detail.text = "저장된 결과 없음"; Refresh();
        }
        internal void ReloadHistory() => RefreshHistory();

        /// <summary>새 실행 중에도 위의 시험 이름과 아래 결과가 같은 기록을 가리키도록 즉시 맞춘다.</summary>
        private void ShowCurrentHistory()
        {
            historyScan?.Dispose(); historyScan = null; pastRuns.Clear();
            MultiLevelHistoryEntry current = new MultiLevelHistoryEntry { Id = record.id, Record = record };
            pastRuns.Add(current);
            historyChoice.choices = new List<string> { HistoryLabel(current) };
            historyChoice.SetValueWithoutNotify(historyChoice.choices[0]);
            historyNotice.text = "현재 실행 중인 시험 · 종료 후 지난 시험 목록을 갱신합니다.";
        }

        /// <summary>기존 결과 영역을 공유하고 실행 목록만 드롭다운으로 추가한다.</summary>
        private void CreateHistory()
        {
            VisualElement row = new VisualElement { name = "multi-history-tools" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingTop = 5; row.style.paddingBottom = 5;
            row.style.borderBottomWidth = 1; row.style.borderBottomColor = new UnityEngine.Color(.32f, .43f, .48f);
            historyChoice = new PopupField<string>("지난 시험 조회", new List<string> { "목록 읽는 중" }, 0) { name = "multi-history" };
            historyChoice.style.flexGrow = 1; historyChoice.style.minWidth = 0;
            historyChoice.labelElement.style.minWidth = 88; historyChoice.labelElement.style.width = 88;
            LevelEditorHelp.Link(historyChoice.labelElement, "지난 실행을 골라 레벨별 결과를 다시 확인합니다. 시험이 자동으로 시작되지 않습니다.", "difficulty.html#test-history");
            historyChoice.RegisterValueChangedCallback(_ => SelectHistory(historyChoice.index));
            row.Add(historyChoice);
            historyRefresh = new Button(RefreshHistory) { name = "multi-history-refresh", text = "새로고침" }; row.Add(historyRefresh);
            historyNotice = new Label { name = "multi-history-notice" }; historyNotice.style.whiteSpace = WhiteSpace.Normal;
            Root.Insert(1, row); Root.Insert(2, historyNotice); RefreshHistory();
        }

        private void RefreshHistory()
        {
            if (CanContinue || otherBusy()) return;
            historyScan?.Dispose(); pastRuns.Clear(); historyScan = store.Scan().GetEnumerator();
            historyNotice.text = "지난 시험 요약을 읽는 중입니다.";
            Refresh();
        }

        /// <param name="budget">현재 Editor 갱신과 공유하는 시간 예산.</param>
        private void AdvanceHistory(Stopwatch budget)
        {
            if (historyScan == null) return;
            try
            {
                do
                {
                    if (historyScan.MoveNext()) pastRuns.Add(historyScan.Current);
                    else
                    {
                        historyScan.Dispose(); historyScan = null;
                        pastRuns.Sort((a, b) => {
                            int order = string.Compare(b.Record?.startedUtc, a.Record?.startedUtc, StringComparison.Ordinal);
                            return order != 0 ? order : string.Compare(b.Id, a.Id, StringComparison.Ordinal);
                        });
                        historyChoice.choices = pastRuns.Count == 0 ? new List<string> { "저장된 시험 없음" } : pastRuns.Select(HistoryLabel).ToList();
                        int index = pastRuns.FindIndex(e => e.Id == record?.id);
                        if (index < 0) index = 0;
                        historyChoice.SetValueWithoutNotify(historyChoice.choices[index]);
                        historyNotice.text = $"저장된 시험 {pastRuns.Count}개 · 선택 후 아래에서 결과 확인";
                        historyChoice.tooltip = "실행·일시정지 중에는 목록을 바꿀 수 없습니다.";
                        if (record == null && pastRuns.Count > 0) SelectHistory(index);
                        Refresh(); return;
                    }
                } while (budget.Elapsed.TotalMilliseconds < 8);
            }
            catch (Exception error)
            {
                historyScan?.Dispose(); historyScan = null; historyNotice.text = "목록 조회 실패: " + error.Message; Refresh();
            }
        }

        /// <param name="entry">요약만 읽은 실행. 손상 기록은 추천 대신 오류로 구분한다.</param>
        /// <returns>동일한 시각의 시험도 구분할 수 있도록 짧은 ID를 포함한 이름.</returns>
        private static string HistoryLabel(MultiLevelHistoryEntry entry)
        {
            if (entry.Error != null) return "읽기 오류 · " + entry.Id.Substring(0, 8);
            MultiLevelTestRecord value = entry.Record;
            string state = value.entries.Any(e => e.status == MultiLevelTestStatus.Error) ? "오류 포함" :
                value.entries.All(e => e.status == MultiLevelTestStatus.Completed) ? "완료" : "중지·중단 포함";
            string time = DateTime.TryParse(value.startedUtc, out DateTime date) ? date.ToLocalTime().ToString("MM.dd HH:mm") : value.startedUtc;
            return $"{time} · {(value.mode == MultiLevelTestMode.Repeat ? "반복" : "추천")} · {value.entries.Count}레벨 · {state} · {entry.Id.Substring(0, 8)}";
        }

        /// <param name="index">표시 목록의 순번. 실행 중에는 프로그램 호출도 거절한다.</param>
        private void SelectHistory(int index)
        {
            if (CanContinue || otherBusy() || index < 0 || index >= pastRuns.Count) return;
            session?.Dispose(); session = null;
            results.ClearSelection(); repeatReader = null; balanceReader = null; selectedFolder = null; record = null;
            ResetErrorDetails();
            try { record = store.Load(pastRuns[index].Id); detail.text = "레벨 결과를 선택하세요."; }
            catch (Exception error) { detail.text = "기록 확인 실패 · 추천 없음\n" + error.Message; }
            results.itemsSource = record?.entries; results.Rebuild(); Refresh();
        }
    }
}
