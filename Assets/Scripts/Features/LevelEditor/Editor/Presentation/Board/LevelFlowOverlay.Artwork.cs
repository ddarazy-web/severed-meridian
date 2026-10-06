using System.Collections.Generic;
using Board;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Levels.Editor
{
    public sealed partial class LevelFlowOverlay
    {
        private readonly List<(VisualElement image, LevelConnectionDefinition connection)> artworkPulses = new List<(VisualElement, LevelConnectionDefinition)>();

        private int ConnectionSlot(LevelConnectionDefinition connection)
        {
            int generator = LevelConnectionRules.Find(level, connection.GeneratorId);
            if (generator < 0 || connection.Vertices == null || connection.Vertices.Count == 0) return 0;
            LevelConnectionBody body = LevelConnectionRules.Body(level, generator);
            BoardCoordinate origin = body.Coordinate, start = connection.Vertices[0];
            return start.Row == origin.Row ? 0 : start.Column == origin.Column + body.Size ? 1 : 2;
        }

        private void RefreshArtwork()
        {
            int wallIndex = 0;
            foreach (BoardEdge wall in level.Flow.Walls)
            {
                if (!wall.IsAdjacent || !Visible(wall.A) || !Visible(wall.B)) continue;
                BoardEdge segment = LevelFlowRules.WallSegment(wall);
                bool vertical = segment.A.Column == segment.B.Column;
                // 원화의 투명 여백을 감안해 양 끝이 칸의 꼭짓점에 닿도록 한다.
                AddArtwork("wall-art-" + wallIndex++, LevelBoardArtwork.Wall(vertical),
                    (Point(segment.A, true) + Point(segment.B, true)) * 0.5f, vertical ? 54 : 46);
            }
            if (level.Connections == null) return;
            for (int index = 0; index < level.Connections.Count; index++)
            {
                LevelConnectionDefinition connection = level.Connections[index];
                if (connection.Vertices == null || connection.Vertices.Count < 2) continue;
                int part = 0;
                foreach (BoardEdge segment in LevelFlowRules.Segments(connection.Vertices))
                {
                    if (!Visible(segment.A, true) || !Visible(segment.B, true)) continue;
                    bool vertical = segment.A.Column == segment.B.Column;
                    AddArtwork("wire-art-" + index + "-" + part++, LevelBoardArtwork.Wire(vertical),
                        (Point(segment.A, true) + Point(segment.B, true)) * 0.5f, vertical ? 50 : 47);
                }
                int target = LevelConnectionRules.Find(level, connection.TargetId);
                BoardCoordinate last = connection.Vertices[connection.Vertices.Count - 1];
                bool complete = target >= 0 && LevelConnectionRules.Terminal(LevelConnectionRules.Body(level, target), last);
                if (complete)
                {
                    VisualElement pulseImage = AddArtwork("charge-art-" + index, LevelBoardArtwork.ChargePulse(0), Point(connection.Vertices[0], true), 48);
                    artworkPulses.Add((pulseImage, connection));
                }
                int slot = ConnectionSlot(connection);
                if (Visible(connection.Vertices[0], true))
                    AddArtwork("terminal-start-art-" + index, LevelBoardArtwork.Terminal(slot, complete), Point(connection.Vertices[0], true), 20);
                if (Visible(last, true)) AddArtwork("terminal-end-art-" + index, LevelBoardArtwork.Terminal(slot, complete), Point(last, true), 20);
            }
            AnimateArtwork();
        }

        private VisualElement AddArtwork(string name, Sprite sprite, Vector2 point, float size)
        {
            VisualElement image = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            image.style.position = Position.Absolute;
            image.style.left = point.x - size * 0.5f; image.style.top = point.y - size * 0.5f;
            image.style.width = image.style.height = size;
            image.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            image.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
            Add(image);
            return image;
        }

        private void AnimateArtwork()
        {
            double time = EditorApplication.timeSinceStartup;
            foreach ((VisualElement image, LevelConnectionDefinition connection) in artworkPulses)
            {
                // 편집 화면의 연결 방향 미리보기이며 실제 충전 상태를 변경하지 않는다.
                float distance = (float)(time % 3 / 3) * (connection.Vertices.Count - 1);
                int segment = Mathf.Min((int)distance, connection.Vertices.Count - 2);
                Vector2 point = Vector2.Lerp(Point(connection.Vertices[segment], true), Point(connection.Vertices[segment + 1], true), distance - segment);
                image.style.left = point.x - 24; image.style.top = point.y - 24;
                Sprite sprite = LevelBoardArtwork.ChargePulse((int)(time * 10) % 4);
                image.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
            }
        }
    }
}
