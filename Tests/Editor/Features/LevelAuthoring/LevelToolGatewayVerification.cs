using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelAuthoring.Storage;
using LevelAuthoring.Runtime;
using Newtonsoft.Json.Linq;
using Levels;
using Levels.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolGatewayVerification
    {
        public static void Run()
        {
            LevelEditorWindow window = null;
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                string folder = File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt");
                var workspace = new AuthoringToolWorkspace(); workspace.Open(folder, "Cancel");
                string id = workspace.Session.SelectedLevelId;
                workspace.Session.Apply("보존 초안", docs => docs[id].Data["displayName"] = "기존 창 초안");
                workspace.Session.SelectCells(new[] { 10 });
                string expected = workspace.Session.ExportState();
                window = ScriptableObject.CreateInstance<LevelEditorWindow>();
                typeof(LevelEditorWindow).GetField("jsonFolder", flags).SetValue(window, Path.GetFullPath(folder));
                typeof(LevelEditorWindow).GetField("jsonEditState", flags).SetValue(window, expected);
                typeof(LevelEditorWindow).GetMethod("RestoreJsonWorkspace", flags).Invoke(window, null);
                window.CreateGUI();
                Check(window.rootVisualElement.Q("level-tool-gateway") != null && window.rootVisualElement.Q("workspace-tabs") == null,
                    "restored old window shows handoff instead of duplicate editing UI");
                string state = (string)typeof(LevelEditorWindow).GetMethod("CaptureToolWorkspace", flags).Invoke(window, null);
                var restored = new AuthoringToolWorkspace(); restored.RestoreState(state);
                Check(restored.Session.ExportState() == expected && restored.Folder == Path.GetFullPath(folder), "old JSON draft handoff preserves complete edit state");
                var source = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Data/Levels/Level_01.asset");
                Check(source != null, "legacy level fixture exists");
                var files = Directory.GetFiles("Assets/Data", "*.asset", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllText);
                string destination = Path.GetFullPath("ContentData/Trials/game-authoring-stage-02/gateway-" + Guid.NewGuid().ToString("N"));
                Type importer = typeof(LevelTool.Editor.LevelToolLauncher).Assembly.GetType("LevelTool.Editor.LevelToolLegacyImport");
                Check(importer != null, "legacy import adapter exists");
                string imported = (string)importer.GetMethod("CreateWorkspace").Invoke(null, new object[] { source, destination });
                restored.RestoreState(imported);
                Check((int)restored.Session.Get(restored.Session.SelectedLevelId).Data["levelNumber"] == source.LevelNumber &&
                    restored.Folder == destination, "legacy import selects requested level in independent JSON folder");
                foreach (var file in files) Check(File.ReadAllText(file.Key) == file.Value, "legacy source unchanged " + Path.GetFileName(file.Key));
                MethodInfo importDraft = importer.GetMethod("CreateDraftWorkspace");
                Check(importDraft != null, "temporary tutorial import exists");
                var draft = UnityEngine.Object.Instantiate(source);
                try
                {
                    // 불완전한 미저장 사본도 잃지 않고 새 화면에서 고칠 수 있어야 한다.
                    using (var data = new SerializedObject(draft)) { data.FindProperty("moveCount").intValue = -1; data.ApplyModifiedPropertiesWithoutUndo(); }
                    string draftFolder = Path.GetFullPath("ContentData/Trials/game-authoring-stage-02/draft-" + Guid.NewGuid().ToString("N"));
                    string transferred = (string)importDraft.Invoke(null, new object[] { draft, null, draftFolder });
                    restored.RestoreState(transferred);
                    Check((int)restored.Session.Get(restored.Session.SelectedLevelId).Data["moveCount"] == -1 && restored.Session.IsDirty,
                        "incomplete temporary draft remains editable and unsaved");
                    Check(!new ContentSnapshotStore(draftFolder).Read().Snapshot.Documents.Any(doc => doc.Id == restored.Session.SelectedLevelId),
                        "temporary draft is not implicitly published");
                }
                finally { UnityEngine.Object.DestroyImmediate(draft); }
                var parent = (JsonAuthoringWorkspace)typeof(LevelEditorWindow).GetField("jsonWorkspace", flags).GetValue(window);
                TutorialDraftEditing.Detach(parent.Session);
                string flowId = TutorialDraftEditing.CreateFlow(parent.Session, "인계 공유 구성");
                // 새 표시 객체가 현재 세션을 읽도록 부모 창을 복구한다.
                typeof(LevelEditorWindow).GetField("jsonEditState", flags).SetValue(window, parent.Session.ExportState());
                parent.Dispose(); typeof(LevelEditorWindow).GetField("jsonWorkspace", flags).SetValue(window, null);
                typeof(LevelEditorWindow).GetMethod("RestoreJsonWorkspace", flags).Invoke(window, null);
                parent = (JsonAuthoringWorkspace)typeof(LevelEditorWindow).GetField("jsonWorkspace", flags).GetValue(window);
                var child = LevelEditorWindow.OpenTutorialFlow(parent.Level, (Tutorial.TutorialFlowDefinition)parent.Resolve(flowId), window);
                try
                {
                    var pending = (Tutorial.TutorialFlowDefinition)typeof(LevelEditorWindow).GetField("tutorialFlowDraft", flags).GetValue(child);
                    pending.name = "미적용 공유 변경";
                    string before = parent.Session.ExportState();
                    MethodInfo capture = typeof(LevelEditorWindow).GetMethod("CaptureSharedToolWorkspace", flags);
                    Check(capture != null, "shared draft handoff exists");
                    var packet = JObject.Parse((string)capture.Invoke(child, null));
                    var transferredDraft = SharedTutorialDraft.Restore((string)packet["sharedTutorialDraft"]);
                    Check(parent.Session.ExportState() == before, "shared handoff leaves parent unchanged");
                    Check((string)transferredDraft.Session.Get(flowId).Data["displayName"] == pending.name && transferredDraft.IsChanged,
                        "shared pending edit remains separate from parent");
                }
                finally { UnityEngine.Object.DestroyImmediate(child); }
                Debug.Log("PASS gateway and legacy import"); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
            finally { if (window != null) UnityEngine.Object.DestroyImmediate(window); }
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}
