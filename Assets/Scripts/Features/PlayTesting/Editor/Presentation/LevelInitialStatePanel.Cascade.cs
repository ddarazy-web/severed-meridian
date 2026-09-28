using System.Linq;
using Simulation;
using UnityEditor;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelInitialStatePanel
    {
        private Button automaticButton, cascadeButton;
        private bool cascadeRunning;
        private BoardActionExecutor cascadeOwner;
        internal bool CascadeRunning => cascadeRunning;

        private void CreateCascadeUI(VisualElement toolbar)
        {
            automaticButton = new Button(ExecuteCascadeStep) { name = "execution-automatic", text = "자동 매칭 단계" }; toolbar.Add(automaticButton);
            cascadeButton = new Button(() =>
            {
                CheckInput();
                if (cascadeRunning) { StopCascadeRun(); UpdateExecutionControls(); return; }
                if (execution == null || !execution.HasPendingCascade) return;
                cascadeOwner = execution; cascadeRunning = true;
                EditorApplication.update += CascadeTick; UpdateExecutionControls();
            }) { name = "execution-cascade", text = "연쇄 끝까지" }; toolbar.Add(cascadeButton);
            CreateEndingUI(toolbar);
        }

        private void StopCascadeRun()
        {
            EditorApplication.update -= CascadeTick; cascadeOwner = null; cascadeRunning = false;
            if (cascadeButton != null) cascadeButton.text = "연쇄 끝까지";
        }

        private void CascadeTick()
        {
            // 편집·재시작으로 실행 객체가 교체되면 이전 객체의 연쇄 예약을 취소한다.
            // 같은 레벨 파일이어도 실행 인스턴스가 다르면 이전 예약을 이어 실행하면 안 된다.
            CheckInput();
            if (!cascadeRunning || execution == null || execution != cascadeOwner) { StopCascadeRun(); return; }
            ExecuteCascadeStep();
        }

        private void ExecuteCascadeStep()
        {
            CheckInput(); if (execution == null) return;
            CascadeStepResult result = execution.AdvanceCascade();
            // 매칭·피격·낙하 순서는 공통 실행기가 결정한다. 이 패널은 결과 표시만 맡으며
            // 에디터 전용 매칭 규칙을 만들지 않아 실제 게임과 테스트의 판정이 갈라지지 않게 한다.
            if (!result.IsApplied || !execution.HasPendingCascade) StopCascadeRun();
            if (result.IsApplied) DisplayState(execution.State);
            rootVisualElement.Q<Label>("initial-boundary").text = result.Message;
            executionInfo.text = result.Message;
            querySummary.text = "수 " + execution.Turn + " · 자동 연쇄 " + execution.CascadeRounds + " · 남은 이동 " + execution.State.MovesRemaining +
                (execution.Outcome == null ? "" : " · " + execution.Outcome.Message);
            queryResults.Clear(); rootVisualElement.Q<Foldout>("query-foldout").style.display = DisplayStyle.Flex;
            if (execution.Phase == BoardActionPhase.Ready)
                foreach (ActionCandidate action in ActionQuery.Find(execution.State))
                {
                    var coordinates = action.Second.HasValue ? new[] { action.First, action.Second.Value } : new[] { action.First };
                    queryResults.Add(new Button(() => Highlight(coordinates)) { text = action.Message + " " + action.First, tooltip = "후보 조회입니다. 실제 지원 여부는 실행 시 확인합니다." });
                }
            details.text = result.Message + "\n수 " + result.Turn + " / 연쇄 " + result.Round + "\n난수 " + result.RandomBefore + " → " + result.RandomAfter +
                "\n공급 커서\n" + string.Join("\n", execution.State.Supply.Sources.Select(s => s.Coordinate + " " + s.ItemIndex + ":" + s.ItemConsumed));
            overview.text = "이번 수 연쇄 기록\n" + string.Join("\n\n", execution.CascadeHistory.Select(step =>
                "연쇄 " + step.Round + " · " + step.Message + "\n난수 " + step.RandomBefore + " → " + step.RandomAfter +
                (step.Settlement == null ? "" : "\n이동/공급 " + step.Settlement.Records.Count + " · 남은 빈칸 " + step.Settlement.EmptyCells.Count) +
                string.Concat(step.Decisions.Select(d => "\n" + (d.Selected.Kind switch { MatchKind.Magnet => "무지개 자석", MatchKind.Bomb => "달폭탄", MatchKind.Rocket => "청소로켓", MatchKind.Drone => "수거드론", _ => "3매칭" }) +
                    " · " + d.Reason + "\n선택 " + string.Join(", ", d.Selected.Cells) +
                    "\n제외 패턴 " + d.Excluded.Count + (d.Spawn.HasValue ? "\n생성 " + d.Spawn : ""))) +
                string.Concat(step.Effects.Select(e => "\n타격 " + e.HitGroup + " · " + e.Target + " " + e.Message + " " + e.DurabilityBefore + "→" + e.DurabilityAfter))));
            ShowTargeting(); HighlightProtectedPowers(); UpdateExecutionControls();
        }

        private void HighlightProtectedPowers()
        {
            foreach (RuntimeCell cell in execution.State.Cells.Where(c => execution.TurnEffects.IsProtected(c.Coordinate)))
            {
                Button button = grid.Q<Button>($"initial-cell-{cell.Coordinate.Row}-{cell.Coordinate.Column}");
                button?.RemoveFromClassList("execution-removed"); button?.AddToClassList("execution-created");
            }
        }
    }
}
