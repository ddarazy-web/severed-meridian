#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private void DrawFlowOverlay(JObject level)
        {
            if (inspectorPage != "흐름") return;
            VisualElement overlay = new VisualElement { name = "flow-overlay", pickingMode = PickingMode.Ignore };
            overlay.style.position = Position.Absolute; overlay.style.left = 0; overlay.style.top = 0;
            overlay.style.width = cellSize * 9; overlay.style.height = cellSize * 9; board.Add(overlay);
            void Badge(int cell, string text)
            {
                Label label = new Label(text) { pickingMode = PickingMode.Ignore };
                label.style.position = Position.Absolute; label.style.left = cell % 9 * cellSize; label.style.top = cell / 9 * cellSize;
                label.style.color = Color.white; label.style.backgroundColor = new Color(.05f, .1f, .15f, .9f); overlay.Add(label);
            }
            foreach (JObject gravity in level["flow"]["gravity"])
                Badge(Cell(gravity["coordinate"]), (string)gravity["direction"] switch { "Up" => "↑", "Left" => "←", "Right" => "→", _ => "↓" });
            foreach (JObject path in level["flow"]["paths"])
                Badge(Cell(path["coordinate"]), (bool)path["isEnd"] ? "끝" : "→" + CellName(Cell(path["next"])));
            foreach (JObject portal in level["flow"]["portals"])
            {
                Badge(Cell(portal["entrance"]), "입구");
                if ((bool)portal["hasExit"]) Badge(Cell(portal["exit"]), "출구");
            }
            foreach (JToken arrival in level["flow"]["arrivals"]) Badge(Cell(arrival), "도착");
            foreach (JObject wall in level["flow"]["walls"])
            {
                int a = Cell(wall["a"]), b = Cell(wall["b"]);
                bool vertical = a / 9 == b / 9;
                VisualElement edge = new VisualElement { pickingMode = PickingMode.Ignore };
                edge.style.position = Position.Absolute; edge.style.backgroundColor = new Color(1f, .55f, .2f);
                edge.style.left = vertical ? Mathf.Max(a % 9, b % 9) * cellSize - 2 : a % 9 * cellSize;
                edge.style.top = vertical ? a / 9 * cellSize : Mathf.Max(a / 9, b / 9) * cellSize - 2;
                edge.style.width = vertical ? 4 : cellSize; edge.style.height = vertical ? cellSize : 4; overlay.Add(edge);
            }
            for (int i = 0; i < flowRoute.Count; i++) Badge(flowRoute[i], "경로 " + (i + 1));
            if (flowFirst >= 0) Badge(flowFirst, "첫 칸");
        }
    }
}
#endif
