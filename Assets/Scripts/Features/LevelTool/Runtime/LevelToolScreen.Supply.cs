#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private readonly Presentation.LevelToolSupplyState supplyState = new Presentation.LevelToolSupplyState();
        private Presentation.LevelToolSupplyPanel supplyPanel;

        private void DrawSupply(JObject level, bool tutorial = false)
        {
            supplyPanel.Refresh(level, definitions, tutorial);
            inspector.Add(supplyPanel.Root);
        }

        private static void Choice(VisualElement parent, string name, string label, string[] values, string[] labels, string current, Action<string> changed) =>
            Presentation.LevelToolPanelFields.Choice(parent, name, label, values, labels, current, changed);

        private static void Number(VisualElement parent, string name, string label, int current, Action<int> changed) =>
            Presentation.LevelToolPanelFields.Number(parent, name, label, current, changed);
    }
}
#endif