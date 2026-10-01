using System;
using System.Linq;
using Board;
using Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    // 보드와 같은 스크롤 좌표계를 사용하며 입력이나 실행 상태를 소유하지 않는다.
    internal sealed class LevelGeneratorOverlay : VisualElement
    {
        private readonly LevelRuntimeState state;
        private readonly int[] pulsing;
        private readonly double started = EditorApplication.timeSinceStartup;
        internal LevelGeneratorOverlay(LevelRuntimeState state, int[] pulsing)
        {
            this.state = state; this.pulsing = pulsing;
            name = "generator-overlay"; pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute; style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            generateVisualContent += Draw;
            schedule.Execute(MarkDirtyRepaint).Every(40).Until(() => EditorApplication.timeSinceStartup - started > 1);
            schedule.Execute(MarkDirtyRepaint).StartingIn(1050);
        }
        private Vector2 Point(BoardCoordinate vertex)
        {
            VisualElement first = parent.Q<Button>("initial-cell-0-0");
            VisualElement last = parent.Q<Button>($"initial-cell-{state.Rows - 1}-{state.Columns - 1}");
            Vector2 a = this.WorldToLocal(first.worldBound.center), b = this.WorldToLocal(last.worldBound.center);
            return a + new Vector2((vertex.Column - 0.5f) * (b.x - a.x) / (state.Columns - 1), (vertex.Row - 0.5f) * (b.y - a.y) / (state.Rows - 1));
        }
        private void Draw(MeshGenerationContext context)
        {
            if (parent == null || parent.Q<Button>("initial-cell-0-0") == null) return;
            Painter2D painter = context.painter2D;
            for (int index = 0; index < state.Obstacles.Count; index++)
            {
                RuntimeObstacle body = state.Obstacles[index];
                if (body.Definition.Kind != ObstacleKind.Generator || !state.Cells.Any(c => c.ObstacleIndex == index)) continue;
                BoardCoordinate origin = body.Definition.Coordinate;
                Vector2 center = Point(new BoardCoordinate(origin.Row + 2, origin.Column + 1)) - Vector2.up * 7;
                for (int lamp = 0; lamp < body.Definition.RequiredCharge; lamp++)
                {
                    Vector2 point = center + Vector2.right * (lamp - (body.Definition.RequiredCharge - 1) * 0.5f) * 11;
                    painter.fillColor = lamp < body.Charge ? new Color(1, 0.85f, 0.3f) : new Color(0.12f, 0.15f, 0.2f);
                    painter.strokeColor = new Color(0.9f, 0.92f, 0.96f); painter.lineWidth = 1;
                    painter.BeginPath(); painter.Arc(point, 3.5f, 0, 360); painter.Fill(); painter.Stroke();
                }
            }
            painter.lineCap = LineCap.Butt;
            foreach (BoardEdge wall in state.Flow.Walls)
            {
                BoardEdge segment = LevelFlowRules.WallSegment(wall);
                painter.strokeColor = new Color(0.7f, 0.73f, 0.8f); painter.lineWidth = 6;
                painter.BeginPath(); painter.MoveTo(Point(segment.A)); painter.LineTo(Point(segment.B)); painter.Stroke();
            }
            painter.lineCap = LineCap.Round; painter.lineJoin = LineJoin.Round;
            foreach (RuntimeConnection connection in GeneratorRules.ActiveConnections(state))
            {
                int index = state.Connections.IndexOf(connection);
                Color color = LevelBoardView.Swatches[index % LevelBoardView.Swatches.Length];
                painter.strokeColor = color; painter.lineWidth = 2;
                painter.BeginPath(); painter.MoveTo(Point(connection.Vertices[0]));
                foreach (BoardCoordinate vertex in connection.Vertices.Skip(1)) painter.LineTo(Point(vertex));
                painter.Stroke();
                foreach (BoardCoordinate vertex in new[] { connection.Vertices[0], connection.Vertices.Last() })
                {
                    Vector2 center = Point(vertex);
                    int sides = 3 + index % 4;
                    painter.fillColor = color; painter.BeginPath();
                    for (int i = 0; i < sides; i++)
                    {
                        float angle = -Mathf.PI / 2 + i * Mathf.PI * 2 / sides;
                        Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 5;
                        if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
                    }
                    painter.ClosePath(); painter.Fill();
                }
                double elapsed = EditorApplication.timeSinceStartup - started;
                if (elapsed < 1 && pulsing.Any(i => state.Obstacles[i].Definition.Id == connection.GeneratorId))
                {
                    float distance = (float)elapsed * (connection.Vertices.Count - 1);
                    int segment = Math.Min((int)distance, connection.Vertices.Count - 2);
                    Vector2 point = Vector2.Lerp(Point(connection.Vertices[segment]), Point(connection.Vertices[segment + 1]), distance - segment);
                    painter.fillColor = Color.white; painter.BeginPath(); painter.Arc(point, 3, 0, 360); painter.Fill();
                }
            }
        }
    }
}
