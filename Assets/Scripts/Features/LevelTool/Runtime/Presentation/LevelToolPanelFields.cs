#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using Elements;
using Levels;
using Simulation;

namespace LevelTool.Presentation
{
    internal static class LevelToolPanelFields
    {
        public static Button Button(VisualElement parent, string name, string text, Action action)
        {
            Button value = new Button(action) { name = name, text = text }; parent.Add(value); return value;
        }
        public static void Choice(VisualElement parent, string name, string label, string[] values, string[] labels, string current, Action<string> changed)
        {
            if (values.Length == 0) { parent.Add(new Label(label + ": 사용 가능한 종류 없음")); return; }
            List<string> choices = labels.Select((text, index) => labels.Count(value => value == text) > 1 ? text + " · " + values[index] : text).ToList();
            DropdownField field = new DropdownField(label, choices, Math.Max(0, Array.IndexOf(values, current))) { name = name };
            field.RegisterValueChangedCallback(e => changed(values[choices.IndexOf(e.newValue)])); parent.Add(field);
        }
        public static void Number(VisualElement parent, string name, string label, int current, Action<int> changed)
        {
            IntegerField field = new IntegerField(label) { name = name, value = current, isDelayed = true };
            field.RegisterValueChangedCallback(e => changed(e.newValue)); parent.Add(field);
        }
        public static string ItemLabel(BoardItem value) => value == BoardItem.Hammer ? "망치" : value == BoardItem.Swap ? "교환" : "섞기";
        public static void TutorialText(VisualElement host, string name, string label, string current, Action<string> changed, string hint = "")
        {
            var field = new TextField(label) { name = name, value = current ?? "", isDelayed = true, tooltip = hint };
            field.RegisterValueChangedCallback(e => changed(e.newValue)); host.Add(field);
        }
        public static void TutorialToggle(VisualElement host, string name, string label, bool current, Action<bool> changed)
        {
            var field = new Toggle(label) { name = name, value = current };
            field.RegisterValueChangedCallback(e => changed(e.newValue)); host.Add(field);
        }
        public static void TutorialEnum<T>(VisualElement host, string name, string label, T current, Action<T> changed, Func<T, string> display = null) where T : struct, Enum
        {
            var values = Enum.GetValues(typeof(T)).Cast<T>().ToArray();
            Choice(host, name, label, values.Select(value => value.ToString()).ToArray(), values.Select(value => display?.Invoke(value) ?? value.ToString()).ToArray(),
                current.ToString(), value => changed((T)Enum.Parse(typeof(T), value)));
        }
        public static void TutorialPower(VisualElement host, LevelDefinition level, string name, string label, string selected, Action<string> changed)
        {
            var powers = level.CreateElementCatalog().Definitions.Where(value => value.Supply?.Behavior == ElementSupplyBehavior.Power).ToArray();
            Choice(host, name, label, new[] { "" }.Concat(powers.Select(value => value.Id.Value)).ToArray(), new[] { "선택하세요" }.Concat(powers.Select(value => value.DisplayName)).ToArray(), selected, changed);
        }
    }
}
#endif
