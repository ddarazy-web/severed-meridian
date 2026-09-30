using System.Collections.Generic;
using Board;
using Levels;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    public sealed class PuzzleWorldBoard : MonoBehaviour
    {
        [SerializeField] private PuzzleCellView cellPrefab;
        [SerializeField] private SpriteRenderer obstaclePrefab;
        [SerializeField] private SpriteRenderer decorationPrefab;
        private readonly List<PuzzleCellView> cells = new List<PuzzleCellView>();
        private readonly List<SpriteRenderer> bodies = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> decorations = new List<SpriteRenderer>();
        private int bodyCount, decorationCount;
        public static Vector3 CellPosition(BoardCoordinate at) => new Vector3(at.Column - 4.5f, 4.5f - at.Row, 0);
        public static Vector3 VertexPosition(BoardCoordinate at) => new Vector3(at.Column - 5, 5 - at.Row, 0);

        public void Configure(PuzzleCellView cell, SpriteRenderer obstacle, SpriteRenderer decoration)
        { cellPrefab = cell; obstaclePrefab = obstacle; decorationPrefab = decoration; }

        public void Draw(LevelRuntimeState state, PuzzleArtwork art)
        {
            bodyCount = decorationCount = 0;
            while (cells.Count < state.Cells.Count) cells.Add(Instantiate(cellPrefab, transform));
            HashSet<int> drawn = new HashSet<int>();
            for (int i = 0; i < cells.Count; i++)
            {
                PuzzleCellView view = cells[i];
                bool active = i < state.Cells.Count && state.Cells[i].IsActive;
                view.gameObject.SetActive(active);
                if (!active) continue;
                RuntimeCell cell = state.Cells[i];
                view.name = "Cell-" + cell.Coordinate.Row + "-" + cell.Coordinate.Column;
                view.transform.localPosition = CellPosition(cell.Coordinate);
                view.Draw(art.Get(PuzzleArtworkPaths.Floor), art.Get(PuzzleArtworkPaths.Dust(cell.DustDurability)),
                    art.Get(PuzzleArtworkPaths.Content(cell)), art.Get(PuzzleArtworkPaths.Cover(cell)));
                if (cell.Content != RuntimeContent.Obstacle || !cell.ObstacleIndex.HasValue || !drawn.Add(cell.ObstacleIndex.Value)) continue;
                RuntimeObstacle body = state.Obstacles[cell.ObstacleIndex.Value];
                int size = LevelPlacementRules.Size(body.Definition.Kind);
                SpriteRenderer image = Take(bodies, obstaclePrefab, bodyCount++);
                image.name = "Obstacle-" + body.Definition.Id;
                image.transform.localPosition = CellPosition(body.Definition.Coordinate) + new Vector3((size - 1) * 0.5f, -(size - 1) * 0.5f, 0);
                PuzzleCellView.Set(image, art.Get(PuzzleArtworkPaths.Obstacle(body)), size * 0.96f);
            }
            foreach (BoardEdge wall in state.Flow.Walls)
            {
                BoardEdge segment = LevelFlowRules.WallSegment(wall);
                bool vertical = segment.A.Column == segment.B.Column;
                // 벽도 실제 불투명 길이가 한 칸에 닿도록 방향별 여백을 보정한다.
                Add("Wall", art.Get(PuzzleArtworkPaths.Wall(vertical)),
                    (VertexPosition(segment.A) + VertexPosition(segment.B)) * 0.5f, vertical ? 1.38f : 1.18f, 30);
            }
            for (int i = 0; i < state.Flow.Portals.Count; i++)
            {
                FlowPortal portal = state.Flow.Portals[i];
                Add("Portal-entry", art.Get(PuzzleArtworkPaths.Portal(i, false)), CellPosition(portal.Entrance), 1, 4);
                if (portal.HasExit) Add("Portal-exit", art.Get(PuzzleArtworkPaths.Portal(i, true)), CellPosition(portal.Exit), 1, 4);
            }
            foreach (BoardCoordinate arrival in state.Flow.Arrivals)
                Add("Recovery-exit", art.Get(PuzzleArtworkPaths.Arrival), CellPosition(arrival), 1, 4);
            DrawConnections(state, art);
            for (int i = bodyCount; i < bodies.Count; i++) bodies[i].gameObject.SetActive(false);
            for (int i = decorationCount; i < decorations.Count; i++) decorations[i].gameObject.SetActive(false);
        }

        private void DrawConnections(LevelRuntimeState state, PuzzleArtwork art)
        {
            foreach (RuntimeConnection connection in state.Connections)
            {
                if (connection.Vertices.Count == 0) continue;
                foreach (BoardEdge segment in LevelFlowRules.Segments(connection.Vertices))
                {
                    bool vertical = segment.A.Column == segment.B.Column;
                    // 원화의 축별 투명 여백을 보정해 인접 선분과 꺾임이 끊기지 않게 한다.
                    Add("Wire", art.Get(PuzzleArtworkPaths.Wire(segment.A.Column == segment.B.Column)),
                        (VertexPosition(segment.A) + VertexPosition(segment.B)) * 0.5f, vertical ? 1.25f : 1.2f, 31);
                }
                RuntimeObstacle generator = null, target = null;
                foreach (RuntimeObstacle body in state.Obstacles)
                {
                    if (body.Definition.Id == connection.GeneratorId) generator = body;
                    if (body.Definition.Id == connection.TargetId) target = body;
                }
                BoardCoordinate first = connection.Vertices[0], last = connection.Vertices[connection.Vertices.Count - 1];
                bool connected = target != null && LevelConnectionRules.Terminal(target.Definition, last);
                int slot = generator == null || first.Row == generator.Definition.Coordinate.Row ? 0 :
                    first.Column == generator.Definition.Coordinate.Column + 2 ? 1 : 2;
                Sprite terminal = art.Get(PuzzleArtworkPaths.Terminal(slot, connected));
                Add("Terminal-start", terminal, VertexPosition(first), 0.42f, 32);
                Add("Terminal-end", terminal, VertexPosition(last), 0.42f, 32);
            }
        }

        private void Add(string label, Sprite sprite, Vector3 position, float size, int order)
        {
            SpriteRenderer image = Take(decorations, decorationPrefab, decorationCount++);
            image.name = label; image.transform.localPosition = position; image.sortingOrder = order;
            PuzzleCellView.Set(image, sprite, size);
        }

        private SpriteRenderer Take(List<SpriteRenderer> pool, SpriteRenderer prefab, int index)
        {
            if (index == pool.Count) pool.Add(Instantiate(prefab, transform));
            pool[index].gameObject.SetActive(true);
            return pool[index];
        }
    }
}
