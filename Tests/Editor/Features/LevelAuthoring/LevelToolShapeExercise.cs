using System;
using System.Linq;
using System.Reflection;
using LevelTool;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolShapeExercise
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static void Run(LevelToolScreen screen)
        {
            VisualElement root = screen.GetComponent<UIDocument>().rootVisualElement;
            void Click(string name)
            {
                Button button = root.Q<Button>(name) ?? throw new Exception("Missing shape button " + name);
                typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
            }
            void Refresh() => typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
            var session = screen.Workspace.Session;
            string originalId = session.SelectedLevelId, original = session.Get(originalId).Data.ToString();
            int shapes = session.Documents.Count(doc => doc.Kind == "shape");
            Click("new-level");
            DropdownField page = root.Q<DropdownField>("inspector-page");
            Check(page.choices.Contains("모양"), "shape inspector exists"); page.value = "모양";
            session.SelectCells(new[] { 80, 79, 70 }); Refresh(); Click("deactivate-cells");
            Check(!(bool)session.Get(session.SelectedLevelId).Data["board"]["cells"][80]["isActive"], "shape mask changes selected cells");
            root.Q<TextField>("shape-name").value = "복사용 테스트 모양"; Click("shape-register");
            Check(session.Documents.Count(doc => doc.Kind == "shape") == shapes + 1, "shape registered");
            Check(!root.Q<Button>("shape-delete").enabledSelf, "used shape deletion disabled");
            Click("shape-create-level");
            var level = session.Get(session.SelectedLevelId).Data;
            Check(!(bool)level["board"]["cells"][80]["isActive"] && !level["elements"].Any() && !level["missions"].Any(), "shape creates level with mask only");
            Check(root.Q("shape-preview") != null && root.Q("shape-usage") != null, "shape preview and usage visible");
            while (session.CanUndo) session.Undo(); Refresh(); root.Q<DropdownField>("inspector-page").value = "기본";
            Check(session.SelectedLevelId == originalId && session.Get(originalId).Data.ToString() == original && session.Documents.Count(doc => doc.Kind == "shape") == shapes,
                "shape and new level undo restore original documents");
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}
