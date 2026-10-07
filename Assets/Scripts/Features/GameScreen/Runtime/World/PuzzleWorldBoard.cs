using System.Collections.Generic;
using Board;
using Levels;
using Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using Elements;

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
        private readonly List<SpriteRenderer> supplyImages = new List<SpriteRenderer>();
        private readonly List<SpriteMask> supplyClips = new List<SpriteMask>();
        private readonly List<PuzzleEffectSprite> effects = new List<PuzzleEffectSprite>();
        private Sprite supplyClipSprite;
        private int bodyCount, decorationCount;
        private readonly Dictionary<BoardCoordinate, SpriteRenderer> occupants = new Dictionary<BoardCoordinate, SpriteRenderer>();
        private SpriteRenderer preview;
        private Vector3 previewOrigin;
        private int previewOrder;
        internal const int SwipeSortingOrder = 50;

        public SpriteRenderer OccupantAt(BoardCoordinate at) => occupants.TryGetValue(at, out SpriteRenderer image) ? image : null;

        internal PuzzleEffectSprite EffectAt(int index)
        {
            if (supplyClipSprite == null)
                supplyClipSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            while (effects.Count <= index) effects.Add(new PuzzleEffectSprite(transform, decorationPrefab, supplyClipSprite));
            return effects[index];
        }

        internal void HideEffects() { foreach (PuzzleEffectSprite effect in effects) effect.Hide(); }

        internal PuzzleBoardSnapshot Capture()
        {
            // 교환 스냅샷에는 선택 전 순서를 저장한다. 미리보기 위치와 원점은 유지한다.
            if (preview != null) preview.sortingOrder = previewOrder;
            PuzzleBoardSnapshot snapshot = new PuzzleBoardSnapshot();
            foreach (KeyValuePair<BoardCoordinate, SpriteRenderer> pair in occupants)
                if (pair.Value.enabled && pair.Value.sprite != null)
                {
                    PuzzleBoardSnapshot.Image image = new PuzzleBoardSnapshot.Image(pair.Value, pair.Key, transform, OccupantOrigin(pair.Value));
                    snapshot.Images.Add(pair.Key, image); snapshot.Owned.Add(image);
                }
            return snapshot;
        }

        internal PuzzleBoardSnapshot.Image SupplyImage(SettlementRecord record, LevelRuntimeState state, PuzzleArtwork art)
        {
            int index = supplyImages.FindIndex(image => !image.gameObject.activeSelf);
            if (index < 0) index = supplyImages.Count;
            SpriteRenderer renderer = Take(supplyImages, obstaclePrefab, index);
            if (supplyClipSprite == null)
                supplyClipSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            if (index == supplyClips.Count)
            {
                // 이웃 생성구 마스크가 다른 공급 그림까지 드러내지 않도록 쌍별로 격리한다.
                SortingGroup group = new GameObject("Supply-group", typeof(SortingGroup)).GetComponent<SortingGroup>();
                group.transform.SetParent(transform, false); group.sortingOrder = 10;
                renderer.transform.SetParent(group.transform, false);
                SpriteMask mask = new GameObject("Supply-clip", typeof(SpriteMask)).GetComponent<SpriteMask>();
                mask.transform.SetParent(group.transform, false); mask.sprite = supplyClipSprite;
                mask.isCustomRangeActive = false;
                supplyClips.Add(mask);
            }
            SpriteMask clip = supplyClips[index]; clip.gameObject.SetActive(true);
            SortingGroup supplyGroup = renderer.transform.parent.GetComponent<SortingGroup>();
            supplyGroup.transform.localPosition = Vector3.zero;
            supplyGroup.transform.localScale = Vector3.one;
            supplyGroup.transform.localRotation = Quaternion.identity;
            clip.sprite = supplyClipSprite;
            clip.transform.localPosition = CellPosition(record.Target);
            ElementVisualFrame frame = art.Visuals.Supply(record, state);
            supplyGroup.sortingOrder = frame?.Order ?? 0;
            PuzzleCellView.SetVisual(renderer, art.GetVisual(frame), frame, record.Content == RuntimeContent.Obstacle ? 1 : .92f);
            // 내부 생성구에서도 공급되는 동안에는 대상 한 칸 안에서만 드러난다.
            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            renderer.name = "Supply-playback"; renderer.transform.localPosition += CellPosition(record.Target);
            return new PuzzleBoardSnapshot.Image(renderer, record.Target, transform, renderer.transform.localPosition, true, clip);
        }

        public Vector3 OccupantOrigin(SpriteRenderer image) => image == preview ? previewOrigin : image.transform.localPosition;

        public void Preview(BoardCoordinate at, Vector3 offset)
        {
            SpriteRenderer image = OccupantAt(at);
            if (image != preview)
            {
                ClearPreview(); preview = image;
                if (preview != null)
                {
                    previewOrigin = preview.transform.localPosition;
                    previewOrder = preview.sortingOrder;
                    int order = SwipeSortingOrder;
                    foreach (PuzzleCellView cell in cells)
                        if (cell.gameObject.activeSelf) order = Mathf.Max(order, cell.HighestSortingOrder + 1);
                    foreach (SpriteRenderer body in bodies)
                        if (body.gameObject.activeSelf && body.enabled) order = Mathf.Max(order, body.sortingOrder + 1);
                    foreach (SpriteRenderer decoration in decorations)
                        if (decoration.gameObject.activeSelf && decoration.enabled) order = Mathf.Max(order, decoration.sortingOrder + 1);
                    preview.sortingOrder = order;
                }
            }
            if (preview != null) preview.transform.localPosition = previewOrigin + preview.transform.parent.InverseTransformVector(transform.TransformVector(offset));
        }

        public void ClearPreview()
        {
            if (preview != null) { preview.transform.localPosition = previewOrigin; preview.sortingOrder = previewOrder; }
            preview = null;
        }

        // 교환 재생기가 현재 표시 위치와 원점을 인수한 뒤 호출한다.
        public void ReleasePreview()
        {
            if (preview != null) preview.sortingOrder = previewOrder;
            preview = null;
        }
        public const float HalfWidth = BoardDefinition.DefaultColumns * 0.5f;
        public const float HalfHeight = BoardDefinition.DefaultRows * 0.5f;
        public static Vector3 CellPosition(BoardCoordinate at) => new Vector3(at.Column - HalfWidth + 0.5f, HalfHeight - 0.5f - at.Row, 0);
        public static Vector3 VertexPosition(BoardCoordinate at) => new Vector3(at.Column - HalfWidth, HalfHeight - at.Row, 0);

        public void Configure(PuzzleCellView cell, SpriteRenderer obstacle, SpriteRenderer decoration)
        { cellPrefab = cell; obstaclePrefab = obstacle; decorationPrefab = decoration; }

        public void Draw(LevelRuntimeState state, PuzzleArtwork art)
        {
            ClearPreview(); occupants.Clear();
            bodyCount = decorationCount = 0;
            while (cells.Count < state.Cells.Count) cells.Add(Instantiate(cellPrefab, transform));
            HashSet<int> drawn = new HashSet<int>();
            for (int i = 0; i < cells.Count; i++)
            {
                PuzzleCellView view = cells[i];
                bool active = i < state.Cells.Count && state.Cells[i].IsActive;
                view.gameObject.SetActive(active);
                if (!active) { view.Clear(); continue; }
                RuntimeCell cell = state.Cells[i];
                view.name = "Cell-" + cell.Coordinate.Row + "-" + cell.Coordinate.Column;
                view.transform.localPosition = CellPosition(cell.Coordinate);
                view.transform.localScale = Vector3.one;
                view.transform.localRotation = Quaternion.identity;
                view.Draw(art, art.Visuals.Dust(cell), art.Visuals.Content(cell), art.Visuals.Cover(cell));
                if (cell.Content != RuntimeContent.Empty && cell.Content != RuntimeContent.Obstacle)
                    occupants[cell.Coordinate] = view.ContentRenderer;
                if (cell.Content != RuntimeContent.Obstacle || !cell.ObstacleIndex.HasValue || !drawn.Add(cell.ObstacleIndex.Value)) continue;
                RuntimeObstacle body = state.Obstacles[cell.ObstacleIndex.Value];
                int size = ElementVisualLookup.Size(body.Element);
                SpriteRenderer image = Take(bodies, obstaclePrefab, bodyCount++);
                image.name = "Obstacle-" + body.Definition.Id;
                ElementVisualFrame frame = art.Visuals.Obstacle(body);
                PuzzleCellView.SetVisual(image, art.GetVisual(frame), frame);
                image.transform.localPosition += CellPosition(size == 1 ? cell.Coordinate : body.Definition.Coordinate) + new Vector3((size - 1) * 0.5f, -(size - 1) * 0.5f, 0);
                if (size == 1) occupants[cell.Coordinate] = image;
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
            for (int i = bodyCount; i < bodies.Count; i++)
            { PuzzleCellView.ResetImage(bodies[i]); bodies[i].gameObject.SetActive(false); }
            for (int i = decorationCount; i < decorations.Count; i++)
            { PuzzleCellView.ResetImage(decorations[i]); decorations[i].gameObject.SetActive(false); }
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
            PuzzleCellView.ResetImage(pool[index]);
            pool[index].gameObject.SetActive(true);
            return pool[index];
        }

        private void OnDestroy()
        {
            if (supplyClipSprite != null) Destroy(supplyClipSprite);
        }
    }
}
