using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Board;
using Cysharp.Threading.Tasks;
using Levels;
using Simulation;
using UnityEngine;

namespace GameScreen
{
    internal sealed partial class PuzzlePowerPlayback
    {
        private sealed class Clip
        {
            internal string Label;
            internal string[] Paths;
            internal Sprite[] Frames;
            internal float Start, End, Size, Angle, MotionDelay;
            internal Vector3 From, To;
            internal Vector3 Offset;
            internal Vector3[] FrameOffsets;
            internal bool Sheet;
        }
        private readonly List<Clip> clips = new List<Clip>();
        private readonly List<(SpriteRenderer image, Vector3 scale, Color color)> matching = new List<(SpriteRenderer, Vector3, Color)>();
        private PuzzleWorldBoard board;
        private PuzzleArtwork art;
        private PuzzleEffectTimeline timeline;
        private LevelRuntimeState visual, final;
        private bool[] applied;
        private bool matched, transformed, materials;
        private float elapsed;
        internal bool IsPlaying { get; private set; }

        internal async UniTask PrepareAsync(LevelRuntimeState before, LevelRuntimeState after, PuzzleArtwork artwork,
            PuzzleEffectTimeline schedule, CancellationToken token)
        {
            Reset(); visual = new LevelRuntimeState(before); final = after; art = artwork; timeline = schedule;
            applied = new bool[timeline.Reactions.Count];
            BuildClips(before);
            await art.PrepareEffectsAsync(clips.SelectMany(clip => clip.Paths), token);
            token.ThrowIfCancellationRequested();
            foreach (Clip clip in clips) clip.Frames = clip.Paths.Select(art.Get).ToArray();
        }

        internal void Begin(PuzzleWorldBoard world)
        {
            board = world; elapsed = 0; IsPlaying = timeline.Duration > 0;
            board.Draw(visual, art);
            foreach (MatchedBlockChange change in timeline.Changes)
            {
                SpriteRenderer image = board.OccupantAt(change.Coordinate);
                if (change.IsConsumed && image != null) matching.Add((image, image.transform.localScale, image.color));
            }
            if (IsPlaying) Paint();
        }

