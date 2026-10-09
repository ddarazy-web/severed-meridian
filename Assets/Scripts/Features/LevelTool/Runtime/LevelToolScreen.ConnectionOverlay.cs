#if UNITY_EDITOR || PRODUCT_LEVEL_EDITOR
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LevelTool
{
    public sealed partial class LevelToolScreen
    {
        private void DrawConnectionOverlay(JObject level)
        {
            if (inspectorPage != "연결") return;
            VisualElement overlay = new VisualElement { name = "connection-overlay", pickingMode = PickingMode.Ignore };
            overlay.style.position = Position.Absolute; overlay.style.left = 0; overlay.style.top = 0;
            overlay.style.width = cellSize * 9; overlay.style.height = cellSize * 9; board.Add(overlay);
            void Lines(int[] vertices, Color color)
            {
                for (int i = 1; i < vertices.Length; i++)
                {
                    Vector2 a = new Vector2(vertices[i - 1] % 10, vertices[i - 1] / 10) * cellSize;
                    Vector2 b = new Vector2(vertices[i] % 10, vertices[i] / 10) * cellSize;
                    VisualElement line = new VisualElement { pickingMode = PickingMode.Ignore };
                    line.style.position = Position.Absolute; line.style.width = Vector2.Distance(a, b); line.style.height = 3;
                    line.style.left = (a.x + b.x - Vector2.Distance(a, b)) / 2; line.style.top = (a.y + b.y) / 2 - 1.5f;
                    line.style.rotate = new Rotate(new Angle(Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg, AngleUnit.Degree));
                    line.style.backgroundColor = color; overlay.Add(line);
                }
            }
            int connection = 0;
            foreach (JObject link in level["connections"])
                Lines(link["vertices"].Select(v => (int)v["row"] * 10 + (int)v["column"]).ToArray(), connection++ == selectedConnection ? Color.cyan : Color.gray);
            if (!wireDrawing) return;
            Lines(wireRoute.ToArray(), Color.yellow);
            for (int i = 0; i < 100; i++)
            {
                int vertex = i;
                VisualElement dot = new VisualElement { name = "wire-vertex-" + i };
                dot.style.position = Position.Absolute; dot.style.width = 12; dot.style.height = 12;
                dot.style.left = i % 10 * cellSize - 6; dot.style.top = i / 10 * cellSize - 6;
                dot.style.backgroundColor = wireRoute.Contains(i) ? Color.yellow : new Color(.3f, .75f, .8f);
                dot.tooltip = "꼭짓점 " + (i / 10 + 1) + "행 " + (i % 10 + 1) + "열";
                dot.RegisterCallback<PointerDownEvent>(e =>
                {
                    e.StopPropagation(); if (e.button != 0 || busy) return;
                    if (wireRoute.Contains(vertex)) { Show("이미 찍은 점입니다. ‘찍은 점 비우기’로 다시 그릴 수 있습니다."); return; }
                    wireRoute.Add(vertex); Refresh();
                }); overlay.Add(dot);
            }
        }
    }
}
#endif
