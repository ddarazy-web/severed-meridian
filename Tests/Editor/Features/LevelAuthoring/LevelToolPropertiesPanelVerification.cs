using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolPropertiesPanelVerification
    {
        private sealed class Host : EditorWindow { }
        public static void Run()
        {
            int exit = 0;
            IDisposable panel = null;
            Host host = null;
            try
            {
                Type type = typeof(LevelToolScreen).Assembly.GetType("LevelTool.Presentation.LevelToolPropertiesPanel");
                Check(type != null, "properties panel owns its presentation lifetime");
                Type optionsType = type.Assembly.GetType("LevelTool.Presentation.LevelToolPropertiesOptions");
                Type actionsType = type.Assembly.GetType("LevelTool.Presentation.LevelToolPropertiesActions");
                object options = Activator.CreateInstance(optionsType);
                object actions = Activator.CreateInstance(actionsType);
                actionsType.GetField("SetColor").SetValue(actions, (Action<string>)(value => optionsType.GetField("Color").SetValue(options, value)));
                actionsType.GetField("SetDirection").SetValue(actions, (Action<string>)(value => optionsType.GetField("Direction").SetValue(options, value)));
                AuthoringToolWorkspace workspace = new AuthoringToolWorkspace();
                workspace.Create(Path.GetFullPath("Logs/LevelToolRefactorStage03/properties-" + Guid.NewGuid().ToString("N")), LevelToolDocuments.NewProject());
                AuthoringEditSession session = workspace.Session;
                int commands = 0;
                Action<string, Action<JObject>> change = (label, edit) => { commands++; session.EditLevel(label, edit); };
                MethodInfo getterMethod = typeof(LevelToolPropertiesPanelVerification).GetMethod(nameof(Getter), BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(optionsType);
                object getter = getterMethod.Invoke(null, new[] { options });
                panel = (IDisposable)Activator.CreateInstance(type, (Func<AuthoringEditSession>)(() => session), change, getter, actions);
                VisualElement root = (VisualElement)type.GetProperty("Root").GetValue(panel);
                host = ScriptableObject.CreateInstance<Host>(); host.Show(); host.rootVisualElement.Add(root);
                string[] selectedIds = Array.Empty<string>();
                void Draw() => type.GetMethod("Refresh").Invoke(panel, new object[] { session.Get(session.SelectedLevelId).Data, LevelToolDocuments.Definitions(session), selectedIds, null });
                void Click(string key) => typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(root.Q<Button>(key).clickable, new object[] { null });
                string original = session.Get(session.SelectedLevelId).Data.ToString();
                Draw();
                Check(root.Q("level-settings") != null && root.Q("selection-properties") != null, "settings and selection visibility roots retain their names");
                TextField name = root.Q<TextField>("level-name"); name.value = "속성 분리 검사";
                Check(commands == 1 && (string)session.Get(session.SelectedLevelId).Data["displayName"] == "속성 분리 검사", "name change uses existing document command once");
                session.Undo();
                Check(session.Get(session.SelectedLevelId).Data.ToString() == original, "property edit undo restores exact document");
                Draw(); host.rootVisualElement.Add(name); name.value = "이전 콜백";
                Check(commands == 1, "old property callbacks cannot edit after refresh");
                var definitions = LevelToolDocuments.Definitions(session);
                string obstacle = definitions.First(pair => LevelToolDocuments.Layer(pair.Value) == "Obstacle" && LevelToolDocuments.Size(pair.Value) == 1).Key;
                session.EditLevel("검사 장애물", data => LevelDocumentEditing.Place(data, definitions, obstacle, 4, 4, false));
                selectedIds = new[] { (string)session.Get(session.SelectedLevelId).Data["elements"][0]["instanceId"] };
                Draw();
                string beforeDurability = session.Get(session.SelectedLevelId).Data.ToString();
                int nextDurability = root.Q<IntegerField>("selected-durability").value + 1;
                root.Q<IntegerField>("selected-durability").value = nextDurability;
                string afterDurability = session.Get(session.SelectedLevelId).Data.ToString();
                Check((int)session.Get(session.SelectedLevelId).Data["elements"][0]["durability"] == nextDurability, "selected durability writes through common edit command");
                session.Undo(); Check(session.Get(session.SelectedLevelId).Data.ToString() == beforeDurability, "durability edit has one Undo");
                session.Redo(); Check(session.Get(session.SelectedLevelId).Data.ToString() == afterDurability, "durability Redo restores exact document");
                Draw();
                root.Query<DropdownField>().ToList().First(field => field.label == "배치 색").value = "Type3";
                root.Query<DropdownField>().ToList().First(field => field.label == "로켓 방향").value = "Vertical";
                Click("apply-color-direction");
                JObject element = (JObject)session.Get(session.SelectedLevelId).Data["elements"][0];
                Check((string)element["color"] == "Type3" && (string)element["rocketDirection"] == "Vertical", "appearance uses current shared placement settings without a redraw");
                session.Undo(); Check(session.Get(session.SelectedLevelId).Data.ToString() == afterDurability, "appearance is one Undo");
                Draw(); Click("add-mission"); Draw();
                string beforeMission = session.Get(session.SelectedLevelId).Data.ToString();
                root.Query<IntegerField>().ToList().Last(field => field.label == "목표 수량").value = 37;
                string afterMission = session.Get(session.SelectedLevelId).Data.ToString();
                Check((int)((JArray)session.Get(session.SelectedLevelId).Data["missions"]).Last["count"] == 37, "mission count remains editable through the panel");
                session.Undo(); Check(session.Get(session.SelectedLevelId).Data.ToString() == beforeMission, "mission count has one Undo");
                session.Redo(); Check(session.Get(session.SelectedLevelId).Data.ToString() == afterMission, "mission Redo restores exact document");
                int beforeDispose = commands;
                TextField current = root.Q<TextField>("level-name");
                panel.Dispose(); host.rootVisualElement.Add(current); current.value = "해제 후 변경";
                Check(commands == beforeDispose && root.childCount == 0, "disposed properties cannot edit the session");
            }
            catch (Exception error) { Debug.LogException(error); exit = 1; }
            finally { panel?.Dispose(); if (host != null) host.Close(); }
            EditorApplication.Exit(exit);
        }
        private static Func<T> Getter<T>(object value) => () => (T)value;
        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("FAIL " + message);
            Debug.Log("PASS " + message);
        }
    }
}
