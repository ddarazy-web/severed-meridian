using System;
using System.Linq;
using System.Reflection;
using LevelAuthoring.Editing;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolConnectionExercise
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static void Run(LevelToolScreen screen)
        {
            VisualElement root = screen.GetComponent<UIDocument>().rootVisualElement;
            void Click(string name)
            {
                Button button = root.Q<Button>(name) ?? throw new Exception("Missing connection button " + name);
                typeof(Clickable).GetMethod("Invoke", Flags).Invoke(button.clickable, new object[] { null });
            }
            void Vertex(int vertex)
            {
                VisualElement tile = root.Q("wire-vertex-" + vertex) ?? throw new Exception("Missing wire vertex");
                using PointerDownEvent click = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0 });
                click.target = tile; tile.SendEvent(click);
            }
            var session = screen.Workspace.Session;
            string originalId = session.SelectedLevelId, original = session.Get(originalId).Data.ToString();
            Click("new-level");
            var definitions = LevelToolDocuments.Definitions(session);
            string generator = definitions.First(pair => (string)pair.Value["reaction"] == "GeneratorCharge").Key;
            string target = definitions.First(pair => (string)pair.Value["reaction"] == "Durability" && LevelToolDocuments.Size(pair.Value) == 2 && (string)pair.Value["removal"]?["kind"] != "Scrap").Key;
            session.Apply("connection fixture", docs =>
            {
                LevelDocumentEditing.Place(docs[session.SelectedLevelId].Data, definitions, generator, 2, 2, false);
                LevelDocumentEditing.Place(docs[session.SelectedLevelId].Data, definitions, target, 2, 6, false);
            });
            typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
            DropdownField page = root.Q<DropdownField>("inspector-page");
            Check(page.choices.Contains("연결"), "connection inspector exists"); page.value = "연결";
            JObject Data() => session.Get(session.SelectedLevelId).Data;
            Click("connection-add"); Check(Data()["connections"].Count() == 1, "generator target link created");
            string generatorId = (string)Data()["connections"][0]["generatorId"];
            Click("wire-start"); Vertex(24); Click("wire-save-draft");
            Check(Data()["connections"][0]["vertices"].Count() == 1, "partial wire saved without forcing endpoint");
            Vertex(25); Vertex(26); Click("wire-finish");
            Check(Data()["connections"][0]["vertices"].Count() == 3, "wire completes at target terminal");
            Click("undo"); Check(Data()["connections"][0]["vertices"].Count() == 1, "wire completion one undo"); Click("redo");
            Click("wire-start"); Click("wire-clear-input"); Vertex(33);
            string before = Data().ToString(); Click("wire-save-draft");
            Check(Data().ToString() == before, "wire inside 2x2 rejected atomically");
            Click("wire-cancel");
            int charge = root.Q<IntegerField>("generator-charge").value;
            root.Q<IntegerField>("generator-charge").value = charge + 1;
            Check((string)Data()["connections"][0]["generatorId"] == generatorId && (int)Data()["elements"].Single(item => (string)item["instanceId"] == generatorId)["requiredCharge"] == charge + 1,
                "charge edit preserves connection identity");
            string targetId = (string)Data()["connections"][0]["targetId"];
            string connection = Data()["connections"][0].ToString();
            session.Apply("연결 대상 이동", docs => LevelDocumentEditing.Move(docs[session.SelectedLevelId].Data, definitions, targetId, 5, 6));
            Check(Data()["connections"][0].ToString() == connection &&
                (int)Data()["elements"].Single(item => (string)item["instanceId"] == targetId)["coordinate"]["row"] == 5,
                "moving 2x2 retains connection IDs and authored wire");
            session.Apply("이동 대상 삭제", docs => LevelDocumentEditing.Erase(docs[session.SelectedLevelId].Data, definitions, "Obstacle", 5, 6));
            Check(!Data()["connections"].Any(), "erasing moved 2x2 removes dangling connection");
            session.Undo(); session.Undo();
            Check(Data()["connections"][0].ToString() == connection, "move and erase undo restore exact connection");
            Click("connection-remove"); Check(!Data()["connections"].Any(), "connection deletion");
            while (session.CanUndo) session.Undo();
            typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
            root.Q<DropdownField>("inspector-page").value = "기본";
            Check(session.SelectedLevelId == originalId && session.Get(originalId).Data.ToString() == original, "connection undo restores original");
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception("FAIL " + message); Debug.Log("PASS " + message); }
    }
}
