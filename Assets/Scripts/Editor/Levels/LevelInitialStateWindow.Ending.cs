using Simulation;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelInitialStateWindow
    {
        private Button skipLastPangButton;
        private void CreateEndingUI(VisualElement toolbar)
        {
            skipLastPangButton = new Button(() =>
            {
                CheckInput(); if (execution == null) return;
                StopCascadeRun();
                CascadeStepResult skipped = execution.SkipLastPang();
                executionInfo.text = skipped.Message;
                if (skipped.IsApplied) DisplayState(execution.State);
                rootVisualElement.Q<Label>("initial-boundary").text = skipped.Message;
                details.text = EndingSummary(); UpdateExecutionControls();
            }) { name = "execution-skip-last-pang", text = "라스트팡 건너뛰기" };
            toolbar.Add(skipLastPangButton);
        }

        private string EndingSummary()
        {
            if (execution == null) return "";
            BoardOutcome outcome = execution.Outcome;
            string text = outcome == null ? "퍼즐 진행 중" :
                (outcome.Kind == BoardOutcomeKind.Aborted ? "실행 중단 · " : "") + outcome.Message + "\n확정 수 " + outcome.Turn + " · 남은 이동 " + outcome.MovesRemaining;
            if (execution.LastShuffle != null)
            {
                string reason = execution.LastShuffle.Reason switch
                {
                    ShuffleReason.Applied => "완료",
                    ShuffleReason.Impossible => "진행 불가",
                    _ => "탐색 한도"
                };
                text += "\n자동 재배치 " + reason + " · 시도 " + execution.LastShuffle.Attempts;
            }
            if (outcome?.Kind == BoardOutcomeKind.Won)
                text += "\n로켓 변환 " + execution.LastPangConversions + " · 발동 묶음 " + execution.LastPangWaves + "\n" + execution.LastPangMessage;
            return text;
        }
    }
}
