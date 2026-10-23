using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelTool;
using LevelTool.Presentation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolNativeMissionVerification
    {
        private sealed class Host : EditorWindow { }
        public static void Run()
        {
            int exit = 0;
            Host host = null;
            LevelToolPropertiesPanel panel = null;
            string folder = Path.GetFullPath("Logs/NativeLevelToolStage03/mission-" + Guid.NewGuid().ToString("N"));
            try
            {
                AuthoringToolWorkspace workspace = new AuthoringToolWorkspace();
                workspace.Create(folder, LevelToolTestProject.Create());
                AuthoringEditSession session = workspace.Session;
                Type checklist = typeof(LevelToolScreen).Assembly.GetType("LevelTool.Presentation.LevelToolCreationChecklist");
                Check(checklist != null, "creation checklist separates configuration from play/save status");
                object ReadChecklist(bool played, bool dirty) => Activator.CreateInstance(checklist, session.Get(session.SelectedLevelId).Data, played, dirty);
                bool Flag(object value, string name) => (bool)checklist.GetProperty(name).GetValue(value);
                object initialChecklist = ReadChecklist(false, false);
                Check(!Flag(initialChecklist, "PlayChecked") && Flag(initialChecklist, "Saved"), "saved level does not imply play verified");
                Check(!Flag(initialChecklist, "MissionsConfigured"), "empty mission list requires configuration");
                panel = new LevelToolPropertiesPanel(() => session, (label, edit) => session.EditLevel(label, edit),
                    () => new LevelToolPropertiesOptions(), new LevelToolPropertiesActions());
                host = ScriptableObject.CreateInstance<Host>(); host.Show(); host.rootVisualElement.Add(panel.Root);
                void Draw() => panel.Refresh(session.Get(session.SelectedLevelId).Data, new Dictionary<string, JObject>(), Array.Empty<string>(), null);
                Draw();
                string baseline = session.Get(session.SelectedLevelId).Data.ToString();
                Click(panel.Root, "add-mission");
                Check(session.Get(session.SelectedLevelId).Data.ToString() == baseline, "opening mission draft does not mutate the level");
                Check(panel.Root.Q<DropdownField>("mission-purpose") != null, "purpose-first mission editor is available");
                IntegerField count = panel.Root.Q<IntegerField>("mission-count");
                count.value = 0;
                Check(!panel.Root.Q<Button>("apply-mission").enabledSelf, "invalid count blocks Apply");
                count.value = 30;
                Click(panel.Root, "apply-mission");
                Check(session.IsDirty && (int)session.Get(session.SelectedLevelId).Data["missions"][0]["count"] == 30, "apply creates one dirty mission");
                object configuredChecklist = ReadChecklist(false, true);
                Check(Flag(configuredChecklist, "MissionsConfigured") && !Flag(configuredChecklist, "PlayChecked") && !Flag(configuredChecklist, "Saved"), "valid missions do not imply play or save completion");
                session.Undo(); Check(session.Get(session.SelectedLevelId).Data.ToString() == baseline, "one Undo restores mission baseline");
                session.Redo(); Draw();
                Check(panel.Root.Q<Label>("mission-summary-0").text.Contains("30"), "mission summary includes target count");
                Click(panel.Root, "edit-mission-0");
                panel.Root.Q<IntegerField>("mission-count").value = 99;
                Click(panel.Root, "cancel-mission");
                Check((int)session.Get(session.SelectedLevelId).Data["missions"][0]["count"] == 30, "cancel keeps the existing mission");
                Draw(); Click(panel.Root, "remove-mission-0");
                Check(session.Get(session.SelectedLevelId).Data["missions"].Count() == 0, "remove mission remains supported");
                Draw(); Click(panel.Root, "add-mission");
                panel.Root.Q<DropdownField>("mission-purpose").index = 1;
                panel.Root.Q<IntegerField>("mission-count").value = 6;
                Click(panel.Root, "apply-mission");
                Check((string)session.Get(session.SelectedLevelId).Data["missions"][0]["kind"] == "Crate", "obstacle purpose creates the selected removal target");
                Draw(); Click(panel.Root, "edit-mission-0");
                panel.Root.Q<IntegerField>("mission-count").value = 8;
                Click(panel.Root, "apply-mission");
                Check((int)session.Get(session.SelectedLevelId).Data["missions"][0]["count"] == 8, "existing mission edit applies count");
                Draw(); Click(panel.Root, "add-mission");
                panel.Root.Q<DropdownField>("mission-purpose").index = 1;
                Check(!panel.Root.Q<Button>("apply-mission").enabledSelf, "duplicate obstacle target is blocked");
                panel.Root.Q<DropdownField>("mission-purpose").index = 0;
                Button staleApply = panel.Root.Q<Button>("apply-mission");
                Click(panel.Root, "cancel-mission");
                host.rootVisualElement.Add(staleApply);
                typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(staleApply.clickable, new object[] { null });
                Check(session.Get(session.SelectedLevelId).Data["missions"].Count() == 1, "cancelled draft callbacks cannot apply later");
            }
            catch (Exception error) { Debug.LogException(error); exit = 1; }
            finally
            {
                panel?.Dispose(); if (host != null) host.Close();
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
            EditorApplication.Exit(exit);
        }
        private static void Click(VisualElement root, string name)
        {
            Button button = root.Q<Button>(name);
            Check(button != null && button.enabledInHierarchy, "enabled action " + name);
            typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button.clickable, new object[] { null });
        }
        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("FAIL " + message);
            Debug.Log("PASS " + message);
        }
    }
}
