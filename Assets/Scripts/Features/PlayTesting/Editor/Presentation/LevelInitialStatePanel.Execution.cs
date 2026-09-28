using System.Linq;
using Board;
using Simulation;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelInitialStatePanel
    {
        private BoardActionExecutor execution;
        private BoardCoordinate? firstInput, secondInput;
        private Toggle executionToggle;
        private Button executeButton, activateButton, settleButton, cancelSelection;
        private Label executionInfo;
        internal BoardActionExecutor Execution => execution;

        private void CreateExecutionUI(VisualElement root)
        {
            VisualElement toolbar = new VisualElement { name = "execution-toolbar" }; root.Add(toolbar);
            executionToggle = new Toggle("실행 시험") { name = "execution-mode" }; toolbar.Add(executionToggle);
            executionToggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue && startingMode && LastSearch?.Status == StartingBoardStatus.Success && CurrentState != null)
                {
                    execution = new BoardActionExecutor(CurrentState, SelectedBoosters()); firstInput = secondInput = null;
                    DisplayState(execution.State);
                    executionInfo.text = "두 칸으로 교환/파워 조합, 한 칸으로 제자리 발동. 회수 부품·도착·공급과 관련 미션 지원.";
                }
                else
                {
                    ClearExecution();
                    if (CurrentState != null) DisplayState();
                }
                UpdateExecutionControls();
            });
            executeButton = new Button(ExecuteSwap) { name = "execution-swap", text = "교환 실행" }; toolbar.Add(executeButton);
            activateButton = new Button(() => ExecuteAction(true)) { name = "execution-activate", text = "제자리 발동" }; toolbar.Add(activateButton);
            settleButton = new Button(ExecuteSettlement) { name = "execution-settle", text = "낙하·공급 실행" }; toolbar.Add(settleButton);
            CreateCascadeUI(toolbar);
            cancelSelection = new Button(() =>
            {
                firstInput = secondInput = null;
                foreach (Button cell in grid.Query<Button>().ToList()) cell.RemoveFromClassList("execution-input");
                executionInfo.text = "선택을 취소했습니다. 출발 칸 → 도착 칸 순서로 선택하세요."; UpdateExecutionControls();
            }) { name = "execution-cancel", text = "선택 취소" }; toolbar.Add(cancelSelection);
            toolbar.Add(new Button(Build) { name = "execution-reset", text = "같은 시드로 초기화" });
            executionInfo = new Label { name = "execution-info" }; root.Add(executionInfo);
            CreateItemsUI(root);
        }

        private void ClearExecution()
        {
            StopCascadeRun();
            selectedItem = null; itemFirst = null; ClearItemHighlights();
            if (boosterChoices != null) foreach (Toggle toggle in boosterChoices) toggle.SetValueWithoutNotify(false);
            execution = null; firstInput = secondInput = null;
            executionToggle?.SetValueWithoutNotify(false);
            executionToggle?.SetEnabled(false); executeButton?.SetEnabled(false); cancelSelection?.SetEnabled(false);
            activateButton?.SetEnabled(false);
            settleButton?.SetEnabled(false);
            automaticButton?.SetEnabled(false); cascadeButton?.SetEnabled(false);
            skipLastPangButton?.SetEnabled(false);
            if (skipLastPangButton != null) skipLastPangButton.style.display = DisplayStyle.None;
            if (executionInfo != null) executionInfo.text = "실행 시험은 시작 조건 구성 성공 후 사용할 수 있습니다.";
            UpdateItemControls();
            UpdateManualControls();
        }

        private void UpdateExecutionControls()
        {
            executionToggle?.SetEnabled(startingMode && LastSearch?.Status == StartingBoardStatus.Success && CurrentState != null);
            bool ready = !selectedItem.HasValue && !cascadeRunning && execution != null && execution.Phase == BoardActionPhase.Ready;
            executeButton?.SetEnabled(ready && firstInput.HasValue && secondInput.HasValue);
            activateButton?.SetEnabled(ready && firstInput.HasValue && !secondInput.HasValue);
            cancelSelection?.SetEnabled(ready && firstInput.HasValue);
            settleButton?.SetEnabled(!cascadeRunning && execution != null && execution.Phase == BoardActionPhase.WaitingForFall);
            automaticButton?.SetEnabled(!cascadeRunning && execution != null && (execution.Phase == BoardActionPhase.WaitingForAutomaticMatch || execution.Phase == BoardActionPhase.WaitingForLastPang));
            if (automaticButton != null) automaticButton.text = execution?.Phase == BoardActionPhase.WaitingForLastPang ? "라스트팡 단계" : "자동 매칭 단계";
            skipLastPangButton?.SetEnabled(execution?.IsLastPang == true);
            if (skipLastPangButton != null) skipLastPangButton.style.display = execution?.IsLastPang == true ? DisplayStyle.Flex : DisplayStyle.None;
            cascadeButton?.SetEnabled(execution != null && execution.HasPendingCascade);
            if (cascadeButton != null) cascadeButton.text = cascadeRunning ? "연쇄 실행 중지" : "연쇄 끝까지";
            UpdateItemControls();
            UpdateManualControls();
        }

        private void SelectExecutionCell(BoardCoordinate coordinate)
        {
            if (selectedItem.HasValue) { SelectItemCell(coordinate); return; }
            if (cascadeRunning || execution == null || execution.Phase != BoardActionPhase.Ready) return;
            if (!firstInput.HasValue || secondInput.HasValue) { firstInput = coordinate; secondInput = null; }
            else if (firstInput.Value.Equals(coordinate)) { firstInput = secondInput = null; }
            else secondInput = coordinate;
            foreach (Button cell in grid.Query<Button>().ToList()) cell.RemoveFromClassList("execution-input");
            if (firstInput.HasValue) grid.Q<Button>($"initial-cell-{firstInput.Value.Row}-{firstInput.Value.Column}")?.AddToClassList("execution-input");
            if (secondInput.HasValue) grid.Q<Button>($"initial-cell-{secondInput.Value.Row}-{secondInput.Value.Column}")?.AddToClassList("execution-input");
            executionInfo.text = firstInput.HasValue ? "출발 " + firstInput + " → 도착 " + (secondInput.HasValue ? secondInput.ToString() : "선택 대기") : "출발 칸을 선택하세요.";
            UpdateExecutionControls();
        }

        private void ExecuteSwap()
            => ExecuteAction(false);

        private void ExecuteAction(bool activation)
        {
            // 지연된 원본 변경 감지보다 실행 버튼이 먼저 눌려도 이전 입력을 실행하지 않는다.
            CheckInput();
            if (selectedItem.HasValue) return;
            if (execution == null || !firstInput.HasValue || (!activation && !secondInput.HasValue)) return;
            BoardActionResult result = activation ? execution.Activate(firstInput.Value) : execution.Swap(firstInput.Value, secondInput.Value);
            executionInfo.text = result.Message;
            if (!result.IsApplied)
            {
                if (execution.Outcome != null)
                {
                    rootVisualElement.Q<Label>("initial-boundary").text = EndingSummary();
                    details.text = EndingSummary();
                }
                UpdateExecutionControls(); return;
            }
            firstInput = secondInput = null;
            DisplayState(execution.State);
            rootVisualElement.Q<Label>("initial-boundary").text = "실행 시험 · 낙하 대기 · 연쇄 완료 후 종료 판정";
            queryResults.Clear();
            rootVisualElement.Q<Foldout>("query-foldout").style.display = DisplayStyle.None;
            querySummary.text = "매칭 " + result.Changes.Count + " · 파워 생성 " + result.Changes.Count(change => change.IsTransformation) +
                " · 발동 " + result.Effects.Count(effect => effect.Response == DamageResponse.Activate) + " · 남은 이동 " + result.MovesAfter;
            executionInfo.text = result.Message + " · 낙하·공급 실행 가능 / 연쇄 완료 후 종료 판정";
            foreach (MatchedBlockChange change in result.Changes)
                grid.Q<Button>($"initial-cell-{change.Coordinate.Row}-{change.Coordinate.Column}")?.AddToClassList(change.IsTransformation ? "execution-created" : "execution-removed");
            foreach (EffectRecord effect in result.Effects)
                if (effect.Response == DamageResponse.Damage || effect.Response == DamageResponse.Remove || effect.Response == DamageResponse.Activate)
                    grid.Q<Button>($"initial-cell-{effect.Target.Row}-{effect.Target.Column}")?.AddToClassList("execution-removed");
            details.text = "입력 " + result.First + (result.IsActivation ? " 제자리 발동" : " → " + result.Second) + "\n이동 " + result.MovesBefore + " → " + result.MovesAfter +
                "\n난수 " + result.RandomBefore + " → " + result.RandomAfter + "\n" + string.Join("\n", result.Decisions.Select(decision =>
                    (decision.Selected.Kind switch { MatchKind.Magnet => "무지개 자석", MatchKind.Bomb => "달 폭탄", MatchKind.Rocket => "청소로켓", MatchKind.Drone => "수거 드론", _ => "3매칭" }) + " / " + decision.Reason + "\n선택: " + string.Join(", ", decision.Selected.Cells) +
                    (decision.Spawn.HasValue ? "\n생성: " + decision.Spawn + " / 수 " + result.Turn : "") +
                    "\n제외 패턴: " + decision.Excluded.Count));
            overview.text = "제거/변환 기록\n" + string.Join("\n", result.Changes.Select(change => change.Coordinate + " 토" + ((int)change.OriginalColor + 1) + " → " +
                (!change.IsConsumed ? "내용물 보존 / 거미줄 " + change.CoverBefore + " → " + change.CoverAfter :
                (change.ResultContent switch { RuntimeContent.Rocket => "청소로켓", RuntimeContent.Bomb => "달 폭탄", RuntimeContent.Drone => "수거 드론", RuntimeContent.Magnet => "무지개 자석", _ => "제거" })))) +
                "\n\n효과 기록\n" + string.Join("\n", result.Effects.Select(effect => "타격 " + effect.HitGroup + " · " + effect.Source + " → " + effect.Target + " " +
                    (effect.Cause == DamageCause.AdjacentMatch ? "인접 매칭 / " : effect.Cause == DamageCause.MagnetAdjacent ? "자석 색 제거 인접 / " : "파워 / ") +
                    (effect.Response == DamageResponse.Activate ? (effect.Content switch { RuntimeContent.Rocket => "청소로켓 / ", RuntimeContent.Bomb => "달폭탄 / ", RuntimeContent.Drone => "수거드론 / ", _ => "무지개 자석 / " }) : "") + effect.Message +
                    (effect.Response == DamageResponse.Damage ? " " + effect.DurabilityBefore + " → " + effect.DurabilityAfter : "") +
                    (effect.CoverBefore != effect.CoverAfter ? " / 거미줄 " + effect.CoverBefore + " → " + effect.CoverAfter : "") +
                    (effect.DustBefore != effect.DustAfter ? " / 먼지 " + effect.DustBefore + " → " + effect.DustAfter : ""))) +
                "\n\n지원 미션 집계 완료. 새 매칭 파워는 이번 턴 피격으로부터 보호됩니다.";
            ShowTargeting();
            UpdateExecutionControls();
            BeginManualCascade();
        }
    }
}
