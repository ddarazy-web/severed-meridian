using System;
using System.Reflection;
using LevelAuthoring.Storage;
using LevelAuthoring.Runtime;
using Newtonsoft.Json.Linq;
using LevelTool;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolHandoffExercise
    {
        internal static void Run(LevelToolScreen screen)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var session = screen.Workspace.Session;
            string id = session.SelectedLevelId;
            string diskHash = new ContentSnapshotStore(screen.Workspace.Folder).Read().Hash;
            session.SelectCells(new[] { 12, 13 });
            session.Apply("인계 초안", docs => docs[id].Data["displayName"] = "이전 창의 미저장 초안");
            session.Apply("인계 Redo", docs => docs[id].Data["displayName"] = "되살릴 이름"); session.Undo();
            string incoming = screen.Workspace.ExportState(), expected = session.ExportState();
            session.Apply("현재 도구 작업", docs => docs[id].Data["displayName"] = "새 도구의 미저장 작업");
            string current = session.ExportState();
            typeof(LevelToolScreen).GetMethod("Refresh", flags).Invoke(screen, null);
            MethodInfo offer = typeof(LevelToolScreen).GetMethod("OfferWorkspace");
            Check(offer != null, "workspace handoff API exists"); offer.Invoke(screen, new object[] { incoming });
            VisualElement root = screen.GetComponent<UIDocument>().rootVisualElement;
            void Click(string name)
            {
                Button button = root.Q<Button>(name) ?? throw new Exception("handoff control missing: " + name);
                typeof(Clickable).GetMethod("Invoke", flags).Invoke(button.clickable, new object[] { null });
            }
            Check(session.ExportState() == current, "offering a workspace does not overwrite current draft");
            Click("open-incoming-workspace"); Click("cancel");
            Check(screen.Workspace.Session.ExportState() == current, "handoff cancel preserves current draft and history");
            // 저장소 작업은 별도 저장 검사가 검증한다. 이 검사는 실제 선택 콜백과 복원 결과를 대조한다.
            MethodInfo accept = typeof(LevelToolScreen).GetMethod("AcceptIncomingWorkspace", flags);
            Check(accept != null && (bool)accept.Invoke(screen, new object[] { "Discard" }), "explicit discard accepts incoming workspace");
            Check(screen.Workspace.Session.ExportState() == expected && screen.Workspace.Session.IsDirty,
                "handoff restores selected cells dirty state undo and redo exactly");
            screen.Workspace.Session.Redo();
            Check((string)screen.Workspace.Session.Get(id).Data["displayName"] == "되살릴 이름", "incoming redo remains usable");
            Check(new ContentSnapshotStore(screen.Workspace.Folder).Read().Hash == diskHash, "handoff does not write source JSON");
            session = screen.Workspace.Session;
            TutorialDraftEditing.Detach(session);
            string flowId = TutorialDraftEditing.CreateFlow(session, "인계 공통 구성");
            session.Apply("미완성 입력", docs => docs[id].Data["moveCount"] = -1);
            var shared = new SharedTutorialDraft(session);
            TutorialDraftEditing.EditFlow(shared.Session, flowId, "미적용 이름", flow => flow.name = "인계한 미적용 사본");
            var packet = JObject.Parse(screen.Workspace.ExportState()); packet["sharedTutorialDraft"] = shared.ExportState();
            string parent = session.ExportState();
            typeof(LevelToolScreen).GetField("brush", flags).SetValue(screen, "previous-palette-selection");
            offer.Invoke(screen, new object[] { packet.ToString() });
            Check((bool)accept.Invoke(screen, new object[] { "Discard" }), "shared packet accepted");
            var accepted = (SharedTutorialDraft)typeof(LevelToolScreen).GetField("sharedTutorialDraft", flags).GetValue(screen);
            Check(accepted != null && accepted.ExportState() == shared.ExportState(), "unapplied shared draft restored exactly");
            Check(typeof(LevelToolScreen).GetField("brush", flags).GetValue(screen) == null, "shared handoff clears prior board placement brush");
            Check(screen.Workspace.Session.ExportState() == parent, "shared handoff does not apply to parent");
            typeof(LevelToolScreen).GetMethod("Refresh", flags).Invoke(screen, null);
            Check(root.Q<Button>("tutorial-apply-flow") != null, "handoff opens pending shared editor");
            Click("tutorial-apply-flow");
            Check((string)screen.Workspace.Session.Get(flowId).Data["displayName"] == "인계한 미적용 사본", "shared changes apply only on explicit action");
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}
