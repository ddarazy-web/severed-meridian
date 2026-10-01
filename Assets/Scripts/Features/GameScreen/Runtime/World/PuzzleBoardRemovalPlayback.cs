using System.Collections.Generic;
using System.Linq;
using Board;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    internal sealed class PuzzleBoardRemovalPlayback
    {
        private readonly List<PuzzleBoardSnapshot.Image> removed = new List<PuzzleBoardSnapshot.Image>();
        private float elapsed, duration;
        internal bool IsPlaying { get; private set; }

        internal void Begin(PuzzleBoardSnapshot snapshot, LevelRuntimeState after, PuzzleArtwork art,
            IEnumerable<MatchedBlockChange> changes, float seconds)
        {
            Reset();
            HashSet<BoardCoordinate> consumed = new HashSet<BoardCoordinate>(changes.Where(change => change.IsConsumed).Select(change => change.Coordinate));
            foreach (KeyValuePair<BoardCoordinate, PuzzleBoardSnapshot.Image> pair in snapshot.Images)
            {
                RuntimeCell cell = after.CellAt(pair.Key);
                Sprite next = art.Get(cell.Content == RuntimeContent.Obstacle && cell.ObstacleIndex.HasValue
                    ? PuzzleArtworkPaths.Obstacle(after.Obstacles[cell.ObstacleIndex.Value]) : PuzzleArtworkPaths.Content(cell));
                if (consumed.Contains(pair.Key) || next == null || next.name != pair.Value.Renderer.sprite.name) removed.Add(pair.Value);
            }
            duration = Mathf.Max(.01f, seconds); elapsed = 0; IsPlaying = removed.Count > 0;
        }

        internal bool Tick(float deltaTime)
        {
            if (!IsPlaying) return false;
            elapsed += Mathf.Max(0, deltaTime);
            float t = Mathf.Clamp01(elapsed / duration);
            foreach (PuzzleBoardSnapshot.Image image in removed)
            {
                image.Renderer.transform.localScale = image.Scale * Mathf.Lerp(1, .65f, t);
                Color color = image.Color; color.a *= 1 - t; image.Renderer.color = color;
            }
            if (t < 1) return false;
            IsPlaying = false; return true;
        }

        internal void Reset()
        {
            foreach (PuzzleBoardSnapshot.Image image in removed) image.Restore();
            removed.Clear(); IsPlaying = false; elapsed = 0;
        }
    }
}
