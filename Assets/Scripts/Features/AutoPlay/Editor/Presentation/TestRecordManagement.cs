using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>두 화면이 같은 삭제 작업과 잠금을 공유한다. 실제 삭제는 확인 이후에만 시작한다.</summary>
    internal sealed class TestRecordManagement : IDisposable
    {
        private readonly Func<bool> blocked;
        private readonly Action prepare, finished;
        private readonly Action<bool> lockPanels;
        private readonly string[] roots;
        private readonly Func<string, bool> confirm;
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<Label> labels = new List<Label>();
        private TestRecordCleanup cleanup;
        private bool deleting, hasRecords = true;
        private double nextCount;
        private string message = "세 종류의 로컬 시험 기록을 정리합니다. 레벨 원본과 내보낸 파일은 유지합니다.";
        internal bool IsBusy => cleanup != null;
        internal bool CanRequest => !IsBusy && !blocked() && hasRecords;

        /// <param name="blocked">시험·일시정지·내보내기 중인지 확인한다.</param>
        /// <param name="prepare">확인 이후 읽기·재생·캐시를 정리한다.</param>
        /// <param name="finished">삭제 종료 후 실제 남은 기록으로 목록을 갱신한다.</param>
        /// <param name="lockPanels">목록 확인과 삭제 중 다른 시험 시작을 차단한다.</param>
        /// <param name="roots">검사 전용 임시 저장 루트. 제품에서는 생략한다.</param>
        /// <param name="confirm">검사 시 확인/취소를 재현한다. 제품은 취소가 기본인 확인 창을 사용한다.</param>
        internal TestRecordManagement(Func<bool> blocked, Action prepare, Action finished, Action<bool> lockPanels,
            string[] roots = null, Func<string, bool> confirm = null)
        {
            this.blocked = blocked; this.prepare = prepare; this.finished = finished; this.lockPanels = lockPanels; this.roots = roots;
            this.confirm = confirm ?? (text => EditorUtility.DisplayDialogComplex("시험 기록 전체 삭제", text, "취소", "전체 삭제", "닫기") == 1);
            EditorApplication.update += Tick;
        }

        /// <param name="parent">공유 조작을 추가할 기존 화면.</param>
        internal void AddTo(VisualElement parent)
        {
            // 시험 실행과 파괴적인 정리 작업을 같은 도구 안에서 구분한다. 평소에는 접어 공간을 확보한다.
            Foldout area = new Foldout { name = "record-management", text = "기록 관리 · 전체 삭제", value = false };
            area.style.flexShrink = 0; area.style.marginTop = 6; area.style.paddingTop = 5;
            area.style.borderTopWidth = 1; area.style.borderTopColor = new Color(.5f, .38f, .28f);
            area.style.backgroundColor = new Color(.24f, .21f, .18f, .35f);
            VisualElement row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; area.Add(row);
            Button button = new Button(Request) { text = "시험 기록 전체 삭제", name = "records-delete-all" };
            row.Add(button); buttons.Add(button);
            Label helpTitle = new Label("기록 관리"); row.Add(helpTitle);
            LevelEditorHelp.Link(helpTitle, "반복·추천·여러 레벨 시험의 로컬 기록 전체를 확인 후 삭제합니다. 되돌리기는 제공하지 않습니다.", "analysis.html#record-management");
            row.Add(new Button(() => { hasRecords = true; RequestScan(false); }) { text = "기록 수 확인", name = "records-count" });
            Label label = new Label(message) { name = "records-management-status" }; label.style.whiteSpace = WhiteSpace.Normal;
            area.Add(label); labels.Add(label); parent.Add(area);
        }

        internal void Request() => RequestScan(true);
        private bool askAfterScan;
        private void RequestScan(bool ask)
        {
            if (IsBusy || blocked()) return;
            askAfterScan = ask; deleting = false; cleanup = new TestRecordCleanup(roots);
            lockPanels(true); message = "삭제 대상 수를 확인하는 중입니다.";
        }

        internal void Tick()
        {
            // 빈 목록에서 새 시험을 만든 뒤에도 다시 삭제할 수 있게 최상위 폴더 존재만 확인한다.
            // 원시 판 파일이나 요약 내용은 이 주기 검사에서 열지 않는다.
            if (!IsBusy && EditorApplication.timeSinceStartup >= nextCount)
            {
                nextCount = EditorApplication.timeSinceStartup + 1;
                try
                {
                    hasRecords = (roots ?? new[] { BotBatchStore.DefaultRoot, BotMoveBalanceStore.DefaultRoot, MultiLevelTestStore.DefaultRoot }).Any(root => {
                        TestRecordPaths.Check(root, root);
                        return Directory.Exists(root) && Directory.EnumerateDirectories(root).Any(path => Guid.TryParseExact(Path.GetFileName(path), "N", out _));
                    });
                }
                catch { hasRecords = true; } // 누락/접근 오류의 상세 이유는 명시적 목록 확인에서 표시한다.
            }
            try
            {
            if (cleanup != null)
            {
                Stopwatch budget = Stopwatch.StartNew();
                do { cleanup.Advance(); } while (!cleanup.IsDone && (cleanup.Scanning || deleting) && budget.Elapsed.TotalMilliseconds < 8);
                if (!cleanup.Scanning && !deleting)
                {
                    hasRecords = cleanup.Counts.Sum() > 0;
                    string summary = $"반복 시험 {cleanup.Counts[0]}개 / 이동 횟수 추천 {cleanup.Counts[1]}개 / 여러 레벨 시험 {cleanup.Counts[2]}개";
                    string warning = cleanup.Errors.Count == 0 ? "" : "\n보존/조회 오류 " + cleanup.Errors.Count + "건: " + string.Join("\n", cleanup.Errors.Take(5));
                    if (hasRecords && askAfterScan && !blocked() && confirm(summary + warning + "\n하위 판 기록도 삭제합니다. 되돌릴 수 없습니다.\n레벨 원본·등록 맵·따로 내보낸 파일은 유지합니다."))
                    { prepare(); deleting = true; cleanup.Begin(); }
                    else { message = summary + warning + (askAfterScan && hasRecords ? " · 삭제 취소" : ""); End(); }
                }
                else if (cleanup.IsDone)
                {
                    message = $"삭제 완료 {cleanup.Deleted}개 · 실패 {cleanup.Failed}개 · 삭제 파일 {cleanup.FilesDeleted}개" +
                        (cleanup.Errors.Count == 0 ? "" : "\n보존/오류 " + cleanup.Errors.Count + "건: " + string.Join("\n", cleanup.Errors.Take(5)));
                    hasRecords = cleanup.Failed > 0;
                    End(); finished();
                }
                else message = $"기록 삭제 중 · 완료 {cleanup.Deleted}개 · 실패 {cleanup.Failed}개 · 파일 {cleanup.FilesDeleted}개";
            }
            }
            catch (Exception error)
            {
                bool changed = deleting;
                message = "기록 정리 실패: " + error.Message; End();
                if (changed) finished();
            }
            foreach (Button button in buttons) button.SetEnabled(!IsBusy && !blocked() && hasRecords);
            foreach (Label label in labels) label.text = blocked() && !IsBusy ? "시험·일시정지·보관 작업을 끝낸 뒤 기록을 삭제할 수 있습니다." : message;
        }

        private void End() { cleanup?.Dispose(); cleanup = null; deleting = false; lockPanels(false); }
        public void Dispose() { EditorApplication.update -= Tick; End(); }
    }
}
