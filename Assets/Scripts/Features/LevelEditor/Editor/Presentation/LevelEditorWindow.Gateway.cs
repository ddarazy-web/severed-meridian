using System;
using System.IO;
using System.Linq;
using LevelAuthoring.Editing;
using LevelAuthoring.Runtime;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelEditorWindow
    {
        public void CreateGUI()
        {
            RestoreJsonWorkspace();
            rootVisualElement.Clear(); editorRoot = null;
            titleContent = new GUIContent("레벨툴 안내");
            var panel = new VisualElement { name = "level-tool-gateway", style = { paddingLeft = 16, paddingRight = 16, paddingTop = 16 } };
            rootVisualElement.Add(panel);
            panel.Add(new Label("레벨 편집은 공통 레벨툴 씬으로 이동했습니다.") { style = { whiteSpace = WhiteSpace.Normal } });
            var message = new Label { style = { whiteSpace = WhiteSpace.Normal } }; panel.Add(message);
            void Run(Action action) { try { action(); } catch (Exception error) { message.text = error.Message; } }
            panel.Add(new Button(() => Run(LevelTool.Editor.LevelToolLauncher.Launch)) { text = "레벨툴 열기" });
            if (IsJsonMode)
            {
                panel.Add(new Label(jsonFolder + (jsonWorkspace.Session.IsDirty ? " · 미저장 작업 있음" : " · 저장된 작업")));
                panel.Add(new Button(() => Run(() => LevelTool.Editor.LevelToolLauncher.LaunchWorkspace(CaptureToolWorkspace())))
                    { text = "이 창의 JSON 작업 이어가기", name = "handoff-json-workspace" });
                panel.Add(new Button(() => Run(SaveJsonWorkspace)) { text = "남은 JSON 작업 저장" });
            }
            if (level != null && EditorUtility.IsPersistent(level))
                panel.Add(new Button(() => Run(() => LevelTool.Editor.LevelToolLegacyImport.Open(level))) { text = "이 구형 레벨을 JSON 사본으로 가져오기" });
            if (temporaryTutorialSample != null || tutorialFlowDraft != null)
            {
                if (IsJsonFlowDraft)
                    panel.Add(new Button(() => Run(() => LevelTool.Editor.LevelToolLauncher.LaunchWorkspace(CaptureSharedToolWorkspace())))
                    { text = "미적용 공유 사본 이어가기", name = "handoff-shared-workspace", tooltip = "부모 JSON 작업과 미적용 공유 사본을 함께 넘깁니다. 새 화면에서도 원본 적용 전까지 분리됩니다." });
                else
                panel.Add(new Button(() => Run(() =>
                {
                    string folder = Path.GetFullPath("ContentData/Trials/game-authoring-stage-02/draft-" + Guid.NewGuid().ToString("N"));
                    string state = LevelTool.Editor.LevelToolLegacyImport.CreateDraftWorkspace(temporaryTutorialSample, tutorialFlowDraft, folder);
                    LevelTool.Editor.LevelToolLauncher.LaunchWorkspace(state);
                })) { text = "임시 튜토리얼을 독립 사본으로 이어가기", tooltip = "새 레벨과 독립 공유 구성으로 가져옵니다. 기존 공유 원본에 적용하지 않습니다." });
                panel.Add(new Label("이 창에 임시 튜토리얼 사본이 남아 있습니다. 창을 닫기 전에 백업하세요. 공유 원본에 자동 적용하지 않습니다.")
                    { style = { whiteSpace = WhiteSpace.Normal } });
                panel.Add(new Button(() => Run(() =>
                {
                    string destination = EditorUtility.SaveFilePanel("임시 튜토리얼 사본 백업", "", "tutorial-draft-backup", "json");
                    if (string.IsNullOrEmpty(destination)) return;
                    var backup = new JObject { ["version"] = 1,
                        ["level"] = temporaryTutorialSample == null ? null : JObject.Parse(EditorJsonUtility.ToJson(temporaryTutorialSample)),
                        ["flow"] = tutorialFlowDraft == null ? null : JObject.Parse(EditorJsonUtility.ToJson(tutorialFlowDraft)) };
                    File.WriteAllText(destination, backup.ToString()); message.text = "임시 사본 백업: " + destination;
                })) { text = "임시 튜토리얼 사본 백업" });
            }
        }

        private string CaptureSharedToolWorkspace()
        {
            if (!TryPrepareJsonDraftTest(out string reason)) throw new InvalidOperationException(reason);
            data?.ApplyModifiedPropertiesWithoutUndo();
            var workspace = new AuthoringToolWorkspace(); workspace.RestoreState(jsonFlowOwner.CaptureToolWorkspace());
            workspace.Session.SelectLevel(jsonFlowSourceLevelId);
            var shared = new SharedTutorialDraft(workspace.Session);
            if (shared.FlowId != jsonFlowDocumentId) throw new InvalidOperationException("원본 레벨의 공유 연결이 바뀌었습니다. 인계 전에 원본 연결을 확인하세요.");
            var flow = Instantiate(tutorialFlowDraft);
            try
            {
                flow.name = tutorialFlowDraft.name;
                flow.steps = Tutorial.TutorialAuthoringRules.CopySteps(temporaryTutorialSample.Tutorial.steps);
                var resources = workspace.Session.Documents.Single(doc => doc.Kind == "project").Data["resources"]
                    .GroupBy(item => (string)item["path"]).ToDictionary(group => group.Key, group => (string)group.First()["id"]);
                var changed = UnityAuthoringCodec.WriteDraft(flow, "tutorialFlow", shared.FlowId,
                    jsonFlowOwner.jsonWorkspace.DocumentId, path => resources[path]);
                shared.Session.Apply("기존 창의 미적용 공유 사본", docs => docs[shared.FlowId] = changed);
                var packet = JObject.Parse(workspace.ExportState()); packet["sharedTutorialDraft"] = shared.ExportState();
                return packet.ToString();
            }
            finally { DestroyImmediate(flow); }
        }

        private string CaptureToolWorkspace()
        {
            RestoreJsonWorkspace();
            if (jsonWorkspace == null) throw new InvalidOperationException("인계할 JSON 작업이 없습니다.");
            data?.ApplyModifiedPropertiesWithoutUndo(); jsonWorkspace.Capture("인계 전 입력 확정"); PersistJsonState();
            var state = new JObject { ["version"] = 1, ["folder"] = Path.GetFullPath(jsonFolder), ["recovery"] = jsonBackupMode,
                ["session"] = JObject.Parse(jsonWorkspace.Session.ExportState()) };
            var workspace = new AuthoringToolWorkspace(); workspace.RestoreState(state.ToString());
            return workspace.ExportState();
        }
    }
}
