using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolUIExercise
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static LevelToolScreen screen;
        private static AuthoringEditSession Session => screen.Workspace.Session;
        private static VisualElement Root => screen.GetComponent<UIDocument>().rootVisualElement;
        private static JObject Data => Session.Get(Session.SelectedLevelId).Data;
        internal static void Run(LevelToolScreen target)
        {
            screen = target;
            string original = Session.SelectedLevelId;
            string originalData = Data.ToString();
            Refresh(); Click("new-level");
            string created = Session.SelectedLevelId;
            Check(created != original && (int)Data["schemaVersion"] == 5 && Data["elementSupply"]["sources"].Count() == 9, "UI new level with basic sources");
            Click("undo"); Check(Session.SelectedLevelId == original, "UI new level one undo");
            Click("redo"); Check(Session.SelectedLevelId == created, "UI new level redo");
            Dictionary<string, JObject> definitions = LevelToolDocuments.Definitions(Session);
            string normal = definitions.First(pair => (string)pair.Value["supply"]?["behavior"] == "FixedNormal").Key;
            string obstacle = definitions.First(pair => LevelToolDocuments.Layer(pair.Value) == "Obstacle" && LevelToolDocuments.Size(pair.Value) == 2 && (int?)pair.Value["placement"]?["maxDurability"] > 1).Key;
            Click(normal);
            Check(Root.Q<Button>(normal).ClassListContains("selected"), "palette choice highlights immediately");
            Pointer(30); Pointer(31); Pointer(39); Pointer(40);
            Check(Data["elements"].Count() == 4, "UI normal placement");
            Root.Q<DropdownField>("edit-layer").value = "Obstacle"; Click(obstacle); Pointer(30, 2);
            JObject body = (JObject)Data["elements"].Single(); string id = (string)body["instanceId"];
            Check((string)body["definitionId"] == obstacle, "UI double click replaces four normal cells with 2x2");
            int max = (int)body["durability"];
            Root.Q<IntegerField>("selected-durability").value = max - 1;
            Check((int)Data["elements"].Single()["durability"] == max - 1, "UI selected durability");
            Click("undo"); Check((int)Data["elements"].Single()["durability"] == max, "UI durability one undo");
            Click("move-body"); Pointer(50);
            Check((string)Data["elements"].Single()["instanceId"] == id && (int)Data["elements"].Single()["coordinate"]["row"] == 5,
                "UI 2x2 move preserves identity");
            Click("undo"); Check((int)Data["elements"].Single()["coordinate"]["row"] == 3, "UI 2x2 move one undo");
            Root.Q<DropdownField>("edit-layer").value = "Block"; Click(normal); Pointer(30, 2);
            Check(Data["elements"].Count() == 1 && (string)Data["elements"].Single()["definitionId"] == normal,
                "UI normal replaces 2x2 obstacle");
            Click("select"); Pointer(10); Pointer(20, 1, true);
            Check(Session.SelectedCells.SequenceEqual(new[] { 10, 11, 19, 20 }), "UI shift rectangular selection");
            Click(normal); Click("place-area");
            Check(Data["elements"].Count() == 5, "UI area placement");
            Click("undo"); Check(Data["elements"].Count() == 1, "UI area one undo");
            Click("sources"); Check(Data["elementSupply"]["sources"].Count() == 13, "UI basic source placement");
            Click("undo"); Check(Data["elementSupply"]["sources"].Count() == 9, "UI source one undo");
            Click("add-mission"); Check(Data["missions"].Count() == 1, "UI basic mission creation");
            IntegerField amount = Root.Query<IntegerField>().ToList().First(field => field.label == "목표 수량");
            int oldAmount = amount.value;
            amount.value = 0;
            Check(Root.Query<IntegerField>().ToList().First(field => field.label == "목표 수량").value == oldAmount,
                "rejected mission amount restores displayed value");
            Click("remove-mission-0"); Check(Data["missions"].Count() == 0, "UI basic mission removal");
            Click("duplicate"); Check(Session.SelectedLevelId != created, "UI duplicate level");
            while (Session.CanUndo) Session.Undo();
            Check(Session.SelectedLevelId == original && Data.ToString() == originalData && !Session.IsDirty,
                "UI edits undo to original including advanced data");
            Refresh(); screen = null;
        }
        private static void Click(string name)
        {
            Button button = Root.Q<Button>(name) ?? throw new Exception("UI button missing: " + name);
            typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
        }
        private static void Pointer(int cell, int count = 1, bool shift = false)
        {
            VisualElement tile = Root.Q("cell-" + cell);
            using PointerDownEvent click = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, clickCount = count,
                modifiers = shift ? EventModifiers.Shift : EventModifiers.None, mousePosition = tile.worldBound.center });
            click.target = tile; tile.SendEvent(click);
        }
        private static void Refresh() => typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}
