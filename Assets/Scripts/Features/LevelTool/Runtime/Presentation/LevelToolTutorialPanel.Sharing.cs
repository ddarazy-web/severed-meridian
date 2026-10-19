#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using LevelAuthoring.Runtime;
using Levels;
using Tutorial;
using UnityEngine.UIElements;

namespace LevelTool.Presentation
{
    public sealed partial class LevelToolTutorialPanel
    {
        private bool DrawTutorialSharing(LevelDefinition level)
        {
            var flows = Session.Documents.Where(doc => doc.Kind == "tutorialFlow").ToArray();
            string current = (string)Session.Get(Session.SelectedLevelId).Data["tutorial"]["flowId"];
            if (!flows.Any(doc => doc.Id == state.SelectedFlow)) state.SelectedFlow = current ?? flows.FirstOrDefault()?.Id;
            var sharing = new Foldout { text = "공통 구성 · 같은 진행을 여러 레벨에서 사용", value = true }; inspector.Add(sharing);
            Choice(sharing, "tutorial-flow-choice", "사용할 구성", flows.Select(doc => doc.Id).ToArray(), flows.Select(doc => (string)doc.Data["displayName"]).ToArray(),
                state.SelectedFlow, value => state.SelectedFlow = value);
            Button(sharing, "tutorial-connect-flow", "선택한 구성 연결", () => Edit(() => TutorialDraftEditing.Connect(Session, state.SelectedFlow))).SetEnabled(flows.Length > 0);
            if (level.Tutorial.flow == null)
            {
                TutorialText(sharing, "tutorial-shared-name", "새 구성 이름", state.SharedFlowName, value => state.SharedFlowName = value);
                Button(sharing, "tutorial-create-flow", "현재 단계를 공통 구성으로 만들기", () => Edit(() => TutorialDraftEditing.CreateFlow(Session, state.SharedFlowName))).SetEnabled(level.Tutorial.steps.Count > 0);
                return false;
            }
            sharing.Add(new Label("현재: " + level.Tutorial.flow.name + "\n흐름과 안내는 공유하며, 아래 레벨별 설정은 이 레벨에만 적용합니다.") { style = { whiteSpace = WhiteSpace.Normal } });
            var usage = new Foldout { text = "이 구성을 사용하는 레벨", name = "tutorial-flow-usage", value = false }; sharing.Add(usage);
            foreach (var doc in Session.Documents.Where(doc => doc.Kind == "level" && (string)doc.Data["tutorial"]?["flowId"] == current))
                usage.Add(new Label(doc.Data["levelNumber"] + ". " + doc.Data["displayName"]));
            Button(sharing, "tutorial-edit-flow", "공유 원본 사본 편집", actions.BeginSharedDraft);
            Button(sharing, "tutorial-detach", "현재 값으로 독립 복사", () => Edit(() => TutorialDraftEditing.Detach(Session)));
            Button(sharing, "tutorial-sync", "레벨 설정 목록 동기화", () => Edit(() => TutorialDraftEditing.Synchronize(Session)))
                .tooltip = "공유 원본의 설정 항목이 추가·변경된 뒤 사용하세요. 키와 종류가 같은 기존 값은 보존합니다.";
            DrawTutorialBindings(level); return true;
        }
        private void DrawSharedDraft(TutorialFlowDefinition flow)
        {
            inspector.Add(new Label("공유 원본의 편집 사본" + (sharedTutorialDraft.IsChanged ? " · 미적용" : "")));
            inspector.Add(new Label("아래 변경은 ‘원본 적용’ 전까지 다른 레벨에 반영되지 않습니다. 위 실행 취소/다시 실행도 이 사본에 적용됩니다. 새 설정을 선언했다면 원본 적용 후 레벨 설정 목록을 동기화하세요.") { style = { whiteSpace = WhiteSpace.Normal } });
            Button(inspector, "tutorial-apply-flow", "원본 적용", ApplySharedTutorialDraft);
            Button(inspector, "tutorial-cancel-flow", "사본 버리고 돌아가기", actions.DiscardSharedDraft);
            TutorialText(inspector, "tutorial-flow-name", "공통 구성 이름", flow.name, value => SharedFlowEdit(data => data.name = value));
            DrawTutorialParameters(flow);
        }
        private void SharedFlowEdit(Action<TutorialFlowDefinition> edit) => Edit(() =>
            TutorialDraftEditing.EditFlow(sharedTutorialDraft.Session, sharedTutorialDraft.FlowId, "공유 설정 선언", edit));
    }
}
#endif
