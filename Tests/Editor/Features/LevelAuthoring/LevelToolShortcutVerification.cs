using System;
using System.Reflection;
using LevelTool;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    public static class LevelToolShortcutVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        public static void Run()
        {
            GameObject owner = null;
            try
            {
                Type type = typeof(LevelToolScreen);
                Type commandType = type.GetNestedType("ToolCommandId", BindingFlags.NonPublic | BindingFlags.Public);
                Check(commandType != null, "common command identifiers exist");
                owner = new GameObject("Shortcut verification"); owner.SetActive(false);
                LevelToolScreen screen = owner.AddComponent<LevelToolScreen>();
                VisualElement root = new VisualElement();
                Set(screen, "root", root); Set(screen, "editor", new VisualElement()); Set(screen, "status", new Label());
                VisualElement menu = new VisualElement(); root.Add(menu);
                Call(screen, "BuildCommandMenus", menu);
                Check(menu.Q("menu-file") != null && menu.Q("menu-edit") != null && menu.Q("menu-view") != null &&
                    menu.Q("menu-test") != null && menu.Q("menu-help") != null, "five menus share command definitions");
                Check(!Allowed(screen, commandType, "Save") && !Allowed(screen, commandType, "Undo"), "empty workspace disables save and undo");
                Check(Allowed(screen, commandType, "Open") && Allowed(screen, commandType, "Help"), "empty workspace can open and ask for help");
                Check(!Allowed(screen, commandType, "Windowed") && !Allowed(screen, commandType, "Fullscreen"), "standalone window commands cannot resize Unity Editor");
                Set(screen, "busy", true);
                Check(!Allowed(screen, commandType, "Open") && !Allowed(screen, commandType, "Play"), "busy blocks file and play commands");
                Set(screen, "busy", false);
                Call(screen, "TryExecuteCommand", Enum.Parse(commandType, "Help"));
                Check(root.Q("shortcut-search") != null, "help is searchable");
                Check(!Allowed(screen, commandType, "Open") && !Allowed(screen, commandType, "Save"), "modal prevents background commands");
                Key(screen, KeyCode.Escape, EventModifiers.None, true);
                Check(root.Q("modal") == null, "escape closes the top modal");
                Key(screen, KeyCode.Escape, EventModifiers.None, false);
                Key(screen, KeyCode.F1, EventModifiers.None, true);
                VisualElement firstModal = root.Q("modal");
                Key(screen, KeyCode.F1, EventModifiers.None, true);
                Check(firstModal != null && ReferenceEquals(firstModal, root.Q("modal")), "held key does not reopen help");
                Key(screen, KeyCode.F1, EventModifiers.None, false);
                Call(screen, "CloseModal");
                Call(screen, "RegisterShortcuts"); Call(screen, "UnregisterShortcuts"); Call(screen, "RegisterShortcuts");
                Call(screen, "UnregisterShortcuts");
                Check(root.Q("modal") == null, "register and unregister have no command side effects");
                Debug.Log("PASS level tool shortcut command boundaries"); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
            finally { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); }
        }
        private static bool Allowed(LevelToolScreen screen, Type commandType, string name)
        {
            object[] arguments = { Enum.Parse(commandType, name), null };
            bool allowed = (bool)typeof(LevelToolScreen).GetMethod("CanExecuteCommand", Flags).Invoke(screen, arguments);
            Check(allowed || !string.IsNullOrEmpty((string)arguments[1]), name + " has a disabled reason");
            return allowed;
        }
        private static void Key(LevelToolScreen screen, KeyCode key, EventModifiers modifiers, bool down)
        {
            Event source = new Event { type = down ? EventType.KeyDown : EventType.KeyUp, keyCode = key, modifiers = modifiers };
            if (down) { using (KeyDownEvent evt = KeyDownEvent.GetPooled(source)) Call(screen, "OnShortcutKeyDown", evt); }
            else { using (KeyUpEvent evt = KeyUpEvent.GetPooled(source)) Call(screen, "OnShortcutKeyUp", evt); }
        }
        private static object Call(LevelToolScreen screen, string name, params object[] arguments) => typeof(LevelToolScreen).GetMethod(name, Flags).Invoke(screen, arguments);
        private static void Set(LevelToolScreen screen, string name, object value) => typeof(LevelToolScreen).GetField(name, Flags).SetValue(screen, value);
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}
