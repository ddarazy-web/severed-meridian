using System;
using System.IO;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolSupplyPanelVerification
    {
        private sealed class Host : EditorWindow { }
        public static void Run()
        {
            int exit = 0;
            IDisposable panel = null;
            Host host = null;
            try
            {
                Type type = typeof(LevelToolScreen).Assembly.GetType("LevelTool.Presentation.LevelToolSupplyPanel");
                Check(type != null, "supply panel owns its presentation lifetime");
                Type stateType = type.Assembly.GetType("LevelTool.Presentation.LevelToolSupplyState");
                object state = Activator.CreateInstance(stateType);
                AuthoringToolWorkspace workspace = new AuthoringToolWorkspace();
                workspace.Create(Path.GetFullPath("Logs/LevelToolRefactorStage03/supply-" + Guid.NewGuid().ToString("N")), LevelToolDocuments.NewProject());
                AuthoringEditSession session = workspace.Session;
                session.SelectCells(new[] { 0 });
                int commands = 0;
                Action<string, Action<JObject>> change = (label, edit) => { commands++; session.EditLevel(label, edit); };
                panel = (IDisposable)Activator.CreateInstance(type, (Func<AuthoringEditSession>)(() => session), change, state);
                VisualElement root = (VisualElement)type.GetProperty("Root").GetValue(panel);
                host = ScriptableObject.CreateInstance<Host>(); host.Show();
                host.rootVisualElement.Add(root);
                MethodInfo refresh = type.GetMethod("Refresh");
                void Draw() => refresh.Invoke(panel, new object[] { session.Get(session.SelectedLevelId).Data, LevelToolDocuments.Definitions(session), false });
                string before = session.ExportState();
                Draw();
                Check(session.ExportState() == before, "supply rendering preserves document and undo history");
                DropdownField old = root.Q<DropdownField>("source-mode");
                Check(old != null, "selected source retains its existing named mode control");
                old.value = "고정 순서";
                Check(commands == 1 && (string)session.Get(session.SelectedLevelId).Data["elementSupply"]["sources"][0]["mode"] == "Fixed", "source edit uses one existing document command");
                session.Undo();
                Check((string)session.Get(session.SelectedLevelId).Data["elementSupply"]["sources"][0]["mode"] == "Random", "source mode undo restores previous value");
                Draw();
                host.rootVisualElement.Add(old);
                old.value = "고철 수량 유지";
                Check(commands == 1, "detached source control cannot issue a stale edit");
                root.Q<IntegerField>("supply-add-count").value = 7;
                Draw();
                Check(root.Q<IntegerField>("supply-add-count").value == 7, "supply draft count survives refresh");
                IntegerField staleCount = root.Q<IntegerField>("supply-add-count");
                panel.Dispose(); panel.Dispose();
                host.rootVisualElement.Add(staleCount);
                staleCount.value = 9;
                Check((int)stateType.GetField("Count").GetValue(state) == 7, "disposed panel cannot change retained draft state");
                Check(root.childCount == 0, "supply disposal clears presentation");
                panel = (IDisposable)Activator.CreateInstance(type, (Func<AuthoringEditSession>)(() => session), change, state);
                root = (VisualElement)type.GetProperty("Root").GetValue(panel); host.rootVisualElement.Add(root);
                Draw();
                Check(root.Q<IntegerField>("supply-add-count").value == 7, "recreated supply panel retains draft inputs without retaining old UI");
            }
            catch (Exception error) { Debug.LogException(error); exit = 1; }
            finally { panel?.Dispose(); if (host != null) host.Close(); }
            EditorApplication.Exit(exit);
        }

        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("FAIL " + message);
            Debug.Log("PASS " + message);
        }
    }
}
