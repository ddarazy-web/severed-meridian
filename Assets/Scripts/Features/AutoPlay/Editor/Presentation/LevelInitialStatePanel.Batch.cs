using System;
using System.Diagnostics;
using AutoPlay;
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelInitialStatePanel
    {
        private BotBatchSession batchSession;
        private BotBatchRecord previousBatch;
        private BotBatchStore batchStore;
        private IntegerField batchCount;
        private Button batchNew, batchRepeat, batchPause, batchStop;
        private Label batchSummary;
        private ProgressBar batchProgress;
        private string batchError;
        private double nextBatchDisplay;
        internal bool IsAnalysisBlocked => batchSession?.NeedsAdvance == true || botSession?.NeedsAdvance == true || balancePanel?.Session?.NeedsAdvance == true;
        // 일시정지한 반복·밸런스 시험도 재개할 수 있으므로 일괄 시험과 동시에 소유하지 않는다.
        internal bool HasPendingTest => batchSession?.CanContinue == true || balancePanel?.CanContinue == true ||
            botSession?.NeedsAdvance == true || botSession?.Status == BotSessionStatus.Ready;

        /// <summary>종료된 시험의 화면 캐시만 비운다. 삭제한 조건으로 재시험하는 버튼도 해제한다.</summary>
        internal void ClearStoredResults()
        {
            batchSession?.Dispose(); batchSession = null; previousBatch = null; batchError = null;
            EditorApplication.update -= BatchTick;
            balancePanel?.ClearStoredResults();
            if (batchNew != null) UpdateBatchControls();
        }

        /// <summary>한 판 시험 아래에 최소한의 반복 실행 조작을 붙이고 직전 기록만 복원한다.</summary>
        private void CreateBatchUI()
        {
            batchStore ??= new BotBatchStore();
            try { previousBatch = batchStore.LoadLatest(false); batchError = null; }
            catch (Exception error) { previousBatch = null; batchError = "이전 시험을 읽을 수 없습니다: " + error.Message; }
            VisualElement area = new VisualElement { name = "batch-trial" };
            botFoldout.Add(area);
            VisualElement row = new VisualElement { name = "batch-buttons" };
            row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap;
            area.Add(row);
            batchCount = new IntegerField("전략별 횟수") { name = "batch-count", value = BotBatchSession.DefaultCount,
                tooltip = "기본·계획 봇을 각각 이 횟수만큼 시험합니다. 100이면 총 200판입니다. 허용 범위 1~10000." };
            batchCount.style.width = 210; batchCount.labelElement.style.minWidth = 90;
            batchCount.RegisterValueChangedCallback(_ => UpdateBatchControls()); row.Add(batchCount);
            batchNew = new Button(() => StartBatch(false)) { name = "batch-new", text = "새 반복 시험", tooltip = "현재 편집 내용의 사본과 새 시드 묶음으로 두 전략을 순서대로 실행합니다." };
            batchRepeat = new Button(() => StartBatch(true)) { name = "batch-repeat", text = "같은 조건 재시험", tooltip = "직전 묶음의 저장된 레벨 사본·시드를 사용합니다. 현재 편집 내용과 다를 수 있습니다." };
            batchPause = new Button(ToggleBatchPause) { name = "batch-pause", text = "일시정지", tooltip = "탐색·연쇄 상태를 유지하여 같은 위치에서 재개합니다. 탭 이동도 일시정지합니다." };
            batchStop = new Button(() => { batchSession?.Stop(); UpdateBotControls(); }) { name = "batch-stop", text = "반복 중지", tooltip = "현재 미완료 판은 중단하고 완료 기록은 보존합니다. 일시정지 중에도 가능합니다." };
            row.Add(batchNew); row.Add(batchRepeat); row.Add(batchPause); row.Add(batchStop);
            batchProgress = new ProgressBar { name = "batch-progress", lowValue = 0, highValue = 100 };
            area.Add(batchProgress);
            batchSummary = new Label { name = "batch-summary" };
            batchSummary.style.whiteSpace = WhiteSpace.Normal;
            area.Add(batchSummary);
            UpdateBatchControls();
        }

        /// <param name="repeat">직전 묶음의 정의·시드를 재사용할지 여부.</param>
        private void StartBatch(bool repeat)
        {
            if (!visible || batchSession?.CanContinue == true || botSession?.NeedsAdvance == true) return;
            if (repeat && previousBatch == null || !repeat && (level == null || batchCount.value < 1 || batchCount.value > BotBatchSession.MaximumCount)) return;
            LevelDefinition saved = null;
            try
            {
                BotBatchRecord conditions = previousBatch;
                if (repeat)
                {
                    saved = ScriptableObject.CreateInstance<LevelDefinition>(); saved.hideFlags = HideFlags.HideAndDontSave;
                    JsonUtility.FromJsonOverwrite(conditions.definitionJson, saved);
                    if (LevelStateBuilder.Fingerprint(saved) != conditions.fingerprint)
                        throw new InvalidOperationException("저장된 레벨 사본과 기록의 지문이 다릅니다.");
                }
                ClearBatch(); Invalidate("반복 시험 준비");
                int[] seeds = repeat ? conditions.seeds : BotBatchSession.NewSeeds(batchCount.value, conditions?.seeds);
                // 실행 도중 선택 레벨이 바뀌어도 최초 원본의 식별 정보를 유지한다.
                string sourceGuid = repeat ? conditions.sourceGuid : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level));
                string sourceName = repeat ? conditions.sourceName : level.name;
                batchSession = new BotBatchSession(repeat ? saved : level, seeds, (record, game) => {
                    record.sourceGuid = sourceGuid; record.sourceName = sourceName; batchStore.Save(record, game);
                });
                previousBatch = batchSession.Record; inputFingerprint = previousBatch.fingerprint;
                batchCount.SetValueWithoutNotify(seeds.Length);
                batchError = repeat && !BotBatchStore.SameVersions(conditions) ? "규칙 버전이 바뀌었습니다. 시작 조건은 같지만 이전 결과의 완전 재현은 아닙니다." : null;
                manualDiagnostics.SetValueWithoutNotify(false); botFoldout.SetValueWithoutNotify(true);
                nextBatchDisplay = 0;
                EditorApplication.update -= BatchTick; EditorApplication.update += BatchTick;
            }
            catch (Exception error) { batchError = "반복 시험 시작 불가: " + error.Message; }
            finally { if (saved != null) UnityEngine.Object.DestroyImmediate(saved); }
            UpdateBotControls();
        }

        /// <summary>재개는 명시적 버튼에서만 수행한다. 탭 복귀나 Editor 갱신으로 재개하지 않는다.</summary>
        private void ToggleBatchPause()
        {
            if (!visible || batchSession?.CanContinue != true) return;
            if (batchSession.Record.status == BotBatchStatus.Paused) batchSession.Resume(); else batchSession.Pause();
            EditorApplication.update -= BatchTick;
            if (batchSession.NeedsAdvance) EditorApplication.update += BatchTick;
            RefreshBatchBoard();
            UpdateBotControls();
        }

        private void PauseBatch()
        {
            batchSession?.Pause(); EditorApplication.update -= BatchTick;
            if (batchSession != null) RefreshBatchBoard();
            UpdateBatchControls();
        }

        /// <summary>8ms마다 Editor에 제어를 돌려준다. 시간은 작업 양보에만 쓰며 전략 결과를 결정하지 않는다.</summary>
        private void BatchTick()
        {
            if (!visible) { PauseBatch(); return; }
            if (batchSession?.NeedsAdvance != true) { EditorApplication.update -= BatchTick; return; }
            Stopwatch budget = Stopwatch.StartNew();
            do { batchSession.Advance(); } while (batchSession.NeedsAdvance && budget.Elapsed.TotalMilliseconds < 8);
            // 한 처리 단위가 8ms보다 길 수 있다. 중간에 전략을 잘라 결과를 바꾸지 않는다.
            // UI는 초당 최대 10회만 갱신해 반복 시험의 실행 시간을 화면 생성에 소모하지 않는다.
            if (EditorApplication.timeSinceStartup >= nextBatchDisplay || !batchSession.NeedsAdvance)
            {
                nextBatchDisplay = EditorApplication.timeSinceStartup + 0.1;
                RefreshBatchBoard();
                UpdateBotControls(); Owner?.Repaint();
            }
            if (!batchSession.NeedsAdvance) EditorApplication.update -= BatchTick;
        }

        /// <summary>화면 갱신 간격 사이에 멈추더라도 실제로 멈춘 보드를 표시한다.</summary>
        private void RefreshBatchBoard()
        {
            CurrentState = batchSession.Current?.State;
            if (CurrentState == null) { grid.Clear(); return; }
            seed = batchSession.Current.Seed; inputFingerprint = batchSession.Record.fingerprint;
            DisplayState(CurrentState);
        }

        /// <summary>창 종료·재로드 전에 완료 판을 보존하고 살아 있는 실행 객체와 구독을 정리한다.</summary>
        private void ClearBatch()
        {
            EditorApplication.update -= BatchTick;
            if (batchSession != null)
            {
                batchSession.Dispose(); previousBatch = batchSession.Record; batchSession = null;
            }
        }

        /// <summary>일부 실행과 오류를 성공률로 뭉개지 않고 간단한 수량만 보여준다.</summary>
        private void UpdateBatchControls()
        {
            if (batchSummary == null) return;
            bool running = batchSession?.CanContinue == true;
            bool singleBusy = botSession?.NeedsAdvance == true;
            batchNew.SetEnabled(!running && !singleBusy && level != null && batchCount.value >= 1 && batchCount.value <= BotBatchSession.MaximumCount);
            batchRepeat.SetEnabled(!running && !singleBusy && previousBatch != null);
            batchCount.SetEnabled(!running); batchPause.SetEnabled(running); batchStop.SetEnabled(running);
            batchPause.text = batchSession?.Record.status == BotBatchStatus.Paused ? "재개" : "일시정지";
            BotBatchRecord record = batchSession?.Record ?? previousBatch;
            if (record == null)
            {
                batchProgress.value = 0; batchProgress.title = "반복 시험 대기";
                batchSummary.text = batchError ?? "기본·계획 각 100회 · 총 200판. 결과는 프로젝트 로컬에 저장됩니다.";
                return;
            }
            string state = record.status switch {
                BotBatchStatus.Running => "실행 중", BotBatchStatus.Paused => "일시정지", BotBatchStatus.Completed => "완료",
                BotBatchStatus.Stopped => "사용자 중지 · 일부 실행", BotBatchStatus.Interrupted => "종료로 중단 · 일부 실행", _ => "오류 · 일부 실행" };
            batchProgress.value = 100f * record.finished / record.Total;
            batchProgress.title = $"{state} · 정상 완료 {record.finished}/{record.Total}판";
            string current = batchSession?.Current == null ? "" : $"\n현재 {record.Recorded + 1}판 · {(batchSession.Current.Strategy == BotStrategyKind.Basic ? "기본" : "계획")} · {batchSession.Current.Message}";
            string changed = !LevelEditorInputIdentity.Matches(level, record.fingerprint) ? "\n현재 편집 내용과 다른 저장 사본의 시험입니다." : "";
            string versions = BotBatchStore.SameVersions(record) ? "" : "\n현재 규칙 버전과 다른 과거 결과입니다.";
            batchSummary.text = $"기본 {record.basicFinished}/{record.seeds.Length} · 계획 {record.planningFinished}/{record.seeds.Length}\n" +
                $"성공 {record.won} · 이동 소진 {record.exhausted} · 막힘 {record.blocked} · 오류 {record.errors} · 중단 {record.stopped} · 미실행 {record.Unrun}" +
                (record.Recorded % 2 != 0 ? " · 미완성 비교 쌍 1개" : "") + current + changed + versions +
                (string.IsNullOrEmpty(batchError) ? "" : "\n" + batchError) +
                (record.status == BotBatchStatus.Error ? "\n" + record.message : "");
            if (batchSession == null) return;
            rootVisualElement.Q<Label>("initial-boundary").text = "반복 시험 · " + state + " · 수동 입력 잠금";
            manualSummary.text = CurrentState == null ? "다음 판 준비 또는 반복 시험 종료" : $"시험 사본 레벨 {CurrentState.LevelNumber} · 남은 이동 {CurrentState.MovesRemaining}";
            rootVisualElement.Q("initial-inspector").style.display = DisplayStyle.None;
            rootVisualElement.Q("initial-diagnostics").style.display = DisplayStyle.None;
            rootVisualElement.Q("booster-toolbar").style.display = DisplayStyle.None;
            executionInfo.style.display = DisplayStyle.None;
        }
    }
}
