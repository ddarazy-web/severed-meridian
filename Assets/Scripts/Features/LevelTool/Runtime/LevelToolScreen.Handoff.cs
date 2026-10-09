#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        [SerializeField] private string incomingWorkspace;
        [SerializeField] private bool incomingDefault;
        private bool recoveryBlocksDefault;

        public void OfferWorkspace(string state)
        {
            // 인계 데이터가 유효한지 먼저 확인하되 현재 작업이나 복구 선택을 덮어쓰지 않는다.
            var candidate = new AuthoringToolWorkspace(); candidate.RestoreState(state);
            ReadIncomingSharedDraft(state, candidate);
            incomingWorkspace = state; incomingDefault = false; DrawIncomingWorkspace();
        }

        public void OfferDefaultWorkspace(string state)
        {
            if (!string.IsNullOrEmpty(incomingWorkspace) && !incomingDefault) return;
            var candidate = new AuthoringToolWorkspace(); candidate.RestoreState(state);
            incomingWorkspace = state; incomingDefault = true;
            TryOpenDefaultWorkspace(); DrawIncomingWorkspace();
        }

        private void TryOpenDefaultWorkspace()
        {
            // 복구 확인이 끝난 빈 화면에서만 자동으로 연다. 기존/복구 작업은 명시적 선택을 기다린다.
            if (!incomingDefault || string.IsNullOrEmpty(incomingWorkspace) || busy || recoveryBlocksDefault || Session != null || modal != null) return;
            AcceptIncomingWorkspace("Cancel"); Refresh();
        }

        private void DrawIncomingWorkspace()
        {
            if (toolbar == null) return;
            var button = toolbar.Q<Button>("open-incoming-workspace");
            if (string.IsNullOrEmpty(incomingWorkspace)) { button?.RemoveFromHierarchy(); return; }
            string label = incomingDefault ? "기본 JSON 작업 열기" : "이전 창의 작업 가져오기";
            if (button != null) { button.text = label; return; }
            Button(toolbar, "open-incoming-workspace", label, () =>
            {
                if (busy || RecordsBusy || BatchActive || MultiActive || toolBot?.NeedsAdvance == true || playRoot != null) return;
                WithDirty(AcceptIncomingWorkspace);
            }).tooltip = "기존 선택·미저장 내용·실행 취소 이력을 이어받습니다. 현재 작업이 있으면 저장/버리기/취소를 먼저 선택합니다.";
        }

        private bool AcceptIncomingWorkspace(string choice)
        {
            if (string.IsNullOrEmpty(incomingWorkspace)) return false;
            if (Session?.IsDirty == true && choice == "Cancel") return false;
            var candidate = new AuthoringToolWorkspace(); candidate.RestoreState(incomingWorkspace);
            var shared = ReadIncomingSharedDraft(incomingWorkspace, candidate);
            if (Session?.IsDirty == true && choice == "Save") Workspace.Save();
            Workspace.RestoreState(incomingWorkspace);
            sharedTutorialDraft = shared;
            if (shared != null) { inspectorPage = "튜토리얼"; tutorialStep = 0; brush = null; }
            incomingWorkspace = null;
            return true;
        }

        private static SharedTutorialDraft ReadIncomingSharedDraft(string state, AuthoringToolWorkspace workspace)
        {
            string saved = (string)JObject.Parse(state)["sharedTutorialDraft"];
            if (string.IsNullOrEmpty(saved)) return null;
            var shared = SharedTutorialDraft.Restore(saved);
            // 기존 원본과 연결이 맞는지 검증하되 적용하지 않는다.
            shared.ValidateParent(workspace.Session);
            return shared;
        }
    }
}
#endif
