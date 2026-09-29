using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AutoPlay;
using Simulation;
using UnityEditor;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>횟수 입력 없이 1~100회 시험을 시작하고 실측 추천을 보여주는 편집기 화면.</summary>
    internal sealed class BotMoveBalancePanel : IDisposable
    {
        private readonly Func<LevelDefinition> source;
        private readonly Func<bool> otherBusy;
        private readonly Action prepare, changed;
        private readonly Button start, pause, stop;
        private readonly Label progress, recommendations, rows;
        private BotMoveBalanceRecord previous;
        private BotMoveBalanceReader loading;
        private bool visible;
        private double nextDisplay;
        private string errorMessage;
        internal BotMoveBalanceStore Store = new BotMoveBalanceStore();
        internal BotMoveBalanceSession Session { get; private set; }
        internal bool CanContinue => Session?.CanContinue == true;
        internal Foldout Root { get; }

        /// <param name="source">현재 편집 레벨 조회.</param><param name="otherBusy">다른 봇 시험 점유 여부.</param>
        /// <param name="prepare">시험 전 수동/기존 종료 객체 정리.</param><param name="changed">부모 입력 잠금 갱신.</param>
        internal BotMoveBalancePanel(Func<LevelDefinition> source, Func<bool> otherBusy, Action prepare, Action changed)
        {
            this.source = source; this.otherBusy = otherBusy; this.prepare = prepare; this.changed = changed;
            Root = new Foldout { text = "이동 횟수별 밸런스 시험", name = "move-balance", value = false };
            Root.style.flexShrink = 0; Root.style.marginTop = 4;
            Label guide = new Label("1~100회 × 기본·계획 각 100판 = 총 20,000판 · 원본은 바꾸지 않습니다.");
            guide.style.whiteSpace = WhiteSpace.Normal; Root.Add(guide);
            LevelEditorHelp.Link(guide, "이동 횟수만 바꾼 사본을 실제 봇으로 시험합니다. 결과를 보고 원본의 이동 횟수를 직접 정하세요.", "difficulty.html#move-balance");
            VisualElement buttons = new VisualElement(); buttons.style.flexDirection = FlexDirection.Row; buttons.style.flexWrap = Wrap.Wrap; Root.Add(buttons);
            start = new Button(Start) { text = "새 밸런스 시험", name = "balance-start", tooltip = "현재 편집 내용의 사본으로 1~100회를 전부 시험합니다. 새 시드 100개를 모든 횟수에 동일하게 사용합니다." };
            pause = new Button(() => { Session?.SetPaused(Session.Record.status != BotBatchStatus.Paused); Refresh(); }) { text = "일시정지", name = "balance-pause" };
            stop = new Button(() => { Session?.Stop(); Refresh(); }) { text = "중지", name = "balance-stop", tooltip = "완료된 횟수의 결과와 판별 기록은 보존합니다. 미완료 횟수는 추천하지 않습니다." };
            buttons.Add(start); buttons.Add(pause); buttons.Add(stop);
            buttons.Add(new Button(() => {
                BotMoveBalanceRecord record = Session?.Record ?? previous;
                if (record != null) EditorUtility.RevealInFinder(Path.GetFullPath(Path.Combine(Store.Root, record.id)));
            }) { text = "결과 폴더", name = "balance-folder", tooltip = "프로젝트 로컬의 요약 JSON과 실제 판 기록을 엽니다. Library 삭제 시 함께 지워집니다." });
            progress = new Label { name = "balance-progress" }; progress.style.whiteSpace = WhiteSpace.Normal; Root.Add(progress);
            recommendations = new Label { name = "balance-recommendations" }; recommendations.style.whiteSpace = WhiteSpace.Normal; Root.Add(recommendations);
            Foldout detail = new Foldout { text = "횟수별 성공률과 판단 근거", value = false, name = "balance-detail" }; Root.Add(detail);
            ScrollView scroll = new ScrollView(); scroll.style.maxHeight = 140; detail.Add(scroll);
            rows = new Label { name = "balance-rows" }; rows.style.whiteSpace = WhiteSpace.Normal; scroll.Add(rows);
            try
            {
                loading = Store.OpenReader(); previous = loading.Record;
                if (loading.IsDone) loading = null;
                else EditorApplication.update += ReadTick;
            }
            catch (Exception error) { errorMessage = "기존 결과를 읽지 못했습니다: " + error.Message; }
            Refresh();
        }

        private void Start()
        {
            if (!visible || CanContinue || otherBusy() || source() == null) return;
            try
            {
                EditorApplication.update -= ReadTick; loading = null;
                Session?.Dispose(); Session = null; errorMessage = null; prepare();
                Session = new BotMoveBalanceSession(source(), Store); previous = Session.Record;
                EditorApplication.update -= Tick; EditorApplication.update += Tick;
            }
            catch (Exception error) { errorMessage = "시험 시작 불가: " + error.Message; }
            Refresh();
        }

        /// <param name="value">탭 표시 여부. 탭 복귀로 시험을 자동 재개하지 않는다.</param>
        internal void SetVisible(bool value)
        {
            visible = value;
            if (!value && Session?.NeedsAdvance == true) Session.SetPaused(true);
            Refresh();
        }

        /// <summary>실행 단위 사이에 Editor에 제어를 돌려 일시정지와 중지 입력을 받을 수 있게 한다.</summary>
        private void Tick()
        {
            if (!CanContinue) { EditorApplication.update -= Tick; Refresh(); return; }
            if (!visible || Session?.NeedsAdvance != true) return;
            Stopwatch budget = Stopwatch.StartNew();
            do { Session.Advance(); } while (Session.NeedsAdvance && budget.Elapsed.TotalMilliseconds < 8);
            if (EditorApplication.timeSinceStartup < nextDisplay && Session.NeedsAdvance) return;
            nextDisplay = EditorApplication.timeSinceStartup + .25; Refresh();
        }

        /// <summary>저장 결과를 UI 갱신 사이에 나누어 검증한다. 검증 중·실패 시에는 추천을 표시하지 않는다.</summary>
        private void ReadTick()
        {
            if (loading == null) { EditorApplication.update -= ReadTick; return; }
            if (!visible) return;
            Stopwatch budget = Stopwatch.StartNew();
            do { loading.Advance(); } while (!loading.IsDone && budget.Elapsed.TotalMilliseconds < 8);
            if (loading.IsDone)
            {
                if (loading.Error != null) { errorMessage = "기존 결과를 읽지 못했습니다: " + loading.Error; previous = null; }
                loading = null; EditorApplication.update -= ReadTick; Refresh();
            }
            else if (EditorApplication.timeSinceStartup >= nextDisplay)
            { nextDisplay = EditorApplication.timeSinceStartup + .25; Refresh(); }
        }

        /// <summary>다른 실행기의 상태만 반영한다. 부모 갱신을 다시 호출하지 않아 순환 갱신을 막는다.</summary>
        internal void RefreshEnabled()
        {
            start.SetEnabled(visible && !CanContinue && !otherBusy() && source() != null);
            pause.SetEnabled(visible && CanContinue); stop.SetEnabled(CanContinue);
            pause.text = Session?.Record.status == BotBatchStatus.Paused ? "재개" : "일시정지";
        }

        private void Refresh()
        {
            RefreshEnabled(); changed();
            BotMoveBalanceRecord record = Session?.Record ?? previous;
            if (record == null) { progress.text = errorMessage ?? "시험 대기"; recommendations.text = errorMessage == null ? "시험 전 · 네 난이도의 이동 횟수를 실제 플레이로 찾습니다." : "기록 확인 실패 · 추천을 표시하지 않습니다."; rows.text = ""; return; }
            if (loading != null)
            {
                progress.text = $"저장 기록 확인 중 · {loading.CheckedTrials}/{record.trials.Count}개 완료 구간";
                recommendations.text = "원시 판 검증을 마친 뒤 추천을 표시합니다."; rows.text = ""; return;
            }
            bool compatible = record.rulesVersion == BotMoveRecommendations.Version && record.engineVersion == BotMoveRecommendations.ExecutionVersion;
            bool complete = record.status == BotBatchStatus.Completed;
            int finished = record.trials.Count * 200 + (Session?.Current?.Record.finished ?? 0);
            progress.text = $"{record.sourceName} · {(complete ? "전체 완료" : "중간 결과")} · {record.trials.Count}/100개 횟수 · {finished:N0}/20,000판\n{record.message}";
            if (errorMessage != null) progress.text += "\n" + errorMessage;
            if (source() == null || LevelStateBuilder.Fingerprint(source()) != record.fingerprint)
                progress.text += "\n현재 편집 내용과 다른 사본의 결과입니다.";
            recommendations.text = compatible ? string.Join("\n", Enumerable.Range(0, 4).Select(grade => {
                string range = BotMoveRecommendations.Ranges(record.trials, grade);
                return BotMoveRecommendations.Titles[grade] + " : " + (range.Length > 0 ? range : complete ? "추천 없음 (1~100회 시험 결과)" : "추가 시험 필요");
            })) : "현재 실행·평가 버전과 다른 기록입니다. 새 밸런스 시험을 진행하세요.";
            rows.text = "기준: 높은 쪽 봇 성공률 · 쉬움 ≥60% / 보통 ≥40% / 어려움 ≥20% / 매우 어려움 >0%\n" +
                "성공 0%·오류·미완료는 추천 제외. 인간 체감 난이도는 별도 확인이 필요합니다.\n" +
                string.Join("\n", record.trials.Select(t => $"{t.moves}회 : 기본 {t.basicWon}/100 · 계획 {t.planningWon}/100 · " +
                    (!compatible ? "평가 버전 다름 · 등급 표시 안 함" : t.grade < 0 ? "보류 · " + t.reason : BotMoveRecommendations.Titles[t.grade])));
        }

        public void Dispose()
        {
            EditorApplication.update -= ReadTick; loading = null;
            EditorApplication.update -= Tick; Session?.Dispose(); Session = null;
        }
    }
}
