using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Storage;
using Levels.Editor;
using Tutorial;
using Tutorial.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class JsonFlowDraftIsolationVerification
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        public static void Run()
        {
            LevelEditorWindow owner = null, child = null;
            try
            {
                var source = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-export.txt")).Read();
                string root = Path.GetFullPath("ContentData/Trials/game-authoring-stage-03/flow-draft-" + Guid.NewGuid().ToString("N"));
                var saved = new ContentSnapshotStore(root).Publish(source.Snapshot, null);
                owner = ScriptableObject.CreateInstance<LevelEditorWindow>(); owner.CreateGUI();
                typeof(LevelEditorWindow).GetMethod("OpenJsonWorkspace", Flags).Invoke(owner, new object[] { root });
                var workspace = (JsonAuthoringWorkspace)typeof(LevelEditorWindow).GetField("jsonWorkspace", Flags).GetValue(owner);
                TutorialFlowDefinition flow = null;
                workspace.Execute("공통 흐름 준비", () =>
                {
                    flow = ScriptableObject.CreateInstance<TutorialFlowDefinition>(); workspace.Register(flow, "tutorialFlow");
                    flow.name = "격리 시험"; flow.steps.Add(new TutorialStepDefinition { authoringId = "draft-step", instructions = "원본" });
                    TutorialFlowAuthoring.Connect(owner.CurrentLevel, flow);
                });
                child = LevelEditorWindow.OpenTutorialFlow(owner.CurrentLevel, flow, owner);
                if (child.rootVisualElement.Q<Button>("open-json-folder")?.enabledInHierarchy == true) throw new Exception("JSON 공통 사본에서 폴더 전환 가능");
                var panel = child.rootVisualElement.Q<LevelTutorialEditorPanel>();
                if (panel.Q<Button>("tutorial-flow-create") != null || panel.Q("tutorial-user-sample") != null || panel.Q("tutorial-flow-choice") != null) throw new Exception("JSON 공통 사본에서 SO 공통 구성·샘플 진입 가능");
                if (child.rootVisualElement.Query<Button>("initial-pack-rebuild").ToList().Any(button => button.enabledSelf)) throw new Exception("JSON 공통 사본에서 배포 팩 갱신 가능");
                foreach (var input in child.rootVisualElement.Query<PopupField<string>>("initial-level-source").ToList())
                    if (input.enabledSelf || input.choices.Contains("MemoryPack")) throw new Exception("JSON 공통 사본이 배포 팩 시험 허용");
                if (child.rootVisualElement.Q<PopupField<string>>("game-level-source").enabledSelf) throw new Exception("게임 입력의 팩 선택 허용");
                bool blocked = false;
                try { typeof(LevelEditorWindow).GetMethod("OpenJsonWorkspace", Flags).Invoke(child, new object[] { root }); }
                catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { blocked = true; }
                if (!blocked) throw new Exception("폴더 열기의 콜백 가드 누락");
                typeof(LevelTutorialEditorPanel).GetMethod("CreateSharedFlow", Flags).Invoke(panel, null);
                typeof(LevelEditorWindow).GetMethod("ShowShapeRecommendations", Flags).Invoke(child, null);
                child.CurrentLevel.Tutorial.steps[0].instructions = "사본 편집";
                if (flow.steps[0].instructions != "원본") throw new Exception("사본 편집이 부모를 즉시 변경함");
                var apply = child.rootVisualElement.Q<Button>("tutorial-flow-apply");
                typeof(Clickable).GetMethod("Invoke", Flags).Invoke(apply.clickable, new object[] { null });
                if (flow.steps[0].instructions != "사본 편집") throw new Exception("공통 적용 기능 차단됨");
                child.CreateGUI();
                if (child.rootVisualElement.Q<LevelTutorialEditorPanel>().Q<Button>("tutorial-flow-create") != null || child.rootVisualElement.Query<Button>("initial-pack-rebuild").ToList().Any(button => button.enabledSelf)) throw new Exception("UI 재생성 후 사본 격리 해제됨");
                var catalogChoice = child.rootVisualElement.Q<UnityEditor.UIElements.ObjectField>("element-catalog-asset");
                if (catalogChoice != null && catalogChoice.enabledSelf) throw new Exception("사본의 SO 카탈로그 연결 허용");
                Close(owner); owner = null;
                if (!(bool)typeof(LevelEditorWindow).GetProperty("IsJsonFlowDraft", Flags).GetValue(child)) throw new Exception("부모 종료 후 SO 제한 해제");
                typeof(Clickable).GetMethod("Invoke", Flags).Invoke(child.rootVisualElement.Q<Button>("tutorial-flow-apply").clickable, new object[] { null });
                if (!child.rootVisualElement.Query<HelpBox>().ToList().Any(box => box.text.Contains("JSON 원본 창이 닫혔거나"))) throw new Exception("부모 종료 후 적용 실패 안내 누락");
                if (new ContentSnapshotStore(root).Read().Hash != saved.Hash) throw new Exception("사본 편집이 저장 원본 변경함");
                File.WriteAllText("Logs/GameAuthoringStage03/flow-draft-isolation-results.txt", "PASS isolated JSON flow draft\nPASS asset actions blocked\nPASS pack input blocked\nPASS mode callback guard\nPASS explicit shared apply\nPASS UI rebuild isolation\nPASS original disk unchanged\n");
                Close(child); child = null; Close(owner); owner = null; EditorApplication.Exit(0);
            }
            catch (Exception error) { Close(child); Close(owner); Debug.LogException(error); EditorApplication.Exit(1); }
        }
        private static void Close(LevelEditorWindow window)
        {
            if (window == null) return;
            typeof(EditorWindow).GetProperty("hasUnsavedChanges").GetSetMethod(true).Invoke(window, new object[] { false });
            UnityEngine.Object.DestroyImmediate(window);
        }
    }
}
