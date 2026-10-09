using System;
using System.IO;
using System.Reflection;
using AutoPlay;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    [InitializeOnLoad]
    public static class LevelToolTrialLifecycleVerification
    {
        private const string Key = "LevelToolTrialLifecycle.";
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static AsyncOperation sceneUnload;
        static LevelToolTrialLifecycleVerification() { if (SessionState.GetBool(Key + "active", false)) Hook(); }
        public static void Run()
        {
            SessionState.SetBool(Key + "active", true);
            SessionState.SetBool(Key + "enabled", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Key + "options", (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetString(Key + "deadline", DateTime.UtcNow.AddMinutes(5).ToString("O"));
            SessionState.SetInt(Key + "phase", 0);
            string folder = Path.GetFullPath("Logs/GameAuthoringStage05/trial-lifecycle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); SessionState.SetString(Key + "folder", folder);
            EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(LevelTool.Editor.LevelToolAssets.ScenePath); LevelToolRecoveryIsolation.Configure(folder);
            Hook(); EditorApplication.EnterPlaymode();
        }
        private static void Hook() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }
        private static void Click(VisualElement root, string name)
        {
            var button = root.Q<Button>(name) ?? throw new Exception("Missing " + name);
            typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
        }
        private static T Field<T>(LevelToolScreen screen, string name) => (T)typeof(LevelToolScreen).GetField(name, Flags).GetValue(screen);
        private static void Tick()
        {
            try
            {
                if (DateTime.UtcNow > DateTime.Parse(SessionState.GetString(Key + "deadline", ""))) throw new Exception("trial lifecycle timeout");
                int phase = SessionState.GetInt(Key + "phase", 0);
                if (phase == 4 && !EditorApplication.isPlayingOrWillChangePlaymode)
                { SessionState.SetInt(Key + "phase", 5); EditorApplication.EnterPlaymode(); return; }
                if (phase == 8 && !EditorApplication.isPlayingOrWillChangePlaymode) { Finish(0); return; }
                if (!EditorApplication.isPlaying) return;
                if (phase == 7)
                {
                    if (sceneUnload == null || !sceneUnload.isDone) return;
                    if (UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>() != null) throw new Exception("FAIL tool scene was not unloaded");
                    string savedFolder = SessionState.GetString(Key + "folder", "");
                    var raw = JsonUtility.FromJson<BotBatchRecord>(File.ReadAllText(Path.Combine(SessionState.GetString(Key + "sceneBatch", ""), "batch.json")));
                    if (raw.status != BotBatchStatus.Interrupted) throw new Exception("FAIL scene unload did not durably interrupt batch");
                    var record = new LevelAuthoring.Storage.ToolDraftStore(savedFolder).Read();
                    var restored = new LevelAuthoring.Editing.AuthoringToolWorkspace(); restored.RestoreState((string)record["workspace"]);
                    if (restored.Session.ExportState() != File.ReadAllText(Path.Combine(savedFolder, "expected.txt"))) throw new Exception("FAIL scene unload lost authoring history");
                    Debug.Log("PASS actual scene unload durably interrupts trial and preserves draft/selection/undo/redo");
                    SessionState.SetInt(Key + "phase", 8); EditorApplication.ExitPlaymode(); return;
                }
                var screen = UnityEngine.Object.FindFirstObjectByType<LevelToolScreen>();
                if (screen == null || Field<bool>(screen, "busy")) return;
                string folder = SessionState.GetString(Key + "folder", ""); LevelToolRecoveryIsolation.Verify(screen, folder);
                var root = screen.GetComponent<UIDocument>().rootVisualElement;
                if (phase == 0)
                {
                    screen.Workspace.Open(File.ReadAllText("Logs/GameAuthoringStage02/latest-fixtures.txt"), "Cancel");
                    typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
                    Click(root, "new-level"); var edit = screen.Workspace.Session; string id = edit.SelectedLevelId;
                    edit.Apply("short lifecycle trial", docs => {
                        docs[id].Data["moveCount"] = 1;
                        docs[id].Data["missions"] = new JArray(new JObject { ["kind"] = "Color", ["color"] = "Type1", ["count"] = 99 });
                    });
                    edit.Apply("redo lifecycle", docs => docs[id].Data["moveCount"] = 2); edit.Undo(); edit.SelectCells(new[] { 20, 21 });
                    typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
                    root.Q<DropdownField>("inspector-page").value = "자동 시험";
                    root.Q<IntegerField>("batch-count").value = 113;
                    root.Q<IntegerField>("multi-samples").value = 127;
                    root.Q<Toggle>("multi-level-" + id).value = true;
                    Click(root, "batch-new"); SessionState.SetInt(Key + "phase", 1);
                }
                else if (phase == 1)
                {
                    var run = Field<PersistedBotBatch>(screen, "toolBatch"); if (run.Games.Count == 0) return;
                    File.WriteAllText(Path.Combine(folder, "expected.txt"), screen.Workspace.Session.ExportState());
                    SessionState.SetString(Key + "batch", Path.Combine(folder, "Trials", run.Record.id));
                    SessionState.SetInt(Key + "savedGames", run.Games.Count);
                    SessionState.SetInt(Key + "phase", 2); EditorUtility.RequestScriptReload();
                }
                else if (phase == 2 || phase == 5)
                {
                    if (root.Q<Button>("restore-draft") == null) return;
                    Click(root, "restore-draft");
                    if (screen.Workspace.Session.ExportState() != File.ReadAllText(Path.Combine(folder, "expected.txt"))) throw new Exception("FAIL trial exit lost draft or undo/redo");
                    if (Field<int>(screen, "batchCount") != 113 || Field<int>(screen, "multiSamples") != 127 ||
                        !root.Q<Toggle>("multi-level-" + screen.Workspace.Session.SelectedLevelId).value)
                        throw new Exception("FAIL trial configuration and selected levels not restored");
                    if (phase == 2)
                    {
                        string directory = SessionState.GetString(Key + "batch", "");
                        var raw = JsonUtility.FromJson<BotBatchRecord>(File.ReadAllText(Path.Combine(directory, "batch.json")));
                        if (raw.status != BotBatchStatus.Interrupted || raw.finished < SessionState.GetInt(Key + "savedGames", 0)) throw new Exception("FAIL reload did not durably interrupt batch");
                        var source = LevelAuthoring.Storage.AuthoringTrialSource.Decode(File.ReadAllText(Path.Combine(directory, "source.context")));
                        using (var graph = new LevelAuthoring.Runtime.AuthoringObjectGraph(source.Snapshot.Documents))
                        {
                            var reader = new BotAnalysisReader(directory, (Levels.LevelDefinition)graph.Resolve(source.LevelId));
                            while (!reader.IsDone) reader.Advance();
                            if (reader.Error != null) throw new Exception("FAIL reload batch records: " + reader.Error);
                        }
                        Debug.Log("PASS script reload durably interrupts trial and restores settings/draft/history");
                        Click(root, "multi-start"); SessionState.SetInt(Key + "phase", 3);
                    }
                    else
                    {
                        string path = SessionState.GetString(Key + "queue", "");
                        var raw = JsonUtility.FromJson<MultiLevelTestRecord>(File.ReadAllText(path));
                        if (raw.entries[0].status != MultiLevelTestStatus.Interrupted || string.IsNullOrEmpty(raw.entries[0].resultId)) throw new Exception("FAIL Play stop queue not durably interrupted");
                        Debug.Log("PASS Play stop durably interrupts multilevel trial and restores draft/settings");
                        Click(root, "batch-new"); SessionState.SetInt(Key + "phase", 6);
                    }
                }
                else if (phase == 3)
                {
                    var run = Field<PersistedMultiLevelTest>(screen, "toolMulti");
                    if (run.Current == null) return;
                    SessionState.SetString(Key + "queue", Path.Combine(folder, "MultiLevelTrials", run.Record.id, "queue.json"));
                    SessionState.SetInt(Key + "phase", 4); EditorApplication.ExitPlaymode();
                }
                else if (phase == 6)
                {
                    var run = Field<PersistedBotBatch>(screen, "toolBatch");
                    if (run.Current == null) return;
                    SessionState.SetString(Key + "sceneBatch", Path.Combine(folder, "Trials", run.Record.id));
                    var previous = screen.gameObject.scene;
                    var empty = UnityEngine.SceneManagement.SceneManager.CreateScene("Lifecycle verification destination");
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(empty);
                    sceneUnload = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(previous);
                    SessionState.SetInt(Key + "phase", 7);
                }
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        private static void Finish(int code)
        {
            EditorApplication.update -= Tick; SessionState.SetBool(Key + "active", false);
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + "enabled", false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Key + "options", 0);
            EditorApplication.Exit(code);
        }
    }
}
