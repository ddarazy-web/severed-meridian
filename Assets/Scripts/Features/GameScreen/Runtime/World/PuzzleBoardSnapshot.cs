using System.Collections.Generic;
using Board;
using UnityEngine;
using UnityEngine.Rendering;

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
                if (temporary) { ReturnTemporary(); return; }
                Renderer.transform.localPosition = originalPosition; Renderer.transform.localScale = Scale;
                Renderer.color = Color; Renderer.enabled = originalEnabled; Renderer.sortingOrder = originalOrder;
            }

            internal void Hide()
            {
                if (temporary) { ReturnTemporary(); return; }
                Renderer.enabled = false;
            }

            private void ReturnTemporary()
            {
                SortingGroup group = Renderer.transform.parent.GetComponent<SortingGroup>();
                group.sortingOrder = 10;
                group.transform.localPosition = Vector3.zero; group.transform.localRotation = Quaternion.identity;
                group.transform.localScale = Vector3.one;
                ReleaseClip(); Renderer.sprite = null; Renderer.enabled = false; Renderer.color = UnityEngine.Color.white;
                Renderer.sortingOrder = 0; Renderer.flipX = Renderer.flipY = false;
                Renderer.transform.localPosition = Vector3.zero; Renderer.transform.localScale = Vector3.one;
                Renderer.transform.localRotation = Quaternion.identity; Renderer.gameObject.SetActive(false);
            }

            internal void ReleaseClip()
            {
                if (clip == null) return;
                clip.gameObject.SetActive(false); Renderer.maskInteraction = SpriteMaskInteraction.None;
                clip.sprite = null; clip.isCustomRangeActive = false; clip.alphaCutoff = .5f;
                clip.frontSortingLayerID = clip.backSortingLayerID = 0; clip.frontSortingOrder = clip.backSortingOrder = 0;
                clip.transform.localPosition = Vector3.zero; clip.transform.localRotation = Quaternion.identity;
                clip.transform.localScale = Vector3.one;
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
