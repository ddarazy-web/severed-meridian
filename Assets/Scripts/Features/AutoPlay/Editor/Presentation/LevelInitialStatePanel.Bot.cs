using System;
using System.Collections.Generic;
using System.Linq;
using AutoPlay;
using Simulation;
using UnityEditor;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    /// <summary>기존 플레이 테스트 보드를 재사용하되 봇 실행과 수동 실행의 소유권을 분리한다.</summary>
    public sealed partial class LevelInitialStatePanel
    {
        private BotPlaySession botSession;
        private Foldout botFoldout;
        private Label botProgress, botReason;
        private Button botNew, botStep, botRun, botStop, botRepeat;
        private PopupField<string> botStrategy;
        private int? botReplaySeed;
        private string botReplayFingerprint;
        private bool botRepeating;
        private double nextBotTick;

        /// <summary>수동 플레이 탭에만 봇 시험 영역을 붙인다. 별도 창이나 통계 화면은 만들지 않는다.</summary>
        private void CreateBotUI()
        {
            if (!manualMode) return;
            botFoldout = new Foldout { name = "bot-trial", text = "봇 시험", value = false };
            botFoldout.Add(new Label("새 봇 시험은 수동 시험을 끝내고 별도 사본으로 시작합니다. 아이템·부스터는 사용하지 않습니다.") { name = "bot-guide" });
            botStrategy = new PopupField<string>("전략", new List<string> { "기본", "계획" }, 0) { name = "bot-strategy",
                tooltip = "기본은 당장의 공개 기여, 계획은 가정 보드의 두 수를 비교합니다. 변경 뒤 새 시험 또는 같은 조건으로 다시를 누르세요." };
            botStrategy.RegisterValueChangedCallback(_ => UpdateBotControls()); botFoldout.Add(botStrategy);
            VisualElement buttons = new VisualElement { name = "bot-buttons" }; botFoldout.Add(buttons);
            botNew = new Button(StartBot) { name = "bot-new", text = "새 봇 시험", tooltip = "현재 편집 내용으로 새 사본과 새 시드를 만듭니다. 원본 파일은 바뀌지 않습니다." };
            botStep = new Button(() => RunBot(false)) { name = "bot-step", text = "한 수 진행", tooltip = "한 행동을 선택하고 낙하·연쇄가 끝날 때까지 진행합니다." };
            botRun = new Button(() => RunBot(true)) { name = "bot-run", text = "한 판 실행", tooltip = "공개 정보로 행동을 선택하며 성공·패배·오류까지 진행합니다." };
            botStop = new Button(StopBot) { name = "bot-stop", text = "중지", tooltip = "현재 행동의 낙하·연쇄를 마치고 멈춥니다." };
            buttons.Add(botNew); buttons.Add(botStep); buttons.Add(botRun); buttons.Add(botStop);
            botRepeat = new Button(() => PrepareBot(true)) { name = "bot-repeat", text = "같은 조건으로 다시",
                tooltip = "같은 시작 레벨·시드로 선택한 전략을 새로 시험합니다. 원본 수정이나 창 재로드 후에는 사용할 수 없습니다." };
            buttons.Add(botRepeat);
            buttons.Add(new Button(() => Build()) { name = "bot-manual", text = "수동 시험 새로", tooltip = "봇 시험을 끝내고 새 수동 시험을 준비합니다." });
            botProgress = new Label("새 봇 시험을 눌러 준비하세요.") { name = "bot-progress" };
            botReason = new Label("점수는 선택 이유이며 실제 미래 결과나 난이도가 아닙니다.") { name = "bot-reason" };
            botFoldout.Add(botProgress); botFoldout.Add(botReason);
            rootVisualElement.Insert(5, botFoldout);
            CreateBatchUI();
            UpdateBotControls();
        }

        /// <summary>명시적인 새 시험에서만 수동 실행을 정리하고 봇 전용 사본을 만든다.</summary>
        private void StartBot()
            => PrepareBot(false);

        /// <param name="repeat">직전 시작 조건의 시드를 재사용할지 여부. 원본 변경 시 재사용하지 않는다.</param>
        private void PrepareBot(bool repeat)
        {
            CheckInput();
            if (level == null || !visible || botSession?.NeedsAdvance == true || batchSession?.CanContinue == true) return;
            string fingerprint = LevelStateBuilder.Fingerprint(level);
            if (repeat && (!botReplaySeed.HasValue || botReplayFingerprint != fingerprint)) return;
            int next = botReplaySeed ?? seed;
            if (!repeat) do { next = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0); } while (next == seed);
            ClearBatch(); Invalidate("수동 시험 종료 · 새 봇 시험 준비");
            seed = next; seedField.SetValueWithoutNotify(seed);
            botReplaySeed = seed; botReplayFingerprint = inputFingerprint = fingerprint;
            botRepeating = repeat;
            botSession = new BotPlaySession(level, seed, botStrategy.value == "계획" ? BotStrategyKind.Planning : BotStrategyKind.Basic);
            manualDiagnostics.SetValueWithoutNotify(false);
            botFoldout.SetValueWithoutNotify(true);
            EditorApplication.update -= BotTick;
            EditorApplication.update += BotTick;
            nextBotTick = 0;
            UpdateBotControls();
        }

        /// <param name="wholeGame">한 판 실행이면 true, 한 수만 진행하면 false.</param>
        private void RunBot(bool wholeGame)
        {
            CheckInput();
            if (!visible || botSession == null || !botSession.Begin(wholeGame)) return;
            nextBotTick = 0;
            EditorApplication.update -= BotTick;
            EditorApplication.update += BotTick;
            UpdateBotControls();
        }

        /// <summary>탭 이탈도 같은 안전 중지를 사용한다. 복귀는 실행 예약을 다시 만들지 않는다.</summary>
        private void StopBot()
        {
            botSession?.RequestStop();
            if (botSession?.NeedsAdvance != true) EditorApplication.update -= BotTick;
            UpdateBotControls();
        }

        /// <summary>Editor를 막지 않도록 갱신마다 공통 실행 한 단계만 처리하고 보드를 표시한다.</summary>
        private void BotTick()
        {
            if (EditorApplication.timeSinceStartup < nextBotTick) return;
            nextBotTick = EditorApplication.timeSinceStartup + (botSession?.IsPlanning == true ? 0.005 : 0.05);
            CheckInput();
            if (botSession == null) { EditorApplication.update -= BotTick; return; }
            botSession.Advance();
            bool changed = !ReferenceEquals(CurrentState, botSession.State);
            CurrentState = botSession.State;
            if (changed && CurrentState != null) DisplayState(CurrentState);
            UpdateBotControls();
            if (!botSession.NeedsAdvance) EditorApplication.update -= BotTick;
            Owner?.Repaint();
        }

        /// <summary>원본 변경·레벨 전환·창 종료 시 오래된 관찰과 갱신 구독을 즉시 폐기한다.</summary>
        private void ClearBot()
        {
            EditorApplication.update -= BotTick;
            botSession?.Dispose(); botSession = null;
            botReplaySeed = null; botReplayFingerprint = null;
            botRepeating = false;
            if (botProgress != null) botProgress.text = "새 봇 시험을 눌러 준비하세요.";
            if (botReason != null) botReason.text = "점수는 선택 이유이며 실제 미래 결과나 난이도가 아닙니다.";
            UpdateBotControls();
        }

        /// <summary>실행 소유자에 맞춰 수동 조작을 잠그고 한국어 상태·평가 이유를 갱신한다.</summary>
        private void UpdateBotControls()
        {
            if (botFoldout == null) return;
            bool active = botSession != null || batchSession != null;
            // 부모를 잠그므로 기존 수동 갱신 코드가 자식 버튼을 켜도 입력이 다시 열리지 않는다.
            rootVisualElement.Q("manual-toolbar")?.SetEnabled(!active);
            rootVisualElement.Q("manual-input")?.SetEnabled(!active);
            manualDiagnostics?.SetEnabled(!active);
            rootVisualElement.Q("item-toolbar")?.SetEnabled(!active);
            rootVisualElement.Q("booster-toolbar")?.SetEnabled(!active);
            // 봇에서 쓸 수 없는 수동 메뉴와 부스터 안내를 숨겨 모드 혼동을 줄이고
            // 보드 높이를 확보한다. 수동 시험으로 돌아가면 기존 표시를 복구한다.
            foreach (string name in new[] { "manual-toolbar", "manual-input", "manual-diagnostics", "item-toolbar", "booster-status" })
            {
                VisualElement element = rootVisualElement.Q(name);
                if (element != null) element.style.display = active ? DisplayStyle.None : DisplayStyle.Flex;
            }
            rootVisualElement.Q("initial-toolbar")?.SetEnabled(batchSession == null);
            rootVisualElement.Q("bot-buttons")?.SetEnabled(batchSession?.CanContinue != true);
            botProgress.style.display = batchSession == null ? DisplayStyle.Flex : DisplayStyle.None;
            botReason.style.display = batchSession == null ? DisplayStyle.Flex : DisplayStyle.None;
            botNew.SetEnabled(level != null && botSession?.NeedsAdvance != true);
            botStrategy.SetEnabled(botSession?.NeedsAdvance != true);
            botRepeat.SetEnabled(level != null && botReplaySeed.HasValue && botSession?.NeedsAdvance != true);
            bool sameStrategy = botSession != null && botSession.Strategy == (botStrategy.value == "계획" ? BotStrategyKind.Planning : BotStrategyKind.Basic);
            bool ready = sameStrategy && (botSession.Status == BotSessionStatus.Ready || botSession.Status == BotSessionStatus.Stopped && botSession.State != null);
            botStep.SetEnabled(ready); botRun.SetEnabled(ready);
            botStop.SetEnabled(botSession != null && botSession.NeedsAdvance && botSession.Status != BotSessionStatus.Stopping);
            UpdateBatchControls();
            if (botSession == null) return;
            string phase = botSession.Status switch {
                BotSessionStatus.Preparing => "준비 중", BotSessionStatus.Ready => "입력 대기", BotSessionStatus.Running => "실행 중",
                BotSessionStatus.Stopping => "중지 대기", BotSessionStatus.Stopped => "사용자 중지", BotSessionStatus.Won => "성공",
                BotSessionStatus.MovesExhausted => "이동 수 소진", BotSessionStatus.Blocked => "진행 불가",
                BotSessionStatus.Error => "실행 오류", _ => "시험 종료" };
            botProgress.text = $"현재 판: {(botSession.Strategy == BotStrategyKind.Planning ? "계획" : "기본")} · {(botRepeating ? "직전과 같은 시작 조건" : "새 시작 조건")} · {phase} · 완료한 행동 {botSession.Records.Count}회 · 시드 {seed}\n{botSession.Message}" +
                (sameStrategy ? "" : "\n선택한 전략으로 새 시험 또는 같은 조건 재시험을 시작하세요.");
            BotChoice choice = botSession.LastChoice;
            botReason.text = choice == null ? "아직 선택한 행동이 없습니다. 봇은 화면에 공개된 값만 읽습니다." :
                $"최근 선택: {choice.Action.First}" + (choice.Action.Second.HasValue ? $" ↔ {choice.Action.Second}" : " 제자리 발동") + "\n" + choice.Reason;
            rootVisualElement.Q<Label>("initial-boundary").text = "봇 시험 · " + phase + " · 수동 입력 잠금";
            manualSummary.text = CurrentState == null ? "봇 전용 사본을 준비하고 있습니다." :
                $"레벨 {CurrentState.LevelNumber} · 남은 이동 {CurrentState.MovesRemaining}\n" +
                string.Join("  /  ", CurrentState.Missions.Select(m => $"{LevelMissionRules.Name(m.Definition.Kind)}" +
                    (m.Definition.Kind == MissionKind.Color ? " 토" + ((int)m.Definition.Color + 1) : "") + $": {m.Progress}/{m.Target}"));
            rootVisualElement.Q("initial-inspector").style.display = DisplayStyle.None;
            rootVisualElement.Q("initial-diagnostics").style.display = DisplayStyle.None;
            rootVisualElement.Q("booster-toolbar").style.display = DisplayStyle.None;
            executionInfo.style.display = DisplayStyle.None;
        }
    }
}
