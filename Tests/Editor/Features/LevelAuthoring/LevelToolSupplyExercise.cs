using System;
using System.Linq;
using System.Reflection;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolSupplyExercise
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static void Run(LevelToolScreen screen)
        {
            VisualElement root = screen.GetComponent<UIDocument>().rootVisualElement;
            void Click(string name)
            {
                Button button = root.Q<Button>(name) ?? throw new Exception("Missing supply button " + name);
                typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
            }
            void Refresh() => typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
            var session = screen.Workspace.Session;
            string originalId = session.SelectedLevelId, original = session.Get(session.SelectedLevelId).Data.ToString();
            Click("new-level");
            session.SelectCells(new[] { 0, 1 }); Refresh();
            DropdownField page = root.Q<DropdownField>("inspector-page");
            Check(page != null, "advanced inspector pages exist");
            page.value = "공급";
            root.Q<DropdownField>("source-mode").value = "고정 순서";
            JObject Data() => session.Get(session.SelectedLevelId).Data;
            Check(Data()["elementSupply"]["sources"].Take(2).All(source => (string)source["mode"] == "Fixed"), "multiple source mode one action");
            root.Q<IntegerField>("supply-add-count").value = 2;
            Click("append-supply");
            Check(Data()["elementSupply"]["sources"].Take(2).All(source => (int)source["items"][0]["count"] == 2), "append fixed items to selected sources");
            Click("undo"); Check(!Data()["elementSupply"]["sources"][0]["items"].Any(), "fixed append one undo");
            Click("redo");
            root.Q<IntegerField>("source-0-item-0-count").value = 4;
            Check((int)Data()["elementSupply"]["sources"][0]["items"][0]["count"] == 4 && (int)Data()["elementSupply"]["sources"][1]["items"][0]["count"] == 2,
                "individual supply row edits only its source");
            root.Q<DropdownField>("source-0-item-0-color").value = "파랑";
            root.Q<DropdownField>("source-exhaustion").value = "무작위로 계속";
            Check((string)Data()["elementSupply"]["sources"][0]["items"][0]["color"] == "Type3" && (string)Data()["elementSupply"]["sources"][1]["exhaustion"] == "Random",
                "supply color and exhaustion edited");
            Click("append-supply"); Click("source-0-item-1-up");
            Check((int)Data()["elementSupply"]["sources"][0]["items"][0]["count"] == 2, "fixed supply order edited");
            Click("source-0-item-1-remove"); Check(Data()["elementSupply"]["sources"][0]["items"].Count() == 1, "fixed supply row removed");
            string before = Data().ToString();
            root.Q<DropdownField>("source-mode").value = "고철 수량 유지";
            Check(Data().ToString() == before && root.Q<DropdownField>("source-mode").value == "고정 순서", "invalid mode change keeps fixed lists and UI");
            Click("clear-supply");
            root.Q<DropdownField>("source-mode").value = "고철 수량 유지";
            root.Q<IntegerField>("scrapTarget").value = 3;
            Check((int)Data()["elementSupply"]["scrapTarget"] == 3, "maintenance target edited");
            string regularSupply = Data()["elementSupply"].ToString();
            root.Q<DropdownField>("inspector-page").value = "튜토리얼 공급";
            Click("sources");
            Check(Data()["tutorial"]["supply"]["sources"].Take(2).All(source => (string)source["mode"] == "Fixed" && (string)source["exhaustion"] == "Stop"), "tutorial sources use fixed stop");
            Check(root.Q<DropdownField>("source-mode") == null && root.Q<DropdownField>("source-exhaustion") == null, "tutorial cannot choose random generation");
            Click("append-supply");
            root.Q<IntegerField>("source-0-item-0-count").value = 3;
            root.Q<DropdownField>("source-0-item-0-color").value = "파랑";
            Check((int)Data()["tutorial"]["supply"]["sources"][0]["items"][0]["count"] == 3, "tutorial generated count edited");
            Click("append-supply"); Click("source-0-item-1-up");
            Check((string)Data()["tutorial"]["supply"]["sources"][0]["items"][1]["color"] == "Type3", "tutorial order edited");
            Click("undo");
            Check((string)Data()["tutorial"]["supply"]["sources"][0]["items"][0]["color"] == "Type3", "tutorial order single undo");
            Click("erase-sources");
            Check(!Data()["tutorial"]["supply"]["sources"].Any() && Data()["elementSupply"].ToString() == regularSupply, "tutorial sources remove without changing regular supply");
            while (session.CanUndo) session.Undo();
            Refresh();
            root.Q<DropdownField>("inspector-page").value = "기본";
            Check(session.SelectedLevelId == originalId && session.Get(originalId).Data.ToString() == original && !session.IsDirty, "advanced edits undo to clean original");
            Debug.Log("PASS supply UI fixed and maintenance workflows");
        }
        private static void Check(bool condition, string text) { if (!condition) throw new Exception("FAIL " + text); Debug.Log("PASS " + text); }
    }
}
