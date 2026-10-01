using System.Collections.Generic;
using Board;
using UnityEngine;

namespace GameScreen
{
    // 한 표시 단계 동안만 점유자의 변환을 소유한다. 규칙 상태에는 접근하지 않는다.
    internal sealed class PuzzleBoardSnapshot
    {
        internal sealed class Image
        {
            internal readonly SpriteRenderer Renderer;
            internal readonly Vector3 Scale, Offset;
            internal readonly Color Color;
            private readonly Vector3 originalPosition;
            private readonly bool originalEnabled;
            private readonly int originalOrder;
            private readonly Transform board;
            private readonly bool temporary;
            private readonly SpriteMask clip;

            internal Image(SpriteRenderer renderer, BoardCoordinate at, Transform board, Vector3 origin, bool temporary = false, SpriteMask clip = null)
            {
                Renderer = renderer; this.board = board;
                this.temporary = temporary;
                this.clip = clip;
                originalPosition = origin; Scale = renderer.transform.localScale; Color = renderer.color;
                originalEnabled = renderer.enabled; originalOrder = renderer.sortingOrder;
                Offset = board.InverseTransformPoint(renderer.transform.parent.TransformPoint(origin)) - PuzzleWorldBoard.CellPosition(at);
            }

            internal void Place(Vector3 position) => Renderer.transform.position = board.TransformPoint(position + Offset);

            internal void Restore()
            {
                if (Renderer == null) return;
                Renderer.transform.localPosition = originalPosition; Renderer.transform.localScale = Scale;
                Renderer.color = Color; Renderer.enabled = originalEnabled; Renderer.sortingOrder = originalOrder;
                if (temporary) { ReleaseClip(); Renderer.gameObject.SetActive(false); }
            }

            internal void Hide()
            {
                Renderer.enabled = false;
                if (temporary) { ReleaseClip(); Renderer.gameObject.SetActive(false); }
            }

            internal void ReleaseClip()
            {
                if (clip == null) return;
                clip.gameObject.SetActive(false); Renderer.maskInteraction = SpriteMaskInteraction.None;
            }
        }

        internal readonly Dictionary<BoardCoordinate, Image> Images = new Dictionary<BoardCoordinate, Image>();
        internal readonly List<Image> Owned = new List<Image>();

        internal void Swap(BoardCoordinate first, BoardCoordinate second)
        {
            Images.TryGetValue(first, out Image a); Images.TryGetValue(second, out Image b);
            Images.Remove(first); Images.Remove(second);
            if (a != null) { Images[second] = a; a.Place(PuzzleWorldBoard.CellPosition(second)); }
            if (b != null) { Images[first] = b; b.Place(PuzzleWorldBoard.CellPosition(first)); }
        }

        internal void Restore()
        {
            foreach (Image image in Owned) image.Restore();
        }
    }
}
