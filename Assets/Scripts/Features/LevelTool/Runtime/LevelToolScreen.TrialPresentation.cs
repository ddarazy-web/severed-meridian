#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private VisualElement trialLoadingModal;

        private void ShowTrialLoading(int lastStep)
        {
            var panel = OpenModal("게임 시험 준비 중…");
            trialLoadingModal = modal;
            panel.Add(new Label(TrialSourceLabel(lastStep)) { name = "trial-source-label", style = { whiteSpace = WhiteSpace.Normal } });
            panel.Add(new Label("선택한 레벨과 게임 리소스를 준비하고 있습니다. 편집 내용은 저장하거나 변경하지 않습니다.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            Button(panel, "trial-loading-cancel", "준비 취소 · 편집으로 돌아가기", CancelTrialLoading);
        }

        private void CloseTrialLoading()
        {
            if (trialLoadingModal != null && ReferenceEquals(modal, trialLoadingModal)) CloseModal();
            trialLoadingModal = null;
        }

        private void CancelTrialLoading()
        {
            ReturnToEditor();
            Show("게임 시험 준비를 취소했습니다. 편집 내용과 실행 취소 이력은 유지했습니다.");
        }

        private string TrialSourceLabel(int lastStep) => lastStep >= 0 ? "현재 편집본 JSON · 첫 단계부터 " + (lastStep + 1) + "단계까지" :
            packedTrial ? "생성된 MemoryPack · 현재 미저장 수정은 반영되지 않습니다" : "현재 편집본 JSON · 미저장 수정 포함";

        private void ShowTrialFailure(string reason, int lastStep)
        {
            Show("시험 시작 실패: " + reason);
            var panel = OpenModal("게임 시험을 시작하지 못했습니다");
            panel.Add(new Label(TrialSourceLabel(lastStep)) { name = "trial-source-label", style = { whiteSpace = WhiteSpace.Normal } });
            panel.Add(new Label(reason) { name = "trial-failure-message", style = { whiteSpace = WhiteSpace.Normal } });
            panel.Add(new Label("편집 내용과 실행 취소 이력은 유지했습니다. 데이터 문제는 레벨 검사에서 위치를 확인하고 수정하세요. 리소스 문제는 연결을 고친 뒤 다시 시험하세요.")
                { style = { whiteSpace = WhiteSpace.Normal } });
            Button(panel, "trial-failure-edit", "편집으로 돌아가기", () =>
            {
                CloseModal(); Show("시험 준비에 실패했습니다. 편집 내용은 유지했습니다. 원인: " + reason);
            });
            Button(panel, "trial-failure-check", "현재 레벨 검사", () => { CloseModal(); TryExecuteCommand(ToolCommandId.Validate); });
            Button(panel, "trial-failure-retry", "같은 원본으로 다시 시험", () => { CloseModal(); BeginPlayCore(lastStep).Forget(); });
        }
    }
}
#endif
