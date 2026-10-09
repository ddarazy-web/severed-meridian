using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Storage;
using Levels.Editor;
using Newtonsoft.Json.Linq;
using Tutorial;
using Tutorial.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class JsonSampleVerification
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        public static void Run()
        {
            LevelEditorWindow window = null;
            try
            {
                var source = new ContentSnapshotStore(File.ReadAllText("Logs/GameAuthoringStage02/latest-export.txt")).Read();
                string root = Path.GetFullPath("ContentData/Trials/game-authoring-stage-03/samples-" + Guid.NewGuid().ToString("N"));
                var saved = new ContentSnapshotStore(root).Publish(source.Snapshot, null);
                window = ScriptableObject.CreateInstance<LevelEditorWindow>(); window.CreateGUI();
                typeof(LevelEditorWindow).GetMethod("OpenJsonWorkspace", Flags).Invoke(window, new object[] { root });
                var workspace = (JsonAuthoringWorkspace)typeof(LevelEditorWindow).GetField("jsonWorkspace", Flags).GetValue(window);
                workspace.Execute("샘플 시험 구성", () =>
                {
                    if (window.CurrentLevel.Tutorial.flow != null) TutorialFlowAuthoring.Detach(window.CurrentLevel);
                    window.CurrentLevel.Tutorial.steps.Clear();
                    window.CurrentLevel.Tutorial.steps.Add(new TutorialStepDefinition { authoringId = "sample-step-original", instructions = "샘플 안내" });
                });
                var panel = (LevelTutorialEditorPanel)typeof(LevelEditorWindow).GetField("tutorialPanel", Flags).GetValue(window);
                panel.GetType().GetMethod("Rebuild", Flags).Invoke(panel, null);
                panel.GetType().GetMethod("SaveJsonSample", Flags).Invoke(panel, new object[] { "JSON 재사용 샘플", true });
                var document = workspace.Session.Documents.Single(doc => doc.Kind == "tutorialSample" && (string)doc.Data["displayName"] == "JSON 재사용 샘플");
                var sample = (TutorialUserSampleDefinition)workspace.Resolve(document.Id);
                if (EditorUtility.IsPersistent(sample) || sample.steps.Count != 1) throw new Exception("샘플이 JSON 표시 객체가 아님");
                JObject original = document.Data;
                var apply = panel.Q<Button>("tutorial-json-sample-apply");
                if (apply == null) throw new Exception("JSON 샘플 적용 버튼 누락");
                // 실제 버튼 콜백을 실행하여 창 변경 이력에 연결되었는지 확인한다.
                var choice = panel.Query<PopupField<string>>().ToList().Single(field => field.label == "적용할 샘플");
                choice.index = choice.choices.FindIndex(label => label.StartsWith("JSON 재사용 샘플 ·", StringComparison.Ordinal));
                typeof(Clickable).GetMethod("Invoke", Flags).Invoke(apply.clickable, new object[] { null });
                if (window.CurrentLevel.Tutorial.steps.Count != 2 || window.CurrentLevel.Tutorial.steps[1].authoringId == sample.steps[0].authoringId)
                    throw new Exception("샘플 단계의 독립 ID 복사 실패");
                if (!JToken.DeepEquals(workspace.Session.Get(document.Id).Data, original)) throw new Exception("샘플 적용이 원본을 변경함");
                typeof(LevelEditorWindow).GetMethod("UndoJson", Flags).Invoke(window, new object[] { false });
                if (window.CurrentLevel.Tutorial.steps.Count != 1 || workspace.Session.Get(document.Id) == null) throw new Exception("샘플 적용 단일 Undo 실패");
                typeof(LevelEditorWindow).GetMethod("UndoJson", Flags).Invoke(window, new object[] { true });
                if (window.CurrentLevel.Tutorial.steps.Count != 2) throw new Exception("샘플 적용 Redo 실패");
                workspace.Execute("샘플 원본 후속 편집", () => sample.steps[0].instructions = "원본만 변경");
                if (window.CurrentLevel.Tutorial.steps[1].instructions != "샘플 안내") throw new Exception("샘플 수정이 적용된 단계로 전파됨");
                if (new ContentSnapshotStore(root).Read().Hash != saved.Hash) throw new Exception("미저장 샘플이 디스크를 변경함");
                typeof(EditorWindow).GetProperty("hasUnsavedChanges").GetSetMethod(true).Invoke(window, new object[] { false }); UnityEngine.Object.DestroyImmediate(window); window = null;
                Directory.CreateDirectory("Logs/GameAuthoringStage03");
                File.WriteAllText("Logs/GameAuthoringStage03/sample-results.txt", "PASS JSON sample create\nPASS actual sample apply callback\nPASS copied authoring IDs\nPASS sample unchanged by application\nPASS single undo redo\nPASS sample copy isolation\nPASS disk unchanged\n");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                if (window != null) { typeof(EditorWindow).GetProperty("hasUnsavedChanges").GetSetMethod(true).Invoke(window, new object[] { false }); UnityEngine.Object.DestroyImmediate(window); }
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
    }
}
