using System;
using System.Linq;
using System.Reflection;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolFlowExercise
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static void Run(LevelToolScreen screen)
        {
            VisualElement root = screen.GetComponent<UIDocument>().rootVisualElement;
            void Click(string name)
            {
                Button button = root.Q<Button>(name) ?? throw new Exception("Missing flow button " + name);
                typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
            }
            void Pointer(int cell)
            {
                VisualElement tile = root.Q("cell-" + cell);
                using PointerDownEvent click = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0 });
                click.target = tile; tile.SendEvent(click);
            }
            void Refresh() => typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
            var session = screen.Workspace.Session;
            string originalId = session.SelectedLevelId, original = session.Get(originalId).Data.ToString();
            Click("new-level");
            DropdownField page = root.Q<DropdownField>("inspector-page");
            Check(page.choices.Contains("흐름"), "flow inspector exists"); page.value = "흐름";
            JObject Data() => session.Get(session.SelectedLevelId).Data;
            session.SelectCells(new[] { 10, 11 }); Refresh(); Click("gravity-right");
            Check(Data()["flow"]["gravity"].Count() == 2, "region gravity common editing");
            session.SelectCells(new[] { 11 }); Refresh(); Click("merge-up-1");
            Check((int)Data()["flow"]["merges"][0]["sources"][0]["row"] == 1, "merge source priority reordered");
            Click("merge-reset"); Check(!Data()["flow"]["merges"].Any(), "merge priority reset");
            Click("path-start"); Pointer(30); Pointer(31); Pointer(32); Click("path-finish");
            Check(Data()["flow"]["paths"].Count() == 3 && (bool)Data()["flow"]["paths"].Last()["isEnd"], "board path with explicit end");
            Click("undo"); Check(!Data()["flow"]["paths"].Any(), "path one undo"); Click("redo");
            Click("portal-start"); Pointer(40);
            Check(!(bool)Data()["flow"]["portals"][0]["hasExit"], "unfinished portal entrance retained");
            Pointer(60); Check((bool)Data()["flow"]["portals"][0]["hasExit"], "portal exit completes pair");
            Click("wall-start"); Pointer(50); Pointer(51);
            Check(Data()["flow"]["walls"].Count() == 1, "wall between adjacent cells");
            Click("wall-erase"); Pointer(50); Pointer(51); Check(!Data()["flow"]["walls"].Any(), "wall erased with same geometry");
            Click("tool-cancel"); session.SelectCells(new[] { 72 }); Refresh(); Click("arrival-add");
            Check(Data()["flow"]["arrivals"].Count() == 1, "arrival added from board selection");
            Check(root.Q("flow-overlay") != null, "flow overlay present");
            while (session.CanUndo) session.Undo(); Refresh(); root.Q<DropdownField>("inspector-page").value = "기본";
            Check(session.SelectedLevelId == originalId && session.Get(originalId).Data.ToString() == original && !session.IsDirty, "flow undo preserves original document");
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}
