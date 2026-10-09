using System;
using System.IO;
using System.Reflection;
using System.Linq;
using LevelAuthoring.Runtime;
using Tutorial;
using LevelTool;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    [InitializeOnLoad]
    public static class LevelToolReloadVerification
    {
        private const string Key = "LevelToolReloadVerification.";
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static LevelToolReloadVerification()
        {
            if (SessionState.GetBool(Key + "active", false)) Hook();
        }
        public static void Run()
        { SessionState.SetBool(Key + "shared", false); Start(); }
        public static void RunShared()
        { SessionState.SetBool(Key + "shared", true); Start(); }
        private static void Start()
        {
            SessionState.SetBool(Key + "active", true);
            SessionState.SetBool(Key + "enabled", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Key + "options", (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetString(Key + "deadline", DateTime.UtcNow.AddMinutes(4).ToString("O"));
            SessionState.SetInt(Key + "phase", 0);
            string folder = Path.GetFullPath("Logs/GameAuthoringStage05/reload-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); SessionState.SetString(Key + "folder", folder);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath);
            LevelToolRecoveryIsolation.Configure(folder);
            Hook(); EditorApplication.EnterPlaymode();
        }
        private static void Hook()
        { EditorApplication.update -= Tick; EditorApplication.update += Tick; }
        private static void Tick()
        {
            try
            {
                if (DateTime.UtcNow > DateTime.Parse(SessionState.GetString(Key + "deadline", ""))) throw new Exception("reload test timeout");
                int phase = SessionState.GetInt(Key + "phase", 0);
                if (phase == 3 && !EditorApplication.isPlayingOrWillChangePlaymode)
                { SessionState.SetInt(Key + "phase", 4); EditorApplication.EnterPlaymode(); return; }
                if (phase == 5 && !EditorApplication.isPlayingOrWillChangePlaymode) { Finish(0); return; }
                if (!EditorApplication.isPlaying) return;
                LevelToolScreen screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                if (screen == null || (bool)typeof(LevelToolScreen).GetField("busy", Flags).GetValue(screen)) return;
                LevelToolRecoveryIsolation.Verify(screen, SessionState.GetString(Key + "folder", ""));
                string expected = Path.Combine(SessionState.GetString(Key + "folder", ""), "expected.json");
                if (phase == 0)
                {
                    screen.Workspace.Open(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt"), "Cancel");
                    string id = screen.Workspace.Session.SelectedLevelId;
                    screen.Workspace.Session.SelectCells(new[] { 20, 21 });
                    screen.Workspace.Session.Apply("reload dirty", docs => docs[id].Data["moveCount"] = 39);
                    screen.Workspace.Session.Apply("reload redo", docs => docs[id].Data["moveCount"] = 40);
                    screen.Workspace.Session.Undo();
                    if (SessionState.GetBool(Key + "shared", false))
                    {
                        var parent = screen.Workspace.Session;
                        parent.SelectLevel(parent.Documents.First(doc => doc.Kind == "level" && (int)doc.Data["levelNumber"] == 2).Id);
                        TutorialDraftEditing.EditLevel(parent, "shared reload", level =>
                        {
                            level.Tutorial.flow = null; level.Tutorial.bindings.Clear(); level.Tutorial.steps.Clear();
                            level.Tutorial.steps.Add(new TutorialStepDefinition { authoringId = "reload-step", instructions = "original" });
                        });
                        string flowId = TutorialDraftEditing.CreateFlow(parent, "reload flow");
                        var draft = new SharedTutorialDraft(parent);
                        TutorialDraftEditing.EditFlow(draft.Session, flowId, "local edit", flow => flow.steps[0].instructions = "pending shared text");
                        TutorialDraftEditing.EditFlow(draft.Session, flowId, "redo edit", flow => flow.steps[0].instructions = "redo text");
                        draft.Session.Undo();
                        typeof(LevelToolScreen).GetField("sharedTutorialDraft", Flags).SetValue(screen, draft);
                        typeof(LevelToolScreen).GetField("inspectorPage", Flags).SetValue(screen, "튜토리얼");
                        File.WriteAllText(expected + ".shared", draft.ExportState());
                    }
                    else
                    {
                        typeof(LevelToolScreen).GetField("inspectorPage", Flags).SetValue(screen, "흐름");
                        typeof(LevelToolScreen).GetField("flowTool", Flags).SetValue(screen, "path");
                        ((System.Collections.Generic.List<int>)typeof(LevelToolScreen).GetField("flowRoute", Flags).GetValue(screen)).AddRange(new[] { 20, 21 });
                    }
                    typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
                    if (SessionState.GetBool(Key + "shared", false))
                    {
                        typeof(LevelToolScreen).GetMethod("BeginTutorialPick", Flags).Invoke(screen, new object[] { "highlights", null, null, null });
                        typeof(LevelToolScreen).GetMethod("UseTutorialPicker", Flags).Invoke(screen, new object[] { 20 });
                        typeof(LevelToolScreen).GetMethod("UseTutorialPicker", Flags).Invoke(screen, new object[] { 21 });
                        var pending = (Newtonsoft.Json.Linq.JObject)typeof(LevelToolScreen).GetField("tutorialPick", Flags).GetValue(screen);
                        File.WriteAllText(expected + ".pick", pending.ToString());
                    }
                    File.WriteAllText(expected, screen.Workspace.Session.ExportState());
                    SessionState.SetInt(Key + "phase", 1);
                    EditorUtility.RequestScriptReload();
                }
                else if (phase == 1 || phase == 4)
                {
                    Button restore = screen.GetComponent<UIDocument>().rootVisualElement.Q<Button>("restore-draft");
                    if (restore == null) return;
                    typeof(Clickable).GetMethod("Invoke", Flags).Invoke(restore.clickable, new object[] { null });
                    if (screen.Workspace.Session.ExportState() != File.ReadAllText(expected))
                        throw new Exception("reload changed draft, cells, undo or redo");
                    if (SessionState.GetBool(Key + "shared", false))
                    {
                        var draft = (SharedTutorialDraft)typeof(LevelToolScreen).GetField("sharedTutorialDraft", Flags).GetValue(screen);
                        if (draft == null || draft.ExportState() != File.ReadAllText(expected + ".shared")) throw new Exception("FAIL shared draft/history lost on reload");
                        if (screen.GetComponent<UIDocument>().rootVisualElement.Q<TextField>("tutorial-instructions")?.value != "pending shared text") throw new Exception("FAIL shared editing UI not restored");
                        if ((string)screen.Workspace.Session.Get(draft.FlowId).Data["steps"][0]["instructions"] != "original") throw new Exception("FAIL reload applied draft without consent");
                        Debug.Log("PASS shared draft remains isolated and restores its UI and undo/redo");
                        var pending = (Newtonsoft.Json.Linq.JObject)typeof(LevelToolScreen).GetField("tutorialPick", Flags).GetValue(screen);
                        var root = screen.GetComponent<UIDocument>().rootVisualElement;
                        if (pending?.ToString() != File.ReadAllText(expected + ".pick") || root.Q("tutorial-pick-panel") == null)
                            throw new Exception("FAIL pending tutorial selection lost on reload");
                        Debug.Log("PASS pending tutorial selection and confirm UI restored");
                        if (phase == 4)
                        {
                            var confirm = root.Q<Button>("tutorial-pick-confirm");
                            typeof(Clickable).GetMethod("Invoke", Flags).Invoke(confirm.clickable, new object[] { null });
                            if (draft.Session.Get(draft.FlowId).Data["steps"][0]["highlights"].Count() != 2 ||
                                screen.Workspace.Session.Get(draft.FlowId).Data["steps"][0]["highlights"].Any())
                                throw new Exception("FAIL restored picker does not apply only to shared draft");
                            Debug.Log("PASS restored pending selection applies to isolated shared draft");
                        }
                    }
                    else if ((string)typeof(LevelToolScreen).GetField("flowTool", Flags).GetValue(screen) != "path" ||
                        ((System.Collections.Generic.List<int>)typeof(LevelToolScreen).GetField("flowRoute", Flags).GetValue(screen)).Count != 2)
                        throw new Exception("FAIL reload lost unfinished flow input");
                    Debug.Log(phase == 1 ? "PASS actual script reload restores draft/cells/undo/redo" : "PASS Play stop and restart restores draft/cells/undo/redo");
                    SessionState.SetInt(Key + "phase", phase == 1 ? 3 : 5);
                    EditorApplication.ExitPlaymode();
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key + "active", false);
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + "enabled", false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Key + "options", 0);
            EditorApplication.Exit(code);
        }
    }
}
