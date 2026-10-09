using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using LevelTool;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolRecoveryExercise
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void Require()
        {
            if (typeof(LevelToolScreen).GetMethod("RequestLeave", Flags) == null)
                throw new Exception("FAIL runtime tool exit protection missing");
        }
        internal static async UniTask Run(LevelToolScreen screen)
        {
            string id = screen.Workspace.Session.SelectedLevelId;
            screen.Workspace.Session.SelectCells(new[] { 40 });
            screen.Workspace.Session.Apply("복구 검사", docs => docs[id].Data["moveCount"] = 34);
            typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
            string draft = screen.Workspace.Session.ExportState();
            screen.enabled = false;
            screen.enabled = true;
            await Idle(screen);
            Check(screen.GetComponent<UIDocument>().rootVisualElement.Q("restore-draft") == null &&
                screen.Workspace.Session.ExportState() == draft, "disable and enable retains live draft selection and history without recovery prompt");
            int leaves = 0;
            Action leave = () => leaves++;
            typeof(LevelToolScreen).GetMethod("RequestLeave", Flags).Invoke(screen, new object[] { leave });
            Click(screen, "cancel");
            Check(leaves == 0 && screen.Workspace.Session.IsDirty, "cancel leaving keeps editor and dirty draft");
            typeof(LevelToolScreen).GetMethod("RequestLeave", Flags).Invoke(screen, new object[] { leave });
            Click(screen, "save-and-leave"); await Idle(screen);
            Check(leaves == 1 && !screen.Workspace.Session.IsDirty, "save and leave persists before continuation");
            screen.Workspace.Session.Apply("버리기 검사", docs => docs[id].Data["moveCount"] = 35);
            typeof(LevelToolScreen).GetMethod("RequestLeave", Flags).Invoke(screen, new object[] { leave });
            Click(screen, "discard-and-leave"); await Idle(screen);
            Check(leaves == 2 && !screen.Workspace.Session.IsDirty && !screen.Workspace.Session.CanUndo,
                "discard and leave removes draft history");
        }
        private static void Click(LevelToolScreen screen, string name)
        {
            Button button = screen.GetComponent<UIDocument>().rootVisualElement.Q<Button>(name) ?? throw new Exception("Missing " + name);
            typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button.clickable, new object[] { null });
        }
        private static async UniTask Idle(LevelToolScreen screen)
        {
            await UniTask.Delay(100);
            for (int i = 0; i < 300 && (bool)typeof(LevelToolScreen).GetField("busy", Flags).GetValue(screen); i++) await UniTask.Delay(50);
            Check(!(bool)typeof(LevelToolScreen).GetField("busy", Flags).GetValue(screen), "recovery IO completes");
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}
