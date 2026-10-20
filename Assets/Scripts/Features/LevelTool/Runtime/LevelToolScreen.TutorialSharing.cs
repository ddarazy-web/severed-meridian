#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using Cysharp.Threading.Tasks;
using LevelAuthoring.Runtime;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private SharedTutorialDraft sharedTutorialDraft;
        private void ApplySharedTutorialDraft() => ContinueAfterManualInput(() =>
            Edit(() => { sharedTutorialDraft.Apply(Session); sharedTutorialDraft = null; })).Forget();
        private void AskSharedDraftBeforeLeave(Action continuation, string operation)
        {
            if (!sharedTutorialDraft.IsChanged && !HasPendingAuthoringInput()) { sharedTutorialDraft = null; Refresh(); RequestLeaveFor(continuation, operation); return; }
            var panel = OpenModal(operation + " 전 공유 사본 확인");
            panel.Add(new Label("공유 사본을 원본에 적용해도 파일에 저장되지는 않습니다. 적용 후 다음 창에서 프로젝트 저장 여부를 선택하세요. 사본만 버리면 기존 원본은 유지됩니다.") { style = { whiteSpace = WhiteSpace.Normal } });
            Button(panel, "apply-flow-and-leave", "원본 적용 후 계속", () =>
            {
                CloseModal();
                ContinueAfterManualInput(() => Edit(() =>
                { sharedTutorialDraft.Apply(Session); sharedTutorialDraft = null; RequestLeaveFor(continuation, operation); })).Forget();
            });
            Button(panel, "discard-flow-and-leave", "사본 버리고 계속", () =>
            { CloseModal(); DiscardUncommittedInputs(); sharedTutorialDraft = null; Refresh(); RequestLeaveFor(continuation, operation); });
            Button(panel, "cancel", "취소", CloseModal);
        }
    }
}
#endif
