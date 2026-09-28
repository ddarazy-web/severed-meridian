using System;
using UnityEngine;

namespace Board
{
    // 칸 사이의 벽과 격자 꼭짓점 사이의 전선 모두 무방향 선분으로 비교한다.
    [Serializable]
    public struct BoardEdge : IEquatable<BoardEdge>
    {
        [SerializeField] private BoardCoordinate a;
        [SerializeField] private BoardCoordinate b;
        public BoardCoordinate A => a;
        public BoardCoordinate B => b;
        public bool IsAdjacent => Math.Abs((long)a.Row - b.Row) + Math.Abs((long)a.Column - b.Column) == 1;

        public BoardEdge(BoardCoordinate a, BoardCoordinate b) { this.a = a; this.b = b; }
        public bool Equals(BoardEdge other) => (a.Equals(other.a) && b.Equals(other.b)) || (a.Equals(other.b) && b.Equals(other.a));
        public override bool Equals(object obj) => obj is BoardEdge other && Equals(other);
        public override int GetHashCode() => a.GetHashCode() ^ b.GetHashCode();
        public override string ToString() => a + " ↔ " + b;
    }
}
