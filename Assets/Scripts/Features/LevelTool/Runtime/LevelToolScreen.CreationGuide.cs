#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System.Collections.Generic;
using LevelAuthoring.Editing;
using LevelTool.Presentation;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private Button creationGuide;
        private AuthoringEditSession checkedPlayOwner, trialChecklistOwner;
        private readonly Dictionary<string, long> checkedPlayVersions = new Dictionary<string, long>();
        private string trialChecklistId;
        private long trialChecklistVersion;

        private LevelToolCreationChecklist CreationChecklist() => new LevelToolCreationChecklist(displayedLevel,
            ReferenceEquals(checkedPlayOwner, Session) && checkedPlayVersions.TryGetValue(Session.SelectedLevelId, out long version) && version == Session.ContentVersion,
            Session.HasLevelChanges(Session.SelectedLevelId) || HasPendingAuthoringInput());

        private void RefreshCreationGuide()
        {
            if (creationGuide == null) return;
            bool available = Session?.SelectedLevelId != null && displayedLevel != null;
            creationGuide.SetEnabled(available && !busy);
            creationGuide.text = available ? "제작 안내 · " + CreationChecklist().NextAction : "제작 안내";
            creationGuide.tooltip = "보드·이동 횟수와 미션·플레이·저장 상태를 확인합니다. 순서는 자유롭게 선택할 수 있습니다.";
        }

        private void OpenCreationGuide()
        {
            if (busy || modal != null || displayedLevel == null || Session?.SelectedLevelId == null) return;
            LevelToolCreationChecklist state = CreationChecklist();
            VisualElement panel = OpenModal("레벨 제작 체크리스트");
            panel.Add(new Label("순서는 자유롭게 선택하세요. 기본 설정 확인과 실제 게임 시험은 서로 다릅니다.") { style = { whiteSpace = WhiteSpace.Normal } });
            Button(panel, "checklist-board", (state.BoardConfigured ? "✓ " : "○ ") + "보드 구성 · 배치 편집", () => OpenCreationSettings(false));
            Button(panel, "checklist-settings", (state.MovesConfigured && state.MissionsConfigured ? "✓ " : "○ ") + "이동 횟수·미션 설정", () => OpenCreationSettings(true));
            if (state.MissionError != null) panel.Add(new Label(state.MissionError) { style = { whiteSpace = WhiteSpace.Normal } });
            panel.Add(new Label(state.PlayChecked ? "✓ 현재 편집본의 플레이 조작 확인됨 · 클리어 보장은 아닙니다." : "○ 플레이 확인 필요 · 설정만으로 시험 완료가 되지 않습니다.")
                { name = "checklist-play-status", style = { whiteSpace = WhiteSpace.Normal } });
            Button(panel, "checklist-play", "게임 시험", () => { CloseModal(); TryExecuteCommand(ToolCommandId.Play); });
            panel.Add(new Label(state.Saved ? "✓ 현재 레벨 저장됨" : "○ 현재 레벨 미저장 · 파일 저장이 필요합니다.") { name = "checklist-save-status" });
            Button(panel, "checklist-save", "파일 저장", () => { CloseModal(); TryExecuteCommand(ToolCommandId.Save); });
            Button(panel, "checklist-close", "닫기", CloseModal);
        }

        private void OpenCreationSettings(bool settings)
        {
            CloseModal();
            if (GuardTutorialWorkExit(() => OpenCreationSettings(settings))) return;
            SelectWorkspacePage("보드 편집");
            levelSettingsVisible = settings; boardFocus = false; rightPanelHidden = false;
            if (mobileNavigation != null) SelectMobilePanel(settings ? "properties" : "materials");
            UpdatePanelVisibility();
            if (settings) properties.ScrollTo(properties.Q("level-moves"));
        }

        private void CaptureTrialChecklist(bool usesCurrentJson)
        {
            trialChecklistOwner = usesCurrentJson && sharedTutorialDraft == null ? Session : null;
            trialChecklistId = Session.SelectedLevelId;
            trialChecklistVersion = Session.ContentVersion;
        }

        private void RecordTrialChecklist()
        {
            // 실제 이동을 소비한 시험만 확인한다. 실행 실패·보기만 한 시험·다른 팩은 인정하지 않는다.
            if (trialChecklistOwner != null && ReferenceEquals(trialChecklistOwner, Session) &&
                trialChecklistId == Session.SelectedLevelId && trialChecklistVersion == Session.ContentVersion &&
                playSession != null && !playSession.HasFailed && playSession.State != null &&
                playSession.State.MovesRemaining < playSession.State.InitialMoves)
            {
                if (!ReferenceEquals(checkedPlayOwner, Session)) { checkedPlayVersions.Clear(); checkedPlayOwner = Session; }
                checkedPlayVersions[trialChecklistId] = trialChecklistVersion;
            }
            trialChecklistOwner = null;
            RefreshCreationGuide();
        }
    }
}
#endif