        private void Paint()
        {
            foreach (var item in matching)
            { item.image.transform.localScale = item.scale; item.image.color = item.color; }
            bool changed = false;
            if (!materials && timeline.Combination != null && elapsed >= .06f)
            {
                Clear(visual.CellAt(timeline.Combination.First)); Clear(visual.CellAt(timeline.Combination.Center));
                materials = true; changed = true;
            }
            if (!matched && elapsed >= .12f)
            {
                foreach (MatchedBlockChange change in timeline.Changes)
                {
                    RuntimeCell cell = visual.CellAt(change.Coordinate);
                    if (change.IsConsumed)
                    {
                        cell.Content = change.ResultContent; cell.Color = null; cell.RocketDirection = change.RocketDirection;
                        cell.DustDurability = final.CellAt(change.Coordinate).DustDurability;
                    }
                    cell.CoverDurability = change.CoverAfter;
                    if (change.CoverAfter == 0 && cell.Cover == CoverKind.Web) cell.Cover = null;
                }
                // 생성 파워로 교체된 렌더러에 이전 일반 블록 축척을 다시 씌우지 않는다.
                matching.Clear(); matched = true; changed |= timeline.Changes.Count > 0;
            }
            if (!transformed && timeline.Combination?.IsTransformation == true && elapsed >= .35f)
            {
                foreach (PowerTransformation transformation in timeline.Combination.Transformations)
                {
                    RuntimeCell cell = visual.CellAt(transformation.Coordinate);
                    cell.Content = transformation.Content; cell.Color = null; cell.RocketDirection = transformation.Direction;
                    cell.DustDurability = final.CellAt(transformation.Coordinate).DustDurability;
                }
                transformed = true; changed = true;
            }
            for (int i = 0; i < applied.Length; i++)
            {
                PuzzleEffectTimeline.Reaction reaction = timeline.Reactions[i];
                if (applied[i] || elapsed < reaction.Time) continue;
                applied[i] = true; EffectRecord effect = reaction.Record;
                RuntimeCell cell = visual.CellAt(effect.Target);
                if (effect.Response == DamageResponse.Damage && reaction.BodyIndex.HasValue)
                {
                    visual.Obstacles[reaction.BodyIndex.Value].Durability = effect.DurabilityAfter;
                    if (effect.DurabilityAfter == 0) ClearBody(reaction.BodyIndex.Value);
                    changed = true;
                }
                if (effect.Response == DamageResponse.Charge && reaction.BodyIndex.HasValue)
                {
                    visual.Obstacles[reaction.BodyIndex.Value].Charge = effect.ChargeAfter;
                    changed = true;
                }
                if (effect.Response == DamageResponse.Remove || effect.Response == DamageResponse.Activate)
                { Clear(cell); changed = true; }
                if (effect.CoverBefore != effect.CoverAfter)
                { cell.CoverDurability = effect.CoverAfter; if (effect.CoverAfter == 0) cell.Cover = null; changed = true; }
                if (effect.DustBefore != effect.DustAfter) { cell.DustDurability = effect.DustAfter; changed = true; }
                foreach (int index in effect.RemovedObstacleIndices) { ClearBody(index); changed = true; }
            }
            if (changed) board.Draw(visual, art);
            if (elapsed < .12f)
                foreach (var item in matching)
                {
                    float fade = Mathf.Clamp01(elapsed / .12f);
                    item.image.transform.localScale = item.scale * Mathf.Lerp(1, .65f, fade);
                    Color color = item.color; color.a *= 1 - fade; item.image.color = color;
                }
            for (int i = 0; i < clips.Count; i++)
            {
                Clip clip = clips[i]; PuzzleEffectSprite image = board.EffectAt(i);
                if (elapsed < clip.Start || elapsed >= clip.End) { image.Hide(); continue; }
                float t = Mathf.Clamp01((elapsed - clip.Start) / (clip.End - clip.Start));
                int frame = clip.Sheet ? (int)((elapsed - clip.Start) / .06f) % 4 : Mathf.Min(clip.Frames.Length - 1, (int)(t * clip.Frames.Length));
                float motion = Mathf.Clamp01((elapsed - clip.Start - clip.MotionDelay) / Mathf.Max(.01f, clip.End - clip.Start - clip.MotionDelay));
                if (clip.MotionDelay > 0) frame = elapsed < clip.Start + clip.MotionDelay ? 0 : 1 + Mathf.Min(2, (int)(motion * 3));
                Vector3 position = Vector3.Lerp(clip.From, clip.To, motion) + (clip.FrameOffsets == null ? clip.Offset : clip.FrameOffsets[frame]);
                if (clip.Sheet && clip.From != clip.To) position += Vector3.up * (.25f * Mathf.Sin(t * Mathf.PI));
                image.Paint(clip.Frames[clip.Sheet ? 0 : frame], position, clip.Size, clip.Angle, clip.Sheet, frame, clip.Label);
            }
        }

        private static void Clear(RuntimeCell cell)
        { cell.Content = RuntimeContent.Empty; cell.Color = null; cell.RocketDirection = null; cell.ObstacleIndex = null; }
        private void ClearBody(int index)
        { foreach (RuntimeCell cell in visual.Cells) if (cell.ObstacleIndex == index) Clear(cell); }

        internal bool Tick(float deltaTime)
        {
            if (!IsPlaying) return false;
            elapsed += Mathf.Max(0, deltaTime); Paint();
            if (elapsed < timeline.Duration) return false;
            board.HideEffects(); board.Draw(final, art); IsPlaying = false; return true;
        }
        internal void Reset()
        {
            foreach (var item in matching)
                if (item.image != null) { item.image.transform.localScale = item.scale; item.image.color = item.color; }
            matching.Clear();
            if (board != null) board.HideEffects();
            board = null; art = null; visual = null; final = null; timeline = null; applied = null;
            clips.Clear(); elapsed = 0; matched = false; transformed = false; materials = false; IsPlaying = false;
        }
    }
}
