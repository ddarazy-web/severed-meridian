#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AutoPlay;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private TestRecordCleanup recordCleanup;
        private bool recordDeleteRequested, recordDeleting, recordConfirming;
        private Label recordStatus;
        private string recordMessage = "이 도구의 로컬 시험 기록만 정리합니다. JSON 작업 폴더와 따로 보관한 파일은 유지합니다.";
        private bool RecordsBusy => recordCleanup != null;
        private bool RecordsBlocked => busy || BatchActive || MultiActive || toolBot?.NeedsAdvance == true ||
            historyReplay?.NeedsAdvance == true || historyExport != null || playRoot != null;

        private void DrawToolRecords()
        {
            var panel = new Foldout { text = "기록 관리 · 전체 삭제", name = "record-management", value = false };
            inspector.Add(panel);
            Button(panel, "records-count", "기록 수 확인", () => StartToolRecords(false));
            Button(panel, "records-delete-all", "시험 기록 전체 삭제", () => StartToolRecords(true))
                .tooltip = "대상 수를 확인한 뒤 다시 삭제를 확인합니다. 실행·일시정지·보관 중에는 삭제할 수 없습니다.";
            recordStatus = new Label(recordMessage) { name = "records-management-status", style = { whiteSpace = WhiteSpace.Normal } };
            panel.Add(recordStatus);
        }

        private void StartToolRecords(bool delete)
        {
            if (RecordsBusy) return;
            if (RecordsBlocked) { Show("시험·일시정지·재생·보관 작업을 끝낸 뒤 기록을 정리하세요."); return; }
            // 추천은 여러 레벨 기록 안에 중첩된다. 독립 추천 루트가 없으면 공통 실행기가 건너뛴다.
            recordCleanup = new TestRecordCleanup(new[] { BatchDirectory, Path.Combine(Path.GetDirectoryName(BatchDirectory), "BalanceTrials"), MultiDirectory });
            recordDeleteRequested = delete; recordDeleting = false; recordConfirming = false;
            editor.SetEnabled(false); SetRecordMessage("기록 수를 확인하는 중입니다.");
        }

        private void SetRecordMessage(string value)
        {
            recordMessage = value;
            if (recordStatus != null) recordStatus.text = value;
        }

        private void AdvanceToolRecords()
        {
            if (recordCleanup == null || recordConfirming) return;
            try
            {
                var timer = Stopwatch.StartNew();
                do { recordCleanup.Advance(); }
                while (!recordCleanup.IsDone && (recordCleanup.Scanning || recordDeleting) && timer.Elapsed.TotalMilliseconds < 4);
                if (!recordCleanup.Scanning && !recordDeleting)
                {
                    string summary = $"반복 {recordCleanup.Counts[0]}개 · 독립 추천 {recordCleanup.Counts[1]}개 · 여러 레벨 {recordCleanup.Counts[2]}개";
                    string warnings = recordCleanup.Errors.Count == 0 ? "" : "\n보존/조회 오류: " + string.Join("\n", recordCleanup.Errors.Take(5));
                    SetRecordMessage(summary + warnings);
                    if (!recordDeleteRequested || recordCleanup.Counts.Sum() == 0) { EndToolRecords(); return; }
                    recordConfirming = true;
                    var panel = OpenModal("시험 기록을 모두 삭제할까요?");
                    panel.Add(new Label(summary + warnings + "\n하위 판과 시험 당시 원본 사본도 삭제합니다. 되돌릴 수 없습니다.\n작업 JSON과 별도 보관 파일은 유지합니다.") { style = { whiteSpace = WhiteSpace.Normal } });
                    Button(panel, "records-cancel-delete", "취소", () => { CloseModal(); SetRecordMessage(summary + " · 삭제 취소"); EndToolRecords(); }).Focus();
                    Button(panel, "records-confirm-delete", "전체 삭제", ConfirmToolRecords);
                }
                else if (recordCleanup.IsDone)
                {
                    SetRecordMessage($"삭제 완료 {recordCleanup.Deleted}개 · 실패 {recordCleanup.Failed}개 · 삭제 파일 {recordCleanup.FilesDeleted}개" +
                        (recordCleanup.Errors.Count == 0 ? "" : "\n보존/오류: " + string.Join("\n", recordCleanup.Errors.Take(5))));
                    EndToolRecords(); Refresh(); StartHistoryScan();
                }
                else SetRecordMessage($"기록 삭제 중 · 완료 {recordCleanup.Deleted}개 · 실패 {recordCleanup.Failed}개");
            }
            catch (Exception error) { SetRecordMessage("기록 정리 실패: " + error.Message); CloseModal(); EndToolRecords(); Refresh(); }
        }

        private void ConfirmToolRecords()
        {
            if (!recordConfirming || recordCleanup == null) return;
            if (RecordsBlocked) { Show("시험 작업을 끝낸 뒤 다시 정리하세요."); CloseModal(); EndToolRecords(); return; }
            // 확인 전에는 읽기/재생 상태도 유지한다. 삭제를 승인한 뒤에만 참조를 해제한다.
            DisposeToolHistory(); historyEntries.Clear();
            multiScan?.Dispose(); multiScan = null; multiHistory.Clear();
            toolBalanceReader = null; balanceGraph?.Dispose(); balanceGraph = null;
            toolBatch?.Dispose(); toolBatch = null; toolBatchGraph?.Dispose(); toolBatchGraph = null;
            toolMulti?.Dispose(); toolMulti = null; multiGraph?.Dispose(); multiGraph = null;
            CloseModal(); recordConfirming = false; recordDeleting = true; recordCleanup.Begin();
        }

        private void EndToolRecords()
        {
            recordCleanup?.Dispose(); recordCleanup = null;
            recordConfirming = false; recordDeleting = false;
            if (editor != null) editor.SetEnabled(!busy);
        }
    }
}
#endif
