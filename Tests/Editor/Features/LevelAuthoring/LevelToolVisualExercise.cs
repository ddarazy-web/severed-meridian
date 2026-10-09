using System;
using System.Linq;
using System.Reflection;
using AutoPlay;
using LevelAuthoring.Editing;
using LevelTool;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelAuthoring.Editor
{
    internal static class LevelToolVisualExercise
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static void Prepare(LevelToolScreen screen)
        {
            var root = screen.GetComponent<UIDocument>().rootVisualElement;
            void Click(string name) => typeof(Clickable).GetMethod("Invoke", Flags).Invoke(root.Q<Button>(name).clickable, new object[] { null });
            Click("new-level"); var session = screen.Workspace.Session;
            var definitions = LevelToolDocuments.Definitions(session);
            string obstacle = definitions.First(pair => LevelToolDocuments.Layer(pair.Value) == "Obstacle" && LevelToolDocuments.Size(pair.Value) == 2 && (int?)pair.Value["placement"]?["maxDurability"] > 1).Key;
            string catalog = (string)session.Get(session.SelectedLevelId).Data["catalogId"];
            string visual = (string)session.Get(catalog).Data["visualId"];
            string visualKey = (string)session.Get(visual).Data["catalog"]["bindings"].Single(value => (string)value["id"] == obstacle)["visualKey"];
            session.Apply("visual trial fixture", docs => {
                var data = docs[session.SelectedLevelId].Data;
                data["missions"] = new JArray(new JObject { ["kind"] = "Color", ["color"] = "Type1", ["count"] = 99 });
                LevelDocumentEditing.Place(data, definitions, obstacle, 3, 3, false);
                foreach (var state in docs[visual].Data["catalog"]["definitions"].Single(value => (string)value["key"] == visualKey)["states"])
                { state["size"] = 1.73f; state["pivotX"] = .25f; state["pivotY"] = .75f; state["offsetX"] = .1f; state["offsetY"] = -.1f; state["angle"] = 30f; }
            });
            typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
            root.Q<DropdownField>("inspector-page").value = "자동 시험"; Click("bot-new");
            var bot = (BotPlaySession)typeof(LevelToolScreen).GetField("toolBot", Flags).GetValue(screen);
            int count = 0;
            while (bot.NeedsAdvance && count++ < 50000) typeof(LevelToolScreen).GetMethod("AdvanceToolBot", Flags).Invoke(screen, null);
            if (bot.Status != BotSessionStatus.Ready || bot.State.Obstacles.Count != 1) throw new Exception("visual fixture preparation failed: " + bot.Message);
            session.Apply("later authoring visual changes", docs => {
                foreach (var state in docs[visual].Data["catalog"]["definitions"].Single(value => (string)value["key"] == visualKey)["states"]) state["size"] = .83f;
            });
            typeof(LevelToolScreen).GetMethod("Refresh", Flags).Invoke(screen, null);
        }
        internal static bool Verify(LevelToolScreen screen)
        {
            var view = screen.GetComponent<UIDocument>().rootVisualElement.Q("bot-trial-board");
            var labels = view.Query<Label>().ToList();
            string error = labels.Select(label => label.text).FirstOrDefault(text => text != null && text.Contains("이미지") && text.Contains("실패"));
            if (error != null) throw new Exception(error);
            var images = view.Query<Image>().ToList();
            if (images.Count == 0) return false;
            var bodies = images.Where(image => image.style.width.value.value > 40 || image.style.height.value.value > 40).ToArray();
            if (bodies.Length != 1 || images.Count != 78) throw new Exception("FAIL 2x2 body must draw once with 77 normal cells");
            var body = bodies[0]; float side = 36 * 1.73f;
            float width = side * body.sprite.rect.width / Mathf.Max(body.sprite.rect.width, body.sprite.rect.height);
            float height = side * body.sprite.rect.height / Mathf.Max(body.sprite.rect.width, body.sprite.rect.height);
            if (Mathf.Abs(body.style.width.value.value - width) > .01f || Mathf.Abs(body.style.height.value.value - height) > .01f ||
                Mathf.Abs(body.style.left.value.value - (144 + .1f * side - width * .25f)) > .01f ||
                Mathf.Abs(body.style.top.value.value - (144 + .1f * side - height * .25f)) > .01f ||
                Mathf.Abs(body.style.rotate.value.angle.value + 30) > .01f)
                throw new Exception("FAIL trial uses wrong custom size/pivot/offset/angle or current edited catalog");
            if (labels.Count(label => label.ClassListContains("trial-cell") && string.IsNullOrEmpty(label.text)) != 81)
                throw new Exception("FAIL footprint fallback labels not cleared");
            Debug.Log("PASS trial 2x2 draws once; frozen custom catalog size/pivot/offset/rotation survives later authoring edits");
            return true;
        }
    }
}
