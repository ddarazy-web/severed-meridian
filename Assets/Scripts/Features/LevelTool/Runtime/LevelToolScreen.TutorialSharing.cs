#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using LevelAuthoring.Runtime;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private SharedTutorialDraft sharedTutorialDraft;
        private void ApplySharedTutorialDraft() => Edit(() => { sharedTutorialDraft.Apply(Session); sharedTutorialDraft = null; });
        private void AskSharedDraftBeforeLeave(Action continuation)
        {
            if (!sharedTutorialDraft.IsChanged) { sharedTutorialDraft = null; Refresh(); RequestLeave(continuation); return; }
            var panel = OpenModal("공유 원본에 적용하지 않은 사본이 있습니다.");
            panel.Add(new Label("원본에 적용한 뒤 저장 여부를 선택하거나 사본을 버릴 수 있습니다.") { style = { whiteSpace = WhiteSpace.Normal } });
            Button(panel, "apply-flow-and-leave", "원본 적용 후 계속", () => Edit(() =>
            { sharedTutorialDraft.Apply(Session); sharedTutorialDraft = null; CloseModal(); RequestLeave(continuation); }));
            Button(panel, "discard-flow-and-leave", "사본 버리고 계속", () => { sharedTutorialDraft = null; CloseModal(); Refresh(); RequestLeave(continuation); });
            Button(panel, "cancel", "취소", CloseModal);
        }
    }
}
#endif
