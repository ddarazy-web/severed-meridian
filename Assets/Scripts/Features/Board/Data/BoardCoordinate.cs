using System;
using UnityEngine;

namespace Board
{
    [Serializable]
    public struct BoardCoordinate : IEquatable<BoardCoordinate>
    {
        [SerializeField] private int row;
        [SerializeField] private int column;

        public int Row => row;
        public int Column => column;

        public BoardCoordinate(int row, int column)
        {
            this.row = row;
            this.column = column;
        }

        public bool Equals(BoardCoordinate other) => row == other.row && column == other.column;
        public override bool Equals(object obj) => obj is BoardCoordinate other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(row, column);

        // 저장 좌표는 0부터, 제작자에게 표시하는 좌표는 1부터 시작한다.
        public override string ToString() => $"(행 {(long)row + 1}, 열 {(long)column + 1})";
    }
}
